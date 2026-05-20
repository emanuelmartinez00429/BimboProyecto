using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CapaDatos.Modelados.Productos;
using CapaDatos.Repositorios.productos_movimientos;
using Producto = CapaDatos.Modelados.Productos.Productos;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public enum EstadoFilter { Habilitados, Deshabilitados, Todos }

    public class ProductosRelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        public ProductosRelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public event EventHandler? CanExecuteChanged
        { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Item para los ComboBox de filtros: guarda Id + Nombre.
    /// </summary>
    public class FiltroItem
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = "";
        public override string ToString() => Nombre;
    }

    public class ProductosViewModel : INotifyPropertyChanged
    {
        private string _query = "";
        private EstadoFilter _estadoFiltro = EstadoFilter.Todos;
        private int? _fabricanteIdFiltro;
        private int? _paisIdFiltro;
        private int _page = 1;
        private Producto? _seleccionado;
        private bool _isLoading;
        private bool _showSuggestions;
        private int _highlightIndex = -1;
        private int _totalCount, _activosCount, _inactivosCount;
        private int _filteredCount;

        public const int PageSize = 50;

        private ObservableCollection<Producto> _pageRows = new();
        private ObservableCollection<Producto> _suggestions = new();

        private List<FiltroItem> _fabricantes = new();
        private List<FiltroItem> _paises = new();

        // Debounce para búsqueda de sugerencias
        private CancellationTokenSource? _searchCts;

        public ObservableCollection<Producto> PageRows { get => _pageRows; private set { _pageRows = value; OnPropertyChanged(); } }
        public ObservableCollection<Producto> Suggestions { get => _suggestions; private set { _suggestions = value; OnPropertyChanged(); } }

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
            set { if (_estadoFiltro == value) return; _estadoFiltro = value; OnPropertyChanged(); _page = 1; _ = CargarPaginaAsync(); }
        }

        public int? FabricanteIdFiltro
        {
            get => _fabricanteIdFiltro;
            set { if (_fabricanteIdFiltro == value) return; _fabricanteIdFiltro = value; OnPropertyChanged(); _page = 1; _ = CargarPaginaAsync(); }
        }

        public int? PaisIdFiltro
        {
            get => _paisIdFiltro;
            set { if (_paisIdFiltro == value) return; _paisIdFiltro = value; OnPropertyChanged(); _page = 1; _ = CargarPaginaAsync(); }
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
                _ = CargarPaginaAsync();
            }
        }

        public Producto? Seleccionado
        {
            get => _seleccionado;
            set { _seleccionado = value; OnPropertyChanged(); OnPropertyChanged(nameof(HaySeleccionado)); OnPropertyChanged(nameof(TextoSeleccionado)); ((ProductosRelayCommand)EditarCommand).RaiseCanExecuteChanged(); }
        }

        public bool HaySeleccionado => _seleccionado != null;
        public string TextoSeleccionado => _seleccionado == null ? "" : $"{_seleccionado.codigoProducto} · {_seleccionado.nombreProducto}";

        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
        public bool ShowSuggestions { get => _showSuggestions; set { _showSuggestions = value; OnPropertyChanged(); } }
        public int HighlightIndex { get => _highlightIndex; set { _highlightIndex = value; OnPropertyChanged(); } }

        public int TotalCount { get => _totalCount; private set { _totalCount = value; OnPropertyChanged(); } }
        public int ActivosCount { get => _activosCount; private set { _activosCount = value; OnPropertyChanged(); } }
        public int InactivosCount { get => _inactivosCount; private set { _inactivosCount = value; OnPropertyChanged(); } }

        public List<FiltroItem> Fabricantes { get => _fabricantes; private set { _fabricantes = value; OnPropertyChanged(); } }
        public List<FiltroItem> Paises { get => _paises; private set { _paises = value; OnPropertyChanged(); } }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
        public bool NoResults => !_isLoading && _filteredCount == 0 && _totalCount > 0;

        public string PageInfo
        {
            get
            {
                if (_filteredCount == 0) return "Sin resultados";
                int from = (_page - 1) * PageSize + 1;
                int to = Math.Min(_page * PageSize, _filteredCount);
                return $"Mostrando {from}–{to} de {_filteredCount} productos";
            }
        }

        public ICommand NuevoCommand { get; }
        public ICommand EditarCommand { get; }
        public ICommand LimpiarFiltrosCommand { get; }
        public ICommand SalirCommand { get; }
        public ICommand PrimeraPaginaCommand { get; }
        public ICommand PaginaAnteriorCommand { get; }
        public ICommand PaginaSiguienteCommand { get; }
        public ICommand UltimaPaginaCommand { get; }

        public event Action? SolicitarNuevo;
        public event Action<Producto>? SolicitarEditar;
        public event Action? SolicitarSalir;
        public event PropertyChangedEventHandler? PropertyChanged;

        public ProductosViewModel()
        {
            NuevoCommand           = new ProductosRelayCommand(_ => SolicitarNuevo?.Invoke());
            EditarCommand          = new ProductosRelayCommand(_ => { if (_seleccionado != null) SolicitarEditar?.Invoke(_seleccionado); }, _ => _seleccionado != null);
            LimpiarFiltrosCommand  = new ProductosRelayCommand(_ => LimpiarFiltros());
            SalirCommand           = new ProductosRelayCommand(_ => SolicitarSalir?.Invoke());
            PrimeraPaginaCommand   = new ProductosRelayCommand(_ => Page = 1,         _ => _page > 1);
            PaginaAnteriorCommand  = new ProductosRelayCommand(_ => Page--,            _ => _page > 1);
            PaginaSiguienteCommand = new ProductosRelayCommand(_ => Page++,            _ => _page < TotalPages);
            UltimaPaginaCommand    = new ProductosRelayCommand(_ => Page = TotalPages, _ => _page < TotalPages);
        }

        /// <summary>
        /// Carga inicial: filtros disponibles + primera página.
        /// </summary>
        public async Task CargarDatosAsync()
        {
            IsLoading = true;
            try
            {
                // Cargar listas de filtros (fabricantes y países)
                var (fabricantes, paises) = await RepositorioProducto.ObtenerFiltrosDisponiblesAsync();
                Fabricantes = fabricantes.Select(f => new FiltroItem { Id = f.id, Nombre = f.nombre }).ToList();
                Paises = paises.Select(p => new FiltroItem { Id = p.id, Nombre = p.nombre }).ToList();

                // Cargar primera página
                await CargarPaginaAsync();
            }
            finally { IsLoading = false; }
        }

        public void RefrescarDatos() => _ = CargarPaginaAsync();

        /// <summary>
        /// Consulta a Supabase solo los items de la página actual con filtros aplicados.
        /// </summary>
        private async Task CargarPaginaAsync()
        {
            IsLoading = true;
            try
            {
                int? idEstadoDb = _estadoFiltro switch
                {
                    EstadoFilter.Habilitados => 1,
                    EstadoFilter.Deshabilitados => 2,
                    _ => null
                };

                var pagina = await RepositorioProducto.ObtenerPaginaAsync(
                    _page, PageSize,
                    idEstado: idEstadoDb,
                    idFabricante: _fabricanteIdFiltro,
                    idPais: _paisIdFiltro);

                PageRows = new ObservableCollection<Producto>(pagina.Items);
                TotalCount = pagina.TotalCount;
                ActivosCount = pagina.ActivosCount;
                InactivosCount = pagina.InactivosCount;
                _filteredCount = idEstadoDb switch
                {
                    1 => pagina.ActivosCount,
                    2 => pagina.InactivosCount,
                    _ => pagina.TotalCount
                };

                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(NoResults));
            }
            finally { IsLoading = false; }
        }

        /// <summary>
        /// Busca sugerencias en Supabase con debounce de 300ms.
        /// </summary>
        private async Task RefrescarSugerenciasAsync()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            var q = _query.Trim();
            if (string.IsNullOrEmpty(q))
            {
                Suggestions = new();
                ShowSuggestions = false;
                return;
            }

            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                int? idEstadoDb = _estadoFiltro switch
                {
                    EstadoFilter.Habilitados => 1,
                    EstadoFilter.Deshabilitados => 2,
                    _ => null
                };

                var sugs = await RepositorioProducto.BuscarSugerenciasAsync(
                    q,
                    idEstado: idEstadoDb,
                    idFabricante: _fabricanteIdFiltro,
                    idPais: _paisIdFiltro);

                if (token.IsCancellationRequested) return;

                Suggestions = new ObservableCollection<Producto>(sugs);
                ShowSuggestions = sugs.Count > 0;
                HighlightIndex = sugs.Count > 0 ? 0 : -1;
            }
            catch (TaskCanceledException) { }
        }

        /// <summary>
        /// Selecciona un producto de las sugerencias.
        /// </summary>
        public void SeleccionarSugerencia(Producto p)
        {
            _query = "";
            OnPropertyChanged(nameof(Query));
            ShowSuggestions = false;

            // Buscar por ID en la página actual
            var enPagina = PageRows.FirstOrDefault(x => x.idProducto == p.idProducto);
            Seleccionado = enPagina ?? p;
        }

        private void LimpiarFiltros()
        {
            _estadoFiltro = EstadoFilter.Todos;
            _fabricanteIdFiltro = null;
            _paisIdFiltro = null;
            OnPropertyChanged(nameof(EstadoFiltro));
            OnPropertyChanged(nameof(FabricanteIdFiltro));
            OnPropertyChanged(nameof(PaisIdFiltro));
            _page = 1;
            _ = CargarPaginaAsync();
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
