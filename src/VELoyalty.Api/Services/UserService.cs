using VELoyalty.Auth;
using VELoyalty.Core;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Services;

/// <summary>
/// Service for managing user accounts (Admin-only operations).
/// Handles user creation with password hashing, updates, and listing.
/// </summary>
public class UserService
{
    private readonly UserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(UserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Lists all users, excluding password hashes from the response.
    /// </summary>
    public async Task<List<UserResponse>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.ListAllAsync(cancellationToken);
        return users.Select(MapToResponse).ToList();
    }

    /// <summary>
    /// Creates a new user with bcrypt-hashed password and role assignment.
    /// </summary>
    public async Task<CreateUserResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // Validate request
        var validationErrors = ValidateCreateRequest(request);
        if (validationErrors.Count > 0)
            return new CreateUserResult(null, validationErrors);

        // Check for duplicate email
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
            return new CreateUserResult(null, new List<string> { "A user with this email already exists." });

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid().ToString("N");
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User(
            UserId: userId,
            Email: request.Email.Trim().ToLowerInvariant(),
            Name: request.Name.Trim(),
            PasswordHash: passwordHash,
            Role: request.Role,
            OutletId: request.Role == nameof(UserRole.Outlet_Manager) ? request.OutletId : null,
            IsActive: true,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _userRepository.CreateAsync(user, cancellationToken);
        return new CreateUserResult(MapToResponse(user), null);
    }

    /// <summary>
    /// Updates an existing user's details, role, and optionally resets password.
    /// </summary>
    public async Task<UpdateUserResult> UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (existingUser is null)
            return new UpdateUserResult(null, null, IsNotFound: true);

        // Validate request
        var validationErrors = ValidateUpdateRequest(request);
        if (validationErrors.Count > 0)
            return new UpdateUserResult(null, validationErrors, IsNotFound: false);

        var now = DateTime.UtcNow;
        var passwordHash = existingUser.PasswordHash;

        // Only hash and update password if provided
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            passwordHash = _passwordHasher.HashPassword(request.Password);
        }

        var updatedUser = new User(
            UserId: existingUser.UserId,
            Email: existingUser.Email, // Email is not updatable
            Name: !string.IsNullOrWhiteSpace(request.Name) ? request.Name.Trim() : existingUser.Name,
            PasswordHash: passwordHash,
            Role: !string.IsNullOrWhiteSpace(request.Role) ? request.Role : existingUser.Role,
            OutletId: DetermineOutletId(request, existingUser),
            IsActive: existingUser.IsActive,
            CreatedAt: existingUser.CreatedAt,
            UpdatedAt: now
        );

        await _userRepository.UpdateAsync(updatedUser, cancellationToken);
        return new UpdateUserResult(MapToResponse(updatedUser), null, IsNotFound: false);
    }

    private static string? DetermineOutletId(UpdateUserRequest request, User existingUser)
    {
        var effectiveRole = !string.IsNullOrWhiteSpace(request.Role) ? request.Role : existingUser.Role;

        if (effectiveRole == nameof(UserRole.Outlet_Manager))
        {
            // Use provided outletId, or keep existing if not provided
            return !string.IsNullOrWhiteSpace(request.OutletId) ? request.OutletId : existingUser.OutletId;
        }

        // Admin role doesn't have an outletId
        return null;
    }

    private static List<string> ValidateCreateRequest(CreateUserRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");
        else if (!IsValidEmail(request.Email))
            errors.Add("Email format is invalid.");

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Name is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");
        else if (request.Password.Length < 8)
            errors.Add("Password must be at least 8 characters.");

        if (string.IsNullOrWhiteSpace(request.Role))
            errors.Add("Role is required.");
        else if (!IsValidRole(request.Role))
            errors.Add("Role must be 'Admin' or 'Outlet_Manager'.");

        if (request.Role == nameof(UserRole.Outlet_Manager) && string.IsNullOrWhiteSpace(request.OutletId))
            errors.Add("OutletId is required for Outlet_Manager role.");

        return errors;
    }

    private static List<string> ValidateUpdateRequest(UpdateUserRequest request)
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Role) && !IsValidRole(request.Role))
            errors.Add("Role must be 'Admin' or 'Outlet_Manager'.");

        if (request.Role == nameof(UserRole.Outlet_Manager) && string.IsNullOrWhiteSpace(request.OutletId))
            errors.Add("OutletId is required for Outlet_Manager role.");

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 8)
            errors.Add("Password must be at least 8 characters.");

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        // Basic email format validation
        var trimmed = email.Trim();
        if (trimmed.Length == 0) return false;

        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex >= trimmed.Length - 1) return false;

        var domain = trimmed[(atIndex + 1)..];
        if (domain.IndexOf('.') <= 0) return false;
        if (domain.EndsWith('.')) return false;

        return true;
    }

    private static bool IsValidRole(string role)
    {
        return role == nameof(UserRole.Admin) || role == nameof(UserRole.Outlet_Manager);
    }

    private static UserResponse MapToResponse(User user) =>
        new(
            UserId: user.UserId,
            Email: user.Email,
            Name: user.Name,
            Role: user.Role,
            OutletId: user.OutletId,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );
}

// ─── Request/Response DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Response DTO for user data (excludes password hash).
/// </summary>
public record UserResponse(
    string UserId,
    string Email,
    string Name,
    string Role,
    string? OutletId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Request DTO for creating a new user.
/// </summary>
public record CreateUserRequest(
    string Email,
    string Name,
    string Password,
    string Role,
    string? OutletId
);

/// <summary>
/// Request DTO for updating an existing user.
/// </summary>
public record UpdateUserRequest(
    string? Name,
    string? Role,
    string? OutletId,
    string? Password
);

/// <summary>
/// Result of a create user operation.
/// </summary>
public record CreateUserResult(
    UserResponse? User,
    List<string>? ValidationErrors
);

/// <summary>
/// Result of an update user operation.
/// </summary>
public record UpdateUserResult(
    UserResponse? User,
    List<string>? ValidationErrors,
    bool IsNotFound
);
