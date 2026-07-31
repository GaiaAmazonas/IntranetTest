using Gaia.Modules.Organization;

namespace Gaia.ArchitectureTests;

public sealed class OrganizationHierarchyTests
{
    [Fact]
    public void MovingUnitBelowItsDescendantCreatesCycle()
    {
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        var grandchild = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?>
        {
            [root] = null,
            [child] = root,
            [grandchild] = child
        };

        var createsCycle = OrganizationHierarchy.WouldCreateCycle(
            root,
            grandchild,
            parents);

        Assert.True(createsCycle);
    }

    [Fact]
    public void MovingUnitToDifferentBranchDoesNotCreateCycle()
    {
        var firstRoot = Guid.NewGuid();
        var secondRoot = Guid.NewGuid();
        var child = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?>
        {
            [firstRoot] = null,
            [secondRoot] = null,
            [child] = firstRoot
        };

        var createsCycle = OrganizationHierarchy.WouldCreateCycle(
            child,
            secondRoot,
            parents);

        Assert.False(createsCycle);
    }

    [Fact]
    public void CorruptedExistingHierarchyIsDetected()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?>
        {
            [first] = second,
            [second] = first,
            [candidate] = null
        };

        var createsCycle = OrganizationHierarchy.WouldCreateCycle(
            candidate,
            first,
            parents);

        Assert.True(createsCycle);
    }
}
