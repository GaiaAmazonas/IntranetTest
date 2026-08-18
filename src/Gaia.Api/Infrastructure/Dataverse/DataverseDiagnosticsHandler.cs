using System.Diagnostics;
namespace Gaia.Api.Infrastructure.Dataverse;
internal sealed class DataverseDiagnosticsHandler(ILogger<DataverseDiagnosticsHandler> logger) : DelegatingHandler
{
    private static readonly Action<ILogger,string?,string?,long,Exception?> LogCompleted = LoggerMessage.Define<string?,string?,long>(LogLevel.Information,new EventId(4201,"DataverseRequestCompleted"),"Dataverse {Method} {Path} completed in {ElapsedMs} ms");
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { var stopwatch=Stopwatch.StartNew(); try{return await base.SendAsync(request,cancellationToken);} finally{LogCompleted(logger,request.Method.Method,request.RequestUri?.PathAndQuery,stopwatch.ElapsedMilliseconds,null);} }
}
