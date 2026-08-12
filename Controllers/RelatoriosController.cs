using CTFoco.Api.DTOs;
using CTFoco.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTFoco.Api.Controllers;

[ApiController]
[Route("api/relatorios")]
[Authorize(Roles = "Admin")] // RF04 - Painel apenas para Admin
public class RelatoriosController : ControllerBase
{
    private readonly IPontoService _pontoService;

    public RelatoriosController(IPontoService pontoService)
    {
        _pontoService = pontoService;
    }


    /// RF04 - Listagem de horas trabalhadas por funcionário, com filtro de data/período.
    /// Ex: GET /api/relatorios?dataInicio=2026-08-01&amp;dataFim=2026-08-04&amp;usuarioId=2

    [HttpGet]
    public async Task<ActionResult<List<RelatorioDiaDto>>> Obter(
    [FromQuery] DateTime? dataInicio,
    [FromQuery] DateTime? dataFim,
    [FromQuery] int? usuarioId)
    {
        try
        {
            // Se a data vier nula, assume a data atual
            var inicio = dataInicio ?? DateTime.UtcNow.Date;
            var fim = dataFim ?? DateTime.UtcNow.Date;

            var relatorio = await _pontoService.GerarRelatorioAsync(inicio, fim, usuarioId);

            // Retorna a lista (ou vazia se for null)
            return Ok(relatorio ?? new List<RelatorioDiaDto>());
        }
        catch (Exception ex)
        {
            
            return StatusCode(500, new { mensagemErro = ex.Message, stackTrace = ex.StackTrace });
        }
    }
