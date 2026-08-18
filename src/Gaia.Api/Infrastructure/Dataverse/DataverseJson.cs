using System.Text.Json;

namespace Gaia.Api.Infrastructure.Dataverse;

internal static class DataverseJson
{
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

    public static async Task<List<JsonElement>> ReadAllAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        var result = new List<JsonElement>();
        string? next = path;
        while (next is not null)
        {
            using var response = await client.GetAsync(next, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Dataverse rechazó la lectura ({(int)response.StatusCode}): {ReadError(content)}");
            }
            using var document = JsonDocument.Parse(content);
            result.AddRange(document.RootElement.GetProperty("value").EnumerateArray().Select(item => item.Clone()));
            next = document.RootElement.TryGetProperty("@odata.nextLink", out var nextLink)
                ? nextLink.GetString()
                : null;
        }
        return result;
    }

    private static string ReadError(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.GetProperty("error").GetProperty("message").GetString()
                ?? "Error sin descripción.";
        }
        catch (JsonException)
        {
            return "Respuesta no válida de Dataverse.";
        }
    }
}
