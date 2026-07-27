using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Productos.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>
    /// Fila del selector: el DTO + lo que la tabla necesita mostrar.
    /// </summary>
    public class ProductoSeleccionable
    {
        public ProductoDto Dto      { get; init; } = null!;
        public string Codigo        { get; init; } = "";
        public string Nombre        { get; init; } = "";
        public string Fabricante    { get; init; } = "";
        public string Pais          { get; init; } = "";
        public string Contenido     { get; init; } = "";

        /// <summary>Ya está en el camión: se muestra atenuado y no se puede elegir.</summary>
        public bool   YaAgregado    { get; init; }
        public string EstadoTexto   => YaAgregado ? "Ya agregado" : "";
    }

    /// <summary>
    /// ViewModel del selector de productos con tabla, paginación y buscador.
    /// <para/>
    /// Deliberadamente NO hereda de RealtimeAwareViewModel ni reutiliza
    /// ProductosViewModel: ese VM se engancha al monitor de conexión en su
    /// constructor y se suscribe a Realtime en la carga, lo que dejaría
    /// suscripciones vivas cada vez que se abre este modal. Acá solo hace falta
    /// leer páginas del catálogo, así que es un ObservableObject plano.
    /// </summary>
    public partial class SelectorProductosViewModel : ObservableObject, IDisposable
    {
        private readonly IPickerProductoRepository _repo;
        private readonly int?        _idProveedor;
        private readonly HashSet<string> _yaAgregados;

        private CancellationTokenSource? _searchCts;
        private int  _loadGeneration;
        private bool _disposed;

        /// <summary>Página chica: el modal muestra menos filas que el formulario grande.</summary>
        public const int PageSize = 15;

        private string _query = "";
        private int    _page  = 1;
        private int    _total;

        [ObservableProperty] private ObservableCollection<ProductoSeleccionable> _pageRows = new();
        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _errorCarga = "";

        /// <summary>true = solo productos del proveedor del camión; false = todo el catálogo.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AlcanceTexto))]
        private bool _soloProveedor;

        public string ProveedorNombre { get; }

        /// <summary>Hay un proveedor al que acotar (si no, el toggle no tiene sentido).</summary>
        public bool PuedeFiltrarPorProveedor => _idProveedor.HasValue;

        public string AlcanceTexto => SoloProveedor
            ? $"Mostrando productos de {ProveedorNombre}"
            : "Mostrando todo el catálogo";

        public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_total / (double)PageSize));
        public bool NoResults  => !IsLoading && _total == 0;

        public string PageInfo
        {
            get
            {
                if (_total == 0) return "Sin resultados";
                int from = (_page - 1) * PageSize + 1;
                int to   = Math.Min(_page * PageSize, _total);
                return $"Mostrando {from}–{to} de {_total} productos";
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
                _ = BuscarConDebounceAsync();
            }
        }

        public int Page
        {
            get => _page;
            set
            {
                if (_page == value || value < 1) return;
                _page = value;
                OnPropertyChanged();
                _ = CargarPaginaAsync();
            }
        }

        public SelectorProductosViewModel(
            IPickerProductoRepository repo,
            int? idProveedor,
            string proveedorNombre,
            IEnumerable<string> yaAgregados)
        {
            _repo            = repo;
            _idProveedor     = idProveedor;
            ProveedorNombre  = proveedorNombre;
            _yaAgregados     = new HashSet<string>(yaAgregados, StringComparer.OrdinalIgnoreCase);
            _soloProveedor   = idProveedor.HasValue;   // arranca acotado, como el picker viejo
        }

        public Task CargarAsync() => CargarPaginaAsync();

        /// <summary>Alterna entre "solo proveedor" y "todo el catálogo" y recarga desde la página 1.</summary>
        public void CambiarAlcance(bool soloProveedor)
        {
            if (SoloProveedor == soloProveedor) return;
            SoloProveedor = soloProveedor;
            _page = 1;
            OnPropertyChanged(nameof(Page));
            _ = CargarPaginaAsync();
        }

        private async Task BuscarConDebounceAsync()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                _page = 1;
                OnPropertyChanged(nameof(Page));
                await CargarPaginaAsync();
            }
            catch (OperationCanceledException) { /* tecleo nuevo, se descarta esta búsqueda */ }
        }

        private async Task CargarPaginaAsync()
        {
            int myGen  = ++_loadGeneration;
            IsLoading  = true;
            ErrorCarga = string.Empty;

            int? proveedor = SoloProveedor ? _idProveedor : null;
            var r = await _repo.GetPagedAsync(proveedor, _query.Trim(), _page, PageSize);

            // Llegó tarde: hay otra carga más nueva en curso.
            if (myGen != _loadGeneration) return;

            if (!r.Success)
            {
                ErrorCarga = r.Error;
                IsLoading  = false;
                return;
            }

            var pagina = r.Value!;
            _total = pagina.Total;

            PageRows = new ObservableCollection<ProductoSeleccionable>(
                pagina.Items.Select(d => new ProductoSeleccionable
                {
                    Dto        = d,
                    Codigo     = d.CodigoInterno,
                    Nombre     = d.Nombre,
                    Fabricante = d.Fabricante,
                    Pais       = d.Pais,
                    Contenido  = d.Contenido,
                    YaAgregado = _yaAgregados.Contains(d.CodigoInterno),
                }));

            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(NoResults));
            IsLoading = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _searchCts?.Cancel();
            _searchCts?.Dispose();
        }
    }
}
