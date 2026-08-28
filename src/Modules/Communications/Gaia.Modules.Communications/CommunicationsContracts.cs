namespace Gaia.Modules.Communications;

public sealed record EventTypeDto(Guid Id,string Name,string Code,string Color,string? Description,int Order,bool IsActive);
public sealed record EventTypeWriteRequest(string Name,string Code,string Color,string? Description,int Order,bool IsActive);
public sealed record EventDto(Guid Id,string Name,Guid EventTypeId,string EventTypeName,string EventTypeColor,string? Summary,string? Description,DateTimeOffset StartsAt,DateTimeOffset? EndsAt,bool AllDay,int? Modality,string? Location,string? EventUrl,int Status,Guid RequesterId,string? RequesterName,bool IsActive);
public sealed record EventWriteRequest(string Name,Guid EventTypeId,string? Summary,string? Description,DateTimeOffset StartsAt,DateTimeOffset? EndsAt,bool AllDay,int? Modality,string? Location,string? EventUrl,Guid RequesterId);
public sealed record BannerDto(Guid Id,string Name,Guid? EventId,string? EventName,string? Eyebrow,string Title,string? Description,DateTimeOffset StartsAt,DateTimeOffset? EndsAt,int Order,int DestinationType,string? ActionUrl,int Status,Guid RequesterId,string? RequesterName,string? RejectionReason,DateTimeOffset? ClosedAt,bool HasDesktopImage,bool HasMobileImage,DateTimeOffset ModifiedAt,bool IsActive);
public sealed record BannerWriteRequest(string Name,Guid? EventId,string? Eyebrow,string Title,string? Description,DateTimeOffset StartsAt,DateTimeOffset? EndsAt,int Order,int DestinationType,string? ActionUrl,Guid RequesterId);
public sealed record PublicEventDto(Guid Id,string Name,string Type,string Color,string? Summary,string? Description,DateTimeOffset StartsAt,DateTimeOffset? EndsAt,bool AllDay,int? Modality,string? Location,string? EventUrl);
public sealed record PublicBannerDto(Guid Id,string? Eyebrow,string Title,string? Description,DateTimeOffset StartsAt,DateTimeOffset? EndsAt,int DestinationType,string? ActionUrl,Guid? EventId,string DesktopImageUrl,string MobileImageUrl);
public sealed record MediaContent(byte[] Bytes,string ContentType);

public interface ICommunicationsStore
{
 Task<IReadOnlyList<EventTypeDto>> ListEventTypesAsync(bool includeInactive,CancellationToken token);
 Task<EventTypeDto> SaveEventTypeAsync(Guid? id,EventTypeWriteRequest request,CancellationToken token);
 Task<IReadOnlyList<EventDto>> ListEventsAsync(bool includeInactive,CancellationToken token);
 Task<EventDto> SaveEventAsync(Guid? id,EventWriteRequest request,CancellationToken token);
 Task<EventDto> ChangeEventStateAsync(Guid id,string action,CancellationToken token);
 Task<IReadOnlyList<BannerDto>> ListBannersAsync(bool includeInactive,Guid? eventId,CancellationToken token);
 Task<BannerDto> SaveBannerAsync(Guid? id,BannerWriteRequest request,CancellationToken token);
 Task<BannerDto> ChangeBannerStateAsync(Guid id,string action,string? reason,CancellationToken token);
 Task UploadBannerImageAsync(Guid id,string variant,Stream content,string contentType,CancellationToken token);
 Task<MediaContent?> ReadBannerImageAsync(Guid id,string variant,CancellationToken token);
 Task<IReadOnlyList<PublicEventDto>> ListPublicEventsAsync(DateTimeOffset from,DateTimeOffset until,CancellationToken token);
 Task<PublicEventDto?> GetPublicEventAsync(Guid id,CancellationToken token);
 Task<IReadOnlyList<PublicBannerDto>> ListPublicBannersAsync(DateTimeOffset now,CancellationToken token);
}
