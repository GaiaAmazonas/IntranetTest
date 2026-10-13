using Gaia.BuildingBlocks.Notifications;

namespace Gaia.ArchitectureTests;

public sealed class EmailNotificationRulesTests
{
    [Fact]
    public void NormalizeCanonicalizesCodesRecipientsAndDefaults()
    {
        var value = EmailNotificationRules.Normalize(new(
            " operation-1 ", null, " solicitudes ", " radicacion ", Guid.NewGuid().ToString("D"),
            " solicitud_radicada ",
            [new("USUARIO@GAIAAMAZONAS.ORG", " Persona "), new("usuario@gaiaamazonas.org")],
            new Dictionary<string, string?> { ["numeroSolicitud"] = "HD-1" }));

        Assert.Equal("SOLICITUDES", value.Module);
        Assert.Equal("RADICACION", value.Event);
        Assert.Equal("SOLICITUD_RADICADA", value.TemplateCode);
        var recipient = Assert.Single(value.To);
        Assert.Equal("usuario@gaiaamazonas.org", recipient.Email);
        Assert.Equal("Persona", recipient.Name);
    }

    [Fact]
    public void RenderHtmlEncodesValuesAndRequiresEveryToken()
    {
        var rendered = EmailNotificationRules.RenderHtml("<p>Hola {{nombre}}</p>",
            new Dictionary<string, string?> { ["nombre"] = "Ana <Administración>" });
        Assert.Equal("<p>Hola Ana &lt;Administraci&#243;n&gt;</p>", rendered);

        var error = Assert.Throws<InvalidOperationException>(() =>
            EmailNotificationRules.RenderSubject("Solicitud {{numero}}", new Dictionary<string, string?>()));
        Assert.Contains("numero", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeRejectsInvalidRecipientAndIncompleteSharePointReference()
    {
        Assert.Throws<ArgumentException>(() => EmailNotificationRules.Normalize(new(
            "operation-1", null, "SOLICITUDES", "RADICACION", "source", "SOLICITUD_RADICADA",
            [new("correo-invalido")], new Dictionary<string, string?>())));

        Assert.Throws<ArgumentException>(() => EmailNotificationRules.Normalize(new(
            "operation-1", null, "SOLICITUDES", "CIERRE", "source", "SOLICITUD_CERRADA",
            [new("usuario@gaiaamazonas.org")], new Dictionary<string, string?>(),
            Attachments: [new("constancia.pdf", "SOLICITUDES", "source", "", "drive", "item", "application/pdf", 10)])));
    }
}
