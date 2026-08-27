using Gaia.Modules.Security;

namespace Gaia.ArchitectureTests;

public sealed class SecurityModuleRulesTests
{
    [Fact]
    public void EffectiveActiveIdsExcludesInactiveModulesAndTheirDescendants()
    {
        var root = Guid.NewGuid(); var inactiveChild = Guid.NewGuid(); var grandchild = Guid.NewGuid(); var activeChild = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?> { [root] = null, [inactiveChild] = root, [grandchild] = inactiveChild, [activeChild] = root };
        var active = new HashSet<Guid> { root, grandchild, activeChild };

        var result = SecurityModuleRules.EffectiveActiveIds(parents, active);

        Assert.Contains(root, result);
        Assert.Contains(activeChild, result);
        Assert.DoesNotContain(inactiveChild, result);
        Assert.DoesNotContain(grandchild, result);
    }

    [Fact]
    public void EffectiveActiveIdsRejectsOrphansAndCycles()
    {
        var orphan = Guid.NewGuid(); var missingParent = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?> { [orphan] = missingParent, [first] = second, [second] = first };
        var active = new HashSet<Guid> { orphan, first, second };

        var result = SecurityModuleRules.EffectiveActiveIds(parents, active);

        Assert.Empty(result);
    }

    [Fact]
    public void ValidateHierarchyAllowsMovingToAnotherBranch()
    {
        var module = Guid.NewGuid(); var previousParent = Guid.NewGuid(); var newParent = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?> { [module] = previousParent, [previousParent] = null, [newParent] = null };
        SecurityModuleRules.ValidateHierarchy(module, newParent, parents);
    }

    [Fact]
    public void ValidateHierarchyRejectsSelfAsParent()
    {
        var module = Guid.NewGuid();
        Assert.Throws<SecurityModuleValidationException>(() => SecurityModuleRules.ValidateHierarchy(module, module, new Dictionary<Guid, Guid?>()));
    }

    [Fact]
    public void ValidateHierarchyRejectsDescendantAsParent()
    {
        var root = Guid.NewGuid(); var child = Guid.NewGuid(); var grandchild = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?> { [root] = null, [child] = root, [grandchild] = child };
        Assert.Throws<SecurityModuleValidationException>(() => SecurityModuleRules.ValidateHierarchy(root, grandchild, parents));
    }
}
