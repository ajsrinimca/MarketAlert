using NLog;
using NLog.Config;
using NLog.Web;
using TriangleAlert;

var builder = WebApplication.CreateBuilder(args);

// NLog
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

// MarketAlert library configuration

// MarketAlert library services
builder.Services.AddMarketAlertService(
    builder.Configuration);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapTriangleAlert();

app.Run();