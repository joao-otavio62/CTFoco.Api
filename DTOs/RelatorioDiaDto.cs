namespace CTFoco.Api.DTOs;

public class RelatorioDiaDto
{
    public int UsuarioId { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public DateTime Data { get; set; }

    public DateTime? Entrada { get; set; }
    public DateTime? InicioIntervalo { get; set; }
    public DateTime? FimIntervalo { get; set; }
    public DateTime? Saida { get; set; }

    /// <summary>
    /// Total de horas trabalhadas no dia, já descontando o intervalo (RF04).
    /// </summary>
    public double HorasTrabalhadas { get; set; }

    /// <summary>
    /// Diferença em relação à jornada padrão (8h). Positivo = hora extra, negativo = atraso/falta.
    /// </summary>
    public double SaldoHoras { get; set; }

    public string Status { get; set; } = "Em andamento"; // Em andamento | Completo | Incompleto
}
