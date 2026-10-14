using Gaia.Api.Infrastructure.Dataverse.Organization;
using Gaia.Api.Infrastructure.Dataverse.ThirdParties;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Security;
using Gaia.Api.Infrastructure.Dataverse.Communications;
using Gaia.Api.Infrastructure.Dataverse.Solicitudes;
using Gaia.Api.Infrastructure.Dataverse.Training;
using Gaia.Api.Infrastructure.Dataverse.Notifications;
using Gaia.BuildingBlocks.Notifications;
using Gaia.Modules.Communications;
using Gaia.Modules.Identity;
using Gaia.Modules.Inventory;
using Gaia.Modules.Solicitudes;
using Gaia.Modules.Organization;
using Gaia.Modules.Security;
using Gaia.Modules.ThirdParties;
using Gaia.Modules.Training;
using Gaia.Api.Infrastructure.Files;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
    builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddConsole();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = 262_144_000);

builder.Services.AddGaiaFileStorage(builder.Configuration, builder.Environment.EnvironmentName);
var dataverseConfiguration = DataverseConfiguration.From(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
if (!builder.Environment.IsDevelopment())
{
    var keysPath = builder.Configuration["DataProtection:KeysPath"];
    var certificateThumbprint = builder.Configuration["DataProtection:CertificateThumbprint"];
    if (string.IsNullOrWhiteSpace(keysPath) || string.IsNullOrWhiteSpace(certificateThumbprint))
        throw new InvalidOperationException("DataProtection:KeysPath and CertificateThumbprint are required outside Development.");
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    var certificate = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, validOnly: true)
        .OfType<X509Certificate2>().SingleOrDefault()
        ?? throw new InvalidOperationException("The Data Protection certificate is unavailable or invalid.");
    builder.Services.AddDataProtection()
        .SetApplicationName("Gaia.Platform")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .ProtectKeysWithCertificate(certificate);
}
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-Gaia.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = builder.Environment.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Strict;
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("oid")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddSingleton(dataverseConfiguration);
builder.Services.AddTransient<DataverseDiagnosticsHandler>();
builder.Services.AddHttpClient("Dataverse", client =>
{
    client.BaseAddress = dataverseConfiguration.WebApiEndpoint;
    client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
    client.DefaultRequestHeaders.Add("OData-Version", "4.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
}).AddHttpMessageHandler<DataverseDiagnosticsHandler>();
builder.Services.AddHttpClient("GraphProfilePhotos", client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Accept.ParseAdd("image/*");
});
builder.Services.AddScoped<OrganizationDataverseImporter>();
builder.Services.AddScoped<IDataverseDelegatedClientFactory, DataverseDelegatedClientFactory>();
builder.Services.AddScoped<IOrganizationUnitReader, DataverseOrganizationUnitReader>();
builder.Services.AddScoped<IOrganizationUnitCreator, DataverseOrganizationUnitCreator>();
builder.Services.AddScoped<IOrganizationUnitUpdater, DataverseOrganizationUnitUpdater>();
builder.Services.AddScoped<IOrganizationUnitDeleter, DataverseOrganizationUnitDeleter>();
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
builder.Services.AddScoped<IIntranetDirectoryReader, DataverseIntranetDirectoryReader>();
builder.Services.AddSingleton<ProfilePhotoMemoryCache>();
builder.Services.AddScoped<IProfilePhotoReader, MicrosoftGraphProfilePhotoReader>();
builder.Services.AddScoped<IAdministrativePersonnelImporter, DataversePersonnelImporter>();
builder.Services.AddScoped<IOrganizationalAssignmentStore, DataverseOrganizationalAssignmentStore>();
builder.Services.AddScoped<IOrganizationalAssignmentImporter, OrganizationalAssignmentWorkbookImporter>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<DataverseSecurityStore>();
builder.Services.AddScoped<ISecurityStore>(provider => provider.GetRequiredService<DataverseSecurityStore>());
builder.Services.AddScoped<IAdminCoreAuthorization>(provider => provider.GetRequiredService<DataverseSecurityStore>());
builder.Services.AddScoped<ICommunicationsStore, DataverseCommunicationsStore>();
builder.Services.AddScoped<PublicLoginSnapshotStore>();
builder.Services.AddScoped<ILoginConfigurationStore, DataverseLoginConfigurationStore>();
builder.Services.AddScoped<IVisualAmbienceStore, DataverseVisualAmbienceStore>();
builder.Services.AddScoped<ISolicitudesAttachmentStore, DataverseSolicitudesAttachmentStore>();
builder.Services.AddScoped<ISolicitudesAttachmentPolicyStore, DataverseSolicitudesAttachmentPolicyStore>();
builder.Services.AddScoped<ISolicitudesHistoryStore, DataverseSolicitudesHistoryStore>();
builder.Services.AddScoped<ISolicitudesPortalReader, DataverseSolicitudesPortalReader>();
builder.Services.AddScoped<DataverseSolicitudesWorkflowDefinitionReader>();
builder.Services.AddScoped<DataverseSolicitudesWorkflowExecutionWriter>();
builder.Services.AddScoped<ISolicitudesWorkflowStore>(provider=>provider.GetRequiredService<DataverseSolicitudesWorkflowExecutionWriter>());
builder.Services.AddScoped<SolicitudesWorkflowApplication>();
builder.Services.AddScoped<ISolicitudesRequestStore, DataverseSolicitudesRequestStore>();
builder.Services.AddScoped<ISolicitudesFormReader, DataverseSolicitudesFormReader>();
builder.Services.AddScoped<IEmailNotificationOutbox, DataverseEmailNotificationQueue>();
builder.Services.AddScoped<ISolicitudesNotificationPublisher, DataverseSolicitudesNotificationPublisher>();
builder.Services.AddScoped<ISolicitudesRequestApplication, SolicitudesRequestApplication>();
builder.Services.AddScoped<ISolicitudesConversationStore, DataverseSolicitudesConversationStore>();
builder.Services.AddScoped<ISolicitudesConversationApplication, SolicitudesConversationApplication>();
builder.Services.AddScoped<ISolicitudesManagementStore, DataverseSolicitudesManagementStore>();
builder.Services.AddScoped<ISolicitudesManagementApplication, SolicitudesManagementApplication>();
builder.Services.AddScoped<ISolicitudesAdministrationAuthorization, DataverseSolicitudesAdministrationAuthorization>();
builder.Services.AddScoped<ISolicitudesAttachmentApplication, SolicitudesAttachmentApplication>();

builder.Services.AddScoped<ISolicitudesObservationApplication, SolicitudesObservationApplication>();
builder.Services.AddScoped<ITrainingAdministrationReader, DataverseTrainingAdministrationReader>();
builder.Services.AddScoped<ITrainingOperations, DataverseTrainingOperations>();
builder.Services.AddScoped<ITrainingParticipantOperations, DataverseTrainingOperations>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddIdentityModule(
    builder.Configuration,
    builder.Environment.IsDevelopment());
builder.Services.AddOrganizationModule(builder.Configuration);
builder.Services.AddThirdPartiesModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddSecurityModule();
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("SecurityBootstrap", policy =>
    policy.RequireAssertion(context =>
    {
        if (!builder.Configuration.GetValue("Authorization:BootstrapEnabled", false)) return false;
        var email = context.User.FindFirst("preferred_username")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var administrators = builder.Configuration.GetSection("Authorization:BootstrapAdministrators").Get<string[]>() ?? [];
        return email is not null && administrators.Contains(email, StringComparer.OrdinalIgnoreCase);
    }));
var configuredWebUrl = builder.Configuration["WebApplication:BaseUrl"]
    ?? throw new InvalidOperationException("WebApplication:BaseUrl is required.");
if (!Uri.TryCreate(configuredWebUrl, UriKind.Absolute, out var configuredWebUri)
    || configuredWebUri.Scheme is not ("http" or "https"))
    throw new InvalidOperationException("WebApplication:BaseUrl must be an absolute HTTP or HTTPS URL.");
if (!builder.Environment.IsDevelopment() && configuredWebUri.Scheme != Uri.UriSchemeHttps)
    throw new InvalidOperationException("WebApplication:BaseUrl must use HTTPS outside Development.");
var allowedWebOrigins = builder.Environment.IsDevelopment()
    ? new[] { configuredWebUri.GetLeftPart(UriPartial.Authority), "https://localhost:3000", "http://localhost:3000" }.Distinct().ToArray()
    : new[] { configuredWebUri.GetLeftPart(UriPartial.Authority) };
builder.Services.AddCors(options => options.AddPolicy("WebClient", policy => policy
    .WithOrigins(allowedWebOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    await next();
});
var logApiCompleted = LoggerMessage.Define<string, string, int, long>(LogLevel.Information,
    new EventId(4200, "ApiRequestCompleted"), "Gaia.Api {Method} {Path} returned {StatusCode} in {ElapsedMs} ms");
app.Use(async (context, next) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
        correlationId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    try { await next(); }
    finally
    {
        logApiCompleted(app.Logger, context.Request.Method, context.Request.Path,
            context.Response.StatusCode, stopwatch.ElapsedMilliseconds, null);
    }
});
var logReauthentication = LoggerMessage.Define(Microsoft.Extensions.Logging.LogLevel.Information, new EventId(4101, "DataverseReauthenticationRequired"),
    "El usuario debe renovar la autorización delegada de Dataverse.");
var logDataverseUnavailable = LoggerMessage.Define(Microsoft.Extensions.Logging.LogLevel.Error, new EventId(4102, "DataverseUnavailable"),
    "No fue posible establecer comunicación con Dataverse.");

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
    catch (DataverseConnectivityException exception)
    {
        logDataverseUnavailable(app.Logger, exception);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://gaiaamazonas.org/problems/dataverse-unavailable",
            title = "Dataverse no está disponible",
            status = StatusCodes.Status503ServiceUnavailable,
            code = "dataverse_unavailable",
            detail = "No fue posible conectar con el servicio de datos. Intenta nuevamente o contacta al administrador."
        });
    }
    catch (Gaia.BuildingBlocks.Files.FileStorageException exception)
    {
        context.Response.StatusCode = exception.Code == Gaia.BuildingBlocks.Files.FileStorageError.FileTooLarge
            ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title = exception.Code == Gaia.BuildingBlocks.Files.FileStorageError.FileTooLarge
                ? "El archivo es demasiado grande" : "No fue posible cargar el archivo",
            status = context.Response.StatusCode,
            detail = exception.Message
        });
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("WebClient");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var unsafeMethod = !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method)
        && !HttpMethods.IsTrace(context.Request.Method);
    if (!unsafeMethod || !context.Request.Path.StartsWithSegments("/api") || context.User.Identity?.IsAuthenticated != true)
    {
        await next();
        return;
    }

    var origin = context.Request.Headers.Origin.FirstOrDefault();
    var referer = context.Request.Headers.Referer.FirstOrDefault();
    var trustedSource = allowedWebOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
        || (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri)
            && allowedWebOrigins.Contains(refererUri.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase));
    if (!trustedSource)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { title = "Origen no permitido", status = 403, code = "csrf_origin_rejected" });
        return;
    }

    try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
    catch (AntiforgeryValidationException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { title = "Solicitud no válida", status = 400, code = "csrf_validation_failed" });
        return;
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "gaia-api"
}))
.AllowAnonymous()
.WithName("GetHealth")
.WithTags("Platform");

app.MapIdentityEndpoints();
app.MapDataverseEndpoints();
app.MapFileStorageDiagnostics();
app.MapOrganizationEndpoints();
app.MapThirdPartiesEndpoints();
app.MapInventoryEndpoints();
app.MapSecurityEndpoints(builder.Configuration.GetValue("Authorization:BootstrapEnabled", false));
app.MapSolicitudesEndpoints();
if(app.Environment.IsDevelopment())
{
    app.MapDevelopmentSolicitudesDataReset();
}
if(app.Environment.IsDevelopment())
{
    app.MapDevelopmentSolicitudesScenarioSeed();
}
app.MapCommunicationsEndpoints();
app.MapTrainingEndpoints();
app.MapGet("/api/intranet/home", async (ICommunicationsStore communications, IIntranetDirectoryReader directory,
    CancellationToken token) =>
{
    var now = DateTimeOffset.UtcNow;
    var bogotaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows()
        ? "SA Pacific Standard Time"
        : "America/Bogota");
    var localToday = TimeZoneInfo.ConvertTime(now, bogotaTimeZone);
    var until = now.AddMonths(6);
    var nextMonth = localToday.Month == 12 ? 1 : localToday.Month + 1;
    var bannersTask = communications.ListPublicBannersAsync(now, token);
    var eventsTask = communications.ListPublicEventsAsync(now.Date, until, token);
    var currentBirthdaysTask = directory.ListBirthdaysAsync(localToday.Month, token);
    var nextBirthdaysTask = directory.ListBirthdaysAsync(nextMonth, token);
    await Task.WhenAll(bannersTask, eventsTask, currentBirthdaysTask, nextBirthdaysTask);
    var birthdays = (await currentBirthdaysTask).Concat(await nextBirthdaysTask)
        .DistinctBy(item => new { item.Id, item.Day, item.Month }).ToArray();
    return Results.Ok(new
    {
        banners = await bannersTask,
        upcomingEvents = (await eventsTask).Take(12),
        birthdays
    });
}).RequireAuthorization(AdminCorePermissions.IntranetInicioVer).WithTags("Intranet");

app.Run();

public partial class Program;
