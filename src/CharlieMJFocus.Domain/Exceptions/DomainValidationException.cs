namespace CharlieMJFocus.Domain.Exceptions;

/// <summary>
/// Thrown when user-supplied data breaks a business rule (for example an empty
/// title or an end time before the start time). The message is safe to show in the UI.
/// </summary>
public sealed class DomainValidationException(string message) : Exception(message);
