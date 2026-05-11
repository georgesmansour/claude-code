using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("invitations");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.CreatedBy).HasColumnName("created_by");
        b.Property(e => e.TemplateId).HasColumnName("template_id");
        b.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(256).IsRequired();
        b.Property(e => e.Title).HasColumnName("title").HasMaxLength(512).IsRequired();
        b.Property(e => e.Status).HasColumnName("status")
         .HasConversion<string>()
         .HasDefaultValue(InvitationStatus.Draft);
        b.Property(e => e.PublicToken).HasColumnName("public_token").HasMaxLength(128).IsRequired();
        b.Property(e => e.PasswordHash).HasColumnName("password_hash");
        b.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(128);
        b.Property(e => e.EventDate).HasColumnName("event_date");
        b.Property(e => e.MaxAttendees).HasColumnName("max_attendees").HasDefaultValue(10);
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        b.Property(e => e.PublishedAt).HasColumnName("published_at");

        b.HasIndex(e => e.Slug).IsUnique();
        b.HasIndex(e => e.PublicToken).IsUnique();
        b.HasIndex(e => e.Status);
        b.HasIndex(e => e.EventDate);

        b.HasOne(e => e.CreatedByAdmin)
         .WithMany(a => a.Invitations)
         .HasForeignKey(e => e.CreatedBy)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(e => e.Template)
         .WithMany(t => t.Invitations)
         .HasForeignKey(e => e.TemplateId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}
