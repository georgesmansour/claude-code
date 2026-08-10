using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class DemoRequestConfiguration : IEntityTypeConfiguration<DemoRequest>
{
    public void Configure(EntityTypeBuilder<DemoRequest> b)
    {
        b.ToTable("demo_requests");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        b.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(64);
        b.Property(e => e.Email).HasColumnName("email").HasMaxLength(256);
        b.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(64);
        b.Property(e => e.Company).HasColumnName("company").HasMaxLength(256);
        b.Property(e => e.Message).HasColumnName("message").HasMaxLength(4000);
        b.Property(e => e.ReadAt).HasColumnName("read_at");
        b.Property(e => e.EmailSentAt).HasColumnName("email_sent_at");
        b.Property(e => e.EmailError).HasColumnName("email_error").HasMaxLength(1024);
        b.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        b.HasIndex(e => e.CreatedAt);
    }
}
