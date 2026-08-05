using CTFoco.Api.Models;

namespace CTFoco.Api.Data;

/// <summary>
/// Popula o banco com usuários predefinidos, já que não há tela de cadastro aberta (RF01).
/// Credenciais de demonstração:
///   Admin       -> username: admin       | senha: Adminfoco@2024
///   Funcionário -> username: Lara  | senha: Funcfoco@2024
/// </summary>
public static class SeedData
{
    public static void Inicializar(AppDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.Usuarios.Any())
            return;

        var admin = new Usuario
        {
            Nome = "Administrador CT FOCO",
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Adminfoco@2024"),
            Role = "Admin",
            Ativo = true
        };

        var funcionario1 = new Usuario
        {
            Nome = "Lara",
            Username = "lara",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Funcfoco@2024"),
            Role = "Funcionario",
            Ativo = true
        };

        var funcionario2 = new Usuario
        {
            Nome = "Maria Souza",
            Username = "maria.souza",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("############"),
            Role = "Funcionario",
            Ativo = false
        };

        context.Usuarios.AddRange(admin, funcionario1, funcionario2);
        context.SaveChanges();
    }
}
