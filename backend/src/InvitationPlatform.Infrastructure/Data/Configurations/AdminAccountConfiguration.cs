using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class AdminAccountConfiguration : IEntityTypeConfiguration<AdminAccount>
{
    public void Configure(EntityTypeBuilder<AdminAccount> b)
    {
        b.ToTable("admin_accounts");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        b.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
        b.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(256).IsRequired();
        b.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        b.HasIndex(e => e.Email).IsUnique();
    }
}
