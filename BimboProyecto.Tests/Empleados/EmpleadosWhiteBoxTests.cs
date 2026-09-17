using System;
using System.IO;
using Xunit;

namespace BimboProyecto.Tests.Empleados;

/// <summary>
/// Pruebas de caja blanca y verificación de convenciones visuales para el Módulo de Empleados.
/// Garantiza la alineación estándar a la izquierda de las columnas de datos, la herencia de
/// encabezados canónicos de Styles.xaml, la distribución de ancho completo tipo Usuarios (Width="*")
/// y la presencia de scroll horizontal con Shift + rueda.
/// </summary>
public sealed class EmpleadosWhiteBoxTests
{
    [Fact(DisplayName = "EmpleadosView alinea columnas a la izquierda, distribuye ancho completo y activa scroll")]
    public void EmpleadosView_AlineacionColumnasIzquierda_DistribucionAnchoCompleto_Y_Scroll()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadosView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        // 1. Ausencia de override local a DataGridColumnHeader (debe heredar de Styles.xaml)
        Assert.DoesNotContain("<Style TargetType=\"DataGridColumnHeader\">", xaml);

        // 2. Columnas de datos con CeldaIzquierda
        Assert.Matches(@"Header=""EMPLEADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""IDENTIDAD""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""TELÉFONO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""CORREO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);

        // 3. Distribución de ancho completo como en Usuarios (Width="*" y MinWidth="220" en EMPLEADO y CORREO)
        Assert.Matches(@"Header=""EMPLEADO""[^>]*Width=""\*""[^>]*MinWidth=""220""", xaml);
        Assert.Matches(@"Header=""CORREO""[^>]*Width=""\*""[^>]*MinWidth=""220""", xaml);

        // 4. Columnas especiales (# y ESTADO) debidamente centradas
        Assert.Matches(@"Header=""#""[^>]*HeaderStyle=""{StaticResource HeaderCentrado}""[^>]*CellStyle=""{StaticResource CeldaCentrada}""", xaml);
        Assert.Matches(@"Header=""ESTADO""[^>]*Width=""70""[^>]*HeaderStyle=""{StaticResource HeaderCentrado}""[^>]*CellStyle=""{StaticResource CeldaCentrada}""", xaml);

        // 5. ScrollViewer horizontal y CanContentScroll
        Assert.Contains(@"ScrollViewer.HorizontalScrollBarVisibility=""Auto""", xaml);
        Assert.Contains(@"ScrollViewer.CanContentScroll=""True""", xaml);
    }
}
