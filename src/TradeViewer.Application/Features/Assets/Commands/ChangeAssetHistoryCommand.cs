using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Commands;

/// <summary>Changes the history start of timeframes of one asset (AS3): the first day as yyyy-MM-dd per timeframe code, null for the complete history.</summary>
public sealed record ChangeAssetHistoryCommand(string Source, string Symbol, IReadOnlyDictionary<string, string?> HistoryFrom) : ICommand<AssetResponse>;
