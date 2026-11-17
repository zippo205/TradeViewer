using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Charts;

namespace TradeViewer.Application.Features.ChartSettings.Commands;

/// <summary>Replaces the chart settings of an asset as a whole (CP2).</summary>
public sealed record SaveChartSettingsCommand(string Source, string Symbol, ChartSettingsResponse? Settings) : ICommand<ChartSettingsResponse>;
