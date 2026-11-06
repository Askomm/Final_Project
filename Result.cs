namespace Code;

public abstract record Result<T, E>
{
    private Result() { }

    public sealed record Success : Result<T, E>
    {
        public T Value { get; }

        public Success(T value)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value), "A successful result must carry a value.");

            Value = value;
        }
    }

    public sealed record Failure : Result<T, E>
    {
        public E Error { get; }

        public Failure(E error)
        {
            if (error is null)
                throw new ArgumentNullException(nameof(error), "A failed result must carry an error.");

            Error = error;
        }
    }

    public static Result<T, E> Ok(T value) => new Success(value);

    public static Result<T, E> Fail(E error) => new Failure(error);

    public Result<U, E> Map<U>(Func<T, U> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return this switch
        {
            Success success => Result<U, E>.Ok(map(success.Value)),
            Failure failure => Result<U, E>.Fail(failure.Error),
            _ => throw new InvalidOperationException("Unknown Result case.")
        };
    }

    public Result<U, E> Bind<U>(Func<T, Result<U, E>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return this switch
        {
            Success success => bind(success.Value)
                ?? throw new InvalidOperationException("Bind function returned no Result."),
            Failure failure => Result<U, E>.Fail(failure.Error),
            _ => throw new InvalidOperationException("Unknown Result case.")
        };
    }
}

public abstract record Option<T>
{
    private Option() { }

    public sealed record Some : Option<T>
    {
        public T Value { get; }

        public Some(T value)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value), "Some must contain a value.");

            Value = value;
        }
    }

    public sealed record None : Option<T>;

    public static Option<T> SomeValue(T value) => new Some(value);

    public static Option<T> NoneValue() => new None();

    public static Option<T> FromNullable(T? value) =>
        value is null ? NoneValue() : SomeValue(value);

    public Result<T, E> ToResult<E>(E error) => this switch
    {
        Some some => Result<T, E>.Ok(some.Value),
        None => Result<T, E>.Fail(error),
        _ => throw new InvalidOperationException("Unknown Option case.")
    };
}
