namespace Gaia.Modules.Helpdesk;

public sealed record HelpdeskFormOption(Guid Id, string Code, string Label, bool IsDefault, int Order);
public sealed record HelpdeskFormField(Guid Id, string Code, string Label, int DataType, int ControlType,
    string? HelpText, string? Placeholder, bool Required, int Order, int Width, int? MinimumLength,
    int? MaximumLength, decimal? MinimumValue, decimal? MaximumValue, bool AllowsMultiple,
    int? MaximumFiles, string? AllowedFileTypes, IReadOnlyList<HelpdeskFormOption> Options);
public sealed record HelpdeskServiceForm(Guid Id, int Version, string Title, string? Instructions,
    IReadOnlyList<HelpdeskFormField> Fields);
public sealed record HelpdeskFieldAnswer(Guid FieldId, string? Value, IReadOnlyList<Guid>? OptionIds = null);

public interface IHelpdeskFormReader
{
    Task<HelpdeskServiceForm?> ReadForServiceAsync(Guid serviceId, CancellationToken cancellationToken);
}
