using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

    public class ProductosViewModel : INotifyPropertyChanged
    {
        private List<Producto> _todos = new();
        private string _query = "";
        private EstadoFilter _estadoFiltro = EstadoFilter.Todos;
        private string _fabricanteFiltro = "";
        private string _paisFiltro = "";
        private int _page = 1;
        private Producto? _seleccionado;
        private bool _isLoading;
        private bool _showSuggestions;
        private int _highlightIndex = -1;
        private int _totalCount, _activosCount, _inactivosCount;
        private List<string> _fabricantes = new();
        private List<string> _paises = new();

        public const int PageSize = 50;

        private ObservableCollection<Producto> _pageRows = new();
        private ObservableCollection<Producto> _suggestions = new();

        public ObservableCollection<Producto> PageRows { get => _pageRows; private set { _pageRows = value; OnPropertyChanged(); } }
        public ObservableCollection<Producto> Suggestions { get => _suggestions; private set { _suggestions = value; OnPropertyChanged(); } }

        public string Query
        {
            get => _query;
            set { if (_query == value) return; _query = value; OnPropertyChanged(); RefrescarSugerencias(); }
        }

        public EstadoFilter EstadoFiltro
        {
            get => _estadoFiltro;
            set { if (_estadoFiltro == value) return; _estadoFiltro = value; OnPropertyChanged(); _page = 1; RefrescarTabla(); }
        }

        public string FabricanteFiltro
        {
            get => _fabricanteFiltro;
            set { if (_fabricanteFiltro == value) return; _fabricanteFiltro = value; OnPropertyChanged(); _page = 1; RefrescarTabla(); }
        }

        public string PaisFiltro
        {
            get => _paisFiltro;
            set { if (_paisFiltro == value) return; _paisFiltro = value; OnPropertyChanged(); _page = 1; RefrescarTabla(); }
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
                RefrescarTabla();
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

        public List<string> Fabricantes { get => _fabricantes; private set { _fabricantes = value; OnPropertyChanged(); } }
        public List<string> Paises { get => _paises; private set { _paises = value; OnPropertyChanged(); } }

        private IEnumerable<Producto> Filtrados => _todos.Where(MatchesFilters);
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Filtrados.Count() / (double)PageSize));
        public bool NoResults => !_isLoading && _todos.Count > 0 && !Filtrados.Any();

        public string PageInfo
        {
            get
            {
                var filtered = Filtrados.Count();
                if (filtered == 0) return "Sin resultados";
                int from = (_page - 1) * PageSize + 1;
                int to = Math.Min(_page * PageSize, filtered);
                return $"Mostrando {from}–{to} de {filtered} productos";
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

        public async Task CargarDatosAsync()
        {
            IsLoading = true;
            try
            {
                _todos = await RepositorioProducto.obtenerProductosJoin();
                Fabricantes    = _todos.Where(p => p.Fabricante != null).Select(p => p.Fabricante!.nombreFabricante).Distinct().OrderBy(x => x).ToList();
                Paises         = _todos.Where(p => p.Paises     != null).Select(p => p.Paises!.nombrePais).Distinct().OrderBy(x => x).ToList();
                TotalCount     = _todos.Count;
                ActivosCount   = _todos.Count(p => p.idEstado == 1);
                InactivosCount = _todos.Count(p => p.idEstado != 1);
                _page = 1;
                RefrescarTabla();
            }
            finally { IsLoading = false; }
        }

        public void RefrescarDatos() => _ = CargarDatosAsync();

        private void RefrescarTabla()
        {
            var rows = Filtrados.Skip((_page - 1) * PageSize).Take(PageSize).ToList();
            PageRows = new ObservableCollection<Producto>(rows);
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(NoResults));
        }

        private void RefrescarSugerencias()
        {
            var q = _query.Trim().ToLower();
            if (string.IsNullOrEmpty(q)) { Suggestions = new(); ShowSuggestions = false; return; }
            var sugs = _todos.Where(MatchesFilters)
                .Select(p => (p, score: Score(p, q))).Where(x => x.score >= 0)
                .OrderByDescending(x => x.score).ThenBy(x => x.p.nombreProducto)
                .Take(10).Select(x => x.p).ToList();
            Suggestions = new ObservableCollection<Producto>(sugs);
            ShowSuggestions = sugs.Count > 0;
            HighlightIndex  = sugs.Count > 0 ? 0 : -1;
        }

        public void SeleccionarSugerencia(Producto p)
        {
            Query = "";
            ShowSuggestions = false;
            var filtered = Filtrados.ToList();
            int idx = filtered.IndexOf(p);
            if (idx >= 0)
            {
                int targetPage = (idx / PageSize) + 1;
                if (targetPage != _page) { _page = targetPage; OnPropertyChanged(nameof(Page)); RefrescarTabla(); }
            }
            Seleccionado = p;
        }

        private void LimpiarFiltros()
        {
            _estadoFiltro = EstadoFilter.Todos; _fabricanteFiltro = ""; _paisFiltro = "";
            OnPropertyChanged(nameof(EstadoFiltro)); OnPropertyChanged(nameof(FabricanteFiltro)); OnPropertyChanged(nameof(PaisFiltro));
            _page = 1; RefrescarTabla();
        }

        private bool MatchesFilters(Producto p)
        {
            if (_estadoFiltro == EstadoFilter.Habilitados    && p.idEstado != 1) return false;
            if (_estadoFiltro == EstadoFilter.Deshabilitados && p.idEstado == 1) return false;
            if (!string.IsNullOrEmpty(_fabricanteFiltro) && p.Fabricante?.nombreFabricante != _fabricanteFiltro) return false;
            if (!string.IsNullOrEmpty(_paisFiltro)       && p.Paises?.nombrePais           != _paisFiltro)       return false;
            return true;
        }

        private static int Score(Producto p, string q)
        {
            var code = (p.codigoProducto ?? "").ToLower();
            var name = (p.nombreProducto ?? "").ToLower();
            if (code == q || name == q)                            return 100;
            if (code.StartsWith(q))                                return 80;
            if (name.StartsWith(q))                                return 70;
            if (name.Split(' ').Any(w => w.StartsWith(q)))         return 60;
            if (code.Contains(q))                                  return 40;
            if (name.Contains(q))                                  return 30;
            return -1;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
