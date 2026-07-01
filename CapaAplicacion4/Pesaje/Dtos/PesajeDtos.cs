namespace CapaAplicacion.Pesaje.Dtos;

/// <summary>Camión (fila de `movimientos`) para la pantalla de Recepción.</summary>
public class CamionDto
{
    public int    Id              { get; init; }
    public int    IdProveedor     { get; init; }
    public string Placa           { get; init; } = string.Empty;
    public string Proveedor       { get; init; } = string.Empty;
    public string FechaAsignacion { get; init; } = string.Empty;
    public string Observaciones   { get; init; } = string.Empty;
    public bool   Cerrado         { get; init; }
}

/// <summary>Producto de un camión (`movimiento_productos` + join a producto y su tara).</summary>
public class MovProductoDto
{
    public int    Id              { get; init; }   // id_mov_producto
    public int    IdProducto      { get; init; }
    public string Codigo          { get; init; } = string.Empty;
    public string Nombre          { get; init; } = string.Empty;
    public double TaraUnitaria    { get; init; }   // tara.peso_tara_envalaje (plano, la usa el trigger)
    public double PesoManifestado { get; init; }
    public int    BultosTeoricos  { get; init; }
    public string Observaciones   { get; init; } = string.Empty;
    public bool   Cerrado         { get; init; }
    public IReadOnlyList<EntradaDto> Entradas { get; init; } = [];
}

/// <summary>Pesaje (`entradas_producto`). Neto/tara los calcula el trigger de BD.</summary>
public class EntradaDto
{
    public int    Id            { get; init; }   // id_pesaje
    public double Bruto         { get; init; }
    public double TaraInd       { get; init; }
    public double TaraExtra     { get; init; }
    public double TaraTotal     { get; init; }
    public double Neto          { get; init; }
    public int    Bultos        { get; init; }
    public string Fecha         { get; init; } = string.Empty;
    public string Hora          { get; init; } = string.Empty;
    public string Observaciones { get; init; } = string.Empty;
}
