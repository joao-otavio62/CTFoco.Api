using CTFoco.Api.Data;
using CTFoco.Api.DTOs;
using CTFoco.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTFoco.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "Admin")] // RF04 - Restringe o controller inteiro apenas para Admin
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsuariosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("criar-funcionario")]
    public async Task<IActionResult> CriarFuncionario([FromBody] CriarFuncionarioDto request)
    {
        // Verifica se o username já existe no banco para evitar duplicidade
        if (await _context.Usuarios.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest(new { mensagem = "Este nome de usuário já está em uso." });
        }

        // Cria o objeto seguindo o padrão exato definido
        var novoFuncionario = new Usuario
        {
            Nome = request.Nome,
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Funcfoco@2024"), // Senha padrão
            Role = "Funcionario",
            Ativo = true
        };

        // Salva no banco de dados
        _context.Usuarios.Add(novoFuncionario);
        await _context.SaveChangesAsync();

        return StatusCode(201, new { mensagem = "Funcionário criado com sucesso!" });
    }

    // Desativa um funcionário do sistema. Acesso exclusivo para Admins.
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletarFuncionario(int id)
    {
        // Busca o usuário no banco de dados pelo ID
        var usuario = await _context.Usuarios.FindAsync(id);

        if (usuario == null)
        {
            return NotFound(new { mensagem = "Funcionário não encontrado." });
        }

        // Impede que contas com role Admin sejam excluídas por esta rota
        if (usuario.Role == "Admin")
        {
            return BadRequest(new { mensagem = "Não é permitido remover contas de Administrador." });
        }

        // Desativa a conta sem perder o histórico do ponto
        usuario.Ativo = false;
        _context.Usuarios.Update(usuario);

        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Funcionário removido com sucesso." });
    }

    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> Listar()
    {
        var usuarios = await _context.Usuarios
            .Where(u => u.Ativo)
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioDto
            {
                Id = u.Id,
                Nome = u.Nome,
                Username = u.Username,
                Role = u.Role
            })
            .ToListAsync();

        return Ok(usuarios);
    }
}