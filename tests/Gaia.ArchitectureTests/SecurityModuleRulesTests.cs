using Gaia.Modules.Security;

namespace Gaia.ArchitectureTests;

public sealed class SecurityModuleRulesTests
{
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
