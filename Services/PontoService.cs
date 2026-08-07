using CTFoco.Api.Data;
using CTFoco.Api.DTOs;
using CTFoco.Api.Enums;
using CTFoco.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CTFoco.Api.Services;

public class PontoService : IPontoService
{
    private const double JornadaPadraoHoras = 8.0;

    // Coordenadas fixas do local de trabalho  -14.777585578719076, -39.26565705036025
    private const double LatEmpresa = -14.777585578719076;
    private const double LonEmpresa = -39.26565705036025;
    private const double RaioMaximoMetros = 100.0; // Distância máxima permitida em metros

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
            TipoRegistro.Saida => null,
            _ => TipoRegistro.Entrada
        };
    }

    public async Task<StatusDiaDto> ObterStatusDoDiaAsync(int usuarioId)
    {
        var agoraBrasilia = ParaHorarioBrasilia(DateTime.UtcNow);
        var hojeBrasilia = agoraBrasilia.Date;

        var dataCorteUtc = DateTime.UtcNow.AddDays(-2);

        var registrosRecentes = await _context.RegistrosPonto
            .Where(r => r.UsuarioId == usuarioId && r.DataHoraRegistro >= dataCorteUtc)
            .OrderBy(r => r.DataHoraRegistro)
            .ToListAsync();

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

    public async Task<RegistroPontoResponseDto> RegistrarPontoAsync(int usuarioId, string? ipAddress, double latitude, double longitude)
    {
        // 1. Valida a distância do usuário em relação à empresa
        double distanciaMetros = CalcularDistanciaMetros(LatEmpresa, LonEmpresa, latitude, longitude);

        if (distanciaMetros > RaioMaximoMetros)
        {
            throw new InvalidOperationException($"Fora do local de trabalho. Você está a {Math.Round(distanciaMetros)}m da empresa (limite permitido: {RaioMaximoMetros}m).");
        }

        // 2. Valida o fluxo do ciclo do dia
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
        var inicioBuscaUtc = DateTime.SpecifyKind(dataInicio.Date.AddDays(-1), DateTimeKind.Utc);
        var fimBuscaUtc = DateTime.SpecifyKind(dataFim.Date.AddDays(2), DateTimeKind.Utc);

        var query = _context.RegistrosPonto
            .Include(r => r.Usuario)
            .Where(r => r.DataHoraRegistro >= inicioBuscaUtc && r.DataHoraRegistro < fimBuscaUtc);

        if (usuarioId.HasValue && usuarioId.Value > 0)
        {
            query = query.Where(r => r.UsuarioId == usuarioId.Value);
        }

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

    private static double CalcularDistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371e3; // Raio da Terra em metros
        var phi1 = lat1 * Math.PI / 180;
        var phi2 = lat2 * Math.PI / 180;
        var deltaPhi = (lat2 - lat1) * Math.PI / 180;
        var deltaLambda = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                Math.Cos(phi1) * Math.Cos(phi2) *
                Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
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