using Microsoft.AspNetCore.Mvc;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Shared.DTOs.Api;
using SmartBuilding.Shared.DTOs.Auth;

namespace SmartBuilding.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return result is null
            ? Unauthorized(ApiResponse<LoginResponse>.Fail("Identifiants invalides."))
            : Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ForgotPassword(
        [FromBody] string email, CancellationToken cancellationToken)
    {
        await _authService.RequestPasswordResetAsync(email, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true, "Si l'email existe, un lien a été envoyé."));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var ok = await _authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
        return ok
            ? Ok(ApiResponse<bool>.Ok(true))
            : BadRequest(ApiResponse<bool>.Fail("Jeton invalide ou expiré."));
    }
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
