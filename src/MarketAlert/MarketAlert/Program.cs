using MarketAlert.Interfaces;
using MarketAlert.Middleware;
using MarketAlert.Models.Configuration;
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
// Services
// --------------------------------------------------

builder.Services.AddControllers();

builder.Services.AddHttpClient<
    ILiveMarketDataService,
    LiveMarketDataService>(client =>
    {
        client.BaseAddress = new Uri(
            "https://dartstock-uatserv.upstox.com/");

        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddScoped<
    IMarketStatusService,
    MarketStatusService>();

builder.Services.AddScoped<
    IHistoricalDataService,
    HistoricalDataService>();

builder.Services.Configure<TriangleSettings>(
    builder.Configuration.GetSection("Triangle"));

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
// Swagger
// --------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --------------------------------------------------
// Global Exception Middleware
// --------------------------------------------------

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();