using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminAccount>       AdminAccounts       { get; set; }
    public DbSet<Template>           Templates           { get; set; }
    public DbSet<Invitation>         Invitations         { get; set; }
    public DbSet<ClientAccount>      ClientAccounts      { get; set; }
    public DbSet<InvitationSection>  InvitationSections  { get; set; }
    public DbSet<Location>           Locations           { get; set; }
    public DbSet<GiftAccount>        GiftAccounts        { get; set; }
    public DbSet<Guest>              Guests              { get; set; }
    public DbSet<Rsvp>               Rsvps               { get; set; }
    public DbSet<RsvpGuest>          RsvpGuests          { get; set; }
    public DbSet<NotificationSetting> NotificationSettings { get; set; }
    public DbSet<AuditLog>           AuditLog            { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
