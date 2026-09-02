using System.Collections.ObjectModel;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using static CapaAplicacion.Common.EstadoRegistro;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Conexion;
using CapaAplicacion.Realtime;
using CapaAplicacion.Common.Catalogos;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using CapaUI.Core.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Productos;

public enum EstadoFilter { Activos, Inactivos, Todos }

/// <summary>
/// ViewModel del formulario de Productos.
/// Hereda de RealtimeAwareViewModel: la suscripción a "productos" se cancela
/// automáticamente al Dispose(), sin posibilidad de olvidar la baja.
/// </summary>
public partial class ProductosViewModel : RealtimeAwareViewModel
{
    private readonly IProductoRepository _repo;
    private readonly ICatalogoRepository _catalogos;
    private readonly SuggestionDebouncer _buscador = new();

    /// <summary>Catálogo completo; <see cref="Fabricantes"/> es su vista acotada por proveedor.</summary>
    private List<FiltroItem> _todosFabricantes = new();

    private string        _query             = "";
    private EstadoFilter  _estadoFiltro      = EstadoFilter.Activos;
    private int?          _fabricanteIdFiltro;
    private int?          _paisIdFiltro;
    private int?          _proveedorIdFiltro;
    private int?          _categoriaIdFiltro;
    private OrdenProducto _orden             = OrdenProducto.IdAsc;
    private int          _page               = 1;
    private int          _filteredCount;
    private int?         _pendingSelectionId;
    private int          _loadGeneration;

    /// <summary>
    /// Cancela la carga de página en vuelo. <see cref="_loadGeneration"/> descarta la
    /// respuesta vieja cuando llega, pero no evita que llegue: sin esto la petición
    /// seguía viaje al servidor aunque nadie fuera a usarla.
    /// </summary>
    private CancellationTokenSource? _ctsPagina;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<ProductoDto> _pageRows = new();

    /// <summary>
    /// Sugerencias ya mapeadas para el <c>SuggestionSearchBox</c>, que se bindea
    /// directo a esta propiedad. <c>null</c> o lista vacía = popup cerrado.
    /// Es una sola señal a propósito: la versión anterior tenía dos
    /// (<c>Suggestions</c> + <c>ShowSuggestions</c>) y el popup se congelaba
    /// cuando la segunda no cambiaba de valor.
    /// </summary>
    [ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    private ProductoDto? _seleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    [NotifyPropertyChangedFor(nameof(NoResults), nameof(MensajeSinResultados))]
    private bool _isLoading;
    [ObservableProperty] private int              _highlightIndex = -1;
    [ObservableProperty] private int              _totalCount;
    [ObservableProperty] private int              _activosCount;
    [ObservableProperty] private int              _inactivosCount;
    [ObservableProperty] private List<FiltroItem> _fabricantes = new();
    [ObservableProperty] private List<FiltroItem> _paises      = new();
    [ObservableProperty] private List<FiltroItem> _proveedores = new();
    [ObservableProperty] private List<FiltroItem> _categorias  = new();
    [ObservableProperty] private string           _errorCarga  = "";

    public bool   HaySeleccionado   => Seleccionado is not null;
    public string TextoSeleccionado => Seleccionado is null
        ? ""
        : $"{Seleccionado.CodigoInterno} · {Seleccionado.Nombre}";

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0;

    public string MensajeSinResultados
    {
        get
        {
            if (IsLoading) return string.Empty;

            if (_estadoFiltro == EstadoFilter.Inactivos)
                return "No hay registros inactivos";

            if (_estadoFiltro == EstadoFilter.Activos && ActivosCount == 0 && TotalCount > 0)
                return "No hay registros activos";

            if (TieneFiltrosBusquedaActivos())
                return "No se encontraron resultados con los filtros actuales";

            return "No hay registros";
        }
    }

    private bool TieneFiltrosBusquedaActivos() =>
        !string.IsNullOrWhiteSpace(_query)
        || _fabricanteIdFiltro.HasValue
        || _paisIdFiltro.HasValue
        || _proveedorIdFiltro.HasValue
        || _categoriaIdFiltro.HasValue;

    public string PageInfo
    {
        get
        {
            if (_filteredCount == 0) return "Sin resultados";
            int from = (_page - 1) * PageSize + 1;
            int to   = Math.Min(_page * PageSize, _filteredCount);
            return $"Mostrando {from}–{to} de {_filteredCount} productos";
        }
    }

    public string Query
    {
        get => _query;
        set
        {
            if (_query == value) return;
            _query = value;
            OnPropertyChanged();
            _ = RefrescarSugerenciasAsync();
        }
    }

    public EstadoFilter EstadoFiltro
    {
        get => _estadoFiltro;
        set
        {
            if (_estadoFiltro == value) return;
            _estadoFiltro = value;
            OnPropertyChanged();
            _page = 1;
            _ = CargarPaginaAsync();
        }
    }

    public int? FabricanteIdFiltro
    {
        get => _fabricanteIdFiltro;
        set
        {
            if (_fabricanteIdFiltro == value) return;
            _fabricanteIdFiltro = value;
            OnPropertyChanged();
            _page = 1;
            _ = CargarPaginaAsync();
        }
    }

    public int? PaisIdFiltro
    {
        get => _paisIdFiltro;
        set
        {
            if (_paisIdFiltro == value) return;
            _paisIdFiltro = value;
            OnPropertyChanged();
            _page = 1;
            _ = CargarPaginaAsync();
        }
    }

    public int? CategoriaIdFiltro
    {
        get => _categoriaIdFiltro;
        set
        {
            if (_categoriaIdFiltro == value) return;
            _categoriaIdFiltro = value;
            OnPropertyChanged();
            _page = 1;
            _ = CargarPaginaAsync();
        }
    }

    /// <summary>
    /// Proveedor. Encadena con Fabricante: la vista reacota el combo de
    /// fabricantes cuando esto cambia.
    /// </summary>
    public int? ProveedorIdFiltro
    {
        get => _proveedorIdFiltro;
        set
        {
            if (_proveedorIdFiltro == value) return;
            _proveedorIdFiltro = value;
            OnPropertyChanged();
            ReacotarFabricantes();
            _page = 1;
            _ = CargarPaginaAsync();
        }
    }

    public OrdenProducto Orden
    {
        get => _orden;
        set
        {
            if (_orden == value) return;
            _orden = value;
            OnPropertyChanged();
            _page = 1;
            _ = CargarPaginaAsync();
        }
    }

    public int Page
    {
        get => _page;
        set
        {
            if (_page == value) return;
            _page = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(TotalPages));
            NotifyPaginationCanExecuteChanged();
            _ = CargarPaginaAsync();
        }
    }

    public event Action?              SolicitarNuevo;
    public event Action<ProductoDto>? SolicitarEditar;
    public event Action?              FiltrosLimpiados;

    public ProductosViewModel(IProductoRepository repo, ICatalogoRepository catalogos,
                              IRealtimeService realtime, IConexionMonitor conexionMonitor)
        : base(realtime, conexionMonitor)
    {
        _repo      = repo;
        _catalogos = catalogos;
    }

    public async Task CargarDatosAsync()
    {
        IsLoading  = true;
        ErrorCarga = string.Empty;

        // Catálogos desde la caché de sesión: la segunda entrada a la pantalla no
        // espera a la red, y la revalidación de fondo corrige el combo si alguien
        // tocó la tabla.
        //
        // El repoblado se saltea cuando ese filtro está en uso: ComboFiltro.Poblar
        // vuelve a "(Todos)" sin notificar, así que repoblar por detrás dejaría el
        // combo diciendo "(Todos)" con la grilla todavía filtrada. La lista nueva
        // igual queda cacheada y entra en el próximo poblado.
        var fabTask  = CatalogoCache.ObtenerParaComboAsync(
            Catalogos.Fabricantes(_catalogos),
            alRevalidar: lista =>
            {
                if (Disposed || _fabricanteIdFiltro is not null) return;
                _todosFabricantes = lista.ToList();
                ReacotarFabricantes();
            });

        var paisTask = CatalogoCache.ObtenerParaComboAsync(
            Catalogos.Paises(_catalogos),
            alRevalidar: lista =>
            {
                if (Disposed || _paisIdFiltro is not null) return;
                Paises = lista.ToList();
            });

        var provTask = CatalogoCache.ObtenerParaComboAsync(
            Catalogos.Proveedores(_catalogos),
            alRevalidar: lista =>
            {
                if (Disposed || _proveedorIdFiltro is not null) return;
                Proveedores = lista.ToList();
            });

        var catTask = CatalogoCache.ObtenerParaComboAsync(
            Catalogos.Categorias(_catalogos),
            alRevalidar: lista =>
            {
                if (Disposed || _categoriaIdFiltro is not null) return;
                Categorias = lista.ToList();
            });
        await Task.WhenAll(fabTask, paisTask, provTask, catTask);

        var rFab = fabTask.Result;
        if (!rFab.Success) { ErrorCarga = rFab.Error; IsLoading = false; return; }
        _todosFabricantes = rFab.Value!.ToList();
        Fabricantes = _todosFabricantes;

        var rPaises = paisTask.Result;
        if (!rPaises.Success) { ErrorCarga = rPaises.Error; IsLoading = false; return; }
        Paises = rPaises.Value!.ToList();

        var rProv = provTask.Result;
        if (!rProv.Success) { ErrorCarga = rProv.Error; IsLoading = false; return; }
        Proveedores = rProv.Value!.ToList();

        var rCat = catTask.Result;
        if (!rCat.Success) { ErrorCarga = rCat.Error; IsLoading = false; return; }
        Categorias = rCat.Value!.ToList();

        await CargarPaginaAsync();

        // Observar() registra el token de baja — se cancela en Dispose() automáticamente
        Observar("productos", OnCambioProducto);

        foreach (var (tabla, clave) in TablasDeJoin)
            Observar(tabla, _ => OnCambioCatalogo(clave));
    }

    /// <summary>
    /// Tablas que aportan columnas a la grilla vía join, con la clave de caché de
    /// su catálogo. Esos nombres no viven en `productos`: renombrar un fabricante
    /// no dispara ningún evento de esa tabla, así que sin estas suscripciones la
    /// columna se queda con el nombre viejo hasta volver a entrar a la pantalla.
    /// </summary>
    private static readonly (string Tabla, string ClaveCache)[] TablasDeJoin =
    {
        ("fabricante",            "fabricantes"),
        ("proveedores",           "proveedores"),
        ("categoria",             "categorias"),
        ("paises",                "paises"),
        ("presentacion_producto", "presentaciones"),
        ("tara",                  "taras"),
        ("unidad_medida",         "unidades"),
    };

    /// <summary>
    /// Cambió un catálogo del join: la lista cacheada queda vieja (y con ella la
    /// cascada proveedor → fabricante) y la grilla muestra el nombre anterior.
    /// El refresco va por la vía silenciosa, y su bandera interna colapsa la
    /// ráfaga si un mismo cambio emite varios eventos seguidos.
    /// </summary>
    private void OnCambioCatalogo(string claveCache)
    {
        if (Disposed) return;

        CatalogoCache.Invalidar(claveCache);
        _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: false);
    }

    /// <summary>
    /// Fabricantes visibles según el proveedor elegido. Filtrado en memoria
    /// sobre la lista ya cacheada: cambiar de proveedor no consulta nada.
    /// </summary>
    private void ReacotarFabricantes()
    {
        Fabricantes = _proveedorIdFiltro is null
            ? _todosFabricantes
            : _todosFabricantes.Where(f => f.IdPadre == _proveedorIdFiltro).ToList();

        // Un fabricante que ya no pertenece al proveedor dejaría un filtro
        // imposible (cero filas sin explicación): se limpia.
        if (_fabricanteIdFiltro is not null &&
            !Fabricantes.Any(f => f.Id == _fabricanteIdFiltro))
        {
            _fabricanteIdFiltro = null;
            OnPropertyChanged(nameof(FabricanteIdFiltro));
        }
    }

    public void RefrescarDatos() => _ = CargarPaginaAsync();

    /// <summary>
    /// Refresco despues de guardar en el modal. Va por la via silenciosa a
    /// proposito: <see cref="CargarPaginaAsync"/> levanta IsLoading y la grilla
    /// se vacia y vuelve, que es el "parpadeo de recarga" que se ve al guardar.
    /// Aca las filas viejas siguen en pantalla y se reemplazan recien cuando
    /// llegan las nuevas.
    /// </summary>
    public void RefrescarTrasGuardar() =>
        _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: false);

    // Al volver la conexión, recarga todo (Observar es idempotente → no duplica suscripción).
    protected override Task OnReconexionAsync() => CargarDatosAsync();

    private const int TimeoutMs = 10_000;

    /// <summary>
    /// El timeout cancela la petición de verdad en vez de solo dejar de esperarla.
    /// <para/>
    /// Antes era <c>Task.WhenAny(task, Task.Delay(TimeoutMs))</c>: la UI se rendía a
    /// los 10s pero el pedido seguía vivo hasta terminar, y cada carga dejaba además
    /// un <c>Task.Delay</c> huérfano corriendo. Pasar páginas rápido acumulaba
    /// peticiones y timers que ya no le importaban a nadie.
    /// <para/>
    /// <see cref="_loadGeneration"/> se mantiene: una respuesta puede llegar en el
    /// hueco entre que se pide la cancelación y que efectivamente corta, y esa hay
    /// que descartarla igual.
    /// </summary>
    private async Task CargarPaginaAsync()
    {
        int myGen  = ++_loadGeneration;
        IsLoading  = true;
        ErrorCarga = string.Empty;

        // La generación nueva mata a la anterior de verdad, no solo la ignora.
        var ctsAnterior = _ctsPagina;
        var cts = new CancellationTokenSource(TimeoutMs);
        _ctsPagina = cts;
        ctsAnterior?.Cancel();
        ctsAnterior?.Dispose();

        var filtros = BuildFiltros();

        Result<PagedResult<ProductoDto>> r;
        try
        {
            r = await _repo.GetPagedAsync(_page, PageSize, filtros, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Dos motivos posibles, y se tratan distinto: si la canceló una carga
            // más nueva, esa se encarga de la UI y acá no hay nada que decir; si
            // venció el timeout, hay que avisar.
            if (myGen != _loadGeneration) return;
            ErrorCarga = "La carga tardó demasiado. Intente de nuevo.";
            IsLoading  = false;
            return;
        }

        if (myGen != _loadGeneration) return;

        if (!r.Success)
        {
            ErrorCarga = r.Error;
            IsLoading  = false;
            return;
        }

        var pagina = r.Value!;

        TotalCount     = pagina.Total;
        ActivosCount   = pagina.Activos;
        InactivosCount = pagina.Inactivos;
        _filteredCount = ResolverFilteredCount(pagina, filtros);

        PageRows = new ObservableCollection<ProductoDto>(pagina.Items);

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        OnPropertyChanged(nameof(MensajeSinResultados));
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;

        if (_pendingSelectionId.HasValue)
        {
            Seleccionado        = PageRows.FirstOrDefault(x => x.Id == _pendingSelectionId.Value);
            _pendingSelectionId = null;
        }
    }

    private Task RefrescarSugerenciasAsync() => _buscador.EjecutarAsync(
        _query,
        async (q, ct) =>
        {
            var r = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), ct);
            return r.Success ? r.Value!.Select(Map).ToList() : null;
        },
        items => { SuggestItems = items; HighlightIndex = -1; });

    private static SuggestionItemData Map(ProductoDto p) => new()
    {
        Codigo = p.CodigoInterno,
        Nombre = p.Nombre,
        Meta   = $"{p.Fabricante} · {p.Pais} · {p.Categoria}",
        Activo = p.IdEstado == Activo,
        Source = p,
    };

    public void SeleccionarSugerencia(ProductoDto p)
    {
        // Cancelar el debounce en vuelo: como se asigna al campo _query y no a la
        // propiedad, no se pasa por el setter y nadie mas cancelaria la busqueda. Sin
        // esto, una busqueda en curso termina despues de la seleccion y reabre el popup.
        _buscador.Cancelar();

        _query = "";
        OnPropertyChanged(nameof(Query));
        SuggestItems = null;

        var enPagina = PageRows.FirstOrDefault(x => x.Id == p.Id);
        if (enPagina is not null) { Seleccionado = enPagina; return; }

        _pendingSelectionId = p.Id;
        _ = NavegarAPaginaDeProductoAsync(p);
    }

    private async Task NavegarAPaginaDeProductoAsync(ProductoDto producto)
    {
        var r = await _repo.GetPaginaDeProductoAsync(producto, PageSize, BuildFiltros());
        if (!r.Success) { _pendingSelectionId = null; ErrorCarga = r.Error; return; }

        _page = r.Value;
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(TotalPages));
        NotifyPaginationCanExecuteChanged();
        await CargarPaginaAsync();
    }

    private ProductoFiltros BuildFiltros() => new()
    {
        IdEstado = _estadoFiltro switch
        {
            EstadoFilter.Activos    => Activo,
            EstadoFilter.Inactivos => Inactivo,
            _                           => null
        },
        IdFabricante = _fabricanteIdFiltro,
        IdPais       = _paisIdFiltro,
        IdProveedor  = _proveedorIdFiltro,
        IdCategoria  = _categoriaIdFiltro,
        Orden        = _orden,
    };

    [RelayCommand]
    private void Nuevo()
    {
        if (SesionPermisos.Tiene(Permiso.CrearProducto)) SolicitarNuevo?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private void Editar()
    {
        if (Seleccionado is not null && SesionPermisos.Tiene(Permiso.ModificarProducto)) SolicitarEditar?.Invoke(Seleccionado);
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        _estadoFiltro       = EstadoFilter.Activos;
        _fabricanteIdFiltro = null;
        _paisIdFiltro       = null;
        _proveedorIdFiltro  = null;
        _categoriaIdFiltro  = null;
        _orden              = OrdenProducto.IdAsc;
        _page = 1;

        // Los campos se pisan directo arriba, sin pasar por el setter de
        // ProveedorIdFiltro — así que ReacotarFabricantes() nunca corre solo.
        // Sin este llamado, Fabricantes se queda con la lista angosta del
        // proveedor que estaba filtrado (p. ej. solo "Canasa"), aunque el combo
        // ya muestre "(Todos)": la selección visual se limpia pero la lista de
        // opciones detrás sigue acotada al proveedor anterior.
        ReacotarFabricantes();

        FiltrosLimpiados?.Invoke();
        _ = CargarPaginaAsync();
    }

    [RelayCommand(CanExecute = nameof(PuedePaginaAnterior))]
    private void PrimeraPagina() => Page = 1;

    [RelayCommand(CanExecute = nameof(PuedePaginaAnterior))]
    private void PaginaAnterior() => Page--;

    [RelayCommand(CanExecute = nameof(PuedePaginaSiguiente))]
    private void PaginaSiguiente() => Page++;

    [RelayCommand(CanExecute = nameof(PuedePaginaSiguiente))]
    private void UltimaPagina() => Page = TotalPages;

    private bool PuedePaginaAnterior()  => !IsLoading && _page > 1;
    private bool PuedePaginaSiguiente() => !IsLoading && _page < TotalPages;

    private void NotifyPaginationCanExecuteChanged()
    {
        PrimeraPaginaCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        UltimaPaginaCommand.NotifyCanExecuteChanged();
    }

    private static int ResolverFilteredCount(PagedResult<ProductoDto> pagina, ProductoFiltros filtros) =>
        filtros.IdEstado switch
        {
            Activo   => pagina.Activos,
            Inactivo => pagina.Inactivos,
            _        => pagina.Total
        };

    // ── Realtime handler ────────────────────────────────────────────

    private void OnCambioProducto(CambioRealtime cambio)
    {
        if (Disposed) return;   // guard post-dispose (Disposed viene de RealtimeAwareViewModel)

        bool afectaPaginaActual = cambio.IdRegistro.HasValue
            && PageRows.Any(p => p.Id == cambio.IdRegistro.Value);

        if (string.Equals(cambio.Operacion, "INSERT", StringComparison.OrdinalIgnoreCase))
        {
            _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: true);
        }
        else if (string.Equals(cambio.Operacion, "UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            if (afectaPaginaActual || !cambio.IdRegistro.HasValue)
                _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: false);
            else
                _ = RefrescarConteosAsync();
        }
    }

    /// <summary>
    /// Guarda contra refrescos silenciosos superpuestos. Al guardar en el modal
    /// se dispara uno, y el eco de Realtime de ese mismo cambio llega mientras
    /// todavia esta en vuelo: sin esta bandera se pagaba dos veces la misma
    /// consulta. Descartar el eco es seguro porque la consulta en vuelo arranco
    /// despues de la escritura, asi que ya trae el cambio.
    /// </summary>
    private bool _refrescoSilencioso;

    private async Task CargarPaginaSilenciosamenteAsync(bool actualizarFilas, bool esInsert)
    {
        try
        {
            if (IsLoading || _refrescoSilencioso) return;
            _refrescoSilencioso = true;

            int genCapturada = _loadGeneration;
            var filtros = BuildFiltros();
            Result<PagedResult<ProductoDto>> r;

            try
            {
                r = await _repo.GetPagedAsync(_page, PageSize, filtros);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ProductosVM: silent refresh — excepción en request");
                return;
            }

            if (_loadGeneration != genCapturada) return;
            if (!r.Success)
            {
                Serilog.Log.Warning("ProductosVM: silent refresh — error: {Error}", r.Error);
                return;
            }

            var pagina = r.Value!;
            int nuevoFilteredCount = ResolverFilteredCount(pagina, filtros);
            int nuevoTotalPages    = Math.Max(1, (int)Math.Ceiling(nuevoFilteredCount / (double)PageSize));

            bool debeActualizarFilas = actualizarFilas
                && (!esInsert || _page == nuevoTotalPages);

            TotalCount     = pagina.Total;
            ActivosCount   = pagina.Activos;
            InactivosCount = pagina.Inactivos;
            _filteredCount = nuevoFilteredCount;

            if (debeActualizarFilas)
            {
                int? idSeleccionadoAntes = Seleccionado?.Id;
                PageRows = new ObservableCollection<ProductoDto>(pagina.Items);
                if (idSeleccionadoAntes.HasValue)
                    Seleccionado = PageRows.FirstOrDefault(x => x.Id == idSeleccionadoAntes.Value);
            }

            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(NoResults));
            OnPropertyChanged(nameof(MensajeSinResultados));
            NotifyPaginationCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "ProductosVM: error inesperado en silent refresh");
        }
        finally
        {
            _refrescoSilencioso = false;
        }
    }

    private async Task RefrescarConteosAsync()
    {
        var filtros = BuildFiltros();
        var r = await _repo.GetPagedAsync(_page, PageSize, filtros);
        if (!r.Success) return;

        var pagina     = r.Value!;
        TotalCount     = pagina.Total;
        ActivosCount   = pagina.Activos;
        InactivosCount = pagina.Inactivos;
        _filteredCount = ResolverFilteredCount(pagina, filtros);

        if (PageRows.Count > 0 && pagina.Items.Count == 0 && _page > 1)
        {
            Page = _page - 1;
            return;
        }

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        OnPropertyChanged(nameof(MensajeSinResultados));
        NotifyPaginationCanExecuteChanged();
    }

    // ── IDisposable — implementado en RealtimeAwareViewModel ────────
    // OnDispose() libera el debounce de búsqueda y corta la carga en vuelo

    protected override void OnDispose()
    {
        _buscador.Dispose();

        // Si se sale de la pantalla con una página cargando, se cancela: la
        // respuesta ya no tiene a dónde llegar.
        _ctsPagina?.Cancel();
        _ctsPagina?.Dispose();
        _ctsPagina = null;
    }
}
