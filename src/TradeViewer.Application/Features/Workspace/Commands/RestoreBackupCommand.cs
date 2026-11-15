using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Workspace.Commands;

/// <summary>Replaces the workspace by the backup of that name (WS3).</summary>
public sealed record RestoreBackupCommand(string Name) : ICommand<Unit>;
