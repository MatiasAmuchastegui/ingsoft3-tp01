using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JoyeriaStock.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, IUsuarioActual usuarioActual) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ct));

    /// <summary>Perfil del usuario del token. El frontend lo usa al recargar la página.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioDto>> Perfil(CancellationToken ct)
        => Ok(await authService.ObtenerPerfilAsync(usuarioActual.Id, ct));
}
