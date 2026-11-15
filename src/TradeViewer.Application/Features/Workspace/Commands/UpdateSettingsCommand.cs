using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Commands;

/// <summary>Replaces the settings as a whole. The default history counts for assets added afterwards (AS3).</summary>
public sealed record UpdateSettingsCommand(IReadOnlyDictionary<string, int?>? DefaultHistory) : ICommand<SettingsResponse>;
