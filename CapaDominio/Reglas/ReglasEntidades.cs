namespace CapaDominio.Reglas;

/// <summary>
/// Qué exige el negocio de cada campo, por entidad.
/// </summary>
/// <remarks>
/// <para>
/// Antes esto vivía en cada modal, dentro de la declaración del validador. Que
/// el nombre de un producto sea obligatorio y tope en 200 caracteres no es una
/// decisión de la pantalla: sale del negocio y del esquema de la base. Con la
/// regla acá, la UI deja de decidirla y pasa a aplicarla — y si mañana hace
/// falta chequear lo mismo antes de un insert, la fuente ya es única.
/// </para>
/// <para>
/// Los largos máximos reflejan las columnas de Supabase (PostgreSQL). Si se cambia una
/// columna hay que cambiar el número acá, y es el único lugar donde tocarlo.
/// </para>
/// </remarks>
public static class ReglasProducto
{
    // Rango compartido por todos los campos numéricos del producto (Contenido, Peso
    // teórico, Tara, Precio por kg): sin negativos ni cero, tope 999999. Un solo lugar
    // para subir la precisión de decimales el día que lo pidan.
    public const decimal ValorNumericoMinimo = 0;   // exclusivo — el valor debe ser > 0
    public const decimal ValorNumericoMaximo = 999999;
    public const int     DecimalesPorDefecto = 2;

    // Columna: codigo_producto (varchar 50)
    public static readonly ReglaCampo Codigo      = new(Obligatorio: true, LargoMaximo: 50);

    // Columna: nombre_producto (varchar 200)
    public static readonly ReglaCampo Nombre      = new(Obligatorio: true, LargoMaximo: 200);

    // Columna: contenido (numeric) — antes varchar de texto libre ("20 kg", "1 und")
    public static readonly ReglaCampo Contenido   = new(Obligatorio: true, Formato: FormatoCampo.Decimal,
        Minimo: ValorNumericoMinimo, Maximo: ValorNumericoMaximo, Decimales: DecimalesPorDefecto);

    // Columna: peso_teorico (numeric)
    public static readonly ReglaCampo PesoTeorico = new(Obligatorio: true, Formato: FormatoCampo.Decimal,
        Minimo: ValorNumericoMinimo, Maximo: ValorNumericoMaximo, Decimales: DecimalesPorDefecto);

    // Columna: peso_tara (numeric) — antes id_tara, FK a un catálogo compartido
    public static readonly ReglaCampo Tara        = new(Obligatorio: true, Formato: FormatoCampo.Decimal,
        Minimo: ValorNumericoMinimo, Maximo: ValorNumericoMaximo, Decimales: DecimalesPorDefecto);

    // Columna: precio_por_kg (numeric)
    public static readonly ReglaCampo PrecioPorKg = new(Obligatorio: true, Formato: FormatoCampo.Decimal,
        Minimo: ValorNumericoMinimo, Maximo: ValorNumericoMaximo, Decimales: DecimalesPorDefecto);
}

public static class ReglasCategoria
{
    // Columna: nombre_categoria (varchar 100)
    public static readonly ReglaCampo Nombre      = new(Obligatorio: true, LargoMaximo: 100);

    // Columna: descripcion_categoria (varchar 200)
    public static readonly ReglaCampo Descripcion = new(LargoMaximo: 200);
}

public static class ReglasPresentacion
{
    // Columna: nombre_presentacion (varchar 100)
    public static readonly ReglaCampo Nombre      = new(Obligatorio: true, LargoMaximo: 100);

    // Columna: descripcion_presentacion (varchar 500)
    public static readonly ReglaCampo Descripcion = new(LargoMaximo: 500);
}

public static class ReglasFabricante
{
    // Columna: nombre_fabricante (varchar 200)
    public static readonly ReglaCampo Nombre      = new(Obligatorio: true, LargoMaximo: 200);

    // Columna: descripcion_fabricante (varchar 500)
    public static readonly ReglaCampo Descripcion = new(LargoMaximo: 500);

    // Proveedor y País quedan fuera: son opcionales por diseño y el combo
    // ofrece "(Ninguno)" como primera opción.
}

public static class ReglasProveedor
{
    // Columna: nombre_proveedor (varchar 200)
    public static readonly ReglaCampo Nombre    = new(Obligatorio: true, LargoMaximo: 200);

    // Columna: rtn_proveedor (varchar 20)
    public static readonly ReglaCampo Rtn       = new(LargoMaximo: 20, Formato: FormatoCampo.Rtn);

    // Columna: telefono_proveedor (varchar 20)
    public static readonly ReglaCampo Telefono  = new(LargoMaximo: 20, Formato: FormatoCampo.Telefono);

    // Columna: correo_proveedor (varchar 100)
    public static readonly ReglaCampo Correo    = new(LargoMaximo: 100, Formato: FormatoCampo.Correo);

    // Columna: direccion_proveedor (varchar 500)
    public static readonly ReglaCampo Direccion = new(LargoMaximo: 500);
}

public static class ReglasEmpleado
{
    // Columna: nombre_empleado (varchar 100)
    public static readonly ReglaCampo Nombre    = new(Obligatorio: true, LargoMaximo: 100);

    // Columna: apellido_empleado (varchar 100)
    public static readonly ReglaCampo Apellido  = new(Obligatorio: true, LargoMaximo: 100);

    // Columna: numero_identidad (varchar 20)
    public static readonly ReglaCampo Identidad = new(Obligatorio: true, LargoMaximo: 20);

    // Columna: telefono_empleado (varchar 20)
    public static readonly ReglaCampo Telefono  = new(LargoMaximo: 20, Formato: FormatoCampo.Telefono);

    // Columna: correo_empleado (varchar 100)
    public static readonly ReglaCampo Correo    = new(LargoMaximo: 100, Formato: FormatoCampo.Correo);
}

public static class ReglasUsuario
{
    // Columna: id_empleado (integer)
    public static readonly ReglaCampo Empleado = new(Obligatorio: true);

    // Columna: id_rol (integer)
    public static readonly ReglaCampo Rol      = new(Obligatorio: true);

    // Columna: alias_usuario (varchar 50)
    public static readonly ReglaCampo Correo   = new(Obligatorio: true, LargoMaximo: 50, Formato: FormatoCampo.Correo);

    /// <summary>
    /// Supabase Auth / Bcrypt: mínimo 6 caracteres exigido por el servidor de autenticación
    /// y máximo 72 caracteres defensivo por el truncamiento de algoritmo Bcrypt.
    /// </summary>
    // Supabase Auth / Bcrypt (mínimo 6, máximo 72)
    public static readonly ReglaCampo Password = new(Obligatorio: true, LargoMinimo: 6, LargoMaximo: 72);
}

public static class ReglasRol
{
    // Columna: nombre_rol (varchar 50)
    public static readonly ReglaCampo Nombre = new(Obligatorio: true, LargoMaximo: 50);
}

public static class ReglasContacto
{
    // Columna: nombre_contacto (varchar 100 en contactos_fabricante / contactos_proveedor)
    public static readonly ReglaCampo Nombre   = new(Obligatorio: true, LargoMaximo: 100);

    // Columna: telefono_contacto (varchar 20 en contactos_fabricante / contactos_proveedor)
    public static readonly ReglaCampo Telefono = new(LargoMaximo: 20, Formato: FormatoCampo.Telefono);

    // Columna: correo_contacto (varchar 100 en contactos_fabricante / contactos_proveedor)
    public static readonly ReglaCampo Correo   = new(LargoMaximo: 100, Formato: FormatoCampo.Correo);
}

/// <summary>
/// Camión de una recepción de materia prima — la fila de <c>movimientos</c>.
/// </summary>
/// <remarks>
/// Los dos largos salen de <c>20260905215247_limitar_texto_movimientos.sql</c>.
/// Antes de esa migración las columnas no tenían tope (varchar sin límite y
/// <c>text</c>), así que no había nada de la base que reflejar acá.
/// </remarks>
public static class ReglasCamion
{
    // Columna: id_proveedor (integer NOT NULL, FK a proveedores)
    public static readonly ReglaCampo Proveedor   = new(Obligatorio: true);

    // Columna: placa_vehiculo (varchar 20)
    public static readonly ReglaCampo Placa       = new(Obligatorio: true, LargoMaximo: 20);

    // Columna: observaciones (varchar 500) — «Descripción» en la pantalla de registro
    public static readonly ReglaCampo Descripcion = new(LargoMaximo: 500);
}

/// <summary>
/// Producto asignado a un camión de recepción — la fila de <c>movimiento_productos</c>.
/// </summary>
public static class ReglasProductoCamion
{
    // Columna: id_producto (integer NOT NULL)
    public static readonly ReglaCampo IdProducto    = new(Obligatorio: true);

    // Columna: cantidad / peso manifestado / bultos teóricos
    public static readonly ReglaCampo Cantidad      = new(Obligatorio: true);

    // Columna: observaciones (varchar 500)
    public static readonly ReglaCampo Observaciones = new(LargoMaximo: 500);
}

/// <summary>
/// Pesaje individual de materia prima — la fila de <c>entradas_producto</c>.
/// </summary>
public static class ReglasEntradaPesaje
{
    // Columna: peso_bruto (numeric NOT NULL)
    public static readonly ReglaCampo PesoBruto     = new(Obligatorio: true);

    // Columna: peso_tara_extra (numeric NOT NULL, default 0)
    public static readonly ReglaCampo TaraExtra     = new(Obligatorio: false);

    // Columna: observaciones (varchar 500)
    public static readonly ReglaCampo Observaciones = new(LargoMaximo: 500);
}

public static class ReglasEmpresa
{
    // Columna: nombre_empresa (varchar 200)
    public static readonly ReglaCampo Nombre    = new(Obligatorio: true, LargoMaximo: 200);

    // Columna: rtn_empresa (varchar 20)
    public static readonly ReglaCampo Rtn       = new(LargoMaximo: 20, Formato: FormatoCampo.Rtn);

    // Columna: telefono_empresa (varchar 20)
    public static readonly ReglaCampo Telefono  = new(LargoMaximo: 20, Formato: FormatoCampo.Telefono);

    // Columna: correo_empresa (varchar 100)
    public static readonly ReglaCampo Correo    = new(LargoMaximo: 100, Formato: FormatoCampo.Correo);

    // Columna: direccion_empresa (varchar 500)
    public static readonly ReglaCampo Direccion = new(LargoMaximo: 500);
}
