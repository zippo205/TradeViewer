using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Watchlists.Commands;

namespace TradeViewer.Application.Features.Watchlists.Validators;

public sealed class ReorderWatchlistCommandValidator
{
    /// <returns>Null when the request can be worked on, otherwise the error naming the fields.</returns>
    public Error? Validate(ReorderWatchlistCommand command)
    {
        var problems = new Dictionary<string, string[]>();

        if (command.Assets == null)
        {
            problems["assets"] = new[] { "The assets of the list in their new order are required." };
        }
        else if (command.Assets.Any(asset => asset == null || string.IsNullOrWhiteSpace(asset.Source) || string.IsNullOrWhiteSpace(asset.Symbol)))
        {
            problems["assets"] = new[] { "Every asset needs its source and symbol." };
        }

        return problems.Count == 0 ? null : Error.Validation("watchlist.order.invalid", "The order is not valid.", problems);
    }
}
