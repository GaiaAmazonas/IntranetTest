namespace Gaia.Modules.Security;

public static class SecurityModuleRules
{
    public static IReadOnlySet<Guid> EffectiveActiveIds(
        IReadOnlyDictionary<Guid, Guid?> parents,
        IReadOnlySet<Guid> activeIds)
    {
        var result = new HashSet<Guid>();
        foreach (var id in activeIds)
        {
            var visited = new HashSet<Guid>();
            Guid? current = id;
            var effective = true;
            while (current.HasValue)
            {
                if (!visited.Add(current.Value) || !activeIds.Contains(current.Value))
                {
                    effective = false;
                    break;
                }

                if (!parents.TryGetValue(current.Value, out current))
                {
                    effective = false;
                    break;
                }
            }

            if (effective) result.Add(id);
        }

        return result;
    }

    public static void ValidateHierarchy(Guid? id, Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        if (!parentId.HasValue) return;
        if (id.HasValue && parentId.Value == id.Value)
            throw new SecurityModuleValidationException("Un elemento no puede ser su propio padre.");

        var visited = new HashSet<Guid>();
        Guid? current = parentId;
        while (current.HasValue)
        {
            if (!visited.Add(current.Value))
                throw new SecurityModuleValidationException("La jerarquía existente contiene un ciclo y debe corregirse.");
            if (id.HasValue && current.Value == id.Value)
                throw new SecurityModuleValidationException("El padre seleccionado convertiría la jerarquía en un ciclo.");
            current = parents.GetValueOrDefault(current.Value);
        }
    }
}

public sealed class SecurityModuleValidationException(string message) : InvalidOperationException(message);
public sealed class SecurityModuleConflictException(string message) : InvalidOperationException(message);
