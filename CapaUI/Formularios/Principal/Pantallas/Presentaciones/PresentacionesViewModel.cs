using System.Collections.ObjectModel;
using CapaAplicacion.Common;
using CapaAplicacion.Presentaciones.Dtos;
using static CapaAplicacion.Common.EstadoRegistro;
using CapaAplicacion.Presentaciones.Interfaces;
using CapaAplicacion.Presentaciones.Queries;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Conexion;
using CapaAplicacion.Realtime;
using CapaUI.Core.Controls;
using CapaUI.Core.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Presentaciones;

public enum EstadoFilter { Habilitados, Deshabilitados, Todos }

/// <summary>
/// ViewModel del formulario de Presentaciones de Producto.
/// Hereda de RealtimeAwareViewModel: la suscripción a "presentacion_producto" se
/// cancela automáticamente al Dispose(), sin posibilidad de olvidar la baja.
/// </summary>
public partial class PresentacionesViewModel : RealtimeAwareViewModel
{
    private readonly IPresentacionRepository _repo;
    private readonly SuggestionDebouncer _buscador = new();

    private string       _query        = "";
    private EstadoFilter _estadoFiltro = EstadoFilter.Habilitados;
    private int          _page         = 1;
    private int          _filteredCount;
    private int?         _pendingSelectionId;
    private int          _loadGeneration;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<PresentacionDto> _pageRows = new();
    /// <summary>Sugerencias ya mapeadas para el <c>SuggestionSearchBox</c> (binding directo). <c>null</c> o vacía = popup cerrado.</summary>
    [ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    private PresentacionDto? _seleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    private bool _isLoading;

    [ObservableProperty] private int    _highlightIndex = -1;
    [ObservableProperty] private int    _totalCount;
    [ObservableProperty] private int    _activosCount;
    [ObservableProperty] private int    _inactivosCount;
    [ObservableProperty] private string _errorCarga = "";

    public bool   HaySeleccionado   => Seleccionado is not null;
    public string TextoSeleccionado => Seleccionado is null
        ? ""
        : $"{Seleccionado.Nombre} · {Seleccionado.Descripcion}";

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0 && TotalCount > 0;
    public string PageInfo
    {
        get
        {
            if (_filteredCount == 0) return "Sin resultados";
            int from = (_page - 1) * PageSize + 1;
            int to   = Math.Min(_page * PageSize, _filteredCount);
            return $"Mostrando {from}–{to} de {_filteredCount} presentaciones";
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

    public event Action?                  SolicitarNuevo;
    public event Action<PresentacionDto>? SolicitarEditar;
    public event Action?                  FiltrosLimpiados;

    public PresentacionesViewModel(IPresentacionRepository repo, IRealtimeService realtime,
                                   IConexionMonitor conexionMonitor)
        : base(realtime, conexionMonitor)
    {
        _repo = repo;
    }

    public async Task CargarDatosAsync()
    {
        await CargarPaginaAsync();
        Observar("presentacion_producto", OnCambioPresentacion);
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

        PageRows = new ObservableCollection<PresentacionDto>(pagina.Items);

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

    private Task RefrescarSugerenciasAsync() => _buscador.EjecutarAsync(
        _query,
        async (q, ct) =>
        {
            var r = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), ct);
            return r.Success ? r.Value!.Select(Map).ToList() : null;
        },
        items => { SuggestItems = items; HighlightIndex = -1; });

    private static SuggestionItemData Map(PresentacionDto p) => new()
    {
        Nombre = p.Nombre,
        Meta   = p.Descripcion,
        Activo = p.IdEstado == Activo,
        Source = p,
    };

    public void SeleccionarSugerencia(PresentacionDto p)
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
        _ = NavegarAPaginaDeRegistroAsync(p.Id);
    }

    private async Task NavegarAPaginaDeRegistroAsync(int id)
    {
        var r = await _repo.GetPaginaDeRegistroAsync(id, PageSize, BuildFiltros());
        if (!r.Success) { _pendingSelectionId = null; ErrorCarga = r.Error; return; }

        _page = r.Value;
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(TotalPages));
        NotifyPaginationCanExecuteChanged();
        await CargarPaginaAsync();
    }

    private PresentacionFiltros BuildFiltros() => new()
    {
        IdEstado = _estadoFiltro switch
        {
            EstadoFilter.Habilitados    => Activo,
            EstadoFilter.Deshabilitados => Inactivo,
            _                           => null
        },
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
        _estadoFiltro = EstadoFilter.Habilitados;
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

    private static int ResolverFilteredCount(PagedResult<PresentacionDto> pagina, PresentacionFiltros filtros) =>
        filtros.IdEstado switch
        {
            Activo   => pagina.Activos,
            Inactivo => pagina.Inactivos,
            _        => pagina.Total
        };

    private void OnCambioPresentacion(CambioRealtime cambio)
    {
        if (Disposed) return;

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
            Result<PagedResult<PresentacionDto>> r;

            try
            {
                r = await _repo.GetPagedAsync(_page, PageSize, filtros);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "PresentacionesVM: silent refresh — excepción en request");
                return;
            }

            if (_loadGeneration != genCapturada) return;
            if (!r.Success)
            {
                Serilog.Log.Warning("PresentacionesVM: silent refresh — error: {Error}", r.Error);
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
                PageRows = new ObservableCollection<PresentacionDto>(pagina.Items);
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
            Serilog.Log.Error(ex, "PresentacionesVM: error inesperado en silent refresh");
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

    protected override void OnDispose()
    {
        _buscador.Dispose();
    }
}
