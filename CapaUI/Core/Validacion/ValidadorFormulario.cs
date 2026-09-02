using CapaDominio.Reglas;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CapaUI.Core.Validacion;

/// <summary>
/// Declara las reglas de un formulario una sola vez y las evalúa cuando haga
/// falta: al salir de cada campo y al guardar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué se retiene y no se arma dentro de <c>BtnGuardar_Click</c>:</b> la
/// validación corre en dos momentos distintos (<c>LostFocus</c> y Guardar). Si
/// las reglas se declararan dentro del handler de guardado, habría que repetirlas
/// para el <c>LostFocus</c>. Por eso esto se construye una vez —en el
/// <c>OnLoaded</c> del modal— y queda guardado en un campo.
/// </para>
/// <para>
/// Solo se ocupa de la UI: leer el control, marcarlo, poner el foco, mostrar el
/// mensaje. El "qué exige el negocio" vive en <c>CapaDominio.Reglas</c>
/// (<see cref="ReglaCampo"/> y <see cref="ReglasFormato"/>), que no sabe nada de
/// WPF; lo único que queda de este lado es el parseo dependiente de cultura, en
/// <see cref="ParseoNumerico"/>.
/// </para>
/// <example>
/// <code>
/// _validador = ValidadorFormulario.Nuevo()
///     .Campo(TxtNombre, "El nombre").Segun(ReglasProveedor.Nombre)
///     .Campo(TxtCorreo, "El correo").Segun(ReglasProveedor.Correo)
///     .Combo(CmbRol,    "El rol").Segun(ReglasUsuario.Rol)
///     .ValidarAlSalirDelCampo();
///
/// // al guardar
/// if (!_validador.Validar()) return;
/// </code>
/// </example>
/// </remarks>
public sealed class ValidadorFormulario
{
    private readonly List<CampoValidado> _campos = new();

    private ValidadorFormulario() { }

    public static ValidadorFormulario Nuevo() => new();

    // ── Declaración de campos ─────────────────────────────────────────────

    public ConstructorCampo Campo(TextBox caja, string etiqueta) =>
        Agregar(new CampoValidado(caja, etiqueta, () => caja.Text, () => !string.IsNullOrWhiteSpace(caja.Text)));

    public ConstructorCampo Clave(PasswordBox caja, string etiqueta) =>
        Agregar(new CampoValidado(caja, etiqueta, () => caja.Password, () => caja.Password.Length > 0));

    public ConstructorCampo Combo(Selector combo, string etiqueta) =>
        Agregar(new CampoValidado(combo, etiqueta, () => combo.SelectedItem?.ToString(), () => combo.SelectedItem is not null));

    private ConstructorCampo Agregar(CampoValidado campo)
    {
        _campos.Add(campo);
        return new ConstructorCampo(this, campo);
    }

    // ── Evaluación ────────────────────────────────────────────────────────

    /// <summary>
    /// Engancha cada campo a su <c>LostFocus</c> para avisar apenas se sale de
    /// uno, sin esperar al guardado.
    /// </summary>
    /// <remarks>
    /// Un campo vacío que nunca se tocó no se marca: si no, abrir un formulario
    /// nuevo y tabular por él lo pintaría todo de rojo antes de que el usuario
    /// escriba nada. Solo se marca lo que se visitó y quedó mal.
    /// </remarks>
    public ValidadorFormulario ValidarAlSalirDelCampo()
    {
        foreach (var campo in _campos)
        {
            var actual = campo;
            actual.Control.LostFocus += (_, _) =>
            {
                actual.Visitado = true;
                Evaluar(actual);
            };
        }
        return this;
    }

    /// <summary>
    /// Valida todo. Marca en rojo los campos con problema, le da el foco al
    /// primero y muestra su mensaje. Devuelve <c>true</c> si se puede guardar.
    /// </summary>
    public bool Validar()
    {
        CampoValidado? primerFallo = null;

        foreach (var campo in _campos)
        {
            campo.Visitado = true;             // al guardar se marca todo, no solo lo visitado
            if (!Evaluar(campo)) primerFallo ??= campo;
        }

        if (primerFallo is null) return true;

        primerFallo.Control.Focus();
        if (primerFallo.Control is TextBox caja) caja.SelectAll();

        MessageBox.Show(primerFallo.MensajeError, "Validación",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    /// <summary>Borra las marcas de error. Útil al reabrir o resetear el modal.</summary>
    public void Limpiar()
    {
        foreach (var campo in _campos)
        {
            campo.Visitado = false;
            AplicarError(campo, null);
        }
    }

    private bool Evaluar(CampoValidado campo)
    {
        string? mensaje = campo.PrimerError();

        // Sin visitar todavía: se calla, pero igual reporta el estado real para
        // que Validar() sepa que hay algo mal.
        AplicarError(campo, campo.Visitado ? mensaje : null);
        campo.MensajeError = mensaje ?? string.Empty;
        return mensaje is null;
    }

    // ── Presentación del error ────────────────────────────────────────────

    private static void AplicarError(CampoValidado campo, string? mensaje)
    {
        Validacion.SetError(campo.Control, mensaje);
        campo.Control.ToolTip = mensaje;   // respaldo si el campo no está en un CampoModal
        MostrarRenglon(campo, mensaje);
    }

    /// <summary>
    /// Inserta (o quita) el renglón de error debajo del campo.
    /// </summary>
    /// <remarks>
    /// El renglón se crea por código y no en XAML porque son ~50 campos repartidos
    /// en 10 modales: agregarlo a mano archivo por archivo sería el mismo trabajo
    /// repetido que este validador viene a eliminar. Se apoya en que todos los
    /// campos comparten la estructura <c>CampoModal</c> (StackPanel con etiqueta +
    /// input) que quedó uniforme al centralizar los estilos.
    ///
    /// Si no encuentra ese StackPanel —Configuración de Empresa usa su propio
    /// layout— no hace nada y el error igual se ve por el ToolTip y el borde rojo.
    /// </remarks>
    private static void MostrarRenglon(CampoValidado campo, string? mensaje)
    {
        var panel = BuscarPanelDelCampo(campo.Control);
        if (panel is null) return;

        if (string.IsNullOrEmpty(mensaje))
        {
            if (campo.Renglon is not null) panel.Children.Remove(campo.Renglon);
            campo.Renglon = null;
            return;
        }

        if (campo.Renglon is null)
        {
            campo.Renglon = new TextBlock
            {
                FontFamily   = new FontFamily("Segoe UI"),
                FontSize     = 13,
                Foreground   = new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5)),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(2, 4, 0, 0),
            };
            panel.Children.Add(campo.Renglon);
        }

        campo.Renglon.Text = mensaje;
    }

    /// <summary>
    /// Sube por el árbol visual hasta el StackPanel que envuelve al campo. No es
    /// siempre el padre directo: los campos de catálogo meten un Grid entremedio
    /// para poner la lupa al lado del TextBox.
    /// </summary>
    private static Panel? BuscarPanelDelCampo(DependencyObject control)
    {
        DependencyObject? actual = control;

        for (int nivel = 0; nivel < 4 && actual is not null; nivel++)
        {
            actual = VisualTreeHelper.GetParent(actual);
            if (actual is StackPanel panel) return panel;
        }

        return null;
    }

    // ── Tipos internos ────────────────────────────────────────────────────

    internal sealed class CampoValidado
    {
        public CampoValidado(Control control, string etiqueta,
                             Func<string?> leerTexto, Func<bool> tieneValor)
        {
            Control    = control;
            Etiqueta   = etiqueta;
            LeerTexto  = leerTexto;
            TieneValor = tieneValor;
        }

        public Control       Control    { get; }
        public string        Etiqueta   { get; }
        public Func<string?> LeerTexto  { get; }
        public Func<bool>    TieneValor { get; }

        public List<(Func<CampoValidado, bool> EsValido, string Mensaje)> Reglas { get; } = new();

        public bool       Visitado     { get; set; }
        public string     MensajeError { get; set; } = string.Empty;
        public TextBlock? Renglon      { get; set; }

        /// <summary>Primer mensaje que falla, o <c>null</c> si está todo bien.</summary>
        public string? PrimerError()
        {
            foreach (var (esValido, mensaje) in Reglas)
                if (!esValido(this)) return mensaje;

            return null;
        }
    }

    /// <summary>
    /// Encadena reglas sobre el último campo declarado y deja seguir con el
    /// siguiente, para que todo el formulario se lea como una sola expresión.
    /// </summary>
    public sealed class ConstructorCampo
    {
        private readonly ValidadorFormulario _validador;
        private readonly CampoValidado       _campo;

        internal ConstructorCampo(ValidadorFormulario validador, CampoValidado campo)
        {
            _validador = validador;
            _campo     = campo;
        }

        public ConstructorCampo Obligatorio(string? mensaje = null)
        {
            _campo.Reglas.Add((c => c.TieneValor(),
                mensaje ?? $"{_campo.Etiqueta} es obligatorio."));
            return this;
        }

        public ConstructorCampo Correo(string? mensaje = null)
        {
            _campo.Reglas.Add((c => ReglasFormato.EsCorreo(c.LeerTexto()),
                mensaje ?? $"{_campo.Etiqueta} no tiene un formato válido."));
            return this;
        }

        public ConstructorCampo Rtn(string? mensaje = null)
        {
            _campo.Reglas.Add((c => ReglasFormato.EsRtn(c.LeerTexto()),
                mensaje ?? $"{_campo.Etiqueta} debe tener 14 dígitos; solo se permiten números, espacios y guiones."));
            return this;
        }

        public ConstructorCampo Telefono(string? mensaje = null)
        {
            _campo.Reglas.Add((c => ReglasFormato.EsTelefono(c.LeerTexto()),
                mensaje ?? $"{_campo.Etiqueta} debe tener entre 8 y 15 dígitos; solo se permiten números, espacios, guiones y + al inicio."));
            return this;
        }

        public ConstructorCampo LargoMaximo(int largo, string? mensaje = null)
        {
            TopePreventivo(largo);
            _campo.Reglas.Add((c => ReglasFormato.NoExcedeLargo(c.LeerTexto(), largo),
                mensaje ?? $"{_campo.Etiqueta} no puede superar los {largo} caracteres."));
            return this;
        }

        public ConstructorCampo LargoMinimo(int largo, string? mensaje = null)
        {
            _campo.Reglas.Add((c => ReglasFormato.TieneLargoMinimo(c.LeerTexto(), largo),
                mensaje ?? $"{_campo.Etiqueta} debe tener al menos {largo} caracteres."));
            return this;
        }

        public ConstructorCampo Decimal(string? mensaje = null)
        {
            _campo.Reglas.Add((c => ParseoNumerico.EsDecimalOpcional(c.LeerTexto(), out _),
                mensaje ?? $"{_campo.Etiqueta} debe ser un número válido."));
            return this;
        }

        public ConstructorCampo Entero(string? mensaje = null)
        {
            _campo.Reglas.Add((c => ParseoNumerico.EsEnteroOpcional(c.LeerTexto(), out _),
                mensaje ?? $"{_campo.Etiqueta} debe ser un número entero."));
            return this;
        }

        /// <summary>
        /// Aplica una regla declarada por el dominio.
        /// </summary>
        /// <remarks>
        /// Es la forma preferida: el "qué" (obligatorio, largo, formato) sale de
        /// <c>CapaDominio.Reglas</c> y acá solo se traduce a las comprobaciones y
        /// los mensajes. Los métodos sueltos de abajo siguen existiendo para lo
        /// que no tiene una regla de negocio detrás.
        /// </remarks>
        public ConstructorCampo Segun(ReglaCampo regla)
        {
            if (regla.Obligatorio)          Obligatorio();
            if (regla.LargoMaximo is int m)
            {
                TopePreventivo(m);
                LargoMaximo(m);
            }
            if (regla.LargoMinimo is int n) LargoMinimo(n);

            switch (regla.Formato)
            {
                case FormatoCampo.Correo:   Correo();   break;
                case FormatoCampo.Rtn:      Rtn();      break;
                case FormatoCampo.Telefono: Telefono(); break;
                case FormatoCampo.Decimal:  Decimal();  break;
                case FormatoCampo.Entero:   Entero();   break;
            }

            return this;
        }

        private void TopePreventivo(int max)
        {
            if (_campo.Control is TextBox tb && tb.MaxLength == 0)
                tb.MaxLength = max;
            else if (_campo.Control is PasswordBox pb && pb.MaxLength == 0)
                pb.MaxLength = max;
        }

        /// <summary>Regla a medida, para lo que no entra en las de arriba.</summary>
        public ConstructorCampo Regla(Func<string?, bool> esValido, string mensaje)
        {
            _campo.Reglas.Add((c => esValido(c.LeerTexto()), mensaje));
            return this;
        }

        /// <summary>
        /// La regla solo aplica si <paramref name="condicion"/> se cumple al
        /// momento de validar. Sirve para lo que solo vale al crear (la contraseña
        /// de un usuario nuevo, por ejemplo).
        /// </summary>
        public ConstructorCampo SoloSi(Func<bool> condicion)
        {
            if (_campo.Reglas.Count == 0) return this;

            var (esValido, mensaje) = _campo.Reglas[^1];
            _campo.Reglas[^1] = (c => !condicion() || esValido(c), mensaje);
            return this;
        }

        // Continuación de la cadena.
        public ConstructorCampo Campo(TextBox caja, string etiqueta) => _validador.Campo(caja, etiqueta);
        public ConstructorCampo Clave(PasswordBox caja, string etiqueta) => _validador.Clave(caja, etiqueta);
        public ConstructorCampo Combo(Selector combo, string etiqueta) => _validador.Combo(combo, etiqueta);

        public ValidadorFormulario ValidarAlSalirDelCampo() => _validador.ValidarAlSalirDelCampo();
        public ValidadorFormulario Listo() => _validador;
    }
}
