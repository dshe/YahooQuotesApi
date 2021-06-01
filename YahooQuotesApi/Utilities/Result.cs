using System;
using System.Threading.Tasks;

namespace YahooQuotesApi
{
    public sealed class Result<T>
    {
        private readonly T? value = default;

        private readonly string error = "";
        public bool HasValue { get; }
        public bool HasError => error != "";
        public bool HasNothing => error == "" && !HasValue;

        public T Value
        {
            get
            {
                if (HasValue && value != null)
                    return value;
                throw new InvalidOperationException("Result has no value.");
            }
        }

        public string Error
        {
            get
            {
                if (HasError)
                    return error;
                throw new InvalidOperationException("Result has no error.");
            }
        }

        private Result() { }

        private Result(T value)
        {
            if (value == null)
                return;
            this.value = value;
            HasValue = true;
        }

        private Result(string error)
        {
            if (string.IsNullOrEmpty(error))
                throw new ArgumentException("invalid value", nameof(error));
            this.error = error;
        }

        public static Result<T> Ok(T value) => new(value: value);
        public static Result<T> Fail(string error) => new(error: error);
        public static Result<T> Nothing() => new();

        public void Deconstruct(out T value, out string error) => (value, error) = (Value, Error);

        public override string ToString() => HasValue ? $"Value: {Value}" : $"Error: {Error}";

        public static Result<T> From(Func<T> producer)
        {
            try
            {
                return Result<T>.Ok(producer());
            }
            catch (Exception e)
            {
                return Result<T>.Fail($"{e.GetType().Name}: {e.Message}");
            }
        }

        public static async Task<Result<T>> From(Func<Task<T>> producer)
        {
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

    public static class ResultExtensions
    {
        public static Result<T> ToResult<T>(this T value) => Result<T>.Ok(value);
    }

}
