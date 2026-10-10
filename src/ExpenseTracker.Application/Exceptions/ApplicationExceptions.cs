namespace ExpenseTracker.Application.Exceptions;

/// <summary>The requested resource does not exist. Surfaces as HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public static NotFoundException Expense(Guid id) => new($"No expense exists with id {id}.");

    public static NotFoundException Category(Guid id) => new($"No category exists with id {id}.");

    public static NotFoundException Budget(Guid id) => new($"No budget exists with id {id}.");
}

/// <summary>The request collides with existing state. Surfaces as HTTP 409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }

    public static ConflictException CategoryNameTaken(string name) =>
        new($"A category named '{name}' already exists.");

    public static ConflictException CategoryInUse(string name, int expenseCount) =>
        new($"Category '{name}' still has {expenseCount} expense(s). Archive it instead of deleting it.");
}

/// <summary>
/// The request is well-formed but semantically invalid. Surfaces as HTTP 400 with a
/// field-keyed error dictionary — the same shape ASP.NET Core produces for
/// model-state failures, so clients only have one error format to parse.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string field, string message)
        : base(message) =>
        Errors = new Dictionary<string, string[]> { [field] = [message] };

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.") =>
        Errors = new Dictionary<string, string[]>(errors);

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
