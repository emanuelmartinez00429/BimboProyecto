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

    /// <summary>
    /// Tara extra TOTAL del camión (movimientos.peso_tara_extra): tarimas, forros y
    /// separadores, pesados una sola vez para toda la carga. Se prorratea entre los
    /// bultos declarados al registrar cada pesada.
    /// </summary>
    public double TaraExtraTotal { get; init; }
}

/// <summary>Producto de un camión (`movimiento_productos` + join a producto y su tara).</summary>
public class MovProductoDto
{
    public int    Id              { get; init; }   // id_mov_producto
    public int    IdProducto      { get; init; }
    public string Codigo          { get; init; } = string.Empty;
    public string Nombre          { get; init; } = string.Empty;

    /// <summary>tara.peso_tara_envalaje del producto (empaque de un bulto).</summary>
    public double TaraUnitaria    { get; init; }

    /// <summary>productos.peso_teorico — peso unitario declarado, sin empaque.</summary>
    public double PesoTeorico     { get; init; }

    public double PesoManifestado { get; init; }

    /// <summary>
    /// Cantidad de bultos que declara el manifiesto (columna BD `bultos_teoricos`,
    /// conservada por compatibilidad). Es el dato de papel, sin verificar.
    /// </summary>
    public int    BultosDeclarados { get; init; }

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
