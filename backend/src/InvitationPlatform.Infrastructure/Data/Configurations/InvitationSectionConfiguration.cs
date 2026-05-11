using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class InvitationSectionConfiguration : IEntityTypeConfiguration<InvitationSection>
{
    public void Configure(EntityTypeBuilder<InvitationSection> b)
    {
        b.ToTable("invitation_sections");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.InvitationId).HasColumnName("invitation_id");
        b.Property(e => e.Type).HasColumnName("type")
         .HasConversion<string>()
         .HasDefaultValue(SectionType.Cover);
        b.Property(e => e.Enabled).HasColumnName("enabled").HasDefaultValue(true);
        b.Property(e => e.OrderIndex).HasColumnName("order_index").HasDefaultValue(0);
        b.Property(e => e.Config).HasColumnName("config").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        b.HasIndex(e => new { e.InvitationId, e.OrderIndex });

        b.HasOne(e => e.Invitation)
         .WithMany(i => i.Sections)
         .HasForeignKey(e => e.InvitationId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
