using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> b)
    {
        b.ToTable("templates");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.CreatedBy).HasColumnName("created_by");
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        b.Property(e => e.Description).HasColumnName("description");
        b.Property(e => e.IsBuiltin).HasColumnName("is_builtin").HasDefaultValue(false);
        b.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(e => e.ThumbnailUrl).HasColumnName("thumbnail_url");
        b.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        b.HasOne(e => e.CreatedByAdmin)
         .WithMany(a => a.Templates)
         .HasForeignKey(e => e.CreatedBy)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
