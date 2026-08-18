using System.Net.Http.Headers;
using System.Text.Json;
using Gaia.Api.Infrastructure.Dataverse.Organization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

internal static class DataverseEndpoints
{
    public static IEndpointRouteBuilder MapDataverseEndpoints(this IEndpointRouteBuilder endpoints)
    {

        endpoints.MapGet("/api/dataverse/status", GetStatusAsync)
            .RequireAuthorization()
            .WithName("GetDataverseStatus")
            .WithTags("Dataverse");
        endpoints.MapGet("/api/dataverse/metadata/{logicalName}", GetTableMetadataAsync)
            .RequireAuthorization()
            .WithName("GetDataverseTableMetadata")
            .WithTags("Dataverse");
        endpoints.MapGet("/api/dataverse/organization/unit-types", GetOrganizationUnitTypesAsync)
            .RequireAuthorization()
            .WithName("GetDataverseOrganizationUnitTypes")
            .WithTags("Dataverse");
        endpoints.MapGet("/api/dataverse/organization/records", GetOrganizationRecordsAsync)
            .RequireAuthorization()
            .WithName("GetDataverseOrganizationRecords")
            .WithTags("Dataverse");
        endpoints.MapGet("/api/dataverse/organization/import/validate", ValidateOrganizationImportAsync)
            .RequireAuthorization()
            .WithName("ValidateDataverseOrganizationImport")
            .WithTags("Dataverse");
        endpoints.MapPost("/api/dataverse/organization/import", ImportOrganizationAsync)
            .RequireAuthorization()
            .WithName("ImportDataverseOrganization")
            .WithTags("Dataverse");
        return endpoints;
    }
    private static async Task<IResult> GetOrganizationUnitTypesAsync(
        ITokenAcquisition tokenAcquisition,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var scope = configuration["Dataverse:Scope"]
            ?? throw new InvalidOperationException("Dataverse:Scope is required.");

        try
        {
            var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(
                [scope],
                authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);

            var client = httpClientFactory.CreateClient("Dataverse");

            const string path =
                "gaia_tipounidadorganizacionals" +
                "?$select=" +
                "gaia_tipounidadorganizacionalid," +
                "gaia_codigo," +
                "gaia_nombre," +
                "gaia_descripcion," +
                "gaia_niveljerarquico," +
                "gaia_activo" +
                "&$orderby=gaia_nombre asc";

            using var request = new HttpRequestMessage(HttpMethod.Get, path);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            var content = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Results.Problem(
                    statusCode: (int)response.StatusCode,
                    title: "No fue posible consultar los tipos de unidad organizacional",
                    detail: ExtractDataverseError(content));
            }

            using var document = JsonDocument.Parse(content);

            return Results.Content(
                document.RootElement.GetRawText(),
                "application/json");
        }
        catch (MicrosoftIdentityWebChallengeUserException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Dataverse requiere una nueva autenticación",
                detail: exception.InnerException is MsalUiRequiredException
                    ? "Cierra la sesión de Gaia e ingresa nuevamente."
                    : "No fue posible obtener autorización para Dataverse.");
        }
    }

    private static async Task<IResult> GetOrganizationRecordsAsync(
        ITokenAcquisition tokenAcquisition,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var scope = configuration["Dataverse:Scope"]
            ?? throw new InvalidOperationException("Dataverse:Scope is required.");

        try
        {
            var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(
                [scope],
                authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);

            var client = httpClientFactory.CreateClient("Dataverse");

            const string path =
                "gaia_organizacions" +
                "?$select=" +
                "gaia_organizacionid," +
                "gaia_codigo," +
                "gaia_nombre," +
                "gaia_descripcion," +
                "gaia_nivel," +
                "gaia_fechainiciovigencia," +
                "gaia_fechafinvigencia," +
                "gaia_estransversal," +
                "statecode,statuscode" +
                "&$orderby=gaia_nivel asc,gaia_nombre asc";

            using var request = new HttpRequestMessage(HttpMethod.Get, path);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            var content = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Results.Problem(
                    statusCode: (int)response.StatusCode,
                    title: "No fue posible consultar Organización en Dataverse",
                    detail: ExtractDataverseError(content));
            }

            using var document = JsonDocument.Parse(content);

            return Results.Content(
                document.RootElement.GetRawText(),
                "application/json");
        }
        catch (MicrosoftIdentityWebChallengeUserException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Dataverse requiere una nueva autenticación",
                detail: exception.InnerException is MsalUiRequiredException
                    ? "Cierra la sesión de Gaia e ingresa nuevamente."
                    : "No fue posible obtener autorización para Dataverse.");
        }
    }

    private static async Task<IResult> GetTableMetadataAsync(
        string logicalName,
        ITokenAcquisition tokenAcquisition,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var scope = configuration["Dataverse:Scope"]
            ?? throw new InvalidOperationException("Dataverse:Scope is required.");

        try
        {
            var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(
                [scope],
                authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);

            var client = httpClientFactory.CreateClient("Dataverse");

            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return Results.BadRequest(new
                {
                    detail = "El nombre lógico de la tabla es obligatorio."
                });
            }

            var normalizedLogicalName = logicalName.Trim().ToLowerInvariant();

            if (!normalizedLogicalName.All(character =>
                    char.IsLetterOrDigit(character) || character == '_'))
            {
                return Results.BadRequest(new
                {
                    detail = "El nombre lógico de la tabla contiene caracteres no permitidos."
                });
            }

            var metadataPath =
                $"EntityDefinitions(LogicalName='{normalizedLogicalName}')" +
                "?$select=LogicalName,SchemaName,EntitySetName," +
                "PrimaryIdAttribute,PrimaryNameAttribute";

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                metadataPath);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            var content = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Results.Problem(
                    statusCode: (int)response.StatusCode,
                    title: "No fue posible consultar la metadata de la tabla",
                    detail: ExtractDataverseError(content));
            }

            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            return Results.Ok(new
            {
                logicalName = ReadString(root, "LogicalName"),
                schemaName = ReadString(root, "SchemaName"),
                entitySetName = ReadString(root, "EntitySetName"),
                primaryIdAttribute = ReadString(root, "PrimaryIdAttribute"),
                primaryNameAttribute = ReadString(root, "PrimaryNameAttribute")
            });
        }
        catch (MicrosoftIdentityWebChallengeUserException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Dataverse requiere una nueva autenticación",
                detail: exception.InnerException is MsalUiRequiredException
                    ? "Cierra la sesión de Gaia e ingresa nuevamente."
                    : "No fue posible obtener autorización para Dataverse.");
        }
    }

    private static async Task<IResult> ValidateOrganizationImportAsync(
        OrganizationDataverseImporter importer,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await importer.ValidateAsync(cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "No fue posible validar la importación organizacional",
                detail: exception.Message);
        }
    }

    private static async Task<IResult> ImportOrganizationAsync(
        OrganizationDataverseImporter importer,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await importer.ImportAsync(cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "No fue posible importar la estructura organizacional",
                detail: exception.Message);
        }
    }

    private static async Task<IResult> GetStatusAsync(
        ITokenAcquisition tokenAcquisition,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var scope = configuration["Dataverse:Scope"]
            ?? throw new InvalidOperationException("Dataverse:Scope is required.");

        try
        {
            var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(
                [scope],
                authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);
            var client = httpClientFactory.CreateClient("Dataverse");
            using var request = new HttpRequestMessage(HttpMethod.Get, "WhoAmI");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Results.Problem(
                    statusCode: (int)response.StatusCode,
                    title: "Dataverse rechazó la solicitud",
                    detail: ExtractDataverseError(content));
            }

            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            return Results.Ok(new
            {
                connected = true,
                environment = configuration["Dataverse:EnvironmentUrl"],
                userId = ReadString(root, "UserId"),
                businessUnitId = ReadString(root, "BusinessUnitId"),
                organizationId = ReadString(root, "OrganizationId")
            });
        }
        catch (MicrosoftIdentityWebChallengeUserException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Dataverse requiere una nueva autenticación",
                detail: exception.InnerException is MsalUiRequiredException
                    ? "Cierra la sesión de Gaia e ingresa nuevamente para autorizar Dataverse."
                    : "No fue posible obtener autorización delegada para Dataverse.");
        }
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;

    private static string ExtractDataverseError(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "Dataverse devolvió un error sin descripción.";
            }
        }
        catch (JsonException)
        {
            // An intermediary may return a non-JSON error response.
        }

        return "Dataverse devolvió una respuesta no satisfactoria.";
    }
}
