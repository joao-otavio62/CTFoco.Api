using CTFoco.Api.Data;
using CTFoco.Api.DTOs;
using CTFoco.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace CTFoco.Api.Controllers;

[ApiController]
[Route("auth")]      // Aceita requisições para /auth/login
[Route("api/auth")]  // Aceita requisições para /api/auth/login
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;

    public AuthController(AppDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Permite que o usuário autenticado altere sua própria senha.
    /// </summary>
    [Authorize]
    [HttpPut("alterar-senha")]
    public async Task<IActionResult> AlterarSenha([FromBody] AlterarSenhaRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new { mensagem = "Token inválido ou usuário não autenticado." });
        }

        var usuario = await _context.Usuarios.FindAsync(userId);

        if (usuario == null || !usuario.Ativo)
        {
            return NotFound(new { mensagem = "Usuário não encontrado ou inativo." });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.SenhaAtual, usuario.PasswordHash))
        {
            return BadRequest(new { mensagem = "A senha atual está incorreta." });
        }

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NovaSenha);

        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync();

        return Ok(new { mensagem = "Senha alterada com sucesso." });
    }

    /// <summary>
    /// RF01 - Autenticação via usuário e senha predefinidos (sem cadastro aberto).
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Ativo);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
        {
            return Unauthorized(new { mensagem = "Usuário ou senha inválidos." });
        }

        var (token, expiraEm) = _tokenService.GerarToken(usuario);

        return Ok(new LoginResponseDto
        {
            Token = token,
            ExpiraEm = expiraEm,
            Usuario = new UsuarioDto
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Username = usuario.Username,
                Role = usuario.Role
            }
        });
    }
}