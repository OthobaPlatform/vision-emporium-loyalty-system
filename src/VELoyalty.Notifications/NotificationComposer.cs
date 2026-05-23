using VELoyalty.Core;

namespace VELoyalty.Notifications;

/// <summary>
/// Composes SMS notification messages for the loyalty system.
/// </summary>
public static class NotificationComposer
{
    /// <summary>
    /// Composes an eligibility SMS message when a customer reaches a purchase threshold.
    /// </summary>
    /// <param name="customerName">The customer's name.</param>
    /// <param name="giftDescription">Description of the gift earned.</param>
    /// <param name="outletName">Name of the designated redemption outlet.</param>
    /// <param name="verificationCode">The 6-digit verification code.</param>
    /// <returns>The composed SMS message text.</returns>
    public static string ComposeEligibilitySms(
        string customerName,
        string giftDescription,
        string outletName,
        string verificationCode)
    {
        return $"Dear {customerName}, congratulations! You've earned a {giftDescription} at {outletName}. " +
               $"Your verification code is {verificationCode}. Please visit the outlet to claim your gift.";
    }

    /// <summary>
    /// Composes a reminder SMS message for verification codes approaching expiration.
    /// </summary>
    /// <param name="customerName">The customer's name.</param>
    /// <param name="verificationCode">The 6-digit verification code.</param>
    /// <param name="giftDescription">Description of the gift.</param>
    /// <param name="outletName">Name of the designated redemption outlet.</param>
    /// <param name="expiryDate">The date the code expires.</param>
    /// <returns>The composed reminder SMS message text.</returns>
    public static string ComposeReminderSms(
        string customerName,
        string verificationCode,
        string giftDescription,
        string outletName,
        DateOnly expiryDate)
    {
        var formattedDate = expiryDate.ToString("dd MMM yyyy");
        return $"Dear {customerName}, your verification code {verificationCode} for {giftDescription} " +
               $"at {outletName} expires on {formattedDate}. Please visit the outlet to claim your gift.";
    }
}
