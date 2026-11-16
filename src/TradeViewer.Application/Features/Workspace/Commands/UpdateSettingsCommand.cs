using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Commands;

/// <summary>
/// Changes the settings. A part that is null stays as it is: the settings page sends the default history,
/// which counts for assets added afterwards (AS3); a chart sends itself as the last chart (CP3).
/// </summary>
public sealed record UpdateSettingsCommand(IReadOnlyDictionary<string, int?>? DefaultHistory, LastChartResponse? LastChart) : ICommand<SettingsResponse>;
