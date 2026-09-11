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
        }

        private void OnFiltrosLimpiados()
        {
            _filtroFabricante.Reiniciar();
            _filtroPais.Reiniciar();
            _filtroProveedor.Reiniciar();
            _filtroCategoria.Reiniciar();
        }

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(ProductosViewModel.Seleccionado): SeleccionarEnTabla(); break;
                // Fabricantes se repuebla también cuando cambia el proveedor:
                // el ViewModel reacota la lista y dispara este mismo aviso.
                case nameof(ProductosViewModel.Fabricantes):  _filtroFabricante.Poblar(_vm.Fabricantes); break;
                case nameof(ProductosViewModel.Paises):       _filtroPais.Poblar(_vm.Paises);            break;
                case nameof(ProductosViewModel.Proveedores):  _filtroProveedor.Poblar(_vm.Proveedores);  break;
                case nameof(ProductosViewModel.Categorias):   _filtroCategoria.Poblar(_vm.Categorias);   break;
            }
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
            if (_vm?.Seleccionado != null)
                DgProductos.ScrollIntoView(_vm.Seleccionado);
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
            var repo      = App.Services.GetRequiredService<IProductoRepository>();
            var catalogos = App.Services.GetRequiredService<CapaAplicacion.Common.Catalogos.ICatalogoRepository>();
            var modal     = new ProductoModal(repo, catalogos, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnProductoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(ProductoDto p)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarProducto)) return;
            var repo      = App.Services.GetRequiredService<IProductoRepository>();
            var catalogos = App.Services.GetRequiredService<CapaAplicacion.Common.Catalogos.ICatalogoRepository>();
            var modal     = new ProductoModal(repo, catalogos, p);
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
