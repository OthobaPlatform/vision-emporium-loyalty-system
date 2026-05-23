namespace VELoyalty.Core.Validation;

/// <summary>
/// Validates loyalty cycle configuration: date ordering and duration constraints.
/// </summary>
public static class CycleValidator
{
    /// <summary>
    /// Validates a loyalty cycle's start and end dates.
    /// A cycle is valid if and only if the end date is after the start date
    /// and the duration is between 30 and 730 days inclusive.
    /// </summary>
    /// <param name="startDate">The cycle start date.</param>
    /// <param name="endDate">The cycle end date.</param>
    /// <returns>A ValidationResult indicating success or failure with specific error messages.</returns>
    public static ValidationResult Validate(DateOnly startDate, DateOnly endDate)
    {
        var errors = new List<string>();

        // End date must be strictly after start date
        if (endDate <= startDate)
        {
            errors.Add("End date must be after the start date.");
            return ValidationResult.Failure(errors);
        }

        // Calculate duration in days
        var durationDays = endDate.DayNumber - startDate.DayNumber;

        if (durationDays < Constants.MinCycleDurationDays)
            errors.Add($"Cycle duration must be at least {Constants.MinCycleDurationDays} days. Current duration: {durationDays} days.");

        if (durationDays > Constants.MaxCycleDurationDays)
            errors.Add($"Cycle duration must not exceed {Constants.MaxCycleDurationDays} days. Current duration: {durationDays} days.");

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
