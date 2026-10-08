using FrotaOnline.Application.Auth;
using FrotaOnline.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrotaOnline.Api.Controllers;

[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [AllowAnonymous]
    [HttpPost("register")]
    [EndpointSummary("Create a Customer or Driver account")]
    [EndpointDescription("Role must be Customer or Driver. Password needs 8+ characters with upper case, lower case and a digit. Returns the first token pair.")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        Handle(await _auth.RegisterAsync(request, cancellationToken), response => StatusCode(StatusCodes.Status201Created, response));

    [AllowAnonymous]
    [HttpPost("login")]
    [EndpointSummary("Sign in with e-mail and password")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Handle(await _auth.LoginAsync(request, cancellationToken), Ok);

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EndpointSummary("Exchange a refresh token for a new token pair")]
    [EndpointDescription("The refresh token is rotated on every use. Presenting an already-used token revokes the whole session.")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        Handle(await _auth.RefreshAsync(request, cancellationToken), Ok);

    [Authorize]
    [HttpPost("logout")]
    [EndpointSummary("Revoke a refresh token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken) =>
        HandleEmpty(await _auth.LogoutAsync(request, cancellationToken), NoContent);

    [Authorize]
    [HttpGet("me")]
    [EndpointSummary("Claims of the authenticated user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me() => Ok(new
    {
        Id = User.FindFirst("sub")?.Value,
        Email = User.FindFirst("email")?.Value,
        Role = User.FindFirst("role")?.Value
    });
}
