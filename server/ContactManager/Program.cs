using ContactManager.Services;
using ContactManager.Services.DbConnectionFactory;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IContactService, ContactService>();

builder.Services.AddControllers();

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
app.UseHttpsRedirection();

app.UseRouting();
app.MapControllers();

app.Run();
