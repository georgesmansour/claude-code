using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class ClientAccountConfiguration : IEntityTypeConfiguration<ClientAccount>
{
    public void Configure(EntityTypeBuilder<ClientAccount> b)
    {
        b.ToTable("client_accounts");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.InvitationId).HasColumnName("invitation_id");
        b.Property(e => e.CreatedBy).HasColumnName("created_by");
        b.Property(e => e.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        b.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
        b.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(256).IsRequired();
        b.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(64);
        b.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(e => e.MustChangePassword).HasColumnName("must_change_password").HasDefaultValue(true);
        b.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        b.HasIndex(e => e.Email).IsUnique();
        b.HasIndex(e => e.InvitationId).IsUnique(); // 1:1 with invitation

        b.HasOne(e => e.Invitation)
         .WithOne(i => i.Client)
         .HasForeignKey<ClientAccount>(e => e.InvitationId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.CreatedByAdmin)
         .WithMany(a => a.ClientAccounts)
         .HasForeignKey(e => e.CreatedBy)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
