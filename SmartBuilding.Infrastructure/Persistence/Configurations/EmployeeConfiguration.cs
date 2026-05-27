using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartBuilding.Domain.Entities.Personnel;

namespace SmartBuilding.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Matricule).IsUnique();
        builder.Property(x => x.Matricule).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BaseSalary).HasPrecision(18, 2);
        builder.Property(x => x.ContractNumber).HasMaxLength(50);
        builder.Property(x => x.ContractType).HasMaxLength(30);
        builder.Property(x => x.Gender).HasMaxLength(20);
        builder.Property(x => x.NationalId).HasMaxLength(50);
    }
}
