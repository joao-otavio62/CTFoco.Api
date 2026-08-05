using CTFoco.Api.Data;
using CTFoco.Api.DTOs;
using CTFoco.Api.Enums;
using CTFoco.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CTFoco.Api.Services;

/// <summary>
/// Concentra as regras de negócio de ponto: validação de sequência (RF03),
/// captura de horário no servidor (RNF) e geração de relatórios (RF04).
/// </summary>
public class PontoService : IPontoService
{
    private const double JornadaPadraoHoras = 8.0;

    private readonly AppDbContext _context;

    public PontoService(AppDbContext context)
    {
        _context = context;
    }

    public TipoRegistro? ObterProximoTipo(TipoRegistro? ultimoTipo)
    {
        return ultimoTipo switch
        {
            null => TipoRegistro.Entrada,
            TipoRegistro.Entrada => TipoRegistro.InicioIntervalo,
            TipoRegistro.InicioIntervalo => TipoRegistro.FimIntervalo,
            TipoRegistro.FimIntervalo => TipoRegistro.Saida,
            TipoRegistro.Saida => null, // Ciclo do dia completo
            _ => TipoRegistro.Entrada
        };
    }

    public async Task<StatusDiaDto> ObterStatusDoDiaAsync(int usuarioId)
    {
        var hoje = DateTime.UtcNow.Date;
        var amanha = hoje.AddDays(1);

        var registrosHoje = await _context.RegistrosPonto
            .Where(r => r.UsuarioId == usuarioId && r.DataHoraRegistro >= hoje && r.DataHoraRegistro < amanha)
            .OrderBy(r => r.DataHoraRegistro)
            .ToListAsync();

        var ultimo = registrosHoje.LastOrDefault();
        var proximo = ObterProximoTipo(ultimo?.TipoRegistro);

        return new StatusDiaDto
        {
            RegistrosHoje = registrosHoje.Select(MapearParaDto).ToList(),
            ProximoRegistro = proximo,
            CicloCompleto = proximo == null
        };
    }

    public async Task<RegistroPontoResponseDto> RegistrarPontoAsync(int usuarioId, string? ipAddress)
    {
        var hoje = DateTime.UtcNow.Date;
        var amanha = hoje.AddDays(1);

        var ultimoRegistroHoje = await _context.RegistrosPonto
            .Where(r => r.UsuarioId == usuarioId && r.DataHoraRegistro >= hoje && r.DataHoraRegistro < amanha)
            .OrderByDescending(r => r.DataHoraRegistro)
            .FirstOrDefaultAsync();

        var proximoTipo = ObterProximoTipo(ultimoRegistroHoje?.TipoRegistro);

        if (proximoTipo == null)
        {
            throw new InvalidOperationException("O ciclo de ponto do dia já foi concluído (Entrada, Intervalo e Saída já registrados).");
        }

        var registro = new RegistroPonto
        {
            UsuarioId = usuarioId,
            TipoRegistro = proximoTipo.Value,
            // Horário capturado SEMPRE no servidor - nunca confiar em dado enviado pelo cliente (RNF).
            DataHoraRegistro = DateTime.UtcNow,
            IpAddress = ipAddress
        };

        _context.RegistrosPonto.Add(registro);
        await _context.SaveChangesAsync();

        var dto = MapearParaDto(registro);
        dto.ProximoRegistro = ObterProximoTipo(registro.TipoRegistro);
        return dto;
    }

    public async Task<List<RelatorioDiaDto>> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim, int? usuarioId)
    {
        var inicio = dataInicio.Date;
        var fim = dataFim.Date.AddDays(1);

        var query = _context.RegistrosPonto
            .Include(r => r.Usuario)
            .Where(r => r.DataHoraRegistro >= inicio && r.DataHoraRegistro < fim);

        if (usuarioId.HasValue)
            query = query.Where(r => r.UsuarioId == usuarioId.Value);

        var registros = await query.ToListAsync();

        var agrupado = registros
            .GroupBy(r => new { r.UsuarioId, Dia = r.DataHoraRegistro.Date })
            .Select(g =>
            {
                var entrada = g.FirstOrDefault(r => r.TipoRegistro == TipoRegistro.Entrada)?.DataHoraRegistro;
                var inicioIntervalo = g.FirstOrDefault(r => r.TipoRegistro == TipoRegistro.InicioIntervalo)?.DataHoraRegistro;
                var fimIntervalo = g.FirstOrDefault(r => r.TipoRegistro == TipoRegistro.FimIntervalo)?.DataHoraRegistro;
                var saida = g.FirstOrDefault(r => r.TipoRegistro == TipoRegistro.Saida)?.DataHoraRegistro;

                double horasTrabalhadas = 0;
                var cicloCompleto = entrada.HasValue && saida.HasValue;

                if (cicloCompleto)
                {
                    var totalBruto = (saida!.Value - entrada!.Value).TotalHours;
                    var totalIntervalo = (inicioIntervalo.HasValue && fimIntervalo.HasValue)
                        ? (fimIntervalo.Value - inicioIntervalo.Value).TotalHours
                        : 0;
                    horasTrabalhadas = Math.Round(totalBruto - totalIntervalo, 2);
                }

                var status = cicloCompleto ? "Completo"
                    : (entrada.HasValue ? "Em andamento" : "Incompleto");

                return new RelatorioDiaDto
                {
                    UsuarioId = g.Key.UsuarioId,
                    NomeUsuario = g.First().Usuario?.Nome ?? "-",
                    Data = g.Key.Dia,
                    Entrada = entrada,
                    InicioIntervalo = inicioIntervalo,
                    FimIntervalo = fimIntervalo,
                    Saida = saida,
                    HorasTrabalhadas = horasTrabalhadas,
                    SaldoHoras = Math.Round(horasTrabalhadas - JornadaPadraoHoras, 2),
                    Status = status
                };
            })
            .OrderByDescending(r => r.Data)
            .ThenBy(r => r.NomeUsuario)
            .ToList();

        return agrupado;
    }

    private static RegistroPontoResponseDto MapearParaDto(RegistroPonto r) => new()
    {
        Id = r.Id,
        TipoRegistro = r.TipoRegistro,
        TipoRegistroDescricao = DescricaoTipo(r.TipoRegistro),
        DataHoraRegistro = r.DataHoraRegistro
    };

    private static string DescricaoTipo(TipoRegistro tipo) => tipo switch
    {
        TipoRegistro.Entrada => "Entrada",
        TipoRegistro.InicioIntervalo => "Início do Intervalo",
        TipoRegistro.FimIntervalo => "Fim do Intervalo",
        TipoRegistro.Saida => "Saída",
        _ => tipo.ToString()
    };
}
