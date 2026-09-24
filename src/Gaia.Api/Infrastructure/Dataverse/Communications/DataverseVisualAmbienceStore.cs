using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Communications;

namespace Gaia.Api.Infrastructure.Dataverse.Communications;

internal sealed class DataverseVisualAmbienceStore(
    IDataverseDelegatedClientFactory delegatedClients,
    IFileStorage storage,
    IFileStorageMaintenance maintenance) : IVisualAmbienceStore
{
    private const string Table = "gaia_ambientacionvisual";
    private static readonly SemaphoreSlim PublicationLock = new(1, 1);

    public async Task<IReadOnlyList<VisualAmbienceDto>> ListAsync(CancellationToken token)
    {
        using var client = await delegatedClients.CreateAsync();
        var (metadata, fields) = await Metadata(client, token);
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{metadata.EntitySetName}?$select={fields.Select}&$filter=statecode eq 0&$orderby=modifiedon desc", token);
        return rows.Select(row => Map(row, fields)).ToArray();
    }

    public async Task<VisualAmbienceDto> SaveAsync(Guid? id, VisualAmbienceWriteRequest request, CancellationToken token)
    {
        Validate(request);
        using var client = await delegatedClients.CreateAsync();
        var (metadata, fields) = await Metadata(client, token);
        var payload = Payload(request, metadata, fields);
        Guid savedId;
        if (id is null)
        {
            payload[fields.PublicationStatus] = metadata.EncodedIntegerValue("gaia_EstadoPublicacion", 1);
            using var response = await client.PostAsJsonAsync(metadata.EntitySetName, payload, token);
            await Ensure(response, token);
            savedId = CreatedId(response);
        }
        else
        {
            savedId = id.Value;
            await Patch(client, metadata, savedId, payload, token);
        }
        return await Read(client, metadata, fields, savedId, token);
    }

    public Task<VisualAmbienceDto> PublishAsync(Guid id, CancellationToken token) => ChangeState(id, 2, retireConflicts: true, token);
    public Task<VisualAmbienceDto> RetireAsync(Guid id, CancellationToken token) => ChangeState(id, 3, retireConflicts: false, token);

    private async Task<VisualAmbienceDto> ChangeState(Guid id, int status, bool retireConflicts, CancellationToken token)
    {
        await PublicationLock.WaitAsync(token);
        try
        {
            using var client = await delegatedClients.CreateAsync();
            var (metadata, fields) = await Metadata(client, token);
            var target = await Read(client, metadata, fields, id, token);
            if (retireConflicts)
            {
                var rows = await DataverseJson.ReadAllAsync(client,
                    $"{metadata.EntitySetName}?$select={fields.Id},{fields.Scope},{fields.PublicationStatus}&$filter=statecode eq 0 and {fields.PublicationStatus} eq 2", token);
                foreach (var row in rows)
                {
                    var otherId = row.GetProperty(fields.Id).GetGuid();
                    var otherScope = Number(row, fields.Scope) ?? 0;
                    if (otherId != id && ScopesOverlap(target.Scope, otherScope))
                        await Patch(client, metadata, otherId, new() { [fields.PublicationStatus] = metadata.EncodedIntegerValue("gaia_EstadoPublicacion", 3) }, token);
                }
            }
            await Patch(client, metadata, id, new() { [fields.PublicationStatus] = metadata.EncodedIntegerValue("gaia_EstadoPublicacion", status) }, token);
            return await Read(client, metadata, fields, id, token);
        }
        finally { PublicationLock.Release(); }
    }

    public async Task UploadImageAsync(Guid id, string variant, Stream content, string contentType, string fileName, long length, CancellationToken token)
    {
        var schema = VariantSchema(variant);
        using var client = await delegatedClients.CreateAsync();
        var (metadata, fields) = await Metadata(client, token);
        _ = await Read(client, metadata, fields, id, token);
        var uploaded = await storage.UploadAsync(new(new("AmbientacionVisual", id.ToString("D")), fileName, contentType,
            length, Guid.NewGuid(), DateTimeOffset.UtcNow), content, token);
        var reference = Encode(uploaded.Id);
        await EnsureReferenceFits(client, reference, schema, token);
        await Patch(client, metadata, id, new() { [variant == "desktop" ? fields.DesktopImage : fields.MobileImage] = reference }, token);
    }

    public async Task<MediaContent?> ReadImageAsync(Guid id, string variant, CancellationToken token)
    {
        using var client = await delegatedClients.CreateAsync();
        var (metadata, fields) = await Metadata(client, token);
        var row = await One(client, $"{metadata.EntitySetName}({id:D})?$select={(variant == "desktop" ? fields.DesktopImage : fields.MobileImage)},statecode", token);
        var reference = Text(row, variant == "desktop" ? fields.DesktopImage : fields.MobileImage);
        if (string.IsNullOrWhiteSpace(reference)) return null;
        await using var download = await storage.DownloadAsync(Decode(reference), token);
        using var memory = new MemoryStream();
        await download.Content.CopyToAsync(memory, token);
        return new(memory.ToArray(), download.Metadata.ContentType);
    }

    public async Task DeleteImageAsync(Guid id, string variant, CancellationToken token)
    {
        using var client = await delegatedClients.CreateAsync();
        var (metadata, fields) = await Metadata(client, token);
        var field = variant == "desktop" ? fields.DesktopImage : fields.MobileImage;
        var row = await One(client, $"{metadata.EntitySetName}({id:D})?$select={field},statecode", token);
        var reference = Text(row, field);
        await Patch(client, metadata, id, new() { [field] = null }, token);
        if (string.IsNullOrWhiteSpace(reference)) return;
        try
        {
            var external = Decode(reference);
            var stored = await storage.GetMetadataAsync(external, token);
            await maintenance.DeletePhysicallyAsync(new(external, stored.ETag, "Visual ambience image removed"), token);
        }
        catch (FileStorageException error) when (error.Code == FileStorageError.FileNotFound) { }
    }

    public async Task<ActiveVisualAmbienceDto?> ReadActiveAsync(string surface, DateTimeOffset now, CancellationToken token)
    {
        var scope = surface.Equals("intranet", StringComparison.OrdinalIgnoreCase) ? 1
            : surface.Equals("admincore", StringComparison.OrdinalIgnoreCase) ? 2
            : throw new ArgumentException("El ámbito solicitado no es válido.");
        using var client = await delegatedClients.CreateAsync();
        var (metadata, fields) = await Metadata(client, token);
        var instant = now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var filter = $"statecode eq 0 and {fields.PublicationStatus} eq 2 and ({fields.Scope} eq {scope} or {fields.Scope} eq 3) and {fields.StartsAt} le {instant} and {fields.EndsAt} ge {instant}";
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{metadata.EntitySetName}?$select={fields.Select}&$filter={Uri.EscapeDataString(filter)}&$orderby=modifiedon desc&$top=1", token);
        if (rows.Count == 0) return null;
        var item = Map(rows[0], fields);
        return new(item.Id, item.Theme, item.Effect, item.Intensity, item.PrimaryColor, item.SecondaryColor,
            item.AccentColor, item.AllowAnimation, item.ShowTopDecoration, item.ShowDecorativeBackground,
            item.PromotionalText, item.DestinationUrl, item.AlternativeText,
            item.HasDesktopImage ? $"/api/communications/visual-ambiences/{item.Id:D}/images/desktop" : null,
            item.HasMobileImage ? $"/api/communications/visual-ambiences/{item.Id:D}/images/mobile" : null);
    }

    private static void Validate(VisualAmbienceWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Theme))
            throw new ArgumentException("Nombre, código y tema son obligatorios.");
        if (request.Scope is < 1 or > 3 || request.Effect is < 1 or > 6 || request.Intensity is < 1 or > 3)
            throw new ArgumentException("Ámbito, efecto o intensidad no son válidos.");
        if (request.EndsAt <= request.StartsAt) throw new ArgumentException("La fecha de finalización debe ser posterior a la fecha de inicio.");
        foreach (var color in new[] { request.PrimaryColor, request.SecondaryColor, request.AccentColor })
            if (!string.IsNullOrWhiteSpace(color) && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$"))
                throw new ArgumentException("Los colores deben utilizar el formato hexadecimal #RRGGBB.");
        if (!string.IsNullOrWhiteSpace(request.DestinationUrl)
            && (!Uri.TryCreate(request.DestinationUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw new ArgumentException("La URL de destino debe ser HTTP(S).");
    }

    private static Dictionary<string, object?> Payload(VisualAmbienceWriteRequest request, DataverseTableMetadata metadata, Fields fields) => new()
    {
        [fields.Name] = request.Name.Trim(), [fields.Code] = request.Code.Trim(), [fields.Description] = Clean(request.Description),
        [fields.Scope] = metadata.EncodedIntegerValue("gaia_Ambito", request.Scope), [fields.Theme] = request.Theme.Trim(),
        [fields.Effect] = metadata.EncodedIntegerValue("gaia_Efecto", request.Effect), [fields.StartsAt] = request.StartsAt,
        [fields.EndsAt] = request.EndsAt, [fields.Intensity] = metadata.EncodedIntegerValue("gaia_Intensidad", request.Intensity),
        [fields.PrimaryColor] = Clean(request.PrimaryColor), [fields.SecondaryColor] = Clean(request.SecondaryColor),
        [fields.AccentColor] = Clean(request.AccentColor), [fields.AllowAnimation] = request.AllowAnimation,
        [fields.ShowTopDecoration] = request.ShowTopDecoration, [fields.ShowDecorativeBackground] = request.ShowDecorativeBackground,
        [fields.PromotionalText] = Clean(request.PromotionalText), [fields.DestinationUrl] = Clean(request.DestinationUrl),
        [fields.AlternativeText] = Clean(request.AlternativeText)
    };

    private static VisualAmbienceDto Map(JsonElement row, Fields f) => new(row.GetProperty(f.Id).GetGuid(), Text(row, f.Name) ?? "",
        Text(row, f.Code) ?? "", Text(row, f.Description), Number(row, f.Scope) ?? 0, Text(row, f.Theme) ?? "",
        Number(row, f.Effect) ?? 1, Number(row, f.PublicationStatus) ?? 1, Date(row, f.StartsAt) ?? default,
        Date(row, f.EndsAt) ?? default, Number(row, f.Intensity) ?? 1, Text(row, f.PrimaryColor), Text(row, f.SecondaryColor),
        Text(row, f.AccentColor), Boolean(row, f.AllowAnimation), Boolean(row, f.ShowTopDecoration),
        Boolean(row, f.ShowDecorativeBackground), Text(row, f.PromotionalText), Text(row, f.DestinationUrl),
        Text(row, f.AlternativeText), HasText(row, f.DesktopImage), HasText(row, f.MobileImage), Date(row, "modifiedon") ?? default);

    private static bool ScopesOverlap(int first, int second) => first == 3 || second == 3 || first == second;
    private static string VariantSchema(string variant) => variant switch { "desktop" => "gaia_ImagenEscritorio", "mobile" => "gaia_ImagenMovil", _ => throw new ArgumentException("La variante de imagen no es válida.") };
    private static async Task<(DataverseTableMetadata Metadata, Fields Fields)> Metadata(HttpClient client, CancellationToken token)
    { var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token); return (metadata, Fields.From(metadata)); }
    private static async Task<VisualAmbienceDto> Read(HttpClient client, DataverseTableMetadata metadata, Fields fields, Guid id, CancellationToken token) =>
        Map(await One(client, $"{metadata.EntitySetName}({id:D})?$select={fields.Select}", token), fields);
    private static async Task<JsonElement> One(HttpClient client, string path, CancellationToken token) => await DataverseMetadataResolver.ReadOneAsync(client, path, token) ?? throw new KeyNotFoundException("La ambientación no existe.");
    private static async Task Patch(HttpClient client, DataverseTableMetadata metadata, Guid id, Dictionary<string, object?> payload, CancellationToken token)
    { using var request = new HttpRequestMessage(HttpMethod.Patch, $"{metadata.EntitySetName}({id:D})") { Content = JsonContent.Create(payload) }; request.Headers.TryAddWithoutValidation("If-Match", "*"); using var response = await client.SendAsync(request, token); if (response.StatusCode == HttpStatusCode.NotFound) throw new KeyNotFoundException("La ambientación no existe."); await Ensure(response, token); }
    private static async Task Ensure(HttpResponseMessage response, CancellationToken token) { if (response.IsSuccessStatusCode) return; var body = await response.Content.ReadAsStringAsync(token); throw new InvalidOperationException($"Dataverse rechazó la operación ({(int)response.StatusCode}): {SafeError(body)}"); }
    private static string SafeError(string body) { try { using var json = JsonDocument.Parse(body); return json.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "Error sin descripción."; } catch (JsonException) { return "Respuesta no válida de Dataverse."; } }
    private static Guid CreatedId(HttpResponseMessage response) { var value = response.Headers.TryGetValues("OData-EntityId", out var values) ? values.SingleOrDefault() : null; var match = System.Text.RegularExpressions.Regex.Match(value ?? "", @"\(([0-9a-f-]{36})\)$"); return match.Success ? Guid.Parse(match.Groups[1].Value) : throw new InvalidOperationException("Dataverse no devolvió el identificador creado."); }
    private static async Task EnsureReferenceFits(HttpClient client, string value, string schemaName, CancellationToken token) { var constraints = await DataverseMetadataResolver.ConstraintsAsync(client, Table, token); var constraint = constraints.TryGetValue(schemaName, out var exact) ? exact : constraints.FirstOrDefault(x => x.Value.LogicalName.Equals(schemaName, StringComparison.OrdinalIgnoreCase)).Value; if (constraint?.MaxLength is int maximum && value.Length > maximum) throw new InvalidOperationException($"La columna {schemaName} admite {maximum} caracteres y la referencia segura requiere {value.Length}."); }
    private static string Encode(ExternalFileId id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(id))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static ExternalFileId Decode(string value) { var normalized = value.Replace('-', '+').Replace('_', '/'); normalized += new string('=', (4 - normalized.Length % 4) % 4); return JsonSerializer.Deserialize<ExternalFileId>(Encoding.UTF8.GetString(Convert.FromBase64String(normalized))) ?? throw new FileStorageException(FileStorageError.FileNotFound); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Text(JsonElement row, string field) => row.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool HasText(JsonElement row, string field) => !string.IsNullOrWhiteSpace(Text(row, field));
    private static int? Number(JsonElement row, string field) => DataverseJson.OptionalEncodedInt32(row, field);
    private static bool Boolean(JsonElement row, string field) => row.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.True;
    private static DateTimeOffset? Date(JsonElement row, string field) => DateTimeOffset.TryParse(Text(row, field), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

    private sealed record Fields(string Id,string Name,string Code,string Description,string Scope,string Theme,string Effect,
        string PublicationStatus,string StartsAt,string EndsAt,string Intensity,string PrimaryColor,string SecondaryColor,
        string AccentColor,string AllowAnimation,string ShowTopDecoration,string ShowDecorativeBackground,string PromotionalText,
        string DestinationUrl,string AlternativeText,string DesktopImage,string MobileImage)
    {
        public string Select => string.Join(',',Id,Name,Code,Description,Scope,Theme,Effect,PublicationStatus,StartsAt,EndsAt,
            Intensity,PrimaryColor,SecondaryColor,AccentColor,AllowAnimation,ShowTopDecoration,ShowDecorativeBackground,
            PromotionalText,DestinationUrl,AlternativeText,DesktopImage,MobileImage,"modifiedon","statecode");
        public static Fields From(DataverseTableMetadata m) => new(m.PrimaryIdAttribute,m.Attribute("gaia_Nombre"),
            m.Attribute("gaia_Codigo"),m.Attribute("gaia_Descripcion"),m.Attribute("gaia_Ambito"),m.Attribute("gaia_Tema"),
            m.Attribute("gaia_Efecto"),m.Attribute("gaia_EstadoPublicacion"),m.Attribute("gaia_FechaInicio"),
            m.Attribute("gaia_FechaFinalizacion"),m.Attribute("gaia_Intensidad"),m.Attribute("gaia_ColorPrincipal"),
            m.Attribute("gaia_ColorSecundario"),m.Attribute("gaia_ColorAcento"),m.Attribute("gaia_PermitirAnimacion"),
            m.Attribute("gaia_MostrarDecoracionSuperior"),m.Attribute("gaia_MostrarFondoDecorativo"),
            m.Attribute("gaia_TextoPromocional"),m.Attribute("gaia_UrlDestino"),m.Attribute("gaia_TextoAlternativo"),
            m.Attribute("gaia_ImagenEscritorio"),m.Attribute("gaia_ImagenMovil"));
    }
}
