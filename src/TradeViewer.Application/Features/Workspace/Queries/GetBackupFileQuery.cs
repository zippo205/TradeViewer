using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Workspace.Queries;

/// <summary>A backup as a file to download (WS3).</summary>
public sealed record GetBackupFileQuery(string Name) : IQuery<FileContent>;
