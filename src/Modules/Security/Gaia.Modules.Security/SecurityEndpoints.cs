using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Gaia.Modules.Security;
internal static class SecurityEndpoints
{
 public static void Map(IEndpointRouteBuilder endpoints)
 {
  var group=endpoints.MapGroup("/api/security").WithTags("Security").RequireAuthorization();
  group.MapGet("/me",(ClaimsPrincipal user,ISecurityStore store,CancellationToken ct)=>store.GetOrProvisionAsync(user,ct));
  group.MapGet("/metadata-audit",(ISecurityStore store,CancellationToken ct)=>store.AuditMetadataAsync(ct)).RequireAuthorization(AdminCorePermissions.TiModulosAdministrar);
  group.MapPost("/bootstrap",BootstrapAsync).RequireAuthorization("SecurityBootstrap");
  group.MapGet("/users",(ISecurityStore store,CancellationToken ct)=>store.ListUsersAsync(ct)).RequireAuthorization(AdminCorePermissions.TiUsuariosVer);
  group.MapGet("/users/preprovision-audit",(ISecurityStore store,CancellationToken ct)=>store.AuditPreprovisionAsync(ct)).RequireAuthorization(AdminCorePermissions.TiUsuariosAdministrar);
  group.MapPost("/users/preprovision",PreprovisionAsync).RequireAuthorization(AdminCorePermissions.TiUsuariosAdministrar);
  group.MapPost("/users/{userId:guid}/roles",AssignUserRoleAsync).RequireAuthorization(AdminCorePermissions.TiUsuariosAdministrar);
  group.MapPut("/users/{userId:guid}/roles/{assignmentId:guid}/end",EndUserRoleAsync).RequireAuthorization(AdminCorePermissions.TiUsuariosAdministrar);
  group.MapGet("/roles",(ISecurityStore store,CancellationToken ct)=>store.ListRolesAsync(ct)).RequireAuthorization(AdminCorePermissions.TiRolesVer);
  group.MapGet("/permissions",(ISecurityStore store,CancellationToken ct)=>store.ListPermissionsAsync(ct)).RequireAuthorization(AdminCorePermissions.TiRolesVer);
  group.MapPost("/roles",(RoleWriteRequest request,ISecurityStore store,CancellationToken ct)=>WriteRoleAsync(null,request,store,ct)).RequireAuthorization(AdminCorePermissions.TiRolesCrear);
  group.MapPut("/roles/{id:guid}",(Guid id,RoleWriteRequest request,ISecurityStore store,CancellationToken ct)=>WriteRoleAsync(id,request,store,ct)).RequireAuthorization(AdminCorePermissions.TiRolesActualizar);
  group.MapPut("/roles/{id:guid}/permissions",(Guid id,RolePermissionsRequest request,ISecurityStore store,CancellationToken ct)=>store.SetRolePermissionsAsync(id,request,ct)).RequireAuthorization(AdminCorePermissions.TiRolesAdministrar);
  group.MapGet("/modules",(ISecurityStore store,CancellationToken ct)=>store.ListModulesAsync(ct)).RequireAuthorization(AdminCorePermissions.TiModulosVer);
  group.MapPost("/modules",(ModuleWriteRequest request,ISecurityStore store,CancellationToken ct)=>WriteModuleAsync(null,request,store,ct)).RequireAuthorization(AdminCorePermissions.TiModulosCrear);
  group.MapPut("/modules/{id:guid}",(Guid id,ModuleWriteRequest request,ISecurityStore store,CancellationToken ct)=>WriteModuleAsync(id,request,store,ct)).RequireAuthorization(AdminCorePermissions.TiModulosActualizar);
 }

 private static async Task<IResult> PreprovisionAsync(ISecurityStore store,CancellationToken ct)
 {
  try { return Results.Ok(await store.PreprovisionEligibleUsersAsync(ct)); }
  catch(InvalidOperationException exception)
  { return Results.Problem(statusCode:StatusCodes.Status422UnprocessableEntity,title:"No fue posible preaprovisionar usuarios",detail:exception.Message); }
 }

 private static async Task<IResult> BootstrapAsync(ClaimsPrincipal user,ISecurityStore store,CancellationToken ct)
 {
  try { return Results.Ok(await store.BootstrapAsync(user,ct)); }
  catch(InvalidOperationException exception)
  { return Results.Problem(statusCode:StatusCodes.Status422UnprocessableEntity,title:"No fue posible inicializar Seguridad",detail:exception.Message); }
 }

 private static async Task<IResult> AssignUserRoleAsync(Guid userId,UserRoleWriteRequest request,ISecurityStore store,CancellationToken ct)
 {
  try { return Results.Ok(await store.AssignUserRoleAsync(userId,request,ct)); }
  catch(SecurityAssignmentConflictException exception)
  { return Results.Conflict(new { title="El rol ya está asignado",detail=exception.Message }); }
  catch(SecurityAssignmentValidationException exception)
  { return Results.ValidationProblem(new Dictionary<string,string[]>{{"assignment",[exception.Message]}}); }
  catch(KeyNotFoundException exception)
  { return Results.NotFound(new { title="No fue posible asignar el rol",detail=exception.Message }); }
 }

 private static async Task<IResult> EndUserRoleAsync(Guid userId,Guid assignmentId,DateOnly endDate,ISecurityStore store,CancellationToken ct)
 {
  try { await store.EndUserRoleAsync(userId,assignmentId,endDate,ct); return Results.NoContent(); }
  catch(SecurityAssignmentValidationException exception)
  { return Results.ValidationProblem(new Dictionary<string,string[]>{{"assignment",[exception.Message]}}); }
  catch(KeyNotFoundException exception)
  { return Results.NotFound(new { title="Asignación no encontrada",detail=exception.Message }); }
 }

 private static async Task<IResult> WriteRoleAsync(Guid? id,RoleWriteRequest request,ISecurityStore store,CancellationToken ct)
 {
  try { return Results.Ok(await store.UpsertRoleAsync(id,request,ct)); }
  catch(SecurityRoleConflictException exception)
  { return Results.Conflict(new { title="El rol ya existe",detail=exception.Message }); }
  catch(SecurityRoleValidationException exception)
  { return Results.ValidationProblem(new Dictionary<string,string[]>{{"role",[exception.Message]}}); }
  catch(KeyNotFoundException exception)
  { return Results.NotFound(new { title="Rol no encontrado",detail=exception.Message }); }
 }

 private static async Task<IResult> WriteModuleAsync(Guid? id,ModuleWriteRequest request,ISecurityStore store,CancellationToken ct)
 {
  try { return Results.Ok(await store.UpsertModuleAsync(id,request,ct)); }
  catch(SecurityModuleConflictException exception)
  { return Results.Conflict(new { title="El elemento ya existe",detail=exception.Message }); }
  catch(SecurityModuleValidationException exception)
  { return Results.ValidationProblem(new Dictionary<string,string[]>{{"module",[exception.Message]}}); }
  catch(KeyNotFoundException exception)
  { return Results.NotFound(new { title="Elemento no encontrado",detail=exception.Message }); }
 }
}
