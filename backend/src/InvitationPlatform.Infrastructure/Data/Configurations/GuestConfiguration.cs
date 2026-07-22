using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> b)
    {
        b.ToTable("guests");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.InvitationId).HasColumnName("invitation_id");
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        b.Property(e => e.MaxAttendees).HasColumnName("max_attendees").HasDefaultValue(1);
        b.Property(e => e.SelectedAttendees).HasColumnName("selected_attendees").HasDefaultValue(0);
        b.Property(e => e.Status).HasColumnName("status")
         .HasConversion<string>()
         .HasMaxLength(32)
         .HasDefaultValue(GuestRsvpStatus.Pending);
        b.Property(e => e.Token).HasColumnName("token").HasMaxLength(64).IsRequired();
        // Slug uniqueness is enforced in application code, so the index is non-unique:
        // adding a UNIQUE constraint would collide on the empty-string default of pre-existing rows.
        b.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(160).IsRequired().HasDefaultValue("");
        b.Property(e => e.RespondedAt).HasColumnName("responded_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        b.HasIndex(e => e.Token).IsUnique();
        b.HasIndex(e => e.Slug);
        b.HasIndex(e => new { e.InvitationId, e.Name });

        b.HasOne(e => e.Invitation)
         .WithMany()
         .HasForeignKey(e => e.InvitationId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
