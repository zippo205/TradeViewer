using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.ChartSettings.Commands;

/// <summary>Deletes an indicator template (IN2). Charts it was applied to keep their indicators.</summary>
public sealed record DeleteIndicatorTemplateCommand(Guid Id) : ICommand<Unit>;
