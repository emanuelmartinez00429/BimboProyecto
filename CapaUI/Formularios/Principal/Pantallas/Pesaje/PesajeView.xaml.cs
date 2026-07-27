using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CapaAplicacion.Proveedores.Interfaces;
using CapaAplicacion.Proveedores.Queries;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje
{
    public partial class PesajeView : UserControl
    {
        private PesajeViewModel _vm = null!;
        private bool _sync;
        private Action? _pendingConfirm;
        private int _modalGen;

        /// <summary>
        /// El usuario pidió ver los camiones cerrados aunque no haya ninguno abierto:
        /// se oculta el estado vacío hasta que vuelva a cargarse la pantalla.
        /// </summary>
        private bool _verHistorico;

        /// <summary>Animación del spinner de la carga inicial.</summary>
        private Storyboard? _spinnerCarga;

        public PesajeView() => InitializeComponent();

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;
            _vm = App.Services.GetRequiredService<PesajeViewModel>();
            _vm.Toast           += MostrarToast;
            _vm.PropertyChanged += (_, __) => ActualizarUI();
            DataContext = _vm;

            await _vm.CargarAsync();

            _sync = true;
            LstCamiones.SelectedItem = _vm.SelectedCamion;
            _sync = false;
            ActualizarUI();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            DetenerSpinnerCarga();   // si se sale mientras cargaba, no dejar la animación viva
            if (_vm == null) return;
            _vm.Toast -= MostrarToast;
            DataContext = null;
            _vm = null!;
        }

        // ── Sincronización de estados/empty-states ─────────────────────────────
        private void ActualizarUI()
        {
            if (_vm == null) return;

            bool hayCamion   = _vm.HayCamion;
            bool cerrado     = _vm.CamionCerrado;
            bool hayProducto = _vm.HayProducto;
            bool prodAbierto = _vm.SelectedProducto?.Estado == "Abierto";
            bool hayEntrada  = _vm.SelectedEntrada != null;

            bool sinProductos = !hayCamion || (_vm.SelectedCamion!.Productos.Count == 0);
            MovEmpty.Visibility    = sinProductos ? Visibility.Visible : Visibility.Collapsed;
            DgProductos.Visibility = sinProductos ? Visibility.Collapsed : Visibility.Visible;
            MovEmpty.Text = !hayCamion
                ? "Selecciona un camión para ver sus productos"
                : "Este camión no tiene productos agregados todavía";

            bool sinEntradas = _vm.FilasEntradas.Count == 0;
            EntEmpty.Visibility   = sinEntradas ? Visibility.Visible : Visibility.Collapsed;
            DgEntradas.Visibility = sinEntradas ? Visibility.Collapsed : Visibility.Visible;
            EntEmpty.Text = !hayCamion
                ? "Selecciona un camión para ver sus pesajes"
                : "Aún no hay pesajes registrados";

            BtnCamionAgregar.IsEnabled   = _vm.PuedeAgregarCamion;
            BtnCamionEditar.IsEnabled    = hayCamion && !cerrado;
            BtnCamionQuitar.IsEnabled    = hayCamion;
            BtnCamionDescargar.IsEnabled = hayCamion && !cerrado;
            BtnCerrarTodos.IsEnabled     = _vm.CamionesActivos > 0;

            // Agregar/editar/quitar productos vive en el megamodal; acá solo se pesa.
            BtnProdPesar.IsEnabled = hayProducto && prodAbierto && !cerrado;

            BtnEntEditar.IsEnabled = hayEntrada && !cerrado;
            BtnEntQuitar.IsEnabled = hayEntrada && !cerrado;

            ActualizarEstadoVacio();
        }

        /// <summary>
        /// Muestra el estado vacío cuando no hay ningún camión descargándose.
        /// Si existen camiones cerrados, ofrece el enlace para verlos sin salir.
        /// <para/>
        /// Mientras carga se muestra el indicador de carga en su lugar: el VM ya
        /// devuelve MostrarEstadoVacio=false durante la carga para no mostrar el
        /// formulario de iniciar descarga con datos a medio traer.
        /// </summary>
        private void ActualizarEstadoVacio()
        {
            if (_vm == null) return;

            // Solo en la primera carga: en recargas ya hay contenido y taparlo parpadearía.
            bool cargandoPrimeraVez = _vm.IsLoading && _vm.Camiones.Count == 0;
            CargandoInicial.Visibility = cargandoPrimeraVez ? Visibility.Visible : Visibility.Collapsed;
            if (cargandoPrimeraVez) IniciarSpinnerCarga(); else DetenerSpinnerCarga();

            bool vacio = _vm.MostrarEstadoVacio && !_verHistorico;
            EstadoVacio.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;

            if (!vacio) return;

            BtnVerCerrados.Visibility = _vm.HayCerrados ? Visibility.Visible : Visibility.Collapsed;
            TxtVerCerrados.Text = _vm.CerradosCount == 1
                ? "Hay 1 camión cerrado — ver historial"
                : $"Hay {_vm.CerradosCount} camiones cerrados — ver historial";
        }

        private void BtnVerCerrados_Click(object sender, RoutedEventArgs e)
        {
            _verHistorico = true;
            ActualizarEstadoVacio();
        }

        // ── Spinner de la carga inicial ────────────────────────────────────
        // Mismo patrón que el resto de los formularios (Productos, Usuarios…):
        // Storyboard guardado en campo para poder detenerlo y liberarlo.

        private void IniciarSpinnerCarga()
        {
            if (_spinnerCarga != null) return;
            _spinnerCarga = new Storyboard();
            var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
            { RepeatBehavior = RepeatBehavior.Forever };
            Storyboard.SetTarget(anim, SpinnerCarga);
            Storyboard.SetTargetProperty(anim,
                new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _spinnerCarga.Children.Add(anim);
            _spinnerCarga.Begin();
        }

        private void DetenerSpinnerCarga()
        {
            if (_spinnerCarga is null) return;
            _spinnerCarga.Stop();
            _spinnerCarga.Remove();
            _spinnerCarga.Children.Clear();
            _spinnerCarga = null;
        }

        // ── Selección ──────────────────────────────────────────────────────────
        private async void LstCamiones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sync || _vm == null) return;
            await _vm.SeleccionarCamionAsync(LstCamiones.SelectedItem as CamionPesaje);
            ActualizarUI();
        }

        private void LstCamiones_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.SelectedCamion != null && !_vm.CamionCerrado)
                AbrirProcesoModal(ModoProceso.Edicion, _vm.SelectedCamion);
        }

        private void DgProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sync || _vm == null) return;
            _vm.SeleccionarProducto(DgProductos.SelectedItem as ProductoCamion);
            ActualizarUI();
        }

        private void DgProductos_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.SelectedProducto is { } p && p.Estado == "Abierto" && !_vm.CamionCerrado)
                AbrirPesajeModal(p, null);
        }

        private void DgEntradas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sync || _vm == null) return;
            _vm.SelectedEntrada = DgEntradas.SelectedItem as EntradaPesaje;
            ActualizarUI();
        }

        private async void EstadoProducto_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ProductoCamion p)
            {
                await _vm.ToggleEstadoProductoAsync(p);
                ActualizarUI();
                e.Handled = true;
            }
        }

        private void RbVista_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            string modo = RbVistaCamion.IsChecked == true ? "camion" : "producto";
            ColEntProducto.Visibility = modo == "camion" ? Visibility.Visible : Visibility.Collapsed;
            _vm.CambiarVista(modo);
            ActualizarUI();
        }

        // ── Camiones ─────────────────────────────────────────────────────────
        private void BtnCerrarTodos_Click(object sender, RoutedEventArgs e)
        {
            PedirConfirmacion(BtnCerrarTodos,
                $"¿Cerrar los {_vm.CamionesActivos} camiones abiertos y generar sus reportes?",
                () => _ = CerrarTodosFlujo());
        }

        private async Task CerrarTodosFlujo()
        {
            var cerradas = await _vm.CerrarTodosAsync();
            ActualizarUI();
            if (cerradas.Count > 0) AbrirReporte(cerradas);
        }

        /// <summary>Alta guiada: abre el proceso en modo wizard.</summary>
        private void BtnNuevoProceso_Click(object sender, RoutedEventArgs e)
            => AbrirProcesoModal(ModoProceso.Wizard, null);

        /// <summary>
        /// Único botón de edición de la pantalla: abre el megamodal con TODO el
        /// proceso del camión seleccionado (datos, productos y tara extra).
        /// </summary>
        private void BtnEditarProceso_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedCamion != null && !_vm.CamionCerrado)
                AbrirProcesoModal(ModoProceso.Edicion, _vm.SelectedCamion);
        }

        private void BtnCamionQuitar_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedCamion == null) return;
            PedirConfirmacion(BtnCamionQuitar,
                $"¿Quitar el camión {_vm.SelectedCamion.Placa}? Se perderán sus productos y pesajes.",
                () => _ = QuitarCamionFlujo());
        }

        private async Task QuitarCamionFlujo()
        {
            await _vm.QuitarCamionAsync();
            SincronizarSeleccion();
            ActualizarUI();
        }

        private async void BtnCamionDescargar_Click(object sender, RoutedEventArgs e)
        {
            var camion = _vm.SelectedCamion;
            if (camion == null || _vm.CamionCerrado) return;
            bool ok = await _vm.DescargarCamionAsync();
            ActualizarUI();
            if (ok) AbrirReporte(new List<CamionPesaje> { camion });
        }

        // ── Productos ────────────────────────────────────────────────────────
        // Agregar/editar/quitar productos se hace desde el megamodal ("Editar proceso"),
        // donde además se respeta la regla de no quitar un producto ya pesado.

        private void BtnProdPesar_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedProducto is { } p && p.Estado == "Abierto" && !_vm.CamionCerrado)
                AbrirPesajeModal(p, null);
        }

        // ── Entradas ─────────────────────────────────────────────────────────
        private void BtnEntEditar_Click(object sender, RoutedEventArgs e)
        {
            var ent = _vm.SelectedEntrada;
            if (ent == null || _vm.SelectedCamion == null) return;
            var prod = _vm.SelectedCamion.Productos.FirstOrDefault(p => p.Id == ent.ProdId);
            if (prod != null) AbrirPesajeModal(prod, ent);
        }

        private void BtnEntQuitar_Click(object sender, RoutedEventArgs e)
        {
            var ent = _vm.SelectedEntrada;
            if (ent == null) return;
            PedirConfirmacion(BtnEntQuitar,
                "¿Quitar esta entrada de pesaje? Se recalculará lo recibido.",
                () => _ = QuitarEntradaFlujo(ent));
        }

        private async Task QuitarEntradaFlujo(EntradaPesaje ent)
        {
            await _vm.QuitarEntradaAsync(ent);
            ActualizarUI();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Modales
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Abre el proceso de descarga: en modo Wizard para dar de alta una descarga
        /// nueva, o en modo Edición (megamodal) para corregir la del camión elegido.
        /// Unifica lo que antes eran CamionModal + ProductoCamionModal + el picker.
        /// </summary>
        private async void AbrirProcesoModal(ModoProceso modo, CamionPesaje? camion)
        {
            var proveedores = await CargarProveedoresAsync();
            var modal = new ProcesoDescargaModal(modo, camion, proveedores);

            modal.Cerrado += CerrarModal;
            modal.Confirmado += async r =>
            {
                var productos = r.Productos
                    .Select(p => (p.IdMovProducto, p.IdProducto, p.PesoManifestado, p.BultosDeclarados))
                    .ToList();

                bool ok = await _vm.GuardarProcesoAsync(
                    camion, r.Placa, r.Proveedor, r.IdProveedor, r.Observaciones,
                    r.TaraExtraTotal, productos, modal.IdsProductosQuitados);

                if (!ok) return;   // el VM ya avisó por Toast; el modal queda abierto

                _verHistorico = false;
                CerrarModal();
                SincronizarSeleccion();
                ActualizarUI();
            };

            MostrarModal(modal);
        }

        private void AbrirPesajeModal(ProductoCamion producto, EntradaPesaje? editInitial)
        {
            if (_vm.SelectedCamion == null) return;
            var modal = new PesajeModal(_vm.SelectedCamion, producto, editInitial);
            modal.Cerrado += CerrarModal;
            modal.GuardarYSeguir += async snap =>
            {
                await _vm.GuardarEntradaAsync(producto, snap, editInitial);
                CerrarModal();
                SincronizarSeleccion();
                ActualizarUI();
            };
            modal.CerrarCamion += async snap =>
            {
                if (snap != null) await _vm.GuardarEntradaAsync(producto, snap, editInitial);
                var camion = _vm.SelectedCamion;
                bool ok = await _vm.DescargarCamionAsync();
                ActualizarUI();
                if (ok && camion != null) AbrirReporte(new List<CamionPesaje> { camion });
                else CerrarModal();
            };
            MostrarModal(modal);
        }

        private void AbrirReporte(List<CamionPesaje> camiones)
        {
            var modal = new ReporteModal(camiones);
            modal.Cerrado += CerrarModal;
            MostrarModal(modal);
        }

        private async Task<List<ProveedorItem>> CargarProveedoresAsync()
        {
            try
            {
                var repo = App.Services.GetRequiredService<IProveedorRepository>();
                var r = await repo.GetPagedAsync(1, 200, new ProveedorFiltros { IdEstado = 1 });
                if (r.Success && r.Value != null)
                    return r.Value.Items.Select(p => new ProveedorItem(p.Id, p.Nombre)).ToList();
            }
            catch { /* combo vacío si falla */ }
            return new List<ProveedorItem>();
        }

        private void SincronizarSeleccion()
        {
            _sync = true;
            LstCamiones.SelectedItem = _vm.SelectedCamion;
            if (_vm.SelectedProducto != null) DgProductos.SelectedItem = _vm.SelectedProducto;
            _sync = false;
        }

        // ── Overlay + animación fade/pop ───────────────────────────────────────
        private void MostrarModal(UserControl modal)
        {
            _modalGen++;
            ModalContent.Content = modal;
            ModalContent.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = new ScaleTransform(0.94, 0.94);
            ModalContent.RenderTransform = scale;

            ModalOverlay.Opacity = 0;
            ModalOverlay.Visibility = Visibility.Visible;
            ModalOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));

            var pop = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        private void CerrarModal()
        {
            int gen = ++_modalGen;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
            fade.Completed += (_, __) =>
            {
                if (gen != _modalGen) return;
                ModalOverlay.Visibility = Visibility.Collapsed;
                ModalContent.Content = null;
            };
            ModalOverlay.BeginAnimation(OpacityProperty, fade);
        }

        // ── Toast ──────────────────────────────────────────────────────────────
        private void MostrarToast(string mensaje)
        {
            var border = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString("#1A1F2E")!,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 9, 18, 9),
                Margin = new Thickness(0, 8, 0, 0),
                Opacity = 0,
            };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.4, Color = Colors.Black };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Path
            {
                Data = Geometry.Parse("M20,6 L9,17 l-5,-5"),
                Stroke = (Brush)new BrushConverter().ConvertFromString("#4ADE80")!,
                StrokeThickness = 2.4, Width = 14, Height = 14, Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            });
            sp.Children.Add(new TextBlock
            {
                Text = mensaje, Foreground = Brushes.White, FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center,
            });
            border.Child = sp;

            var trans = new TranslateTransform(0, 8);
            border.RenderTransform = trans;
            ToastHost.Children.Add(border);

            border.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            trans.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
            timer.Tick += (_, __) =>
            {
                timer.Stop();
                var outAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                outAnim.Completed += (_, ___) => ToastHost.Children.Remove(border);
                border.BeginAnimation(OpacityProperty, outAnim);
            };
            timer.Start();
        }

        // ── Confirm popup ───────────────────────────────────────────────────────
        private void PedirConfirmacion(UIElement anchor, string texto, Action alConfirmar)
        {
            _pendingConfirm = alConfirmar;
            ConfirmText.Text = texto;
            ConfirmPopup.PlacementTarget = anchor;
            ConfirmPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            ConfirmPopup.IsOpen = true;
        }

        private void ConfirmCancel_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPopup.IsOpen = false;
            _pendingConfirm = null;
        }

        private void ConfirmOk_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPopup.IsOpen = false;
            var action = _pendingConfirm;
            _pendingConfirm = null;
            action?.Invoke();
        }
    }
}
