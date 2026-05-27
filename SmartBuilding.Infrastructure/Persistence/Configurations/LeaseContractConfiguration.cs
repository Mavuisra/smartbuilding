using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartBuilding.Domain.Entities.Location;

namespace SmartBuilding.Infrastructure.Persistence.Configurations;

public class LeaseContractConfiguration : IEntityTypeConfiguration<LeaseContract>
{
    public void Configure(EntityTypeBuilder<LeaseContract> builder)
    {
        builder.ToTable("LeaseContracts");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ContractNumber).IsUnique();
        builder.Property(x => x.MonthlyRent).HasPrecision(18, 2);
        builder.Property(x => x.Deposit).HasPrecision(18, 2);
        builder.HasOne(x => x.Premise).WithMany(x => x.LeaseContracts).HasForeignKey(x => x.PremiseId);
        builder.HasOne(x => x.Tenant).WithMany(x => x.LeaseContracts).HasForeignKey(x => x.TenantId);
    }
}
