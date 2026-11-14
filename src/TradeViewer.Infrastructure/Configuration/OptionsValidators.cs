using Microsoft.Extensions.Options;

namespace TradeViewer.Infrastructure.Configuration;

internal sealed class MarketDataOptionsValidator : IValidateOptions<MarketDataOptions>
{
    public ValidateOptionsResult Validate(string? name, MarketDataOptions options)
    {
        return OptionsValidation.ToResult(ConfigurationRules.Check(options));
    }
}

internal sealed class EwtsOptionsValidator : IValidateOptions<EwtsOptions>
{
    public ValidateOptionsResult Validate(string? name, EwtsOptions options)
    {
        return OptionsValidation.ToResult(ConfigurationRules.Check(options));
    }
}

internal sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        return OptionsValidation.ToResult(ConfigurationRules.Check(options));
    }
}

internal static class OptionsValidation
{
    public static ValidateOptionsResult ToResult(IReadOnlyList<string> errors)
    {
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
