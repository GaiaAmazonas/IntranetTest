using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gaia.Api.Infrastructure.Dataverse;

internal static class DataverseChangeSet
{
    internal static async Task ExecuteAsync(HttpClient client,IReadOnlyList<(string Method,string Path,object Payload)> operations,CancellationToken token)
    {
        if(operations.Count==0)throw new ArgumentException("El ChangeSet debe contener al menos una operación.",nameof(operations));
        var batch="batch_"+Guid.NewGuid().ToString("N");var change="changeset_"+Guid.NewGuid().ToString("N");var body=new StringBuilder();
        body.Append(CultureInfo.InvariantCulture,$"--{batch}\r\nContent-Type: multipart/mixed; boundary={change}\r\n\r\n");
        var index=0;
        foreach(var operation in operations)
        {
            index++;body.Append(CultureInfo.InvariantCulture,$"--{change}\r\nContent-Type: application/http\r\nContent-Transfer-Encoding: binary\r\nContent-ID: {index}\r\n\r\n{operation.Method} {new Uri(client.BaseAddress!,operation.Path)} HTTP/1.1\r\nContent-Type: application/json\r\n");
            if(operation.Method=="PATCH")body.Append("If-Match: *\r\n");
            body.Append("\r\n").Append(JsonSerializer.Serialize(operation.Payload)).Append("\r\n");
        }
        body.Append(CultureInfo.InvariantCulture,$"--{change}--\r\n--{batch}--\r\n");
        using var message=new HttpRequestMessage(HttpMethod.Post,"$batch"){Content=new StringContent(body.ToString(),Encoding.UTF8)};
        message.Content.Headers.ContentType=MediaTypeHeaderValue.Parse($"multipart/mixed; boundary={batch}");
        using var response=await client.SendAsync(message,token);var result=await response.Content.ReadAsStringAsync(token);
        if(!response.IsSuccessStatusCode||Regex.IsMatch(result,@"HTTP/1\.[01] [45]\d\d"))
            throw new InvalidOperationException($"Dataverse no pudo confirmar el ChangeSet ({(int)response.StatusCode}).");
    }
}
