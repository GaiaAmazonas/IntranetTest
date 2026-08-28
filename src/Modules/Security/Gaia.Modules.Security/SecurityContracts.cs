using System.Security.Claims;

namespace Gaia.Modules.Security;

public static class AdminCorePermissions
{
    public const string IntranetVer="INTRANET.VER";
    public const string IntranetInicioVer="INT.INICIO.VER";
    public const string IntranetPersonasVer="INT.PERSONAS.VER";
    public const string IntranetCalendarioVer="INT.CALENDARIO.VER";
    public const string IntranetAplicacionesVer="INT.APLICACIONES.VER";
    public const string IntranetHelpdeskVer="INT.HELPDESK.VER";
    public const string IntranetAdminCoreVer="INT.APP.ADMINCORE.VER";
    public const string InicioVer="INICIO.VER";
    public const string OrgOrganigramaVer="ORG.ORGANIGRAMA.VER"; public const string OrgOrganigramaExportar="ORG.ORGANIGRAMA.EXPORTAR";
    public const string OrgUnidadesVer="ORG.UNIDADES.VER"; public const string OrgUnidadesCrear="ORG.UNIDADES.CREAR"; public const string OrgUnidadesActualizar="ORG.UNIDADES.ACTUALIZAR"; public const string OrgUnidadesActivar="ORG.UNIDADES.ACTIVAR"; public const string OrgUnidadesInactivar="ORG.UNIDADES.INACTIVAR"; public const string OrgUnidadesExportar="ORG.UNIDADES.EXPORTAR";
    public const string OrgAsignacionesVer="ORG.ASIGNACIONES.VER"; public const string OrgAsignacionesCrear="ORG.ASIGNACIONES.CREAR"; public const string OrgAsignacionesActualizar="ORG.ASIGNACIONES.ACTUALIZAR"; public const string OrgAsignacionesExportar="ORG.ASIGNACIONES.EXPORTAR";
    public const string OrgCargosVer="ORG.CARGOS.VER"; public const string OrgCargosCrear="ORG.CARGOS.CREAR"; public const string OrgCargosActualizar="ORG.CARGOS.ACTUALIZAR";
    public const string OrgCatalogosVer="ORG.SEDES_TIPOS.VER"; public const string OrgCatalogosCrear="ORG.SEDES_TIPOS.CREAR"; public const string OrgCatalogosActualizar="ORG.SEDES_TIPOS.ACTUALIZAR";
    public const string ThColaboradoresVer="TH.COLABORADORES.VER"; public const string ThColaboradoresCrear="TH.COLABORADORES.CREAR"; public const string ThColaboradoresActualizar="TH.COLABORADORES.ACTUALIZAR"; public const string ThColaboradoresActivar="TH.COLABORADORES.ACTIVAR"; public const string ThColaboradoresInactivar="TH.COLABORADORES.INACTIVAR";
    public const string ThColaboradoresImportar="TH.COL.IMPORT.ADMINISTRAR";
    public const string ThInfoVer="TH.COLABORADORES.INFO.VER"; public const string ThInfoActualizar="TH.COL.INFO.ACTUALIZAR";
    public const string ThVinculacionesVer="TH.VINCULACIONES.VER"; public const string ThVinculacionesCrear="TH.VINCULACIONES.CREAR"; public const string ThVinculacionesActualizar="TH.VINCULACIONES.ACTUALIZAR";
    public const string ThCorreosVer="TH.COLABORADORES.CORREOS.VER"; public const string ThCorreosCrear="TH.COLABORADORES.CORREOS.CREAR"; public const string ThCorreosActualizar="TH.COL.EMAIL.ACTUALIZAR"; public const string ThCorreosActivar="TH.COL.EMAIL.ACTIVAR"; public const string ThCorreosInactivar="TH.COL.EMAIL.INACTIVAR";
    public const string ThTelefonosVer="TH.COLABORADORES.TELEFONOS.VER"; public const string ThTelefonosCrear="TH.COL.TEL.CREAR"; public const string ThTelefonosActualizar="TH.COL.TEL.ACTUALIZAR"; public const string ThTelefonosActivar="TH.COL.TEL.ACTIVAR"; public const string ThTelefonosInactivar="TH.COL.TEL.INACTIVAR";
    public const string InvVer="INV.VER"; public const string InvAsignar="INV.ASIGNAR.ACTUALIZAR"; public const string InvImportar="INV.IMPORTAR.ADMINISTRAR";
    public const string ComEventsRead="COM.EVENTOS.VER"; public const string ComEventsCreate="COM.EVENTOS.CREAR"; public const string ComEventsEdit="COM.EVENTOS.ACTUALIZAR"; public const string ComEventsState="COM.EVENTOS.ADMINISTRAR";
    public const string ComEventTypesRead="COM.TIPOS_EVENTO.VER"; public const string ComEventTypesManage="COM.TIPOS_EVENTO.ADMINISTRAR";
    public const string ComBannersRead="COM.DESTACADOS.VER"; public const string ComBannersCreate="COM.DESTACADOS.CREAR"; public const string ComBannersEdit="COM.DESTACADOS.ACTUALIZAR"; public const string ComBannersState="COM.DESTACADOS.ADMINISTRAR";
    public const string TiUsuariosVer="TI.USUARIOS.VER"; public const string TiUsuariosCrear="TI.USUARIOS.CREAR"; public const string TiUsuariosActualizar="TI.USUARIOS.ACTUALIZAR"; public const string TiUsuariosActivar="TI.USUARIOS.ACTIVAR"; public const string TiUsuariosInactivar="TI.USUARIOS.INACTIVAR"; public const string TiUsuariosAdministrar="TI.USUARIOS.ADMINISTRAR";
    public const string TiRolesVer="TI.ROLES.VER"; public const string TiRolesCrear="TI.ROLES.CREAR"; public const string TiRolesActualizar="TI.ROLES.ACTUALIZAR"; public const string TiRolesAdministrar="TI.ROLES.ADMINISTRAR";
    public const string TiModulosVer="TI.MODULOS.VER"; public const string TiModulosCrear="TI.MODULOS.CREAR"; public const string TiModulosActualizar="TI.MODULOS.ACTUALIZAR"; public const string TiModulosActivar="TI.MODULOS.ACTIVAR"; public const string TiModulosInactivar="TI.MODULOS.INACTIVAR"; public const string TiModulosAdministrar="TI.MODULOS.ADMINISTRAR";
    public static readonly string[] All = typeof(AdminCorePermissions).GetFields().Where(f=>f.IsLiteral&&f.FieldType==typeof(string)).Select(f=>(string)f.GetRawConstantValue()!).Distinct().ToArray();
}

public static class DefaultRolePermissions
{
    public static readonly string[] Consulta =
    [
        AdminCorePermissions.IntranetVer,
        AdminCorePermissions.IntranetInicioVer,
        AdminCorePermissions.IntranetPersonasVer,
        AdminCorePermissions.IntranetCalendarioVer,
        AdminCorePermissions.IntranetAplicacionesVer,
        AdminCorePermissions.IntranetHelpdeskVer,
    ];
}

public static class PermissionScope
{
    public static bool RequiresAdminCore(string permission) =>
        !permission.Equals(AdminCorePermissions.IntranetVer, StringComparison.OrdinalIgnoreCase) &&
        !permission.StartsWith("INT.", StringComparison.OrdinalIgnoreCase);
}

public sealed record SecurityUser(Guid Id,string Name,string Email,string EntraObjectId,Guid? ThirdPartyId,string? DocumentNumber,DateTimeOffset? LastAccess,bool IsActive);
public sealed record SecurityUserListItem(Guid? Id,string Name,string Email,string? EntraObjectId,Guid? ThirdPartyId,string? DocumentNumber,DateTimeOffset? LastAccess,bool IsActive,string ProvisioningStatus);
public sealed record SecurityContextResponse(SecurityUser User,IReadOnlyList<string> Roles,IReadOnlyList<string> Permissions,IReadOnlyList<SecurityNavigationModule> Modules);
public sealed record SecurityNavigationModule(Guid Id,string Code,string Name,string? Description,string Route,string? Icon,int Order);
public sealed record SecurityModuleItem(Guid Id,string Code,string Name,string? Description,string Type,Guid? ParentId,string? Route,string? Icon,int Order,bool Visible,bool SupportsVisibility,bool IsActive);
public sealed record SecurityPermissionItem(Guid Id,string Code,string Name,string Action,Guid ModuleId,bool IsActive);
public sealed record SecurityRoleItem(Guid Id,string Code,string Name,string? Description,bool IsSystem,bool IsActive,int AssignedUsers,IReadOnlyList<string> Permissions);
public sealed record SecurityUserRoleItem(Guid Id,Guid RoleId,string RoleCode,string RoleName,DateOnly StartDate,DateOnly? EndDate,bool IsActive);
public sealed record SecurityUserDetail(SecurityUserListItem User,IReadOnlyList<SecurityUserRoleItem> Roles);
public sealed record SecurityMetadataTable(string LogicalName,string EntitySetName,string PrimaryId,string PrimaryName,IReadOnlyDictionary<string,string> Fields);
public sealed record SecurityMetadataAudit(IReadOnlyList<SecurityMetadataTable> Tables,IReadOnlyDictionary<string,int> ModuleTypes,IReadOnlyDictionary<string,int> Actions);
public sealed record SecurityBootstrapResult(int Modules,int Permissions,int Roles,int RolePermissions,string EdgarResult,string YilverResult);
public sealed record SecurityPreprovisionIssue(string Code,string Description,string? Email,Guid? ThirdPartyId);
public sealed record SecurityPreprovisionAudit(int ActiveThirdParties,int WithInstitutionalEmail,int Eligible,int ExistingApplicationUsers,int ToPreprovision,int DuplicateEmails,int MultipleInstitutionalEmails,bool EntraObjectIdAllowsNull,IReadOnlyList<SecurityPreprovisionIssue> Issues);
public sealed record SecurityPreprovisionResult(int CreatedUsers,int ExistingUsers,int AssignedConsulta,int AssignedAdmin,int Errors,IReadOnlyList<SecurityPreprovisionIssue> Issues);
public sealed record RoleWriteRequest(string Code,string Name,string? Description,bool IsActive);
public sealed record RolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);
public sealed record UserRoleWriteRequest(Guid RoleId,DateOnly StartDate,DateOnly? EndDate,string? Observations);
public sealed record ModuleWriteRequest(string Code,string Name,string? Description,string Type,Guid? ParentId,string? Route,string? Icon,int Order,bool Visible,bool IsActive);

public interface ISecurityStore
{
    Task<SecurityMetadataAudit> AuditMetadataAsync(CancellationToken token);
    Task<SecurityBootstrapResult> BootstrapAsync(ClaimsPrincipal principal,CancellationToken token);
    Task<SecurityContextResponse> GetOrProvisionAsync(ClaimsPrincipal principal,CancellationToken token);
    Task<IReadOnlyList<SecurityUserDetail>> ListUsersAsync(CancellationToken token);
    Task<SecurityPreprovisionAudit> AuditPreprovisionAsync(CancellationToken token);
    Task<SecurityPreprovisionResult> PreprovisionEligibleUsersAsync(CancellationToken token);
    Task<IReadOnlyList<SecurityRoleItem>> ListRolesAsync(CancellationToken token);
    Task<IReadOnlyList<SecurityModuleItem>> ListModulesAsync(CancellationToken token);
    Task<IReadOnlyList<SecurityPermissionItem>> ListPermissionsAsync(CancellationToken token);
    Task<Guid> UpsertRoleAsync(Guid? id,RoleWriteRequest request,CancellationToken token);
    Task SetRolePermissionsAsync(Guid roleId,RolePermissionsRequest request,CancellationToken token);
    Task<Guid> AssignUserRoleAsync(Guid userId,UserRoleWriteRequest request,CancellationToken token);
    Task EndUserRoleAsync(Guid userId,Guid assignmentId,DateOnly endDate,CancellationToken token);
    Task<Guid> UpsertModuleAsync(Guid? id,ModuleWriteRequest request,CancellationToken token);
}

public interface IAdminCoreAuthorization
{
    Task<bool> HasPermissionAsync(ClaimsPrincipal principal,string permission,CancellationToken token=default);
}
