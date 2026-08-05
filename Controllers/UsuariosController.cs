using CTFoco.Api.Data;
using CTFoco.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTFoco.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "Admin")] // RF04 - Listagem de funcionários apenas para Admin
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsuariosController(AppDbContext context)
    {
        _context = context;
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
