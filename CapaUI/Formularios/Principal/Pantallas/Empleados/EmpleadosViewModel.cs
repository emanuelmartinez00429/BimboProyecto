using System.Collections.ObjectModel;
using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Interfaces;
using CapaAplicacion.Empleados.Queries;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Empleados;

public enum EstadoEmpleadoFilter { Activos, Inactivos, Todos }

/// <summary>
/// ViewModel del formulario de Empleados. Paginación server-side, búsqueda con
/// sugerencias y filtro por estado — mismo patrón que Usuarios/Categorías.
/// CRUD completo: crear, editar y cambiar estado.
/// </summary>
public partial class EmpleadosViewModel : ObservableObject, IDisposable
{
    private readonly IEmpleadoRepository _repo;
    private readonly SuggestionDebouncer    _buscador = new();
    private bool _disposed;

    private string               _query        = "";
    private EstadoEmpleadoFilter _estadoFiltro = EstadoEmpleadoFilter.Activos;
    private int                  _page         = 1;
    private int                  _filteredCount;
    private int?                 _pendingSelectionId;
    private int                  _loadGeneration;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<EmpleadoDto> _pageRows    = new();
    /// <summary>Sugerencias ya mapeadas para el <c>SuggestionSearchBox</c> (binding directo). <c>null</c> o vacía = popup cerrado.</summary>
    [ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleEstadoCommand))]
    [NotifyCanExecuteChangedFor(nameof(CrearUsuarioCommand))]
    private EmpleadoDto? _seleccionado;

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
        : $"{Seleccionado.NombreEmpleado} {Seleccionado.ApellidoEmpleado} · {Seleccionado.NumeroIdentidad}";

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0 && TotalCount > 0;

    public string PageInfo
    {
        get
        {
            if (_filteredCount == 0) return "Sin resultados";
            int from = (_page - 1) * PageSize + 1;
            int to   = Math.Min(_page * PageSize, _filteredCount);
            return $"Mostrando {from}–{to} de {_filteredCount} empleados";
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

    public EstadoEmpleadoFilter EstadoFiltro
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

    // ── Eventos ────────────────────────────────────────────────────────
    public event Action?              SolicitarNuevo;
    public event Action<EmpleadoDto>? SolicitarEditar;
    public event Action<EmpleadoDto>? SolicitarCrearUsuario;
    public event Action?              FiltrosLimpiados;

    // ── Constructor ────────────────────────────────────────────────────
    public EmpleadosViewModel(IEmpleadoRepository repo)
    {
        _repo = repo;
    }

    // ── Carga ──────────────────────────────────────────────────────────
    public async Task CargarDatosAsync() => await CargarPaginaAsync();

    public void RefrescarDatos() => _ = CargarPaginaAsync();

    private const int TimeoutMs = 10_000;

    private EmpleadoFiltros BuildFiltros() => new()
    {
        IdEstado = _estadoFiltro switch
        {
            EstadoEmpleadoFilter.Activos   => 1,
            EstadoEmpleadoFilter.Inactivos => 2,
            _                              => null,
        },
    };

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
        _filteredCount = _estadoFiltro switch
        {
            EstadoEmpleadoFilter.Activos   => pagina.Activos,
            EstadoEmpleadoFilter.Inactivos => pagina.Inactivos,
            _                              => pagina.Total,
        };

        PageRows = new ObservableCollection<EmpleadoDto>(pagina.Items);

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;

        if (_pendingSelectionId.HasValue)
        {
            Seleccionado        = PageRows.FirstOrDefault(x => x.IdEmpleado == _pendingSelectionId.Value);
            _pendingSelectionId = null;
        }
    }

    // ── Búsqueda con debounce ──────────────────────────────────────────
    private Task RefrescarSugerenciasAsync() => _buscador.EjecutarAsync(
        _query,
        async (q, ct) =>
        {
            var r = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), ct);
            return r.Success ? r.Value!.Select(Map).ToList() : null;
        },
        items => { SuggestItems = items; HighlightIndex = -1; });

    private static SuggestionItemData Map(EmpleadoDto e) => new()
    {
        Nombre = $"{e.NombreEmpleado} {e.ApellidoEmpleado}",
        Meta   = $"{e.NumeroIdentidad} · {e.CorreoEmpleado}",
        Activo = e.IdEstado == 1,
        Source = e,
    };

    public void SeleccionarSugerencia(EmpleadoDto e)
    {
        // Cancelar el debounce en vuelo: como se asigna al campo _query y no a la
        // propiedad, no se pasa por el setter y nadie mas cancelaria la busqueda. Sin
        // esto, una busqueda en curso termina despues de la seleccion y reabre el popup.
        _buscador.Cancelar();

        _query = "";
        OnPropertyChanged(nameof(Query));
        SuggestItems = null;

        var enPagina = PageRows.FirstOrDefault(x => x.IdEmpleado == e.IdEmpleado);
        if (enPagina is not null) { Seleccionado = enPagina; return; }

        _pendingSelectionId = e.IdEmpleado;
        _ = NavegarAPaginaDeRegistroAsync(e.IdEmpleado);
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

    // ── Comandos CRUD ──────────────────────────────────────────────────
    [RelayCommand]
    private void Nuevo()
    {
        if (SesionPermisos.Tiene(Permiso.CrearEmpleado)) SolicitarNuevo?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private void Editar()
    {
        if (Seleccionado is not null && SesionPermisos.Tiene(Permiso.ModificarEmpleado)) SolicitarEditar?.Invoke(Seleccionado);
    }

    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private void CrearUsuario()
    {
        if (Seleccionado is not null && SesionPermisos.Tiene(Permiso.CrearUsuario)) SolicitarCrearUsuario?.Invoke(Seleccionado);
    }

    /// <summary>
    /// Cambia el estado del empleado seleccionado entre activo (1) e inactivo (2).
    /// </summary>
    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private async Task ToggleEstadoAsync()
    {
        if (Seleccionado is null || !SesionPermisos.Tiene(Permiso.EliminarEmpleado)) return;
        int nuevoEstado = Seleccionado.IdEstado == 1 ? 2 : 1;
        var r = await _repo.CambiarEstadoAsync(Seleccionado.IdEmpleado, nuevoEstado);
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
        _estadoFiltro = EstadoEmpleadoFilter.Activos;
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
        _buscador.Dispose();
    }
}
