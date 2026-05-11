using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class RsvpConfiguration : IEntityTypeConfiguration<Rsvp>
{
    public void Configure(EntityTypeBuilder<Rsvp> b)
    {
        b.ToTable("rsvps");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.InvitationId).HasColumnName("invitation_id");
        b.Property(e => e.Response).HasColumnName("response")
         .HasConversion<string>()
         .HasDefaultValue(RsvpResponse.Yes);
        b.Property(e => e.PartySize).HasColumnName("party_size").HasDefaultValue(0);
        b.Property(e => e.ContactName).HasColumnName("contact_name").HasMaxLength(256);
        b.Property(e => e.ContactEmail).HasColumnName("contact_email").HasMaxLength(256);
        b.Property(e => e.ContactPhone).HasColumnName("contact_phone").HasMaxLength(64);
        b.Property(e => e.Message).HasColumnName("message");
        b.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        b.Property(e => e.UserAgent).HasColumnName("user_agent");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(e => e.InvitationId);
        b.HasIndex(e => e.CreatedAt);

        b.HasOne(e => e.Invitation)
         .WithMany(i => i.Rsvps)
         .HasForeignKey(e => e.InvitationId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
