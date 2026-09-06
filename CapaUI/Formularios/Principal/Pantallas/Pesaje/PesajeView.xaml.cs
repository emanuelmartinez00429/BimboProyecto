using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CapaDominio.Reportes;
using CapaUI.Core.Permisos;
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

        /// <summary>Animación del spinner de la carga inicial.</summary>
        private Storyboard? _spinnerCarga;

        /// <summary>
        /// Animación de los spinners de "cambiando de camión" (CargandoMovimiento +
        /// CargandoEntradas) — un solo Storyboard con una rotación por cada Path, así se
        /// prenden/apagan juntos con un solo Begin/Stop.
        /// </summary>
        private Storyboard? _spinnerProductos;

        /// <summary>
        /// ScrollViewer interno de DgEntradas. No se busca en el árbol visual: lo
        /// entrega el propio ScrollChanged como OriginalSource, que es la forma
        /// barata y estable de tenerlo (el DataGrid lo crea en su plantilla).
        /// </summary>
        private ScrollViewer? _svEntradas;
        private ScrollViewer? _svProductos;

        public PesajeView() => InitializeComponent();

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;
            _vm = App.Services.GetRequiredService<PesajeViewModel>();
            _vm.Toast           += MostrarToast;
            _vm.PropertyChanged += OnVmPropertyChanged;
            DataContext = _vm;

            // Agrupar por placa acá y no con un CollectionViewSource en los Resources del
            // XAML: los recursos no heredan DataContext, así que el Source="{Binding
            // Camiones}" de allá no resuelve nunca y la lista queda vacía sin avisar.
            // Sobre la vista por defecto de la colección alcanza — el ListBox está atado a
            // Camiones, así que la usa.
            var vista = CollectionViewSource.GetDefaultView(_vm.Camiones);
            if (vista.GroupDescriptions.Count == 0)
                vista.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CamionPesaje.Placa)));

            try
            {
                // Se captura la instancia ANTES del await. Si el usuario cierra o navega a otra
                // pantalla mientras carga, Unloaded pone _vm = null y la continuación volvería
                // sobre una vista ya descargada (_vm fue null). Se compara por referencia
                // para cubrir también el abrir-cerrar-abrir rápido.
                var vm = _vm;
                await vm.CargarAsync();

                if (!ReferenceEquals(_vm, vm)) return;

                _sync = true;
                LstCamiones.SelectedItem = _vm.SelectedCamion;
                _sync = false;
                ActualizarUI();
            }
            catch (OperationCanceledException)
            {
                // Cancelación intencional al cambiar de vista.
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Pesaje] Falló la carga inicial de la pantalla");
            }
        }

        private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            PedirActualizarUI();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            DetenerSpinnerCarga();   // si se sale mientras cargaba, no dejar la animación viva
            DetenerSpinnerProductos();
            if (_vm == null) return;
            _vm.Toast -= MostrarToast;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            DataContext = null;
            _vm = null!;
        }

        // ── Sincronización de estados/empty-states ─────────────────────────────

        /// <summary>
        /// Agenda UN barrido de <see cref="ActualizarUI"/> por frame.
        /// <para/>
        /// El VM notifica de a ráfagas: NotificarStats y NotificarTotales levantan una docena
        /// de propiedades cada uno, y cada notificación disparaba el barrido completo de ~20
        /// controles — varios tocando Visibility de los DataGrid, que invalida layout. Con la
        /// bandera, la ráfaga entera se colapsa en una sola pasada.
        /// </summary>
        private bool _refrescoPendiente;

        private void PedirActualizarUI()
        {
            if (_refrescoPendiente) return;
            _refrescoPendiente = true;

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _refrescoPendiente = false;
                ActualizarUI();
            }));
        }

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
            BtnCamionCerrar.IsEnabled     = hayCamion && !cerrado;
            // Con la placa compartida el botón cierra UNA recepción, no el camión: el
            // camión sigue en el andén hasta que se cierre la del otro proveedor. Cada
            // recepción se recibe y se firma por separado, así que cerrar de a una es
            // lo correcto — solo hay que decirlo bien.
            TxtBtnCamionCerrar.Text = _vm.SelectedCamion?.PlacaCompartida == true
                ? "Cerrar recepción"
                : "Cerrar camión";
            // El reporte se puede reimprimir aunque el camión ya esté cerrado — ya
            // no depende de cerrarlo, así que su único requisito es tener uno seleccionado.
            BtnImprimirReporte.IsEnabled = hayCamion;
            BtnCerrarTodos.IsEnabled     = _vm.CamionesActivos > 0;

            BtnProdPesar.IsEnabled = hayProducto && prodAbierto && !cerrado;

            // Agregar/editar (y, adentro del modal de editar, quitar) un producto del
            // camión seleccionado — cada uno abre ProductoCamionModal.
            BtnProdAgregar.IsEnabled = hayCamion && !cerrado;
            BtnProdEditar.IsEnabled  = hayProducto && !cerrado;

            // Cerrar/reabrir el producto seleccionado: se activa con solo seleccionarlo
            // (no depende de si está abierto o cerrado, porque sirve para las dos
            // direcciones), pero no si el camión entero ya está cerrado.
            BtnProdCerrar.IsEnabled = hayProducto && !cerrado;
            TxtBtnProdCerrar.Text = prodAbierto ? "Cerrar producto" : "Reabrir producto";

            // La tara extra se reparte entre pesadas ya registradas: sin pesadas no hay nada
            // que repartir.
            BtnProdTaraExtra.IsEnabled = hayCamion && !cerrado
                                      && _vm.SelectedCamion!.Productos.Any(p => p.Entradas.Count > 0);

            int sinTara = hayCamion ? _vm.SelectedCamion!.PesadasSinTaraExtra : 0;
            TxtAvisoTaraProducto.Visibility = sinTara > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtAvisoTaraProducto.Text = sinTara == 1
                ? "1 pesada sin tara extra — bultos aproximados"
                : $"{sinTara} pesadas sin tara extra — bultos aproximados";

            BtnEntEditar.IsEnabled = hayEntrada && !cerrado;
            BtnEntQuitar.IsEnabled = hayEntrada && !cerrado;

            AjustarLayoutEntradas();
            AjustarLayoutProductos();

            ActualizarEstadoVacio();
            ActualizarCargaProductos();
        }

        /// <summary>
        /// Muestra/oculta los overlays "Actualizando productos…"/"Actualizando pesajes…"
        /// mientras <see cref="PesajeViewModel.CargarProductosAsync"/> resuelve un cambio de
        /// camión. A diferencia de <see cref="ActualizarEstadoVacio"/> esto SÍ se repite en
        /// cada cambio (no solo en la primera carga): acá no hay contenido previo del MISMO
        /// camión que tapar de golpe, es contenido de OTRO camión que hay que dejar de ver.
        /// </summary>
        private void ActualizarCargaProductos()
        {
            if (_vm == null) return;

            bool cargando = _vm.CargandoProductos;
            CargandoMovimiento.Visibility = cargando ? Visibility.Visible : Visibility.Collapsed;
            CargandoEntradas.Visibility   = cargando ? Visibility.Visible : Visibility.Collapsed;
            if (cargando) IniciarSpinnerProductos(); else DetenerSpinnerProductos();
        }

        private void IniciarSpinnerProductos()
        {
            if (_spinnerProductos != null) return;
            _spinnerProductos = new Storyboard();
            foreach (var target in new[] { SpinnerMovimiento, SpinnerEntradas })
            {
                var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
                { RepeatBehavior = RepeatBehavior.Forever };
                Storyboard.SetTarget(anim, target);
                Storyboard.SetTargetProperty(anim,
                    new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
                _spinnerProductos.Children.Add(anim);
            }
            _spinnerProductos.Begin();
        }

        private void DetenerSpinnerProductos()
        {
            if (_spinnerProductos is null) return;
            _spinnerProductos.Stop();
            _spinnerProductos.Remove();
            _spinnerProductos.Children.Clear();
            _spinnerProductos = null;
        }

        /// <summary>
        /// Muestra el estado vacío cuando no hay ningún camión descargándose.
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

            EstadoVacio.Visibility = _vm.MostrarEstadoVacio ? Visibility.Visible : Visibility.Collapsed;
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
        // Sin el ActualizarUI() de acá: SeleccionarCamionAsync ya deja el VM en un
        // estado que dispara PropertyChanged (RecalcularFilas/NotificarStats lo hacen
        // siempre, tengan o no cambios reales), y eso ya agenda un ActualizarUI() vía
        // PedirActualizarUI(). Llamarlo también acá lo corría dos veces por click —
        // uno síncrono y el mismo de nuevo un instante después.
        private async void LstCamiones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sync || _vm == null) return;
            await _vm.SeleccionarCamionAsync(LstCamiones.SelectedItem as CamionPesaje);
        }

        private void LstCamiones_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.SelectedCamion != null && !_vm.CamionCerrado)
                AbrirCamionModal(_vm.SelectedCamion);
        }

        // Mismo criterio que LstCamiones_SelectionChanged: sin el ActualizarUI() extra
        // acá, que ya lo agenda el propio SeleccionarProducto vía PropertyChanged.
        private void DgProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sync || _vm == null) return;
            _vm.SeleccionarProducto(DgProductos.SelectedItem as ProductoCamion);
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
                var vm = _vm;
                if (vm == null) return;
                await vm.ToggleEstadoProductoAsync(p);
                if (!ReferenceEquals(_vm, vm)) return;
                ActualizarUI();
                e.Handled = true;
            }
        }

        // ── Entradas: scroll horizontal ───────────────────────────────────────

        /// <summary>
        /// Mantiene la fila TOTAL pegada a las columnas: vive fuera del ScrollViewer
        /// de la grilla, así que hay que desplazarla a mano y darle el ancho del
        /// contenido (no el del viewport) para que no quede corta al scrollear.
        /// </summary>
        private void DgEntradas_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _svEntradas ??= e.OriginalSource as ScrollViewer;

            // Los cambios verticales son los más frecuentes y no mueven nada acá.
            if (e.HorizontalChange == 0 && e.ExtentWidthChange == 0 && e.ViewportWidthChange == 0)
                return;

            TotalScrollTransform.X = -e.HorizontalOffset;

            // NaN = "medí solo": cuando no hay scroll horizontal la fila se estira
            // con el panel, igual que la grilla.
            TotalGrid.Width = e.ExtentWidth > e.ViewportWidth ? e.ExtentWidth : double.NaN;
        }

        /// <summary>
        /// Shift + rueda desplaza en horizontal. WPF no lo trae de fábrica: sin esto
        /// la única forma de llegar a las columnas de la derecha es arrastrar la barra.
        /// </summary>
        private void DgEntradas_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Shift) return;
            if (_svEntradas is null || _svEntradas.ScrollableWidth <= 0) return;

            _svEntradas.ScrollToHorizontalOffset(_svEntradas.HorizontalOffset - e.Delta);
            e.Handled = true;   // que no se lo lleve el scroll vertical
        }

        private void DgProductos_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _svProductos ??= e.OriginalSource as ScrollViewer;
        }

        private void DgProductos_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Shift) return;
            if (_svProductos is null || _svProductos.ScrollableWidth <= 0) return;

            _svProductos.ScrollToHorizontalOffset(_svProductos.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void DgEntradas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AjustarLayoutEntradas();
        }

        private void DgProductos_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AjustarLayoutProductos();
        }

        /// <summary>
        /// Ajusta dinámicamente el scroll horizontal y el dimensionamiento de columnas de DgEntradas.
        /// Cuando el ancho del DataGrid es suficiente para albergar las columnas cómodamente,
        /// desactiva el scroll horizontal (Disabled) para que las columnas Star (*) absorban el 100%
        /// del ancho disponible sin dejar huecos vacíos a la derecha ni truncar texto innecesariamente.
        /// Cuando el formulario se reduce por debajo del mínimo legible, activa el scroll horizontal (Auto)
        /// para que aparezca la barra de desplazamiento y no se aplasten las columnas.
        /// </summary>
        private void AjustarLayoutEntradas()
        {
            if (DgEntradas == null) return;

            // Suma de anchos mínimos de las columnas (PRODUCTO ya es fija, no condicional):
            // ID(65) + PRODUCTO(180) + BRUTO(96) + TARA(90) + TARA_EXTRA(100) + NETO(96) + BULTOS(85) + FECHA/HORA(135) + margen scrollbar(~16)
            double minAncho = 65 + 180 + 96 + 90 + 100 + 96 + 85 + 135 + 16;

            double anchoActual = DgEntradas.ActualWidth;
            if (anchoActual <= 0) return;

            if (anchoActual < minAncho)
            {
                ScrollViewer.SetHorizontalScrollBarVisibility(DgEntradas, ScrollBarVisibility.Auto);
            }
            else
            {
                ScrollViewer.SetHorizontalScrollBarVisibility(DgEntradas, ScrollBarVisibility.Disabled);
            }
        }

        /// <summary>
        /// Mismo ajuste adaptativo para la grilla de productos de camión en el panel izquierdo.
        /// </summary>
        private void AjustarLayoutProductos()
        {
            if (DgProductos == null) return;

            // CÓDIGO(85) + PRODUCTO(180) + REST(72) + %REST(120) + ESTADO(104) + margen(~16) = ~577 px
            double minAncho = 85 + 180 + 72 + 120 + 104 + 16;
            double anchoActual = DgProductos.ActualWidth;
            if (anchoActual <= 0) return;

            if (anchoActual < minAncho)
            {
                ScrollViewer.SetHorizontalScrollBarVisibility(DgProductos, ScrollBarVisibility.Auto);
            }
            else
            {
                ScrollViewer.SetHorizontalScrollBarVisibility(DgProductos, ScrollBarVisibility.Disabled);
            }
        }

        private void RbVista_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            string modo = RbVistaCamion.IsChecked == true ? "camion" : "producto";

            // La columna PRODUCTO (y su celda espejo en la fila TOTAL, TotalColProducto)
            // ya no se prende/apaga con el modo: queda visible siempre, incluso en
            // "Producto actual" con una sola fila repitiendo el mismo nombre — es más
            // fácil detectar de qué producto es cada entrada que confiar en recordar
            // cuál está seleccionado arriba.
            _vm.CambiarVista(modo);
            AjustarLayoutEntradas();
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
            var vm = _vm;
            if (vm == null) return;
            var cerradas = await vm.CerrarTodosAsync();
            if (!ReferenceEquals(_vm, vm)) return;
            ActualizarUI();
            if (cerradas.Count > 0) AbrirReporte(cerradas);
        }

        /// <summary>Alta de camiones: la tabla del proceso de descarga, hasta
        /// <see cref="PesajeViewModel.MaxCamiones"/> de una sola vez. Solo sus datos —
        /// los productos se agregan después, uno a la vez, desde el panel de Movimiento.</summary>
        private void BtnNuevoProceso_Click(object sender, RoutedEventArgs e)
        {
            if (SesionPermisos.Tiene(Permiso.RegistrarEntrada))
                AbrirRegistroCamionesModal();
        }

        /// <summary>Edita los datos del camión seleccionado (placa/proveedor/observaciones).
        /// Los productos se editan aparte, desde el panel de Movimiento.</summary>
        private void BtnEditarProceso_Click(object sender, RoutedEventArgs e)
        {
            if (SesionPermisos.Tiene(Permiso.ModificarPesaje) && _vm.SelectedCamion != null && !_vm.CamionCerrado)
                AbrirCamionModal(_vm.SelectedCamion);
        }

        /// <summary>
        /// Basurero de la propia fila en la lista de camiones (antes era un botón aparte
        /// en la toolbar). <c>Tag="{Binding}"</c> en el XAML lleva el <see cref="CamionPesaje"/>
        /// de esa fila directo al handler, así que borra el que se tocó sin depender de
        /// la selección — mismo patrón que "Quitar" en el megamodal de productos.
        /// MessageBox nativo (no el popup propio de la pantalla) y no PedirConfirmacion:
        /// mismo criterio que "Cerrar/Reabrir producto" (BtnProdCerrar_Click) para
        /// confirmar acciones que se disparan desde un ícono suelto en una fila.
        /// </summary>
        private void QuitarCamion_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionPermisos.Tiene(Permiso.CancelarPesaje)) return;
            if (sender is not Button b || b.Tag is not CamionPesaje camion) return;

            var confirmar = MessageBox.Show(
                $"¿Quitar el camión {camion.Placa}? Se perderán sus productos y pesajes.",
                "Quitar camión",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirmar != MessageBoxResult.Yes) return;

            _ = QuitarCamionFlujo(camion);
        }

        private async Task QuitarCamionFlujo(CamionPesaje camion)
        {
            var vm = _vm;
            if (vm == null) return;
            await vm.QuitarCamionAsync(camion);
            if (!ReferenceEquals(_vm, vm)) return;
            SincronizarSeleccion();
            ActualizarUI();
        }

        /// <summary>
        /// Cierra el camión seleccionado. Ya no abre el reporte — eso es
        /// "Imprimir reporte" (<see cref="BtnImprimirReporte_Click"/>), independiente.
        /// </summary>
        private async void BtnCamionCerrar_Click(object sender, RoutedEventArgs e)
        {
            var vm = _vm;
            if (vm == null || vm.SelectedCamion == null || vm.CamionCerrado) return;
            await vm.DescargarCamionAsync();
            if (!ReferenceEquals(_vm, vm)) return;
            ActualizarUI();
        }

        /// <summary>
        /// Abre el reporte del camión seleccionado, esté abierto o cerrado — antes
        /// esto solo pasaba como efecto secundario de cerrarlo con "Descargar".
        /// </summary>
        private void BtnImprimirReporte_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedCamion is { } camion) AbrirReporte(new List<CamionPesaje> { camion });
        }

        // ── Productos ────────────────────────────────────────────────────────
        // Agregar/editar (y, adentro del modal de editar, quitar) un producto se hace
        // con ProductoCamionModal, uno a la vez — ahí se respeta la regla de no quitar
        // un producto ya pesado.

        private void BtnProdPesar_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedProducto is { } p && p.Estado == "Abierto" && !_vm.CamionCerrado)
                AbrirPesajeModal(p, null);
        }

        private void BtnProdAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (SesionPermisos.Tiene(Permiso.ModificarPesaje) && _vm.SelectedCamion != null && !_vm.CamionCerrado)
                AbrirProductoModal(null);
        }

        private void BtnProdEditar_Click(object sender, RoutedEventArgs e)
        {
            if (SesionPermisos.Tiene(Permiso.ModificarPesaje) && _vm.SelectedProducto != null && !_vm.CamionCerrado)
                AbrirProductoModal(_vm.SelectedProducto);
        }

        /// <summary>
        /// "Cerrar producto" / "Reabrir producto" — antes vivía adentro del modal de
        /// pesaje como "Terminar de pesar"; ahora está acá, al lado de "Pesar", así se
        /// activa con solo seleccionar el producto en la tabla, sin tener que abrir el
        /// modal. Mismo cambio de estado que el chip de la tabla de Movimiento
        /// (<see cref="EstadoProducto_Click"/>), pero con confirmación: es un botón fijo
        /// en el footer, al lado de "Pesar" (el que se clickea seguido), así que un toque
        /// accidentalmente trae aparejado cerrar o reabrir el producto — más caro de
        /// deshacer que perder un clic.
        /// </summary>
        private async void BtnProdCerrar_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedProducto is not { } p || _vm.CamionCerrado) return;

            bool abierto = p.Estado == "Abierto";
            var confirmar = MessageBox.Show(
                abierto
                    ? "Este producto va a quedar cerrado y no se van a poder registrar más " +
                      "pesadas para él.\n\n¿Confirmás que terminaste de pesarlo?"
                    : "El producto va a quedar abierto de nuevo y vas a poder seguir " +
                      "registrando pesadas para él.\n\n¿Desea reabrir producto?",
                abierto ? "Cerrar producto" : "Reabrir producto",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirmar != MessageBoxResult.Yes) return;

            var vm = _vm;
            if (vm == null) return;
            await vm.ToggleEstadoProductoAsync(p);
            if (!ReferenceEquals(_vm, vm)) return;
            ActualizarUI();
        }

        private void BtnProdTaraExtra_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedCamion is not null && !_vm.CamionCerrado
                && SesionPermisos.Tiene(Permiso.ModificarPesaje))
                AbrirTaraExtraModal();
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
            var vm = _vm;
            if (vm == null) return;
            await vm.QuitarEntradaAsync(ent);
            if (!ReferenceEquals(_vm, vm)) return;
            ActualizarUI();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Modales
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Tabla de alta del proceso de descarga: hasta
        /// <see cref="PesajeViewModel.MaxCamiones"/> camiones en una sola pasada. En el
        /// andén los camiones llegan juntos, así que darlos de alta de a uno significaba
        /// abrir y cerrar <see cref="CamionModal"/> cinco veces seguidas.
        /// <para/>
        /// Editar sigue siendo de a uno — ver <see cref="AbrirCamionModal"/>.
        /// </summary>
        private void AbrirRegistroCamionesModal()
        {
            // Las recepciones abiertas viajan al modal para dos cosas: avisar que una placa
            // ya está abierta con otro proveedor (no bloquea) y rechazar el duplicado
            // exacto placa+proveedor (sí bloquea).
            var abiertos = _vm.Camiones.Where(c => c.Estado == "Abierto").ToList();
            var modal = new RegistroCamionesModal(abiertos);

            modal.Cerrado += CerrarModal;
            modal.Confirmado += async camiones =>
            {
                var lote = camiones
                    .Select(c => (c.Placa, c.IdProveedor, c.Descripcion))
                    .ToList();

                int creados = await _vm.RegistrarCamionesAsync(lote);

                // Si entraron todos, se cierra. Si el lote se cortó a mitad el modal queda
                // abierto con lo que falta —el VM ya dijo por Toast cuántos entraron— pero
                // hay que descontarle lo que sí se guardó, o el próximo "Guardar" lo
                // registraría dos veces.
                if (creados == lote.Count)
                    CerrarModal();
                else
                    modal.AplicarGuardadoParcial(
                        creados, _vm.Camiones.Where(c => c.Estado == "Abierto").ToList());

                SincronizarSeleccion();
                ActualizarUI();
            };

            MostrarModal(modal);
        }

        /// <summary>
        /// Edición de UN camión — solo sus datos. El alta ya no pasa por acá: es
        /// <see cref="AbrirRegistroCamionesModal"/>. Los productos no viajan en este
        /// modal, ver <see cref="AbrirProductoModal"/>.
        /// </summary>
        private void AbrirCamionModal(CamionPesaje? camion)
        {
            // Las recepciones abiertas viajan al modal solo para avisar que la placa que
            // se escribe ya está abierta con otro proveedor (no bloquea: es el caso
            // legítimo del camión con carga de dos proveedores).
            var abiertos = _vm.Camiones.Where(c => c.Estado == "Abierto").ToList();
            var modal = new CamionModal(camion, abiertos);

            modal.Cerrado += CerrarModal;
            modal.Confirmado += async r =>
            {
                bool ok = await _vm.GuardarCamionAsync(camion, r.Placa, r.Proveedor, r.IdProveedor, r.Observaciones);
                if (!ok) return;   // el VM ya avisó por Toast; el modal queda abierto

                CerrarModal();
                SincronizarSeleccion();
                ActualizarUI();
            };

            MostrarModal(modal);
        }

        /// <summary>
        /// Agrega o edita UN producto del camión seleccionado
        /// (<paramref name="producto"/> null = agregar). "Quitar producto" vive dentro
        /// del propio modal (evento <c>QuitarSolicitado</c>) — se confirma acá con el
        /// mismo <see cref="MessageBox"/> nativo que usa <see cref="QuitarCamion_Click"/>.
        /// </summary>
        private void AbrirProductoModal(ProductoCamion? producto)
        {
            if (_vm.SelectedCamion is not { } camion) return;
            string placa = camion.Placa;

            var modal = new ProductoCamionModal(camion, _vm.RecepcionesDePlaca(placa), producto);

            modal.Cerrado += CerrarModal;
            // El modal NO se cierra al guardar: eso es lo que significa "Guardar y agregar
            // otro". Cerrarlo lo decide el propio modal según qué botón se tocó.
            modal.Guardar += async r =>
            {
                bool ok = producto is null
                    ? await _vm.AgregarProductoAsync(
                        placa, r.IdProveedor, r.Proveedor,
                        r.IdProducto, r.PesoManifestado, r.BultosDeclarados, r.Observaciones)
                    : await _vm.ActualizarProductoAsync(
                        producto, r.PesoManifestado, r.BultosDeclarados, r.Observaciones);

                if (!ok) return false;   // el VM ya avisó por Toast; el modal queda abierto

                // Recepciones frescas: el guardado pudo haber creado la del otro proveedor,
                // y el producto recién agregado no debe volver a ofrecerse en el catálogo.
                modal.ActualizarRecepciones(_vm.RecepcionesDePlaca(placa));
                SincronizarSeleccion();
                ActualizarUI();
                return true;
            };
            modal.QuitarSolicitado += p =>
            {
                var confirmar = MessageBox.Show(
                    $"¿Quitar «{p.ProductoNombre}» de la carga? Esta acción no se puede deshacer.",
                    "Quitar producto",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (confirmar != MessageBoxResult.Yes) return;

                _ = QuitarProductoFlujo(p);
            };

            MostrarModal(modal);
        }

        private async Task QuitarProductoFlujo(ProductoCamion producto)
        {
            await _vm.QuitarProductoAsync(producto);
            CerrarModal();
            SincronizarSeleccion();
            ActualizarUI();
        }

        /// <summary>
        /// Abre el modal para cargar una tara extra ya pesada y repartirla entre las pesadas
        /// del producto seleccionado o de todo el camión.
        /// </summary>
        private void AbrirTaraExtraModal()
        {
            if (_vm.SelectedCamion == null) return;

            var modal = new TaraExtraTotalModal(_vm.SelectedCamion, _vm.SelectedProducto);
            modal.Cerrado += CerrarModal;
            modal.Confirmado += async r =>
            {
                bool ok = await _vm.RepartirTaraExtraAsync(r.Entradas, r.Total);
                if (!ok) return;   // el VM ya avisó por Toast; el modal queda abierto

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

            // El modal NO se cierra al guardar: eso es lo que significa "Seguir pesando".
            // Queda abierto y limpio para la tarima siguiente, y se cierra con "Volver".
            // Se lee modal.EntradaEnEdicion en vez de la variable capturada porque el modal
            // deja de estar editando en cuanto guarda la corrección.
            modal.GuardarYSeguir += async snap =>
            {
                bool ok = await _vm.GuardarEntradaAsync(producto, snap, modal.EntradaEnEdicion);
                SincronizarSeleccion();
                ActualizarUI();
                return ok;
            };
            MostrarModal(modal);
        }

        private void AbrirReporte(List<CamionPesaje> camiones)
        {
            var todos = _vm.Camiones.ToList();
            var modal = new ReporteModal(camiones, todos);
            modal.Cerrado += CerrarModal;
            modal.FormatoSeleccionado += async (formato, listaCamiones) =>
            {
                CerrarModal();
                await ElegirRutaYGenerarReporteAsync(formato, listaCamiones);
            };
            MostrarModal(modal);
        }

        private async Task ElegirRutaYGenerarReporteAsync(ReportFormat formato, List<CamionPesaje> camiones)
        {
            string extension = formato == ReportFormat.Pdf ? ".pdf" : ".xlsx";
            string nombreSugerido = camiones.Count == 1
                ? $"Pesaje_Camion_{camiones[0].Placa}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}"
                : $"Pesaje_Insumos_BES_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Guardar reporte de pesajes (Pesado de Insumos BES)",
                FileName = nombreSugerido,
                DefaultExt = extension,
                AddExtension = true,
                OverwritePrompt = true,
                Filter = formato == ReportFormat.Pdf
                    ? "Documento PDF (*.pdf)|*.pdf"
                    : "Libro de Excel (*.xlsx)|*.xlsx",
            };

            if (dialog.ShowDialog() != true) return;

            bool ok = await _vm.GenerarReportePesajesAsync(formato, dialog.FileName, camiones);
            if (ok)
            {
                OnReporteCreado(dialog.FileName);
            }
        }

        private static void OnReporteCreado(string ruta)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"El reporte se guardó correctamente en:\n{ruta}\n\nNo pudo abrirse automáticamente: {ex.Message}",
                    "Reporte creado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void SincronizarSeleccion()
        {
            if (_vm == null) return;
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
