using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.ChartSettings.Queries;
using TradeViewer.Contracts.Charts;

namespace TradeViewer.Application.Features.ChartSettings.Handlers;

public sealed class GetChartSettingsQueryHandler : IQueryHandler<GetChartSettingsQuery, ChartSettingsResponse>
{
    private readonly IAssetRepository _assets;
    private readonly IChartSettingsRepository _settings;

    public GetChartSettingsQueryHandler(IAssetRepository assets, IChartSettingsRepository settings)
    {
        _assets = assets;
        _settings = settings;
    }

    public async Task<Result<ChartSettingsResponse>> HandleAsync(GetChartSettingsQuery query, CancellationToken cancellationToken)
    {
        if (await _assets.GetAsync(query.Source, query.Symbol, cancellationToken) == null)
        {
            return Error.NotFound("asset.notFound", "There is no asset " + query.Source + "/" + query.Symbol + ".");
        }

        var saved = await _settings.GetAsync(query.Source, query.Symbol, cancellationToken);
        return Result.Success(saved ?? ChartSettingsResponse.Default);
    }
}
