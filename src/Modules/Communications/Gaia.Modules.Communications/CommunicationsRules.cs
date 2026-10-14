using System.Text.RegularExpressions;
namespace Gaia.Modules.Communications;
public static partial class CommunicationsRules
{
 public static void Validate(EventTypeWriteRequest r){if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Code)||!Hex().IsMatch(r.Color??""))throw new ArgumentException("Nombre, código y un color hexadecimal válido son obligatorios.");}
 public static void Validate(EventWriteRequest r){if(string.IsNullOrWhiteSpace(r.Name)||r.EventTypeId==Guid.Empty||r.RequesterId==Guid.Empty)throw new ArgumentException("Nombre, tipo de evento y solicitante son obligatorios.");if(r.EndsAt<=r.StartsAt)throw new ArgumentException("La fecha final debe ser posterior a la fecha inicial.");if(r.EventUrl is not null&&!IsSafeWebUrl(r.EventUrl))throw new ArgumentException("El enlace del evento debe usar HTTPS.");}
 public static void Validate(BannerWriteRequest r){if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Title)||r.RequesterId==Guid.Empty)throw new ArgumentException("Nombre, título y solicitante son obligatorios.");if(r.Description?.Length>500)throw new ArgumentException("La descripción no puede superar 500 caracteres.");if(r.EndsAt<=r.StartsAt)throw new ArgumentException("La vigencia final debe ser posterior al inicio.");if(r.DestinationType==1&&r.EventId is null)throw new ArgumentException("Selecciona el evento de destino.");if(r.DestinationType==2&&!IsSafeWebUrl(r.ActionUrl))throw new ArgumentException("El enlace externo debe usar HTTPS.");if(r.DestinationType is <1 or >3)throw new ArgumentException("El tipo de destino no es válido.");}
 public static bool IsSafeWebUrl(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps&&!string.IsNullOrWhiteSpace(uri.Host)&&uri.UserInfo.Length==0;
 public static bool EventTransition(int current,string action)=>action.ToLowerInvariant() switch{"publish"=>current is 1 or 3 or 4,"cancel"=>current is 1 or 2,"finish"=>current==2,"deactivate"=>true,"restore"=>true,_=>false};
 public static bool BannerTransition(int current,string action)=>action.ToLowerInvariant() switch{"design"=>current==1,"publish"=>current==2,"close"=>current==3,"reject"=>current is 1 or 2,"deactivate"=>true,"restore"=>true,_=>false};
 [GeneratedRegex("^#[0-9A-Fa-f]{6}$")] private static partial Regex Hex();
}
