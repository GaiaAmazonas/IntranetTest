using Gaia.Modules.Security;
using System.Globalization;

namespace Gaia.ArchitectureTests;

public sealed class SecurityAssignmentRulesTests
{
    [Theory]
    [InlineData("2026-01-01", "2026-06-30", "2026-06-30", null, true)]
    [InlineData("2026-01-01", "2026-06-30", "2026-07-01", null, false)]
    [InlineData("2026-01-01", null, "2027-01-01", null, true)]
    [InlineData("2027-01-01", null, "2026-01-01", "2026-12-31", false)]
    public void OverlapsEvaluatesClosedDateIntervals(string firstStart, string? firstEnd,
        string secondStart, string? secondEnd, bool expected)
    {
        var result = SecurityAssignmentRules.Overlaps(
            DateOnly.Parse(firstStart, CultureInfo.InvariantCulture), Parse(firstEnd),
            DateOnly.Parse(secondStart, CultureInfo.InvariantCulture), Parse(secondEnd));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidatePeriodRejectsEndBeforeStart()
    {
        var exception = Assert.Throws<SecurityAssignmentValidationException>(() =>
            SecurityAssignmentRules.ValidatePeriod(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 13)));

        Assert.Contains("fecha final", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePeriodAllowsOpenAndHistoricalPeriods()
    {
        SecurityAssignmentRules.ValidatePeriod(new DateOnly(2026, 1, 1), null);
        SecurityAssignmentRules.ValidatePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
    }

    [Theory]
    [InlineData("ADMIN", "CONSULTA", true)]
    [InlineData("CONSULTA", "ADMIN", true)]
    [InlineData("ADMIN", "ADMIN", false)]
    [InlineData("CONSULTA", "TALENTO", false)]
    public void BaseRolesOnlyBlockRedundantAdminConsultaCombination(string first, string second, bool expected) =>
        Assert.Equal(expected, SecurityAssignmentRules.AreRedundantBaseRoles(first, second));

    private static DateOnly? Parse(string? value) =>
        value is null ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);
}
