using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.ArchitectureTests.Probe.Layers;

/// <summary>A deliberate violation: pretend this type is in the Domain, which must not use the Application.</summary>
public class ProbeLayerViolator
{
    public Result<int> Value { get; } = Result.Success(1);
}
