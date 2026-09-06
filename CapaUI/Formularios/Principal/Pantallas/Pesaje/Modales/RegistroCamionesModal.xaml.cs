using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CapaAplicacion.Common.Catalogos;
using CapaDominio.Reglas;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaUI.Core.Validacion;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>Un camión listo para darse de alta, tal como quedó en la tabla.</summary>
    public record CamionRegistrado(string Placa, string Proveedor, int IdProveedor, string Descripcion);

    /// <summary>
    /// Alta de <b>varios</b> camiones de una sola vez — el "proceso de descarga".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reemplaza a <see cref="CamionModal"/> <b>solo en el alta</b>: en el andén los
    /// camiones llegan juntos y darlos de alta de a uno significaba abrir y cerrar el
    /// mismo modal cinco veces. Editar un camión ya registrado sigue siendo
    /// <see cref="CamionModal"/>, que es donde tiene sentido el formulario de a uno.
    /// </para>
    /// <para>
    /// La tabla tiene <see cref="PesajeViewModel.MaxCamiones"/> filas fijas y las filas
    /// se "ocupan", no se crean: así el número de cada fila nunca cambia y las etiquetas
    /// que el validador capturó al construirse ("La placa del camión 3…") siguen siendo
    /// ciertas después de quitar una fila del medio.
    /// </para>
    /// <para>
    /// <b>Validación:</b> cada fila arma su propio <see cref="ValidadorFormulario"/> con
    /// las reglas de <see cref="ReglasCamion"/> — los largos máximos salen de las
    /// columnas de <c>movimientos</c>, no de esta pantalla. El validador además pone el
    /// <c>MaxLength</c> del TextBox (tope preventivo), así que el operador no puede
    /// siquiera escribir de más.
    /// </para>
    /// </remarks>
    public partial class RegistroCamionesModal : UserControl
    {
        private readonly ICatalogoRepository _catalogos;

        /// <summary>
        /// Recepciones abiertas. Sirven para dos cosas: avisar que una placa ya está
        /// abierta con otro proveedor (no bloquea) y rechazar el duplicado exacto
        /// placa+proveedor (sí bloquea). No es <c>readonly</c> porque un guardado que se
        /// corta a mitad deja recepciones nuevas que este modal tiene que ver — ver
        /// <see cref="AplicarGuardadoParcial"/>.
        /// </summary>
        private IReadOnlyList<CamionPesaje> _camionesAbiertos;

        /// <summary>Placas distintas ya abiertas — lo que consume cupo (ver <c>PesajeViewModel.PlacasAbiertas</c>).</summary>
        private IReadOnlyCollection<string> _placasAbiertas;

        private readonly ObservableCollection<FilaCamion> _filas = new();

        private SelectorCatalogoModal? _selectorCatalogo;
        private FilaCamion? _filaDelSelector;

        /// <summary>
        /// Tamaño propio del modal, leído del XAML. Mientras el selector de catálogo está
        /// abierto el marco crece y al cerrarlo vuelve acá — mismo mecanismo que
        /// <see cref="CamionModal"/>.
        /// </summary>
        private readonly double _anchoPropio;
        private readonly double _altoPropio;

        public event Action? Cerrado;
        public event Action<IReadOnlyList<CamionRegistrado>>? Confirmado;

        public RegistroCamionesModal(IReadOnlyList<CamionPesaje> camionesAbiertos)
        {
            InitializeComponent();

            _camionesAbiertos = camionesAbiertos;
            _placasAbiertas   = PlacasDe(camionesAbiertos);

            _catalogos   = App.Services.GetRequiredService<ICatalogoRepository>();
            _anchoPropio = Width;
            _altoPropio  = Height;

            for (int i = 1; i <= PesajeViewModel.MaxCamiones; i++)
                _filas.Add(new FilaCamion(i) { EsAlterna = i % 2 == 0 });

            _filas[0].Activa  = true;   // el modal abre con una fila lista para escribir
            FilasHost.ItemsSource = _filas;

            TxtInstruccion.Text =
                $"Registrá los camiones que llegan en este proceso. Podés cargar hasta " +
                $"{PesajeViewModel.MaxCamiones}; la placa y el proveedor son obligatorios y cada " +
                "camión se pesa por separado.";

            ActualizarContadores();
        }

        private int Ocupadas => _filas.Count(f => f.Activa);

        private static string Normalizar(CamionPesaje c) => (c.Placa ?? "").Trim().ToUpperInvariant();

        private static IReadOnlyCollection<string> PlacasDe(IEnumerable<CamionPesaje> camiones) => camiones
            .Select(Normalizar)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // ── Alta y baja de filas ────────────────────────────────────────────────

        private void AgregarFila_Click(object sender, RoutedEventArgs e)
        {
            var libre = _filas.FirstOrDefault(f => !f.Activa);
            if (libre is null) return;

            libre.Activa = true;
            ActualizarContadores();

            // Diferido: la fila recién se hace visible con el DataTrigger, y Focus() sobre un
            // elemento que todavía no pasó por el layout (IsVisible en false) devuelve false
            // sin avisar — el cursor quedaba en el botón y había que clickear la placa.
            Dispatcher.BeginInvoke(new Action(() => libre.CajaPlaca?.Focus()),
                                   System.Windows.Threading.DispatcherPriority.Input);
        }

        private void QuitarFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.DataContext is not FilaCamion fila) return;

            // Nunca se queda sin ninguna fila: la última se vacía pero sigue ocupada, si no
            // la tabla quedaría en blanco y habría que tocar "Registrar otro camión" para
            // volver a escribir.
            if (Ocupadas <= 1) fila.Limpiar();
            else               CompactarDesde(fila);

            LimpiarMarcasDeError();
            RevisarPlacas();
            ActualizarContadores();
        }

        /// <summary>
        /// Saca una fila compactando hacia arriba: los valores de las filas de abajo suben
        /// un lugar y se libera la última ocupada. Así no quedan huecos en el medio de la
        /// tabla ("camión 1, camión 3") y los números siguen leyéndose como el orden en que
        /// llegaron los camiones.
        /// <para/>
        /// Se mueven los <b>valores</b>, no las filas: cada <see cref="FilaCamion"/> conserva
        /// su número y su validador, que capturó ese número en las etiquetas al construirse.
        /// </summary>
        private void CompactarDesde(FilaCamion fila)
        {
            int desde = _filas.IndexOf(fila);
            for (int i = desde; i < _filas.Count - 1; i++)
                _filas[i].CopiarDe(_filas[i + 1]);

            // La ÚLTIMA fila de la tabla, no la última ocupada.
            //
            // El bucle de arriba ya deja bien el estado "ocupada" de cada fila, porque
            // CopiarDe también copia Activa: al borrar el camión 3 de tres, la fila 3
            // copia de la 4 (libre) y queda libre sola. Preguntar después cuál es la
            // última ocupada devolvía entonces la fila 2 —que es un camión que el
            // operador sí quería— y la borraba de yapa: se tocaba un basurero y
            // desaparecían dos camiones.
            //
            // La fila que hay que vaciar es siempre la del final: es la única que el
            // bucle no alcanza a pisar (no tiene una fila siguiente de donde copiar).
            var ultima = _filas[^1];
            ultima.Limpiar();
            ultima.Activa = false;
        }

        /// <summary>
        /// Los valores se movieron de fila: las marcas rojas y los renglones de error
        /// quedaron apuntando al camión equivocado.
        /// </summary>
        private void LimpiarMarcasDeError()
        {
            foreach (var f in _filas) f.Validador?.Limpiar();
        }

        /// <summary>
        /// El lote se guardó a medias. Las primeras <paramref name="guardadas"/> filas ya
        /// existen en la base (se persisten en orden), así que salen de la tabla, y la
        /// lista de recepciones abiertas se refresca: sin eso, volver a tocar "Guardar"
        /// no vería como duplicadas las que acaban de entrar y las crearía de nuevo.
        /// </summary>
        public void AplicarGuardadoParcial(int guardadas, IReadOnlyList<CamionPesaje> camionesAbiertos)
        {
            _camionesAbiertos = camionesAbiertos;
            _placasAbiertas   = PlacasDe(camionesAbiertos);

            for (int i = 0; i < guardadas && Ocupadas > 0; i++)
                CompactarDesde(_filas[0]);

            // Acá sí puede quedar la tabla vacía (se guardaron todas menos la que falló):
            // se reabre una fila para que haya dónde corregir.
            if (Ocupadas == 0) _filas[0].Activa = true;

            LimpiarMarcasDeError();
            RevisarPlacas();
            ActualizarContadores();
        }

        // ── Selector de catálogo (Proveedor) ────────────────────────────────────
        // El selector reemplaza todo el contenido del modal mientras está abierto (es
        // "chromeless": hereda este marco, no tiene fondo ni tamaño propio), y el marco
        // crece para que la tabla del buscador entre completa.

        private void BuscarProveedor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.DataContext is not FilaCamion fila) return;

            _filaDelSelector = fila;

            var selector = new SelectorCatalogoModal(Catalogos.Proveedores(_catalogos));
            selector.Cerrado += CerrarSelectorCatalogo;
            // NO se cierra el selector acá: lo cierra él mismo (evento Cerrado) apenas
            // termina de emitir. Cerrarlo desde este handler lo dispone a mitad de su
            // propio bucle de emisión y la excepción que sale de ahí se lleva la app
            // puesta — mismo cuidado que en CamionModal.
            selector.Seleccionado += item =>
            {
                var destino = _filaDelSelector;
                if (destino is null) return;

                destino.IdProveedor = item.Id ?? 0;
                destino.Proveedor   = item.Nombre;
                if (destino.CajaProveedor is not null) destino.CajaProveedor.Text = item.Nombre;

                // El aviso depende del proveedor: la misma placa con el MISMO proveedor es
                // un duplicado; con otro, es una recepción aparte.
                RevisarPlacas();
            };

            _selectorCatalogo               = selector;
            CatalogoSelectorHost.Content    = selector;
            CatalogoSelectorHost.Visibility = Visibility.Visible;
            ContenidoPrincipal.Visibility   = Visibility.Collapsed;
            AplicarMarcoSelector(true);
        }

        private void CerrarSelectorCatalogo()
        {
            _selectorCatalogo?.Dispose();
            _selectorCatalogo               = null;
            _filaDelSelector                = null;
            CatalogoSelectorHost.Content    = null;
            CatalogoSelectorHost.Visibility = Visibility.Collapsed;
            ContenidoPrincipal.Visibility   = Visibility.Visible;
            AplicarMarcoSelector(false);
        }

        /// <summary>
        /// Marco grande mientras se ve la tabla del buscador, propio cuando se ve el
        /// formulario. Los MaxWidth/MaxHeight del XAML lo siguen acotando contra la ventana.
        /// </summary>
        private void AplicarMarcoSelector(bool abierto)
        {
            Width  = abierto ? 880 : _anchoPropio;
            Height = abierto ? 780 : _altoPropio;
        }

        private void CajaProveedor_DobleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            BuscarProveedor_Click(sender, new RoutedEventArgs());
        }

        private void CajaProveedor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            e.Handled = true;
            BuscarProveedor_Click(sender, new RoutedEventArgs());
        }

        // ── Enganche de los campos con su fila ──────────────────────────────────
        // Las cajas nacen dentro del DataTemplate, así que el code-behind no las ve por
        // nombre: cada una se presenta al cargarse y la fila arma su validador cuando ya
        // tiene las tres.

        private void CajaPlaca_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaPlaca = caja);

        private void CajaProveedor_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaProveedor = caja);

        private void CajaDescripcion_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaDescripcion = caja);

        private static void Enganchar(object sender, Action<FilaCamion, TextBox> asignar)
        {
            if (sender is not TextBox caja || caja.DataContext is not FilaCamion fila) return;
            asignar(fila, caja);
            fila.ArmarValidadorSiEstaCompleto();
        }

        // ── Reacciones a cambios ────────────────────────────────────────────────

        private void Placa_Changed(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox caja) return;

            // Las placas se guardan y se comparan en mayúsculas; hacerlo al escribir evita
            // que "hab 4821" y "HAB 4821" parezcan dos camiones distintos.
            int caret = caja.CaretIndex;
            string arriba = caja.Text.ToUpperInvariant();
            if (caja.Text != arriba)
            {
                caja.Text = arriba;
                caja.CaretIndex = caret;
            }

            if (caja.DataContext is FilaCamion fila) fila.Placa = arriba;
            RevisarPlacas();
            ActualizarContadores();
        }

        private void Descripcion_Changed(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox caja && caja.DataContext is FilaCamion fila)
                fila.Descripcion = caja.Text;
        }

        /// <summary>
        /// Mientras se escribe, el TextBox sigue el cursor: con texto largo eso deja
        /// visible la COLA, no el inicio. Al salir del campo se vuelve a ver desde el
        /// principio — que es además el estado en el que aparece la vista previa
        /// recortada con "…" (el TextBlock superpuesto en el XAML), así que conviene
        /// que las dos cosas cambien juntas al perder el foco.
        /// </summary>
        private void Descripcion_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox caja) return;
            caja.CaretIndex = 0;
            caja.ScrollToHome();
        }

        /// <summary>
        /// Avisa —sin bloquear— que alguna placa de la tabla ya está abierta con OTRO
        /// proveedor. No es un error: un camión que trae carga de dos proveedores se
        /// registra como dos recepciones, una por proveedor. El aviso está para que no
        /// parezca un duplicado por equivocación. Mismo criterio que en CamionModal.
        /// </summary>
        private void RevisarPlacas()
        {
            var avisos = new List<string>();

            foreach (var fila in _filas.Where(f => f.Activa && f.PlacaNormalizada.Length > 0))
            {
                var otra = _camionesAbiertos.FirstOrDefault(c =>
                    Normalizar(c) == fila.PlacaNormalizada && c.IdProveedor != fila.IdProveedor);

                if (otra is not null)
                    avisos.Add($"La placa {fila.PlacaNormalizada} ya está abierta con {otra.Proveedor}.");
            }

            if (avisos.Count == 0) { PanelAviso.Visibility = Visibility.Collapsed; return; }

            TxtAviso.Text = string.Join(" ", avisos.Distinct()) +
                            " Se registrará una recepción aparte para el proveedor que elijas.";
            PanelAviso.Visibility = Visibility.Visible;
        }

        private void ActualizarContadores()
        {
            int ocupadas = Ocupadas;

            TxtContador.Text = $"{ocupadas} de {PesajeViewModel.MaxCamiones} camiones";
            TxtPie.Text      = $"{ocupadas} camión(es) · máximo {PesajeViewModel.MaxCamiones}";

            BtnAgregarFila.IsEnabled = ocupadas < PesajeViewModel.MaxCamiones;

            // El cupo del andén se cuenta por placa distinta, no por fila: dos filas con la
            // misma placa (un camión con carga de dos proveedores) ocupan un solo lugar.
            int cupo = PesajeViewModel.MaxCamiones - _placasAbiertas.Count;
            int placasNuevas = PlacasNuevas().Count;

            if (placasNuevas > cupo)
                MostrarError($"Ya hay {_placasAbiertas.Count} camión(es) en el andén y solo quedan " +
                             $"{Math.Max(0, cupo)} lugar(es) libres; estás registrando {placasNuevas} placas distintas.");
            else
                PanelError.Visibility = Visibility.Collapsed;
        }

        /// <summary>Placas de la tabla que todavía no están abiertas — las que consumen cupo.</summary>
        private List<string> PlacasNuevas() => _filas
            .Where(f => f.Activa && f.PlacaNormalizada.Length > 0)
            .Select(f => f.PlacaNormalizada)
            .Distinct(StringComparer.Ordinal)
            .Where(p => !_placasAbiertas.Contains(p))
            .ToList();

        // ── Guardado ────────────────────────────────────────────────────────────

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            var activas = _filas.Where(f => f.Activa).ToList();
            if (activas.Count == 0) { MostrarError("Registrá al menos un camión."); return; }

            // Reglas de campo, fila por fila. La primera que falla se queda con el foco y
            // su mensaje — el validador ya se encarga de eso.
            foreach (var fila in activas)
                if (fila.Validador is not null && !fila.Validador.Validar()) return;

            if (!SinDuplicados(activas)) return;

            int cupo = PesajeViewModel.MaxCamiones - _placasAbiertas.Count;
            if (PlacasNuevas().Count > cupo)
            {
                ActualizarContadores();   // ya redacta el mensaje exacto
                return;
            }

            PanelError.Visibility = Visibility.Collapsed;

            Confirmado?.Invoke(activas
                .Select(f => new CamionRegistrado(f.PlacaNormalizada, f.Proveedor, f.IdProveedor!.Value, f.Descripcion.Trim()))
                .ToList());
        }

        /// <summary>
        /// Rechaza el mismo camión dos veces. "El mismo" es placa + proveedor: la misma
        /// placa con proveedores distintos son dos recepciones legítimas, pero repetir el
        /// par crearía dos <c>movimientos</c> gemelos que nadie sabría distinguir después.
        /// </summary>
        private bool SinDuplicados(IReadOnlyList<FilaCamion> activas)
        {
            var vistas = new HashSet<(string, int)>();

            foreach (var fila in activas)
            {
                var clave = (fila.PlacaNormalizada, fila.IdProveedor!.Value);

                if (!vistas.Add(clave))
                {
                    MostrarError($"El camión {fila.Numero} repite la placa {fila.PlacaNormalizada} " +
                                 $"con el mismo proveedor ({fila.Proveedor}). Quitá la fila repetida " +
                                 "o cambiale el proveedor.");
                    fila.CajaPlaca?.Focus();
                    return false;
                }

                if (_camionesAbiertos.Any(c => Normalizar(c) == fila.PlacaNormalizada && c.IdProveedor == fila.IdProveedor))
                {
                    MostrarError($"La placa {fila.PlacaNormalizada} ya tiene una recepción abierta con " +
                                 $"{fila.Proveedor}. Agregale los productos a esa recepción en vez de " +
                                 "registrarla de nuevo.");
                    fila.CajaPlaca?.Focus();
                    return false;
                }
            }

            return true;
        }

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            if (_selectorCatalogo is not null) { CerrarSelectorCatalogo(); return; }
            Cerrado?.Invoke();
        }

        // ── Fila de la tabla ────────────────────────────────────────────────────

        /// <summary>
        /// Una fila de la tabla. Existe siempre (las cinco se crean al abrir el modal);
        /// <see cref="Activa"/> decide si se ve como camión o como "espacio libre".
        /// </summary>
        public partial class FilaCamion : ObservableObject
        {
            public FilaCamion(int numero) => Numero = numero;

            public int Numero { get; }

            /// <summary>Fila de índice par: fondo gris clarito, para leer la tabla en zigzag.</summary>
            public bool EsAlterna { get; init; }

            [ObservableProperty] private bool _activa;

            public string  Placa       { get; set; } = "";
            public string  Proveedor   { get; set; } = "";
            public int?    IdProveedor { get; set; }
            public string  Descripcion { get; set; } = "";

            public string PlacaNormalizada => Placa.Trim().ToUpperInvariant();

            public TextBox? CajaPlaca       { get; set; }
            public TextBox? CajaProveedor   { get; set; }
            public TextBox? CajaDescripcion { get; set; }

            public ValidadorFormulario? Validador { get; private set; }

            /// <summary>
            /// Arma el validador de la fila en cuanto las tres cajas se presentaron. Se
            /// hace una sola vez: <c>ValidarAlSalirDelCampo()</c> suscribe handlers, y
            /// rearmarlo sobre las mismas cajas los duplicaría.
            /// </summary>
            public void ArmarValidadorSiEstaCompleto()
            {
                if (Validador is not null) return;
                if (CajaPlaca is null || CajaProveedor is null || CajaDescripcion is null) return;

                // La etiqueta lleva el número de fila: en una tabla de cinco camiones,
                // "La placa es obligatoria" no dice cuál.
                Validador = ValidadorFormulario.Nuevo()
                    .Campo(CajaPlaca, $"La placa del camión {Numero}").Segun(ReglasCamion.Placa)
                    .Catalogo(CajaProveedor, $"El proveedor del camión {Numero}", () => IdProveedor.HasValue)
                        .Segun(ReglasCamion.Proveedor)
                    .Campo(CajaDescripcion, $"La descripción del camión {Numero}").Segun(ReglasCamion.Descripcion)
                    .ValidarAlSalirDelCampo();
            }

            public void CopiarDe(FilaCamion otra)
            {
                Activa      = otra.Activa;
                Placa       = otra.Placa;
                Proveedor   = otra.Proveedor;
                IdProveedor = otra.IdProveedor;
                Descripcion = otra.Descripcion;

                if (CajaPlaca       is not null) CajaPlaca.Text       = otra.Placa;
                if (CajaProveedor   is not null) CajaProveedor.Text   = otra.Proveedor;
                if (CajaDescripcion is not null) CajaDescripcion.Text = otra.Descripcion;
            }

            public void Limpiar()
            {
                Placa       = "";
                Proveedor   = "";
                IdProveedor = null;
                Descripcion = "";

                if (CajaPlaca       is not null) CajaPlaca.Text       = "";
                if (CajaProveedor   is not null) CajaProveedor.Text   = "";
                if (CajaDescripcion is not null) CajaDescripcion.Text = "";
            }
        }
    }
}
