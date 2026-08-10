using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Api.Services.Email;
using InvitationPlatform.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Tests;

/// <summary>Records what would have been sent, so tests never touch a real SMTP server.</summary>
internal sealed class FakeEmailSender(bool configured = true) : IEmailSender
{
    public bool IsConfigured { get; } = configured;
    public bool ShouldThrow { get; set; }
    public string? To { get; private set; }
    public string? Subject { get; private set; }
    public string? Body { get; private set; }
    public string? ReplyTo { get; private set; }
    public int SendCount { get; private set; }

    public Task SendAsync(string to, string subject, string body, string? replyTo = null, CancellationToken ct = default)
    {
        SendCount++;
        if (ShouldThrow) throw new InvalidOperationException("smtp exploded");
        To = to; Subject = subject; Body = body; ReplyTo = replyTo;
        return Task.CompletedTask;
    }
}

public class DemoRequestTests
{
    /// <summary>SMTP options with an optional dedicated notification inbox.</summary>
    private static Microsoft.Extensions.Options.IOptions<SmtpOptions> Smtp(string? notificationRecipient = null) =>
        Microsoft.Extensions.Options.Options.Create(new SmtpOptions
        {
            Host = "smtp.example.com",
            NotificationRecipient = notificationRecipient ?? ""
        });

    private static void SeedContactEmail(InvitationPlatform.Infrastructure.Data.AppDbContext db, string? email)
    {
        db.LandingSettings.Add(new LandingSettings { CompanyEmail = email, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    private static DemoRequestSubmission Valid(string? email = "vera@example.com", string? phone = null) =>
        new("Vera Haddad", "Wedding", email, phone, "Vera Events", "Looking for a June date.");

    [Fact]
    public async Task Valid_request_is_saved_and_emailed_to_the_configured_address()
    {
        using var db = TestSupport.NewDb();
        SeedContactEmail(db, "hello@company.com");
        var mail = new FakeEmailSender();
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitDemoRequest(Valid(), mail, Smtp(), default);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.DemoRequests.SingleAsync();
        Assert.Equal("Vera Haddad", saved.Name);
        Assert.Equal("Vera Events", saved.Company);
        Assert.NotNull(saved.EmailSentAt);
        Assert.Null(saved.EmailError);

        Assert.Equal("hello@company.com", mail.To);
        Assert.Contains("Vera Haddad", mail.Body);
        Assert.Contains("Vera Events", mail.Body);
        Assert.Equal("vera@example.com", mail.ReplyTo);   // replying goes back to the visitor
    }

    [Fact]
    public async Task Request_is_still_saved_when_sending_fails()
    {
        // A mail outage must never lose a lead — the enquiry is stored and the reason recorded.
        using var db = TestSupport.NewDb();
        SeedContactEmail(db, "hello@company.com");
        var mail = new FakeEmailSender { ShouldThrow = true };
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitDemoRequest(Valid(), mail, Smtp(), default);

        Assert.IsType<OkObjectResult>(result);          // the visitor never sees our mail problems
        var saved = await db.DemoRequests.SingleAsync();
        Assert.Null(saved.EmailSentAt);
        Assert.Contains("smtp exploded", saved.EmailError);
    }

    [Fact]
    public async Task Request_is_saved_when_no_recipient_is_configured()
    {
        using var db = TestSupport.NewDb();
        var mail = new FakeEmailSender();
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitDemoRequest(Valid(), mail, Smtp(), default);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.DemoRequests.SingleAsync();
        Assert.Equal(0, mail.SendCount);
        Assert.Contains("admin panel", saved.EmailError);   // still reachable in the UI
        Assert.Null(saved.ReadAt);                          // and flagged unread
    }

    [Fact]
    public async Task Dedicated_notification_inbox_wins_over_the_public_contact_email()
    {
        // The business should not have to publish the address it wants alerts on.
        using var db = TestSupport.NewDb();
        SeedContactEmail(db, "public@company.com");
        var mail = new FakeEmailSender();
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitDemoRequest(Valid(), mail, Smtp("leads@internal.com"), default);

        Assert.Equal("leads@internal.com", mail.To);
    }

    [Fact]
    public async Task Request_is_saved_unread_when_email_is_not_configured_at_all()
    {
        // The whole point: notifications work with NO email setup — the admin panel shows it.
        using var db = TestSupport.NewDb();
        var mail = new FakeEmailSender(configured: false);
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitDemoRequest(Valid(), mail, Smtp(), default);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.DemoRequests.SingleAsync();
        Assert.Equal(0, mail.SendCount);
        Assert.Null(saved.ReadAt);
        Assert.Contains("admin panel", saved.EmailError);
    }

    [Fact]
    public async Task Phone_only_request_is_accepted()
    {
        using var db = TestSupport.NewDb();
        SeedContactEmail(db, "hello@company.com");
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitDemoRequest(Valid(email: null, phone: "+961 71 234 567"), new FakeEmailSender(), Smtp(), default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await db.DemoRequests.CountAsync());
    }

    [Theory]
    [InlineData(null, "a@b.com", null)]       // no name
    [InlineData("Vera", null, null)]          // neither email nor phone
    [InlineData("Vera", "not-an-email", null)]
    public async Task Invalid_requests_are_rejected_and_not_saved(string? name, string? email, string? phone)
    {
        using var db = TestSupport.NewDb();
        SeedContactEmail(db, "hello@company.com");
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitDemoRequest(
            new DemoRequestSubmission(name, "Wedding", email, phone, null, null), new FakeEmailSender(), Smtp(), default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await db.DemoRequests.CountAsync());
    }
}
