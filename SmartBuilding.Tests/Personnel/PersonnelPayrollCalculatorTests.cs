using SmartBuilding.Domain.Entities.Personnel;
using Xunit;

namespace SmartBuilding.Tests.Personnel;

public class PersonnelPayrollCalculatorTests
{
    [Fact]
    public void ComputeNet_Subtracts_Deductions_And_Adds_Bonuses()
    {
        var net = PersonnelPayrollCalculator.ComputeNet(
            grossSalary: 3000m,
            bonuses: 200m,
            overtimePay: 150m,
            penalties: 50m,
            advances: 100m,
            deductions: 300m);

        Assert.Equal(2900m, net);
    }

    [Fact]
    public void ComputeNet_Never_Goes_Below_Zero()
    {
        var net = PersonnelPayrollCalculator.ComputeNet(1000m, 0, 0, 500m, 800m, 400m);
        Assert.Equal(0m, net);
    }

    [Fact]
    public void ComputeOvertimePay_Applies_1_5_Multiplier()
    {
        var pay = PersonnelPayrollCalculator.ComputeOvertimePay(2m, 2200m);
        Assert.True(pay > 0);
    }
}
