using System.Globalization;
using System.Text;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using RachelTower.Web.Data;
using RachelTower.Web.Infrastructure;
using RachelTower.Web.Services;

namespace RachelTower.Web.Reservations;

/// <summary>
/// Booking emails: a confirmation or cancellation to the guest (in their language, if they gave
/// an email address) and a notice to the hotel inbox (in the staff language).
/// A failed email never fails the booking itself.
/// </summary>
public sealed class BookingNotifier(
    IEmailSender email,
    IStringLocalizer<SharedResource> L,
    IOptions<HotelOptions> hotel,
    IOptions<BookingOptions> rules,
    ILogger<BookingNotifier> logger)
{
    /// <param name="notifyHotel">False for bookings made by staff, who already know about them.</param>
    public Task BookingCreatedAsync(Booking b, string manageUrl, CancellationToken ct = default, bool notifyHotel = true) =>
        SendBothAsync(b,
            guest: () => Compose(b, "Email.Confirm.Subject", "Email.Confirm.Intro", manageUrl, greet: true),
            staff: notifyHotel ? () => Compose(b, "Email.Staff.NewSubject", "Email.Staff.NewIntro", null, greet: false) : null,
            ct);

    public Task BookingCancelledAsync(Booking b, CancellationToken ct = default) =>
        SendBothAsync(b,
            guest: () => Compose(b, "Email.Cancel.Subject", "Email.Cancel.Intro", null, greet: true),
            staff: () => Compose(b, "Email.Staff.CancelSubject", "Email.Staff.CancelIntro", null, greet: false),
            ct);

    private async Task SendBothAsync(Booking b, Func<(string Subject, string Body)> guest, Func<(string Subject, string Body)>? staff, CancellationToken ct)
    {
        var messages = new List<EmailMessage>();
        if (b.GuestEmail is not null)
        {
            var (subject, body) = InCulture(b.Culture, guest);
            messages.Add(new EmailMessage(b.GuestEmail, subject, body, ReplyTo: hotel.Value.Email));
        }
        if (staff is not null && !string.IsNullOrWhiteSpace(hotel.Value.Email))
        {
            var (subject, body) = InCulture(hotel.Value.StaffLanguage, staff);
            messages.Add(new EmailMessage(hotel.Value.Email, subject, body, ReplyTo: b.GuestEmail));
        }

        foreach (var m in messages)
        {
            try
            {
                await email.SendAsync(m, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not send booking email for {Reference} to {To}", b.Reference, m.To);
            }
        }
    }

    private (string Subject, string Body) Compose(Booking b, string subjectKey, string introKey, string? manageUrl, bool greet)
    {
        var culture = CultureInfo.CurrentUICulture;
        var h = hotel.Value;
        var sep = culture.TwoLetterISOLanguageName == "fr" ? " : " : ": "; // French puts a space before the colon
        var body = new StringBuilder();
        if (greet) body.AppendLine(string.Format(L["Email.Greeting"], b.GuestName)).AppendLine();
        body.AppendLine(L[introKey])
            .AppendLine()
            .AppendLine($"{L["Booking.Reference"]}{sep}{b.Reference}")
            .AppendLine($"{L["Booking.RoomType"]}{sep}{b.RoomType.Name(culture.TwoLetterISOLanguageName)} ({b.Room.Number})")
            .AppendLine($"{L["Book.CheckIn"]}{sep}{b.CheckIn.ToString("D", culture)}")
            .AppendLine($"{L["Book.CheckOut"]}{sep}{b.CheckOut.ToString("D", culture)}")
            .AppendLine($"{L["Booking.Nights"]}{sep}{b.Nights}")
            .AppendLine($"{L["Book.Guests"]}{sep}{b.Guests}")
            .AppendLine($"{L["Booking.Total"]}{sep}{Money.Usd(b.TotalUsd, culture)}")
            .AppendLine($"{L["Booking.Payment"]}{sep}{L[$"Payment.{b.PaymentMethod}"]} ({L[$"PaymentStatus.{b.PaymentStatus}"]})")
            .AppendLine($"{L["Booking.Guest"]}{sep}{b.GuestName}")
            .AppendLine($"{L["Booking.Phone"]}{sep}{b.GuestPhone}");

        if (b.Notes is not null) body.AppendLine($"{L["Booking.Notes"]}{sep}{b.Notes}");

        if (manageUrl is not null)
        {
            if (b.PaymentMethod == PaymentMethod.PayAtDesk && rules.Value.ReleaseUnpaidAtHour is { } hour)
            {
                body.AppendLine().AppendLine(string.Format(L["Booking.PayAtDeskNote"], $"{hour}:00"));
            }
            body.AppendLine().AppendLine(string.Format(L["Email.Manage"], manageUrl));
        }

        body.AppendLine()
            .AppendLine(h.Name)
            .AppendLine(string.Join(" / ", h.Phones))
            .AppendLine(h.WhatsAppUrl());

        return (string.Format(L[subjectKey], b.Reference, b.GuestName, b.CheckIn.ToString("d", culture), b.CheckOut.ToString("d", culture)), body.ToString());
    }

    private static T InCulture<T>(string culture, Func<T> build)
    {
        var previous = CultureInfo.CurrentUICulture;
        var previousFormat = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new CultureInfo(culture);
            return build();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            CultureInfo.CurrentCulture = previousFormat;
        }
    }
}

public static class Money
{
    public static string Usd(decimal amount, CultureInfo? culture = null) =>
        string.Format(culture ?? CultureInfo.CurrentCulture, "{0:N2} USD", amount);
}
