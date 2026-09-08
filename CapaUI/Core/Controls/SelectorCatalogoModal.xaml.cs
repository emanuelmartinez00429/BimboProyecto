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
    /// Ids que alguna vez pasaron por el checkbox en esta apertura del modal
    /// (tildado y después destildado incluido). Existe solo para el atajo de
    /// Enter de <see cref="Confirmar"/> en modo múltiple (P-044): sin esto,
    /// destildar una fila deja igual su <c>Dg.SelectedItem</c> (clickear el
    /// checkbox también selecciona la fila) y el atajo la emitiría de nuevo
    /// como si el usuario jamás la hubiera tocado — el mismo riesgo que el
    /// comentario original de <see cref="Dg_DoubleClick"/> ya advertía para el
    /// doble clic. El atajo solo debe correr sobre una fila que el operador
    /// jamás marcó, no sobre una que marcó y se arrepintió.
    /// </summary>
    private readonly HashSet<int> _idsTocados = new();

    /// <summary>
    /// Vive lo que vive el modal. Aparte de <c>_cts</c>, que se recrea en cada
    /// tecla del buscador: acá cuelga la revalidación de fondo, que tiene que
    /// abortarse al cerrar la lupa y no cuando el usuario sigue escribiendo.
    /// </summary>
    private readonly CancellationTokenSource _ctsVida = new();

    public SelectorCatalogoModal(CatalogoConfig cfg)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        if (cfg.PageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(cfg), "El tamaño de página debe ser mayor que cero.");

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

        // Reparto de ancho. Por defecto Nombre es la columna flexible ("*") y la
        // de descripción va a su ancho natural — correcto cuando esa columna es
        // un dato corto (RTN en proveedores, código en productos...). Si la
        // descripción es una frase (presentaciones, categorías) se invierte:
        // Nombre se ajusta a su contenido y Descripción se queda con el sobrante,
        // recortando con "…" (lo hace CatCellMuted vía TextTrimming).
        if (cfg.DescripcionExtensa)
        {
            ColNombre.Width      = DataGridLength.Auto;
            ColNombre.MinWidth   = 140;
            ColNombre.MaxWidth   = 320;   // un nombre largo no puede ahogar a la descripción
            ColDescripcion.Width    = new DataGridLength(1, DataGridLengthUnitType.Star);
            ColDescripcion.MinWidth = 220;
            ColDescripcion.MaxWidth = double.PositiveInfinity;
        }

        if (cfg.PermiteMultiple)
            TxtPie.Text = "Marcá los que necesites y tocá Seleccionar.";

        RefrescarEstadoMarcas();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (_dispuesto) return;

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

        // Algunos hosts (Reportería) necesitan el mismo contrato visible de las
        // grillas administrativas: 50 registros por página y navegación incluso
        // cuando el catálogo entraría completo en memoria. Los demás consumidores
        // conservan el modo adaptativo y su caché de apertura inmediata.
        if (_cfg.ForzarPaginacion)
        {
            FooterPaginacion.Visibility = Visibility.Visible;
            await CargarPaginaAsync();
            return;
        }

        // La caché vive ahora detrás del repositorio (ADR-026): si el catálogo ya
        // está en memoria, esto no toca la red. Antes se revalidaba en cada apertura,
        // así que un acierto de caché pagaba el viaje igual.
        var r = await _cfg.Cargar(string.Empty, 1, UmbralMemoria, _ctsVida.Token);

        if (_dispuesto) return;

        if (!r.Success)
        {
            MostrarCargando(false);
            MostrarError(r.Error);
            return;
        }

        var pagina = r.Value!;
        if (pagina.Total <= UmbralMemoria)
        {
            PintarEnMemoria(pagina.Items);
            return;
        }

        // Excede el umbral: paginación server-side.
        FooterPaginacion.Visibility = Visibility.Visible;
        await CargarPaginaAsync();
    }

    // ── Modo memoria ──────────────────────────────────────────────────────────

    /// <summary>
    /// Vuelca la lista completa a la tabla: filtrado en memoria, sin paginación
    /// ni red por tecla. El texto buscado se conserva solo — <see cref="FiltroEnMemoria"/>
    /// lee el campo _query.
    ///
    /// <para>Se llama una sola vez, al abrir. El repintado en caliente de ADR-015
    /// desapareció junto con <c>alRevalidar</c>: la caché ahora se invalida por
    /// evento de Realtime antes de que la lupa se abra, así que no hay nada que
    /// corregir con el modal a la vista. De paso se va el parpadeo del DataGrid y
    /// la carrera en la que el repintado desmarcaba la fila recién elegida.</para>
    /// </summary>
    private void PintarEnMemoria(IReadOnlyList<FiltroItem> items)
    {
        _todas = items.Select(CrearFila).ToList();
        _vista = CollectionViewSource.GetDefaultView(_todas);
        _vista.Filter = FiltroEnMemoria;

        Dg.ItemsSource = _vista;
        FooterPaginacion.Visibility = Visibility.Collapsed;

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

        var r = await _cfg.Cargar(_query, _page, _cfg.PageSize, CancellationToken.None);

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

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_total / (double)_cfg.PageSize));

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

            // P-044: Soporte para barra espaciadora en la tabla.
            // Si el foco está en la búsqueda, Espacio escribe en el texto normalmente.
            // Si el foco está en un botón, activa el botón por el camino nativo de WPF.
            // Si el foco está en la tabla:
            // - En modo múltiple: conmuta el estado de marcado de la fila (checkbox).
            // - En modo simple: confirma y emite la fila seleccionada.
            case Key.Space when !SearchBox.IsKeyboardFocusWithin && !EsBoton(Keyboard.FocusedElement):
                if (Dg.IsKeyboardFocusWithin && Dg.SelectedItem is FilaCatalogo filaEspacio && !filaEspacio.YaElegido)
                {
                    if (_cfg.PermiteMultiple)
                    {
                        ToggleMarcado(filaEspacio);
                    }
                    else
                    {
                        Confirmar();
                    }
                    e.Handled = true;
                }
                break;

            // Ojo con el alcance: esto es PreviewKeyDown en el UserControl, así que
            // ve el Enter de CUALQUIER hijo, botones incluidos. Antes marcaba
            // Handled siempre, y eso se tragaba el Enter cuando el foco estaba
            // sobre "Elegir" o "Cerrar" — el botón nunca recibía su Click y solo
            // respondía a Espacio. Ahora solo se marca Handled cuando el selector
            // realmente hizo algo; si el foco está en un botón, Enter sigue de
            // largo y lo activa por el camino normal de WPF.
            case Key.Enter when !EsBoton(Keyboard.FocusedElement):
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
    /// ¿El foco actual está sobre algún botón del selector (pie o paginación)?
    /// Si lo está, teclas como Enter o Espacio le pertenecen al botón nativo.
    /// </summary>
    private static bool EsBoton(IInputElement? foco)
    {
        if (foco is DependencyObject dep)
        {
            DependencyObject? actual = dep;
            while (actual != null)
            {
                if (actual is Button) return true;
                if (actual is Visual or System.Windows.Media.Media3D.Visual3D)
                {
                    actual = VisualTreeHelper.GetParent(actual) ?? LogicalTreeHelper.GetParent(actual);
                }
                else
                {
                    actual = LogicalTreeHelper.GetParent(actual);
                }
            }
        }
        return false;
    }

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
            _idsTocados.Add(id);
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
        _idsTocados.Clear();

        // Destildar lo que está en pantalla: FilaCatalogo notifica Marcado, así
        // que los CheckBox visibles se apagan solos.
        foreach (var f in FilasEnPantalla()) f.Marcado = false;

        RefrescarEstadoMarcas();
    }

    /// <summary>
    /// Conmuta el estado de marcado de una fila en modo selección múltiple,
    /// actualizando el acumulador interno y refrescando la UI.
    /// </summary>
    private void ToggleMarcado(FilaCatalogo fila)
    {
        if (fila.YaElegido) return;

        fila.Marcado = !fila.Marcado;
        if (fila.Item.Id is int id)
        {
            _idsTocados.Add(id);
            if (fila.Marcado) _marcados[id] = fila.Item;
            else           _marcados.Remove(id);
        }

        RefrescarEstadoMarcas();
    }

    private void Dg_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Dg.SelectedItem is not FilaCatalogo fila || fila.YaElegido) return;

        if (_cfg.PermiteMultiple)
        {
            // En modo múltiple el doble clic no confirma nada — solo tilda o
            // destilda el checkbox de esa fila, como si el usuario le hubiera
            // clickeado directo.
            ToggleMarcado(fila);
            return;
        }

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

            if (elegidos.Count > 0)
            {
                Emitir(elegidos);
                return;
            }

            // P-044 atajo rápido por teclado: si el operador presiona Enter sobre una fila
            // que jamás tocó (ni tildó ni destildó) se emite esa fila directamente.
            // Exige "nunca tocada" y no solo "sin marcar ahora": clickear el checkbox
            // también selecciona la fila (Dg.SelectedItem), así que tildarla y
            // arrepentirse (destildarla) la dejaría igual de "seleccionada pero sin
            // marcar" — el atajo la emitiría por error pese a que el usuario ya dijo
            // que no la quería. Mismo riesgo que ya evita Dg_DoubleClick.
            if (Dg.SelectedItem is FilaCatalogo filaActual && !filaActual.YaElegido
                && filaActual.Item.Id is int idActual && !_idsTocados.Contains(idActual))
            {
                Emitir(new[] { filaActual.Item });
                return;
            }

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

        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

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
