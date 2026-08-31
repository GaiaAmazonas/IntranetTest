using System.Globalization;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Gaia.Modules.Security;
using Microsoft.Extensions.Caching.Memory;

namespace Gaia.Api.Infrastructure.Dataverse.Security;

internal sealed class DataverseSecurityStore(
    IDataverseDelegatedClientFactory clientFactory,
    IMemoryCache cache,
    ILogger<DataverseSecurityStore> logger,
    IConfiguration configuration) : ISecurityStore, IAdminCoreAuthorization
{
    private static readonly Action<ILogger, string, Exception?> LogPermissionResolutionFailure =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4601, "SecurityPermissionResolutionFailed"),
            "No fue posible resolver el permiso {Permission}.");
    private static readonly Action<ILogger, Exception?> LogThirdPartyLinkNotResolved =
        LoggerMessage.Define(LogLevel.Warning, new EventId(4602, "SecurityThirdPartyLinkNotResolved"),
            "El Usuario Aplicación autenticado no tiene Tercero relacionado y no fue posible resolver uno de forma inequívoca por correo. El acceso continuará sin contexto de colaborador.");
    private const string ModuleTable = "gaia_modulo";
    private const string PermissionTable = "gaia_permiso";
    private const string RoleTable = "gaia_rol";
    private const string RolePermissionTable = "gaia_rolpermiso";
    private const string UserTable = "gaia_usuarioaplicacion";
    private const string UserRoleTable = "gaia_usuariorol";
    private const string ThirdPartyTable = "gaia_terceros";
    private const string EmailTable = "gaia_correocolaborador";
    private const string CachePrefix = "admincore:";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProvisionLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> AssignmentLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> SecurityCacheKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim PreprovisionLock = new(1, 1);

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken token = default)
    {
        if (principal.Identity?.IsAuthenticated != true) return false;
        try
        {
            var context = await GetOrProvisionAsync(principal, token);
            return context.User.IsActive && context.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException exception)
        {
            LogPermissionResolutionFailure(logger, permission, exception);
            return false;
        }
    }

    public async Task<SecurityMetadataAudit> AuditMetadataAsync(CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var definitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [ModuleTable] = ["gaia_Codigo", "gaia_Descripcion", "gaia_Icono", "gaia_Modulopadre", "gaia_Nombre", "gaia_Orden", "gaia_Ruta", "gaia_Tipodemodulo", "gaia_Visiblenavegacion"],
            [PermissionTable] = ["gaia_Accion", "gaia_Codigo", "gaia_Descripcion", "gaia_ModuloPermiso", "gaia_Nombre"],
            [RoleTable] = ["gaia_Codigo", "gaia_Descripcion", "gaia_EsSistema", "gaia_Nombre"],
            [RolePermissionTable] = ["gaia_Nombre", "gaia_Permiso", "gaia_Rol"],
            [UserTable] = ["gaia_Correo", "gaia_EntraObjectId", "gaia_Nombre", "gaia_Tercero", "gaia_UltimoAcceso"],
            [UserRoleTable] = ["gaia_FechaFin", "gaia_FechaInicio", "gaia_Nombre", "gaia_Observaciones", "gaia_Rol", "gaia_Usuario"]
        };
        var tables = new List<SecurityMetadataTable>();
        foreach (var definition in definitions)
        {
            var metadata = await DataverseMetadataResolver.TableAsync(client, definition.Key, token);
            var fields = definition.Value
                .Select(schema => new { Schema = schema, Logical = metadata.OptionalAttribute(schema) })
                .Where(item => item.Logical is not null)
                .ToDictionary(item => item.Schema, item => item.Logical!, StringComparer.OrdinalIgnoreCase);
            tables.Add(new(metadata.LogicalName, metadata.EntitySetName, metadata.PrimaryIdAttribute, metadata.PrimaryNameAttribute, fields));
        }
        var module = await DataverseMetadataResolver.TableAsync(client, ModuleTable, token);
        var permission = await DataverseMetadataResolver.TableAsync(client, PermissionTable, token);
        return new(tables,
            await DataverseMetadataResolver.ChoicesAsync(client, ModuleTable, module.Attribute("gaia_Tipodemodulo"), token),
            await DataverseMetadataResolver.ChoicesAsync(client, PermissionTable, permission.Attribute("gaia_Accion"), token));
    }

    public async Task<SecurityBootstrapResult> BootstrapAsync(ClaimsPrincipal principal, CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        await AuditMetadataAsync(token);
        var moduleMeta = await DataverseMetadataResolver.TableAsync(client, ModuleTable, token);
        var permissionMeta = await DataverseMetadataResolver.TableAsync(client, PermissionTable, token);
        var roleMeta = await DataverseMetadataResolver.TableAsync(client, RoleTable, token);
        var rolePermissionMeta = await DataverseMetadataResolver.TableAsync(client, RolePermissionTable, token);
        var moduleType = await DataverseMetadataResolver.ChoicesAsync(client, ModuleTable, moduleMeta.Attribute("gaia_Tipodemodulo"), token);
        var actions = await DataverseMetadataResolver.ChoicesAsync(client, PermissionTable, permissionMeta.Attribute("gaia_Accion"), token);

        var modules = ModuleSeeds();
        var moduleIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in modules.OrderBy(x => x.ParentCode is null ? 0 : 1))
        {
            Guid? parent = seed.ParentCode is null ? null : moduleIds.GetValueOrDefault(seed.ParentCode);
            var typeValue = Choice(moduleType, seed.Type);
            var payload = new Dictionary<string, object?>
            {
                [moduleMeta.Attribute("gaia_Codigo")] = seed.Code,
                [moduleMeta.Attribute("gaia_Nombre")] = seed.Name,
                [moduleMeta.Attribute("gaia_Descripcion")] = seed.Description,
                [moduleMeta.Attribute("gaia_Tipodemodulo")] = typeValue,
                [moduleMeta.Attribute("gaia_Ruta")] = seed.Route,
                [moduleMeta.Attribute("gaia_Icono")] = seed.Icon,
                [moduleMeta.Attribute("gaia_Orden")] = seed.Order,
                ["statecode"] = 0
            };
            var visibleNavigation = moduleMeta.OptionalAttribute("gaia_Visiblenavegacion");
            if (visibleNavigation is not null) payload[visibleNavigation] = seed.Visible;
            if (parent.HasValue)
            {
                var relation = moduleMeta.Relationship("gaia_Modulopadre", ModuleTable);
                payload[$"{relation.NavigationProperty}@odata.bind"] = $"/{moduleMeta.EntitySetName}({parent:D})";
            }
            moduleIds[seed.Code] = await UpsertByCode(client, moduleMeta, seed.Code, payload, token);
        }

        var permissionIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in AdminCorePermissions.All)
        {
            var split = code.LastIndexOf('.');
            var moduleCode = PermissionModuleCode(split > 0 ? code[..split] : code);
            if (!moduleIds.TryGetValue(moduleCode, out var moduleId)) continue;
            var action = split > 0 ? code[(split + 1)..] : "VER";
            var relation = permissionMeta.Relationship("gaia_ModuloPermiso", ModuleTable);
            var payload = new Dictionary<string, object?>
            {
                [permissionMeta.Attribute("gaia_Codigo")] = code,
                [permissionMeta.Attribute("gaia_Nombre")] = $"{action} · {modules.First(x => x.Code.Equals(moduleCode, StringComparison.OrdinalIgnoreCase)).Name}",
                [permissionMeta.Attribute("gaia_Descripcion")] = $"Permite {action.ToLowerInvariant()} el recurso {moduleCode}.",
                [permissionMeta.Attribute("gaia_Accion")] = Choice(actions, action),
                [$"{relation.NavigationProperty}@odata.bind"] = $"/{moduleMeta.EntitySetName}({moduleId:D})",
                ["statecode"] = 0
            };
            permissionIds[code] = await UpsertByCode(client, permissionMeta, code, payload, token);
        }

        var adminId = await UpsertRole(client, roleMeta, "ADMIN", "Administrador", "Acceso administrativo completo al AdminCore Gaia.", true, token);
        var consultId = await UpsertRole(client, roleMeta, "CONSULTA", "Consulta", "Acceso de consulta a los módulos operativos autorizados.", true, token);
        var readCodes = DefaultRolePermissions.Consulta.Where(permissionIds.ContainsKey).ToArray();
        await EnsureRolePermissions(client, rolePermissionMeta, roleMeta, permissionMeta, adminId, permissionIds.Values, token);
        await EnsureRolePermissions(client, rolePermissionMeta, roleMeta, permissionMeta, consultId, readCodes.Select(x => permissionIds[x]), token);

        var current = await GetOrProvisionCoreAsync(principal, adminId, token);
        await EnsureUserRole(client, current.User.Id, adminId, "Bootstrap ADMIN", token);
        Invalidate(IdentityFrom(principal).Oid);
        var refreshed = await GetOrProvisionAsync(principal, token);
        var bootstrapDocument = BootstrapAdministratorDocuments().FirstOrDefault();
        var bootstrapThirdParty = string.IsNullOrWhiteSpace(bootstrapDocument)
            ? null
            : await FindThirdPartyByDocumentAsync(client, bootstrapDocument, token);
        return new(moduleIds.Count, permissionIds.Count, 2, permissionIds.Count + readCodes.Length,
            refreshed.Roles.Contains("ADMIN") ? "ADMIN asignado y verificado." : "No fue posible verificar ADMIN.",
            string.IsNullOrWhiteSpace(bootstrapDocument)
                ? "No hay un documento administrador adicional configurado."
                : bootstrapThirdParty is null
                    ? "El administrador adicional no se resolvió de forma única; se asignará cuando inicie sesión."
                    : "Administrador adicional identificado; se asignará con OID real cuando inicie sesión.");
    }

    public Task<SecurityContextResponse> GetOrProvisionAsync(ClaimsPrincipal principal, CancellationToken token)
    {
        var identity = IdentityFrom(principal);
        if (cache.TryGetValue<SecurityContextResponse>(CachePrefix + identity.Oid, out var cached)) return Task.FromResult(cached!);
        return GetOrProvisionCoreAsync(principal, null, token);
    }

    private async Task<SecurityContextResponse> GetOrProvisionCoreAsync(ClaimsPrincipal principal, Guid? forcedRole, CancellationToken token)
    {
        var identity = IdentityFrom(principal);
        var gate = ProvisionLocks.GetOrAdd(identity.Oid, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try { return await GetOrProvisionUnsafeAsync(principal, forcedRole, token); }
        finally { gate.Release(); }
    }

    private async Task<SecurityContextResponse> GetOrProvisionUnsafeAsync(ClaimsPrincipal principal, Guid? forcedRole, CancellationToken token)
    {
        var identity = IdentityFrom(principal);
        var client = await clientFactory.CreateAsync();
        var userMeta = await DataverseMetadataResolver.TableAsync(client, UserTable, token);
        var oidField = userMeta.Attribute("gaia_EntraObjectId");
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{userMeta.EntitySetName}?$select={userMeta.PrimaryIdAttribute},{userMeta.Attribute("gaia_Nombre")},{userMeta.Attribute("gaia_Correo")},{userMeta.Attribute("gaia_UltimoAcceso")},_{userMeta.Attribute("gaia_Tercero")}_value,statecode&$filter={oidField} eq '{Escape(identity.Oid)}'&$top=2", token);
        Guid userId;
        Guid? thirdPartyId;
        string? document;
        if (rows.Count == 0)
        {
            (thirdPartyId, document) = await MatchThirdPartyAsync(client, identity.Email, identity.Name, token);
            if (!thirdPartyId.HasValue)
                throw new InvalidOperationException("El correo autenticado no se relaciona inequívocamente con un Tercero activo y un único correo institucional.");
            var preprovisioned = await DataverseJson.ReadAllAsync(client,
                $"{userMeta.EntitySetName}?$select={userMeta.PrimaryIdAttribute},statecode&$filter=_{userMeta.Attribute("gaia_Tercero")}_value eq {thirdPartyId:D} and {userMeta.Attribute("gaia_Correo")} eq '{Escape(identity.Email)}' and {oidField} eq null&$top=2", token);
            if (preprovisioned.Count > 1)
                throw new InvalidOperationException("Existe más de un Usuario Aplicación preaprovisionado para el mismo Tercero y correo institucional.");
            if (preprovisioned.Count == 1)
            {
                if ((DataverseJson.OptionalInt32(preprovisioned[0], "statecode") ?? 0) != 0)
                    throw new InvalidOperationException("El Usuario Aplicación preaprovisionado está inactivo.");
                userId = GuidValue(preprovisioned[0], userMeta.PrimaryIdAttribute);
                await Patch(client, $"{userMeta.EntitySetName}({userId:D})", new()
                {
                    [userMeta.Attribute("gaia_Nombre")] = identity.Name,
                    [userMeta.Attribute("gaia_Correo")] = identity.Email,
                    [oidField] = identity.Oid,
                    [userMeta.Attribute("gaia_UltimoAcceso")] = DateTimeOffset.UtcNow
                }, token);
            }
            else
            {
                var payload = new Dictionary<string, object?>
                {
                    [userMeta.Attribute("gaia_Nombre")] = identity.Name,
                    [userMeta.Attribute("gaia_Correo")] = identity.Email,
                    [oidField] = identity.Oid,
                    [userMeta.Attribute("gaia_UltimoAcceso")] = DateTimeOffset.UtcNow,
                    ["statecode"] = 0
                };
                var thirdMeta = await DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
                var relation = userMeta.Relationship("gaia_Tercero", ThirdPartyTable);
                payload[$"{relation.NavigationProperty}@odata.bind"] = $"/{thirdMeta.EntitySetName}({thirdPartyId:D})";
                userId = await Post(client, userMeta.EntitySetName, payload, token);
                var role = forcedRole ?? await FindRoleId(client, IsBootstrapAdministrator(identity.Email, document) ? "ADMIN" : "CONSULTA", token)
                    ?? throw new InvalidOperationException("El catálogo de roles de Seguridad aún no ha sido inicializado.");
                await EnsureUserRole(client, userId, role, "Asignación automática JIT", token);
            }
        }
        else if (rows.Count == 1)
        {
            var row = rows[0];
            userId = GuidValue(row, userMeta.PrimaryIdAttribute);
            thirdPartyId = OptionalGuid(row, $"_{userMeta.Attribute("gaia_Tercero")}_value");
            document = thirdPartyId.HasValue ? await ReadDocumentAsync(client, thirdPartyId.Value, token) : null;
            if (!thirdPartyId.HasValue)
            {
                (thirdPartyId, document) = await MatchThirdPartyAsync(client, identity.Email, identity.Name, token);
                if (!thirdPartyId.HasValue)
                {
                    LogThirdPartyLinkNotResolved(logger, null);
                }
                else
                {
                    var thirdMeta = await DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
                    var relation = userMeta.Relationship("gaia_Tercero", ThirdPartyTable);
                    await Patch(client, $"{userMeta.EntitySetName}({userId:D})", new()
                    {
                        [$"{relation.NavigationProperty}@odata.bind"] = $"/{thirdMeta.EntitySetName}({thirdPartyId:D})",
                        [userMeta.Attribute("gaia_Nombre")] = identity.Name,
                        [userMeta.Attribute("gaia_Correo")] = identity.Email,
                        [userMeta.Attribute("gaia_UltimoAcceso")] = DateTimeOffset.UtcNow
                    }, token);
                }
            }
            await Patch(client, $"{userMeta.EntitySetName}({userId:D})", new() { [userMeta.Attribute("gaia_UltimoAcceso")] = DateTimeOffset.UtcNow }, token);
        }
        else
        {
            var duplicateIds = string.Join(", ", rows.Select(row => GuidValue(row, userMeta.PrimaryIdAttribute).ToString("D")));
            throw new InvalidOperationException($"Existe más de un Usuario Aplicación para el mismo Entra Object ID. Registros: {duplicateIds}.");
        }

        if (forcedRole.HasValue)
        {
            await EnsureUserRole(client, userId, forcedRole.Value, "Bootstrap ADMIN", token);
        }
        else if (IsBootstrapAdministrator(identity.Email, document))
        {
            var administratorRole = await FindRoleId(client, "ADMIN", token)
                ?? throw new InvalidOperationException("El rol interno ADMIN no está disponible en el catálogo de Seguridad.");
            await EnsureAdministratorPermissions(client, administratorRole, token);
            await EnsureUserRole(client, userId, administratorRole, "Recuperación automática de administrador autorizado", token);
        }
        var context = await LoadContext(client, userMeta, userId, identity.Oid, identity.Name, identity.Email, thirdPartyId, document, token);
        var cacheKey = CachePrefix + identity.Oid;
        cache.Set(cacheKey, context, TimeSpan.FromMinutes(3));
        SecurityCacheKeys[cacheKey] = 0;
        return context;
    }

    private static async Task<SecurityContextResponse> LoadContext(HttpClient client, DataverseTableMetadata userMeta, Guid userId,
        string entraObjectId, string name, string email, Guid? thirdPartyId, string? document, CancellationToken token)
    {
        var userRow = await DataverseMetadataResolver.ReadOneAsync(client, $"{userMeta.EntitySetName}({userId:D})?$select=statecode,{userMeta.Attribute("gaia_UltimoAcceso")}", token);
        var active = userRow is not null && (DataverseJson.OptionalInt32(userRow.Value, "statecode") ?? 0) == 0;
        var roleMeta = await DataverseMetadataResolver.TableAsync(client, RoleTable, token);
        var userRoleMeta = await DataverseMetadataResolver.TableAsync(client, UserRoleTable, token);
        var rolePermissionMeta = await DataverseMetadataResolver.TableAsync(client, RolePermissionTable, token);
        var permissionMeta = await DataverseMetadataResolver.TableAsync(client, PermissionTable, token);
        var moduleMeta = await DataverseMetadataResolver.TableAsync(client, ModuleTable, token);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var userLookup = userRoleMeta.RelationshipTo(UserTable).ReferencingAttribute;
        var roleLookup = userRoleMeta.RelationshipTo(RoleTable).ReferencingAttribute;
        var start = userRoleMeta.Attribute("gaia_FechaInicio");
        var end = userRoleMeta.Attribute("gaia_FechaFin");
        var assignments = await DataverseJson.ReadAllAsync(client,
            $"{userRoleMeta.EntitySetName}?$select=_{roleLookup}_value&$filter=_{userLookup}_value eq {userId:D} and statecode eq 0 and {start} le {today} and ({end} eq null or {end} ge {today})", token);
        var roleIds = assignments.Select(x => OptionalGuid(x, $"_{roleLookup}_value")).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (roleIds.Length == 0) return new(new(userId, name, email, entraObjectId, thirdPartyId, document, null, active), [], [], []);
        var roleFilter = string.Join(" or ", roleIds.Select(x => $"{roleMeta.PrimaryIdAttribute} eq {x:D}"));
        var roles = await DataverseJson.ReadAllAsync(client, $"{roleMeta.EntitySetName}?$select={roleMeta.Attribute("gaia_Codigo")}&$filter=statecode eq 0 and ({roleFilter})", token);
        var rolePermissionRole = rolePermissionMeta.Attribute("gaia_Rol");
        var rolePermissionPermission = rolePermissionMeta.Attribute("gaia_Permiso");
        var rpFilter = string.Join(" or ", roleIds.Select(x => $"_{rolePermissionRole}_value eq {x:D}"));
        var links = await DataverseJson.ReadAllAsync(client, $"{rolePermissionMeta.EntitySetName}?$select=_{rolePermissionPermission}_value&$filter=statecode eq 0 and ({rpFilter})", token);
        var permissionIds = links.Select(x => OptionalGuid(x, $"_{rolePermissionPermission}_value")).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var permissions = Array.Empty<string>();
        var navigationModules = Array.Empty<SecurityNavigationModule>();
        if (permissionIds.Length > 0)
        {
            var filter = string.Join(" or ", permissionIds.Select(x => $"{permissionMeta.PrimaryIdAttribute} eq {x:D}"));
            var permissionModule = permissionMeta.Attribute("gaia_ModuloPermiso");
            var assignedPermissions = await DataverseJson.ReadAllAsync(client,
                $"{permissionMeta.EntitySetName}?$select={permissionMeta.Attribute("gaia_Codigo")},_{permissionModule}_value&$filter=statecode eq 0 and ({filter})", token);
            var moduleParent = moduleMeta.Attribute("gaia_Modulopadre");
            var moduleVisible = moduleMeta.OptionalAttribute("gaia_Visiblenavegacion");
            var moduleFields = $"{moduleMeta.PrimaryIdAttribute},{moduleMeta.Attribute("gaia_Codigo")},{moduleMeta.Attribute("gaia_Nombre")},{moduleMeta.Attribute("gaia_Descripcion")},_{moduleParent}_value,{moduleMeta.Attribute("gaia_Ruta")},{moduleMeta.Attribute("gaia_Icono")},{moduleMeta.Attribute("gaia_Orden")},statecode";
            if (moduleVisible is not null) moduleFields += $",{moduleVisible}";
            var moduleRows = await DataverseJson.ReadAllAsync(client,
                $"{moduleMeta.EntitySetName}?$select={moduleFields}", token);
            var moduleParents = moduleRows.ToDictionary(
                row => GuidValue(row, moduleMeta.PrimaryIdAttribute),
                row => OptionalGuid(row, $"_{moduleParent}_value"));
            var activeModuleIds = moduleRows
                .Where(row => (DataverseJson.OptionalInt32(row, "statecode") ?? 0) == 0)
                .Select(row => GuidValue(row, moduleMeta.PrimaryIdAttribute))
                .ToHashSet();
            var effectiveActiveModuleIds = SecurityModuleRules.EffectiveActiveIds(moduleParents, activeModuleIds);
            permissions = assignedPermissions
                .Where(row => OptionalGuid(row, $"_{permissionModule}_value") is Guid moduleId && effectiveActiveModuleIds.Contains(moduleId))
                .Select(x => StringValue(x, permissionMeta.Attribute("gaia_Codigo"))).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var assignedModuleIds = assignedPermissions
                .Select(row => OptionalGuid(row, $"_{permissionModule}_value"))
                .Where(id => id.HasValue && effectiveActiveModuleIds.Contains(id.Value))
                .Select(id => id!.Value)
                .ToHashSet();
            var authorizedRootIds = new HashSet<Guid>();
            foreach (var assignedModuleId in assignedModuleIds)
            {
                var current = assignedModuleId;
                while (moduleParents.TryGetValue(current, out var parent) && parent.HasValue) current = parent.Value;
                authorizedRootIds.Add(current);
            }
            var authorizedApplicationIds = moduleRows
                .Where(row => (StringValue(row, moduleMeta.Attribute("gaia_Codigo")) ?? "").StartsWith("INT.APP.", StringComparison.OrdinalIgnoreCase))
                .Select(row => GuidValue(row, moduleMeta.PrimaryIdAttribute))
                .Where(assignedModuleIds.Contains)
                .ToHashSet();
            navigationModules = moduleRows
                .Where(row => authorizedRootIds.Contains(GuidValue(row, moduleMeta.PrimaryIdAttribute)) || authorizedApplicationIds.Contains(GuidValue(row, moduleMeta.PrimaryIdAttribute)))
                .Where(row => effectiveActiveModuleIds.Contains(GuidValue(row, moduleMeta.PrimaryIdAttribute)))
                .Where(row => moduleVisible is null || BoolValue(row, moduleVisible))
                .Select(row => new SecurityNavigationModule(
                    GuidValue(row, moduleMeta.PrimaryIdAttribute),
                    StringValue(row, moduleMeta.Attribute("gaia_Codigo")) ?? "",
                    StringValue(row, moduleMeta.Attribute("gaia_Nombre")) ?? "",
                    StringValue(row, moduleMeta.Attribute("gaia_Descripcion")),
                    StringValue(row, moduleMeta.Attribute("gaia_Ruta")) ?? "",
                    StringValue(row, moduleMeta.Attribute("gaia_Icono")),
                    DataverseJson.OptionalInt32(row, moduleMeta.Attribute("gaia_Orden")) ?? 0))
                .Where(module => !string.IsNullOrWhiteSpace(module.Route) &&
                    (module.Code.StartsWith("INT.APP.", StringComparison.OrdinalIgnoreCase) || (module.Route != "/admincore" && module.Route != "/intranet")))
                .OrderBy(module => module.Order)
                .ToArray();
        }
        return new(new(userId, name, email, entraObjectId, thirdPartyId, document, DateTimeOffset.UtcNow, active),
            roles.Select(x => StringValue(x, roleMeta.Attribute("gaia_Codigo"))).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray(), permissions, navigationModules);
    }

    public async Task<IReadOnlyList<SecurityUserDetail>> ListUsersAsync(CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var user = await DataverseMetadataResolver.TableAsync(client, UserTable, token);
        var role = await DataverseMetadataResolver.TableAsync(client, RoleTable, token);
        var assignment = await DataverseMetadataResolver.TableAsync(client, UserRoleTable, token);
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
        var thirdPartyLookup = user.Attribute("gaia_Tercero");
        var userRows = await DataverseJson.ReadAllAsync(client,
            $"{user.EntitySetName}?$select={user.PrimaryIdAttribute},{user.Attribute("gaia_Nombre")},{user.Attribute("gaia_Correo")},{user.Attribute("gaia_EntraObjectId")},{user.Attribute("gaia_UltimoAcceso")},_{thirdPartyLookup}_value,statecode&$orderby={user.Attribute("gaia_Nombre")}", token);
        var roleRows = await DataverseJson.ReadAllAsync(client,
            $"{role.EntitySetName}?$select={role.PrimaryIdAttribute},{role.Attribute("gaia_Codigo")},{role.Attribute("gaia_Nombre")},statecode", token);
        var assignmentUser = assignment.RelationshipTo(UserTable).ReferencingAttribute;
        var assignmentRole = assignment.RelationshipTo(RoleTable).ReferencingAttribute;
        var assignmentRows = await DataverseJson.ReadAllAsync(client,
            $"{assignment.EntitySetName}?$select={assignment.PrimaryIdAttribute},_{assignmentUser}_value,_{assignmentRole}_value,{assignment.Attribute("gaia_FechaInicio")},{assignment.Attribute("gaia_FechaFin")},statecode", token);

        var thirdPartyIds = userRows.Select(row => OptionalGuid(row, $"_{thirdPartyLookup}_value")).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var documents = new Dictionary<Guid, string?>();
        if (thirdPartyIds.Length > 0)
        {
            var filter = string.Join(" or ", thirdPartyIds.Select(id => $"{thirdParty.PrimaryIdAttribute} eq {id:D}"));
            var documentField = thirdParty.Attribute("gaia_NumeroDocumento");
            var thirdPartyRows = await DataverseJson.ReadAllAsync(client,
                $"{thirdParty.EntitySetName}?$select={thirdParty.PrimaryIdAttribute},{documentField}&$filter={filter}", token);
            documents = thirdPartyRows.ToDictionary(row => GuidValue(row, thirdParty.PrimaryIdAttribute), row => StringValue(row, documentField));
        }

        var roles = roleRows.ToDictionary(row => GuidValue(row, role.PrimaryIdAttribute));
        var assignmentsByUser = assignmentRows
            .Where(row => OptionalGuid(row, $"_{assignmentUser}_value").HasValue)
            .GroupBy(row => OptionalGuid(row, $"_{assignmentUser}_value")!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SecurityUserRoleItem>)group.Select(row =>
            {
                var roleId = OptionalGuid(row, $"_{assignmentRole}_value") ?? Guid.Empty;
                roles.TryGetValue(roleId, out var roleRow);
                return new SecurityUserRoleItem(GuidValue(row, assignment.PrimaryIdAttribute), roleId,
                    roleRow.ValueKind == JsonValueKind.Undefined ? "" : StringValue(roleRow, role.Attribute("gaia_Codigo")) ?? "",
                    roleRow.ValueKind == JsonValueKind.Undefined ? "" : StringValue(roleRow, role.Attribute("gaia_Nombre")) ?? "",
                    OptionalDateOnly(row, assignment.Attribute("gaia_FechaInicio")) ?? DateOnly.MinValue,
                    OptionalDateOnly(row, assignment.Attribute("gaia_FechaFin")),
                    (DataverseJson.OptionalInt32(row, "statecode") ?? 0) == 0);
            }).OrderByDescending(item => item.StartDate).ToArray());

        return userRows.Select(row =>
        {
            var id = GuidValue(row, user.PrimaryIdAttribute);
            var relatedThirdParty = OptionalGuid(row, $"_{thirdPartyLookup}_value");
            var entraObjectId = StringValue(row, user.Attribute("gaia_EntraObjectId"));
            return new SecurityUserDetail(new(id,
                StringValue(row, user.Attribute("gaia_Nombre")) ?? "",
                StringValue(row, user.Attribute("gaia_Correo")) ?? "",
                entraObjectId,
                relatedThirdParty,
                relatedThirdParty.HasValue && documents.TryGetValue(relatedThirdParty.Value, out var document) ? document : null,
                OptionalDateTime(row, user.Attribute("gaia_UltimoAcceso")),
                (DataverseJson.OptionalInt32(row, "statecode") ?? 0) == 0,
                string.IsNullOrWhiteSpace(entraObjectId) ? "PENDING_FIRST_ACCESS" : "PROVISIONED"),
                assignmentsByUser.GetValueOrDefault(id) ?? []);
        }).Concat(await ReadEligiblePendingUsersAsync(client, userRows, token)).OrderBy(item => item.User.Name).ToArray();
    }

    public async Task<SecurityPreprovisionAudit> AuditPreprovisionAsync(CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var analysis = await AnalyzePreprovisionAsync(client, token);
        return analysis.Audit;
    }

    public async Task<SecurityPreprovisionResult> PreprovisionEligibleUsersAsync(CancellationToken token)
    {
        await PreprovisionLock.WaitAsync(token);
        try
        {
            var client = await clientFactory.CreateAsync();
            var analysis = await AnalyzePreprovisionAsync(client, token);
            if (!analysis.Audit.EntraObjectIdAllowsNull)
                throw new InvalidOperationException("Dataverse exige gaia_EntraObjectId. Debe hacerse opcional antes de preaprovisionar usuarios sin identidad Microsoft Entra.");
            var user = await DataverseMetadataResolver.TableAsync(client, UserTable, token);
            var thirdParty = await DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
            var relation = user.Relationship("gaia_Tercero", ThirdPartyTable);
            var consulta = await FindRoleId(client, "CONSULTA", token) ?? throw new InvalidOperationException("No existe el rol CONSULTA activo.");
            var admin = await FindRoleId(client, "ADMIN", token) ?? throw new InvalidOperationException("No existe el rol ADMIN activo.");
            var created = 0; var existing = analysis.Audit.ExistingApplicationUsers; var assignedConsulta = 0; var assignedAdmin = 0; var errors = new List<SecurityPreprovisionIssue>();
            var administratorEmails = BootstrapAdministratorEmails();
            var administratorDocuments = BootstrapAdministratorDocuments();
            foreach (var candidate in analysis.ToCreate)
            {
                try
                {
                    var payload = new Dictionary<string, object?>
                    {
                        [user.Attribute("gaia_Nombre")] = candidate.Name,
                        [user.Attribute("gaia_Correo")] = candidate.Email,
                        [user.Attribute("gaia_EntraObjectId")] = null,
                        ["statecode"] = 0,
                        [$"{relation.NavigationProperty}@odata.bind"] = $"/{thirdParty.EntitySetName}({candidate.ThirdPartyId:D})"
                    };
                    var id = await Post(client, user.EntitySetName, payload, token);
                    var isAdmin = administratorDocuments.Contains(candidate.Document, StringComparer.OrdinalIgnoreCase)
                        || administratorEmails.Contains(candidate.Email, StringComparer.OrdinalIgnoreCase);
                    await EnsureUserRole(client, id, isAdmin ? admin : consulta, "Preaprovisionamiento administrativo", token);
                    created++; if (isAdmin) assignedAdmin++; else assignedConsulta++;
                }
                catch (Exception exception)
                {
                    errors.Add(new("PREPROVISION_ERROR", exception.Message, candidate.Email, candidate.ThirdPartyId));
                }
            }
            InvalidateAll();
            return new(created, existing, assignedConsulta, assignedAdmin, errors.Count, errors);
        }
        finally { PreprovisionLock.Release(); }
    }

    private string[] BootstrapAdministratorEmails() =>
        configuration.GetSection("Authorization:BootstrapAdministrators").Get<string[]>() ?? [];

    private string[] BootstrapAdministratorDocuments() =>
        configuration.GetSection("Authorization:BootstrapAdministratorDocuments").Get<string[]>() ?? [];

    private bool IsBootstrapAdministrator(string? email, string? document) =>
        (!string.IsNullOrWhiteSpace(email) && BootstrapAdministratorEmails().Contains(email, StringComparer.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(document) && BootstrapAdministratorDocuments().Contains(document, StringComparer.OrdinalIgnoreCase));

    private static async Task<PreprovisionAnalysis> AnalyzePreprovisionAsync(HttpClient client, CancellationToken token)
    {
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
        var email = await DataverseMetadataResolver.TableAsync(client, EmailTable, token);
        var user = await DataverseMetadataResolver.TableAsync(client, UserTable, token);
        var documentField = thirdParty.Attribute("gaia_NumeroDocumento");
        var thirdPartyRows = await DataverseJson.ReadAllAsync(client,
            $"{thirdParty.EntitySetName}?$select={thirdParty.PrimaryIdAttribute},{thirdParty.PrimaryNameAttribute},{documentField}&$filter=statecode eq 0", token);
        var parentField = email.Attribute("gaia_Tercero"); var addressField = email.Attribute("gaia_Correoelectronico");
        var emailRows = await DataverseJson.ReadAllAsync(client,
            $"{email.EntitySetName}?$select={addressField},_{parentField}_value&$filter=statecode eq 0", token);
        var userThirdParty = user.Attribute("gaia_Tercero"); var userEmail = user.Attribute("gaia_Correo");
        var userRows = await DataverseJson.ReadAllAsync(client,
            $"{user.EntitySetName}?$select={user.PrimaryIdAttribute},{userEmail},_{userThirdParty}_value,{user.Attribute("gaia_EntraObjectId")},statecode", token);
        var constraints = await DataverseMetadataResolver.ConstraintsAsync(client, UserTable, token);
        var oidLogical = user.Attribute("gaia_EntraObjectId");
        var oidAllowsNull = !constraints.TryGetValue("gaia_EntraObjectId", out var oidConstraint)
            ? constraints.TryGetValue(oidLogical, out oidConstraint) && oidConstraint.RequiredLevel is not ("SystemRequired" or "ApplicationRequired")
            : oidConstraint.RequiredLevel is not ("SystemRequired" or "ApplicationRequired");

        var activeIds = thirdPartyRows.Select(row => GuidValue(row, thirdParty.PrimaryIdAttribute)).ToHashSet();
        var institutional = emailRows.Select(row => new { Id = OptionalGuid(row, $"_{parentField}_value"), Email = StringValue(row, addressField)?.Trim().ToLowerInvariant() })
            .Where(row => row.Id.HasValue && activeIds.Contains(row.Id.Value) && row.Email is not null && row.Email.EndsWith("@gaiaamazonas.org", StringComparison.OrdinalIgnoreCase)).ToArray();
        var byEmail = institutional.GroupBy(row => row.Email!, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Select(row => row.Id!.Value).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
        var byThirdParty = institutional.GroupBy(row => row.Id!.Value).ToDictionary(group => group.Key, group => group.Select(row => row.Email!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var issues = new List<SecurityPreprovisionIssue>();
        foreach (var item in byEmail.Where(pair => pair.Value.Length > 1)) issues.Add(new("DUPLICATE_INSTITUTIONAL_EMAIL", "El correo institucional está relacionado con más de un Tercero activo.", item.Key, null));
        foreach (var item in byThirdParty.Where(pair => pair.Value.Length > 1)) issues.Add(new("MULTIPLE_INSTITUTIONAL_EMAILS", $"El Tercero tiene {item.Value.Length} correos institucionales activos.", null, item.Key));
        var existingThirdParties = userRows.Select(row => OptionalGuid(row, $"_{userThirdParty}_value")).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        var existingEmails = userRows.Select(row => StringValue(row, userEmail)?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var thirdPartyMap = thirdPartyRows.ToDictionary(row => GuidValue(row, thirdParty.PrimaryIdAttribute));
        var eligible = byThirdParty.Where(pair => pair.Value.Length == 1 && byEmail[pair.Value[0]].Length == 1)
            .Select(pair => new PreprovisionCandidate(pair.Key, StringValue(thirdPartyMap[pair.Key], thirdParty.PrimaryNameAttribute) ?? pair.Value[0], pair.Value[0], StringValue(thirdPartyMap[pair.Key], documentField))).ToArray();
        var toCreate = eligible.Where(candidate => !existingThirdParties.Contains(candidate.ThirdPartyId) && !existingEmails.Contains(candidate.Email)).ToArray();
        var existingEligible = eligible.Length - toCreate.Length;
        var audit = new SecurityPreprovisionAudit(thirdPartyRows.Count, byThirdParty.Count, eligible.Length, existingEligible, toCreate.Length,
            byEmail.Count(pair => pair.Value.Length > 1), byThirdParty.Count(pair => pair.Value.Length > 1), oidAllowsNull, issues);
        return new(audit, toCreate);
    }

    private sealed record PreprovisionCandidate(Guid ThirdPartyId,string Name,string Email,string? Document);
    private sealed record PreprovisionAnalysis(SecurityPreprovisionAudit Audit,IReadOnlyList<PreprovisionCandidate> ToCreate);

    private static async Task<IReadOnlyList<SecurityUserDetail>> ReadEligiblePendingUsersAsync(
        HttpClient client, IReadOnlyList<JsonElement> provisionedRows, CancellationToken token)
    {
        var email = await DataverseMetadataResolver.TableAsync(client, EmailTable, token);
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
        var parentField = email.Attribute("gaia_Tercero");
        var addressField = email.Attribute("gaia_Correoelectronico");
        var primaryField = email.OptionalAttribute("gaia_Principal");
        var emailSelect = $"{addressField},_{parentField}_value" + (primaryField is null ? "" : $",{primaryField}");
        var emailRows = await DataverseJson.ReadAllAsync(client,
            $"{email.EntitySetName}?$select={emailSelect}&$filter=statecode eq 0", token);
        var candidates = emailRows
            .Select(row => new { ThirdPartyId = OptionalGuid(row, $"_{parentField}_value"), Address = StringValue(row, addressField)?.Trim().ToLowerInvariant(), Primary = primaryField is not null && BoolValue(row, primaryField) })
            .Where(row => row.ThirdPartyId.HasValue && row.Address is not null && row.Address.EndsWith("@gaiaamazonas.org", StringComparison.OrdinalIgnoreCase))
            .GroupBy(row => row.ThirdPartyId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(row => row.Primary).ThenBy(row => row.Address).First().Address!);
        if (candidates.Count == 0) return [];

        var userMeta = await DataverseMetadataResolver.TableAsync(client, UserTable, token);
        var userThirdParty = userMeta.Attribute("gaia_Tercero");
        var provisionedThirdParties = provisionedRows.Select(row => OptionalGuid(row, $"_{userThirdParty}_value")).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        var provisionedEmails = provisionedRows.Select(row => StringValue(row, userMeta.Attribute("gaia_Correo"))?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pendingIds = candidates.Where(pair => !provisionedThirdParties.Contains(pair.Key) && !provisionedEmails.Contains(pair.Value)).Select(pair => pair.Key).ToArray();
        if (pendingIds.Length == 0) return [];

        var documentField = thirdParty.Attribute("gaia_NumeroDocumento");
        var filter = string.Join(" or ", pendingIds.Select(id => $"{thirdParty.PrimaryIdAttribute} eq {id:D}"));
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{thirdParty.EntitySetName}?$select={thirdParty.PrimaryIdAttribute},{thirdParty.PrimaryNameAttribute},{documentField},statecode&$filter=statecode eq 0 and ({filter})", token);
        return rows.Select(row =>
        {
            var thirdPartyId = GuidValue(row, thirdParty.PrimaryIdAttribute);
            return new SecurityUserDetail(new(null,
                StringValue(row, thirdParty.PrimaryNameAttribute) ?? candidates[thirdPartyId],
                candidates[thirdPartyId], null, thirdPartyId, StringValue(row, documentField), null, true,
                "PENDING_FIRST_ACCESS"), []);
        }).ToArray();
    }

    public async Task<IReadOnlyList<SecurityRoleItem>> ListRolesAsync(CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var role = await DataverseMetadataResolver.TableAsync(client, RoleTable, token);
        var permission = await DataverseMetadataResolver.TableAsync(client, PermissionTable, token);
        var rolePermission = await DataverseMetadataResolver.TableAsync(client, RolePermissionTable, token);
        var userRole = await DataverseMetadataResolver.TableAsync(client, UserRoleTable, token);
        var rolePermissionRole = rolePermission.Attribute("gaia_Rol");
        var rolePermissionPermission = rolePermission.Attribute("gaia_Permiso");
        var userRoleRole = userRole.RelationshipTo(RoleTable).ReferencingAttribute;
        var userRoleUser = userRole.RelationshipTo(UserTable).ReferencingAttribute;
        var startField = userRole.Attribute("gaia_FechaInicio");
        var endField = userRole.Attribute("gaia_FechaFin");

        var roleRowsTask = DataverseJson.ReadAllAsync(client, $"{role.EntitySetName}?$select={role.PrimaryIdAttribute},{role.Attribute("gaia_Codigo")},{role.Attribute("gaia_Nombre")},{role.Attribute("gaia_Descripcion")},{role.Attribute("gaia_EsSistema")},statecode&$orderby={role.Attribute("gaia_Nombre")}", token);
        var permissionRowsTask = DataverseJson.ReadAllAsync(client, $"{permission.EntitySetName}?$select={permission.PrimaryIdAttribute},{permission.Attribute("gaia_Codigo")}&$filter=statecode eq 0", token);
        var rolePermissionRowsTask = DataverseJson.ReadAllAsync(client, $"{rolePermission.EntitySetName}?$select=_{rolePermissionRole}_value,_{rolePermissionPermission}_value&$filter=statecode eq 0", token);
        var userRoleRowsTask = DataverseJson.ReadAllAsync(client, $"{userRole.EntitySetName}?$select=_{userRoleRole}_value,_{userRoleUser}_value,{startField},{endField}&$filter=statecode eq 0", token);
        await Task.WhenAll(roleRowsTask, permissionRowsTask, rolePermissionRowsTask, userRoleRowsTask);

        var permissionCodes = permissionRowsTask.Result.ToDictionary(row => GuidValue(row, permission.PrimaryIdAttribute), row => StringValue(row, permission.Attribute("gaia_Codigo")) ?? "");
        var permissionsByRole = rolePermissionRowsTask.Result
            .Where(row => OptionalGuid(row, $"_{rolePermissionRole}_value").HasValue)
            .GroupBy(row => OptionalGuid(row, $"_{rolePermissionRole}_value")!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group
                .Select(row => OptionalGuid(row, $"_{rolePermissionPermission}_value"))
                .Where(id => id.HasValue && permissionCodes.ContainsKey(id.Value))
                .Select(id => permissionCodes[id!.Value]).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var usersByRole = userRoleRowsTask.Result
            .Where(row => OptionalGuid(row, $"_{userRoleRole}_value").HasValue && OptionalGuid(row, $"_{userRoleUser}_value").HasValue)
            .Where(row => OptionalDateOnly(row, startField) is { } start && start <= today && (OptionalDateOnly(row, endField) is not { } end || end >= today))
            .GroupBy(row => OptionalGuid(row, $"_{userRoleRole}_value")!.Value)
            .ToDictionary(group => group.Key, group => group.Select(row => OptionalGuid(row, $"_{userRoleUser}_value")!.Value).Distinct().Count());

        return roleRowsTask.Result.Select(row =>
        {
            var id = GuidValue(row, role.PrimaryIdAttribute);
            return new SecurityRoleItem(id, StringValue(row, role.Attribute("gaia_Codigo")) ?? "", StringValue(row, role.Attribute("gaia_Nombre")) ?? "", StringValue(row, role.Attribute("gaia_Descripcion")), BoolValue(row, role.Attribute("gaia_EsSistema")), (DataverseJson.OptionalInt32(row, "statecode") ?? 0) == 0, usersByRole.GetValueOrDefault(id), permissionsByRole.GetValueOrDefault(id) ?? []);
        }).ToArray();
    }

    public async Task<IReadOnlyList<SecurityModuleItem>> ListModulesAsync(CancellationToken token)
    {
        var client=await clientFactory.CreateAsync(); var m=await DataverseMetadataResolver.TableAsync(client,ModuleTable,token); var type=m.Attribute("gaia_Tipodemodulo"); var visible=m.OptionalAttribute("gaia_Visiblenavegacion"); var route=m.Attribute("gaia_Ruta");
        var optionalVisible=visible is null?"":$",{visible}"; var rows=await DataverseJson.ReadAllAsync(client,$"{m.EntitySetName}?$select={m.PrimaryIdAttribute},{m.Attribute("gaia_Codigo")},{m.Attribute("gaia_Nombre")},{m.Attribute("gaia_Descripcion")},{type},_{m.Attribute("gaia_Modulopadre")}_value,{route},{m.Attribute("gaia_Icono")},{m.Attribute("gaia_Orden")}{optionalVisible},statecode&$orderby={m.Attribute("gaia_Orden")}",token);
        return rows.Select(x=>new SecurityModuleItem(GuidValue(x,m.PrimaryIdAttribute),StringValue(x,m.Attribute("gaia_Codigo"))??"",StringValue(x,m.Attribute("gaia_Nombre"))??"",StringValue(x,m.Attribute("gaia_Descripcion")),Formatted(x,type)??"",OptionalGuid(x,$"_{m.Attribute("gaia_Modulopadre")}_value"),StringValue(x,route),StringValue(x,m.Attribute("gaia_Icono")),DataverseJson.OptionalInt32(x,m.Attribute("gaia_Orden"))??0,visible is null?StringValue(x,route) is not null:BoolValue(x,visible),visible is not null,(DataverseJson.OptionalInt32(x,"statecode")??0)==0)).ToArray();
    }

    public async Task<IReadOnlyList<SecurityPermissionItem>> ListPermissionsAsync(CancellationToken token)
    {
        var client=await clientFactory.CreateAsync(); var p=await DataverseMetadataResolver.TableAsync(client,PermissionTable,token); var action=p.Attribute("gaia_Accion"); var module=p.Attribute("gaia_ModuloPermiso");
        var rows=await DataverseJson.ReadAllAsync(client,$"{p.EntitySetName}?$select={p.PrimaryIdAttribute},{p.Attribute("gaia_Codigo")},{p.Attribute("gaia_Nombre")},{action},_{module}_value,statecode&$orderby={p.Attribute("gaia_Codigo")}",token);
        return rows.Select(x=>new SecurityPermissionItem(GuidValue(x,p.PrimaryIdAttribute),StringValue(x,p.Attribute("gaia_Codigo"))??"",StringValue(x,p.Attribute("gaia_Nombre"))??"",Formatted(x,action)??"",OptionalGuid(x,$"_{module}_value")??Guid.Empty,(DataverseJson.OptionalInt32(x,"statecode")??0)==0)).ToArray();
    }

    public async Task<Guid> UpsertRoleAsync(Guid? id, RoleWriteRequest request, CancellationToken token)
    {
        var code=request.Code.Trim().ToUpperInvariant(); var name=request.Name.Trim();
        if(string.IsNullOrWhiteSpace(code)||code.Length>30)throw new SecurityRoleValidationException("El código del rol es obligatorio y admite máximo 30 caracteres.");
        if(string.IsNullOrWhiteSpace(name))throw new SecurityRoleValidationException("El nombre del rol es obligatorio.");
        var client=await clientFactory.CreateAsync(); var m=await DataverseMetadataResolver.TableAsync(client,RoleTable,token); var codeField=m.Attribute("gaia_Codigo");
        var matches=await DataverseJson.ReadAllAsync(client,$"{m.EntitySetName}?$select={m.PrimaryIdAttribute},{m.Attribute("gaia_EsSistema")}&$filter={codeField} eq '{Escape(code)}'&$top=2",token);
        if(!id.HasValue&&matches.Count>0)throw new SecurityRoleConflictException("Ya existe un rol con el mismo identificador técnico.");
        if(id.HasValue)
        {
            var current=await DataverseMetadataResolver.ReadOneAsync(client,$"{m.EntitySetName}({id.Value:D})?$select={m.Attribute("gaia_EsSistema")}",token);
            if(current is null)throw new KeyNotFoundException("El rol solicitado no existe.");
            if(BoolValue(current.Value,m.Attribute("gaia_EsSistema")))throw new SecurityRoleValidationException("Los datos maestros y el estado de un rol del sistema no pueden modificarse.");
            if(matches.Any(row=>GuidValue(row,m.PrimaryIdAttribute)!=id.Value))throw new SecurityRoleConflictException("Ya existe otro rol con el mismo identificador técnico.");
        }
        var payload=new Dictionary<string,object?>{{codeField,code},{m.Attribute("gaia_Nombre"),name},{m.Attribute("gaia_Descripcion"),request.Description},{"statecode",request.IsActive?0:1}};
        var result=id.HasValue?await PatchReturn(client,m.EntitySetName,id.Value,payload,token):await Post(client,m.EntitySetName,payload,token); InvalidateAll(); return result;
    }
    public async Task SetRolePermissionsAsync(Guid roleId, RolePermissionsRequest request, CancellationToken token)
    { var client=await clientFactory.CreateAsync(); var rp=await DataverseMetadataResolver.TableAsync(client,RolePermissionTable,token); var role=await DataverseMetadataResolver.TableAsync(client,RoleTable,token); var permission=await DataverseMetadataResolver.TableAsync(client,PermissionTable,token); var roleField=rp.Attribute("gaia_Rol"); var current=await DataverseJson.ReadAllAsync(client,$"{rp.EntitySetName}?$select={rp.PrimaryIdAttribute},_{rp.Attribute("gaia_Permiso")}_value&$filter=_{roleField}_value eq {roleId:D} and statecode eq 0",token); var wanted=request.PermissionIds.ToHashSet(); foreach(var row in current.Where(x=>!wanted.Contains(OptionalGuid(x,$"_{rp.Attribute("gaia_Permiso")}_value")??Guid.Empty))) await Patch(client,$"{rp.EntitySetName}({GuidValue(row,rp.PrimaryIdAttribute):D})",new(){{"statecode",1}},token); await EnsureRolePermissions(client,rp,role,permission,roleId,wanted,token); InvalidateAll(); }
    public async Task<Guid> AssignUserRoleAsync(Guid userId, UserRoleWriteRequest request, CancellationToken token)
    {
        SecurityAssignmentRules.ValidatePeriod(request.StartDate, request.EndDate);
        var lockKey = userId.ToString("D");
        var gate = AssignmentLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            var client = await clientFactory.CreateAsync();
            await EnsureActiveRecordExists(client, UserTable, userId, "Usuario Aplicación", token);
            await EnsureActiveRecordExists(client, RoleTable, request.RoleId, "Rol", token);
            await EnsureNoOverlappingUserRole(client, userId, request.RoleId, request.StartDate, request.EndDate, null, token);
            await EnsureNoRedundantBaseRole(client, userId, request.RoleId, request.StartDate, request.EndDate, token);
            var id = await CreateUserRole(client, userId, request.RoleId, request.StartDate, request.EndDate, request.Observations, token);
            InvalidateAll();
            return id;
        }
        finally { gate.Release(); }
    }
    private static async Task EnsureNoRedundantBaseRole(HttpClient client, Guid userId, Guid requestedRoleId,
        DateOnly startDate, DateOnly? endDate, CancellationToken token)
    {
        var role = await DataverseMetadataResolver.TableAsync(client, RoleTable, token);
        var codeField = role.Attribute("gaia_Codigo");
        var requested = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{role.EntitySetName}({requestedRoleId:D})?$select={codeField}", token);
        var requestedCode = requested is null ? null : StringValue(requested.Value, codeField)?.ToUpperInvariant();
        if (requestedCode is null || !SecurityAssignmentRules.AreRedundantBaseRoles(requestedCode, requestedCode == "ADMIN" ? "CONSULTA" : "ADMIN")) return;
        var otherCode = requestedCode == "ADMIN" ? "CONSULTA" : "ADMIN";
        var otherRows = await DataverseJson.ReadAllAsync(client,
            $"{role.EntitySetName}?$select={role.PrimaryIdAttribute}&$filter={codeField} eq '{otherCode}' and statecode eq 0&$top=1", token);
        if (otherRows.Count == 0) return;
        var otherRoleId = GuidValue(otherRows[0], role.PrimaryIdAttribute);
        var overlaps = await FindOverlappingUserRoles(client, userId, otherRoleId, startDate, endDate, null, token);
        if (overlaps.Count > 0)
            throw new SecurityAssignmentConflictException($"El rol {requestedCode} no puede estar vigente simultáneamente con {otherCode}. Finaliza primero la asignación actual para conservar el histórico.");
    }
    public async Task EndUserRoleAsync(Guid userId, Guid assignmentId, DateOnly endDate, CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var ur = await DataverseMetadataResolver.TableAsync(client, UserRoleTable, token);
        var userLookup = ur.RelationshipTo(UserTable).ReferencingAttribute;
        var startField = ur.Attribute("gaia_FechaInicio");
        var row = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{ur.EntitySetName}({assignmentId:D})?$select=_{userLookup}_value,{startField},statecode", token);
        if (row is null || OptionalGuid(row.Value, $"_{userLookup}_value") != userId)
            throw new KeyNotFoundException("Asignación no encontrada.");
        if ((DataverseJson.OptionalInt32(row.Value, "statecode") ?? 0) != 0)
            throw new SecurityAssignmentValidationException("La asignación ya está inactiva.");
        var startDate = OptionalDateOnly(row.Value, startField)
            ?? throw new SecurityAssignmentValidationException("La asignación no contiene una fecha inicial válida.");
        SecurityAssignmentRules.ValidatePeriod(startDate, endDate);
        await Patch(client, $"{ur.EntitySetName}({assignmentId:D})",
            new()
            {
                { ur.Attribute("gaia_FechaFin"), endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                { "statecode", 1 },
                { "statuscode", 2 }
            }, token);
        var updated = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{ur.EntitySetName}({assignmentId:D})?$select={ur.Attribute("gaia_FechaFin")},statecode,statuscode", token);
        if (updated is null || (DataverseJson.OptionalInt32(updated.Value, "statecode") ?? 0) == 0)
            throw new SecurityAssignmentValidationException("Dataverse no confirmó la finalización de la asignación. El rol continúa vigente.");
        InvalidateAll();
    }
    public async Task<Guid> UpsertModuleAsync(Guid? id, ModuleWriteRequest request, CancellationToken token)
    {
        var code=request.Code.Trim().ToUpperInvariant(); var name=request.Name.Trim();
        if(string.IsNullOrWhiteSpace(code))throw new SecurityModuleValidationException("El identificador técnico no pudo generarse.");
        if(string.IsNullOrWhiteSpace(name))throw new SecurityModuleValidationException("El nombre funcional es obligatorio.");
        if(request.Order<0)throw new SecurityModuleValidationException("El orden visual no puede ser negativo.");
        var route=string.IsNullOrWhiteSpace(request.Route)?null:request.Route.Trim();
        if(route is { Length: > 3000 })throw new SecurityModuleValidationException("La ruta no puede superar 3000 caracteres.");
        if(route is not null&&!IsValidModuleRoute(route))throw new SecurityModuleValidationException("La ruta debe comenzar por / o ser un enlace completo HTTP/HTTPS.");
        if(code.StartsWith("INT.APP.",StringComparison.OrdinalIgnoreCase)&&route is null)throw new SecurityModuleValidationException("Las aplicaciones deben tener una ruta o enlace configurado.");
        var isRoot=Normalize(request.Type)==Normalize("MÓDULO");
        if(isRoot&&request.ParentId.HasValue)throw new SecurityModuleValidationException("Un módulo principal no puede depender de otro elemento.");
        if(!isRoot&&!request.ParentId.HasValue)throw new SecurityModuleValidationException("Los submódulos y funcionalidades deben tener un elemento padre.");

        var client=await clientFactory.CreateAsync(); var m=await DataverseMetadataResolver.TableAsync(client,ModuleTable,token); var typeField=m.Attribute("gaia_Tipodemodulo"); var types=await DataverseMetadataResolver.ChoicesAsync(client,ModuleTable,typeField,token); var typeValue=Choice(types,request.Type);
        var codeField=m.Attribute("gaia_Codigo"); var parentField=m.Attribute("gaia_Modulopadre");
        var rows=await DataverseJson.ReadAllAsync(client,$"{m.EntitySetName}?$select={m.PrimaryIdAttribute},{codeField},{typeField},_{parentField}_value,statecode",token);
        var current=id.HasValue?rows.FirstOrDefault(row=>GuidValue(row,m.PrimaryIdAttribute)==id.Value):default;
        if(id.HasValue&&current.ValueKind==JsonValueKind.Undefined)throw new KeyNotFoundException("El elemento solicitado no existe.");
        if(id.HasValue&&!string.Equals(StringValue(current,codeField),code,StringComparison.OrdinalIgnoreCase))throw new SecurityModuleValidationException("El identificador técnico de un elemento existente no puede modificarse.");
        if(rows.Any(row=>string.Equals(StringValue(row,codeField),code,StringComparison.OrdinalIgnoreCase)&&(!id.HasValue||GuidValue(row,m.PrimaryIdAttribute)!=id.Value)))throw new SecurityModuleConflictException("Ya existe un elemento con el mismo identificador técnico.");
        if(request.ParentId.HasValue)
        {
            var parent=rows.FirstOrDefault(row=>GuidValue(row,m.PrimaryIdAttribute)==request.ParentId.Value);
            if(parent.ValueKind==JsonValueKind.Undefined)throw new SecurityModuleValidationException("El elemento padre seleccionado no existe.");
            if((DataverseJson.OptionalInt32(parent,"statecode")??0)!=0)throw new SecurityModuleValidationException("El elemento padre seleccionado está inactivo.");
            var parentType=DataverseJson.OptionalInt32(parent,typeField);
            if(parentType==Choice(types,"FUNCIONALIDAD"))throw new SecurityModuleValidationException("Una funcionalidad no puede contener elementos hijos.");
            if(typeValue==Choice(types,"SUBMÓDULO")&&parentType!=Choice(types,"MÓDULO"))throw new SecurityModuleValidationException("Un submódulo debe depender directamente de un módulo principal.");
        }
        if(id.HasValue&&!request.IsActive&&rows.Any(row=>OptionalGuid(row,$"_{parentField}_value")==id.Value&&(DataverseJson.OptionalInt32(row,"statecode")??0)==0))throw new SecurityModuleValidationException("Inactiva primero los elementos hijos activos.");
        var parents=rows.ToDictionary(row=>GuidValue(row,m.PrimaryIdAttribute),row=>OptionalGuid(row,$"_{parentField}_value"));
        SecurityModuleRules.ValidateHierarchy(id,request.ParentId,parents);

        var payload=new Dictionary<string,object?>{{codeField,code},{m.Attribute("gaia_Nombre"),name},{m.Attribute("gaia_Descripcion"),request.Description},{typeField,typeValue},{m.Attribute("gaia_Ruta"),route},{m.Attribute("gaia_Icono"),string.IsNullOrWhiteSpace(request.Icon)?null:request.Icon.Trim()},{m.Attribute("gaia_Orden"),request.Order},{"statecode",request.IsActive?0:1}};
        var visible=m.OptionalAttribute("gaia_Visiblenavegacion");if(visible is not null)payload[visible]=request.Visible;
        var relation=m.Relationship("gaia_Modulopadre",ModuleTable);payload[$"{relation.NavigationProperty}@odata.bind"]=request.ParentId.HasValue?$"/{m.EntitySetName}({request.ParentId:D})":null;
        var result=id.HasValue?await PatchReturn(client,m.EntitySetName,id.Value,payload,token):await Post(client,m.EntitySetName,payload,token); InvalidateAll(); return result;
    }

    private static bool IsValidModuleRoute(string route)
    {
        if(route.StartsWith('/')&&!route.StartsWith("//",StringComparison.Ordinal))return true;
        return Uri.TryCreate(route,UriKind.Absolute,out var uri)&&(uri.Scheme==Uri.UriSchemeHttp||uri.Scheme==Uri.UriSchemeHttps);
    }

    private static IReadOnlyList<ModuleSeed> ModuleSeeds() =>
    [
        new("INTRANET","Intranet Gaia","MÓDULO",null,"/intranet","intranet",5,true,"Espacio institucional de los colaboradores."),
        new("INT.INICIO","Inicio","SUBMÓDULO","INTRANET","/intranet","home",6,true,"Inicio de la Intranet Gaia."),
        new("INT.PERSONAS","Personas","SUBMÓDULO","INTRANET","/intranet/personas","people",7,true,"Directorio corporativo autorizado."),
        new("INT.CALENDARIO","Calendario","SUBMÓDULO","INTRANET","/intranet/calendario","calendar",8,true,"Agenda y actividades institucionales."),
        new("INT.APLICACIONES","Aplicaciones","SUBMÓDULO","INTRANET","/intranet/aplicaciones","applications",9,true,"Catálogo de aplicaciones autorizadas."),
        new("INT.HELPDESK","Helpdesk","SUBMÓDULO","INTRANET","/intranet/helpdesk","helpdesk",10,true,"Autoservicio del colaborador."),
        new("INT.APP.ADMINCORE","AdminCore","FUNCIONALIDAD","INT.APLICACIONES","/admincore","admincore",11,true,"Espacio de administración de la plataforma Gaia."),
        new("INICIO","Inicio","MÓDULO",null,"/admincore","home",10,true,"Página inicial de la plataforma."),
        new("ORG","Organización","MÓDULO",null,"/organizacion","organization",20,true,"Estructura organizacional."),
        new("ORG.ORGANIGRAMA","Organigrama","SUBMÓDULO","ORG","/organizacion?tab=organigrama","hierarchy",21,true,"Diagrama organizacional."),
        new("ORG.UNIDADES","Unidades","SUBMÓDULO","ORG","/organizacion?tab=unidades","units",22,true,"Unidades organizacionales."),
        new("ORG.ASIGNACIONES","Asignaciones","SUBMÓDULO","ORG","/organizacion?tab=asignaciones","assignments",23,true,"Asignaciones de personas a cargos y unidades."),
        new("ORG.CARGOS","Cargos","SUBMÓDULO","ORG","/organizacion?tab=cargos","positions",24,true,"Cargos institucionales."),
        new("ORG.SEDES_TIPOS","Sedes y tipos","SUBMÓDULO","ORG","/organizacion?tab=catalogos","catalog",25,true,"Catálogos organizacionales."),
        new("TH","Talento Humano","MÓDULO",null,"/talento-humano/colaboradores","people",30,true,"Gestión de colaboradores."),
        new("TH.COLABORADORES","Colaboradores","SUBMÓDULO","TH","/talento-humano/colaboradores","collaborators",31,true,"Directorio de colaboradores."),
        new("TH.VINCULACIONES","Vinculaciones","SUBMÓDULO","TH","/talento-humano/vinculaciones","links",32,true,"Gestión de asignaciones organizacionales."),
        new("TH.COLABORADORES.IMPORTAR","Carga administrativa","FUNCIONALIDAD","TH.COLABORADORES",null,null,31,false,"Herramienta técnica de importación."),
        new("TH.COLABORADORES.INFO","Información del colaborador","FUNCIONALIDAD","TH.COLABORADORES",null,null,32,false,"Información personal."),
        new("TH.COLABORADORES.CORREOS","Correos","FUNCIONALIDAD","TH.COLABORADORES",null,null,33,false,"Correos del colaborador."),
        new("TH.COLABORADORES.TELEFONOS","Teléfonos","FUNCIONALIDAD","TH.COLABORADORES",null,null,34,false,"Teléfonos del colaborador."),
        new("INV","Inventario","MÓDULO",null,"/inventario","inventory",40,true,"Inventario institucional."),
        new("INV.ASIGNAR","Asignar inventario","FUNCIONALIDAD","INV",null,null,41,false,"Asignación de elementos."),
        new("INV.IMPORTAR","Importar inventario","FUNCIONALIDAD","INV",null,null,42,false,"Carga administrativa de inventario."),
        new("COM","Comunicaciones","MÓDULO",null,"/comunicaciones/eventos","communications",50,true,"Administración del contenido institucional visible en la Intranet."),
        new("COM.EVENTOS","Eventos","SUBMÓDULO","COM","/comunicaciones/eventos","calendar",51,true,"Agenda y eventos institucionales."),
        new("COM.TIPOS_EVENTO","Tipos de evento","SUBMÓDULO","COM","/comunicaciones/tipos-evento","tags",52,true,"Clasificación y presentación visual de eventos."),
        new("COM.DESTACADOS","Destacados","SUBMÓDULO","COM","/comunicaciones/destacados","image",53,true,"Piezas destacadas y promociones visibles en la portada de la Intranet."),
        new("TI","Seguridad","MÓDULO",null,"/seguridad","security",90,true,"Administración de identidad, roles, permisos y recursos protegidos."),
        new("TI.USUARIOS","Usuarios","SUBMÓDULO","TI","/seguridad/usuarios","users",91,true,"Usuarios y roles."),
        new("TI.ROLES","Roles y permisos","SUBMÓDULO","TI","/seguridad/roles","roles",92,true,"Roles y permisos."),
        new("TI.MODULOS","Módulos","SUBMÓDULO","TI","/seguridad/modulos","modules",93,true,"Catálogo de recursos protegidos.")
    ];

    private static async Task<(Guid? Id,string? Document)> MatchThirdPartyAsync(
        HttpClient client, string email, string name, CancellationToken token)
    {
        var emailMeta = await DataverseMetadataResolver.TableAsync(client, EmailTable, token);
        var parent = emailMeta.Attribute("gaia_Tercero");
        var address = emailMeta.Attribute("gaia_Correoelectronico");
        var normalizedEmail = email.Trim();
        var exactRows = await DataverseJson.ReadAllAsync(client,
            $"{emailMeta.EntitySetName}?$select=_{parent}_value&$filter={address} eq '{Escape(normalizedEmail)}' and statecode eq 0&$top=3", token);
        var ids = DistinctThirdPartyIds(exactRows, parent);

        // Some imported addresses contain harmless surrounding whitespace. Dataverse string
        // equality does not normalize it, so resolve it locally before considering another key.
        if (ids.Length == 0)
        {
            var activeEmails = await DataverseJson.ReadAllAsync(client,
                $"{emailMeta.EntitySetName}?$select={address},_{parent}_value&$filter=statecode eq 0", token);
            ids = DistinctThirdPartyIds(activeEmails
                .Where(row => string.Equals(StringValue(row, address)?.Trim(), normalizedEmail,
                    StringComparison.OrdinalIgnoreCase)), parent);
        }

        // Entra and the personnel workbook can contain different institutional aliases. A
        // normalized full name is accepted only when it identifies exactly one active Tercero.
        if (ids.Length == 0 && !string.IsNullOrWhiteSpace(name))
        {
            var thirdParty = await DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
            var activePeople = await DataverseJson.ReadAllAsync(client,
                $"{thirdParty.EntitySetName}?$select={thirdParty.PrimaryIdAttribute},{thirdParty.PrimaryNameAttribute}&$filter=statecode eq 0", token);
            ids = activePeople
                .Where(row => Normalize(StringValue(row, thirdParty.PrimaryNameAttribute) ?? string.Empty) == Normalize(name))
                .Select(row => GuidValue(row, thirdParty.PrimaryIdAttribute))
                .Distinct()
                .ToArray();
        }

        if (ids.Length != 1) return (null, null);
        return (ids[0], await ReadDocumentAsync(client, ids[0], token));
    }

    private static Guid[] DistinctThirdPartyIds(IEnumerable<JsonElement> rows, string parentAttribute) =>
        rows.Select(row => OptionalGuid(row, $"_{parentAttribute}_value"))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
    private static async Task<string?> ReadDocumentAsync(HttpClient client,Guid id,CancellationToken token){var t=await DataverseMetadataResolver.TableAsync(client,ThirdPartyTable,token);var field=t.Attribute("gaia_NumeroDocumento");var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{t.EntitySetName}({id:D})?$select={field}",token);return row is null?null:StringValue(row.Value,field);}
    private static async Task<Guid?> FindThirdPartyByDocumentAsync(HttpClient client,string document,CancellationToken token){var t=await DataverseMetadataResolver.TableAsync(client,ThirdPartyTable,token);var f=t.Attribute("gaia_NumeroDocumento");var rows=await DataverseJson.ReadAllAsync(client,$"{t.EntitySetName}?$select={t.PrimaryIdAttribute}&$filter={f} eq '{Escape(document)}' and statecode eq 0&$top=2",token);return rows.Count==1?GuidValue(rows[0],t.PrimaryIdAttribute):null;}
    private static async Task<Guid?> FindRoleId(HttpClient client,string code,CancellationToken token){var r=await DataverseMetadataResolver.TableAsync(client,RoleTable,token);var rows=await DataverseJson.ReadAllAsync(client,$"{r.EntitySetName}?$select={r.PrimaryIdAttribute}&$filter={r.Attribute("gaia_Codigo")} eq '{Escape(code)}' and statecode eq 0&$top=1",token);return rows.Count==1?GuidValue(rows[0],r.PrimaryIdAttribute):null;}
    private static async Task EnsureAdministratorPermissions(HttpClient client, Guid administratorRoleId, CancellationToken token)
    {
        var role = await DataverseMetadataResolver.TableAsync(client, RoleTable, token);
        var permission = await DataverseMetadataResolver.TableAsync(client, PermissionTable, token);
        var rolePermission = await DataverseMetadataResolver.TableAsync(client, RolePermissionTable, token);
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{permission.EntitySetName}?$select={permission.PrimaryIdAttribute}&$filter=statecode eq 0", token);
        var permissionIds = rows.Select(row => GuidValue(row, permission.PrimaryIdAttribute)).ToArray();
        await EnsureRolePermissions(client, rolePermission, role, permission, administratorRoleId, permissionIds, token);
    }
    private static async Task<IReadOnlyList<SecurityUserRoleItem>> ReadUserRoles(HttpClient client,Guid userId,CancellationToken token){var ur=await DataverseMetadataResolver.TableAsync(client,UserRoleTable,token);var r=await DataverseMetadataResolver.TableAsync(client,RoleTable,token);var roleField=ur.RelationshipTo(RoleTable).ReferencingAttribute;var userField=ur.RelationshipTo(UserTable).ReferencingAttribute;var rows=await DataverseJson.ReadAllAsync(client,$"{ur.EntitySetName}?$select={ur.PrimaryIdAttribute},_{roleField}_value,{ur.Attribute("gaia_FechaInicio")},{ur.Attribute("gaia_FechaFin")},statecode&$filter=_{userField}_value eq {userId:D}",token);var result=new List<SecurityUserRoleItem>();foreach(var x in rows){var roleId=OptionalGuid(x,$"_{roleField}_value")??Guid.Empty;var rr=await DataverseMetadataResolver.ReadOneAsync(client,$"{r.EntitySetName}({roleId:D})?$select={r.Attribute("gaia_Codigo")},{r.Attribute("gaia_Nombre")}",token);result.Add(new(GuidValue(x,ur.PrimaryIdAttribute),roleId,rr is null?"":StringValue(rr.Value,r.Attribute("gaia_Codigo"))??"",rr is null?"":StringValue(rr.Value,r.Attribute("gaia_Nombre"))??"",OptionalDateOnly(x,ur.Attribute("gaia_FechaInicio"))??DateOnly.MinValue,OptionalDateOnly(x,ur.Attribute("gaia_FechaFin")),(DataverseJson.OptionalInt32(x,"statecode")??0)==0));}return result;}
    private static async Task<IReadOnlyList<string>> ReadRolePermissionCodes(HttpClient client,Guid roleId,CancellationToken token){var rp=await DataverseMetadataResolver.TableAsync(client,RolePermissionTable,token);var p=await DataverseMetadataResolver.TableAsync(client,PermissionTable,token);var lookup=rp.Attribute("gaia_Permiso");var rows=await DataverseJson.ReadAllAsync(client,$"{rp.EntitySetName}?$select=_{lookup}_value&$filter=_{rp.Attribute("gaia_Rol")}_value eq {roleId:D} and statecode eq 0",token);var ids=rows.Select(x=>OptionalGuid(x,$"_{lookup}_value")).Where(x=>x.HasValue).Select(x=>x!.Value).ToArray();if(ids.Length==0)return[];var filter=string.Join(" or ",ids.Select(x=>$"{p.PrimaryIdAttribute} eq {x:D}"));return(await DataverseJson.ReadAllAsync(client,$"{p.EntitySetName}?$select={p.Attribute("gaia_Codigo")}&$filter={filter}",token)).Select(x=>StringValue(x,p.Attribute("gaia_Codigo"))).Where(x=>!string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray();}
    private static async Task<Guid> UpsertRole(HttpClient client,DataverseTableMetadata meta,string code,string name,string description,bool system,CancellationToken token)=>await UpsertByCode(client,meta,code,new(){{meta.Attribute("gaia_Codigo"),code},{meta.Attribute("gaia_Nombre"),name},{meta.Attribute("gaia_Descripcion"),description},{meta.Attribute("gaia_EsSistema"),system},{"statecode",0}},token);
    private static async Task EnsureRolePermissions(HttpClient client,DataverseTableMetadata rp,DataverseTableMetadata role,DataverseTableMetadata permission,Guid roleId,IEnumerable<Guid> permissionIds,CancellationToken token){var roleRel=rp.Relationship("gaia_Rol",RoleTable);var permRel=rp.Relationship("gaia_Permiso",PermissionTable);foreach(var pid in permissionIds.Distinct()){var rows=await DataverseJson.ReadAllAsync(client,$"{rp.EntitySetName}?$select={rp.PrimaryIdAttribute},statecode&$filter=_{rp.Attribute("gaia_Rol")}_value eq {roleId:D} and _{rp.Attribute("gaia_Permiso")}_value eq {pid:D}&$top=1",token);if(rows.Count>0){if((DataverseJson.OptionalInt32(rows[0],"statecode")??0)!=0)await Patch(client,$"{rp.EntitySetName}({GuidValue(rows[0],rp.PrimaryIdAttribute):D})",new(){{"statecode",0}},token);continue;}await Post(client,rp.EntitySetName,new(){{rp.PrimaryNameAttribute,$"{roleId:D}:{pid:D}"},{$"{roleRel.NavigationProperty}@odata.bind",$"/{role.EntitySetName}({roleId:D})"},{$"{permRel.NavigationProperty}@odata.bind",$"/{permission.EntitySetName}({pid:D})"},{"statecode",0}},token);}}
    private static async Task EnsureUserRole(HttpClient client,Guid userId,Guid roleId,string observations,CancellationToken token)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await FindOverlappingUserRoles(client, userId, roleId, today, null, null, token);
        if (existing.Count > 0) return;
        await CreateUserRole(client,userId,roleId,today,null,observations,token);
    }

    private static async Task EnsureNoOverlappingUserRole(HttpClient client, Guid userId, Guid roleId,
        DateOnly startDate, DateOnly? endDate, Guid? excludedAssignmentId, CancellationToken token)
    {
        var overlaps = await FindOverlappingUserRoles(client, userId, roleId, startDate, endDate, excludedAssignmentId, token);
        if (overlaps.Count > 0)
            throw new SecurityAssignmentConflictException("El usuario ya tiene este rol asignado durante un período que se superpone.");
    }

    private static async Task<IReadOnlyList<Guid>> FindOverlappingUserRoles(HttpClient client, Guid userId, Guid roleId,
        DateOnly startDate, DateOnly? endDate, Guid? excludedAssignmentId, CancellationToken token)
    {
        var ur = await DataverseMetadataResolver.TableAsync(client, UserRoleTable, token);
        var userField = ur.RelationshipTo(UserTable).ReferencingAttribute;
        var roleField = ur.RelationshipTo(RoleTable).ReferencingAttribute;
        var startField = ur.Attribute("gaia_FechaInicio");
        var endField = ur.Attribute("gaia_FechaFin");
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{ur.EntitySetName}?$select={ur.PrimaryIdAttribute},{startField},{endField},statecode&$filter=_{userField}_value eq {userId:D} and _{roleField}_value eq {roleId:D} and statecode eq 0", token);
        return rows
            .Where(row => !excludedAssignmentId.HasValue || GuidValue(row, ur.PrimaryIdAttribute) != excludedAssignmentId.Value)
            .Where(row => OptionalDateOnly(row, startField) is { } existingStart &&
                SecurityAssignmentRules.Overlaps(existingStart, OptionalDateOnly(row, endField), startDate, endDate))
            .Select(row => GuidValue(row, ur.PrimaryIdAttribute))
            .ToArray();
    }

    private static async Task EnsureActiveRecordExists(HttpClient client, string logicalTable, Guid id, string functionalName, CancellationToken token)
    {
        var metadata = await DataverseMetadataResolver.TableAsync(client, logicalTable, token);
        var row = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{metadata.EntitySetName}({id:D})?$select=statecode", token);
        if (row is null) throw new KeyNotFoundException($"{functionalName} no encontrado.");
        if ((DataverseJson.OptionalInt32(row.Value, "statecode") ?? 0) != 0)
            throw new SecurityAssignmentValidationException($"{functionalName} está inactivo.");
    }
    private static async Task<Guid> CreateUserRole(HttpClient client,Guid userId,Guid roleId,DateOnly start,DateOnly? end,string? observations,CancellationToken token){var ur=await DataverseMetadataResolver.TableAsync(client,UserRoleTable,token);var u=await DataverseMetadataResolver.TableAsync(client,UserTable,token);var r=await DataverseMetadataResolver.TableAsync(client,RoleTable,token);var userRel=ur.RelationshipTo(UserTable);var roleRel=ur.RelationshipTo(RoleTable);return await Post(client,ur.EntitySetName,new(){{ur.PrimaryNameAttribute,$"{userId:D}:{roleId:D}"},{ur.Attribute("gaia_FechaInicio"),start.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)},{ur.Attribute("gaia_FechaFin"),end?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)},{ur.Attribute("gaia_Observaciones"),observations},{$"{userRel.NavigationProperty}@odata.bind",$"/{u.EntitySetName}({userId:D})"},{$"{roleRel.NavigationProperty}@odata.bind",$"/{r.EntitySetName}({roleId:D})"},{"statecode",0}},token);}
    private static async Task<Guid> UpsertByCode(HttpClient client,DataverseTableMetadata meta,string code,Dictionary<string,object?> payload,CancellationToken token){var field=meta.Attribute("gaia_Codigo");var rows=await DataverseJson.ReadAllAsync(client,$"{meta.EntitySetName}?$select={meta.PrimaryIdAttribute}&$filter={field} eq '{Escape(code.Trim().ToUpperInvariant())}'&$top=1",token);if(rows.Count==0)return await Post(client,meta.EntitySetName,payload,token);var id=GuidValue(rows[0],meta.PrimaryIdAttribute);await Patch(client,$"{meta.EntitySetName}({id:D})",payload,token);return id;}
    private static async Task<Guid> Post(HttpClient client,string set,Dictionary<string,object?> payload,CancellationToken token){using var response=await client.PostAsJsonAsync(set,payload,token);await Ensure(response,token);var header=response.Headers.TryGetValues("OData-EntityId",out var values)?values.SingleOrDefault():null;var match=header is null?null:System.Text.RegularExpressions.Regex.Match(header,@"\(([0-9a-f-]{36})\)$");if(match?.Success!=true)throw new InvalidOperationException("Dataverse creó el registro sin devolver su identificador.");return Guid.Parse(match.Groups[1].Value);}
    private static async Task<Guid> PatchReturn(HttpClient client,string set,Guid id,Dictionary<string,object?> payload,CancellationToken token){await Patch(client,$"{set}({id:D})",payload,token);return id;}
    private static async Task Patch(HttpClient client,string path,Dictionary<string,object?> payload,CancellationToken token){using var request=new HttpRequestMessage(HttpMethod.Patch,path){Content=JsonContent.Create(payload)};request.Headers.TryAddWithoutValidation("If-Match","*");using var response=await client.SendAsync(request,token);await Ensure(response,token);}
    private static async Task Ensure(HttpResponseMessage response,CancellationToken token){if(response.IsSuccessStatusCode)return;var body=await response.Content.ReadAsStringAsync(token);throw new InvalidOperationException($"Dataverse rechazó Seguridad ({(int)response.StatusCode}): {body}");}
    private static (string Oid,string Email,string Name) IdentityFrom(ClaimsPrincipal p){var oid=p.FindFirstValue("oid")??p.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");var email=(p.FindFirstValue("preferred_username")??p.FindFirstValue(ClaimTypes.Email))?.Trim().ToLowerInvariant();var name=p.FindFirstValue("name")??p.Identity?.Name;if(string.IsNullOrWhiteSpace(oid)||!Guid.TryParse(oid,out _))throw new InvalidOperationException("Microsoft Entra no entregó un Object ID válido.");if(string.IsNullOrWhiteSpace(email)||!email.EndsWith("@gaiaamazonas.org",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Solo se permiten cuentas corporativas @gaiaamazonas.org.");return(oid,email,string.IsNullOrWhiteSpace(name)?email:name);}
    private static int Choice(IReadOnlyDictionary<string,int> choices,string label){var normalized=Normalize(label);var match=choices.FirstOrDefault(x=>Normalize(x.Key)==normalized);if(string.IsNullOrEmpty(match.Key))throw new InvalidOperationException($"Dataverse no contiene la opción {label}.");return match.Value;}
    private static string Normalize(string value){var text=value.Normalize(NormalizationForm.FormD);var chars=text.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray();return new string(chars).Normalize(NormalizationForm.FormC).Replace("_","",StringComparison.Ordinal).Replace(" ","",StringComparison.Ordinal).ToUpperInvariant();}
    private static string PermissionModuleCode(string code) => code switch
    {
        "TH.COL.INFO" => "TH.COLABORADORES.INFO",
        "TH.COL.EMAIL" => "TH.COLABORADORES.CORREOS",
        "TH.COL.TEL" => "TH.COLABORADORES.TELEFONOS",
        "TH.COL.IMPORT" => "TH.COLABORADORES.IMPORTAR",
        _ => code
    };
    private static string Escape(string value)=>value.Replace("'","''",StringComparison.Ordinal);
    private static Guid GuidValue(JsonElement x,string p)=>x.TryGetProperty(p,out var v)&&Guid.TryParse(v.GetString(),out var id)?id:throw new InvalidOperationException($"Dataverse no devolvió {p}.");
    private static Guid? OptionalGuid(JsonElement x,string p)=>x.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.String&&Guid.TryParse(v.GetString(),out var id)?id:null;
    private static string? StringValue(JsonElement x,string p)=>x.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static string? Formatted(JsonElement x,string p)=>StringValue(x,$"{p}@OData.Community.Display.V1.FormattedValue");
    private static bool BoolValue(JsonElement x,string p)=>x.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.True;
    private static DateTimeOffset? OptionalDateTime(JsonElement x,string p)=>StringValue(x,p) is { } value&&DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var result)?result:null;
    private static DateOnly? OptionalDateOnly(JsonElement x,string p)=>StringValue(x,p) is { } value&&DateOnly.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var result)?result:null;
    private void Invalidate(string identity)
    {
        var key = CachePrefix + identity;
        cache.Remove(key);
        SecurityCacheKeys.TryRemove(key, out _);
    }
    private void InvalidateAll()
    {
        foreach (var key in SecurityCacheKeys.Keys)
        {
            cache.Remove(key);
            SecurityCacheKeys.TryRemove(key, out _);
        }
    }
    private sealed record ModuleSeed(string Code,string Name,string Type,string? ParentCode,string? Route,string? Icon,int Order,bool Visible,string Description);
}
