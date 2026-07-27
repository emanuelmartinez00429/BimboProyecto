using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Productos.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>
    /// Selector de productos con tabla, paginación y buscador — la "interfaz entre
    /// módulos" que trae el catálogo de Productos dentro del flujo de Pesaje.
    /// <para/>
    /// Reemplaza al viejo SeleccionarProductoModal (lista de 10 sin paginación).
    /// Arranca acotado al proveedor de la placa y permite ampliar a todo el catálogo.
    /// </summary>
    public partial class SelectorProductosModal : System.Windows.Controls.UserControl, IDisposable
    {
        private readonly SelectorProductosViewModel _vm;
        private bool _suppressAlcance;
        private bool _disposed;

        public event Action? Cerrado;
        public event Action<ProductoDto>? Seleccionado;

        public SelectorProductosModal(int? idProveedor, string proveedorNombre, IEnumerable<string> yaAgregados)
        {
            InitializeComponent();

            var repo = App.Services.GetRequiredService<IPickerProductoRepository>();
            _vm = new SelectorProductosViewModel(repo, idProveedor, proveedorNombre, yaAgregados);
            _vm.PropertyChanged += OnVmPropertyChanged;
            DataContext = _vm;

            // El SuggestionSearchBox se usa acá solo como caja de texto con estilo:
            // el popup de sugerencias no aplica porque los resultados van a la tabla.
            SearchBox.SetBinding(Core.Controls.SuggestionSearchBox.QueryProperty,
                new System.Windows.Data.Binding(nameof(SelectorProductosViewModel.Query))
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                });

            ConfigurarAlcance(idProveedor, proveedorNombre);
            Loaded += async (_, __) => await _vm.CargarAsync();
        }

        private void ConfigurarAlcance(int? idProveedor, string proveedorNombre)
        {
            _suppressAlcance = true;
            if (_vm.PuedeFiltrarPorProveedor)
            {
                TxtRbProveedor.Text = $"Solo {proveedorNombre}";
                // El texto se trunca si el nombre es largo: el tooltip lo muestra entero.
                RbProveedor.ToolTip = $"Mostrar solo productos de {proveedorNombre}";
                RbProveedor.IsChecked = true;
            }
            else
            {
                // Sin proveedor asociado no hay nada que acotar: se oculta el toggle.
                PanelAlcance.Visibility = Visibility.Collapsed;
                RbTodos.IsChecked = true;
            }
            _suppressAlcance = false;
        }

        // ── Sincronización VM → UI ──────────────────────────────────────────

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SelectorProductosViewModel.PageRows):   RefrescarTabla();     break;
                case nameof(SelectorProductosViewModel.IsLoading):  ActualizarCarga();    break;
                case nameof(SelectorProductosViewModel.ErrorCarga): ActualizarError();    break;
                case nameof(SelectorProductosViewModel.AlcanceTexto):
                    TxtAlcance.Text = _vm.AlcanceTexto;
                    break;
            }
        }

        private void ActualizarCarga()
        {
            PanelCargando.Visibility = _vm.IsLoading ? Visibility.Visible : Visibility.Collapsed;
            DgProductos.Visibility   = _vm.IsLoading ? Visibility.Collapsed : Visibility.Visible;
            if (_vm.IsLoading) TxtVacio.Visibility = Visibility.Collapsed;
        }

        private void ActualizarError()
        {
            bool hay = !string.IsNullOrEmpty(_vm.ErrorCarga);
            TxtError.Text        = _vm.ErrorCarga;
            PanelError.Visibility = hay ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefrescarTabla()
        {
            DgProductos.ItemsSource = _vm.PageRows;
            TxtVacio.Visibility     = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
            TxtPageInfo.Text        = _vm.PageInfo;
            TxtAlcance.Text         = _vm.AlcanceTexto;
            RefrescarPaginacion();
        }

        private void RefrescarPaginacion()
        {
            PaginacionPanel.Items.Clear();
            int total   = _vm.TotalPages;
            int current = _vm.Page;

            BtnPrimera.IsEnabled  = current > 1;
            BtnAnterior.IsEnabled = current > 1;
            BtnSiguiente.IsEnabled = current < total;
            BtnUltima.IsEnabled    = current < total;

            foreach (var p in CalcularPaginas(current, total))
            {
                if (p == -1)
                {
                    PaginacionPanel.Items.Add(new TextBlock
                    {
                        Text = "…",
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 12.5,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(3, 0, 3, 0),
                        Foreground = new SolidColorBrush(Color.FromArgb(0xA6, 0xFF, 0xFF, 0xFF)),
                    });
                }
                else
                {
                    var btn = new Button
                    {
                        Content = p.ToString(),
                        Margin  = new Thickness(3, 0, 0, 0),
                        Style   = (Style)(p == current
                            ? FindResource("SelPageBtnActivo")
                            : FindResource("SelPageBtn")),
                        Tag = p,
                    };
                    btn.Click += (s, _) =>
                    {
                        if (_vm.IsLoading) return;
                        if (s is Button b && b.Tag is int pg) _vm.Page = pg;
                    };
                    PaginacionPanel.Items.Add(btn);
                }
            }
        }

        /// <summary>Mismo algoritmo de ventana de páginas que el resto de los formularios.</summary>
        private static IEnumerable<int> CalcularPaginas(int current, int total)
        {
            if (total <= 7) return Enumerable.Range(1, total);

            var pages = new List<int> { 1 };
            if (current > 3) pages.Add(-1);
            for (int i = Math.Max(2, current - 1); i <= Math.Min(total - 1, current + 1); i++)
                pages.Add(i);
            if (current < total - 2) pages.Add(-1);
            pages.Add(total);
            return pages;
        }

        // ── Interacción ─────────────────────────────────────────────────────

        private void Alcance_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressAlcance) return;
            _vm.CambiarAlcance(RbProveedor.IsChecked == true);
        }

        private void Fila_Click(object sender, MouseButtonEventArgs e)
        {
            if (DgProductos.SelectedItem is not ProductoSeleccionable item) return;

            // Los ya agregados se ven atenuados y no se pueden volver a elegir.
            if (item.YaAgregado)
            {
                DgProductos.SelectedItem = null;
                return;
            }

            Seleccionado?.Invoke(item.Dto);
        }

        private void Primera_Click(object sender, RoutedEventArgs e)   => _vm.Page = 1;
        private void Anterior_Click(object sender, RoutedEventArgs e)  => _vm.Page = Math.Max(1, _vm.Page - 1);
        private void Siguiente_Click(object sender, RoutedEventArgs e) => _vm.Page = Math.Min(_vm.TotalPages, _vm.Page + 1);
        private void Ultima_Click(object sender, RoutedEventArgs e)    => _vm.Page = _vm.TotalPages;

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        /// <summary>
        /// Libera el debounce del VM. Lo llama quien hospeda el modal al cerrarlo —
        /// sin esto quedaría un CancellationTokenSource vivo por cada apertura.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.Dispose();
        }
    }
}
