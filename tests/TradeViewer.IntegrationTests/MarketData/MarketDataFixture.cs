using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Files;
using TradeViewer.Infrastructure.MarketData.Import;
using TradeViewer.Infrastructure.MarketData.Store;
using TradeViewer.IntegrationTests.Workspace;

namespace TradeViewer.IntegrationTests.MarketData;

/// <summary>
/// A temporary output root in the Downloader's layout, written by the test, together with a workspace and a
/// candle store on a temporary data path. Nothing of the real Downloader or its files is touched.
/// </summary>
public sealed class MarketDataFixture : IDisposable
{
    public MarketDataFixture()
    {
        Workspace.Start();
        Directory.CreateDirectory(Workspace.Locations.MarketDataRoot);

        Database = new MarketDataDatabase(
            Workspace.Locations.MarketDataDatabasePath,
            MarketDataSchema.FromEmbeddedScripts(),
            Workspace.Clock,
            NullLogger<MarketDataDatabase>.Instance);
        Database.Initialize();

        Store = new SqliteCandleStore(Database, Workspace.Clock);
        Files = new AssetFiles(Workspace.Locations);
        Importer = new AssetImporter(Files, new BinanceCandleFileReader(), Store, NullLogger<AssetImporter>.Instance);
    }

    public WorkspaceFixture Workspace { get; } = new WorkspaceFixture();

    public MarketDataDatabase Database { get; }

    public SqliteCandleStore Store { get; }

    public AssetFiles Files { get; }

    public AssetImporter Importer { get; }

    public RecordingNotifier Notifier { get; } = new RecordingNotifier();

    public static SymbolInfo Symbol(string symbol)
    {
        return new SymbolInfo("binance", symbol, symbol, AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
    }

    /// <summary>An asset with the complete history in the given timeframes (all five without any).</summary>
    public static Asset Asset(string symbol, params Timeframe[] timeframes)
    {
        var starts = (timeframes.Length == 0 ? Timeframes.All : timeframes).ToDictionary(timeframe => timeframe, _ => (DateOnly?)null);
        return new Asset(Symbol(symbol), new AssetHistory(starts), 1, true);
    }

    public ImportService NewImportService()
    {
        return new ImportService(Workspace.Assets, Files, Importer, Store, Notifier, NullLogger<ImportService>.Instance);
    }

    public string FilePath(string symbol, string name)
    {
        CandleFileRules.TryParseName(name, out var file);
        return Path.Combine(Workspace.Locations.MarketDataRoot, "binance", symbol, Timeframes.Code(file!.Timeframe), name);
    }

    /// <summary>
    /// Writes a candle file with evenly spaced candles over its period, in the Binance format (milliseconds).
    /// A different seed gives different prices, like a rewritten file.
    /// </summary>
    public IReadOnlyList<Candle> WriteFile(string symbol, string name, int count, int seed = 0)
    {
        CandleFileRules.TryParseName(name, out var file);
        var step = Timeframes.Seconds(file!.Timeframe);
        var lines = new List<string>();
        var candles = new List<Candle>();

        for (var index = 0; index < count; index++)
        {
            var time = file.PeriodStart + index * step;
            var price = 100 + seed + (file.PeriodStart / 86400 % 50) + index * 0.25;
            var candle = new Candle(time, price, price + 2, price - 1, price + 1, 10 + index);
            candles.Add(candle);
            lines.Add(string.Join(
                ",",
                (time * 1000).ToString(CultureInfo.InvariantCulture),
                Number(candle.Open),
                Number(candle.High),
                Number(candle.Low),
                Number(candle.Close),
                Number(candle.Volume),
                ((time + step) * 1000 - 1).ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
                "0",
                "0",
                "0"));
        }

        WriteText(symbol, name, string.Join("\n", lines) + (lines.Count > 0 ? "\n" : string.Empty));
        return candles;
    }

    /// <summary>Writes a file with the given content; the write time moves on with every write, as on a real disk.</summary>
    public void WriteText(string symbol, string name, string content)
    {
        var path = FilePath(symbol, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        _writes++;
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc).AddSeconds(_writes));
    }

    private int _writes;

    public IReadOnlyList<Candle> Candles(string symbol, Timeframe timeframe)
    {
        return Store.GetCandles(new SeriesKey("binance", symbol, timeframe), null, 5000);
    }

    public void Dispose()
    {
        Workspace.Dispose();
    }

    private static string Number(double value)
    {
        return value.ToString("0.00000000", CultureInfo.InvariantCulture);
    }
}
