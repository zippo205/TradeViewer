namespace TradeViewer.Contracts.Workspace;

/// <summary>A backup of the workspace. Kind: daily, manual or before-upgrade.</summary>
public sealed record BackupResponse(string Name, DateTimeOffset CreatedAt, string Kind, long SizeBytes);
