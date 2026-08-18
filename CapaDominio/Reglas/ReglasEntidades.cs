namespace CapaDominio.Reglas;

/// <summary>
/// Qué exige el negocio de cada campo, por entidad.
/// </summary>
/// <remarks>
/// <para>
/// Antes esto vivía en cada modal, dentro de la declaración del validador. Que
/// el nombre de un producto sea obligatorio y tope en 150 caracteres no es una
/// decisión de la pantalla: sale del negocio y del esquema de la base. Con la
/// regla acá, la UI deja de decidirla y pasa a aplicarla — y si mañana hace
/// falta chequear lo mismo antes de un insert, la fuente ya es única.
/// </para>
/// <para>
/// Los largos máximos reflejan las columnas de Supabase. Si se cambia una
/// columna hay que cambiar el número acá, y es el único lugar donde tocarlo.
/// </para>
/// </remarks>
public static class ReglasProducto
{
    public static readonly ReglaCampo Codigo      = new(Obligatorio: true, LargoMaximo: 50);
    public static readonly ReglaCampo Nombre      = new(Obligatorio: true, LargoMaximo: 150);
    public static readonly ReglaCampo PesoTeorico = new(Formato: FormatoCampo.Decimal);
    public static readonly ReglaCampo PrecioPorKg = new(Formato: FormatoCampo.Decimal);
}

public static class ReglasCategoria
{
    public static readonly ReglaCampo Nombre      = new(Obligatorio: true, LargoMaximo: 100);
    public static readonly ReglaCampo Descripcion = new(LargoMaximo: 255);
}

public static class ReglasPresentacion
{
    public static readonly ReglaCampo Nombre      = new(Obligatorio: true, LargoMaximo: 100);
    public static readonly ReglaCampo Descripcion = new(LargoMaximo: 255);
}

public static class ReglasFabricante
{
    public static readonly ReglaCampo Nombre      = new(Obligatorio: true, LargoMaximo: 100);
    public static readonly ReglaCampo Descripcion = new(LargoMaximo: 255);
    // Proveedor y País quedan fuera: son opcionales por diseño y el combo
    // ofrece "(Ninguno)" como primera opción.
}

public static class ReglasProveedor
{
    public static readonly ReglaCampo Nombre    = new(Obligatorio: true, LargoMaximo: 100);
    public static readonly ReglaCampo Rtn       = new(Formato: FormatoCampo.Rtn);
    public static readonly ReglaCampo Telefono  = new(Formato: FormatoCampo.Telefono);
    public static readonly ReglaCampo Correo    = new(Formato: FormatoCampo.Correo);
    public static readonly ReglaCampo Direccion = new(LargoMaximo: 255);
}

public static class ReglasEmpleado
{
    public static readonly ReglaCampo Nombre   = new(Obligatorio: true, LargoMaximo: 100);
    public static readonly ReglaCampo Apellido = new(Obligatorio: true, LargoMaximo: 100);
    public static readonly ReglaCampo Telefono = new(Formato: FormatoCampo.Telefono);
    public static readonly ReglaCampo Correo   = new(Formato: FormatoCampo.Correo);
}

public static class ReglasUsuario
{
    public static readonly ReglaCampo Empleado = new(Obligatorio: true);
    public static readonly ReglaCampo Rol      = new(Obligatorio: true);

    /// <summary>
    /// Mínimo de 6 caracteres. Es el piso que exige Supabase Auth al dar de alta
    /// una credencial: por debajo, el alta falla del lado del servidor.
    /// </summary>
    public static readonly ReglaCampo Password = new(LargoMinimo: 6);
}

public static class ReglasContacto
{
    public static readonly ReglaCampo Nombre   = new(Obligatorio: true, LargoMaximo: 100);
    public static readonly ReglaCampo Telefono = new(Formato: FormatoCampo.Telefono);
    public static readonly ReglaCampo Correo   = new(Formato: FormatoCampo.Correo);
}

public static class ReglasEmpresa
{
    public static readonly ReglaCampo Nombre   = new(Obligatorio: true, LargoMaximo: 150);
    public static readonly ReglaCampo Rtn      = new(Formato: FormatoCampo.Rtn);
    public static readonly ReglaCampo Telefono = new(Formato: FormatoCampo.Telefono);
    public static readonly ReglaCampo Correo   = new(Formato: FormatoCampo.Correo);
}
