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

    // Garante compatibilidade de timezone tanto em Windows quanto em Linux/Docker
    private static readonly TimeZoneInfo FusoBrasilia = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo"
    );

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
        var agoraBrasilia = ParaHorarioBrasilia(DateTime.UtcNow);
        var hojeBrasilia = agoraBrasilia.Date;

        // Traz registros das últimas 48h para evitar gargalos de fuso na query SQL
        var dataCorteUtc = DateTime.UtcNow.AddDays(-2);

        var registrosRecentes = await _context.RegistrosPonto
            .Where(r => r.UsuarioId == usuarioId && r.DataHoraRegistro >= dataCorteUtc)
            .OrderBy(r => r.DataHoraRegistro)
            .ToListAsync();

        // Filtra em memória garantindo a data exata no fuso de Brasília
        var registrosHoje = registrosRecentes
            .Where(r => ParaHorarioBrasilia(r.DataHoraRegistro).Date == hojeBrasilia)
            .ToList();

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
        var statusAtual = await ObterStatusDoDiaAsync(usuarioId);

        if (statusAtual.CicloCompleto || statusAtual.ProximoRegistro == null)
        {
            throw new InvalidOperationException("O ciclo de ponto do dia já foi concluído (Entrada, Intervalo e Saída já registrados).");
        }

        var registro = new RegistroPonto
        {
            UsuarioId = usuarioId,
            TipoRegistro = statusAtual.ProximoRegistro.Value,
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
        var inicioBuscaUtc = dataInicio.Date.AddDays(-1);
        var fimBuscaUtc = dataFim.Date.AddDays(2);

        var query = _context.RegistrosPonto
            .Include(r => r.Usuario)
            .Where(r => r.DataHoraRegistro >= inicioBuscaUtc && r.DataHoraRegistro < fimBuscaUtc);

        if (usuarioId.HasValue)
            query = query.Where(r => r.UsuarioId == usuarioId.Value);

        var registros = await query.ToListAsync();

        var agrupado = registros
            .Select(r => new { Registro = r, DataBrasilia = ParaHorarioBrasilia(r.DataHoraRegistro) })
            .Where(x => x.DataBrasilia.Date >= dataInicio.Date && x.DataBrasilia.Date <= dataFim.Date)
            .GroupBy(x => new { x.Registro.UsuarioId, Dia = x.DataBrasilia.Date })
            .Select(g =>
            {
                var registrosDoDia = g.Select(x => x.Registro).OrderBy(r => r.DataHoraRegistro).ToList();

                var entrada = registrosDoDia.FirstOrDefault(r => r.TipoRegistro == TipoRegistro.Entrada)?.DataHoraRegistro;
                var inicioIntervalo = registrosDoDia.FirstOrDefault(r => r.TipoRegistro == TipoRegistro.InicioIntervalo)?.DataHoraRegistro;
                var fimIntervalo = registrosDoDia.FirstOrDefault(r => r.TipoRegistro == TipoRegistro.FimIntervalo)?.DataHoraRegistro;
                var saida = registrosDoDia.FirstOrDefault(r => r.TipoRegistro == TipoRegistro.Saida)?.DataHoraRegistro;

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
                    NomeUsuario = g.First().Registro.Usuario?.Nome ?? "-",
                    Data = g.Key.Dia,
                    Entrada = entrada.HasValue ? ParaHorarioBrasilia(entrada.Value) : null,
                    InicioIntervalo = inicioIntervalo.HasValue ? ParaHorarioBrasilia(inicioIntervalo.Value) : null,
                    FimIntervalo = fimIntervalo.HasValue ? ParaHorarioBrasilia(fimIntervalo.Value) : null,
                    Saida = saida.HasValue ? ParaHorarioBrasilia(saida.Value) : null,
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
        DataHoraRegistro = ParaHorarioBrasilia(r.DataHoraRegistro)
    };

    private static DateTime ParaHorarioBrasilia(DateTime utcDate)
    {
        var dataUtc = utcDate.Kind == DateTimeKind.Utc
            ? utcDate
            : DateTime.SpecifyKind(utcDate, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(dataUtc, FusoBrasilia);
    }

    private static string DescricaoTipo(TipoRegistro tipo) => tipo switch
    {
        TipoRegistro.Entrada => "Entrada",
        TipoRegistro.InicioIntervalo => "Início do Intervalo",
        TipoRegistro.FimIntervalo => "Fim do Intervalo",
        TipoRegistro.Saida => "Saída",
        _ => tipo.ToString()
    };
}