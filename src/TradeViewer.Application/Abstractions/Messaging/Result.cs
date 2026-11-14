namespace TradeViewer.Application.Abstractions.Messaging;

/// <summary>The outcome of a handler: a value or an <see cref="Messaging.Error"/>.</summary>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException("A failed result has no value.");
            }

            return _value!;
        }
    }

    public Error Error
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("A successful result has no error.");
            }

            return _error!;
        }
    }

    public static Result<T> Success(T value)
    {
        return new Result<T>(value);
    }

    public static Result<T> Failure(Error error)
    {
        return new Result<T>(error);
    }

    public static implicit operator Result<T>(Error error)
    {
        return Failure(error);
    }
}

/// <summary>Shortcuts that let the compiler infer the type.</summary>
public static class Result
{
    public static Result<T> Success<T>(T value)
    {
        return Result<T>.Success(value);
    }

    /// <summary>A successful command that returns nothing.</summary>
    public static Result<Unit> Success()
    {
        return Result<Unit>.Success(default);
    }

    public static Result<T> Failure<T>(Error error)
    {
        return Result<T>.Failure(error);
    }
}
