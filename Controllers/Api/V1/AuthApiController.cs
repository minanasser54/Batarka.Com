using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp10.Data;
using Microsoft.Extensions.Options;
using MyMvcApp10.Models;
using System.Security.Claims;

namespace MyMvcApp10.Controllers.Api.V1;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IOptionsMonitor<BearerTokenOptions> _bearerOptions;

    public AuthApiController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IOptionsMonitor<BearerTokenOptions> bearerOptions)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _bearerOptions = bearerOptions;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] ApiLoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);
        if (user == null || !user.IsActive)
            return Unauthorized(new ProblemDetails { Title = "Invalid username or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new ProblemDetails { Title = "Invalid username or password." });

        var mustChangePassword = user.MustChangePassword || (!user.IsAdmin && user.LastLoginAt == null);
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = user.Id,
            Action = "ApiLoginSucceeded",
            TargetType = "User",
            TargetId = user.Id,
            Details = "Successful API login.",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        Response.Headers["X-Must-Change-Password"] = mustChangePassword ? "true" : "false";
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        await HttpContext.SignInAsync(IdentityConstants.BearerScheme, principal, new AuthenticationProperties());
        return new EmptyResult();
    }

    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = "Identity.Bearer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ApiChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["password"] = result.Errors.Select(error => error.Description).ToArray()
            }));

        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        return NoContent();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] ApiRefreshTokenRequest request)
    {
        var options = _bearerOptions.Get(IdentityConstants.BearerScheme);
        var ticket = options.RefreshTokenProtector.Unprotect(request.RefreshToken);
        if (ticket?.AuthenticationScheme != $"{IdentityConstants.BearerScheme}:RefreshToken"
            || ticket.Properties?.ExpiresUtc is not { } expiresAt
            || DateTimeOffset.UtcNow >= expiresAt)
            return Unauthorized();

        var userId = ticket.Principal.FindFirstValue(_userManager.Options.ClaimsIdentity.UserIdClaimType);
        if (userId == null) return Unauthorized();
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive) return Unauthorized();

        var securityStampType = _userManager.Options.ClaimsIdentity.SecurityStampClaimType;
        var tokenStamp = ticket.Principal.FindFirstValue(securityStampType);
        var currentStamp = await _userManager.GetSecurityStampAsync(user);
        if (tokenStamp == null || tokenStamp != currentStamp) return Unauthorized();

        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        await HttpContext.SignInAsync(IdentityConstants.BearerScheme, principal, new AuthenticationProperties());
        return new EmptyResult();
    }
}