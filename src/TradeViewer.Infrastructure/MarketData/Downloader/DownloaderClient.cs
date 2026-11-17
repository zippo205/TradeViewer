using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Downloader.Contracts.Catalog;
using Downloader.Contracts.Lookup;
using Downloader.Contracts.Status;
using Downloader.Contracts.Subscriptions;
using Microsoft.Extensions.Options;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.Configuration;

namespace TradeViewer.Infrastructure.MarketData.Downloader;

/// <summary>
/// The typed HttpClient for the Downloader. It speaks the copied contract records and hands out the models
/// of the Application; problem details, timeouts and network errors become result errors, never exceptions.
/// </summary>
public sealed class DownloaderClient : IDownloaderClient
{
    /// <summary>
    /// The start sent for a complete history. The Downloader clips a start before what the source has to the
    /// first available day, for every timeframe; without a date its own defaults would apply (4h: 2 years).
    /// </summary>
    public static readonly DateOnly CompleteHistoryFrom = new DateOnly(1970, 1, 1);

    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly DownloaderOptions _options;

    public DownloaderClient(HttpClient http, IOptions<MarketDataOptions> options)
    {
        _http = http;
        _options = options.Value.Downloader;
    }

    public bool Enabled => _options.Enabled;

    public async Task<Result<CatalogPage>> SearchAsync(string source, string text, int limit, CancellationToken cancellationToken)
    {
        // Without a text the Downloader lists the first symbols of the source.
        var query = string.IsNullOrEmpty(text) ? string.Empty : "q=" + Uri.EscapeDataString(text) + "&";
        var path = "api/v1/sources/" + Escape(source) + "/symbols?" + query + "limit=" + limit.ToString(CultureInfo.InvariantCulture);
        var result = await SendAsync<CatalogResponse>(HttpMethod.Get, path, null, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var symbols = result.Value.Symbols
            .Select(symbol => new CatalogEntry(symbol.SourceSymbol, symbol.Symbol, symbol.DisplayName, symbol.AssetClass, symbol.Exchange))
            .ToList();
        return Result.Success(new CatalogPage(Utc(result.Value.RefreshedAt), result.Value.Total, result.Value.Truncated, symbols));
    }

    public async Task<Result<SymbolLookup>> LookupAsync(string source, string ticker, CancellationToken cancellationToken)
    {
        var result = await SendAsync<SymbolLookupResponse>(
            HttpMethod.Get,
            "api/v1/sources/" + Escape(source) + "/symbols/" + Escape(ticker),
            null,
            cancellationToken);

        return result.IsFailure ? result.Error : ToLookup(result.Value);
    }

    public async Task<Result<SubscriptionResult>> SubscribeAsync(string source, string symbol, string ticker, AssetHistory history, CancellationToken cancellationToken)
    {
        var request = new SubscriptionRequest
        {
            SourceSymbol = ticker,
            Timeframes = history.Timeframes.Select(Timeframes.Code).ToList(),
            HistoryFromByTimeframe = history.Timeframes.ToDictionary(Timeframes.Code, timeframe => history.StartOf(timeframe) ?? CompleteHistoryFrom)
        };

        var result = await SendAsync<SubscriptionResponse>(HttpMethod.Put, SubscriptionPath(source, symbol), request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var timeframes = new List<TimeframeSubscription>();
        foreach (var item in result.Value.Timeframes)
        {
            if (Timeframes.TryParse(item.Timeframe, out var timeframe))
            {
                timeframes.Add(new TimeframeSubscription(timeframe, item.Status == "accepted", item.HistoryFrom, item.Clipped == true));
            }
        }

        return Result.Success(new SubscriptionResult(timeframes));
    }

    public async Task<Result<Unit>> UnsubscribeAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        var result = await SendAsync<object>(HttpMethod.Delete, SubscriptionPath(source, symbol), null, cancellationToken);

        // 404: there was no subscription, which is what was wanted.
        return result.IsSuccess || result.Error.Type == ErrorType.NotFound ? Result.Success() : result.Error;
    }

    public async Task<Result<IReadOnlyList<(string Source, string Symbol)>>> GetOwnSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var path = "api/v1/clients/" + Escape(_options.Client) + "/subscriptions";
        var result = await SendAsync<List<SubscriptionResponse>>(HttpMethod.Get, path, null, cancellationToken);

        // 404: the Downloader has never heard of this client, so it has no subscriptions.
        if (result.IsFailure && result.Error.Type == ErrorType.NotFound)
        {
            return Result.Success<IReadOnlyList<(string, string)>>(Array.Empty<(string, string)>());
        }

        if (result.IsFailure)
        {
            return result.Error;
        }

        IReadOnlyList<(string, string)> subscriptions = result.Value.Select(item => (item.Source, item.Symbol)).ToList();
        return Result.Success(subscriptions);
    }

    public async Task<Result<SymbolStatus>> GetSymbolStatusAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        var result = await SendAsync<SymbolStatusResponse>(HttpMethod.Get, StatusPath(source, symbol), null, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var timeframes = new List<TimeframeStatus>();
        foreach (var item in result.Value.Timeframes)
        {
            if (!Timeframes.TryParse(item.Timeframe, out var timeframe))
            {
                continue;
            }

            timeframes.Add(new TimeframeStatus(
                timeframe,
                item.WantedFrom,
                Utc(item.CoveredFrom),
                Utc(item.CoveredTo),
                item.PendingJobs,
                item.Gaps.Select(gap => new CoverageGap(Utc(gap.From)!.Value, Utc(gap.To)!.Value, gap.Reason)).ToList(),
                item.LastError));
        }

        var subscribed = result.Value.SubscribedBy.Contains(_options.Client, StringComparer.Ordinal);
        return Result.Success(new SymbolStatus(result.Value.LastChange, subscribed, timeframes));
    }

    public async Task<Result<SymbolChanges>> GetChangesAsync(string source, string symbol, long after, CancellationToken cancellationToken)
    {
        var path = StatusPath(source, symbol) + "/changes?after=" + after.ToString(CultureInfo.InvariantCulture);
        var result = await SendAsync<SymbolChangesResponse>(HttpMethod.Get, path, null, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var files = result.Value.Changes.Select(change => change.File).Distinct(StringComparer.Ordinal).ToList();
        return Result.Success(new SymbolChanges(result.Value.LastChange, result.Value.Complete, files));
    }

    private static Result<SymbolLookup> ToLookup(SymbolLookupResponse response)
    {
        if (!AssetCodes.TryParse(response.AssetClass, out AssetClass assetClass) || !AssetCodes.TryParse(response.Session.Type, out SessionType sessionType))
        {
            return Error.InvalidData(
                "downloader.unknownValue",
                "The Downloader describes " + response.Symbol + " with an asset class or session TradeViewer does not know ("
                + response.AssetClass + ", " + response.Session.Type + ").");
        }

        var timeframes = new List<Timeframe>();
        foreach (var code in response.SourceTimeframes)
        {
            // A timeframe TradeViewer does not know yet (1m, 1M, ...) is left out.
            if (Timeframes.TryParse(code, out var timeframe))
            {
                timeframes.Add(timeframe);
            }
        }

        var earliest = new Dictionary<Timeframe, DateOnly>();
        foreach (var pair in response.Earliest)
        {
            if (Timeframes.TryParse(pair.Key, out var timeframe))
            {
                earliest[timeframe] = pair.Value;
            }
        }

        var info = new SymbolInfo(
            response.Source,
            response.Symbol,
            string.IsNullOrEmpty(response.DisplayName) ? response.Symbol : response.DisplayName,
            assetClass,
            (double)(response.TickSize ?? 0.01m),
            new Session(sessionType, response.Session.TimeZone, response.Session.Open, response.Session.Close),
            timeframes.OrderBy(Timeframes.Seconds).ToList(),
            string.IsNullOrEmpty(response.SourceSymbol) || response.SourceSymbol == response.Symbol ? null : response.SourceSymbol);

        return Result.Success(new SymbolLookup(info, earliest));
    }

    private async Task<Result<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        where T : class
    {
        if (!Enabled)
        {
            return Error.Unavailable("downloader.disabled", "The Downloader is switched off (MarketData:Downloader:Enabled).");
        }

        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body != null)
            {
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);
            }

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await ToErrorAsync(response, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return Result.Success<T>(null!);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
            return value == null
                ? Error.InvalidData("downloader.emptyAnswer", "The Downloader answered " + path + " without content.")
                : Result.Success(value);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.Unavailable("downloader.timeout", "The Downloader did not answer in time.");
        }
        catch (HttpRequestException ex)
        {
            return Error.Unavailable("downloader.unreachable", "The Downloader cannot be reached: " + ex.Message);
        }
        catch (JsonException)
        {
            return Error.InvalidData("downloader.invalidAnswer", "The answer of the Downloader to " + path + " could not be read.");
        }
    }

    /// <summary>Maps the problem details of the Downloader; its message is kept, it is shown to the user (AS2).</summary>
    private static async Task<Error> ToErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var message = "The Downloader answered " + (int)response.StatusCode + ".";
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (var name in new[] { "detail", "title" })
            {
                if (document.RootElement.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String && text.GetString()!.Length > 0)
                {
                    message = text.GetString()!;
                    break;
                }
            }
        }
        catch (JsonException)
        {
            // no problem details: the status has to do
        }

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                return Error.Validation("downloader.invalidRequest", message);
            case HttpStatusCode.NotFound:
                return Error.NotFound("downloader.notFound", message);
            case HttpStatusCode.UnprocessableEntity:
                return Error.InvalidData("downloader.unknownSymbol", message);
            default:
                return Error.Unavailable("downloader.unavailable", message);
        }
    }

    private string SubscriptionPath(string source, string symbol)
    {
        return "api/v1/clients/" + Escape(_options.Client) + "/subscriptions/" + Escape(source) + "/" + Escape(symbol);
    }

    private static string StatusPath(string source, string symbol)
    {
        return "api/v1/status/" + Escape(source) + "/" + Escape(symbol);
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static DateTimeOffset? Utc(DateTime? value)
    {
        return value == null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
    }
}
