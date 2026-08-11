using System.ComponentModel.DataAnnotations;
namespace CTFoco.Api.Models;
using CTFoco.Api.Enums;

public class Usuario
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Username { get; set; } = string.Empty;

    /// Senha armazenada SEMPRE como hash (BCrypt). Nunca em texto plano.
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// "Admin" ou "Funcionario" - usado como Claim de Role no JWT (RNF Segurança).
        [Required, MaxLength(20)]
    public string Role { get; set; } = "Recepcionista";

    [Required]
    public Cargo Cargo { get; set; } = Cargo.Recepcionista;

    public bool Ativo { get; set; } = true;

    public ICollection<RegistroPonto> RegistrosPonto { get; set; } = new List<RegistroPonto>();
}
