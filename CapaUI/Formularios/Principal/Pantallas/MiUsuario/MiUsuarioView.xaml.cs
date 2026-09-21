using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CapaUI.Core.Controls;

namespace CapaUI.Formularios.Principal.Pantallas.MiUsuario;

/// <summary>
/// Vista de «Mi Usuario». El comportamiento vive en <see cref="MiUsuarioViewModel"/>,
/// que llega por <c>DataContext</c> desde el <c>DataTemplate</c> de <c>MainWindow</c>.
/// <para/>
/// La única lógica que vive en code-behind es la gestión de <c>PasswordBox</c>:
/// WPF no permite binding bidireccional del campo Password, así que la vista
/// traduce los eventos <c>PasswordChanged</c> al VM (mismo patrón que el panel
/// «ForgotNewPanel» del login) y sincroniza el ojo de mostrar/ocultar de cada
/// campo con <see cref="PasswordVisibilityController"/>.
/// </summary>
public partial class MiUsuarioView : UserControl
{
    // (PasswordBox, TextBox visible que lo reemplaza al ojo abierto)
    private bool _controlesIniciados;
    private (PasswordBox Box, TextBox Visible)? _parActual;
    private (PasswordBox Box, TextBox Visible)? _parNueva;
    private (PasswordBox Box, TextBox Visible)? _parConfirmar;

    private PasswordVisibilityController? _ctlActual;
    private PasswordVisibilityController? _ctlNueva;
    private PasswordVisibilityController? _ctlConfirmar;

    public MiUsuarioView()
    {
        InitializeComponent();

        // Los PasswordBox no previsualizan su texto reutilizando DataContext, así
        // que el ojo actúa en la ACTUAL, la NUEVA y la CONFIRMACIÓN, siempre con
        // el mismo PasswordVisibilityController del login/UsuarioModal.
        Loaded += (_, _) => IniciarControlesPassword();
    }

    private void IniciarControlesPassword()
    {
        // Idempotente (anti-leak): `Loaded` de un UserControl se dispara CADA vez que
        // la vista entra al árbol visual, no solo al primer arranque. Subscribir de
        // nuevo en cada visita sumaba suscripciones sobre las MISMAS PasswordBox sin
        // liberar las anteriores (el controlador viejo quedaba vivo con su closure:
        // fuga de memoria por navegación, exactamente lo que NO habia en los demás
        // módulos, que no re-ensamblan nada en Loaded). Una sola vez es suficiente:
        // los PasswordBox y el VM son nombres estables de la misma vista.
        if (_controlesIniciados) return;
        _controlesIniciados = true;

        // Armado defensivo: FindName tolera el orden del ciclo de vida (§10 del nodo
        // de convenciones) — evita CS0103 si la caché .g.i.cs se desincroniza en VS.

        _parActual    = FindName("TxtActual") as PasswordBox is { } actual
            && FindName("TxtActualVisible") as TextBox is { } actualVisible
                ? (actual, actualVisible)
                : null;
        _parNueva = FindName("TxtNueva") as PasswordBox is { } nueva
            && FindName("TxtNuevaVisible") as TextBox is { } nuevaVisible
                ? (nueva, nuevaVisible)
                : null;
        _parConfirmar = FindName("TxtConfirmar") as PasswordBox is { } confirmar
            && FindName("TxtConfirmarVisible") as TextBox is { } confirmarVisible
                ? (confirmar, confirmarVisible)
                : null;

        _ctlActual     = CrearControl(_parActual,    HandleActualChanged);
        _ctlNueva      = CrearControl(_parNueva,     HandleNuevaChanged);
        _ctlConfirmar  = CrearControl(_parConfirmar, HandleConfirmarChanged);
    }

    private static PasswordVisibilityController? CrearControl(
        (PasswordBox Box, TextBox Visible)? par,
        Action<PasswordBox>? onPasswordChanged)
    {
        if (par is not { } pair) return null;

        pair.Box.PasswordChanged += (_, _) => onPasswordChanged?.Invoke(pair.Box);
        return new PasswordVisibilityController(pair.Box, pair.Visible);
    }

    /// <summary>Empuja la contraseña actual al VM y limpia el resultado previo.</summary>
    private void HandleActualChanged(PasswordBox box)
        => (DataContext as MiUsuarioViewModel)?.EstablecerActual(box.Password);

    private void HandleNuevaChanged(PasswordBox box)
    {
        var vm = DataContext as MiUsuarioViewModel;
        if (vm is null) return;

        vm.EstablecerNueva(box.Password);

        // Redibuja el estado visual: medidor, checklist y etiqueta de fortaleza.
        PintarMedidor(vm.ScoreFortaleza);
        PintarChecklist(vm.RequisitosOk);
        if (!vm.HayCoincidencia) MostrarMismatch(); else OcultarMismatch();
    }

    private void HandleConfirmarChanged(PasswordBox box)
    {
        var vm = DataContext as MiUsuarioViewModel;
        if (vm is null) return;

        vm.EstablecerConfirmar(box.Password);
        if (!vm.HayCoincidencia) MostrarMismatch(); else OcultarMismatch();
    }

    // ── Ojos de mostrar/ocultar ─────────────────────────────────────────────────
    // Los tres campos (ACTUAL, NUEVA y CONFIRMACIÓN) usan el mismo
    // PasswordVisibilityController del login/UsuarioModal: intercambia
    // PasswordBox ⇄ TextBox y sincroniza el texto, y los PasswordChanged de la
    // caja canónica siguen empujando al VM. Decisión del usuario 2026-09-21:
    // la ACTUAL también lleva ojo.

    private void BtnVerActual_Click(object sender, RoutedEventArgs e)    => _ctlActual?.Toggle();
    private void BtnVerNueva_Click(object sender, RoutedEventArgs e)     => _ctlNueva?.Toggle();
    private void BtnVerConfirmar_Click(object sender, RoutedEventArgs e) => _ctlConfirmar?.Toggle();

    // ── Teclado: Enter = ejecutar, Tab = bajar ──────────────────────────────────
    // El orden de Tab lo fija TabIndex en el XAML (ACTUAL 10 → NUEVA 20 →
    // CONFIRMAR 30, ojo después de cada caja). Enter en el apodo guarda; Enter en
    // cualquier campo de contraseña intenta el cambio si el formulario está completo.

    /// <summary>Enter en el apodo ejecuta GuardarApodoCommand.</summary>
    private void TxtApodo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        // El TextBox visible tiene UpdateSourceTrigger=PropertyChanged, así que
        // el VM ya tiene el texto final antes del guardado.
        (DataContext as MiUsuarioViewModel)?.GuardarApodoCommand.Execute(null);
    }

    /// <summary>Enter en ACTUAL/NUEVA/CONFIRMAR intenta el cambio si la forma está completa.</summary>
    private void Contrasena_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var vm = DataContext as MiUsuarioViewModel;
        if (vm is null || !vm.PuedeEnviarCambio) return;

        e.Handled = true;
        vm.CambiarContrasenaCommand.Execute(null);
    }

    /// <summary>
    /// «Limpiar»: los PasswordBox son unidireccionales hacia el VM — setear
    /// <c>ContrasenaActual = ""</c> en el VM NO vacía la caja, así que este handler
    /// vacía las tres parejas (ojo abierto incluido) en la vista y luego deja al VM
    /// limpiar sus banderas de aviso/éxito/error.
    /// </summary>
    private void BtnLimpiarFormulario_Click(object sender, RoutedEventArgs e)
    {
        if (FindName("TxtActual") as PasswordBox is { } actual)  actual.Password = string.Empty;
        if (FindName("TxtNueva") as PasswordBox is { } nueva)      nueva.Password = string.Empty;
        if (FindName("TxtNuevaVisible") as TextBox is { } nV)      nV.Text = string.Empty;
        if (FindName("TxtConfirmar") as PasswordBox is { } conf)   conf.Password = string.Empty;
        if (FindName("TxtConfirmarVisible") as TextBox is { } cV)  cV.Text = string.Empty;

        // El medidor, la checklist y el aviso de no-coincidencia vuelven al estado vacío.
        PintarMedidor(0);
        PintarChecklist(new[] { false, false, false, false });
        OcultarMismatch();

        (DataContext as MiUsuarioViewModel)?.LimpiarFormularioCommand.Execute(null);
    }

    // ── Estado visual: medidor de fortaleza (lapsos 0..5) ──────────────────────
    private void PintarMedidor(int score)
    {
        if (FindName("Bar1") is not Border b1) return;
        var barras = new[] { b1, FindName("Bar2") as Border, FindName("Bar3") as Border,
                             FindName("Bar4") as Border, FindName("Bar5") as Border };

        // Color por nivel de fortaleza: rojo suave → ámbar → esmeralda.
        var colorAlcanzado = score <= 2
            ? new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71))
            : score == 3
                ? new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24))
                : new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));

        for (int i = 0; i < barras.Length; i++)
            if (barras[i] is not null)
                barras[i]!.Background = i < score ? colorAlcanzado : new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));

        if (FindName("LblFortaleza") is TextBlock lbl)
            lbl.Text = score switch
            {
                0 => "Vacía",
                1 => "Muy débil",
                2 => "Débil",
                3 => "Aceptable",
                4 => "Fuerte",
                _ => "Muy fuerte",
            };
    }

    private void PintarChecklist(bool[] reglasOk)
    {
        var referencias = new[] { ("R1Icon", "R1Text"), ("R2Icon", "R2Text"),
                                  ("R3Icon", "R3Text"), ("R4Icon", "R4Text")};

        for (int i = 0; i < referencias.Length; i++)
        {
            if (FindName(referencias[i].Item1) is not Ellipse icono) return;
            icono.Fill = reglasOk[i]
                ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)) // verde dominio
                : new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
        }
    }

    private void MostrarMismatch()
    {
        if (FindName("LblCoincidencia") is TextBlock lbl) lbl.Visibility = Visibility.Visible;
    }

    private void OcultarMismatch()
    {
        if (FindName("LblCoincidencia") is TextBlock lbl) lbl.Visibility = Visibility.Collapsed;
    }
}
