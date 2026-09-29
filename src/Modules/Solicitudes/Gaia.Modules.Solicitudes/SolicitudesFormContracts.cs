namespace Gaia.Modules.Solicitudes;

public sealed record SolicitudesFormOption(Guid Id, string Code, string Label, bool IsDefault, int Order);
public sealed record SolicitudesFormField(Guid Id, string Code, string Label, int DataType, int ControlType,
    string? HelpText, string? Placeholder, bool Required, int Order, int Width, int? MinimumLength,
    int? MaximumLength, decimal? MinimumValue, decimal? MaximumValue, bool AllowsMultiple,
    int? MaximumFiles, string? AllowedFileTypes, bool Visible, IReadOnlyList<SolicitudesFormOption> Options,
    string? ValidationPattern=null,string? ValidationMessage=null,string? DefaultValue=null,string? ConfigurationJson=null);
public sealed record SolicitudesServiceForm(Guid Id, int Version, string Title, string? Instructions,
    IReadOnlyList<SolicitudesFormField> Fields);
public sealed record SolicitudesFieldAnswer(Guid FieldId, string? Value, IReadOnlyList<Guid>? OptionIds = null);

public interface ISolicitudesFormReader
{
    Task<SolicitudesServiceForm?> ReadForServiceAsync(Guid serviceId, CancellationToken cancellationToken);
}
