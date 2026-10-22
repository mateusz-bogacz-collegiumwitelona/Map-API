using Api.Configuration;
using Api.Middlewares;
using Infrastructure;
using Serilog;
using Services;

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
    builder.Services.AddHttpContextAccessor();

    // configs 
    builder.Services.AddCorsConfiguration(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSwaggerConfiguration();
    builder.Services.AddHealthCheckConfiguration();
    builder.Services.AddRoutingConfiguration();
    builder.Services.AddFluentValidationConfiguration();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddApplicationServices(builder.Configuration);

    var app = builder.Build();

    app.UseExceptionHandler();

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
