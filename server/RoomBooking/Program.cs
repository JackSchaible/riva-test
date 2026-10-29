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

WebApplication app = builder.Build();

DatabaseMigrator.Migrate(
    app.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."),
    app.Logger);

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

app.MapControllers();

app.Run();

// Make the implicit Program class public for integration testing
public partial class Program { }
