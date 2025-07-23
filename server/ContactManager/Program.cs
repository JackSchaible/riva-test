using ContactManager.Services;
using ContactManager.Services.DbConnectionFactory;
using ContactManager.Models.Data.Responses;
using Microsoft.AspNetCore.Mvc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IContactService, ContactService>();

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

if (builder.Environment.IsProduction())
{
    builder.WebHost.UseUrls("http://0.0.0.0:80");
}

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

IConfiguration configuration = app.Configuration;

app.UseCors(options =>
{
    options.WithOrigins(configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5147"])
           .AllowAnyMethod()
           .AllowAnyHeader();
});

app.UseHsts();

app.UseRouting();
app.MapControllers();

app.Run();

// Make the implicit Program class public for integration testing
public partial class Program { }
