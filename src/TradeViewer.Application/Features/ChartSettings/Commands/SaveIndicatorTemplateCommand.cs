using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Commands;

/// <summary>Stores an indicator template under its id, new or replaced (IN2). Charts it was applied to keep their indicators.</summary>
public sealed record SaveIndicatorTemplateCommand(Guid Id, IndicatorTemplateRecord? Template) : ICommand<IndicatorTemplateRecord>;
