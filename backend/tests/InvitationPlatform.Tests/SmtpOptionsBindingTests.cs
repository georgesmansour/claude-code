using InvitationPlatform.Api.Services.Email;
using Microsoft.Extensions.Configuration;

namespace InvitationPlatform.Tests;

/// <summary>Guards that the "Smtp" section (including the AuthMode enum and STARTTLS port)
/// binds the way appsettings.json expects.</summary>
public class SmtpOptionsBindingTests
{
    private static SmtpOptions Bind(params (string Key, string Value)[] pairs)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();
        var o = new SmtpOptions();
        cfg.GetSection("Smtp").Bind(o);
        return o;
    }

    [Fact]
    public void Binds_outlook_starttls_basic_settings()
    {
        var o = Bind(
            ("Smtp:Host", "smtp-mail.outlook.com"), ("Smtp:Port", "587"),
            ("Smtp:EnableSsl", "true"), ("Smtp:AuthMode", "Basic"),
            ("Smtp:FromAddress", "noreply@example.com"),
            ("Smtp:NotificationRecipient", "leads@example.com"));

        Assert.True(o.IsConfigured);
        Assert.Equal(587, o.Port);                       // 587 + EnableSsl => STARTTLS
        Assert.Equal(SmtpAuthMode.Basic, o.AuthMode);
        Assert.Equal("leads@example.com", o.NotificationRecipient);
    }

    [Fact]
    public void Binds_oauth2_mode_from_string()
    {
        var o = Bind(("Smtp:Host", "smtp.office365.com"), ("Smtp:AuthMode", "OAuth2"),
                     ("Smtp:TenantId", "t"), ("Smtp:ClientId", "c"), ("Smtp:ClientSecret", "s"));
        Assert.Equal(SmtpAuthMode.OAuth2, o.AuthMode);
    }

    [Fact]
    public void Defaults_to_disabled_when_no_host()
    {
        Assert.False(Bind(("Smtp:Port", "587")).IsConfigured);
    }
}
