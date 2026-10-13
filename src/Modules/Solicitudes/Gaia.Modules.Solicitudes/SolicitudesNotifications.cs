namespace Gaia.Modules.Solicitudes;

/// <summary>
/// Publishes request lifecycle notifications without coupling Solicitudes to a delivery provider.
/// Implementations must record failures for operational recovery and must not undo a completed
/// business operation merely because a notification could not be queued.
/// </summary>
public interface ISolicitudesNotificationPublisher
{
    Task RequestSubmittedAsync(CreatedSolicitudesRequest request, Guid requesterId, CancellationToken token);
    Task RequestReassignedAsync(Guid requestId, Guid responsibleId, CancellationToken token);
    Task ManagementReassignedAsync(Guid managementId, Guid responsibleId, CancellationToken token);
}
