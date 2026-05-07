using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class RsvpGuestConfiguration : IEntityTypeConfiguration<RsvpGuest>
{
    public void Configure(EntityTypeBuilder<RsvpGuest> b)
    {
        b.ToTable("rsvp_guests");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.RsvpId).HasColumnName("rsvp_id");
        b.Property(e => e.OrderIndex).HasColumnName("order_index").HasDefaultValue(0);
        b.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(256).IsRequired();
        b.Property(e => e.AgeGroup).HasColumnName("age_group").HasMaxLength(32);
        b.Property(e => e.MealPreference).HasColumnName("meal_preference").HasMaxLength(128);
        b.Property(e => e.DietaryRestrictions).HasColumnName("dietary_restrictions").HasMaxLength(512);

        b.HasIndex(e => new { e.RsvpId, e.OrderIndex });

        b.HasOne(e => e.Rsvp)
         .WithMany(r => r.Guests)
         .HasForeignKey(e => e.RsvpId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
