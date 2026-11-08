using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace RachelTower.Web.Services;

public sealed record EmailMessage(string To, string Subject, string Body, string? ReplyTo = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
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

/// <summary>Used until SMTP is configured: writes emails to the log so nothing is lost in development.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage m, CancellationToken ct = default)
    {
        logger.LogWarning("SMTP not configured. Email to {To}: {Subject}\n{Body}", m.To, m.Subject, m.Body);
        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> smtp) : IEmailSender
{
    public async Task SendAsync(EmailMessage m, CancellationToken ct = default)
    {
        var s = smtp.Value;
        using var mail = new MailMessage(s.From, m.To) { Subject = m.Subject, Body = m.Body };
        if (m.ReplyTo is not null)
        {
            mail.ReplyToList.Add(new MailAddress(m.ReplyTo));
        }

        using var client = new SmtpClient(s.Host, s.Port)
        {
            EnableSsl = s.EnableSsl,
            Credentials = string.IsNullOrEmpty(s.User) ? null : new NetworkCredential(s.User, s.Password),
        };
        await client.SendMailAsync(mail, ct);
    }
}
