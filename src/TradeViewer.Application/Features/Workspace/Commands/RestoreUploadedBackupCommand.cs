using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Workspace.Commands;

/// <summary>Replaces the workspace by an uploaded backup file (WS3).</summary>
public sealed record RestoreUploadedBackupCommand(Stream Content) : ICommand<Unit>;
