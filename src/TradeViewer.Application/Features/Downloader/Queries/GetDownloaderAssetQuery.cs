using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Downloader;

namespace TradeViewer.Application.Features.Downloader.Queries;

/// <summary>Coverage of an asset of the workspace (DL2); the sync's last status when the Downloader does not answer.</summary>
public sealed record GetDownloaderAssetQuery(string Source, string Symbol) : IQuery<DownloaderAssetResponse>;
