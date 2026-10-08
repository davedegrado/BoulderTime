using System.Text.Json;
using System.Text.Json.Serialization;
using BoulderTime.Api.Auth;
using BoulderTime.Api.Cli;
using BoulderTime.Api.Errors;
using BoulderTime.Application;
using BoulderTime.Application.Abstractions;
using BoulderTime.Infrastructure;

using BoulderTime.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddSupabaseAuthentication(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddMemoryCache();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ApiExceptionHandler.Customize);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services
    .AddControllers()
    // Enums travel as SCREAMING_SNAKE strings ("OWNER", "PENDING") to match the product vocabulary.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false)));

builder.Services.AddBoulderTimeRateLimiting(builder.Configuration);

// Behind a hosting proxy (Railway, Fly, …) every request arrives from the proxy's address. Reading the client's real
// address and scheme from the proxy headers keeps per-client rate limits and HTTPS detection correct.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                         | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    // The platform's proxy addresses aren't known in advance; the app is only reachable through it.
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 1;
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetPreflightMaxAge(TimeSpan.FromHours(1))));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (AdminCli.IsCommand(args))
    return await AdminCli.RunAsync(app, args);

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(); // ProblemDetails bodies for bare 401/403/404

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
if (builder.Configuration.GetValue("RateLimiting:Enabled", true)) app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<UserProvisioningMiddleware>();
app.UseMiddleware<AccountSuspensionMiddleware>();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
return 0;

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
