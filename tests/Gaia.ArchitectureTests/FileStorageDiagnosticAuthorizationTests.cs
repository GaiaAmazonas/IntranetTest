using System.Security.Claims;
using Gaia.Api.Infrastructure.Files;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.ArchitectureTests;

public sealed class FileStorageDiagnosticAuthorizationTests
{
    [Fact]
    public async Task DiagnosticExposesOnlyProtectedReadEndpoint()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGaiaFileStorage(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), "Development");
        await using var app = builder.Build();
        app.MapFileStorageDiagnostics();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        Assert.Equal(3, endpoints.Length);
        Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == "/api/infrastructure/files/diagnostics"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.SequenceEqual(["GET"]));
        Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == "/api/infrastructure/files/diagnostics/write-probe"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.SequenceEqual(["POST"]));
        Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == "/api/infrastructure/files/diagnostics/write-probe"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.SequenceEqual(["GET"]));
        Assert.All(endpoints, endpoint =>
        {
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), item => item.Policy == FileStorageDiagnosticEndpoints.Policy);
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        });
    }

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public async Task PolicyRequiresAuthenticatedAdminCoreAndModuleAdministrator(bool authenticated, bool adminCore, bool modules, bool allowed)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IAdminCoreAuthorization>(new Permissions(adminCore, modules));
        services.AddSecurityModule(); services.AddFileStorageDiagnostics();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], authenticated ? "test" : null));
        var result = await authorization.AuthorizeAsync(principal, null, FileStorageDiagnosticEndpoints.Policy);
        Assert.Equal(allowed, result.Succeeded);
    }

    private sealed class Permissions(bool adminCore, bool modules) : IAdminCoreAuthorization
    {
        public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken token = default) =>
            Task.FromResult(permission == AdminCorePermissions.IntranetAdminCoreVer ? adminCore
                : permission == AdminCorePermissions.TiModulosAdministrar && modules);
    }
}
