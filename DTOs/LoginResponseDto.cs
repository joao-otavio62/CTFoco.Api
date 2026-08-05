namespace CTFoco.Api.DTOs;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
    public UsuarioDto Usuario { get; set; } = new();
}
