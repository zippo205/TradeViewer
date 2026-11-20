namespace TradeViewer.Web.Scenarios;

/// <summary>Where the user placed or let go a named point, as the chart reports it (time in Unix seconds); the server snaps it.</summary>
public sealed record PlacedPoint(string Field, int? Index, long Time, double Price, string? Kind);
