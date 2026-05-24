using System.Collections.ObjectModel;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Productos.Queries;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Productos;

public enum EstadoFilter { Habilitados, Deshabilitados, Todos }

public partial class ProductosViewModel : ObservableObject
{
    private readonly IProductoRepository _repo;
    private CancellationTokenSource? _searchCts;

    private string       _query              = "";
    private EstadoFilter _estadoFiltro       = EstadoFilter.Habilitados;
    private int?         _fabricanteIdFiltro;
    private int?         _paisIdFiltro;
    private int          _page               = 1;
    private int          _filteredCount;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<ProductoDto> _pageRows    = new();
    [ObservableProperty] private ObservableCollection<ProductoDto> _suggestions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    private ProductoDto? _seleccionado;

    [ObservableProperty] private bool          _isLoading;
    [ObservableProperty] private bool          _showSuggestions;
    [ObservableProperty] private int           _highlightIndex = -1;
    [ObservableProperty] private int           _totalCount;
    [ObservableProperty] private int           _activosCount;
    [ObservableProperty] private int           _inactivosCount;
    [ObservableProperty] private List<FiltroItem> _fabricantes = new();
    [ObservableProperty] private List<FiltroItem> _paises      = new();
    [ObservableProperty] private string        _errorCarga    = "";

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
    public event Action?              SolicitarSalir;
    public event Action?              FiltrosLimpiados;

    public ProductosViewModel(IProductoRepository repo)
    {
        _repo = repo;
    }

    public async Task CargarDatosAsync()
    {
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var rFab = await _repo.GetFabricantesAsync();
        if (!rFab.Success) { ErrorCarga = rFab.Error; IsLoading = false; return; }
        Fabricantes = rFab.Value!.ToList();

        var rPaises = await _repo.GetPaisesAsync();
        if (!rPaises.Success) { ErrorCarga = rPaises.Error; IsLoading = false; return; }
        Paises = rPaises.Value!.ToList();

        await CargarPaginaAsync();
    }

    public void RefrescarDatos() => _ = CargarPaginaAsync();

    private async Task CargarPaginaAsync()
    {
        IsLoading = true;

        var filtros = BuildFiltros();
        var r       = await _repo.GetPagedAsync(_page, PageSize, filtros);

        if (!r.Success)
        {
            ErrorCarga = r.Error;
            IsLoading  = false;
            return;
        }

        var pagina     = r.Value!;
        ErrorCarga     = string.Empty;
        PageRows       = new ObservableCollection<ProductoDto>(pagina.Items);
        TotalCount     = pagina.Total;
        ActivosCount   = pagina.Activos;
        InactivosCount = pagina.Inactivos;
        _filteredCount = filtros.IdEstado switch
        {
            1 => pagina.Activos,
            2 => pagina.Inactivos,
            _ => pagina.Total
        };

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;
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
            HighlightIndex  = r.Value!.Count > 0 ? 0 : -1;
        }
        catch (OperationCanceledException) { }
    }

    public void SeleccionarSugerencia(ProductoDto p)
    {
        _query = "";
        OnPropertyChanged(nameof(Query));
        ShowSuggestions = false;
        var enPagina = PageRows.FirstOrDefault(x => x.Id == p.Id);
        Seleccionado = enPagina ?? p;
    }

    private ProductoFiltros BuildFiltros() => new()
    {
        IdEstado = _estadoFiltro switch
        {
            EstadoFilter.Habilitados    => 1,
            EstadoFilter.Deshabilitados => 2,
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

    [RelayCommand]
    private void Salir() => SolicitarSalir?.Invoke();

    [RelayCommand(CanExecute = nameof(PuedePaginaAnterior))]
    private void PrimeraPagina() => Page = 1;

    [RelayCommand(CanExecute = nameof(PuedePaginaAnterior))]
    private void PaginaAnterior() => Page--;

    [RelayCommand(CanExecute = nameof(PuedePaginaSiguiente))]
    private void PaginaSiguiente() => Page++;

    [RelayCommand(CanExecute = nameof(PuedePaginaSiguiente))]
    private void UltimaPagina() => Page = TotalPages;

    private bool PuedePaginaAnterior()  => _page > 1;
    private bool PuedePaginaSiguiente() => _page < TotalPages;

    private void NotifyPaginationCanExecuteChanged()
    {
        PrimeraPaginaCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        UltimaPaginaCommand.NotifyCanExecuteChanged();
    }
}
