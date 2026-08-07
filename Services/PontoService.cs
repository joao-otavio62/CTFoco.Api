using CTFoco.Api.DTOs;
using CTFoco.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CTFoco.Api.Controllers;

[ApiController]
[Authorize]
[Route("ponto")]      // Permite requisições para /ponto/status
[Route("api/ponto")]  // Permite requisições para /api/ponto/status
public class PontoController : ControllerBase
{
    private readonly IPontoService _pontoService;

    public PontoController(IPontoService pontoService)
    {
        _pontoService = pontoService;
    }

    /// <summary>
    /// Retorna o status das batidas do dia atual para o usuário logado (RF03).
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<StatusDiaDto>> ObterStatus()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int usuarioId))
        {
            return Unauthorized(new { mensagem = "Usuário não autenticado." });
        }

        var status = await _pontoService.ObterStatusDoDiaAsync(usuarioId);
        return Ok(status);
    }

    /// <summary>
    /// Dispara o registro do próximo ponto (Entrada, Início Intervalo, Fim Intervalo, Saída).
    /// </summary>
    [HttpPost("registrar")]
    public async Task<ActionResult<RegistroPontoResponseDto>> Registrar()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int usuarioId))
        {
            return Unauthorized(new { mensagem = "Usuário não autenticado." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var resultado = await _pontoService.RegistrarPontoAsync(usuarioId, ipAddress);
        return Ok(resultado);
    }
}