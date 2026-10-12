using Api.Configuration;
using Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure logging with Serilog
    builder.ConfigureSerilog();
    
    // Add services to the container.
    builder.Services.AddControllers();

    // configs 
    builder.Services.AddCorsConfiguration(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSwaggerConfiguration();
    builder.Services.AddHealthCheckConfiguration();
    builder.Services.AddRoutingConfiguration();
    builder.Services.AddFluentValidationConfiguration();

    var app = builder.Build();

    // Auto-migrate database on startup
    await app.ApplyMigrationsAsync();

    app.UseSerilogRequestLogging();

    app.MapHealthChecks("/health");

    app.UseSwaggerUIConfiguration();

    app.UseHttpsRedirection();

    app.UseCorsConfiguration();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
    throw;
}
