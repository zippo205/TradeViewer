using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Commands;

/// <summary>"Back up now": makes a backup of the workspace at once (WS3).</summary>
public sealed record CreateBackupCommand : ICommand<BackupResponse>;
