using System.Collections.ObjectModel;
using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Proveedores.Dtos;
using CapaAplicacion.Contactos.Proveedores.Interfaces;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Interfaces;
using CapaAplicacion.Proveedores.Queries;
using CapaAplicacion.Realtime;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using CapaUI.Core.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CapaAplicacion.Common.EstadoRegistro;

namespace CapaUI.Formularios.Principal.Pantallas.ContactosProveedores;

public partial class ContactosProveedoresViewModel : RealtimeAwareViewModel
{
    private readonly IProveedorRepository         _provRepo;
    private readonly IContactoProveedorRepository _contactoRepo;
    private readonly SuggestionDebouncer          _buscador = new();

    private string _query         = "";
    private int    _page          = 1;
    private int    _filteredCount;
    private int?   _pendingSelectionId;
    private int    _loadGeneration;
    // P-029: CTS para cancelar peticiones en vuelo al desmontar la vista.
    private readonly CancellationTokenSource _cts = new();

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<ProveedorDto>          _pageRows    = new();
    /// <summary>Sugerencias ya mapeadas para el <c>SuggestionSearchBox</c> (binding directo). <c>null</c> o vacía = popup cerrado.</summary>
    [ObservableProperty] private IReadOnlyList<SuggestionItemData>?          _suggestItems;
    [ObservableProperty] private ObservableCollection<ContactoProveedorDto>  _contactos   = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewingContacts), nameof(Titulo))]
    private ProveedorDto? _proveedorSeleccionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayContactoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarContactoCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarContactoCommand))]
    private ContactoProveedorDto? _contactoSeleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    private bool _isLoading;

    [ObservableProperty] private bool   _isLoadingContactos;
    [ObservableProperty] private int    _highlightIndex = -1;
    [ObservableProperty] private int    _totalCount;
    [ObservableProperty] private int    _activosCount;
    [ObservableProperty] private int    _inactivosCount;
    [ObservableProperty] private string _errorCarga = "";

    public bool   IsViewingContacts       => ProveedorSeleccionado is not null;
    public bool   HayContactoSeleccionado => ContactoSeleccionado is not null;
    public string Titulo => ProveedorSeleccionado is null
        ? "Proveedores"
        : ProveedorSeleccionado.Nombre;

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0 && TotalCount > 0;

    public string PageInfo
    {
        get
        {
            if (_filteredCount == 0) return "Sin resultados";
            int from = (_page - 1) * PageSize + 1;
            int to   = Math.Min(_page * PageSize, _filteredCount);
            return $"Mostrando {from}–{to} de {_filteredCount} proveedores";
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

    public event Action?                       SolicitarNuevoContacto;
    public event Action<ContactoProveedorDto>? SolicitarEditarContacto;

    public ContactosProveedoresViewModel(
        IProveedorRepository         provRepo,
        IContactoProveedorRepository contactoRepo,
        IRealtimeService             realtime,
        IConexionMonitor             conexionMonitor)
        : base(realtime, conexionMonitor)
    {
        _provRepo     = provRepo;
        _contactoRepo = contactoRepo;
    }

    public async Task CargarDatosAsync()
    {
        await CargarPaginaAsync();
        Observar("contactos_proveedor", OnCambioContacto);
    }

    protected override Task OnReconexionAsync() => CargarDatosAsync();

    private const int TimeoutMs = 10_000;

    private async Task CargarPaginaAsync()
    {
        int myGen  = ++_loadGeneration;
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var filtros = new ProveedorFiltros { IdEstado = Activo };
        var task    = _provRepo.GetPagedAsync(_page, PageSize, filtros, _cts.Token);

        // P-029: el Task.Delay del timeout usa un token enlazado que se cancela
        // apenas gana la consulta. Sin esto, CADA carga dejaba un timer de 10 s
        // vivo en el TimerQueue aunque la consulta tardara solo 200 ms.
        using var ctsTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var demora   = Task.Delay(TimeoutMs, ctsTimeout.Token);
        var ganador  = await Task.WhenAny(task, demora);
        ctsTimeout.Cancel();

        if (Disposed) return;
        if (ganador != task)
        {
            if (myGen != _loadGeneration) return;
            ErrorCarga = "La carga tardó demasiado. Intente de nuevo.";
            IsLoading  = false;
            return;
        }

        var r = await task;
        if (Disposed || myGen != _loadGeneration) return;

        if (!r.Success) { ErrorCarga = r.Error; IsLoading = false; return; }

        var pagina = r.Value!;
        TotalCount     = pagina.Total;
        ActivosCount   = pagina.Activos;
        InactivosCount = pagina.Inactivos;
        _filteredCount = pagina.Activos;

        PageRows = new ObservableCollection<ProveedorDto>(pagina.Items);
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;

        if (_pendingSelectionId.HasValue)
        {
            var prov = PageRows.FirstOrDefault(x => x.Id == _pendingSelectionId.Value);
            if (prov is not null) await AbrirProveedorAsync(prov);
            _pendingSelectionId = null;
        }
    }

    private Task RefrescarSugerenciasAsync() => _buscador.EjecutarAsync(
        _query,
        async (q, ct) =>
        {
            var r = await _provRepo.BuscarSugerenciasAsync(q, new ProveedorFiltros { IdEstado = Activo }, ct);
            return r.Success ? r.Value!.Select(Map).ToList() : null;
        },
        items => { SuggestItems = items; HighlightIndex = -1; });

    private static SuggestionItemData Map(ProveedorDto p) => new()
    {
        Nombre = p.Nombre,
        Meta   = p.Rtn,
        Activo = p.IdEstado == Activo,
        Source = p,
    };

    public void SeleccionarSugerencia(ProveedorDto prov)
    {
        // Cancelar el debounce en vuelo: como se asigna al campo _query y no a la
        // propiedad, no se pasa por el setter y nadie mas cancelaria la busqueda. Sin
        // esto, una busqueda en curso termina despues de la seleccion y reabre el popup.
        _buscador.Cancelar();

        _query = "";
        OnPropertyChanged(nameof(Query));
        SuggestItems = null;

        var enPagina = PageRows.FirstOrDefault(x => x.Id == prov.Id);
        if (enPagina is not null)
        {
            _ = AbrirProveedorAsync(enPagina);
            return;
        }

        _pendingSelectionId = prov.Id;
        _ = NavegarAPaginaDeProveedorAsync(prov.Id);
    }

    private async Task NavegarAPaginaDeProveedorAsync(int id)
    {
        var r = await _provRepo.GetPaginaDeRegistroAsync(id, PageSize, new ProveedorFiltros { IdEstado = Activo });
        if (!r.Success) { _pendingSelectionId = null; ErrorCarga = r.Error; return; }

        _page = r.Value;
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(TotalPages));
        NotifyPaginationCanExecuteChanged();
        await CargarPaginaAsync();
    }

    public async Task AbrirProveedorAsync(ProveedorDto prov)
    {
        ProveedorSeleccionado = prov;
        ContactoSeleccionado  = null;
        await CargarContactosAsync();
    }

    /// <summary>
    /// En modo silencioso no levanta IsLoadingContactos: se usa al volver del
    /// modal de guardado, para que la lista no se vacie y vuelva a aparecer.
    /// </summary>
    public async Task CargarContactosAsync(bool silencioso = false)
    {
        if (ProveedorSeleccionado is null) return;
        if (!silencioso) IsLoadingContactos = true;
        ErrorCarga         = string.Empty;

        var r = await _contactoRepo.GetByProveedorAsync(ProveedorSeleccionado.Id, _cts.Token);
        if (Disposed) return;
        IsLoadingContactos = false;

        if (!r.Success) { ErrorCarga = r.Error; return; }
        Contactos = new ObservableCollection<ContactoProveedorDto>(r.Value!);
    }

    [RelayCommand]
    private void Volver()
    {
        ProveedorSeleccionado = null;
        ContactoSeleccionado  = null;
        Contactos.Clear();
        ErrorCarga = string.Empty;
    }

    [RelayCommand]
    private void NuevoContacto()
    {
        if (SesionPermisos.Tiene(Permiso.CrearProveedor)) SolicitarNuevoContacto?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HayContactoSeleccionado))]
    private void EditarContacto()
    {
        if (ContactoSeleccionado is not null && SesionPermisos.Tiene(Permiso.ModificarProveedor))
            SolicitarEditarContacto?.Invoke(ContactoSeleccionado);
    }

    [RelayCommand(CanExecute = nameof(HayContactoSeleccionado))]
    private async Task EliminarContacto()
    {
        if (ContactoSeleccionado is null || !SesionPermisos.Tiene(Permiso.EliminarProveedor)) return;
        var r = await _contactoRepo.DeleteAsync(ContactoSeleccionado.Id);
        if (!r.Success) { ErrorCarga = r.Error; return; }
        ContactoSeleccionado = null;
        await CargarContactosAsync();
    }

    // ── Paginación ─────────────────────────────────────────────────

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

    // ── Realtime ───────────────────────────────────────────────────

    private void OnCambioContacto(CambioRealtime cambio)
    {
        if (Disposed || ProveedorSeleccionado is null) return;
        if (!string.Equals(cambio.Operacion, "INSERT", StringComparison.OrdinalIgnoreCase)
         && !string.Equals(cambio.Operacion, "UPDATE", StringComparison.OrdinalIgnoreCase)) return;
        _ = CargarContactosAsync();
    }

    // ── IDisposable ────────────────────────────────────────────────

    protected override void OnDispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _buscador.Dispose();
    }
}
