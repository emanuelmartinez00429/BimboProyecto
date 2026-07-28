using System.Collections.ObjectModel;
using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Fabricantes.Dtos;
using CapaAplicacion.Contactos.Fabricantes.Interfaces;
using CapaAplicacion.Fabricantes.Dtos;
using CapaAplicacion.Fabricantes.Interfaces;
using CapaAplicacion.Fabricantes.Queries;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Realtime;
using CapaUI.Core.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CapaAplicacion.Common.EstadoRegistro;

namespace CapaUI.Formularios.Principal.Pantallas.ContactosFabricantes;

public partial class ContactosFabricantesViewModel : RealtimeAwareViewModel
{
    private readonly IFabricanteRepository         _fabRepo;
    private readonly IContactoFabricanteRepository _contactoRepo;
    private CancellationTokenSource?               _searchCts;

    private string _query         = "";
    private int    _page          = 1;
    private int    _filteredCount;
    private int?   _pendingSelectionId;
    private int    _loadGeneration;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<FabricanteDto>         _pageRows    = new();
    [ObservableProperty] private ObservableCollection<FabricanteDto>         _suggestions = new();
    [ObservableProperty] private ObservableCollection<ContactoFabricanteDto> _contactos   = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewingContacts), nameof(Titulo))]
    private FabricanteDto? _fabricanteSeleccionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayContactoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarContactoCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarContactoCommand))]
    private ContactoFabricanteDto? _contactoSeleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    private bool _isLoading;

    [ObservableProperty] private bool   _isLoadingContactos;
    [ObservableProperty] private bool   _showSuggestions;
    [ObservableProperty] private int    _highlightIndex = -1;
    [ObservableProperty] private int    _totalCount;
    [ObservableProperty] private int    _activosCount;
    [ObservableProperty] private int    _inactivosCount;
    [ObservableProperty] private string _errorCarga = "";

    public bool   IsViewingContacts    => FabricanteSeleccionado is not null;
    public bool   HayContactoSeleccionado => ContactoSeleccionado is not null;
    public string Titulo => FabricanteSeleccionado is null
        ? "Fabricantes"
        : FabricanteSeleccionado.Nombre;

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

    public event Action?                SolicitarNuevoContacto;
    public event Action<ContactoFabricanteDto>? SolicitarEditarContacto;

    public ContactosFabricantesViewModel(
        IFabricanteRepository         fabRepo,
        IContactoFabricanteRepository contactoRepo,
        IRealtimeService              realtime,
        IConexionMonitor              conexionMonitor)
        : base(realtime, conexionMonitor)
    {
        _fabRepo      = fabRepo;
        _contactoRepo = contactoRepo;
    }

    public async Task CargarDatosAsync()
    {
        await CargarPaginaAsync();
        Observar("contactos_fabricante", OnCambioContacto);
    }

    protected override Task OnReconexionAsync() => CargarDatosAsync();

    private const int TimeoutMs = 10_000;

    private async Task CargarPaginaAsync()
    {
        int myGen  = ++_loadGeneration;
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var filtros = new FabricanteFiltros { IdEstado = Activo };
        var task    = _fabRepo.GetPagedAsync(_page, PageSize, filtros);

        if (await Task.WhenAny(task, Task.Delay(TimeoutMs)) != task)
        {
            if (myGen != _loadGeneration) return;
            ErrorCarga = "La carga tardó demasiado. Intente de nuevo.";
            IsLoading  = false;
            return;
        }

        var r = await task;
        if (myGen != _loadGeneration) return;

        if (!r.Success) { ErrorCarga = r.Error; IsLoading = false; return; }

        var pagina = r.Value!;
        TotalCount     = pagina.Total;
        ActivosCount   = pagina.Activos;
        InactivosCount = pagina.Inactivos;
        _filteredCount = pagina.Activos;

        PageRows = new ObservableCollection<FabricanteDto>(pagina.Items);
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;

        if (_pendingSelectionId.HasValue)
        {
            var fab = PageRows.FirstOrDefault(x => x.Id == _pendingSelectionId.Value);
            if (fab is not null) await AbrirFabricanteAsync(fab);
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
            Suggestions     = new ObservableCollection<FabricanteDto>();
            ShowSuggestions = false;
            return;
        }

        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            var r = await _fabRepo.BuscarSugerenciasAsync(q, new FabricanteFiltros { IdEstado = Activo }, token);
            if (token.IsCancellationRequested) return;
            if (!r.Success) { ShowSuggestions = false; return; }

            Suggestions     = new ObservableCollection<FabricanteDto>(r.Value!);
            ShowSuggestions = r.Value!.Count > 0;
            HighlightIndex  = -1;
        }
        catch (OperationCanceledException) { }
    }

    public void SeleccionarSugerencia(FabricanteDto fab)
    {
        // Cancelar el debounce en vuelo: como se asigna al campo _query y no a la
        // propiedad, no se pasa por el setter y nadie mas cancelaria el token. Sin
        // esto, una busqueda en curso termina despues de la seleccion y reabre el popup.
        _searchCts?.Cancel();

        _query = "";
        OnPropertyChanged(nameof(Query));
        ShowSuggestions = false;

        var enPagina = PageRows.FirstOrDefault(x => x.Id == fab.Id);
        if (enPagina is not null)
        {
            _ = AbrirFabricanteAsync(enPagina);
            return;
        }

        _pendingSelectionId = fab.Id;
        _ = NavegarAPaginaDeFabricanteAsync(fab.Id);
    }

    private async Task NavegarAPaginaDeFabricanteAsync(int id)
    {
        var r = await _fabRepo.GetPaginaDeRegistroAsync(id, PageSize, new FabricanteFiltros { IdEstado = Activo });
        if (!r.Success) { _pendingSelectionId = null; ErrorCarga = r.Error; return; }

        _page = r.Value;
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(TotalPages));
        NotifyPaginationCanExecuteChanged();
        await CargarPaginaAsync();
    }

    public async Task AbrirFabricanteAsync(FabricanteDto fab)
    {
        FabricanteSeleccionado = fab;
        ContactoSeleccionado   = null;
        await CargarContactosAsync();
    }

    public async Task CargarContactosAsync()
    {
        if (FabricanteSeleccionado is null) return;
        IsLoadingContactos = true;
        ErrorCarga         = string.Empty;

        var r = await _contactoRepo.GetByFabricanteAsync(FabricanteSeleccionado.Id);
        IsLoadingContactos = false;

        if (!r.Success) { ErrorCarga = r.Error; return; }
        Contactos = new ObservableCollection<ContactoFabricanteDto>(r.Value!);
    }

    [RelayCommand]
    private void Volver()
    {
        FabricanteSeleccionado = null;
        ContactoSeleccionado   = null;
        Contactos.Clear();
        ErrorCarga = string.Empty;
    }

    [RelayCommand]
    private void NuevoContacto() => SolicitarNuevoContacto?.Invoke();

    [RelayCommand(CanExecute = nameof(HayContactoSeleccionado))]
    private void EditarContacto()
    {
        if (ContactoSeleccionado is not null)
            SolicitarEditarContacto?.Invoke(ContactoSeleccionado);
    }

    [RelayCommand(CanExecute = nameof(HayContactoSeleccionado))]
    private async Task EliminarContacto()
    {
        if (ContactoSeleccionado is null) return;
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
        if (Disposed || FabricanteSeleccionado is null) return;
        if (!string.Equals(cambio.Operacion, "INSERT", StringComparison.OrdinalIgnoreCase)
         && !string.Equals(cambio.Operacion, "UPDATE", StringComparison.OrdinalIgnoreCase)) return;
        _ = CargarContactosAsync();
    }

    // ── IDisposable ────────────────────────────────────────────────

    protected override void OnDispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
