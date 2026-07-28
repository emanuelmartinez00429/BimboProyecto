using System.Collections.ObjectModel;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Usuarios;

public enum EstadoUsuarioFilter { Activos, Inactivos, Todos }

/// <summary>
/// ViewModel del formulario de Usuarios.
/// Paginación, búsqueda, filtros por estado/rol y CRUD vía IUsuarioRepository.
/// </summary>
public partial class UsuariosViewModel : ObservableObject, IDisposable
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IRolRepository     _rolRepo;
    private CancellationTokenSource?   _searchCts;
    private bool _disposed;

    private string              _query          = "";
    private EstadoUsuarioFilter _estadoFiltro   = EstadoUsuarioFilter.Activos;
    private int?                _rolFiltro;
    private int                 _page           = 1;
    private int                 _filteredCount;
    private int                 _loadGeneration;

    public const int PageSize = 50;

    // ── Datos de la grilla ─────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<UsuarioVistaDto> _pageRows    = new();
    [ObservableProperty] private ObservableCollection<UsuarioVistaDto> _suggestions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleEstadoCommand))]
    private UsuarioVistaDto? _seleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    private bool _isLoading;

    [ObservableProperty] private bool  _showSuggestions;
    [ObservableProperty] private int   _highlightIndex = -1;
    [ObservableProperty] private int   _totalCount;
    [ObservableProperty] private int   _activosCount;
    [ObservableProperty] private int   _inactivosCount;
    [ObservableProperty] private List<RolDto> _roles = new();
    [ObservableProperty] private string _errorCarga = "";

    // ── Propiedades derivadas ──────────────────────────────────────────
    public bool   HaySeleccionado   => Seleccionado is not null;
    public string TextoSeleccionado => Seleccionado is null
        ? ""
        : $"{Seleccionado.CorreoUsuario} · {Seleccionado.NombreEmpleado}";

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0 && TotalCount > 0;

    public string PageInfo
    {
        get
        {
            if (_filteredCount == 0) return "Sin resultados";
            int from = (_page - 1) * PageSize + 1;
            int to   = Math.Min(_page * PageSize, _filteredCount);
            return $"Mostrando {from}–{to} de {_filteredCount} usuarios";
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

    public EstadoUsuarioFilter EstadoFiltro
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

    public int? RolFiltro
    {
        get => _rolFiltro;
        set
        {
            if (_rolFiltro == value) return;
            _rolFiltro = value;
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

    // ── Eventos ────────────────────────────────────────────────────────
    public event Action<UsuarioVistaDto>? SolicitarEditar;
    public event Action?                 FiltrosLimpiados;

    // ── Constructor ────────────────────────────────────────────────────
    public UsuariosViewModel(IUsuarioRepository usuarioRepo, IRolRepository rolRepo)
    {
        _usuarioRepo = usuarioRepo;
        _rolRepo     = rolRepo;
    }

    // ── Carga ──────────────────────────────────────────────────────────
    public async Task CargarDatosAsync()
    {
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var rolesResult = await _rolRepo.ObtenerTodosAsync();
        if (rolesResult.Success)
            Roles = rolesResult.Value!.ToList();
        else
            ErrorCarga = rolesResult.Error;

        await CargarPaginaAsync();
    }

    public void RefrescarDatos() => _ = CargarPaginaAsync();

    private const int TimeoutMs = 10_000;

    private async Task CargarPaginaAsync()
    {
        int myGen  = ++_loadGeneration;
        IsLoading  = true;
        ErrorCarga = string.Empty;

        int? idEstado = _estadoFiltro switch
        {
            EstadoUsuarioFilter.Activos   => 1,
            EstadoUsuarioFilter.Inactivos => 2,
            _                            => null,
        };

        var task = _usuarioRepo.ObtenerPaginaAsync(_page, PageSize, idEstado, _rolFiltro, _query);
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
        _filteredCount = _estadoFiltro switch
        {
            EstadoUsuarioFilter.Activos   => pagina.Activos,
            EstadoUsuarioFilter.Inactivos => pagina.Inactivos,
            _                            => pagina.Total,
        };

        PageRows = new ObservableCollection<UsuarioVistaDto>(pagina.Items);

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;
    }

    // ── Búsqueda con debounce ──────────────────────────────────────────
    private async Task RefrescarSugerenciasAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        var q = _query.Trim();
        if (string.IsNullOrEmpty(q))
        {
            Suggestions     = new ObservableCollection<UsuarioVistaDto>();
            ShowSuggestions = false;
            return;
        }

        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            int? idEstado = _estadoFiltro switch
            {
                EstadoUsuarioFilter.Activos   => 1,
                EstadoUsuarioFilter.Inactivos => 2,
                _                            => null,
            };

            var r = await _usuarioRepo.ObtenerPaginaAsync(1, 10, idEstado, _rolFiltro, q);
            if (token.IsCancellationRequested) return;

            if (!r.Success) { ShowSuggestions = false; return; }

            Suggestions     = new ObservableCollection<UsuarioVistaDto>(r.Value!.Items);
            ShowSuggestions = r.Value!.Items.Count > 0;
            HighlightIndex  = -1;
        }
        catch (OperationCanceledException) { }
    }

    public void SeleccionarSugerencia(UsuarioVistaDto u)
    {
        // Cancelar el debounce en vuelo: como se asigna al campo _query y no a la
        // propiedad, no se pasa por el setter y nadie mas cancelaria el token. Sin
        // esto, una busqueda en curso termina despues de la seleccion y reabre el popup.
        _searchCts?.Cancel();

        _query = "";
        OnPropertyChanged(nameof(Query));
        ShowSuggestions = false;

        var enPagina = PageRows.FirstOrDefault(x => x.IdUsuario == u.IdUsuario);
        if (enPagina is not null) { Seleccionado = enPagina; return; }

        _ = CargarPaginaAsync();
    }

    // ── Comandos CRUD ──────────────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private void Editar()
    {
        if (Seleccionado is not null) SolicitarEditar?.Invoke(Seleccionado);
    }

    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private async Task ToggleEstadoAsync()
    {
        if (Seleccionado is null) return;
        int nuevoEstado = Seleccionado.IdEstado == 1 ? 2 : 1;
        var r = await _usuarioRepo.CambiarEstadoAsync(Seleccionado.IdUsuario, nuevoEstado);
        if (!r.Success)
        {
            ErrorCarga = r.Error;
            return;
        }
        await CargarPaginaAsync();
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        _estadoFiltro = EstadoUsuarioFilter.Activos;
        _rolFiltro    = null;
        _page         = 1;
        FiltrosLimpiados?.Invoke();
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
