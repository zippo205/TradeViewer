using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Queries;

/// <summary>The settings that are not part of a chart (WS5).</summary>
public sealed record GetSettingsQuery : IQuery<SettingsResponse>;
