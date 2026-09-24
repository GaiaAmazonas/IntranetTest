using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class HelpdeskAttachmentRulesTests
{
    [Fact]
    public void ManagementFieldResponseRequiresManagement()
    {
        var command=Valid() with{ManagementFieldResponseId=Guid.NewGuid()};
        Assert.Throws<ArgumentException>(()=>HelpdeskAttachmentRules.Validate(command));
    }
    [Fact]
    public void ValidMetadataMatchesApprovedDataverseChoiceValues()
    {
        var command = Valid();
        HelpdeskAttachmentRules.Validate(command);
        Assert.Equal(299540080, (int)AttachmentVisibility.Requester);
        Assert.Equal(299540081, (int)AttachmentVisibility.Internal);
        Assert.Equal(299540090, HelpdeskAttachmentRules.SharePointProvider);
    }

    [Fact]
    public void AttachmentCannotBelongToCommentAndFieldResponseAtOnce()
    {
        var command = Valid() with { CommentId = Guid.NewGuid(), FieldResponseId = Guid.NewGuid() };
        Assert.Throws<ArgumentException>(() => HelpdeskAttachmentRules.Validate(command));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Sha256IsRequiredAndMustHaveCanonicalLength(string? hash)
    {
        var command = Valid() with { File = Valid().File with { Sha256 = hash } };
        Assert.Throws<ArgumentException>(() => HelpdeskAttachmentRules.Validate(command));
    }

    private static PersistHelpdeskAttachment Valid() => new(Guid.NewGuid(), Guid.NewGuid(), null, null,
        Guid.NewGuid(), AttachmentVisibility.Requester,
        new(new("SharePoint", "site", "drive", "item"), "\"etag\"", "evidence.pdf", "technical.pdf",
            "application/pdf", 10, Sha256: new string('a', 64), UploadedAt: DateTimeOffset.UtcNow));
}
