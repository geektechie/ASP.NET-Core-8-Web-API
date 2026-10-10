using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using ExpenseTracker.Api.Extensions;
using ExpenseTracker.Api.Infrastructure;
using ExpenseTracker.Application;
using ExpenseTracker.Application.Options;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Persistence;

const string CorsPolicy = "Default";

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root. Each layer owns its own registrations; this file only wires
// them together and configures what is genuinely host-level.
// ---------------------------------------------------------------------------
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// ValidateOnStart ships in the hosting package, which the Application layer
// deliberately does not reference, so the check is attached here.
builder.Services.AddOptions<TrackerOptions>().ValidateOnStart();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        // Enums as names, not numbers: "CreditCard" survives a reordering of the
        // enum and is readable in a log or a browser's network tab.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "database");

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    if (allowedOrigins.Length == 0)
    {
        // No configured origins means same-origin only. Failing closed beats
        // quietly allowing any site to call the API through a wildcard.
        policy.WithOrigins().AllowAnyHeader().AllowAnyMethod();
        return;
    }

    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Expense Tracker API",
        Version = "v1",
        Description =
            "Record expenses against categories, set monthly budgets, and read "
            + "aggregated reports: totals by category and payment method, spending "
            + "trends over time, and budget versus actual."
    });

    // Surfaces the XML doc comments from the controllers and the DTOs in Swagger UI.
    foreach (var assembly in new[] { Assembly.GetExecutingAssembly(), typeof(TrackerOptions).Assembly })
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");

        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline. Order matters: exception handling must wrap everything that follows,
// and CORS must run before anything that can short-circuit the request, or the
// browser sees a CORS failure instead of the real status.
// ---------------------------------------------------------------------------
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "Expense Tracker API");

    await app.InitializeDatabaseAsync();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Exposed so integration tests can drive the real host through
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
