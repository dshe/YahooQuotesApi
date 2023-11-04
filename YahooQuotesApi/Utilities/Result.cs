namespace YahooQuotesApi;

public sealed record ErrorResult(string Message, Exception? Exception = null);

public readonly struct Result<T> : IEquatable<Result<T>>
{
    // The default value of a struct is the value produced when all fields equal their default values.
    // The fields in this struct are T? and ErrorResult?.
    // The default for these fields is null and IsUndefined is true.
    private readonly T? value;
    private readonly ErrorResult? errorResult;

    public bool HasValue { get; }
    public bool HasError { get; }
    public bool IsUndefined => !HasValue && !HasError;

    public T Value
    {
        get
        {
            if (value is not null)
                return value;
            throw new InvalidOperationException("Result has no value.");
        }
    }

    public ErrorResult Error
    {
        get
        {
            if (errorResult is not null)
                return errorResult;
            throw new InvalidOperationException("Result has no error.");
        }
    }

    internal Result(T value) // result may not be null!
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = value;
        HasValue = true;
        errorResult = null;
        HasError = false;
    }

    internal Result(ErrorResult errorResult)
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
            return new Result<T>(producer());
        }
#pragma warning disable CA1031 // catch a more specific allowed exception type 
        catch (Exception e)
#pragma warning restore CA1031
        {
            return Result<T>.Fail(e);
        }
    }

    public static async Task<Result<T>> FromAsync(Func<Task<T>> producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        try
        {
            return new Result<T>(await producer().ConfigureAwait(false));
        }
#pragma warning disable CA1031 // catch a more specific allowed exception type 
        catch (Exception e)
#pragma warning restore CA1031
        {
            return Result<T>.Fail(e);
        }
    }
}

#pragma warning restore CA1000

public static class ResultExtensions
{
    public static Result<T> ToResult<T>(this T value) => new(value);
    public static Result<T2> ToResult<T1,T2>(this Result<T1> result, Func<T1,T2> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (result.HasValue)
            return new Result<T2>(projection(result.Value));
        if (result.HasError)
            return new Result<T2>(result.Error);
        return new Result<T2>();
    }
}
