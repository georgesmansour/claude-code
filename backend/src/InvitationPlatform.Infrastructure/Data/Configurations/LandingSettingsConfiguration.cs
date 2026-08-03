using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class LandingSettingsConfiguration : IEntityTypeConfiguration<LandingSettings>
{
    public void Configure(EntityTypeBuilder<LandingSettings> b)
    {
        b.ToTable("landing_settings");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.CompanyEmail).HasColumnName("company_email").HasMaxLength(256);
        b.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(64);
        b.Property(e => e.WhatsAppNumber).HasColumnName("whatsapp_number").HasMaxLength(64);
        b.Property(e => e.CompanyAddress).HasColumnName("company_address").HasMaxLength(512);
        b.Property(e => e.InstagramUrl).HasColumnName("instagram_url").HasMaxLength(512);
        b.Property(e => e.FacebookUrl).HasColumnName("facebook_url").HasMaxLength(512);
        b.Property(e => e.TikTokUrl).HasColumnName("tiktok_url").HasMaxLength(512);
        b.Property(e => e.PinterestUrl).HasColumnName("pinterest_url").HasMaxLength(512);
        b.Property(e => e.MapEmbedUrl).HasColumnName("map_embed_url").HasMaxLength(1024);
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
    }
}
