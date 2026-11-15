using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Queries;

/// <summary>The backups of the workspace, newest first (WS3).</summary>
public sealed record GetBackupsQuery : IQuery<IReadOnlyList<BackupResponse>>;
