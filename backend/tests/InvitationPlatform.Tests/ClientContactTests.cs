using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Tests;

public class ContactHelperTests
{
    [Theory]
    [InlineData("+961 71 234 567", "+96171234567")]
    [InlineData("03-123-456", "03123456")]
    [InlineData("(03) 123 456", "03123456")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    [InlineData("no-digits", null)]
    public void NormalizePhone_strips_formatting(string? input, string? expected)
        => Assert.Equal(expected, ContactHelper.NormalizePhone(input));

    [Theory]
    [InlineData("  Foo@Bar.COM ", "foo@bar.com")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeEmail_trims_and_lowercases(string? input, string? expected)
        => Assert.Equal(expected, ContactHelper.NormalizeEmail(input));

    [Theory]
    [InlineData("a@b.com", true)]
    [InlineData("+96171234567", false)]
    [InlineData("70123456", false)]
    public void LooksLikeEmail_detects_the_at_sign(string value, bool expected)
        => Assert.Equal(expected, ContactHelper.LooksLikeEmail(value));
}

public class ClientContactApiTests
{
    private static (Guid invId, Guid adminId) Seed(InvitationPlatform.Infrastructure.Data.AppDbContext db)
    {
        var admin = TestSupport.SeedAdmin(db, "boss@example.com", "x", isSuperAdmin: true);
        var inv = TestSupport.SeedInvitation(db);
        return (inv.Id, admin.Id);
    }

    [Fact]
    public async Task CreateClient_rejects_when_neither_email_nor_phone()
    {
        using var db = TestSupport.NewDb();
        var (invId, adminId) = Seed(db);
        var ctrl = TestSupport.NewAdminController(db, adminId);

        var result = await ctrl.CreateClient(new CreateClientRequest(invId, null, "secret1", "Couple", null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateClient_accepts_phone_only()
    {
        using var db = TestSupport.NewDb();
        var (invId, adminId) = Seed(db);
        var ctrl = TestSupport.NewAdminController(db, adminId);

        var result = await ctrl.CreateClient(new CreateClientRequest(invId, null, "secret1", "Couple", "+961 71 234 567"));

        Assert.IsType<OkObjectResult>(result);
        var client = await db.ClientAccounts.SingleAsync();
        Assert.Null(client.Email);
        Assert.Equal("+96171234567", client.Phone);   // stored normalised
    }

    [Fact]
    public async Task CreateClient_accepts_email_only()
    {
        using var db = TestSupport.NewDb();
        var (invId, adminId) = Seed(db);
        var ctrl = TestSupport.NewAdminController(db, adminId);

        var result = await ctrl.CreateClient(new CreateClientRequest(invId, "Couple@Example.com", "secret1", "Couple", null));

        Assert.IsType<OkObjectResult>(result);
        var client = await db.ClientAccounts.SingleAsync();
        Assert.Equal("couple@example.com", client.Email);
        Assert.Null(client.Phone);
    }

    [Fact]
    public async Task Login_works_by_email_and_by_phone()
    {
        using var db = TestSupport.NewDb();
        var (invId, adminId) = Seed(db);
        var admin = TestSupport.NewAdminController(db, adminId);
        await admin.CreateClient(new CreateClientRequest(invId, "couple@example.com", "secret1", "Couple", "+961 71 234 567"));

        var auth = TestSupport.NewAuthController(db);

        var byEmail = await auth.ClientLogin(new LoginRequest("couple@example.com", "secret1"));
        Assert.IsType<OkObjectResult>(byEmail);

        // Phone typed with different spacing must still resolve (normalised match).
        var byPhone = await auth.ClientLogin(new LoginRequest("+961-71-234-567", "secret1"));
        Assert.IsType<OkObjectResult>(byPhone);

        var wrong = await auth.ClientLogin(new LoginRequest("couple@example.com", "nope"));
        Assert.IsType<UnauthorizedObjectResult>(wrong);
    }
}
