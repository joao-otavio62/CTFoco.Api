using CTFoco.Api.DTOs;
using CTFoco.Api.Enums;

namespace CTFoco.Api.Services;

public interface IPontoService
{
    TipoRegistro? ObterProximoTipo(TipoRegistro? ultimoTipo);
    Task<StatusDiaDto> ObterStatusDoDiaAsync(int usuarioId);
    Task<RegistroPontoResponseDto> RegistrarPontoAsync(int usuarioId, string? ipAddress, double latitude, double longitude);
    Task<List<RelatorioDiaDto>> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim, int? usuarioId);
}