using CTFoco.Api.DTOs;
using CTFoco.Api.Enums;

namespace CTFoco.Api.Services;

public interface IPontoService
{
    Task<StatusDiaDto> ObterStatusDoDiaAsync(int usuarioId);
    Task<RegistroPontoResponseDto> RegistrarPontoAsync(int usuarioId, string? ipAddress);
    Task<List<RelatorioDiaDto>> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim, int? usuarioId);
    TipoRegistro? ObterProximoTipo(TipoRegistro? ultimoTipo);
}
