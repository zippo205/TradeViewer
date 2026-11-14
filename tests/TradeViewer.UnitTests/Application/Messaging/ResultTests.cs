using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.UnitTests.Application.Messaging;

public class ResultTests
{
    [Fact]
    public void Success_holds_its_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Success_without_a_value_is_a_unit_result()
    {
        Result<Unit> result = Result.Success();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Failure_holds_its_error_and_has_no_value()
    {
        Result<int> result = Error.NotFound("asset.notFound", "Unknown asset.");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("asset.notFound", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success("x");

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.InvalidData)]
    [InlineData(ErrorType.Unavailable)]
    public void Every_error_factory_sets_its_type(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Validation => Error.Validation("c", "m"),
            ErrorType.NotFound => Error.NotFound("c", "m"),
            ErrorType.Conflict => Error.Conflict("c", "m"),
            ErrorType.InvalidData => Error.InvalidData("c", "m"),
            _ => Error.Unavailable("c", "m")
        };

        Assert.Equal(type, error.Type);
    }

    [Fact]
    public void Validation_error_carries_the_messages_per_field()
    {
        var fields = new Dictionary<string, string[]> { ["symbol"] = new[] { "Required." } };

        var error = Error.Validation("asset.invalid", "Invalid asset.", fields);

        Assert.Equal(new[] { "Required." }, error.Fields["symbol"]);
        Assert.Empty(Error.NotFound("c", "m").Fields);
    }
}
