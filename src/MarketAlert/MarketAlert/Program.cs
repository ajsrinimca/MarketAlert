using MarketAlert.Library;
using MarketAlert.Library.Endpoints;
using NLog;
using NLog.Config;
using NLog.Web;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// MarketAlert configuration
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
// Load NSE / BSE tickers
// --------------------------------------------------

var nseSymbols = await LoadTickersAsync(
    Path.Combine(
        builder.Environment.ContentRootPath,
        "Tickers",
        "NSETickerList.json"));

var bseSymbols = await LoadTickersAsync(
    Path.Combine(
        builder.Environment.ContentRootPath,
        "Tickers",
        "BSETickerList.json"));

// --------------------------------------------------
// MarketAlert services
// --------------------------------------------------

builder.Services.AddMarketAlert(
    builder.Configuration,
    nseSymbols,
    bseSymbols);

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
// MarketAlert endpoints
// --------------------------------------------------

app.MapMarketAlert();

// --------------------------------------------------
// Run
// --------------------------------------------------

app.Run();

// ==================================================
// Helper
// ==================================================

static async Task<IReadOnlyList<string>> LoadTickersAsync(
    string filePath)
{
    if (!File.Exists(filePath))
    {
        throw new FileNotFoundException(
            $"Ticker JSON file was not found: {filePath}",
            filePath);
    }

    var json =
        await File.ReadAllTextAsync(filePath);

    var instruments =
        JsonSerializer.Deserialize<List<MarketInstrumentJson>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

    if (instruments == null)
    {
        throw new InvalidOperationException(
            $"Unable to deserialize ticker JSON file: {filePath}");
    }

    var tickers = instruments
        .Where(x => !string.IsNullOrWhiteSpace(x.Ticker))
        .Select(x => x.Ticker.Trim().ToUpperInvariant())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (tickers.Count == 0)
    {
        throw new InvalidOperationException(
            $"No tickers were found in JSON file: {filePath}");
    }

    return tickers;
}

// ==================================================
// JSON model
// ==================================================

internal sealed class MarketInstrumentJson
{
    public string Exchange { get; set; } = string.Empty;

    public string Ticker { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string SymbolName { get; set; } = string.Empty;

    public string Series { get; set; } = string.Empty;

    public string Groups { get; set; } = string.Empty;

    public int XchInstype { get; set; }
}
