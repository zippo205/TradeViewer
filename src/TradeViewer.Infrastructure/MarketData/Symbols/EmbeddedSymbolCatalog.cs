using System.Text.Json;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.MarketData.Symbols;

/// <summary>
/// The symbol files shipped with TradeViewer (MarketData/Symbols/{source}.json, embedded), in the format of
/// the Downloader's symbols.json. They are read once at the start; a file this build cannot read stops it.
/// </summary>
public sealed class EmbeddedSymbolCatalog : ISymbolCatalog
{
    private const int KnownVersion = 1;

    private static readonly string ResourcePrefix = typeof(EmbeddedSymbolCatalog).Namespace + ".";

    private readonly Dictionary<string, List<SymbolInfo>> _symbols = new Dictionary<string, List<SymbolInfo>>(StringComparer.Ordinal);

    public EmbeddedSymbolCatalog()
    {
        var assembly = typeof(EmbeddedSymbolCatalog).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !resource.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            var source = resource.Substring(ResourcePrefix.Length, resource.Length - ResourcePrefix.Length - ".json".Length);
            using var stream = assembly.GetManifestResourceStream(resource)!;
            _symbols[source] = Parse(source, stream);
        }
    }

    public IReadOnlyList<string> Sources => _symbols.Keys.OrderBy(source => source, StringComparer.Ordinal).ToList();

    public IReadOnlyList<SymbolInfo> Search(string source, string? text)
    {
        if (!_symbols.TryGetValue(source, out var symbols))
        {
            return Array.Empty<SymbolInfo>();
        }

        var wanted = text?.Trim();
        if (string.IsNullOrEmpty(wanted))
        {
            return symbols;
        }

        return symbols
            .Where(symbol => symbol.Symbol.Contains(wanted, StringComparison.OrdinalIgnoreCase)
                || symbol.DisplayName.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public SymbolInfo? Find(string source, string symbol)
    {
        return _symbols.TryGetValue(source, out var symbols)
            ? symbols.FirstOrDefault(candidate => candidate.Symbol == symbol)
            : null;
    }

    /// <summary>Reads a symbols.json. Unknown fields are ignored; an unknown version or value is an error.</summary>
    public static List<SymbolInfo> Parse(string source, Stream json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var version = root.GetProperty("version").GetInt32();
        if (version != KnownVersion)
        {
            throw new InvalidOperationException(
                "The symbol file of " + source + " has version " + version + ", but this version of TradeViewer only knows version " + KnownVersion + ".");
        }

        var symbols = new List<SymbolInfo>();
        foreach (var entry in root.GetProperty("symbols").EnumerateObject())
        {
            var value = entry.Value;
            var session = value.GetProperty("session");

            if (!AssetCodes.TryParse(value.GetProperty("assetClass").GetString(), out AssetClass assetClass))
            {
                throw Invalid(source, entry.Name, "assetClass");
            }

            if (!AssetCodes.TryParse(session.GetProperty("type").GetString(), out SessionType sessionType))
            {
                throw Invalid(source, entry.Name, "session.type");
            }

            var timeframes = new List<Timeframe>();
            foreach (var code in value.GetProperty("sourceTimeframes").EnumerateArray())
            {
                // A timeframe TradeViewer does not know yet (1m, 1M, ...) is left out.
                if (Timeframes.TryParse(code.GetString(), out var timeframe))
                {
                    timeframes.Add(timeframe);
                }
            }

            symbols.Add(new SymbolInfo(
                source,
                entry.Name,
                value.GetProperty("displayName").GetString() ?? entry.Name,
                assetClass,
                value.GetProperty("tickSize").GetDouble(),
                new Session(sessionType, session.GetProperty("timeZone").GetString() ?? "UTC", OptionalText(session, "open"), OptionalText(session, "close")),
                timeframes.OrderBy(Timeframes.Seconds).ToList()));
        }

        return symbols.OrderBy(symbol => symbol.Symbol, StringComparer.Ordinal).ToList();
    }

    private static string? OptionalText(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static InvalidOperationException Invalid(string source, string symbol, string field)
    {
        return new InvalidOperationException("The symbol file of " + source + " has an unknown " + field + " for " + symbol + ".");
    }
}
