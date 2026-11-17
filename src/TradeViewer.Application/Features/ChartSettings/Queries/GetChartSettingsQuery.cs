using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Charts;

namespace TradeViewer.Application.Features.ChartSettings.Queries;

/// <summary>The chart settings of an asset (CP2); the defaults when the user never changed them.</summary>
public sealed record GetChartSettingsQuery(string Source, string Symbol) : IQuery<ChartSettingsResponse>;
