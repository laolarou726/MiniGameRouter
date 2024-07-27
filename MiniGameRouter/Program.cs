using System.Runtime;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Setup logger
// For entry logging

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("SourceContext", "Main")
    .MinimumLevel.Debug()
    .CreateLogger();

builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration));

// Add services to the container.

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddHealthChecks();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Log.Information("Starting MiniGameRouter...");
Log.Information("[GC] IsServer={0} LatencyMode={1} LargeObjectHeapCompactionMode={2}",
    GCSettings.IsServerGC,
    GCSettings.LatencyMode,
    GCSettings.LargeObjectHeapCompactionMode);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors(corsBuilder => corsBuilder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.MapHealthChecks("/ready");
app.MapHealthChecks("/app-health/minigamerouter/readyz");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();