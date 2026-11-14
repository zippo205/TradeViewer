using TradeViewer.ArchitectureTests.Probe.Features.A;

namespace TradeViewer.ArchitectureTests.Probe.Features.B;

/// <summary>A deliberate violation: feature B uses a type of feature A.</summary>
public class ProbeFeatureB
{
    public ProbeFeatureA Other { get; } = new ProbeFeatureA();
}
