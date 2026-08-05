using System.ComponentModel.DataAnnotations;

namespace CTFoco.Api.Models;

public class Usuario
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Senha armazenada SEMPRE como hash (BCrypt). Nunca em texto plano.
    /// </summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// "Admin" ou "Funcionario" - usado como Claim de Role no JWT (RNF Segurança).
    /// </summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "Funcionario";

    public bool Ativo { get; set; } = true;

    public ICollection<RegistroPonto> RegistrosPonto { get; set; } = new List<RegistroPonto>();
}
