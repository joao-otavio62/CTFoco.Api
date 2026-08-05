using CTFoco.Api.Models;

namespace CTFoco.Api.Services;

public interface ITokenService
{
    (string token, DateTime expiraEm) GerarToken(Usuario usuario);
}
