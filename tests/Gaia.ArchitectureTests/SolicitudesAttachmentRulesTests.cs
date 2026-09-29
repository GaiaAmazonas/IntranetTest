using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Solicitudes;

namespace Gaia.ArchitectureTests;

public sealed class SolicitudesAttachmentRulesTests
{
    [Fact]
    public void ManagementFieldResponseRequiresManagement()
    {
        var command=Valid() with{ManagementFieldResponseId=Guid.NewGuid()};
        Assert.Throws<ArgumentException>(()=>SolicitudesAttachmentRules.Validate(command));
    }
    [Fact]
    public void ValidMetadataMatchesApprovedDataverseChoiceValues()
    {
        var command = Valid();
        SolicitudesAttachmentRules.Validate(command);
        Assert.Equal(299540080, (int)AttachmentVisibility.Requester);
        Assert.Equal(299540081, (int)AttachmentVisibility.Internal);
        Assert.Equal(299540090, SolicitudesAttachmentRules.SharePointProvider);
    }

    [Fact]
    public void AttachmentCannotBelongToCommentAndFieldResponseAtOnce()
    {
        var command = Valid() with { CommentId = Guid.NewGuid(), FieldResponseId = Guid.NewGuid() };
        Assert.Throws<ArgumentException>(() => SolicitudesAttachmentRules.Validate(command));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Sha256IsRequiredAndMustHaveCanonicalLength(string? hash)
    {
        var command = Valid() with { File = Valid().File with { Sha256 = hash } };
        Assert.Throws<ArgumentException>(() => SolicitudesAttachmentRules.Validate(command));
    }

    private static PersistSolicitudesAttachment Valid() => new(Guid.NewGuid(), Guid.NewGuid(), null, null,
        Guid.NewGuid(), AttachmentVisibility.Requester,
        new(new("SharePoint", "site", "drive", "item"), "\"etag\"", "evidence.pdf", "technical.pdf",
            "application/pdf", 10, Sha256: new string('a', 64), UploadedAt: DateTimeOffset.UtcNow));
}
