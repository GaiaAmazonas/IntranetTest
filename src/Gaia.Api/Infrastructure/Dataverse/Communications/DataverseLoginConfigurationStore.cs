using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Communications;

namespace Gaia.Api.Infrastructure.Dataverse.Communications;

internal sealed class DataverseLoginConfigurationStore(
    IDataverseDelegatedClientFactory delegatedClients,
    IFileStorage storage,
    IFileStorageMaintenance maintenance,
    PublicLoginSnapshotStore publicSnapshot) : ILoginConfigurationStore
{
    private const string Configurations = "gaia_configuracionlogin";
    // The publisher prefix already ends in an underscore; the actual Dataverse logical name has two.
    private const string Socials = "gaia__redsociallogin";
    private static readonly SemaphoreSlim PublicationLock = new(1, 1);

    public async Task<IReadOnlyList<LoginConfigurationDto>> ListAsync(CancellationToken token)
    {
        using var client = await delegatedClients.CreateAsync();
        return await ReadAllConfigurations(client, token);
    }

    public async Task<LoginConfigurationDto> CreateAsync(LoginConfigurationWriteRequest request, Stream desktopImage,
        string contentType, string fileName, long length, CancellationToken token)
    {
        Validate(request);
        var correlationId = Guid.NewGuid();
        StoredFile? uploaded = null;
        var committed = false;
        try
        {
            using var client = await delegatedClients.CreateAsync();
            var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
            var fields = ConfigurationFields.From(metadata);
            // Validate the complete model before uploading or creating anything. This prevents partial records
            // when a related table or relationship is missing or has a different logical name.
            var socialMetadata = await DataverseMetadataResolver.TableAsync(client, Socials, token);
            _ = SocialFields.From(socialMetadata);
            uploaded = await storage.UploadAsync(new(new("LoginInstitucional", correlationId.ToString("D")), fileName,
                contentType, length, Guid.NewGuid(), DateTimeOffset.UtcNow), desktopImage, token);
            var reference = Encode(uploaded.Id);
            await EnsureReferenceFits(client, reference, "gaia_imagenescritorio", token);
            var payload = Payload(request, fields);
            payload[fields.Status] = metadata.EncodedIntegerValue("gaia_estado", 1);
            payload[fields.Current] = false;
            payload[fields.Desktop] = reference;
            payload["statecode"] = 0;
            using var response = await client.PostAsJsonAsync(metadata.EntitySetName, payload, token);
            await Ensure(response, token);
            var createdId = CreatedId(response);
            committed = true;
            return await ReadOneConfiguration(client, createdId, token);
        }
        catch
        {
            if (!committed && uploaded is not null)
                try { await maintenance.DeletePhysicallyAsync(new(uploaded.Id, uploaded.ETag, "Cleanup of uncommitted login configuration"), CancellationToken.None); }
                catch (FileStorageException) { }
            throw;
        }
    }

    public async Task<LoginConfigurationDto> SaveAsync(Guid id, LoginConfigurationWriteRequest request, CancellationToken token)
    {
        Validate(request);
        using var client = await delegatedClients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
        var fields = ConfigurationFields.From(metadata);
        await Patch(client, metadata, id, Payload(request, fields), token);
        var saved = await ReadOneConfiguration(client, id, token);
        if (saved.IsCurrent && saved.Status == 2) await PublishSnapshotAsync(client, saved, fields, token);
        return saved;
    }

    public async Task<LoginConfigurationDto> PublishAsync(Guid id, CancellationToken token)
    {
        await PublicationLock.WaitAsync(token);
        try
        {
            using var client = await delegatedClients.CreateAsync();
            var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
            var fields = ConfigurationFields.From(metadata);
            var target = await One(client, $"{metadata.EntitySetName}({id:D})?$select={fields.Desktop},statecode", token);
            var desktopReference = Text(target, fields.Desktop);
            if (string.IsNullOrWhiteSpace(desktopReference) || Number(target, "statecode") != 0)
                throw new InvalidOperationException("La configuración debe estar activa y tener imagen de escritorio antes de publicarse.");
            if (!await storage.ExistsAsync(Decode(desktopReference), token))
                throw new InvalidOperationException("La imagen de escritorio ya no existe. Cárgala nuevamente antes de publicar.");
            var currentRows = await DataverseJson.ReadAllAsync(client,
                $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute}&$filter={fields.Current} eq true", token);
            foreach (var row in currentRows)
            {
                var currentId = row.GetProperty(metadata.PrimaryIdAttribute).GetGuid();
                if (currentId != id) await Patch(client, metadata, currentId, new() { [fields.Current] = false }, token);
            }
            await Patch(client, metadata, id, new()
            {
                [fields.Status] = metadata.EncodedIntegerValue("gaia_estado", 2),
                [fields.Current] = true,
                [fields.Published] = DateTimeOffset.UtcNow
            }, token);
            var published = await ReadOneConfiguration(client, id, token);
            await PublishSnapshotAsync(client, published, fields, token);
            return published;
        }
        finally { PublicationLock.Release(); }
    }

    public async Task<LoginConfigurationDto> RetireAsync(Guid id, CancellationToken token)
    {
        using var client = await delegatedClients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
        var fields = ConfigurationFields.From(metadata);
        await Patch(client, metadata, id, new()
        {
            [fields.Status] = metadata.EncodedIntegerValue("gaia_estado", 3),
            [fields.Current] = false
        }, token);
        return await ReadOneConfiguration(client, id, token);
    }

    public async Task UploadImageAsync(Guid id, string variant, Stream content, string contentType, string fileName,
        long length, CancellationToken token)
    {
        using var client = await delegatedClients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
        var fields = ConfigurationFields.From(metadata);
        _ = await One(client, $"{metadata.EntitySetName}({id:D})?$select={metadata.PrimaryIdAttribute}", token);
        var column = variant switch { "desktop" => fields.Desktop, "tablet" => fields.Tablet, "mobile" => fields.Mobile, _ => throw new ArgumentException("La variante de imagen no es válida.") };
        StoredFile? uploaded = null;
        try
        {
            uploaded = await storage.UploadAsync(new(new("LoginInstitucional", id.ToString("D")), fileName, contentType,
                length, Guid.NewGuid(), DateTimeOffset.UtcNow), content, token);
            var reference = Encode(uploaded.Id);
            await EnsureReferenceFits(client, reference, variant == "desktop" ? "gaia_imagenescritorio" : variant == "tablet" ? "gaia_imagentablet" : "gaia_imagenmovil", token);
            await Patch(client, metadata, id, new() { [column] = reference }, token);
        }
        catch
        {
            if (uploaded is not null)
                try { await maintenance.DeletePhysicallyAsync(new(uploaded.Id, uploaded.ETag, "Cleanup of uncommitted login image"), CancellationToken.None); }
                catch (FileStorageException) { }
            throw;
        }
    }

    public async Task<MediaContent?> ReadImageAsync(Guid id, string variant, CancellationToken token)
    {
        if (variant is not ("desktop" or "tablet" or "mobile")) return null;
        using var client = await delegatedClients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
        var fields = ConfigurationFields.From(metadata);
        var column = variant switch { "desktop" => fields.Desktop, "tablet" => fields.Tablet, _ => fields.Mobile };
        var row = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{metadata.EntitySetName}({id:D})?$select={column},statecode", token);
        if (row is null || Number(row.Value, "statecode") != 0) return null;
        var reference = Text(row.Value, column);
        if (string.IsNullOrWhiteSpace(reference)) return null;
        try
        {
            await using var download = await storage.DownloadAsync(Decode(reference), token);
            using var memory = new MemoryStream();
            await download.Content.CopyToAsync(memory, token);
            return new(memory.ToArray(), download.Metadata.ContentType);
        }
        catch (FileStorageException error) when (error.Code == FileStorageError.FileNotFound) { return null; }
    }

    public async Task DeleteImageAsync(Guid id, string variant, CancellationToken token)
    {
        if (variant is not ("tablet" or "mobile"))
            throw new ArgumentException("La imagen de escritorio es obligatoria y sólo puede reemplazarse.");
        using var client = await delegatedClients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
        var fields = ConfigurationFields.From(metadata);
        var column = variant == "tablet" ? fields.Tablet : fields.Mobile;
        var row = await One(client, $"{metadata.EntitySetName}({id:D})?$select={column}", token);
        var reference = Text(row, column);
        if (string.IsNullOrWhiteSpace(reference)) return;
        var externalId = Decode(reference);
        StoredFile? stored = null;
        try { stored = await storage.GetMetadataAsync(externalId, token); }
        catch (FileStorageException error) when (error.Code == FileStorageError.FileNotFound) { }
        await Patch(client, metadata, id, new() { [column] = null }, token);
        if (stored is not null)
            try { await maintenance.DeletePhysicallyAsync(new(externalId, stored.ETag, $"Login {variant} image removed by administrator"), token); }
            catch (FileStorageException error) when (error.Code == FileStorageError.FileNotFound) { }
    }

    public async Task<LoginSocialDto> SaveSocialAsync(Guid configurationId, Guid? id, LoginSocialWriteRequest request, CancellationToken token)
    {
        Validate(request);
        using var client = await delegatedClients.CreateAsync();
        var configuration = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
        _ = await One(client, $"{configuration.EntitySetName}({configurationId:D})?$select={configuration.PrimaryIdAttribute}", token);
        var metadata = await DataverseMetadataResolver.TableAsync(client, Socials, token);
        var fields = SocialFields.From(metadata);
        var payload = new Dictionary<string, object?>
        {
            [fields.Name] = request.Name.Trim(), [fields.Label] = request.Label.Trim(),
            [fields.Order] = metadata.EncodedIntegerValue("gaia_Orden", request.Order), [fields.Url] = request.Url.Trim(),
            [fields.ConfigurationNavigation + "@odata.bind"] = $"/{configuration.EntitySetName}({configurationId:D})", ["statecode"] = 0
        };
        Guid saved;
        if (id.HasValue)
        {
            var owned = await One(client, $"{metadata.EntitySetName}({id:D})?$select=_{fields.ConfigurationLookup}_value", token);
            if (Lookup(owned, fields.ConfigurationLookup) != configurationId) throw new KeyNotFoundException("La red social no pertenece a esta configuración.");
            payload.Remove(fields.ConfigurationNavigation + "@odata.bind"); payload.Remove("statecode");
            await Patch(client, metadata, id.Value, payload, token); saved = id.Value;
        }
        else
        {
            using var response = await client.PostAsJsonAsync(metadata.EntitySetName, payload, token);
            await Ensure(response, token); saved = CreatedId(response);
        }
        return MapSocial(await One(client, $"{metadata.EntitySetName}({saved:D})?$select={fields.Select}", token), fields);
    }

    public async Task DeleteSocialAsync(Guid configurationId, Guid id, CancellationToken token)
    {
        using var client = await delegatedClients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Socials, token);
        var fields = SocialFields.From(metadata);
        var owned = await One(client, $"{metadata.EntitySetName}({id:D})?$select=_{fields.ConfigurationLookup}_value", token);
        if (Lookup(owned, fields.ConfigurationLookup) != configurationId) throw new KeyNotFoundException("La red social no pertenece a esta configuración.");
        using var response = await client.DeleteAsync($"{metadata.EntitySetName}({id:D})", token);
        await Ensure(response, token);
    }

    public async Task<PublicLoginConfigurationDto?> ReadPublicAsync(CancellationToken token)
        => await publicSnapshot.ReadAsync(token);

    public async Task<MediaContent?> ReadPublicImageAsync(Guid id, string variant, CancellationToken token)
        => await publicSnapshot.ReadImageAsync(variant, token);

    private static PublicLoginConfigurationDto Public(LoginConfigurationDto item, string version)
    {
        string Image(string variant) => $"/api/public/login-configuration/{item.Id:D}/images/{variant}?v={Uri.EscapeDataString(version)}";
        return new(item.PlatformName, item.Eyebrow, item.Description, item.LowerLeftText, item.FooterTitle,
            item.FooterDescription, item.ImageAlt, Image("desktop"), item.HasTabletImage ? Image("tablet") : Image("desktop"),
            item.HasMobileImage ? Image("mobile") : item.HasTabletImage ? Image("tablet") : Image("desktop"),
            item.SocialNetworks.Select(x => new PublicLoginSocialDto(x.Name, x.Label, x.Order, x.Url)).ToArray());
    }

    private async Task PublishSnapshotAsync(HttpClient client, LoginConfigurationDto item,
        ConfigurationFields fields, CancellationToken token)
    {
        var row = await One(client, $"{(await DataverseMetadataResolver.TableAsync(client, Configurations, token)).EntitySetName}({item.Id:D})?$select={fields.Desktop},{fields.Tablet},{fields.Mobile},modifiedon", token);
        var images = new Dictionary<string, ExternalFileId>(StringComparer.OrdinalIgnoreCase)
        {
            ["desktop"] = Decode(Text(row, fields.Desktop) ?? throw new InvalidOperationException("La publicación requiere una imagen de escritorio."))
        };
        if (Text(row, fields.Tablet) is { Length: > 0 } tablet) images["tablet"] = Decode(tablet);
        if (Text(row, fields.Mobile) is { Length: > 0 } mobile) images["mobile"] = Decode(mobile);
        var version = Text(row, "modifiedon") ?? DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture);
        await publicSnapshot.PublishAsync(Public(item, version), images, token);
    }

    private static async Task<IReadOnlyList<LoginConfigurationDto>> ReadAllConfigurations(HttpClient client, CancellationToken token)
    {
        var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
        var fields = ConfigurationFields.From(metadata);
        var rows = await DataverseJson.ReadAllAsync(client, $"{metadata.EntitySetName}?$select={fields.Select}&$orderby=modifiedon desc", token);
        var result = new List<LoginConfigurationDto>();
        foreach (var row in rows) result.Add(MapConfiguration(row, fields, await ReadSocials(client, row.GetProperty(metadata.PrimaryIdAttribute).GetGuid(), token)));
        return result;
    }

    private static async Task<LoginConfigurationDto> ReadOneConfiguration(HttpClient client, Guid id, CancellationToken token)
    {
        var metadata = await DataverseMetadataResolver.TableAsync(client, Configurations, token);
        var fields = ConfigurationFields.From(metadata);
        var row = await One(client, $"{metadata.EntitySetName}({id:D})?$select={fields.Select}", token);
        return MapConfiguration(row, fields, await ReadSocials(client, id, token));
    }

    private static LoginConfigurationDto MapConfiguration(JsonElement row, ConfigurationFields fields, IReadOnlyList<LoginSocialDto> socials) =>
        new(row.GetProperty(fields.Id).GetGuid(), Text(row, fields.Name) ?? "", Text(row, fields.Code) ?? "", Text(row, fields.Eyebrow) ?? "",
            Text(row, fields.Alt), Text(row, fields.Description), Number(row, fields.Status) ?? 1, Boolean(row, fields.Current), Date(row, fields.Published),
            HasText(row, fields.Desktop), HasText(row, fields.Tablet), HasText(row, fields.Mobile), Text(row, fields.Platform), Text(row, fields.LowerLeft),
            Text(row, fields.FooterTitle), Text(row, fields.FooterDescription), socials);

    private static Dictionary<string, object?> Payload(LoginConfigurationWriteRequest request, ConfigurationFields fields) => new()
    {
        [fields.Name] = request.Name.Trim(), [fields.Code] = request.Code.Trim(), [fields.Eyebrow] = request.Eyebrow.Trim(),
        [fields.Alt] = Clean(request.ImageAlt), [fields.Description] = Clean(request.Description), [fields.Platform] = Clean(request.PlatformName),
        [fields.LowerLeft] = Clean(request.LowerLeftText), [fields.FooterTitle] = Clean(request.FooterTitle), [fields.FooterDescription] = Clean(request.FooterDescription)
    };

    private static async Task<IReadOnlyList<LoginSocialDto>> ReadSocials(HttpClient client, Guid configurationId, CancellationToken token)
    {
        var metadata = await DataverseMetadataResolver.TableAsync(client, Socials, token);
        var fields = SocialFields.From(metadata);
        var rows = await DataverseJson.ReadAllAsync(client, $"{metadata.EntitySetName}?$select={fields.Select}&$filter=statecode eq 0 and _{fields.ConfigurationLookup}_value eq {configurationId:D}&$orderby={fields.Order} asc", token);
        return rows.Select(row => MapSocial(row, fields)).ToArray();
    }

    private static LoginSocialDto MapSocial(JsonElement row, SocialFields fields) => new(row.GetProperty(fields.Id).GetGuid(),
        Text(row, fields.Name) ?? "", Text(row, fields.Label) ?? "", Number(row, fields.Order) ?? 0, Text(row, fields.Url) ?? "");

    private static void Validate(LoginConfigurationWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Eyebrow))
            throw new ArgumentException("Nombre, código y antetítulo son obligatorios.");
    }
    private static void Validate(LoginSocialWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Label) || request.Order < 0
            || !Uri.TryCreate(request.Url, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
            throw new ArgumentException("Nombre, etiqueta, orden y una URL HTTP(S) válida son obligatorios.");
    }
    private static async Task EnsureReferenceFits(HttpClient client, string value, string schemaName, CancellationToken token)
    {
        var constraints = await DataverseMetadataResolver.ConstraintsAsync(client, Configurations, token);
        var constraint = constraints.TryGetValue(schemaName, out var exact) ? exact : constraints.FirstOrDefault(x => x.Value.LogicalName.Equals(schemaName, StringComparison.OrdinalIgnoreCase)).Value;
        if (constraint?.MaxLength is int maximum && value.Length > maximum)
            throw new InvalidOperationException($"La columna {schemaName} admite {maximum} caracteres y la referencia segura requiere {value.Length}.");
    }
    private static string Encode(ExternalFileId id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(id))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static ExternalFileId Decode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/'); normalized += new string('=', (4 - normalized.Length % 4) % 4);
        return JsonSerializer.Deserialize<ExternalFileId>(Encoding.UTF8.GetString(Convert.FromBase64String(normalized))) ?? throw new FileStorageException(FileStorageError.FileNotFound);
    }
    private static async Task Patch(HttpClient client, DataverseTableMetadata metadata, Guid id, Dictionary<string, object?> payload, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{metadata.EntitySetName}({id:D})") { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("If-Match", "*"); using var response = await client.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new KeyNotFoundException("El registro no existe."); await Ensure(response, token);
    }
    private static async Task<JsonElement> One(HttpClient client, string path, CancellationToken token) => await DataverseMetadataResolver.ReadOneAsync(client, path, token) ?? throw new KeyNotFoundException("El registro no existe.");
    private static async Task Ensure(HttpResponseMessage response, CancellationToken token) { if (response.IsSuccessStatusCode) return; var body = await response.Content.ReadAsStringAsync(token); throw new InvalidOperationException($"Dataverse rechazó la operación ({(int)response.StatusCode}): {SafeError(body)}"); }
    private static string SafeError(string body) { try { using var json = JsonDocument.Parse(body); return json.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "Error sin descripción."; } catch (JsonException) { return "Respuesta no válida de Dataverse."; } }
    private static Guid CreatedId(HttpResponseMessage response) { var value = response.Headers.TryGetValues("OData-EntityId", out var values) ? values.SingleOrDefault() : null; var match = System.Text.RegularExpressions.Regex.Match(value ?? "", @"\(([0-9a-f-]{36})\)$"); return match.Success ? Guid.Parse(match.Groups[1].Value) : throw new InvalidOperationException("Dataverse no devolvió el identificador creado."); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Text(JsonElement row, string field) => row.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool HasText(JsonElement row, string field) => !string.IsNullOrWhiteSpace(Text(row, field));
    private static int? Number(JsonElement row, string field) => DataverseJson.OptionalEncodedInt32(row, field);
    private static bool Boolean(JsonElement row, string field) => row.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.True;
    private static DateTimeOffset? Date(JsonElement row, string field) => DateTimeOffset.TryParse(Text(row, field), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
    private static Guid? Lookup(JsonElement row, string field) => Guid.TryParse(Text(row, $"_{field}_value"), out var value) ? value : null;

    private sealed record ConfigurationFields(string Id,string Name,string Code,string Eyebrow,string Alt,string Description,string Status,string Current,string Published,string Desktop,string Tablet,string Mobile,string Platform,string LowerLeft,string FooterTitle,string FooterDescription)
    {
        public string Select => string.Join(',',Id,Name,Code,Eyebrow,Alt,Description,Status,Current,Published,Desktop,Tablet,Mobile,Platform,LowerLeft,FooterTitle,FooterDescription,"modifiedon","statecode");
        public static ConfigurationFields From(DataverseTableMetadata m) => new(m.PrimaryIdAttribute,m.Attribute("gaia_nombre"),m.Attribute("gaia_codigo"),m.Attribute("gaia_antetituloprincipal"),m.Attribute("gaia_altimagen"),m.Attribute("gaia_descripcionprincipal"),m.Attribute("gaia_estado"),m.Attribute("gaia_esvigente"),m.Attribute("gaia_fechapublicacion"),m.Attribute("gaia_imagenescritorio"),m.Attribute("gaia_imagentablet"),m.Attribute("gaia_imagenmovil"),m.Attribute("gaia_nombreplataforma"),m.Attribute("gaia_textoinferiorizquierdo"),m.Attribute("gaia_titulopie"),m.Attribute("gaia_descripcionpie"));
    }
    private sealed record SocialFields(string Id,string Name,string ConfigurationLookup,string ConfigurationNavigation,string Label,string Order,string Url)
    {
        public string Select => string.Join(',',Id,Name,$"_{ConfigurationLookup}_value",Label,Order,Url,"statecode");
        public static SocialFields From(DataverseTableMetadata m) { var relation=m.Relationship("gaia_configuracionlogin",Configurations); return new(m.PrimaryIdAttribute,m.Attribute("gaia_nombre"),relation.ReferencingAttribute,relation.NavigationProperty,m.Attribute("gaia_etiqueta"),m.Attribute("gaia_Orden"),m.Attribute("gaia_url")); }
    }
}
