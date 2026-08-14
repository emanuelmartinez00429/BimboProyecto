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

    public SelectorCatalogoModal(CatalogoConfig cfg)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        InitializeComponent();

        TxtTitulo.Text          = cfg.Titulo;
        SearchBox.Placeholder   = cfg.Placeholder;
        ColDescripcion.Visibility = cfg.MostrarDescripcion ? Visibility.Visible : Visibility.Collapsed;
        ColEstado.Visibility      = cfg.MostrarEstado      ? Visibility.Visible : Visibility.Collapsed;

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

        var r = await CatalogoCache.ObtenerCompletoAsync(_cfg, UmbralMemoria);
        if (_dispuesto) return;

        if (!r.Success)
        {
            MostrarCargando(false);
            MostrarError(r.Error);
            return;
        }

        if (r.Value is { } completo)
        {
            // Cabe entero: filtrado en memoria, sin paginación ni red por tecla.
            _todas = completo.Select(i => new FilaCatalogo(i)).ToList();
            _vista = CollectionViewSource.GetDefaultView(_todas);
            _vista.Filter = FiltroEnMemoria;

            Dg.ItemsSource = _vista;
            FooterPaginacion.Visibility = Visibility.Collapsed;
            MostrarCargando(false);
            RefrescarConteoEnMemoria();
            return;
        }

        // Excede el umbral: paginación server-side.
        FooterPaginacion.Visibility = Visibility.Visible;
        await CargarPaginaAsync();
    }

    // ── Modo memoria ──────────────────────────────────────────────────────────

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

        Dg.ItemsSource = pagina.Items.Select(i => new FilaCatalogo(i)).ToList();
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

            case Key.Enter:
                if (Dg.SelectedItem is FilaCatalogo) Confirmar();
                else if (SearchBox.IsKeyboardFocusWithin) BajarALaTabla();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Marca la primera fila (o la ya marcada) y le pasa el foco.</summary>
    private void BajarALaTabla()
    {
        if (Dg.Items.Count == 0) return;

        if (Dg.SelectedIndex < 0) Dg.SelectedIndex = 0;
        Dg.ScrollIntoView(Dg.Items[Dg.SelectedIndex]);

        // Con virtualización el contenedor puede no existir todavía.
        Dg.UpdateLayout();
        if (Dg.ItemContainerGenerator.ContainerFromIndex(Dg.SelectedIndex) is DataGridRow fila)
            fila.Focus();
        else
            Dg.Focus();
    }

    private void Dg_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        BtnElegir.IsEnabled = Dg.SelectedItem is FilaCatalogo;

    private void Dg_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Confirmar();

    private void Elegir_Click(object sender, RoutedEventArgs e) => Confirmar();

    private void Confirmar()
    {
        if (Dg.SelectedItem is FilaCatalogo fila)
            Seleccionado?.Invoke(fila.Item);
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
    }

    /// <summary>
    /// Proyección de fila. Envuelve al DTO para no meterle texto de presentación
    /// (mismo criterio que <c>ProductoSeleccionable</c> en el selector de pesaje).
    /// </summary>
    private sealed class FilaCatalogo
    {
        public FilaCatalogo(FiltroItem item) => Item = item;

        public FiltroItem Item { get; }

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
