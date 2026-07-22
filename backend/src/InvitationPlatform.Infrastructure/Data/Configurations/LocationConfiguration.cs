using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.ToTable("locations");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.SectionId).HasColumnName("section_id");
        b.Property(e => e.OrderIndex).HasColumnName("order_index").HasDefaultValue(0);
        b.Property(e => e.TimeLabel).HasColumnName("time_label").HasMaxLength(128);
        b.Property(e => e.Label).HasColumnName("label").HasMaxLength(128);
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(512).IsRequired();
        b.Property(e => e.ImageUrl).HasColumnName("image_url");
        b.Property(e => e.Address).HasColumnName("address").HasMaxLength(512);
        b.Property(e => e.MapUrl).HasColumnName("map_url");
        b.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
        b.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(9, 6);

        b.HasIndex(e => new { e.SectionId, e.OrderIndex });

        b.HasOne(e => e.Section)
         .WithMany(s => s.Locations)
         .HasForeignKey(e => e.SectionId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
