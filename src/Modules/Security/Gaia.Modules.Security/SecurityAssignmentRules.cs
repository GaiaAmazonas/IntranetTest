namespace Gaia.Modules.Security;

public static class SecurityAssignmentRules
{
    public static void ValidatePeriod(DateOnly startDate, DateOnly? endDate)
    {
        if (endDate.HasValue && endDate.Value < startDate)
            throw new SecurityAssignmentValidationException("La fecha final no puede ser anterior a la fecha inicial.");
    }

    public static bool Overlaps(DateOnly firstStart, DateOnly? firstEnd, DateOnly secondStart, DateOnly? secondEnd)
    {
        var firstLimit = firstEnd ?? DateOnly.MaxValue;
        var secondLimit = secondEnd ?? DateOnly.MaxValue;
        return firstStart <= secondLimit && secondStart <= firstLimit;
    }

    public static bool AreRedundantBaseRoles(string firstCode, string secondCode) =>
        !string.Equals(firstCode, secondCode, StringComparison.OrdinalIgnoreCase) &&
        new[] { firstCode, secondCode }.All(code => code is not null &&
            (code.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) || code.Equals("CONSULTA", StringComparison.OrdinalIgnoreCase)));
}

public sealed class SecurityAssignmentValidationException(string message) : InvalidOperationException(message);
public sealed class SecurityAssignmentConflictException(string message) : InvalidOperationException(message);
public sealed class SecurityRoleValidationException(string message) : InvalidOperationException(message);
public sealed class SecurityRoleConflictException(string message) : InvalidOperationException(message);
