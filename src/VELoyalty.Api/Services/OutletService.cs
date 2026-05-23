using VELoyalty.Core;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Services;

/// <summary>
/// Service handling outlet management operations including CRUD and status changes
/// with last-active-outlet protection.
/// </summary>
public class OutletService
{
    private readonly OutletRepository _outletRepository;

    public OutletService(OutletRepository outletRepository)
    {
        _outletRepository = outletRepository;
    }

    /// <summary>
    /// Lists all outlets with their current status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all outlets.</returns>
    public async Task<List<OutletResponse>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var outlets = await _outletRepository.ListAllAsync(cancellationToken);
        return outlets.Select(MapToResponse).ToList();
    }

    /// <summary>
    /// Creates a new outlet with a generated identifier and active status.
    /// </summary>
    /// <param name="request">The create outlet request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created outlet response.</returns>
    public async Task<OutletResponse> CreateAsync(CreateOutletRequest request, CancellationToken cancellationToken = default)
    {
        var outletId = GenerateOutletId();

        var outlet = new Outlet(
            OutletId: outletId,
            Name: request.Name,
            Address: request.Address,
            PhoneNumber: request.PhoneNumber,
            AssignedManagerId: request.AssignedManagerId,
            IsActive: true
        );

        await _outletRepository.CreateAsync(outlet, cancellationToken);

        return MapToResponse(outlet);
    }

    /// <summary>
    /// Updates an existing outlet's details.
    /// </summary>
    /// <param name="outletId">The outlet identifier.</param>
    /// <param name="request">The update outlet request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated outlet response, or null if not found.</returns>
    public async Task<OutletResponse?> UpdateAsync(string outletId, UpdateOutletRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _outletRepository.GetByIdAsync(outletId, cancellationToken);
        if (existing is null)
            return null;

        var updated = new Outlet(
            OutletId: outletId,
            Name: request.Name,
            Address: request.Address,
            PhoneNumber: request.PhoneNumber,
            AssignedManagerId: request.AssignedManagerId,
            IsActive: existing.IsActive
        );

        await _outletRepository.UpdateAsync(updated, cancellationToken);

        return MapToResponse(updated);
    }

    /// <summary>
    /// Updates the active/inactive status of an outlet with last-active-outlet protection.
    /// </summary>
    /// <param name="outletId">The outlet identifier.</param>
    /// <param name="isActive">The desired active status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or the specific failure reason.</returns>
    public async Task<OutletStatusUpdateResult> UpdateStatusAsync(string outletId, bool isActive, CancellationToken cancellationToken = default)
    {
        var existing = await _outletRepository.GetByIdAsync(outletId, cancellationToken);
        if (existing is null)
            return OutletStatusUpdateResult.NotFound();

        // If deactivating, check last-active-outlet protection
        if (!isActive && existing.IsActive)
        {
            var activeCount = await _outletRepository.CountActiveAsync(cancellationToken);
            if (activeCount <= 1)
            {
                return OutletStatusUpdateResult.LastActiveOutlet();
            }
        }

        await _outletRepository.UpdateStatusAsync(outletId, isActive, cancellationToken);

        var updated = existing with { IsActive = isActive };
        return OutletStatusUpdateResult.Success(MapToResponse(updated));
    }

    /// <summary>
    /// Generates a unique outlet identifier.
    /// </summary>
    private static string GenerateOutletId()
    {
        return $"OTL-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    }

    private static OutletResponse MapToResponse(Outlet outlet) =>
        new(
            OutletId: outlet.OutletId,
            Name: outlet.Name,
            Address: outlet.Address,
            PhoneNumber: outlet.PhoneNumber,
            AssignedManagerId: outlet.AssignedManagerId,
            IsActive: outlet.IsActive
        );
}

/// <summary>
/// Result of an outlet status update operation.
/// </summary>
public class OutletStatusUpdateResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorType { get; private init; }
    public string? Message { get; private init; }
    public OutletResponse? Outlet { get; private init; }

    public static OutletStatusUpdateResult Success(OutletResponse outlet) =>
        new()
        {
            IsSuccess = true,
            Outlet = outlet,
            Message = "Outlet status updated successfully."
        };

    public static OutletStatusUpdateResult NotFound() =>
        new()
        {
            IsSuccess = false,
            ErrorType = "NotFound",
            Message = "Outlet not found."
        };

    public static OutletStatusUpdateResult LastActiveOutlet() =>
        new()
        {
            IsSuccess = false,
            ErrorType = "ValidationError",
            Message = "At least one outlet must remain active."
        };
}
