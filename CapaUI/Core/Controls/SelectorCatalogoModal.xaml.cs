using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CapaAplicacion.Productos.Dtos;
using CapaUI.Core.Catalogos;

namespace CapaUI.Core.Controls;

/// <summary>
/// Selector genérico de catálogo. Reutilizable para cualquier campo de tipo
/// "elegir un registro de una tabla": lo único que cambia es el
/// <see cref="CatalogoConfig"/> que se le pasa.
///
/// Contrato igual al resto de los modales del proyecto: se hospeda en un overlay
/// del formulario padre y devuelve el resultado por eventos.
///
/// <para><b>Estrategia automática.</b> No hay flag que mantener: pide la primera
/// página con tamaño <see cref="UmbralMemoria"/> y, según el total que vuelva,
/// se queda filtrando en memoria (catálogos chicos, búsqueda instantánea) o pasa
/// a paginar contra el servidor. Si un catálogo crece y cruza el umbral, el
/// cambio ocurre solo.</para>
/// </summary>
public partial class SelectorCatalogoModal : UserControl, IDisposable
{
    /// <summary>Hasta este total, el catálogo entra entero en memoria.</summary>
    private const int UmbralMemoria = 200;

    /// <summary>Tamaño de página cuando se supera el umbral.</summary>
    private const int PageSize = 15;

    private const int DebounceMs = 300;

    public event Action? Cerrado;
    public event Action<FiltroItem>? Seleccionado;

    private readonly CatalogoConfig _cfg;

    // Modo memoria: lista completa + vista filtrable. Null ⇒ modo servidor.
    private List<FilaCatalogo>? _todas;
    private ICollectionView?    _vista;

    // Modo servidor.
    private int _page  = 1;
    private int _total = 0;
    private CancellationTokenSource? _cts;
    private int _generacion;

    private string _query = string.Empty;
    private bool   _dispuesto;

    /// <summary>
    /// Lo tildado en esta apertura, indexado por Id. Es el dueño de la verdad:
    /// las filas (<see cref="FilaCatalogo"/>) se destruyen y se recrean en cada
    /// refiltrado, cambio de página y repintado por revalidación, así que la
    /// selección del usuario no puede vivir en ellas.
    ///
    /// Diccionario y no HashSet: al confirmar hace falta el FiltroItem, y en
    /// modo servidor la fila de otra página ya no existe para darlo. Clave int
    /// y no la referencia: cada página construye FiltroItem nuevos, así que la
    /// identidad por referencia se rompe justo donde más falta hace.
    /// </summary>
    private readonly Dictionary<int, FiltroItem> _marcados = new();

    /// <summary>
    /// Vive lo que vive el modal. Aparte de <c>_cts</c>, que se recrea en cada
    /// tecla del buscador: acá cuelga la revalidación de fondo, que tiene que
    /// abortarse al cerrar la lupa y no cuando el usuario sigue escribiendo.
    /// </summary>
    private readonly CancellationTokenSource _ctsVida = new();

    public SelectorCatalogoModal(CatalogoConfig cfg)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        InitializeComponent();

        TxtTitulo.Text          = cfg.Titulo;
        SearchBox.Placeholder   = cfg.Placeholder;
        // Mismo campo genérico (FiltroItem.Descripcion), significado distinto
        // por catálogo (RTN en Proveedores, código en Productos...) — el
        // encabezado tiene que decir qué es de verdad, no quedar en
        // "Descripción" fijo. Mayúsculas para seguir la misma convención que
        // el resto de los headers de esta tabla (NOMBRE, ESTADO).
        ColDescripcion.Header    = cfg.TituloDescripcion.ToUpperInvariant();
        ColDescripcion.Visibility = cfg.MostrarDescripcion ? Visibility.Visible : Visibility.Collapsed;
        ColEstado.Visibility      = cfg.MostrarEstado      ? Visibility.Visible : Visibility.Collapsed;

        // Productos: el código es lo que se escanea/reconoce primero. El
        // resto de los catálogos deja Nombre adelante (orden declarado en el XAML).
        if (cfg.DescripcionPrimero)
            Dg.Columns.Move(Dg.Columns.IndexOf(ColDescripcion), Dg.Columns.IndexOf(ColNombre));

        if (cfg.PermiteMultiple)
            TxtPie.Text = "Marcá los que necesites y tocá Seleccionar.";

        RefrescarEstadoMarcas();
        Loaded += OnLoaded;
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        DependencyPropertyDescriptor
            .FromProperty(SuggestionSearchBox.QueryProperty, typeof(SuggestionSearchBox))
            .AddValueChanged(SearchBox, OnQueryChanged);

        await CargarInicialAsync();

        // Cursor en el buscador al abrir: se puede tipear, o bajar con ↓ directo.
        SearchBox.EnfocarCaja();
    }

    private async Task CargarInicialAsync()
    {
        MostrarCargando(true);

        // El callback repinta si la revalidación encontró la tabla cambiada: la
        // lupa abre con lo que ya estaba en memoria y se corrige sola en el acto.
        var r = await CatalogoCache.ObtenerCompletoAsync(
            _cfg, UmbralMemoria,
            alRevalidar: lista => { if (!_dispuesto) PintarEnMemoria(lista, preservarSeleccion: true); },
            _ctsVida.Token);

        if (_dispuesto) return;

        if (!r.Success)
        {
            MostrarCargando(false);
            MostrarError(r.Error);
            return;
        }

        if (r.Value is { } completo)
        {
            PintarEnMemoria(completo, preservarSeleccion: false);
            return;
        }

        // Excede el umbral: paginación server-side.
        FooterPaginacion.Visibility = Visibility.Visible;
        await CargarPaginaAsync();
    }

    // ── Modo memoria ──────────────────────────────────────────────────────────

    /// <summary>
    /// Vuelca la lista completa a la tabla: filtrado en memoria, sin paginación
    /// ni red por tecla. Se llama al abrir y otra vez si la revalidación trajo
    /// cambios, por eso <paramref name="preservarSeleccion"/>: en el repintado
    /// hay que devolverle al usuario la fila que tenía marcada. El texto buscado
    /// se conserva solo — <see cref="FiltroEnMemoria"/> lee el campo _query.
    /// </summary>
    private void PintarEnMemoria(IReadOnlyList<FiltroItem> items, bool preservarSeleccion)
    {
        int? idMarcado = preservarSeleccion
            ? (Dg.SelectedItem as FilaCatalogo)?.Item.Id
            : null;

        _todas = items.Select(CrearFila).ToList();
        _vista = CollectionViewSource.GetDefaultView(_todas);
        _vista.Filter = FiltroEnMemoria;

        Dg.ItemsSource = _vista;
        FooterPaginacion.Visibility = Visibility.Collapsed;

        if (idMarcado is not null)
            Dg.SelectedItem = _todas.FirstOrDefault(f => f.Item.Id == idMarcado);

        MostrarCargando(false);
        RefrescarConteoEnMemoria();
    }

    private bool FiltroEnMemoria(object obj)
    {
        if (string.IsNullOrWhiteSpace(_query)) return true;
        if (obj is not FilaCatalogo f) return false;

        return f.Nombre.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || f.Descripcion.Contains(_query, StringComparison.OrdinalIgnoreCase);
    }

    // ── Modo servidor ─────────────────────────────────────────────────────────

    private async Task CargarPaginaAsync()
    {
        int gen = ++_generacion;
        MostrarCargando(true);

        var r = await _cfg.Cargar(_query, _page, PageSize, CancellationToken.None);

        // Llegó tarde: ya hay otra carga más nueva en curso.
        if (_dispuesto || gen != _generacion) return;

        MostrarCargando(false);

        if (!r.Success) { MostrarError(r.Error); return; }

        var pagina = r.Value!;
        _total = pagina.Total;

        Dg.ItemsSource = pagina.Items.Select(CrearFila).ToList();
        ActualizarVacio(pagina.Items.Count > 0);
        ActualizarContador(pagina.Items.Count, _total);
        RefrescarPaginacion();
    }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_total / (double)PageSize));

    private void RefrescarPaginacion()
    {
        int total = TotalPages;

        TxtPageInfo.Text = $"Página {_page} de {total} · {_total} registros";

        BtnPrimera.IsEnabled   = _page > 1;
        BtnAnterior.IsEnabled  = _page > 1;
        BtnSiguiente.IsEnabled = _page < total;
        BtnUltima.IsEnabled    = _page < total;

        PaginacionPanel.Items.Clear();
        foreach (int p in Paginacion.Calcular(_page, total))
        {
            if (p == Paginacion.Elipsis)
            {
                PaginacionPanel.Items.Add(new TextBlock
                {
                    Text              = "…",
                    FontFamily        = new FontFamily("Segoe UI"),
                    FontSize          = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(3, 0, 3, 0),
                    Foreground        = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
                });
                continue;
            }

            var btn = new Button
            {
                Content = p.ToString(),
                Margin  = new Thickness(3, 0, 0, 0),
                Style   = (Style)FindResource(p == _page ? "CatPageBtnActivo" : "CatPageBtn"),
                Tag     = p,
            };
            btn.Click += async (s, _) =>
            {
                if (s is Button b && b.Tag is int pg && pg != _page)
                {
                    _page = pg;
                    await CargarPaginaAsync();
                }
            };
            PaginacionPanel.Items.Add(btn);
        }
    }

    // ── Búsqueda ──────────────────────────────────────────────────────────────

    private async void OnQueryChanged(object? sender, EventArgs e)
    {
        _query = SearchBox.Query?.Trim() ?? string.Empty;

        if (_vista is not null)
        {
            // Memoria: refresco inmediato, sin red ni debounce.
            _vista.Refresh();
            RefrescarConteoEnMemoria();
            return;
        }

        // Servidor: debounce para no disparar una consulta por tecla.
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Task.Delay(DebounceMs, token);
            if (token.IsCancellationRequested || _dispuesto) return;

            _page = 1;
            await CargarPaginaAsync();
        }
        catch (OperationCanceledException) { /* siguió escribiendo */ }
    }

    // ── Interacción ───────────────────────────────────────────────────────────

    /// <summary>
    /// Navegación con flechas entre el buscador y la tabla.
    ///
    /// Va en PreviewKeyDown del UserControl (tunneling: baja de la raíz a la
    /// hoja) para llegar antes que el TextBox del buscador, que se queda con
    /// Escape. Down/Up/Enter sí pasan porque SuggestionSearchBox los ignora
    /// cuando no tiene sugerencias, que es como se usa acá.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Cerrado?.Invoke();
                e.Handled = true;
                break;

            // Del buscador a la tabla.
            case Key.Down when SearchBox.IsKeyboardFocusWithin:
                BajarALaTabla();
                e.Handled = true;
                break;

            // De la primera fila, de vuelta al buscador.
            case Key.Up when Dg.IsKeyboardFocusWithin && Dg.SelectedIndex <= 0:
                SearchBox.EnfocarCaja();
                e.Handled = true;
                break;

            // Ojo con el alcance: esto es PreviewKeyDown en el UserControl, así que
            // ve el Enter de CUALQUIER hijo, botones incluidos. Antes marcaba
            // Handled siempre, y eso se tragaba el Enter cuando el foco estaba
            // sobre "Elegir" o "Cerrar" — el botón nunca recibía su Click y solo
            // respondía a Espacio. Ahora solo se marca Handled cuando el selector
            // realmente hizo algo; si el foco está en un botón, Enter sigue de
            // largo y lo activa por el camino normal de WPF.
            case Key.Enter when !EsBotonDelPie(Keyboard.FocusedElement):
                if (Dg.SelectedItem is FilaCatalogo)
                {
                    Confirmar();
                    e.Handled = true;
                }
                else if (SearchBox.IsKeyboardFocusWithin)
                {
                    BajarALaTabla();
                    e.Handled = true;
                }
                break;
        }
    }

    /// <summary>
    /// ¿El foco está sobre un botón del pie del selector? Si lo está, Enter le
    /// pertenece al botón y el manejo global de Enter no debe interceptarlo.
    /// </summary>
    private bool EsBotonDelPie(IInputElement? foco) =>
        foco is Button b && (b == BtnElegir || b == BtnCerrar);

    /// <summary>Marca la primera fila (o la ya marcada) y le pasa el foco a la celda.</summary>
    private void BajarALaTabla()
    {
        if (Dg.Items.Count == 0) return;

        if (Dg.SelectedIndex < 0) Dg.SelectedIndex = 0;
        Dg.ScrollIntoView(Dg.Items[Dg.SelectedIndex]);

        // Con virtualización el contenedor puede no existir todavía.
        Dg.UpdateLayout();

        var contenedor = Dg.ItemContainerGenerator
            .ContainerFromIndex(Dg.SelectedIndex) as DataGridRow;

        if (contenedor != null)
        {
            // Enfocar la primera celda de la fila para que ↑/↓ funcionen
            // dentro del DataGrid sin necesitar un segundo Down.
            Dg.CurrentCell = new DataGridCellInfo(Dg.Items[Dg.SelectedIndex],
                                                  Dg.Columns[0]);
            contenedor.MoveFocus(
                new TraversalRequest(FocusNavigationDirection.First));
        }
        else
        {
            Dg.Focus();
        }
    }

    /// <summary>
    /// Siembra el tilde desde <see cref="_marcados"/>: una fila recreada (otra
    /// página, refiltrado, repintado por revalidación) vuelve marcada como estaba.
    /// </summary>
    private FilaCatalogo CrearFila(FiltroItem item) =>
        new(item,
            _cfg.PermiteMultiple,
            _cfg.EstaYaElegido?.Invoke(item.Id) ?? false,
            marcado: item.Id is int id && _marcados.ContainsKey(id));

    /// <summary>Filas materializadas ahora mismo (memoria filtrada o página actual).</summary>
    private IEnumerable<FilaCatalogo> FilasEnPantalla() =>
        Dg.ItemsSource?.Cast<object>().OfType<FilaCatalogo>() ?? Enumerable.Empty<FilaCatalogo>();

    private void Dg_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefrescarEstadoMarcas();

    /// <summary>
    /// El checkbox de una fila cambió. Lee el estado del propio CheckBox y no el
    /// de la fila: así el acumulador queda correcto sin depender de que el
    /// write-back del binding haya corrido antes que el evento.
    /// </summary>
    private void Marcado_Changed(object sender, RoutedEventArgs e)
    {
        // Id nulo entonces fila no marcable. Hoy solo Productos usa PermiteMultiple
        // y siempre trae id, pero el tipo lo permite y el acumulador necesita clave.
        if (sender is CheckBox cb && cb.DataContext is FilaCatalogo f && f.Item.Id is int id)
        {
            f.Marcado = cb.IsChecked == true;
            if (f.Marcado) _marcados[id] = f.Item;
            else           _marcados.Remove(id);
        }

        RefrescarEstadoMarcas();
    }

    /// <summary>Botón Elegir, contador y "Limpiar marcas", en un solo lugar.</summary>
    private void RefrescarEstadoMarcas()
    {
        int marcados = _marcados.Count;

        bool haySeleccionSimple = Dg.SelectedItem is FilaCatalogo f && !f.YaElegido;
        BtnElegir.IsEnabled = marcados > 0 || (!_cfg.PermiteMultiple && haySeleccionSimple);

        // Las marcas sobreviven al filtro y a la paginación, así que puede haber
        // N marcados y 0 visibles: sin el conteo el usuario no sabe qué va a agregar.
        var visible = _cfg.PermiteMultiple && marcados > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtMarcados.Visibility = visible;
        BtnLimpiar.Visibility  = visible;
        TxtMarcados.Text = $"{marcados} marcado{(marcados == 1 ? "" : "s")}";
    }

    private void Limpiar_Click(object sender, RoutedEventArgs e)
    {
        _marcados.Clear();

        // Destildar lo que está en pantalla: FilaCatalogo notifica Marcado, así
        // que los CheckBox visibles se apagan solos.
        foreach (var f in FilasEnPantalla()) f.Marcado = false;

        RefrescarEstadoMarcas();
    }

    private void Dg_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // En modo múltiple el doble clic no hace nada: la única forma de
        // confirmar es el botón, para que un doble clic accidental no descarte
        // en silencio lo que el usuario venía marcando.
        if (_cfg.PermiteMultiple) return;

        if (Dg.SelectedItem is FilaCatalogo fila && !fila.YaElegido)
            Emitir(new[] { fila.Item });
    }

    private void Elegir_Click(object sender, RoutedEventArgs e) => Confirmar();

    private void Confirmar()
    {
        if (_cfg.PermiteMultiple)
        {
            // EstaYaElegido se re-evalúa acá: el YaElegido de la fila es un
            // snapshot de cuando se creó y puede haber quedado viejo.
            var elegidos = _marcados.Values
                .Where(i => !(_cfg.EstaYaElegido?.Invoke(i.Id) ?? false))
                .ToList();

            if (elegidos.Count > 0) Emitir(elegidos);

            // Sin marcas no se emite nada: en modo multiple la fila resaltada
            // NO cuenta como elegida (clickear un checkbox tambien selecciona
            // su fila, asi que caer al camino simple agregaria justo la ultima
            // que se toco). Mismo criterio que el doble clic.
            return;
        }

        if (Dg.SelectedItem is FilaCatalogo fila && !fila.YaElegido)
            Emitir(new[] { fila.Item });
    }

    /// <summary>
    /// Emite lo elegido y recién ahí cierra. El cierre lo dispara el selector y
    /// no el host: con selección múltiple hay N invocaciones de Seleccionado y el
    /// host no tiene forma de saber cuál es la última — si cierra en la primera,
    /// el resto del bucle corre sobre un control ya dispuesto.
    /// </summary>
    private void Emitir(IEnumerable<FiltroItem> items)
    {
        foreach (var item in items) Seleccionado?.Invoke(item);
        Cerrado?.Invoke();
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

    private async void Primera_Click(object sender, RoutedEventArgs e)   { _page = 1;                       await CargarPaginaAsync(); }
    private async void Anterior_Click(object sender, RoutedEventArgs e)  { _page = Math.Max(1, _page - 1);  await CargarPaginaAsync(); }
    private async void Siguiente_Click(object sender, RoutedEventArgs e) { _page = Math.Min(TotalPages, _page + 1); await CargarPaginaAsync(); }
    private async void Ultima_Click(object sender, RoutedEventArgs e)    { _page = TotalPages;              await CargarPaginaAsync(); }

    // ── Estado visual ─────────────────────────────────────────────────────────

    private void MostrarCargando(bool cargando)
    {
        PanelCargando.Visibility = cargando ? Visibility.Visible : Visibility.Collapsed;
        if (cargando)
        {
            TxtVacio.Visibility = Visibility.Collapsed;
            TxtError.Visibility = Visibility.Collapsed;
        }
    }

    private void ActualizarVacio(bool hayFilas) =>
        TxtVacio.Visibility = hayFilas ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Vacío + contador para el modo memoria, en un solo lugar.</summary>
    private void RefrescarConteoEnMemoria()
    {
        int visibles = _vista?.Cast<object>().Count() ?? 0;
        ActualizarVacio(visibles > 0);
        ActualizarContador(visibles, _todas?.Count ?? 0);
    }

    /// <summary>"Mostrando N de M" — N es lo visible tras filtrar, M el total.</summary>
    private void ActualizarContador(int mostrados, int total)
    {
        TxtContador.Text = mostrados == total
            ? $"Mostrando {total} registro{(total == 1 ? "" : "s")}"
            : $"Mostrando {mostrados} de {total} registros";
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text       = mensaje;
        TxtError.Visibility = Visibility.Visible;
        TxtVacio.Visibility = Visibility.Collapsed;
    }

    // ── Limpieza ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_dispuesto) return;
        _dispuesto = true;

        DependencyPropertyDescriptor
            .FromProperty(SuggestionSearchBox.QueryProperty, typeof(SuggestionSearchBox))
            .RemoveValueChanged(SearchBox, OnQueryChanged);

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _ctsVida.Cancel();
        _ctsVida.Dispose();
    }

    /// <summary>
    /// Proyección de fila. Envuelve al DTO para no meterle texto de presentación
    /// (mismo criterio que tenía <c>ProductoSeleccionable</c> en el viejo selector de pesaje, ya retirado).
    /// </summary>
    private sealed class FilaCatalogo : INotifyPropertyChanged
    {
        public FilaCatalogo(FiltroItem item, bool permiteMultiple, bool yaElegido, bool marcado)
        {
            Item            = item;
            PermiteMultiple = permiteMultiple;
            YaElegido       = yaElegido;
            _marcado        = marcado;
        }

        public FiltroItem Item { get; }

        /// <summary>Copia por fila de <see cref="CatalogoConfig.PermiteMultiple"/> — la
        /// plantilla de la celda indicadora la usa para decidir círculo vs. checkbox.</summary>
        public bool PermiteMultiple { get; }

        /// <summary>Ya elegido en otro lado (p. ej. ya está en la carga): fila bloqueada.</summary>
        public bool YaElegido { get; }

        private bool _marcado;

        /// <summary>
        /// Tildado en esta apertura del selector (modo múltiple). Notifica porque
        /// "Limpiar marcas" lo apaga desde afuera y los CheckBox visibles tienen
        /// que reflejarlo; el resto de las propiedades son de solo lectura.
        /// </summary>
        public bool Marcado
        {
            get => _marcado;
            set
            {
                if (_marcado == value) return;
                _marcado = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Marcado)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Nombre      => Item.Nombre;
        public string Descripcion => Item.Descripcion;

        public string EstadoTexto => Item.Activo switch
        {
            true  => "Activo",
            false => "Inactivo",
            null  => string.Empty,
        };
    }
}
