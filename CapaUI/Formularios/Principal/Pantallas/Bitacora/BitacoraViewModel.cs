using System.Collections.ObjectModel;
using CapaAplicacion.Bitacora.Dtos;
using CapaAplicacion.Bitacora.Interfaces;
using CapaAplicacion.Bitacora.Queries;
using CapaAplicacion.Productos.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Bitacora;

/// <summary>
/// ViewModel de la Bitácora de auditoría. Solo lectura: no hay comandos
/// Nuevo/Editar/Cambiar Estado — la bitácora la escribe el sistema al ejecutar
/// acciones, nunca esta pantalla. Orden por fecha descendente (más reciente
/// primero), paginación server-side y filtros combinables.
/// </summary>
public partial class BitacoraViewModel : ObservableObject, IDisposable
{
    private readonly IBitacoraRepository _repo;
    private CancellationTokenSource?    _searchCts;
    private bool _disposed;

    private string    _query = "";
    private int?      _usuarioFiltro;
    private int?      _moduloFiltro;
    private int?      _accionFiltro;
    private DateTime? _fechaDesde;
    private DateTime? _fechaHasta;
    private int       _page = 1;
    private int       _filteredCount;
    private int?      _pendingSelectionId;
    private int       _loadGeneration;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<BitacoraDto> _pageRows    = new();
    [ObservableProperty] private ObservableCollection<BitacoraDto> _suggestions = new();

    /// <summary>Solo para resaltar la fila elegida desde el buscador — sin acción asociada.</summary>
    [ObservableProperty] private BitacoraDto? _seleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    private bool _isLoading;

    [ObservableProperty] private bool   _showSuggestions;
    [ObservableProperty] private int    _highlightIndex = -1;
    [ObservableProperty] private int    _totalCount;
    [ObservableProperty] private string _errorCarga = "";

    // Lookups de los dropdowns de filtro
    [ObservableProperty] private List<FiltroItem> _modulos  = new();
    [ObservableProperty] private List<FiltroItem> _acciones = new();
    [ObservableProperty] private List<FiltroItem> _usuarios = new();

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0;

    public string PageInfo
    {
        get
        {
            if (_filteredCount == 0) return "Sin resultados";
            int from = (_page - 1) * PageSize + 1;
            int to   = Math.Min(_page * PageSize, _filteredCount);
            return $"Mostrando {from}–{to} de {_filteredCount} registros";
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

    public int? UsuarioFiltro
    {
        get => _usuarioFiltro;
        set { if (_usuarioFiltro == value) return; _usuarioFiltro = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public int? ModuloFiltro
    {
        get => _moduloFiltro;
        set { if (_moduloFiltro == value) return; _moduloFiltro = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public int? AccionFiltro
    {
        get => _accionFiltro;
        set { if (_accionFiltro == value) return; _accionFiltro = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public DateTime? FechaDesde
    {
        get => _fechaDesde;
        set { if (_fechaDesde == value) return; _fechaDesde = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public DateTime? FechaHasta
    {
        get => _fechaHasta;
        set { if (_fechaHasta == value) return; _fechaHasta = value; OnPropertyChanged(); ReiniciarYCargar(); }
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

    // ── Eventos ────────────────────────────────────────────────────────
    public event Action? FiltrosLimpiados;

    /// <summary>La View repuebla el ComboBox de Acción cuando cambia el Módulo.</summary>
    public event Action? AccionesRecargadas;

    // ── Constructor ────────────────────────────────────────────────────
    public BitacoraViewModel(IBitacoraRepository repo) => _repo = repo;

    // ── Carga ──────────────────────────────────────────────────────────
    public async Task CargarDatosAsync()
    {
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var modulosTask  = _repo.ObtenerModulosAsync();
        var usuariosTask = _repo.ObtenerUsuariosAsync();
        var accionesTask = _repo.ObtenerAccionesAsync(null);
        await Task.WhenAll(modulosTask, usuariosTask, accionesTask);

        if (modulosTask.Result.Success)  Modulos  = modulosTask.Result.Value!.ToList();
        if (usuariosTask.Result.Success) Usuarios = usuariosTask.Result.Value!.ToList();
        if (accionesTask.Result.Success) Acciones = accionesTask.Result.Value!.ToList();

        await CargarPaginaAsync();
    }

    public void RefrescarDatos() => _ = CargarPaginaAsync();

    private void ReiniciarYCargar()
    {
        _page = 1;
        _ = CargarPaginaAsync();
    }

    /// <summary>Recarga las acciones disponibles según el módulo seleccionado (cascada).</summary>
    public async Task RecargarAccionesAsync()
    {
        var r = await _repo.ObtenerAccionesAsync(_moduloFiltro);
        if (!r.Success) return;
        Acciones = r.Value!.ToList();
        AccionesRecargadas?.Invoke();
    }

    private const int TimeoutMs = 10_000;

    private BitacoraFiltros BuildFiltros() => new()
    {
        IdUsuario  = _usuarioFiltro,
        IdModulo   = _moduloFiltro,
        IdAccion   = _accionFiltro,
        FechaDesde = _fechaDesde,
        FechaHasta = _fechaHasta,
    };

    private async Task CargarPaginaAsync()
    {
        int myGen  = ++_loadGeneration;
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var task = _repo.GetPagedAsync(_page, PageSize, BuildFiltros());
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
        _filteredCount = pagina.Total;

        PageRows = new ObservableCollection<BitacoraDto>(pagina.Items);

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;

        if (_pendingSelectionId.HasValue)
        {
            Seleccionado        = PageRows.FirstOrDefault(x => x.IdBitacora == _pendingSelectionId.Value);
            _pendingSelectionId = null;
        }
    }

    // ── Búsqueda con debounce ──────────────────────────────────────────
    private async Task RefrescarSugerenciasAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token  = _searchCts.Token;

        var q = _query.Trim();
        if (string.IsNullOrEmpty(q))
        {
            Suggestions     = new ObservableCollection<BitacoraDto>();
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

            Suggestions     = new ObservableCollection<BitacoraDto>(r.Value!);
            ShowSuggestions = r.Value!.Count > 0;
            HighlightIndex  = -1;
        }
        catch (OperationCanceledException) { }
    }

    public void SeleccionarSugerencia(BitacoraDto b)
    {
        // Cancelar el debounce en vuelo: como se asigna al campo _query y no a la
        // propiedad, no se pasa por el setter y nadie mas cancelaria el token. Sin
        // esto, una busqueda en curso termina despues de la seleccion y reabre el popup.
        _searchCts?.Cancel();

        _query = "";
        OnPropertyChanged(nameof(Query));
        ShowSuggestions = false;

        var enPagina = PageRows.FirstOrDefault(x => x.IdBitacora == b.IdBitacora);
        if (enPagina is not null) { Seleccionado = enPagina; return; }

        _pendingSelectionId = b.IdBitacora;
        _ = NavegarAPaginaDeRegistroAsync(b.IdBitacora);
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

    // ── Filtros ────────────────────────────────────────────────────────
    [RelayCommand]
    private void LimpiarFiltros()
    {
        _usuarioFiltro = null;
        _moduloFiltro  = null;
        _accionFiltro  = null;
        _fechaDesde    = null;
        _fechaHasta    = null;
        _page          = 1;
        FiltrosLimpiados?.Invoke();
        _ = RecargarAccionesAsync();
        _ = CargarPaginaAsync();
    }

    // ── Paginación ─────────────────────────────────────────────────────
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

    // ── IDisposable ────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
