using System.Security.Claims;
using CTFoco.Api.DTOs;
using CTFoco.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTFoco.Api.Controllers;

[ApiController]
[Route("api/ponto")]
[Authorize] // Admin e Funcionário podem bater ponto (RF02)
public class PontoController : ControllerBase
{
    private readonly IPontoService _pontoService;

    public PontoController(IPontoService pontoService)
    {
        _pontoService = pontoService;
    }

    /// <summary>
    /// RF03 - Retorna os registros do dia e qual o próximo estado esperado,
    /// para o front-end decidir o rótulo do botão.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<StatusDiaDto>> ObterStatus()
    {
        var usuarioId = ObterUsuarioId();
        var status = await _pontoService.ObterStatusDoDiaAsync(usuarioId);
        return Ok(status);
    }

    /// <summary>
    /// RF02 / RNF - Registra o próximo ponto do funcionário.
    /// O horário é capturado no servidor (DateTime.UtcNow); o front-end NUNCA envia data/hora.
    /// </summary>
    [HttpPost("registrar")]
    public async Task<ActionResult<RegistroPontoResponseDto>> Registrar()
    {
        var usuarioId = ObterUsuarioId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var registro = await _pontoService.RegistrarPontoAsync(usuarioId, ip);
            return Ok(registro);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    private int ObterUsuarioId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.Parse(claim!);
    }
}
