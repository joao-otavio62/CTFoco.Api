using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CTFoco.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace CTFoco.Api.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string token, DateTime expiraEm) GerarToken(Usuario usuario)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var chave = jwtSettings["Key"]!;
        var emissor = jwtSettings["Issuer"]!;
        var audiencia = jwtSettings["Audience"]!;
        var expiraMinutos = int.Parse(jwtSettings["ExpiresInMinutes"] ?? "480");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Username),
            new("nome", usuario.Nome),
            // Claim de Role, usada para bloquear rotas do dashboard (RNF Segurança)
            new(ClaimTypes.Role, usuario.Role)
        };

        var expiraEm = DateTime.UtcNow.AddMinutes(expiraMinutos);

        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: emissor,
            audience: audiencia,
            claims: claims,
            expires: expiraEm,
            signingCredentials: credenciais);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }
}
