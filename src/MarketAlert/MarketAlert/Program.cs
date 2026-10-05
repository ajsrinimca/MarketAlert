using MarketAlert.Library;
using NLog;
using NLog.Config;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// TriangleAlert configuration
// --------------------------------------------------

builder.Configuration.ConfigureMarketAlert();

// --------------------------------------------------
// NLog configuration
// --------------------------------------------------

var logDirectory = Path.Combine(
    builder.Environment.ContentRootPath,
    "Logs");

Directory.CreateDirectory(logDirectory);

var nlogConfig = new XmlLoggingConfiguration(
    Path.Combine(
        builder.Environment.ContentRootPath,
        "nlog.config"));

nlogConfig.Variables["logDirectory"] = logDirectory;

LogManager.Configuration = nlogConfig;

builder.Logging.ClearProviders();
builder.Host.UseNLog();

// --------------------------------------------------
// TriangleAlert services
// --------------------------------------------------

builder.Services.AddMarketAlertService(
    builder.Configuration);

// --------------------------------------------------
// Controllers
// --------------------------------------------------

builder.Services.AddControllers();

// --------------------------------------------------
// Swagger
// --------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --------------------------------------------------
// Build application
// --------------------------------------------------

var app = builder.Build();

// --------------------------------------------------
// Swagger
// --------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --------------------------------------------------
// Middleware
// --------------------------------------------------

app.UseHttpsRedirection();

app.UseAuthorization();

// --------------------------------------------------
// TriangleAlert endpoints
// --------------------------------------------------

app.MapTriangleAlert();

// --------------------------------------------------
// Run
// --------------------------------------------------

app.Run();