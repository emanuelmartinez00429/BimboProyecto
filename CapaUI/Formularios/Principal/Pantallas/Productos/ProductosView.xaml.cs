using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Productos.Queries;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using Microsoft.Extensions.DependencyInjection;
using static CapaAplicacion.Common.EstadoRegistro;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public partial class ProductosView : System.Windows.Controls.UserControl
    {
        private ProductosViewModel _vm = null!;
        private bool _suppressFilterChange = false;
        private Storyboard? _spinnerStory;

        // Combos de filtro: toda la mecánica (sentinela "(Todos)", autocompletado
        // en memoria, limpieza) vive en ComboFiltro, no replicada por combo.
        private readonly ComboFiltro _filtroFabricante;
        private readonly ComboFiltro _filtroPais;
        private readonly ComboFiltro _filtroProveedor;
        private readonly ComboFiltro _filtroCategoria;

        public ProductosView()
        {
            InitializeComponent();

            ScrollHorizontalConShift.Habilitar(DgProductos);

            _filtroFabricante = new ComboFiltro(CmbFabricante);
            _filtroPais       = new ComboFiltro(CmbPais);
            _filtroProveedor  = new ComboFiltro(CmbProveedor);
            _filtroCategoria  = new ComboFiltro(CmbCategoria);

            _filtroFabricante.SeleccionCambiada += id => { if (_vm != null) _vm.FabricanteIdFiltro = id; };
            _filtroPais.SeleccionCambiada       += id => { if (_vm != null) _vm.PaisIdFiltro       = id; };
            _filtroProveedor.SeleccionCambiada  += id => { if (_vm != null) _vm.ProveedorIdFiltro  = id; };
            _filtroCategoria.SeleccionCambiada  += id => { if (_vm != null) _vm.CategoriaIdFiltro  = id; };
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Guard: Loaded puede dispararse varias veces (re-parenting en WPF).
            // Sin este guard, dos VMs se suscriben al mismo canal Realtime y el primero
            // queda retenido para siempre en RealtimeService._suscriptores.
            if (_vm != null) return;

            _vm = App.CrearVm<ProductosViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;
            DgProductos.ItemsSource = _vm.PageRows;

            try
            {
                var vm = _vm;
                await vm.CargarDatosAsync();
            }
            catch (OperationCanceledException)
            {
                // Navegación rápida: la vista se descargó mientras cargaba.
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Productos] Falló la carga inicial de la pantalla");
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SolicitarNuevo   -= AbrirModalNuevo;
            _vm.SolicitarEditar  -= AbrirModalEditar;
            _vm.FiltrosLimpiados -= OnFiltrosLimpiados;
            _vm.PropertyChanged  -= OnVmPropertyChanged;
            _vm.Dispose();
            DataContext = null;
            _vm = null!;          // permite recrear limpio si el control vuelve al árbol
            DetenerSpinner();     // cierra el Storyboard para liberar SpinnerPath
        }

        private void OnFiltrosLimpiados()
        {
            _suppressFilterChange = true;
            RbActivos.IsChecked = true;
            RbOrdenId.IsChecked     = true;
            _filtroFabricante.Reiniciar();
            _filtroPais.Reiniciar();
            _filtroProveedor.Reiniciar();
            _filtroCategoria.Reiniciar();
            _suppressFilterChange = false;
        }

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(ProductosViewModel.PageRows):
                    DgProductos.ItemsSource = _vm.PageRows;   // único punto donde hay filas nuevas
                    break;
                case nameof(ProductosViewModel.IsLoading):       ActualizarCarga();       break;
                case nameof(ProductosViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ProductosViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ProductosViewModel.Seleccionado):    SeleccionarEnTabla();    break;
                // Fabricantes se repuebla también cuando cambia el proveedor:
                // el ViewModel reacota la lista y dispara este mismo aviso.
                case nameof(ProductosViewModel.Fabricantes):  _filtroFabricante.Poblar(_vm.Fabricantes); break;
                case nameof(ProductosViewModel.Paises):       _filtroPais.Poblar(_vm.Paises);            break;
                case nameof(ProductosViewModel.Proveedores):  _filtroProveedor.Poblar(_vm.Proveedores);  break;
                case nameof(ProductosViewModel.Categorias):   _filtroCategoria.Poblar(_vm.Categorias);   break;
            }
        }

        // ── Loading state ─────────────────────────────────────────────

        private void ActualizarCarga()
        {
            if (_vm == null) return;
            if (_vm.IsLoading)
            {
                DgProductos.Visibility  = Visibility.Collapsed;
                EmptyState.Visibility   = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                DetenerSpinner();
                DgProductos.Visibility  = Visibility.Visible;
                EmptyState.Visibility   = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ── Spinner ───────────────────────────────────────────────────

        private void IniciarSpinner()
        {
            if (_spinnerStory != null) return;
            _spinnerStory = new Storyboard();
            var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
            { RepeatBehavior = RepeatBehavior.Forever };
            Storyboard.SetTarget(anim, SpinnerPath);
            Storyboard.SetTargetProperty(anim,
                new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _spinnerStory.Children.Add(anim);
            _spinnerStory.Begin();
        }

        private void DetenerSpinner()
        {
            if (_spinnerStory is null) return;
            _spinnerStory.Stop();
            _spinnerStory.Remove();        // desasocia el clock del elemento destino
            _spinnerStory.Children.Clear(); // corta la referencia a SpinnerPath
            _spinnerStory = null;
        }

        // ── Filters ───────────────────────────────────────────────────

        private void EstadoFiltro_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            if (RbActivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoFilter.Activos;
            else if (RbInactivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoFilter.Inactivos;
            else
                _vm.EstadoFiltro = EstadoFilter.Todos;
        }

        /// <summary>
        /// Orden alfabético. El listado y el salto de página del buscador leen
        /// el mismo valor desde el ViewModel, así que no pueden divergir.
        /// </summary>
        private void Orden_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;

            _vm.Orden = RbOrdenAZ.IsChecked == true  ? OrdenProducto.NombreAsc
                      : RbOrdenZA.IsChecked == true  ? OrdenProducto.NombreDesc
                      : OrdenProducto.IdAsc;
        }

        // ── Search ────────────────────────────────────────────────────

        // El popup se alimenta por binding (SuggestItems="{Binding SuggestItems}").
        // Acá solo queda la reacción de la tabla, que es responsabilidad de la vista.
        //
        // Elegir una sugerencia abre el modal de edición de una — no hace falta
        // el paso intermedio de buscar la fila y tocar "Editar". El DTO ya viene
        // completo en la sugerencia, así que el modal no espera el salto de
        // página que SeleccionarSugerencia dispara para ubicarlo en la grilla.
        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            var producto = (ProductoDto)e.Source;
            _vm.SeleccionarSugerencia(producto);
            SeleccionarEnTabla();
            AbrirModalEditar(producto);
        }

        private void SeleccionarEnTabla()
        {
            if (_vm.Seleccionado == null) return;
            if (DgProductos.SelectedItem == _vm.Seleccionado) return;
            DgProductos.SelectedItem = _vm.Seleccionado;
            DgProductos.ScrollIntoView(_vm.Seleccionado);
        }

        // ── Table ─────────────────────────────────────────────────────

        private void DgProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgProductos.SelectedItem as ProductoDto;
        }

        private void DgProductos_MouseDoubleClick(object sender, WpfMouseButton e)
        {
            if (_vm?.Seleccionado != null)
                AbrirModalEditar(_vm.Seleccionado);
        }

        // Enter con una fila seleccionada abre el modal de edición, igual que el
        // doble clic — antes Enter solo hacía la navegación de celda por defecto
        // de WPF (sin efecto real acá, la grilla es IsReadOnly).
        private void DgProductos_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || _vm?.Seleccionado == null) return;
            e.Handled = true;
            AbrirModalEditar(_vm.Seleccionado);
        }



        // ── Modal ─────────────────────────────────────────────────────

        private void AbrirModalNuevo()
        {
            if (!SesionPermisos.Tiene(Permiso.CrearProducto)) return;
            var repo  = App.Services.GetRequiredService<IProductoRepository>();
            var modal = new ProductoModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnProductoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(ProductoDto p)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarProducto)) return;
            var repo  = App.Services.GetRequiredService<IProductoRepository>();
            var modal = new ProductoModal(repo, p);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnProductoGuardado;
            MostrarModal(modal);
        }

        private void MostrarModal(System.Windows.Controls.UserControl modal)
        {
            CapaUI.Core.ModalLayout.LimitarAlOverlay(modal, ModalOverlay); // ADR-028: el limite lo pone el overlay, no un ancestro
            ModalContent.Content    = modal;
            ModalOverlay.Opacity    = 1;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void CerrarModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalContent.Content    = null;
        }

        private void OnProductoGuardado()
        {
            CerrarModal();
            _vm.RefrescarTrasGuardar();
        }
    }
}
