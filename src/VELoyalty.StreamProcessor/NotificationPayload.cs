namespace VELoyalty.StreamProcessor;

/// <summary>
/// Payload sent to the Notification Lambda when a customer reaches a purchase threshold.
/// Contains all information needed to compose and send the eligibility SMS.
/// </summary>
/// <param name="CustomerId">The eligible customer's identifier.</param>
/// <param name="CustomerName">The customer's display name.</param>
/// <param name="CustomerPhone">The customer's phone number in E.164 format.</param>
/// <param name="VerificationCode">The generated 6-digit verification code.</param>
/// <param name="GiftDescription">Description of the gift associated with the threshold tier.</param>
/// <param name="GiftType">Type of gift: Cash_Return or Gift_Item.</param>
/// <param name="GiftValue">Monetary value of the gift in BDT.</param>
/// <param name="OutletId">The designated outlet for redemption.</param>
/// <param name="Tier">The threshold tier number reached.</param>
public record NotificationPayload(
    string CustomerId,
    string CustomerName,
    string CustomerPhone,
    string VerificationCode,
    string GiftDescription,
    string GiftType,
    decimal GiftValue,
    string OutletId,
    int Tier
);
