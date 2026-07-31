using Gaia.Modules.Identity;
using Gaia.Modules.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddIdentityModule(builder.Configuration);
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

await app.Services.InitializeIdentityAsync(builder.Configuration);

app.Run();

public partial class Program;
