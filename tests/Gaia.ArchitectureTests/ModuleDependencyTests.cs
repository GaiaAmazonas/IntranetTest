using Gaia.Modules.Identity;
using Gaia.Modules.Inventory;
using Gaia.Modules.Organization;
using Gaia.Modules.ThirdParties;

namespace Gaia.ArchitectureTests;

public sealed class ModuleDependencyTests
{
    [Fact]
    public void IdentityDoesNotDependOnBusinessModules()
    {
        var references = ReferencedAssembliesOf<IdentityModule>();

        Assert.DoesNotContain(typeof(OrganizationModule).Assembly.GetName().Name!, references);
        Assert.DoesNotContain(typeof(ThirdPartiesModule).Assembly.GetName().Name!, references);
        Assert.DoesNotContain(typeof(InventoryModule).Assembly.GetName().Name!, references);
    }

    [Fact]
    public void OrganizationDoesNotDependOnLaterBusinessModules()
    {
        var references = ReferencedAssembliesOf<OrganizationModule>();

        Assert.DoesNotContain(typeof(ThirdPartiesModule).Assembly.GetName().Name!, references);
        Assert.DoesNotContain(typeof(InventoryModule).Assembly.GetName().Name!, references);
    }

    private static HashSet<string> ReferencedAssembliesOf<T>()
    {
        return typeof(T).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
