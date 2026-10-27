namespace Risk.Domain.Exceptions;

/// <summary>Base type for everything the domain rejects. Never thrown directly.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>An input value is invalid on its own (empty id, negative troop count, ...).</summary>
public sealed class DomainValidationException(string message) : DomainException(message);

/// <summary>The input is valid, but a game rule forbids the action in the current state.</summary>
public sealed class BusinessRuleException(string message) : DomainException(message);
