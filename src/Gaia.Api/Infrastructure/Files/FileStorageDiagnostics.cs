using System.Net;
using System.Text;
using System.Text.Json;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Security;

namespace Gaia.Api.Infrastructure.Files;

// Technical statuses only: no tenant IDs, signed URLs, credentials or provider response bodies.
internal sealed record FileStorageDiagnosticReport(
    string Configuration = "Unknown", string Authentication = "Unknown", string Repository = "Unknown",
    string Container = "Unknown", string RootFolder = "Unknown", string Read = "Unknown",
    string Write = "Unknown", string? ErrorCode = null);

internal interface IFileStorageDiagnostics
{
    Task<FileStorageDiagnosticReport> CheckAsync(CancellationToken cancellationToken);
}

internal sealed record FileStorageWriteProbeReport(
    string Upload = "Unknown", string Download = "Unknown", string Delete = "Unknown",
    string Cleanup = "Unknown", string ValidationRecord = "Unknown", string? ErrorCode = null);

internal sealed class FileStorageWriteProbe(IFileStorage storage, IFileStorageMaintenance maintenance,
    IConfiguration configuration, IHostEnvironment environment,
    IEnumerable<ISharePointConfigurationValidationRecorder> validationRecorders)
{
    private static readonly byte[] Content = Encoding.ASCII.GetBytes("%PDF-1.4\n% Gaia storage connectivity probe\n%%EOF\n");

    public async Task<FileStorageWriteProbeReport> RunAsync(CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("FileStorage:SharePoint");
        if (!section.GetValue("RealTestsEnabled", false)
            || environment.IsProduction()
            || !string.Equals(section["RealTestsEnvironment"], environment.EnvironmentName, StringComparison.Ordinal))
            return new(ErrorCode: "RealTestsDisabled");

        StoredFile? uploaded = null;
        var deleted = false;
        try
        {
            await using var source = new MemoryStream(Content, writable: false);
            uploaded = await storage.UploadAsync(new(new("Diagnostics", "ConnectivityProbe"),
                $"gaia-storage-probe-{Guid.NewGuid():N}.pdf", "application/pdf", Content.Length,
                Guid.NewGuid(), DateTimeOffset.UtcNow), source, cancellationToken);
            await using var download = await storage.DownloadAsync(uploaded.Id, cancellationToken);
            using var copy = new MemoryStream();
            await download.Content.CopyToAsync(copy, cancellationToken);
            if (!copy.ToArray().AsSpan().SequenceEqual(Content))
                throw new FileStorageException(FileStorageError.VersionConflict);
            await maintenance.DeletePhysicallyAsync(new(uploaded.Id, uploaded.ETag, "Controlled connectivity probe"), cancellationToken);
            deleted = true;
            try
            {
                _ = await storage.GetMetadataAsync(uploaded.Id, cancellationToken);
                return new("Available", "Available", "Unavailable", "ResidueDetected", "Unavailable", "VersionConflict");
            }
            catch (FileStorageException error) when (error.Code == FileStorageError.FileNotFound)
            {
                var recorded = await RecordAsync(3, "Lectura y escritura verificadas por la prueba controlada.", cancellationToken);
                return new("Available", "Available", "Available", "Clean", recorded);
            }
        }
        catch (FileStorageException error)
        {
            var cleanup = uploaded is null || deleted ? "Clean" : "ResidueDetected";
            if (uploaded is not null && !deleted)
            {
                try
                {
                    await maintenance.DeletePhysicallyAsync(new(uploaded.Id, uploaded.ETag, "Connectivity probe cleanup"), CancellationToken.None);
                    cleanup = "Clean";
                }
                catch (FileStorageException) { }
            }
            var recorded = await RecordAsync(4, $"Prueba controlada: {error.Code}.", cancellationToken);
            return new(uploaded is null ? "Unavailable" : "Available", uploaded is null ? "Unknown" : "Unavailable",
                deleted ? "Available" : "Unavailable", cleanup, recorded, error.Code.ToString());
        }
    }

    private async Task<string> RecordAsync(int result, string detail, CancellationToken cancellationToken)
    {
        var recorder = validationRecorders.SingleOrDefault();
        if (recorder is null) return "NotConfigured";
        try { await recorder.RecordAsync(result, detail, cancellationToken); return "Updated"; }
        catch (FileStorageException) { return "Unavailable"; }
    }
}

internal sealed class FileStorageDiagnostics(SharePointStorageConfiguration options, GraphFileTransport transport) : IFileStorageDiagnostics
{
    public async Task<FileStorageDiagnosticReport> CheckAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled) return new(Configuration: "Unavailable", ErrorCode: "MissingConfiguration");
        var report = new FileStorageDiagnosticReport(Configuration: "Available");
        var stage = "Repository";
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await ReadAsync($"sites/{Uri.EscapeDataString(options.SiteId)}?$select=id", budget.Token);
            report = report with { Authentication = "Available", Repository = "Available" };
            stage = "Container";
            var next = $"sites/{Uri.EscapeDataString(options.SiteId)}/drives?$select=id";
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var found = false;
            while (visited.Add(next))
            {
                var page = await ReadAsync(next, budget.Token);
                if (!page.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
                    throw new FileStorageException(FileStorageError.TransientFailure);
                found = values.EnumerateArray().Any(item => item.TryGetProperty("id", out var id)
                    && id.ValueKind == JsonValueKind.String && id.GetString() == options.DriveId);
                if (found || !page.TryGetProperty("@odata.nextLink", out var link)) break;
                next = link.GetString() ?? throw new FileStorageException(FileStorageError.TransientFailure);
            }
            if (!found) return report with { Container = "Unavailable", ErrorCode = "ContainerInaccessible" };
            report = report with { Container = "Available" };
            stage = "RootFolder";
            var root = string.Join('/', options.RootFolder.Split('/').Select(Uri.EscapeDataString));
            var folder = await ReadAsync($"drives/{Uri.EscapeDataString(options.DriveId)}/root:/{root}?$select=id,folder,remoteItem", budget.Token);
            if (!folder.TryGetProperty("folder", out _) || folder.TryGetProperty("remoteItem", out _))
                return report with { RootFolder = "Unavailable", ErrorCode = "InvalidLogicalPath" };
            return report with { RootFolder = "Available", Read = "Available" };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return report with { ErrorCode = "DiagnosticTimeout" }; }
        catch (FileStorageException error)
        {
            report = report with { ErrorCode = error.Code.ToString() };
            if (error.Code == FileStorageError.InvalidCredentials)
                return report with { Authentication = "Unavailable" };
            // A timeout/transient failure does not prove lack of permissions.
            if (error.Code == FileStorageError.TransientFailure) return report;
            return stage switch
            {
                "Repository" => report with { Repository = "Unavailable" },
                "Container" => report with { Container = "Unavailable" },
                _ => report with { RootFolder = "Unavailable" }
            };
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        { return report with { ErrorCode = "InvalidProviderResponse" }; }
    }

    private async Task<JsonElement> ReadAsync(string path, CancellationToken token)
    {
        var uri = Uri.TryCreate(path, UriKind.Absolute, out var absolute)
            ? absolute : new Uri("https://graph.microsoft.com/v1.0/" + path);
        using var response = await transport.SendAsync(() => new(HttpMethod.Get, uri), true, true, token);
        // Preserve the distinction between a rejected token and a denied/missing resource.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new FileStorageException(FileStorageError.InvalidCredentials);
        GraphFileTransport.EnsureSuccess(response);
        return await GraphFileTransport.JsonAsync(response, token);
    }
}

internal static class FileStorageDiagnosticEndpoints
{
    internal const string Policy = "FileStorage.Diagnostics";
    private static readonly Action<ILogger, string, Exception?> LogReport =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(4302, "FileStorageDiagnosticReport"),
            "File storage diagnostic: {Report}");
    private static readonly Action<ILogger, string, Exception?> LogWriteProbe =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4303, "FileStorageWriteProbe"),
            "File storage write probe: {Report}");

    internal static IServiceCollection AddFileStorageDiagnostics(this IServiceCollection services, bool registerServer = true)
    {
        if (registerServer)
        {
            services.AddScoped<FileStorageDiagnostics>();
            services.AddScoped<IFileStorageDiagnostics>(provider => provider.GetRequiredService<FileStorageDiagnostics>());
        }
        services.AddScoped<FileStorageWriteProbe>();
        // Reuse existing administrative permissions; no new Dataverse permission is seeded.
        services.AddAuthorizationBuilder().AddPolicy(Policy, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new AdminCorePermissionRequirement(AdminCorePermissions.IntranetAdminCoreVer),
                new AdminCorePermissionRequirement(AdminCorePermissions.TiModulosAdministrar)));
        return services;
    }

    internal static IEndpointRouteBuilder MapFileStorageDiagnostics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/infrastructure/files/diagnostics", async (IFileStorageDiagnostics diagnostics,
            HttpContext context, ILogger<FileStorageDiagnostics> logger, CancellationToken token) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var report = await diagnostics.CheckAsync(token);
            LogReport(logger,
                $"configuration={report.Configuration}, authentication={report.Authentication}, repository={report.Repository}, container={report.Container}, root={report.RootFolder}, read={report.Read}, error={report.ErrorCode ?? "none"}", null);
            return Results.Ok(report);
        }).RequireAuthorization(Policy).WithTags("Infrastructure");
        endpoints.MapGet("/api/infrastructure/files/diagnostics/write-probe", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            const string page = """
                <!doctype html><html lang="es"><meta charset="utf-8"><title>Prueba de almacenamiento Gaia</title>
                <body><main><h1>Prueba controlada de almacenamiento</h1><p>Esta prueba crea, verifica y elimina un archivo temporal.</p>
                <form method="post"><button type="submit">Ejecutar prueba</button></form></main></body></html>
                """;
            return Results.Content(page, "text/html; charset=utf-8");
        }).RequireAuthorization(Policy).WithTags("Infrastructure");
        endpoints.MapPost("/api/infrastructure/files/diagnostics/write-probe", async (FileStorageWriteProbe probe,
            HttpContext context, ILogger<FileStorageDiagnostics> logger, CancellationToken token) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var report = await probe.RunAsync(token);
            LogWriteProbe(logger,
                $"upload={report.Upload}, download={report.Download}, delete={report.Delete}, cleanup={report.Cleanup}, validationRecord={report.ValidationRecord}, error={report.ErrorCode ?? "none"}", null);
            return Results.Ok(report);
        }).RequireAuthorization(Policy).WithTags("Infrastructure");
        return endpoints;
    }
}
