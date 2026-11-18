using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Downloader;

namespace TradeViewer.Application.Features.Downloader.Queries;

/// <summary>All subscriptions of the Downloader (DL3); unavailable when it does not answer or is switched off.</summary>
public sealed record GetDownloaderSubscriptionsQuery : IQuery<DownloaderSubscriptionsResponse>;
