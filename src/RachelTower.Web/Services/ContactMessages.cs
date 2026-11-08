using Microsoft.Extensions.Options;
using RachelTower.Web.Infrastructure;

namespace RachelTower.Web.Services;

public sealed record ContactMessage(string Name, string Email, string? Phone, string Topic, string Message, string Culture);

public interface IContactMessageSender
{
    Task SendAsync(ContactMessage message, CancellationToken ct = default);
}

/// <summary>Emails contact form submissions to the hotel's reservations inbox.</summary>
public sealed class ContactMessageSender(IEmailSender email, IOptions<HotelOptions> hotel) : IContactMessageSender
{
    public Task SendAsync(ContactMessage m, CancellationToken ct = default) =>
        email.SendAsync(new EmailMessage(
            To: hotel.Value.Email,
            Subject: $"[Website] {m.Topic}: {m.Name}",
            Body: $"""
                Name: {m.Name}
                Email: {m.Email}
                Phone: {m.Phone ?? "-"}
                Topic: {m.Topic}
                Language: {m.Culture}

                {m.Message}
                """,
            ReplyTo: m.Email), ct);
}
