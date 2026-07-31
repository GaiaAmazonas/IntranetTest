using Gaia.Modules.Identity;
using Gaia.Modules.Identity.Infrastructure;
using Gaia.Modules.Inventory;
using Gaia.Modules.Organization;
using Gaia.Modules.ThirdParties;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddIdentityModule(
    builder.Configuration,
    builder.Environment.IsDevelopment());
builder.Services.AddOrganizationModule(builder.Configuration);
builder.Services.AddThirdPartiesModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
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
app.MapOrganizationEndpoints();
app.MapThirdPartiesEndpoints();
app.MapInventoryEndpoints();

await app.Services.InitializeIdentityAsync(builder.Configuration);
await app.Services.InitializeOrganizationAsync();
await app.Services.InitializeThirdPartiesAsync();
await app.Services.InitializeInventoryAsync();

app.Run();

public partial class Program;
