using System.Collections.Generic;
using System.Threading.Tasks;
namespace YahooQuotesApi;

public struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? value;
    private readonly string error = "";
    public bool HasValue => error.Length == 0;
    public bool HasError => error.Length != 0;
    public bool HasNothing => error.Length == 0 && !HasValue;

    public T Value {
        get {
            if (HasValue && value is not null)
                return value;
            throw new InvalidOperationException("Result has no value.");
        }
    }

    public string Error {
        get {
            if (HasError)
                return error;
            throw new InvalidOperationException("Result has no error.");
        }
    }

    private Result(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = value;
    }

    private Result(string error)
    {
        value = default;
        if (string.IsNullOrEmpty(error))
            throw new ArgumentException("Invalid error.", nameof(error));
        this.error = error;
    }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error) => new(error);
    public static Result<T> Nothing() => new();

    public override string ToString() => HasValue ? $"Value: {Value}" : $"Error: {Error}";
    public void Deconstruct(out T value, out string error) => (value, error) = (Value, Error);

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

    public override int GetHashCode()
    {
        if (value == null)
            return -1521134295 + EqualityComparer<string>.Default.GetHashCode(error);
        return EqualityComparer<T>.Default.GetHashCode(value) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(error);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Result<T> result)
            return Equals(result);
        return false;
    }

    public bool Equals(Result<T> other)
    {
        if (EqualityComparer<T>.Default.Equals(value, other.value))
            return EqualityComparer<string>.Default.Equals(error, other.error);
        return false;
    }

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !(left == right);
}

public static partial class ResultExtensions
{
    public static Result<T> ToResult<T>(this T value) => Result<T>.Ok(value);
}
