using Microsoft.AspNetCore.Mvc;
using RoomBooking.Database;
using RoomBooking.Models.Responses;
using RoomBooking.Services;
using RoomBooking.Services.DbConnectionFactory;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IBookingService, BookingService>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            List<string> errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();

            ApiResponse<object> apiResponse = ApiResponse<object>.ValidationErrorResult(errors);
            return new BadRequestObjectResult(apiResponse);
        };
    });

builder.Services.AddHealthChecks();

// CORS is off unless origins are configured: locally the frontend calls the API same-origin through Vite.
// The deployed demo sets Cors:AllowedOrigins to the frontend's exact origin.
string[] corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .WithMethods("GET", "POST")
        .WithHeaders("Content-Type")
        .WithExposedHeaders("Location")
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
}

WebApplication app = builder.Build();

string connectionString = app.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
MigrationMode migrationMode = app.Configuration.GetValue("Database:Migrations", MigrationMode.OnStartup);

if (migrationMode != MigrationMode.Skip)
{
    DatabaseMigrator.Migrate(connectionString, app.Logger);
}

if (migrationMode == MigrationMode.Only)
{
    // The deployment's one-off migration task: also provision the API's least-privilege role, then exit.
    string? appRole = app.Configuration["Database:AppRole:Name"];
    if (appRole is not null)
    {
        AppRoleProvisioner.Provision(connectionString, appRole,
            app.Configuration["Database:AppRole:Password"] ?? "", app.Logger);
    }

    return;
}

if (app.Environment.IsDevelopment())
{
    await DevelopmentSeeder.SeedAsync(app.Services.GetRequiredService<IDbConnectionFactory>(), app.Logger);
}

// Unhandled errors still use the ApiResponse envelope, without leaking exception details.
app.UseExceptionHandler(errorApp => errorApp.Run(context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    return context.Response.WriteAsJsonAsync(ApiResponse<object>.ErrorResult("An unexpected error occurred"));
}));

if (corsOrigins.Length > 0)
{
    app.UseCors();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Make the implicit Program class public for integration testing
public partial class Program { }
