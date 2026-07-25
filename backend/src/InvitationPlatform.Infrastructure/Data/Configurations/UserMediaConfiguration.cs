using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class UserMediaConfiguration : IEntityTypeConfiguration<UserMedia>
{
    public void Configure(EntityTypeBuilder<UserMedia> b)
    {
        b.ToTable("user_media");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.InvitationId).HasColumnName("invitation_id");
        b.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(512).IsRequired();
        b.Property(e => e.StoredFileName).HasColumnName("stored_file_name").HasMaxLength(160).IsRequired();
        b.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(128).IsRequired();
        b.Property(e => e.ByteSize).HasColumnName("byte_size");
        b.Property(e => e.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        b.Property(e => e.Width).HasColumnName("width");
        b.Property(e => e.Height).HasColumnName("height");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(e => e.InvitationId);
        // Same bytes uploaded twice for one invitation are de-duplicated.
        b.HasIndex(e => new { e.InvitationId, e.ContentHash }).IsUnique();

        b.HasOne(e => e.Invitation)
         .WithMany()
         .HasForeignKey(e => e.InvitationId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
