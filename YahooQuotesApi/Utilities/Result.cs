using System.Diagnostics.CodeAnalysis;

namespace YahooQuotesApi;

public sealed record ErrorResult(string Message, Exception? Exception = null);

public readonly struct Result<T> : IEquatable<Result<T>> where T : notnull
{
    // 3 states driven by HasValue/HasError booleans:
    //   undefined  : !HasValue && !HasError  (default struct)
    //   ok         :  HasValue
    //   error      :  HasError
    // We store the booleans explicitly because T may be a value type,
    // meaning 'value' is never null even in the error/undefined state.
    private readonly T? _value;
    private readonly ErrorResult? _errorResult;

    public bool HasValue { get; }
    public bool HasError { get; }
    public bool IsUndefined => !HasValue && !HasError;

    public T Value
    {
        get
        {
            if (HasValue) 
                return _value!;
            if (HasError) 
                throw new InvalidOperationException(_errorResult!.Message, _errorResult.Exception);
            throw new InvalidOperationException("Result is undefined.");
        }
    }

    public ErrorResult Error
    {
        get
        {
            if (HasError) 
                return _errorResult!;
            throw new InvalidOperationException("Result has no error.");
        }
    }

    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        if (HasValue)
        {
            value = _value!;
            return true;
        }
        value = default!;
        return false;
    }
    public TResult Match<TResult>(Func<T, TResult> ok,
        Func<ErrorResult, TResult> error, Func<TResult> undefined)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(undefined);

        if (HasValue)
            return ok(_value!);

        if (HasError)
            return error(_errorResult!);

        return undefined();
    }

    public TResult Match<TResult>(
        Func<T, TResult> ok, Func<ErrorResult, TResult> error)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(error);

        if (HasValue)
            return ok(_value!);

        if (HasError)
            return error(_errorResult!);

        throw new InvalidOperationException("Result is undefined.");
    }

    internal Result(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
        _errorResult = null;
        HasValue = true;
        HasError = false;
    }

    internal Result(ErrorResult error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _value = default;
        _errorResult = error;
        HasValue = false;
        HasError = true;
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    public bool Equals(Result<T> other) =>
    HasValue == other.HasValue && HasError == other.HasError &&
    EqualityComparer<T>.Default.Equals(_value, other._value) &&
    EqualityComparer<ErrorResult?>.Default.Equals(_errorResult, other._errorResult);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    // HashCode.Combine handles null fields gracefully.
    public override int GetHashCode() => HashCode.Combine(HasValue, HasError, _value, _errorResult);

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !(left == right);

    // ── Formatting ───────────────────────────────────────────────────────────

    public override string ToString() =>
        HasValue ? $"Ok({_value})" :
        HasError ? $"Error({_errorResult!.Message})" :
                    "Undefined";

    // ── Deconstruct ──────────────────────────────────────────────────────────

    // Returns (hasValue, value-or-default, error-or-null) so callers can use
    // a single switch/if without calling into throwing properties.
    public void Deconstruct(out bool hasValue, out T? value, out ErrorResult? error)
    {
        hasValue = HasValue;
        value = _value;
        error = _errorResult;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(ErrorResult error) => new(error);
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
            return Ok(producer()); 
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            return Fail(ex); 
        }
#pragma warning restore CA1031
    }

    public static async Task<Result<T>> FromAsync(Func<Task<T>> producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        try 
        { 
            return Ok(await producer().ConfigureAwait(false)); 
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex) 
        {
            return Fail(ex); 
        }
#pragma warning restore CA1031
    }
}

public static class ResultExtensions
{
    public static Result<T> ToResult<T>(this T value) where T : notnull => Result<T>.Ok(value);

    // Projects a successful result through a mapping function.
    // Error and undefined states pass through unchanged.
    public static Result<TOut> Select<TIn, TOut>(
        this Result<TIn> result, Func<TIn, TOut> projection)
        where TIn : notnull where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (result.HasValue) 
            return Result<TOut>.Ok(projection(result.Value));
        if (result.HasError) 
            return Result<TOut>.Fail(result.Error);
        return default; // undefined → undefined
    }
}
