namespace VELoyalty.Api.Services;

/// <summary>
/// Request model for creating a new outlet.
/// </summary>
/// <param name="Name">Outlet display name.</param>
/// <param name="Address">Physical address of the outlet.</param>
/// <param name="PhoneNumber">Contact phone number for the outlet.</param>
/// <param name="AssignedManagerId">User ID of the assigned outlet manager.</param>
public record CreateOutletRequest(
    string Name,
    string Address,
    string PhoneNumber,
    string AssignedManagerId
);

/// <summary>
/// Request model for updating an existing outlet.
/// </summary>
/// <param name="Name">Outlet display name.</param>
/// <param name="Address">Physical address of the outlet.</param>
/// <param name="PhoneNumber">Contact phone number for the outlet.</param>
/// <param name="AssignedManagerId">User ID of the assigned outlet manager.</param>
public record UpdateOutletRequest(
    string Name,
    string Address,
    string PhoneNumber,
    string AssignedManagerId
);

/// <summary>
/// Request model for changing outlet active/inactive status.
/// </summary>
/// <param name="IsActive">The desired active status.</param>
public record UpdateOutletStatusRequest(
    bool IsActive
);

/// <summary>
/// Response model for outlet data returned by the API.
/// </summary>
/// <param name="OutletId">Unique outlet identifier.</param>
/// <param name="Name">Outlet display name.</param>
/// <param name="Address">Physical address of the outlet.</param>
/// <param name="PhoneNumber">Contact phone number for the outlet.</param>
/// <param name="AssignedManagerId">User ID of the assigned outlet manager.</param>
/// <param name="IsActive">Whether the outlet is currently active.</param>
public record OutletResponse(
    string OutletId,
    string Name,
    string Address,
    string PhoneNumber,
    string AssignedManagerId,
    bool IsActive
);
