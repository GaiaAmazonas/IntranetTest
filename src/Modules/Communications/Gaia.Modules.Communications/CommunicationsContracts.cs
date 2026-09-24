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
public sealed record LoginSocialDto(Guid Id,string Name,string Label,int Order,string Url);
public sealed record LoginConfigurationDto(Guid Id,string Name,string Code,string Eyebrow,string? ImageAlt,string? Description,
 int Status,bool IsCurrent,DateTimeOffset? PublishedAt,bool HasDesktopImage,bool HasTabletImage,bool HasMobileImage,
 string? PlatformName,string? LowerLeftText,string? FooterTitle,string? FooterDescription,IReadOnlyList<LoginSocialDto> SocialNetworks);
public sealed record LoginConfigurationWriteRequest(string Name,string Code,string Eyebrow,string? ImageAlt,string? Description,
 string? PlatformName,string? LowerLeftText,string? FooterTitle,string? FooterDescription);
public sealed record LoginSocialWriteRequest(string Name,string Label,int Order,string Url);
public sealed record PublicLoginSocialDto(string Name,string Label,int Order,string Url);
public sealed record PublicLoginConfigurationDto(string? PlatformName,string Eyebrow,string? Description,string? LowerLeftText,
 string? FooterTitle,string? FooterDescription,string? ImageAlt,string DesktopImageUrl,string TabletImageUrl,string MobileImageUrl,
 IReadOnlyList<PublicLoginSocialDto> SocialNetworks);
public sealed record VisualAmbienceDto(Guid Id,string Name,string Code,string? Description,int Scope,string Theme,int Effect,
 int PublicationStatus,DateTimeOffset StartsAt,DateTimeOffset EndsAt,int Intensity,string? PrimaryColor,string? SecondaryColor,
 string? AccentColor,bool AllowAnimation,bool ShowTopDecoration,bool ShowDecorativeBackground,string? PromotionalText,
 string? DestinationUrl,string? AlternativeText,bool HasDesktopImage,bool HasMobileImage,DateTimeOffset ModifiedAt);
public sealed record VisualAmbienceWriteRequest(string Name,string Code,string? Description,int Scope,string Theme,int Effect,
 DateTimeOffset StartsAt,DateTimeOffset EndsAt,int Intensity,string? PrimaryColor,string? SecondaryColor,string? AccentColor,
 bool AllowAnimation,bool ShowTopDecoration,bool ShowDecorativeBackground,string? PromotionalText,string? DestinationUrl,
 string? AlternativeText);
public sealed record ActiveVisualAmbienceDto(Guid Id,string Theme,int Effect,int Intensity,string? PrimaryColor,
 string? SecondaryColor,string? AccentColor,bool AllowAnimation,bool ShowTopDecoration,bool ShowDecorativeBackground,
 string? PromotionalText,string? DestinationUrl,string? AlternativeText,string? DesktopImageUrl,string? MobileImageUrl);

public interface ILoginConfigurationStore
{
 Task<IReadOnlyList<LoginConfigurationDto>> ListAsync(CancellationToken token);
 Task<LoginConfigurationDto> CreateAsync(LoginConfigurationWriteRequest request,Stream desktopImage,string contentType,string fileName,long length,CancellationToken token);
 Task<LoginConfigurationDto> SaveAsync(Guid id,LoginConfigurationWriteRequest request,CancellationToken token);
 Task<LoginConfigurationDto> PublishAsync(Guid id,CancellationToken token);
 Task<LoginConfigurationDto> RetireAsync(Guid id,CancellationToken token);
 Task UploadImageAsync(Guid id,string variant,Stream content,string contentType,string fileName,long length,CancellationToken token);
 Task<MediaContent?> ReadImageAsync(Guid id,string variant,CancellationToken token);
 Task DeleteImageAsync(Guid id,string variant,CancellationToken token);
 Task<LoginSocialDto> SaveSocialAsync(Guid configurationId,Guid? id,LoginSocialWriteRequest request,CancellationToken token);
 Task DeleteSocialAsync(Guid configurationId,Guid id,CancellationToken token);
 Task<PublicLoginConfigurationDto?> ReadPublicAsync(CancellationToken token);
 Task<MediaContent?> ReadPublicImageAsync(Guid id,string variant,CancellationToken token);
}

public interface IVisualAmbienceStore
{
 Task<IReadOnlyList<VisualAmbienceDto>> ListAsync(CancellationToken token);
 Task<VisualAmbienceDto> SaveAsync(Guid? id,VisualAmbienceWriteRequest request,CancellationToken token);
 Task<VisualAmbienceDto> PublishAsync(Guid id,CancellationToken token);
 Task<VisualAmbienceDto> RetireAsync(Guid id,CancellationToken token);
 Task UploadImageAsync(Guid id,string variant,Stream content,string contentType,string fileName,long length,CancellationToken token);
 Task<MediaContent?> ReadImageAsync(Guid id,string variant,CancellationToken token);
 Task DeleteImageAsync(Guid id,string variant,CancellationToken token);
 Task<ActiveVisualAmbienceDto?> ReadActiveAsync(string surface,DateTimeOffset now,CancellationToken token);
}

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
 Task UploadBannerImageAsync(Guid id,string variant,Stream content,string contentType,string fileName,long length,CancellationToken token);
 Task<MediaContent?> ReadBannerImageAsync(Guid id,string variant,CancellationToken token);
 Task DeleteBannerImageAsync(Guid id,string variant,CancellationToken token);
 Task DeleteBannerAsync(Guid id,CancellationToken token);
 Task<IReadOnlyList<PublicEventDto>> ListPublicEventsAsync(DateTimeOffset from,DateTimeOffset until,CancellationToken token);
 Task<PublicEventDto?> GetPublicEventAsync(Guid id,CancellationToken token);
 Task<IReadOnlyList<PublicBannerDto>> ListPublicBannersAsync(DateTimeOffset now,CancellationToken token);
}
