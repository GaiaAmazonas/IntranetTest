namespace Gaia.Modules.Organization;

public interface IOrganizationPositionStore
{
    Task<IReadOnlyList<PositionResponse>> ListAsync(CancellationToken cancellationToken);
    Task<PositionWriteResult> CreateAsync(PositionWriteCommand command, CancellationToken cancellationToken);
    Task<PositionWriteResult> UpdateAsync(Guid id, PositionWriteCommand command, CancellationToken cancellationToken);
}

public sealed record PositionResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    string? Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record PositionWriteCommand(string? Code, string Name, string? Description, bool IsActive, string Actor);
public enum PositionWriteStatus { Created, Updated, NotFound, DuplicateCode }
public sealed record PositionWriteResult(PositionWriteStatus Status, PositionResponse? Item = null);
