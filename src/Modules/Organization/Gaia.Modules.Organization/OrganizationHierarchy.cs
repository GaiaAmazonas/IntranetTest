using Gaia.Modules.Organization.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.Organization;

public static class OrganizationHierarchy
{
    public static async Task<int> CalculateLevelAsync(
        Guid? parentId,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        if (!parentId.HasValue)
        {
            return 0;
        }

        var parentLevel = await context.Units
            .Where(unit => unit.Id == parentId)
            .Select(unit => (int?)unit.Level)
            .SingleAsync(cancellationToken);
        return (parentLevel
            ?? throw new InvalidOperationException("La unidad padre no existe.")) + 1;
    }

    public static async Task<bool> WouldCreateCycleAsync(
        Guid unitId,
        Guid proposedParentId,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var parents = await context.Units
            .AsNoTracking()
            .ToDictionaryAsync(
                unit => unit.Id,
                unit => unit.ParentId,
                cancellationToken);
        return WouldCreateCycle(unitId, proposedParentId, parents);
    }

    public static bool WouldCreateCycle(
        Guid unitId,
        Guid proposedParentId,
        IReadOnlyDictionary<Guid, Guid?> parents)
    {
        var cursor = (Guid?)proposedParentId;
        var visited = new HashSet<Guid>();

        while (cursor.HasValue)
        {
            if (cursor.Value == unitId || !visited.Add(cursor.Value))
            {
                return true;
            }

            cursor = parents.GetValueOrDefault(cursor.Value);
        }

        return false;
    }

    public static async Task RecalculateDescendantLevelsAsync(
        OrganizationalUnit root,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var currentIds = new[] { root.Id };
        var level = root.Level + 1;

        while (currentIds.Length > 0)
        {
            var children = await context.Units
                .Where(unit => unit.ParentId.HasValue && currentIds.Contains(unit.ParentId.Value))
                .ToListAsync(cancellationToken);

            foreach (var child in children)
            {
                child.Level = level;
            }

            currentIds = children.Select(child => child.Id).ToArray();
            level++;
        }
    }
}
