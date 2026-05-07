using InvitationPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvitationPlatform.Infrastructure.Data.Configurations;

public class GiftAccountConfiguration : IEntityTypeConfiguration<GiftAccount>
{
    public void Configure(EntityTypeBuilder<GiftAccount> b)
    {
        b.ToTable("gift_accounts");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.SectionId).HasColumnName("section_id");
        b.Property(e => e.OrderIndex).HasColumnName("order_index").HasDefaultValue(0);
        b.Property(e => e.BankName).HasColumnName("bank_name").HasMaxLength(256);
        b.Property(e => e.AccountNumber).HasColumnName("account_number").HasMaxLength(256).IsRequired();
        b.Property(e => e.AccountKind).HasColumnName("account_kind").HasMaxLength(64);
        b.Property(e => e.Notes).HasColumnName("notes");

        b.HasIndex(e => new { e.SectionId, e.OrderIndex });

        b.HasOne(e => e.Section)
         .WithMany(s => s.GiftAccounts)
         .HasForeignKey(e => e.SectionId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
