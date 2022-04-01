using System.Collections.Generic;
using System.Threading.Tasks;
namespace YahooQuotesApi;

public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? value;
    private readonly string? error;
    public bool HasValue => error is null || error.Length == 0;
    public bool HasError => error is not null && error.Length != 0;
    public bool IsUndefined => error is null;

    public T Value {
        get {
            if (HasValue)
                return value!;
            throw new InvalidOperationException("Result has no value.");
        }
    }

    public string Error {
        get {
            if (HasError)
                return error!;
            throw new InvalidOperationException("Result has no error.");
        }
    }

    private Result(T value) // result may not be null
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = value;
        error = "";
    }

    private Result(string error)
    {
        if (string.IsNullOrEmpty(error))
            throw new ArgumentException("Invalid error message.", nameof(error));
        this.error = error;
        value = default;
    }

    public override int GetHashCode()
    {
        if (HasValue)
            return EqualityComparer<T>.Default.GetHashCode(value!);
        if (HasError)
            return EqualityComparer<string>.Default.GetHashCode(error!) * -1521134295;
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

    public override bool Equals(object? obj) =>
        obj is Result<T> result && Equals(result);

    public bool Equals(Result<T> other) =>
        EqualityComparer<T>.Default.Equals(value, other.value) &&
        EqualityComparer<string>.Default.Equals(error, other.error);

    public void Deconstruct(out T value, out string error)
    {
        if (IsUndefined)
            throw new ArgumentException("Result is undefined.");
        (value, error) = (Value, Error);
    }

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !(left == right);

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error) => new(error);

    public static Result<T> From(Func<T> producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        try
        {
            return Result<T>.Ok(producer());
        }
        catch (Exception e)
        {
            return Result<T>.Fail($"{e.GetType().Name}: {e.Message}");
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
            return Result<T>.Fail($"{e.GetType().Name}: {e.Message}");
        }
    }
}

public static partial class ResultExtensions
{
    public static Result<T> ToResult<T>(this T value) => Result<T>.Ok(value);
    public static Result<T> ToResultError<T>(this string error) => Result<T>.Fail(error);
    public static Result<T> ToResultUndefined<T>() => default;
}
