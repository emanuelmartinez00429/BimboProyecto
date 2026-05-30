using System.Collections.ObjectModel;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using static CapaAplicacion.Common.EstadoRegistro;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Conexion;
using CapaAplicacion.Realtime;
using CapaUI.Core.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Productos;

public enum EstadoFilter { Habilitados, Deshabilitados, Todos }

/// <summary>
/// ViewModel del formulario de Productos.
/// Hereda de RealtimeAwareViewModel: la suscripción a "productos" se cancela
/// automáticamente al Dispose(), sin posibilidad de olvidar la baja.
/// </summary>
public partial class ProductosViewModel : RealtimeAwareViewModel
{
    private readonly IProductoRepository _repo;
    private CancellationTokenSource? _searchCts;

    private string       _query              = "";
    private EstadoFilter _estadoFiltro       = EstadoFilter.Habilitados;
    private int?         _fabricanteIdFiltro;
    private int?         _paisIdFiltro;
    private int          _page               = 1;
    private int          _filteredCount;
    private int?         _pendingSelectionId;
    private int          _loadGeneration;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<ProductoDto> _pageRows    = new();
    [ObservableProperty] private ObservableCollection<ProductoDto> _suggestions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    private ProductoDto? _seleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    private bool _isLoading;
    [ObservableProperty] private bool             _showSuggestions;
    [ObservableProperty] private int              _highlightIndex = -1;
    [ObservableProperty] private int              _totalCount;
    [ObservableProperty] private int              _activosCount;
    [ObservableProperty] private int              _inactivosCount;
    [ObservableProperty] private List<FiltroItem> _fabricantes = new();
    [ObservableProperty] private List<FiltroItem> _paises      = new();
    [ObservableProperty] private string           _errorCarga  = "";

    public bool   HaySeleccionado   => Seleccionado is not null;
    public string TextoSeleccionado => Seleccionado is null
        ? ""
        : $"{Seleccionado.CodigoInterno} · {Seleccionado.Nombre}";

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0 && TotalCount > 0;
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

    public ProductosViewModel(IProductoRepository repo, IRealtimeService realtime,
                              IConexionMonitor conexionMonitor)
        : base(realtime, conexionMonitor)
    {
        _repo = repo;
    }

    public async Task CargarDatosAsync()
    {
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var fabTask  = _repo.GetFabricantesAsync();
        var paisTask = _repo.GetPaisesAsync();
        await Task.WhenAll(fabTask, paisTask);

        var rFab = fabTask.Result;
        if (!rFab.Success) { ErrorCarga = rFab.Error; IsLoading = false; return; }
        Fabricantes = rFab.Value!.ToList();

        var rPaises = paisTask.Result;
        if (!rPaises.Success) { ErrorCarga = rPaises.Error; IsLoading = false; return; }
        Paises = rPaises.Value!.ToList();

        await CargarPaginaAsync();

        // Observar() registra el token de baja — se cancela en Dispose() automáticamente
        Observar("productos", OnCambioProducto);
    }

    public void RefrescarDatos() => _ = CargarPaginaAsync();

    // Al volver la conexión, recarga todo (Observar es idempotente → no duplica suscripción).
    protected override Task OnReconexionAsync() => CargarDatosAsync();

    private const int TimeoutMs = 10_000;

    private async Task CargarPaginaAsync()
    {
        int myGen  = ++_loadGeneration;
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var filtros = BuildFiltros();

        var task = _repo.GetPagedAsync(_page, PageSize, filtros);
        if (await Task.WhenAny(task, Task.Delay(TimeoutMs)) != task)
        {
            if (myGen != _loadGeneration) return;
            ErrorCarga = "La carga tardó demasiado. Intente de nuevo.";
            IsLoading  = false;
            return;
        }

        var r = await task;
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
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;

        if (_pendingSelectionId.HasValue)
        {
            Seleccionado        = PageRows.FirstOrDefault(x => x.Id == _pendingSelectionId.Value);
            _pendingSelectionId = null;
        }
    }

    private async Task RefrescarSugerenciasAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token  = _searchCts.Token;

        var q = _query.Trim();
        if (string.IsNullOrEmpty(q))
        {
            Suggestions     = new ObservableCollection<ProductoDto>();
            ShowSuggestions = false;
            return;
        }

        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            var r = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), token);
            if (token.IsCancellationRequested) return;

            if (!r.Success) { ShowSuggestions = false; return; }

            Suggestions     = new ObservableCollection<ProductoDto>(r.Value!);
            ShowSuggestions = r.Value!.Count > 0;
            HighlightIndex  = -1;
        }
        catch (OperationCanceledException) { }
    }

    public void SeleccionarSugerencia(ProductoDto p)
    {
        _query = "";
        OnPropertyChanged(nameof(Query));
        ShowSuggestions = false;

        var enPagina = PageRows.FirstOrDefault(x => x.Id == p.Id);
        if (enPagina is not null) { Seleccionado = enPagina; return; }

        _pendingSelectionId = p.Id;
        _ = NavegarAPaginaDeProductoAsync(p.Id);
    }

    private async Task NavegarAPaginaDeProductoAsync(int idProducto)
    {
        var r = await _repo.GetPaginaDeProductoAsync(idProducto, PageSize, BuildFiltros());
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
            EstadoFilter.Habilitados    => Activo,
            EstadoFilter.Deshabilitados => Inactivo,
            _                           => null
        },
        IdFabricante = _fabricanteIdFiltro,
        IdPais       = _paisIdFiltro,
    };

    [RelayCommand]
    private void Nuevo() => SolicitarNuevo?.Invoke();

    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private void Editar()
    {
        if (Seleccionado is not null) SolicitarEditar?.Invoke(Seleccionado);
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        _estadoFiltro       = EstadoFilter.Habilitados;
        _fabricanteIdFiltro = null;
        _paisIdFiltro       = null;
        _page = 1;
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

    private async Task CargarPaginaSilenciosamenteAsync(bool actualizarFilas, bool esInsert)
    {
        try
        {
            if (IsLoading) return;

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
            NotifyPaginationCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "ProductosVM: error inesperado en silent refresh");
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
        NotifyPaginationCanExecuteChanged();
    }

    // ── IDisposable — implementado en RealtimeAwareViewModel ────────
    // OnDispose() libera el CancellationTokenSource de búsqueda

    protected override void OnDispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
