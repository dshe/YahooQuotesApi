using System.Collections.Generic;
using System.Threading.Tasks;

namespace YahooQuotesApi;

public sealed record ErrorResult(string Message, Exception? Exception = null);

public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? value;
    private readonly ErrorResult? errorResult;

    public bool HasValue { get; }
    public bool HasError { get; }
    public bool IsUndefined => !HasValue && !HasError;

    public T Value {
        get {
            if (value is not null)
                return value;
            throw new InvalidOperationException("Result has no value.");
        }
    }

    public ErrorResult Error {
        get {
            if (errorResult is not null)
                return errorResult;
            throw new InvalidOperationException("Result has no error.");
        }
    }

    private Result(T value) // result may not be null!
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = value;
        HasValue = true;
        errorResult = null;
        HasError = false;
    }

    private Result(ErrorResult errorResult)
    {
        ArgumentNullException.ThrowIfNull(errorResult);
        value = default;
        HasValue = false;
        this.errorResult = errorResult;
        HasError = true;
    }

    public override int GetHashCode()
    {
        if (HasValue)
            return EqualityComparer<T>.Default.GetHashCode(value!);
        if (HasError)
            return EqualityComparer<ErrorResult>.Default.GetHashCode(errorResult!) * -1521134295;
        return 0;
    }

    public override string ToString()
    {
        if (HasValue)
            return $"Value: {Value}";
        if (HasError)
            return $"Error: {Error}";
        return "Undefined";
    }

    public override bool Equals(object? obj) => obj is Result<T> result && Equals(result);

    public bool Equals(Result<T> other) =>
        EqualityComparer<T>.Default.Equals(value, other.value) &&
        EqualityComparer<ErrorResult>.Default.Equals(errorResult, other.errorResult);

    public void Deconstruct(out T value, out ErrorResult error)
    {
        if (IsUndefined)
            throw new ArgumentException("Result is undefined.");
        (value, error) = (Value, Error);
    }

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !(left == right);

#pragma warning disable CA1000 // static members on generic types
    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(ErrorResult errorResult) => new(errorResult);
    public static Result<T> Fail(string message, Exception? ex = null) => new(new ErrorResult(message, ex));
    public static Result<T> Fail(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return new(new ErrorResult(ex.Message, ex));
    }

    public static Result<T> From(Func<T> producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        try
        {
            return Result<T>.Ok(producer());
        }
#pragma warning disable CA1031 // catch a more specific allowed exception type 
        catch (Exception e)
        {
            return Result<T>.Fail(e);
        }
    }

    public static async Task<Result<T>> FromAsync(Func<Task<T>> producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        try
        {
            return Result<T>.Ok(await producer().ConfigureAwait(false));
        }
        catch (Exception e)
        {
            return Result<T>.Fail(e);
        }
    }
}

public static class ResultExtensions
{
    public static Result<T> ToResult<T>(this T value) => Result<T>.Ok(value);
}
