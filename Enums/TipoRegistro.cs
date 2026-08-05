namespace CTFoco.Api.Enums;

/// <summary>
/// Representa os 4 estados diários de um registro de ponto (RF02 / RF03).
/// A sequência lógica é sempre: Entrada -> InicioIntervalo -> FimIntervalo -> Saida.
/// </summary>
public enum TipoRegistro
{
    Entrada = 1,
    InicioIntervalo = 2,
    FimIntervalo = 3,
    Saida = 4
}
