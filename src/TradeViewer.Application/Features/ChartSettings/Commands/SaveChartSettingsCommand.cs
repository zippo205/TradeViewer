using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Charts;

namespace TradeViewer.Application.Features.ChartSettings.Commands;

/// <summary>
/// Replaces the chart settings of an asset as a whole (CP2); indicators left out (null) stay as saved.
/// Window is the id of the window that made the change: it ignores its own ChartSettingsChanged event.
/// </summary>
public sealed record SaveChartSettingsCommand(string Source, string Symbol, ChartSettingsResponse? Settings, string? Window = null) : ICommand<ChartSettingsResponse>;
