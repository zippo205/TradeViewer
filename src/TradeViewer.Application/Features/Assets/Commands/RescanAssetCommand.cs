using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Assets.Commands;

/// <summary>"Reload": imports the files of the asset that are new or changed.</summary>
public sealed record RescanAssetCommand(string Source, string Symbol) : ICommand<Unit>;
