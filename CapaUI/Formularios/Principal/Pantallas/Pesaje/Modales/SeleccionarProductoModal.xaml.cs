using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Productos.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    public partial class SeleccionarProductoModal : UserControl
    {
        private sealed class PickerItem
        {
            public ProductoDto Dto    { get; init; } = null!;
            public string      Codigo { get; init; } = "";
            public string      Nombre { get; init; } = "";
            public string      Meta   { get; init; } = "";
            public bool        Dup    { get; init; }
        }

        private readonly IPickerProductoRepository _repo;
        private readonly int?   _idProveedor;
        private readonly string _proveedorNombre;
        private readonly HashSet<string> _yaAgregados;
        private CancellationTokenSource? _cts;
        private bool _modoTodos;

        public event Action? Cerrado;
        public event Action<ProductoDto>? Seleccionado;

        public SeleccionarProductoModal(int? idProveedor, string proveedorNombre, IEnumerable<string> yaAgregados)
        {
            InitializeComponent();
            _repo            = App.Services.GetRequiredService<IPickerProductoRepository>();
            _idProveedor     = idProveedor;
            _proveedorNombre = proveedorNombre;
            _yaAgregados     = new HashSet<string>(yaAgregados, StringComparer.OrdinalIgnoreCase);
            _modoTodos       = !idProveedor.HasValue;

            if (!idProveedor.HasValue)
                BtnAlcance.Visibility = Visibility.Collapsed; // sin proveedor: solo global

            ActualizarModoUI();
            Loaded += async (_, __) => { TxtBuscar.Focus(); await BuscarAsync(TxtBuscar.Text); };
        }

        private void ActualizarModoUI()
        {
            if (_modoTodos)
            {
                TxtModo.Text     = "Todos los proveedores";
                TxtAlcance.Text  = $"Buscar solo del proveedor: {_proveedorNombre}";
            }
            else
            {
                TxtModo.Text     = $"Proveedor: {_proveedorNombre}";
                TxtAlcance.Text  = "Buscar en todos los proveedores";
            }
        }

        private async void BtnAlcance_Click(object sender, RoutedEventArgs e)
        {
            if (!_idProveedor.HasValue) return;
            _modoTodos = !_modoTodos;
            ActualizarModoUI();
            await BuscarAsync(TxtBuscar.Text);
        }

        private async void Buscar_Changed(object sender, TextChangedEventArgs e)
            => await BuscarAsync(TxtBuscar.Text);

        private async Task BuscarAsync(string termino)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try { await Task.Delay(300, token); }
            catch (OperationCanceledException) { return; }

            termino = (termino ?? "").Trim();

            try
            {
                IReadOnlyList<ProductoDto> datos;

                if (_modoTodos)
                {
                    if (termino.Length < 2)
                    {
                        MostrarEstado("Escribe al menos 2 caracteres para buscar en todo el catálogo.");
                        return;
                    }
                    var r = await _repo.BuscarTodosAsync(termino, token);
                    if (token.IsCancellationRequested) return;
                    datos = r.Success ? r.Value! : Array.Empty<ProductoDto>();
                }
                else if (termino.Length == 0)
                {
                    var r = await _repo.TopPorProveedorAsync(_idProveedor!.Value, 10, token);
                    if (token.IsCancellationRequested) return;
                    datos = r.Success ? r.Value! : Array.Empty<ProductoDto>();
                }
                else
                {
                    var r = await _repo.BuscarPorProveedorAsync(termino, _idProveedor!.Value, token);
                    if (token.IsCancellationRequested) return;
                    datos = r.Success ? r.Value! : Array.Empty<ProductoDto>();
                }

                Render(datos);
            }
            catch (OperationCanceledException) { /* búsqueda superada */ }
        }

        private void Render(IReadOnlyList<ProductoDto> datos)
        {
            var items = datos.Select(d => new PickerItem
            {
                Dto    = d,
                Codigo = d.CodigoInterno,
                Nombre = d.Nombre,
                Dup    = _yaAgregados.Contains(d.CodigoInterno),
                Meta   = _yaAgregados.Contains(d.CodigoInterno) ? "Ya agregado" : d.Fabricante,
            }).ToList();

            LstResultados.ItemsSource = items;
            if (items.Count == 0) MostrarEstado("Sin resultados.");
            else TxtEstado.Visibility = Visibility.Collapsed;
        }

        private void MostrarEstado(string texto)
        {
            LstResultados.ItemsSource = null;
            TxtEstado.Text = texto;
            TxtEstado.Visibility = Visibility.Visible;
        }

        private void Item_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is PickerItem item && !item.Dup)
                Seleccionado?.Invoke(item.Dto);
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
    }
}
