using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Solicitudes;

namespace Gaia.ArchitectureTests;

public sealed class SolicitudesAttachmentRulesTests
{
    [Theory]
    [InlineData("documento.pdf", "application/pdf")]
    [InlineData("imagen.png", "image/png")]
    [InlineData("foto.jpeg", "image/jpeg")]
    [InlineData("informe.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("matriz.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("presentacion.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    public void SolicitudesAcceptsApprovedDocumentTypes(string name,string contentType) =>
        SolicitudesFilePolicy.Validate(name,contentType);

    [Theory]
    [InlineData("programa.exe", "application/octet-stream")]
    [InlineData("documento.pdf", "application/octet-stream")]
    [InlineData("video.mp4", "video/mp4")]
    public void SolicitudesRejectsUnapprovedOrMismatchedTypes(string name,string contentType) =>
        Assert.Throws<ArgumentException>(()=>SolicitudesFilePolicy.Validate(name,contentType));

    [Fact]
    public void EmptyFieldConfigurationUsesTheCompleteSolicitudesAllowlist() =>
        Assert.Equal(SolicitudesFilePolicy.DefaultAccept,SolicitudesFilePolicy.NormalizeAccept(null));

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
