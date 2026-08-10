using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace InvitationPlatform.Api.Services.Email;

/// <summary>How the app authenticates to the SMTP server.</summary>
public enum SmtpAuthMode
{
    /// <summary>Username + password (or an app password). Simple, but Microsoft has disabled it
    /// for many Outlook/Microsoft 365 accounts in favour of Modern Auth.</summary>
    Basic,
    /// <summary>OAuth2 / Modern Auth (SASL XOAUTH2) — what Outlook and Microsoft 365 now require.</summary>
    OAuth2,
    /// <summary>No AUTH at all (internal relay / local MTA that trusts the host).</summary>
    None
}

/// <summary>SMTP settings, bound from the "Smtp" configuration section.</summary>
public class SmtpOptions
{
    public string Host { get; set; } = "";
    /// <summary>587 = STARTTLS (the usual choice), 465 = implicit TLS.</summary>
    public int Port { get; set; } = 587;
    /// <summary>Use TLS. On port 587 this means STARTTLS; on 465, implicit SSL.</summary>
    public bool EnableSsl { get; set; } = true;

    public SmtpAuthMode AuthMode { get; set; } = SmtpAuthMode.Basic;

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    // ── OAuth2 / Modern Auth (client-credentials flow) ────────────────────────
    // Requires an Entra ID (Azure AD) app registration with the
    // https://outlook.office365.com/.default application permission, admin-consented,
    // and the service principal granted SMTP.SendAsApp on the mailbox.
    // NOTE: this flow works for Microsoft 365 / Exchange Online mailboxes. Personal
    // outlook.com accounts cannot use client credentials — see docs/EMAIL.md.
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    /// <summary>Mailbox to authenticate as. Falls back to Username, then FromAddress.</summary>
    public string OAuthUser { get; set; } = "";

    /// <summary>Envelope sender. Falls back to Username when blank.</summary>
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Invitation Platform";

    /// <summary>
    /// Internal inbox that receives new demo-request notifications. Deliberately separate from the
    /// landing page's public contact email, so the address you monitor need never be published.
    /// Blank = fall back to the public contact email.
    /// </summary>
    public string NotificationRecipient { get; set; } = "";

    /// <summary>Sending is only attempted when a host is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

public interface IEmailSender
{
    /// <summary>Sends a plain-text email. Throws on failure so callers can record the reason.</summary>
    Task SendAsync(string to, string subject, string body, string? replyTo = null, CancellationToken ct = default);

    bool IsConfigured { get; }
}

/// <summary>
/// Sends mail over SMTP using MailKit.
///
/// MailKit (not System.Net.Mail) is required because Outlook / Microsoft 365 mandate
/// OAuth2 "Modern Auth" (SASL XOAUTH2), which the framework's SmtpClient cannot do.
/// Both Basic and OAuth2 are supported so the same code works against any provider.
/// </summary>
public class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<SmtpEmailSender> log) : IEmailSender
{
    private readonly SmtpOptions _o = options.Value;

    public bool IsConfigured => _o.IsConfigured;

    public async Task SendAsync(string to, string subject, string body, string? replyTo = null, CancellationToken ct = default)
    {
        if (!_o.IsConfigured)
            throw new InvalidOperationException("SMTP is not configured (Smtp:Host is empty).");

        var from = FirstNonEmpty(_o.FromAddress, _o.Username)
            ?? throw new InvalidOperationException("Smtp:FromAddress (or Smtp:Username) must be set.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_o.FromName, from));
        message.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrWhiteSpace(replyTo)) message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        // Port 587 -> STARTTLS (upgrade the plain connection); 465 -> implicit TLS.
        var security = !_o.EnableSsl ? SecureSocketOptions.None
            : _o.Port == 465 ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        await client.ConnectAsync(_o.Host, _o.Port, security, ct);
        try
        {
            switch (_o.AuthMode)
            {
                case SmtpAuthMode.OAuth2:
                    var user = FirstNonEmpty(_o.OAuthUser, _o.Username, _o.FromAddress)
                        ?? throw new InvalidOperationException("Smtp:OAuthUser (or Username) must be set for OAuth2.");
                    var token = await AcquireAccessTokenAsync(ct);
                    await client.AuthenticateAsync(new SaslMechanismOAuth2(user, token), ct);
                    break;

                case SmtpAuthMode.Basic:
                    if (!string.IsNullOrWhiteSpace(_o.Username))
                        await client.AuthenticateAsync(_o.Username, _o.Password, ct);
                    break;

                case SmtpAuthMode.None:
                    break;
            }

            await client.SendAsync(message, ct);
            log.LogInformation("Sent notification email to {To} ({Subject})", to, subject);
        }
        finally
        {
            await client.DisconnectAsync(true, ct);
        }
    }

    /// <summary>
    /// Gets an Exchange Online access token using the OAuth2 client-credentials flow.
    /// </summary>
    private async Task<string> AcquireAccessTokenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_o.TenantId) || string.IsNullOrWhiteSpace(_o.ClientId) ||
            string.IsNullOrWhiteSpace(_o.ClientSecret))
            throw new InvalidOperationException(
                "OAuth2 requires Smtp:TenantId, Smtp:ClientId and Smtp:ClientSecret. See docs/EMAIL.md.");

        using var http = httpClientFactory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _o.ClientId,
            ["client_secret"] = _o.ClientSecret,
            ["scope"] = "https://outlook.office365.com/.default",
            ["grant_type"] = "client_credentials"
        });

        var url = $"https://login.microsoftonline.com/{_o.TenantId}/oauth2/v2.0/token";
        using var res = await http.PostAsync(url, form, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Could not obtain an OAuth2 token ({(int)res.StatusCode}): {payload}");

        using var doc = System.Text.Json.JsonDocument.Parse(payload);
        return doc.RootElement.TryGetProperty("access_token", out var t)
            ? t.GetString()!
            : throw new InvalidOperationException("OAuth2 token response did not contain an access_token.");
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
