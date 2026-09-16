namespace BoulderTime.Application.Common;

/// <summary>
/// Base type for expected failures. The API maps these to RFC 7807 problem responses with a stable
/// machine-readable <see cref="Code"/> so clients never have to parse messages.
/// </summary>
public abstract class AppException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class UnauthorizedException(string message = "Sign in to continue.")
    : AppException(message, "unauthorized");

public sealed class ForbiddenException(string message = "You don't have permission to do that.", string code = "forbidden")
    : AppException(message, code);

public sealed class NotFoundException(string entity, object key)
    : AppException($"{entity} was not found.", "not_found")
{
    public string Entity { get; } = entity;
    public object Key { get; } = key;
}

public sealed class ConflictException(string message, string code = "conflict")
    : AppException(message, code);

public sealed class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more fields are invalid.", "validation_failed") => Errors = errors;

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}

/// <summary>Raised by the persistence layer, translated from the provider-specific error.</summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception inner)
    : Exception($"Unique constraint '{constraintName}' was violated.", inner)
{
    public string? ConstraintName { get; } = constraintName;
}
