namespace Risk.Application.Exceptions;

/// <summary>Base type for errors raised by the use cases. The API maps each subtype to an HTTP status.</summary>
public abstract class ApplicationExceptionBase(string message) : Exception(message);

public sealed class NotFoundException(string resource, Guid id)
    : ApplicationExceptionBase($"{resource} '{id}' was not found.");

public sealed class ForbiddenException(string message) : ApplicationExceptionBase(message);

public sealed class AuthenticationFailedException()
    : ApplicationExceptionBase("Email or password is incorrect.");

public sealed class ConcurrencyConflictException(Exception inner)
    : ApplicationExceptionBase("The data was changed by someone else. Reload and try again.")
{
    public Exception Inner { get; } = inner;
}

/// <summary>Input failed validation. <see cref="Errors"/> is keyed by field or error code.</summary>
public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : ApplicationExceptionBase("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}
