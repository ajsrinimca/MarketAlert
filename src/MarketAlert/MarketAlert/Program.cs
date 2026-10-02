using MarketAlert.Infrastructure;
using MarketAlert.Interfaces;
using MarketAlert.Models.Configuration;
using MarketAlert.Repositories;
using MarketAlert.Services;
using NLog;
using NLog.Config;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// NLog
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
// Controllers
// --------------------------------------------------

builder.Services.AddControllers();

// --------------------------------------------------
// Database
// --------------------------------------------------

builder.Services.AddSingleton<
    IDbConnectionFactory,
    SqliteConnectionFactory>();

// --------------------------------------------------
// Repositories
// --------------------------------------------------

builder.Services.AddScoped<
    IHistoricalDataRepository,
    HistoricalDataRepository>();

builder.Services.AddScoped<
    IMarketSymbolRepository,
    MarketSymbolRepository>();

builder.Services.AddScoped<
    ISegmentRepository,
    SegmentRepository>();

// --------------------------------------------------
// Services
// --------------------------------------------------

builder.Services.AddScoped<
    IMarketStatusService,
    MarketStatusService>();

builder.Services.AddScoped<
    ISwingPointService,
    SwingPointService>();

builder.Services.AddScoped<
    ITriangleDetectionService,
    TriangleDetectionService>();

builder.Services.AddScoped<
    IMarketDataService,
    MarketDataService>();

builder.Services.AddScoped<
    ITriangleAlertService,
    TriangleAlertService>();

// --------------------------------------------------
// Live Market API
// --------------------------------------------------

builder.Services.AddHttpClient<
    ILiveMarketDataService,
    LiveMarketDataService>(client =>
    {
        client.BaseAddress = new Uri(
            "https://dartstock-uatserv.upstox.com/");

        client.Timeout = TimeSpan.FromSeconds(30);
    });

// --------------------------------------------------
// Configuration
// --------------------------------------------------

builder.Services.Configure<TriangleSettings>(
    builder.Configuration.GetSection("Triangle"));

// --------------------------------------------------
// Swagger
// --------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --------------------------------------------------
// Build
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
// Controllers
// --------------------------------------------------

app.MapControllers();

app.Run();