namespace CapaDominio.Entities;

public class Cliente
{
    public int    Id          { get; set; }
    public string Codigo      { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string RTN         { get; set; } = string.Empty;
    public string Telefono    { get; set; } = string.Empty;
    public int    IdEstado    { get; set; }
}
