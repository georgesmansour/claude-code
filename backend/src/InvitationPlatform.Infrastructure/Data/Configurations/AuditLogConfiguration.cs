using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(e => e.AdminId).HasColumnName("admin_id");
        b.Property(e => e.InvitationId).HasColumnName("invitation_id");
        b.Property(e => e.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        b.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(64);
        b.Property(e => e.EntityId).HasColumnName("entity_id");
        b.Property(e => e.Details).HasColumnName("details").HasColumnType("jsonb");
        b.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(e => new { e.AdminId, e.CreatedAt });
        b.HasIndex(e => new { e.EntityType, e.EntityId });

        b.HasOne(e => e.Admin)
         .WithMany(a => a.AuditLogs)
         .HasForeignKey(e => e.AdminId)
         .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(e => e.Invitation)
         .WithMany(i => i.AuditLogs)
         .HasForeignKey(e => e.InvitationId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}
