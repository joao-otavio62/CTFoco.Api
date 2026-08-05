using CTFoco.Api.Enums;

namespace CTFoco.Api.Models;

/// <summary>
/// Registro imutável de ponto (RF05 - Trilha de Auditoria).
/// Nunca deve ser atualizado ou deletado via API por funcionários.
/// </summary>
public class RegistroPonto
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public TipoRegistro TipoRegistro { get; set; }

    /// <summary>
    /// Capturado SEMPRE no servidor (DateTime.UtcNow), nunca recebido do front-end (RNF).
    /// </summary>
    public DateTime DataHoraRegistro { get; set; }

    /// <summary>
    /// IP de origem da requisição, opcional, para reforço de auditoria.
    /// </summary>
    public string? IpAddress { get; set; }
}
