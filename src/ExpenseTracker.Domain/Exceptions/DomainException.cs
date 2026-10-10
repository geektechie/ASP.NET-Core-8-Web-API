namespace ExpenseTracker.Domain.Exceptions;

/// <summary>
/// Base type for violations of a domain invariant — "this object may never exist in
/// that state" — as opposed to application failures such as "not found".
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

/// <summary>Thrown when a monetary amount is outside what the domain accepts.</summary>
public sealed class InvalidAmountException : DomainException
{
    public InvalidAmountException(string message) : base(message)
    {
    }
}

/// <summary>Thrown when a date or date range makes no sense for an expense.</summary>
public sealed class InvalidDateRangeException : DomainException
{
    public InvalidDateRangeException(string message) : base(message)
    {
    }
}

/// <summary>Thrown when text on an entity is missing or too long.</summary>
public sealed class InvalidTextException : DomainException
{
    public InvalidTextException(string message) : base(message)
    {
    }
}

/// <summary>Thrown when an operation is not allowed in the entity's current state.</summary>
public sealed class InvalidOperationForStateException : DomainException
{
    public InvalidOperationForStateException(string message) : base(message)
    {
    }
}
