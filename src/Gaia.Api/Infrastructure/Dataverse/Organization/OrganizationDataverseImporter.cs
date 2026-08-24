using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class OrganizationDataverseImporter(
    ITokenAcquisition tokenAcquisition,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private const string OrganizationTable = "gaia_organizacion";
    private const string UnitTypeTable = "gaia_tipounidadorganizacional";
    private const string SiteTable = "gaia_sede";

    private static readonly Dictionary<string, string> UnitTypeCodes =
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["DIRECTIVOS"] = "DIR",
        ["SUBDIRECCION"] = "SUB",
        ["ASESORIA ESTRATEGICA"] = "ASE",
        ["COORDINACION DIRECTA"] = "COD",
        ["COORDINACION TRANSVERSAL"] = "COT",
        ["OPERATIVA"] = "OPE"
    };

    public async Task<object> ValidateAsync(CancellationToken cancellationToken)
    {
        var errors = ValidateSource();
        var client = await CreateClientAsync(cancellationToken);
        var organization = await GetMetadataAsync(client, OrganizationTable, cancellationToken);
        var unitType = await GetMetadataAsync(client, UnitTypeTable, cancellationToken);
        var site = await GetMetadataAsync(client, SiteTable, cancellationToken);

        RequireAttribute(organization, "gaia_codigo", errors);
        RequireAttribute(organization, "gaia_nombre", errors);
        RequireAttribute(organization, "gaia_nivel", errors);

        var parentRelations = organization.Relationships
            .Where(item => item.ReferencedEntity == OrganizationTable
                && item.ReferencingAttribute.EndsWith("unidadpadre", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var correctParent = parentRelations.FirstOrDefault(item =>
            item.ReferencingAttribute.StartsWith("gaia_", StringComparison.OrdinalIgnoreCase));
        var obsoleteParents = parentRelations
            .Where(item => !item.ReferencingAttribute.StartsWith("gaia_", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ReferencingAttribute)
            .ToArray();
        if (correctParent is null)
        {
            errors.Add("No existe el lookup gaia_UnidadPadre hacia Organización.");
        }

        var typeRelation = organization.Relationships.FirstOrDefault(item =>
            item.ReferencedEntity == UnitTypeTable);
        if (typeRelation is null)
        {
            errors.Add("No existe el lookup de Organización hacia Tipo Unidad Organizacional.");
        }

        var siteRelation = organization.Relationships.FirstOrDefault(item =>
            item.ReferencedEntity == SiteTable
            && item.ReferencingAttribute.Equals("gaia_sede", StringComparison.OrdinalIgnoreCase));
        if (siteRelation is null)
        {
            errors.Add("No existe el lookup gaia_sede de Organización hacia Sede.");
        }

        return new
        {
            valid = errors.Count == 0,
            sourceRows = OrganizationImportSource.Rows.Length,
            roots = OrganizationImportSource.Rows.Count(item => item.ParentCode is null),
            unitTypes = OrganizationImportSource.Rows.Select(item => item.UnitType)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(),
            organization = new
            {
                organization.EntitySetName,
                organization.PrimaryIdAttribute,
                organization.PrimaryNameAttribute,
                parentLookup = correctParent?.ReferencingAttribute,
                obsoleteParentLookups = obsoleteParents,
                unitTypeLookup = typeRelation?.ReferencingAttribute
            },
            unitType = new
            {
                unitType.EntitySetName,
                unitType.PrimaryIdAttribute,
                unitType.PrimaryNameAttribute
            },
            site = new
            {
                site.EntitySetName,
                site.PrimaryIdAttribute,
                site.PrimaryNameAttribute,
                lookup = siteRelation?.ReferencingAttribute
            },
            errors
        };
    }

    public async Task<object> ImportAsync(CancellationToken cancellationToken)
    {
        var sourceErrors = ValidateSource();
        if (sourceErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", sourceErrors));
        }

        var client = await CreateClientAsync(cancellationToken);
        var organization = await GetMetadataAsync(client, OrganizationTable, cancellationToken);
        var unitType = await GetMetadataAsync(client, UnitTypeTable, cancellationToken);
        var site = await GetMetadataAsync(client, SiteTable, cancellationToken);
        var parentRelation = organization.Relationships.Single(item =>
            item.ReferencedEntity == OrganizationTable
            && item.ReferencingAttribute.Equals("gaia_unidadpadre", StringComparison.OrdinalIgnoreCase));
        var typeRelation = organization.Relationships.Single(item =>
            item.ReferencedEntity == UnitTypeTable);
        var siteRelation = organization.Relationships.Single(item =>
            item.ReferencedEntity == SiteTable
            && item.ReferencingAttribute.Equals("gaia_sede", StringComparison.OrdinalIgnoreCase));

        var typesByName = await EnsureUnitTypesAsync(client, unitType, cancellationToken);
        var bogotaSiteId = await EnsureBogotaSiteAsync(client, site, cancellationToken);
        var codes = OrganizationImportSource.Rows.Select(item => item.Code).ToArray();

        foreach (var source in OrganizationImportSource.Rows)
        {
            var payload = new Dictionary<string, object?>
            {
                ["gaia_codigo"] = source.Code,
                ["gaia_nombre"] = source.Name,
                ["gaia_nivel"] = source.Level,
                [$"{typeRelation.NavigationProperty}@odata.bind"] =
                    $"/{unitType.EntitySetName}({typesByName[source.UnitType]})",
                ["statecode"] = source.Status.Equals("Activo", StringComparison.OrdinalIgnoreCase) ? 0 : 1
            };
            await UpsertByAlternateKeyAsync(
                client,
                organization.EntitySetName,
                "gaia_codigo",
                source.Code,
                payload,
                cancellationToken);
        }

        var unitsByCode = await ReadUnitsByCodeAsync(client, organization, codes, cancellationToken);
        foreach (var source in OrganizationImportSource.Rows)
        {
            var payload = new Dictionary<string, object?>
            {
                [$"{parentRelation.NavigationProperty}@odata.bind"] = source.ParentCode is null
                    ? null
                    : $"/{organization.EntitySetName}({unitsByCode[source.ParentCode]})",
                [$"{siteRelation.NavigationProperty}@odata.bind"] =
                    $"/{site.EntitySetName}({bogotaSiteId:D})"
            };
            await PatchAsync(
                client,
                $"{organization.EntitySetName}({unitsByCode[source.Code]})",
                payload,
                cancellationToken);
        }

        var inactivatedCodes = await InactivateUnitsOutsideSourceAsync(
            client, organization, codes, cancellationToken);

        return new
        {
            imported = OrganizationImportSource.Rows.Length,
            unitTypes = typesByName.Count,
            parentsAssigned = OrganizationImportSource.Rows.Count(item => item.ParentCode is not null),
            sitesAssigned = OrganizationImportSource.Rows.Length,
            bogotaSiteId,
            roots = OrganizationImportSource.Rows.Count(item => item.ParentCode is null),
            inactivated = inactivatedCodes.Length,
            inactivatedCodes,
            codes
        };
    }

    private static async Task<string[]> InactivateUnitsOutsideSourceAsync(
        HttpClient client,
        TableMetadata metadata,
        IReadOnlyCollection<string> expectedCodes,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute},gaia_codigo,statecode",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var obsolete = json.RootElement.GetProperty("value").EnumerateArray()
            .Select(item => new
            {
                Id = item.GetProperty(metadata.PrimaryIdAttribute).GetGuid(),
                Code = item.TryGetProperty("gaia_codigo", out var code) ? code.GetString() : null,
                State = item.TryGetProperty("statecode", out var state) && state.ValueKind == JsonValueKind.Number
                    ? state.GetInt32() : 0
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Code)
                && !expectedCodes.Contains(item.Code!, StringComparer.OrdinalIgnoreCase)
                && item.State == 0)
            .ToArray();

        foreach (var item in obsolete)
        {
            await PatchAsync(client, $"{metadata.EntitySetName}({item.Id:D})",
                new Dictionary<string, object?> { ["statecode"] = 1 }, cancellationToken);
        }
        return obsolete.Select(item => item.Code!).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var scope = configuration["Dataverse:Scope"]
            ?? throw new InvalidOperationException("Dataverse:Scope is required.");
        var token = await tokenAcquisition.GetAccessTokenForUserAsync(
            [scope],
            authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);
        var client = httpClientFactory.CreateClient("Dataverse");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static List<string> ValidateSource()
    {
        var errors = new List<string>();
        var rows = OrganizationImportSource.Rows;
        var duplicates = rows.GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0)
        {
            errors.Add($"Códigos duplicados: {string.Join(", ", duplicates)}.");
        }

        var byCode = rows.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.ParentCode is null)
            {
                if (row.Level != 1) errors.Add($"Fila {row.SourceRow}: una raíz debe tener nivel 1.");
                continue;
            }
            if (!byCode.TryGetValue(row.ParentCode, out var parent))
            {
                errors.Add($"Fila {row.SourceRow}: no existe el padre {row.ParentCode}.");
            }
            else if (row.Level != parent.Level + 1)
            {
                errors.Add($"Fila {row.SourceRow}: nivel inconsistente con el padre {row.ParentCode}.");
            }
        }
        return errors;
    }

    private static void RequireAttribute(TableMetadata table, string logicalName, List<string> errors)
    {
        if (!table.Attributes.Contains(logicalName, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Falta la columna {logicalName} en {table.LogicalName}.");
        }
    }

    private static async Task<TableMetadata> GetMetadataAsync(
        HttpClient client,
        string logicalName,
        CancellationToken cancellationToken)
    {
        var path = $"EntityDefinitions(LogicalName='{logicalName}')" +
            "?$select=LogicalName,EntitySetName,PrimaryIdAttribute,PrimaryNameAttribute" +
            "&$expand=Attributes($select=LogicalName),ManyToOneRelationships" +
            "($select=ReferencingAttribute,ReferencingEntityNavigationPropertyName,ReferencedEntity)";
        using var response = await client.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        return new TableMetadata(
            root.GetProperty("LogicalName").GetString()!,
            root.GetProperty("EntitySetName").GetString()!,
            root.GetProperty("PrimaryIdAttribute").GetString()!,
            root.GetProperty("PrimaryNameAttribute").GetString()!,
            root.GetProperty("Attributes").EnumerateArray()
                .Select(item => item.GetProperty("LogicalName").GetString()!).ToArray(),
            root.GetProperty("ManyToOneRelationships").EnumerateArray()
                .Select(item => new RelationshipMetadata(
                    item.GetProperty("ReferencingAttribute").GetString()!,
                    item.GetProperty("ReferencingEntityNavigationPropertyName").GetString()!,
                    item.GetProperty("ReferencedEntity").GetString()!)).ToArray());
    }

    private static async Task<Guid> EnsureBogotaSiteAsync(
        HttpClient client,
        TableMetadata metadata,
        CancellationToken cancellationToken)
    {
        var filter = Uri.EscapeDataString("gaia_codigo eq 'BOG'");
        using (var response = await client.GetAsync(
            $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute}&$filter={filter}&$top=1",
            cancellationToken))
        {
            await EnsureSuccessAsync(response, cancellationToken);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var existing = json.RootElement.GetProperty("value").EnumerateArray().FirstOrDefault();
            if (existing.ValueKind != JsonValueKind.Undefined)
                return existing.GetProperty(metadata.PrimaryIdAttribute).GetGuid();
        }

        var payload = new Dictionary<string, object?>
        {
            [metadata.PrimaryNameAttribute] = "Bogotá",
            ["gaia_codigo"] = "BOG",
            ["gaia_ciudad"] = "Bogotá D.C.",
            ["gaia_activo"] = true,
            ["statecode"] = 0
        };
        using var create = await client.PostAsJsonAsync(
            metadata.EntitySetName, payload, cancellationToken);
        await EnsureSuccessAsync(create, cancellationToken);
        return ReadCreatedId(create);
    }

    private static async Task<Dictionary<string, Guid>> EnsureUnitTypesAsync(
        HttpClient client,
        TableMetadata metadata,
        CancellationToken cancellationToken)
    {
        var names = OrganizationImportSource.Rows.Select(item => item.UnitType)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        using (var response = await client.GetAsync(
            $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute},{metadata.PrimaryNameAttribute}",
            cancellationToken))
        {
            await EnsureSuccessAsync(response, cancellationToken);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (var item in json.RootElement.GetProperty("value").EnumerateArray())
            {
                var name = item.GetProperty(metadata.PrimaryNameAttribute).GetString();
                if (name is not null && names.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    result[name] = item.GetProperty(metadata.PrimaryIdAttribute).GetGuid();
                }
            }
        }

        foreach (var name in names.Where(name => !result.ContainsKey(name)))
        {
            if (!UnitTypeCodes.TryGetValue(name, out var code))
            {
                throw new InvalidOperationException(
                    $"No se ha definido un código para el tipo de unidad '{name}'.");
            }

            var payload = new Dictionary<string, object?>
            {
                [metadata.PrimaryNameAttribute] = name,
                ["gaia_codigo"] = code,
                ["gaia_activo"] = true
            };

            using var response = await client.PostAsJsonAsync(
                metadata.EntitySetName,
                payload,
                cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);
            result[name] = ReadCreatedId(response);
        }
        return result;
    }

    private static async Task<Dictionary<string, Guid>> ReadUnitsByCodeAsync(
        HttpClient client,
        TableMetadata metadata,
        string[] expectedCodes,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute},gaia_codigo",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var result = json.RootElement.GetProperty("value").EnumerateArray()
            .Where(item => expectedCodes.Contains(item.GetProperty("gaia_codigo").GetString()!))
            .ToDictionary(
                item => item.GetProperty("gaia_codigo").GetString()!,
                item => item.GetProperty(metadata.PrimaryIdAttribute).GetGuid(),
                StringComparer.OrdinalIgnoreCase);
        if (result.Count != expectedCodes.Length)
        {
            throw new InvalidOperationException("Dataverse no devolvió todas las unidades después del upsert.");
        }
        return result;
    }

    private static Task UpsertByAlternateKeyAsync(
        HttpClient client,
        string entitySet,
        string keyName,
        string keyValue,
        object payload,
        CancellationToken cancellationToken) =>
        PatchAsync(
            client,
            $"{entitySet}({keyName}='{keyValue.Replace("'", "''")}')",
            payload,
            updateOnly: false,
            cancellationToken);

    private static async Task PatchAsync(
        HttpClient client,
        string path,
        object payload,
        CancellationToken cancellationToken) =>
        await PatchAsync(client, path, payload, updateOnly: true, cancellationToken);

    private static async Task PatchAsync(
        HttpClient client,
        string path,
        object payload,
        bool updateOnly,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(payload)
        };
        if (updateOnly)
        {
            request.Headers.TryAddWithoutValidation("If-Match", "*");
        }
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static Guid ReadCreatedId(HttpResponseMessage response)
    {
        var entityId = response.Headers.GetValues("OData-EntityId").Single();
        var start = entityId.LastIndexOf('(') + 1;
        return Guid.Parse(entityId[start..entityId.LastIndexOf(')')]);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Dataverse {(int)response.StatusCode}: {body}");
    }

    private sealed record TableMetadata(
        string LogicalName,
        string EntitySetName,
        string PrimaryIdAttribute,
        string PrimaryNameAttribute,
        string[] Attributes,
        RelationshipMetadata[] Relationships);

    private sealed record RelationshipMetadata(
        string ReferencingAttribute,
        string NavigationProperty,
        string ReferencedEntity);
}
