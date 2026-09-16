using System.Text.Json;
using System.Text.Json.Serialization;
using BoulderTime.Api.Auth;
using BoulderTime.Api.Cli;
using BoulderTime.Api.Errors;
using BoulderTime.Application;
using BoulderTime.Application.Abstractions;
using BoulderTime.Infrastructure;

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

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<UserProvisioningMiddleware>();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
return 0;

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
