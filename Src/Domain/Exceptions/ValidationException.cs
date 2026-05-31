namespace Domain.Exceptions;

/// <summary>Thrown when one or more input values violate domain validation rules. Maps to HTTP 422.</summary>
/// <remarks>
/// <para>
/// The <see cref="Errors"/> dictionary uses field names as keys (e.g. <c>"Email"</c>, <c>"Price"</c>)
/// and an array of human-readable error messages as values. This shape maps directly to
/// <c>ValidationProblemDetails.Errors</c> in the HTTP handler.
/// </para>
/// </remarks>
public sealed class ValidationException : DomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class
    /// with a single summary message and no field errors.
    /// </summary>
    /// <param name="message">A summary description of the validation failure.</param>
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class
    /// with field-level errors.
    /// </summary>
    /// <param name="message">A summary description of the validation failure.</param>
    /// <param name="errors">
    /// A dictionary mapping field names to arrays of validation error messages.
    /// </param>
    public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    /// <summary>Gets the field-level validation errors.</summary>
    /// <value>
    /// A read-only dictionary where each key is a field name and each value is
    /// a non-empty array of error messages for that field.
    /// </value>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
