using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using RachelTower.Web.Infrastructure;

namespace RachelTower.Web.Services;

public sealed record ContactMessage(string Name, string Email, string? Phone, string Topic, string Message, string Culture);

public interface IContactMessageSender
{
    Task SendAsync(ContactMessage message, CancellationToken ct = default);
}

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>Used until SMTP is configured: writes the message to the log so nothing is lost in development.</summary>
public sealed class LoggingContactMessageSender(ILogger<LoggingContactMessageSender> logger) : IContactMessageSender
{
    public Task SendAsync(ContactMessage m, CancellationToken ct = default)
    {
        logger.LogWarning(
            "SMTP not configured. Contact message from {Name} <{Email}> ({Phone}) [{Topic}, {Culture}]: {Message}",
            m.Name, m.Email, m.Phone, m.Topic, m.Culture, m.Message);
        return Task.CompletedTask;
    }
}

/// <summary>Emails contact form submissions to the hotel's reservations inbox.</summary>
public sealed class SmtpContactMessageSender(IOptions<SmtpOptions> smtp, IOptions<HotelOptions> hotel) : IContactMessageSender
{
    public async Task SendAsync(ContactMessage m, CancellationToken ct = default)
    {
        var s = smtp.Value;
        using var mail = new MailMessage(s.From, hotel.Value.Email)
        {
            Subject = $"[Website] {m.Topic}: {m.Name}",
            Body = $"""
                Name: {m.Name}
                Email: {m.Email}
                Phone: {m.Phone ?? "-"}
                Topic: {m.Topic}
                Language: {m.Culture}

                {m.Message}
                """,
        };
        mail.ReplyToList.Add(new MailAddress(m.Email, m.Name));

        using var client = new SmtpClient(s.Host, s.Port)
        {
            EnableSsl = s.EnableSsl,
            Credentials = string.IsNullOrEmpty(s.User) ? null : new NetworkCredential(s.User, s.Password),
        };
        await client.SendMailAsync(mail, ct);
    }
}
