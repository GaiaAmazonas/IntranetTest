using System.Diagnostics;
namespace Gaia.Api.Infrastructure.Dataverse;
internal sealed class DataverseDiagnosticsHandler(ILogger<DataverseDiagnosticsHandler> logger) : DelegatingHandler
{
    private static readonly Action<ILogger,string?,string?,long,Exception?> LogCompleted = LoggerMessage.Define<string?,string?,long>(LogLevel.Information,new EventId(4201,"DataverseRequestCompleted"),"Dataverse {Method} {Path} completed in {ElapsedMs} ms");
    private static readonly Action<ILogger,string?,long,Exception?> LogUnavailable = LoggerMessage.Define<string?,long>(LogLevel.Warning,new EventId(4202,"DataverseRequestUnavailable"),"Dataverse request for {Path} failed after {ElapsedMs} ms.");
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopwatch=Stopwatch.StartNew();
        try { return await base.SendAsync(request,cancellationToken); }
        catch (HttpRequestException exception)
        {
            LogUnavailable(logger, request.RequestUri?.PathAndQuery, stopwatch.ElapsedMilliseconds, exception);
            throw new DataverseConnectivityException("No fue posible conectar con Dataverse.", exception);
        }
        finally { LogCompleted(logger,request.Method.Method,request.RequestUri?.PathAndQuery,stopwatch.ElapsedMilliseconds,null); }
    }
}
