using Gaia.Modules.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Gaia.Modules.Inventory;

internal static class InventoryEndpoints
{
    public static void MapUnavailable(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/api/inventory/{**path}", ["GET", "POST", "PUT", "DELETE"], () =>
            Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Inventario no está disponible",
                detail: "El módulo de Inventario todavía no cuenta con una implementación Dataverse."))
            .WithTags("Inventory")
            .RequireAuthorization();
    }

}
