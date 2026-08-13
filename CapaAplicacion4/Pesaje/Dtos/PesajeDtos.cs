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
    /// LEGADO — <c>movimientos.peso_tara_extra</c> del flujo anterior, donde la tara extra se
    /// pesaba una vez por camión y se prorrateaba entre los bultos declarados. Solo lectura:
    /// nunca se vuelve a escribir. Sirve para reconocer camiones cargados con el flujo viejo.
    /// La tara extra vigente se pesa y se guarda en cada entrada.
    /// </summary>
    public double TaraExtraLegado { get; init; }
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

    /// <summary>
    /// <c>numero_bultos_recibido</c>. Null en las entradas nuevas: los bultos ya no se capturan,
    /// se estiman a partir del peso. Solo tiene valor en las pesadas del flujo anterior.
    /// </summary>
    public int?   BultosCapturados { get; init; }

    public string Fecha         { get; init; } = string.Empty;
    public string Hora          { get; init; } = string.Empty;
    public string Observaciones { get; init; } = string.Empty;
}
