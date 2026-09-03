using System.Collections.ObjectModel;
using CapaAplicacion.Common;
using CapaAplicacion.Fabricantes.Dtos;
using CapaAplicacion.Productos.Dtos;
using static CapaAplicacion.Common.EstadoRegistro;
using CapaAplicacion.Fabricantes.Interfaces;
using CapaAplicacion.Fabricantes.Queries;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Conexion;
using CapaAplicacion.Realtime;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using CapaUI.Core.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Fabricantes;

public enum EstadoFilter { Activos, Inactivos, Todos }

public partial class FabricantesViewModel : RealtimeAwareViewModel
{
    private readonly IFabricanteRepository _repo;
    private readonly SuggestionDebouncer _buscador = new();

    private string       _query        = "";
    private EstadoFilter _estadoFiltro = EstadoFilter.Activos;
    private int?         _paisIdFiltro;
    private int          _page         = 1;
    private int          _filteredCount;
    private int?         _pendingSelectionId;
    private int          _loadGeneration;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<FabricanteDto> _pageRows    = new();
    /// <summary>Sugerencias ya mapeadas para el <c>SuggestionSearchBox</c> (binding directo). <c>null</c> o vacía = popup cerrado.</summary>
    [ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    private FabricanteDto? _seleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    private bool _isLoading;

    [ObservableProperty] private int              _highlightIndex = -1;
    [ObservableProperty] private int              _totalCount;
    [ObservableProperty] private int              _activosCount;
    [ObservableProperty] private int              _inactivosCount;
    [ObservableProperty] private List<FiltroItem> _paises    = new();
    [ObservableProperty] private string           _errorCarga = "";

    public bool   HaySeleccionado   => Seleccionado is not null;
    public string TextoSeleccionado => Seleccionado is null ? "" : Seleccionado.Nombre;

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0 && TotalCount > 0;
    public string PageInfo
    {
        get
        {
            if (_filteredCount == 0) return "Sin resultados";
            int from = (_page - 1) * PageSize + 1;
            int to   = Math.Min(_page * PageSize, _filteredCount);
            return $"Mostrando {from}–{to} de {_filteredCount} fabricantes";
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
            AplicarCambioDeFiltro();
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
            AplicarCambioDeFiltro();
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

    public event Action?                SolicitarNuevo;
    public event Action<FabricanteDto>? SolicitarEditar;
    public event Action?                FiltrosLimpiados;

    public FabricantesViewModel(IFabricanteRepository repo, IRealtimeService realtime,
                                IConexionMonitor conexionMonitor)
        : base(realtime, conexionMonitor)
    {
        _repo = repo;
    }

    public async Task CargarDatosAsync()
    {
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var rPaises = await _repo.GetPaisesAsync();
        if (!rPaises.Success) { ErrorCarga = rPaises.Error; IsLoading = false; return; }
        Paises = rPaises.Value!.ToList();

        await CargarPaginaAsync();

        Observar("fabricante", OnCambioFabricante);
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

        PageRows = new ObservableCollection<FabricanteDto>(pagina.Items);

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

    /// <summary>
    /// Entrada única de todo cambio de filtro/orden: vuelve a la primera página,
    /// recarga la grilla y RE-LANZA la búsqueda de sugerencias con el texto
    /// actual, para que el popup del SuggestionSearchBox refleje el filtro nuevo
    /// sin que el usuario tenga que reescribir. Un filtro nuevo solo llama acá.
    /// </summary>
    private void AplicarCambioDeFiltro()
    {
        _page = 1;
        _ = CargarPaginaAsync();
        _ = RefrescarSugerenciasAsync();
    }

    private static SuggestionItemData Map(FabricanteDto f) => new()
    {
        Nombre = f.Nombre,
        Meta   = $"{f.NombreProveedor} · {f.NombrePais}",
        Activo = f.IdEstado == Activo,
        Source = f,
    };

    public void SeleccionarSugerencia(FabricanteDto f)
    {
        // Cancelar el debounce en vuelo: como se asigna al campo _query y no a la
        // propiedad, no se pasa por el setter y nadie mas cancelaria la busqueda. Sin
        // esto, una busqueda en curso termina despues de la seleccion y reabre el popup.
        _buscador.Cancelar();

        _query = "";
        OnPropertyChanged(nameof(Query));
        SuggestItems = null;

        var enPagina = PageRows.FirstOrDefault(x => x.Id == f.Id);
        if (enPagina is not null) { Seleccionado = enPagina; return; }

        _pendingSelectionId = f.Id;
        _ = NavegarAPaginaDeRegistroAsync(f.Id);
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

    private FabricanteFiltros BuildFiltros() => new()
    {
        IdEstado = _estadoFiltro switch
        {
            EstadoFilter.Activos    => Activo,
            EstadoFilter.Inactivos => Inactivo,
            _                           => null
        },
        IdPais = _paisIdFiltro,
    };

    [RelayCommand]
    private void Nuevo()
    {
        if (SesionPermisos.Tiene(Permiso.CrearFabricante)) SolicitarNuevo?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private void Editar()
    {
        if (Seleccionado is not null && SesionPermisos.Tiene(Permiso.ModificarFabricante)) SolicitarEditar?.Invoke(Seleccionado);
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        _estadoFiltro = EstadoFilter.Activos;
        _paisIdFiltro = null;
        FiltrosLimpiados?.Invoke();
        AplicarCambioDeFiltro();
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

    private static int ResolverFilteredCount(PagedResult<FabricanteDto> pagina, FabricanteFiltros filtros) =>
        filtros.IdEstado switch
        {
            Activo   => pagina.Activos,
            Inactivo => pagina.Inactivos,
            _        => pagina.Total
        };

    private void OnCambioFabricante(CambioRealtime cambio)
    {
        if (Disposed) return;

        bool afectaPaginaActual = cambio.IdRegistro.HasValue
            && PageRows.Any(f => f.Id == cambio.IdRegistro.Value);

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
            Result<PagedResult<FabricanteDto>> r;

            try
            {
                r = await _repo.GetPagedAsync(_page, PageSize, filtros);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "FabricantesVM: silent refresh — excepción en request");
                return;
            }

            if (_loadGeneration != genCapturada) return;
            if (!r.Success)
            {
                Serilog.Log.Warning("FabricantesVM: silent refresh — error: {Error}", r.Error);
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
                PageRows = new ObservableCollection<FabricanteDto>(pagina.Items);
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
            Serilog.Log.Error(ex, "FabricantesVM: error inesperado en silent refresh");
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
        NotifyPaginationCanExecuteChanged();
    }

    protected override void OnDispose()
    {
        _buscador.Dispose();
    }
}
