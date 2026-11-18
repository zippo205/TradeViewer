using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Downloader;

namespace TradeViewer.Application.Features.Downloader.Queries;

/// <summary>The Downloader's state, asked now (DL1, DL4). Never an error: a Downloader that does not answer is a state.</summary>
public sealed record GetDownloaderStatusQuery : IQuery<DownloaderStatusResponse>;
