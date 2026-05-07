using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class NotificationSettingConfiguration : IEntityTypeConfiguration<NotificationSetting>
{
    public void Configure(EntityTypeBuilder<NotificationSetting> b)
    {
        b.ToTable("notification_settings");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.ClientId).HasColumnName("client_id");
        b.Property(e => e.EmailOnRsvp).HasColumnName("email_on_rsvp").HasDefaultValue(true);
        b.Property(e => e.SmsOnRsvp).HasColumnName("sms_on_rsvp").HasDefaultValue(false);
        b.Property(e => e.DailySummary).HasColumnName("daily_summary").HasDefaultValue(false);

        b.HasIndex(e => e.ClientId).IsUnique();

        b.HasOne(e => e.Client)
         .WithOne(c => c.NotificationSetting)
         .HasForeignKey<NotificationSetting>(e => e.ClientId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
