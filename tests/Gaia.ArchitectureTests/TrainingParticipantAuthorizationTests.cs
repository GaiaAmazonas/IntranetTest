using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Security;
using Gaia.Modules.Training;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.ArchitectureTests;

public sealed class TrainingParticipantAuthorizationTests
{
    [Theory]
    [InlineData("/api/training/my/assignments/{id:guid}/evaluations","GET","INT.CAPACITACIONES.VER")]
    [InlineData("/api/training/my/assignments/{id:guid}/evaluations/{evaluationId:guid}/start","POST","INT.CAPACITACIONES.VER")]
    [InlineData("/api/training/my/assignments/{id:guid}/attempts/{attemptId:guid}/submit","POST","INT.CAPACITACIONES.VER")]
    [InlineData("/api/training/my/assignments/{id:guid}/resources/{resourceId:guid}","GET","INT.CAPACITACIONES.VER")]
    [InlineData("/api/training/administration/pending-reviews","GET","CAP.REVISAR")]
    [InlineData("/api/training/administration/attempts/{attemptId:guid}/grade","POST","CAP.REVISAR")]
    [InlineData("/api/training/administration/results/export","GET","CAP.RESULTADOS.EXPORTAR")]
    [InlineData("/api/training/administration/versions/{versionId:guid}/resources/{resourceId:guid}","GET","CAP.CONTENIDO.VER")]
    public void EveryParticipantAndReviewerOperationRequiresItsOwnPolicy(string route,string method,string policy)
    {
        var builder=WebApplication.CreateBuilder();
        builder.Services.AddScoped<ITrainingParticipantOperations>(_=>throw new NotSupportedException());
        builder.Services.AddScoped<ITrainingOperations>(_=>throw new NotSupportedException());
        builder.Services.AddScoped<ISecurityStore>(_=>throw new NotSupportedException());
        builder.Services.AddScoped<IFileStorage>(_=>throw new NotSupportedException());
        var app=builder.Build();app.MapTrainingParticipantEndpoints();
        var endpoint=((IEndpointRouteBuilder)app).DataSources.SelectMany(x=>x.Endpoints).OfType<RouteEndpoint>().Single(x=>x.RoutePattern.RawText==route&&x.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),x=>x.Policy==policy);
    }
}
