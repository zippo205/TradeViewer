using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Queries;

/// <summary>
/// A new indicator of each kind as IN1 sets it up (TradingView's parameters and colours), with an empty id: the
/// client gives each one it adds its own. The defaults live in the Domain only; the client reads them here.
/// </summary>
public sealed record GetIndicatorDefaultsQuery : IQuery<IReadOnlyList<IndicatorRecord>>;
