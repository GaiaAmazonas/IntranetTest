using Gaia.Api.Infrastructure.Dataverse.Organization;
using Gaia.Api.Infrastructure.Dataverse.ThirdParties;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Security;
using Gaia.Modules.Identity;
using Gaia.Modules.Inventory;
using Gaia.Modules.Organization;
using Gaia.Modules.Security;
using Gaia.Modules.ThirdParties;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddTransient<DataverseDiagnosticsHandler>();
builder.Services.AddHttpClient("Dataverse", client =>
{
    var endpoint = builder.Configuration["Dataverse:WebApiEndpoint"]
        ?? throw new InvalidOperationException("Dataverse:WebApiEndpoint is required.");
    client.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
    client.DefaultRequestHeaders.Add("OData-Version", "4.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
}).AddHttpMessageHandler<DataverseDiagnosticsHandler>();
builder.Services.AddScoped<OrganizationDataverseImporter>();
builder.Services.AddScoped<IDataverseDelegatedClientFactory, DataverseDelegatedClientFactory>();
builder.Services.AddScoped<IOrganizationUnitReader, DataverseOrganizationUnitReader>();
builder.Services.AddScoped<IOrganizationUnitCreator, DataverseOrganizationUnitCreator>();
builder.Services.AddScoped<IOrganizationUnitUpdater, DataverseOrganizationUnitUpdater>();
builder.Services.AddScoped<IOrganizationUnitTypeReader, DataverseOrganizationUnitTypeReader>();
builder.Services.AddScoped<IOrganizationUnitTypeCreator, DataverseOrganizationUnitTypeCreator>();
builder.Services.AddScoped<IOrganizationUnitTypeUpdater, DataverseOrganizationUnitTypeUpdater>();
builder.Services.AddScoped<IOrganizationSiteReader, DataverseOrganizationSiteReader>();
builder.Services.AddScoped<IOrganizationSiteCreator, DataverseOrganizationSiteCreator>();
builder.Services.AddScoped<IOrganizationSiteUpdater, DataverseOrganizationSiteUpdater>();
builder.Services.AddScoped<IOrganizationPositionStore, DataverseOrganizationPositionStore>();
builder.Services.AddScoped<DataverseThirdPartyStore>();
builder.Services.AddScoped<IThirdPartyReader>(provider => provider.GetRequiredService<DataverseThirdPartyStore>());
builder.Services.AddScoped<IThirdPartyWriter>(provider => provider.GetRequiredService<DataverseThirdPartyStore>());
builder.Services.AddScoped<IDocumentTypeReader>(provider => provider.GetRequiredService<DataverseThirdPartyStore>());
builder.Services.AddScoped<ICollaboratorEmailStore, DataverseCollaboratorEmailStore>();
builder.Services.AddScoped<ICollaboratorPhoneStore, DataverseCollaboratorPhoneStore>();
builder.Services.AddScoped<IAdministrativePersonnelImporter, DataversePersonnelImporter>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<DataverseSecurityStore>();
builder.Services.AddScoped<ISecurityStore>(provider => provider.GetRequiredService<DataverseSecurityStore>());
builder.Services.AddScoped<IAdminCoreAuthorization>(provider => provider.GetRequiredService<DataverseSecurityStore>());
builder.Services.AddIdentityModule(
    builder.Configuration,
    builder.Environment.IsDevelopment());
builder.Services.AddOrganizationModule(builder.Configuration);
builder.Services.AddThirdPartiesModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddSecurityModule();
builder.Services.AddAuthorizationBuilder().AddPolicy("SecurityBootstrap", policy =>
    policy.RequireAssertion(context =>
    {
        var email = context.User.FindFirst("preferred_username")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var administrators = builder.Configuration.GetSection("Authorization:BootstrapAdministrators").Get<string[]>() ?? [];
        return email is not null && administrators.Contains(email, StringComparer.OrdinalIgnoreCase);
    }));
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebDevelopment", policy =>
        policy
            .WithOrigins("https://localhost:3000", "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();
var logApiCompleted = LoggerMessage.Define<string, string, int, long>(LogLevel.Information,
    new EventId(4200, "ApiRequestCompleted"), "Gaia.Api {Method} {Path} returned {StatusCode} in {ElapsedMs} ms");
app.Use(async (context, next) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await next();
    logApiCompleted(app.Logger, context.Request.Method, context.Request.Path,
        context.Response.StatusCode, stopwatch.ElapsedMilliseconds, null);
});
var logReauthentication = LoggerMessage.Define(Microsoft.Extensions.Logging.LogLevel.Information, new EventId(4101, "DataverseReauthenticationRequired"),
    "El usuario debe renovar la autorización delegada de Dataverse.");

app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception exception) when (DataverseReauthentication.IsRequired(exception))
    {
        logReauthentication(app.Logger, exception);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://gaiaamazonas.org/problems/reauth-required",
            title = "Tu sesión necesita renovarse",
            status = StatusCodes.Status401Unauthorized,
            code = "reauth_required",
            detail = "Por seguridad, vuelve a iniciar sesión para continuar."
        });
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("WebDevelopment");
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "gaia-api",
    version = typeof(Program).Assembly.GetName().Version?.ToString()
}))
.WithName("GetHealth")
.WithTags("Platform");

app.MapIdentityEndpoints();
app.MapDataverseEndpoints();
app.MapOrganizationEndpoints();
app.MapThirdPartiesEndpoints();
app.MapInventoryEndpoints();
app.MapSecurityEndpoints();

await app.Services.InitializeOrganizationAsync();
await app.Services.InitializeThirdPartiesAsync();
await app.Services.InitializeInventoryAsync();

app.Run();

public partial class Program;
