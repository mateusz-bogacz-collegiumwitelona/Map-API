using Api.Configuration;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// configs 
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSwaggerConfiguration();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();


// Auto-migrate database on startup
await app.ApplyMigrationsAsync();

app.MapHealthChecks("/health");

app.UseSwaggerUIConfiguration();

app.UseHttpsRedirection();

app.UseCorsConfiguration();

app.UseAuthorization();

app.MapControllers();

app.Run();
