using CTFoco.Api.Enums;

namespace CTFoco.Api.DTOs;

public class RegistroPontoResponseDto
{
    public int Id { get; set; }
    public TipoRegistro TipoRegistro { get; set; }
    public string TipoRegistroDescricao { get; set; } = string.Empty;
    public DateTime DataHoraRegistro { get; set; }

    /// <summary>
    /// Próximo tipo de registro esperado para o funcionário (ou null se o ciclo do dia já foi concluído).
    /// Usado pelo front-end para saber qual rótulo exibir no botão (RF03).
    /// </summary>
    public TipoRegistro? ProximoRegistro { get; set; }
}

public class StatusDiaDto
{
    public List<RegistroPontoResponseDto> RegistrosHoje { get; set; } = new();
    public TipoRegistro? ProximoRegistro { get; set; }
    public bool CicloCompleto { get; set; }
}
