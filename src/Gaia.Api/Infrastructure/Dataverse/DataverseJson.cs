using System.Text.Json;

namespace Gaia.Api.Infrastructure.Dataverse;

internal static class DataverseJson
{
    internal sealed record Page(IReadOnlyList<JsonElement> Items, int Total, string? NextLink);

    public static int? OptionalInt32(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    public static int? OptionalEncodedInt32(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : null;
    }

    public static async Task<List<JsonElement>> ReadAllAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        var result = new List<JsonElement>();
        string? next = path;
        while (next is not null)
        {
            ValidateTarget(client, next);
            using var response = await client.GetAsync(next, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Dataverse rechazó la lectura ({(int)response.StatusCode}).");
            }
            using var document = JsonDocument.Parse(content);
            result.AddRange(document.RootElement.GetProperty("value").EnumerateArray().Select(item => item.Clone()));
            next = document.RootElement.TryGetProperty("@odata.nextLink", out var nextLink)
                ? nextLink.GetString()
                : null;
        }
        return result;
    }

    public static async Task<Page> ReadPageAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        ValidateTarget(client, path);
        using var response = await client.GetAsync(path, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Dataverse rechazó la lectura ({(int)response.StatusCode}).");
        }

        using var document = JsonDocument.Parse(content);
        var items = document.RootElement.GetProperty("value")
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
        var total = document.RootElement.TryGetProperty("@odata.count", out var count)
            && count.ValueKind == JsonValueKind.Number
            && count.TryGetInt32(out var number)
                ? number
                : items.Length;
        var nextLink = document.RootElement.TryGetProperty("@odata.nextLink", out var next)
            ? next.GetString()
            : null;
        return new Page(items, total, nextLink);
    }

    private static void ValidateTarget(HttpClient client, string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var absolute)) return;
        var expected = client.BaseAddress ?? throw new InvalidOperationException("Dataverse no tiene un origen configurado.");
        if (!string.Equals(absolute.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(absolute.Host, expected.Host, StringComparison.OrdinalIgnoreCase)
            || absolute.Port != expected.Port)
            throw new InvalidOperationException("Dataverse devolvió una URL de paginación no confiable.");
    }
}
