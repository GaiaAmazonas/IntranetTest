using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal sealed class GraphFileTransport(HttpClient client, IGraphApplicationTokenProvider tokens,
    SharePointStorageConfiguration configuration, TimeProvider clock) : IDisposable
{
    public void Dispose() => client.Dispose();
    internal const int MaximumRetries = 3;

    public async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, bool authenticated,
        bool retry, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            ValidateDestination(request.RequestUri!, authenticated);
            if (authenticated) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(cancellationToken));
            HttpResponseMessage response;
            try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
            catch (HttpRequestException)
            {
                if (!retry || attempt >= MaximumRetries) throw new FileStorageException(FileStorageError.TransientFailure);
                await DelayAsync(null, attempt, cancellationToken); continue;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (!retry || attempt >= MaximumRetries) throw new FileStorageException(FileStorageError.TransientFailure);
                await DelayAsync(null, attempt, cancellationToken); continue;
            }
            if (!retry || !IsTransient(response.StatusCode) || attempt >= MaximumRetries) return response;
            using (response) await DelayAsync(response.Headers.RetryAfter, attempt, cancellationToken);
        }
    }

    public async Task DelayAsync(RetryConditionHeaderValue? retryAfter, int attempt, CancellationToken cancellationToken)
    {
        var delay = retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - clock.GetUtcNow() : TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        // Do not retry earlier than Graph requested; fail instead when outside our request budget.
        if (delay > TimeSpan.FromSeconds(120)) throw new FileStorageException(FileStorageError.TransientFailure);
        if (delay > TimeSpan.Zero) await Task.Delay(delay, clock, cancellationToken);
    }

    internal void ValidateDestination(Uri uri, bool authenticated)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
            || (authenticated ? uri.Host != "graph.microsoft.com" || !uri.AbsolutePath.StartsWith("/v1.0/", StringComparison.Ordinal)
                : !configuration.TransferAllowedHosts.Contains(uri.IdnHost)))
            throw new FileStorageException(FileStorageError.FileUnauthorized);
    }

    internal static bool IsTransient(HttpStatusCode code) => code is HttpStatusCode.TooManyRequests
        or HttpStatusCode.RequestTimeout or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
        or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    internal static void EnsureSuccess(HttpResponseMessage response, FileStorageError? accessError = null)
    {
        if (response.IsSuccessStatusCode) return;
        throw new FileStorageException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => FileStorageError.InvalidCredentials,
            HttpStatusCode.Forbidden => accessError ?? FileStorageError.FileUnauthorized,
            HttpStatusCode.NotFound => accessError ?? FileStorageError.FileNotFound,
            HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed => FileStorageError.VersionConflict,
            HttpStatusCode.RequestEntityTooLarge => FileStorageError.FileTooLarge,
            HttpStatusCode.UnsupportedMediaType => FileStorageError.TypeNotAllowed,
            _ => FileStorageError.TransientFailure
        });
    }

    internal static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await response.Content.LoadIntoBufferAsync(1024 * 1024, cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.Clone();
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException)
        { throw new FileStorageException(FileStorageError.TransientFailure); }
    }
}
