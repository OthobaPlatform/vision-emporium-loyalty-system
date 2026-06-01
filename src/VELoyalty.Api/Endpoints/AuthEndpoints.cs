using Asp.Versioning;
using VELoyalty.Auth;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/login", async (
            HttpContext httpContext,
            UserRepository userRepository,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<LoginRequestDto>();
            if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.Json(new { error = "Unauthorized", message = "Invalid email or password" }, statusCode: 401);
            }

            var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
            if (user is null || !user.IsActive)
            {
                return Results.Json(new { error = "Unauthorized", message = "Invalid email or password" }, statusCode: 401);
            }

            if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Results.Json(new { error = "Unauthorized", message = "Invalid email or password" }, statusCode: 401);
            }

            var authToken = jwtTokenService.GenerateToken(user.UserId, user.Role, user.OutletId);
            return Results.Ok(new { token = authToken.Token, expiresAt = authToken.ExpiresAt });
        }).MapToApiVersion(1, 0);

        return group;
    }
}

public record LoginRequestDto(string Email, string Password);
