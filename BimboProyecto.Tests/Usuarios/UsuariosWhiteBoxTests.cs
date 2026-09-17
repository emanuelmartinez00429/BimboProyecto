using System;
using System.IO;
using Xunit;

namespace BimboProyecto.Tests.Usuarios;

/// <summary>
/// Pruebas de caja blanca y verificación de convenciones visuales para el Módulo de Usuarios.
/// Garantiza la alineación estándar a la izquierda de las columnas de datos, la herencia de
/// encabezados canónicos de Styles.xaml y la ausencia de regresiones hacia HeaderDerecho/CeldaDerecha.
/// </summary>
public sealed class UsuariosWhiteBoxTests
{
    [Fact(DisplayName = "UsuariosView alinea columnas a la izquierda y elimina overrides locales de encabezado")]
    public void UsuariosView_AlineacionColumnasIzquierda_Y_SinOverrideLocalDeHeader()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuariosView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        // 1. Ausencia absoluta de estilos derechos residuales
        Assert.DoesNotContain("HeaderDerecho", xaml);
        Assert.DoesNotContain("CeldaDerecha", xaml);

        // 2. Ausencia de override local a DataGridColumnHeader (debe heredar de Styles.xaml)
        Assert.DoesNotContain("<Style TargetType=\"DataGridColumnHeader\">", xaml);

        // 3. CeldaIzquierda como estilo por defecto del DataGrid
        Assert.Contains("CellStyle=\"{StaticResource CeldaIzquierda}\"", xaml);

        // 4. Columnas de texto y fecha alineadas a la izquierda con CeldaIzquierda
        Assert.Matches(@"Header=""EMPLEADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""EMAIL""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ROL""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ÚLTIMO ACCESO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""CREADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ACTUALIZADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);

        // 5. Columnas especiales (# y ESTADO) debidamente centradas
        Assert.Matches(@"Header=""#""[^>]*CellStyle=""{StaticResource CeldaCentrada}""", xaml);
        Assert.Matches(@"Header=""ESTADO""[^>]*HeaderStyle=""{StaticResource HeaderCentrado}""[^>]*CellStyle=""{StaticResource CeldaCentrada}""", xaml);
    }
}
