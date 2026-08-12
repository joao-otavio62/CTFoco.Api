using CTFoco.Api.Data;
using CTFoco.Api.DTOs;
using CTFoco.Api.Enums;
using CTFoco.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CTFoco.Api.Services;

public class PontoService : IPontoService
{
    private static readonly TimeZoneInfo FusoBrasilia = ObterFusoHorarioBrasilia();

    private static TimeZoneInfo ObterFusoHorarioBrasilia()
    {
        string[] idsTentar = OperatingSystem.IsWindows()
            ? new[] { "E. South America Standard Time", "Brazil Eastern Standard Time" }
            : new[] { "America/Sao_Paulo", "America/Bahia", "Brazil/East" };

        foreach (var id in idsTentar)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                // Continua para o próximo ID
            }
        }

        // Fallback para GMT-3 caso o contêiner Linux não possua a base tzdata instalada
        return TimeZoneInfo.CreateCustomTimeZone("BrasiliaFallback", TimeSpan.FromHours(-3), "Horário de Brasília", "Horário de Brasília");
    }

    private readonly AppDbContext _context;

    public PontoService(AppDbContext context)
    {
        _context = context;
    }

    private static double ObterJornadaPadraoHoras(Cargo cargo) => cargo switch
    {
        Cargo.EstagiarioManha => 6.0,
        Cargo.EstagiarioTarde => 6.0,
        Cargo.EstagiarioNoite => 6.0,
        Cargo.Recepcionista => 8.0,
        _ => 8.0
    };

    private static void ValidarHorarioTurno(Cargo cargo, DateTime utcNow)
    {
        var dataHoraBrasilia = ParaHorarioBrasilia(utcNow);
        var horaAtual = dataHoraBrasilia.TimeOfDay;

        bool dentroDoHorario = cargo switch
        {
            Cargo.EstagiarioManha => horaAtual >= new TimeSpan(6, 0, 0) && horaAtual <= new TimeSpan(11, 0, 0),
            Cargo.EstagiarioTarde => horaAtual >= new TimeSpan(14, 0, 0) && horaAtual <= new TimeSpan(17, 30, 0),
            Cargo.EstagiarioNoite => horaAtual >= new TimeSpan(17, 30, 0) && horaAtual <= new TimeSpan(21, 0, 0),
            Cargo.Recepcionista => horaAtual >= new TimeSpan(6, 0, 0) && horaAtual <= new TimeSpan(18, 0, 0),
            _ => true
        };

        if (!dentroDoHorario)
        {
            var descricaoTurno = cargo switch
            {
                Cargo.EstagiarioManha => "Estagiário Manhã (06:00 às 11:00)",
                Cargo.EstagiarioTarde => "Estagiário Tarde (14:00 às 17:30)",
                Cargo.EstagiarioNoite => "Estagiário Noite (17:30 às 21:00)",
                Cargo.Recepcionista => "Recepcionista (06:00 às 18:00)",
                _ => "Horário permitido"
            };

            throw new InvalidOperationException($"Registro não permitido fora do seu turno de trabalho. Seu turno é: {descricaoTurno}.");
        }
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
        var (inicioUtc, fimUtc) = ObterLimitesDoDiaUtc(DateTime.UtcNow);

        var registrosHoje = await _context.RegistrosPonto
            .Where(r => r.UsuarioId == usuarioId && r.DataHoraRegistro >= inicioUtc && r.DataHoraRegistro < fimUtc)
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

    public async Task<RegistroPontoResponseDto> RegistrarPontoAsync(int usuarioId, string? ipAddress, double latitude, double longitude)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        ValidarHorarioTurno(usuario.Cargo, DateTime.UtcNow);

        var (inicioUtc, fimUtc) = ObterLimitesDoDiaUtc(DateTime.UtcNow);

        var ultimoRegistroHoje = await _context.RegistrosPonto
            .Where(r => r.UsuarioId == usuarioId && r.DataHoraRegistro >= inicioUtc && r.DataHoraRegistro < fimUtc)
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
        // Garante que o Kind seja Utc para evitar exceções do Npgsql/PostgreSQL
        var inicio = DateTime.SpecifyKind(dataInicio.Date, DateTimeKind.Utc);
        var fim = DateTime.SpecifyKind(dataFim.Date.AddDays(1), DateTimeKind.Utc);

        var query = _context.RegistrosPonto
            .Include(r => r.Usuario)
            .Where(r => r.DataHoraRegistro >= inicio && r.DataHoraRegistro < fim);

        if (usuarioId.HasValue)
            query = query.Where(r => r.UsuarioId == usuarioId.Value);

        var registros = await query.ToListAsync();

        var agrupado = registros
            .GroupBy(r => new { r.UsuarioId, Dia = ParaHorarioBrasilia(r.DataHoraRegistro).Date })
            .Select(g =>
            {
                var usuario = g.First().Usuario;
                var cargoUsuario = usuario?.Cargo ?? Cargo.Recepcionista;
                var jornadaEsperada = ObterJornadaPadraoHoras(cargoUsuario);

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
                    NomeUsuario = usuario?.Nome ?? "-",
                    Data = g.Key.Dia,
                    Entrada = entrada.HasValue ? ParaHorarioBrasilia(entrada.Value) : null,
                    InicioIntervalo = inicioIntervalo.HasValue ? ParaHorarioBrasilia(inicioIntervalo.Value) : null,
                    FimIntervalo = fimIntervalo.HasValue ? ParaHorarioBrasilia(fimIntervalo.Value) : null,
                    Saida = saida.HasValue ? ParaHorarioBrasilia(saida.Value) : null,
                    HorasTrabalhadas = horasTrabalhadas,
                    SaldoHoras = Math.Round(horasTrabalhadas - jornadaEsperada, 2),
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
        var dataUtc = DateTime.SpecifyKind(utcDate, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(dataUtc, FusoBrasilia);
    }

    private static (DateTime InicioUtc, DateTime FimUtc) ObterLimitesDoDiaUtc(DateTime utcNow)
    {
        var agoraBrasilia = ParaHorarioBrasilia(utcNow);
        var inicioDiaBrasilia = agoraBrasilia.Date;
        var fimDiaBrasilia = inicioDiaBrasilia.AddDays(1);

        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioDiaBrasilia, FusoBrasilia);
        var fimUtc = TimeZoneInfo.ConvertTimeToUtc(fimDiaBrasilia, FusoBrasilia);

        return (inicioUtc, fimUtc);
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