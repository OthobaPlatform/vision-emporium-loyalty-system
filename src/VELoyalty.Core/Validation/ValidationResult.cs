namespace VELoyalty.Core.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
/// <param name="IsValid">Whether the validation passed.</param>
/// <param name="Errors">List of specific error messages if validation failed.</param>
public record ValidationResult(bool IsValid, List<string> Errors)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(true, new List<string>());

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string error) => new(false, new List<string> { error });

    /// <summary>
    /// Creates a failed validation result with multiple errors.
    /// </summary>
    public static ValidationResult Failure(List<string> errors) => new(false, errors);
}
