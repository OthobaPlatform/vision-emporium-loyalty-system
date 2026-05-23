namespace VELoyalty.Core.Validation;

/// <summary>
/// Validates purchase threshold configurations: value range (1–100), count (1–10), and uniqueness.
/// </summary>
public static class ThresholdValidator
{
    /// <summary>
    /// Validates a set of purchase threshold values.
    /// The configuration is valid if and only if:
    /// - There are between 1 and 10 thresholds
    /// - Each value is a positive integer between 1 and 100
    /// - No two thresholds have the same value
    /// </summary>
    /// <param name="thresholdValues">The list of threshold purchase count values to validate.</param>
    /// <returns>A ValidationResult indicating success or failure with specific error messages.</returns>
    public static ValidationResult Validate(IReadOnlyList<int> thresholdValues)
    {
        var errors = new List<string>();

        if (thresholdValues == null || thresholdValues.Count == 0)
        {
            errors.Add($"At least {Constants.MinThresholdCount} threshold must be configured.");
            return ValidationResult.Failure(errors);
        }

        // Validate count
        if (thresholdValues.Count < Constants.MinThresholdCount)
            errors.Add($"At least {Constants.MinThresholdCount} threshold must be configured.");

        if (thresholdValues.Count > Constants.MaxThresholdCount)
            errors.Add($"No more than {Constants.MaxThresholdCount} thresholds can be configured. Current count: {thresholdValues.Count}.");

        // Validate each value is within range
        for (int i = 0; i < thresholdValues.Count; i++)
        {
            var value = thresholdValues[i];
            if (value < Constants.MinThresholdValue || value > Constants.MaxThresholdValue)
            {
                errors.Add($"Threshold value at position {i + 1} must be between {Constants.MinThresholdValue} and {Constants.MaxThresholdValue}. Got: {value}.");
            }
        }

        // Validate uniqueness
        var duplicates = thresholdValues
            .GroupBy(v => v)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            errors.Add($"Duplicate threshold value: {duplicate}. Each threshold must be unique.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
