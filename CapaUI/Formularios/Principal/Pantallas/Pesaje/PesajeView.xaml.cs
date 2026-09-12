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
using CapaAplicacion.Common.Catalogos;
using CapaDominio.Reportes;
using CapaUI.Converters;
using CapaUI.Core.Permisos;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje
{
    public partial class PesajeView : UserControl
    {
        private PesajeViewModel _vm = null!;

        /// <summary>
        /// Lectura de catálogos que consumen los modales que abre esta pantalla. Se
        /// resuelve una vez en <c>Loaded</c> — el punto de composición que esta vista ya
        /// tenía para el ViewModel — y viaja a los modales por constructor, en vez de que
        /// cada modal se lo pida al contenedor (AGENTS.md, regla 9).
        /// </summary>
        private ICatalogoRepository _catalogos = null!;

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
            _vm = App.CrearVm<PesajeViewModel>();
            _catalogos = App.Services.GetRequiredService<ICatalogoRepository>();
            _vm.Toast           += MostrarToast;
            _vm.PropertyChanged += OnVmPropertyChanged;
            DataContext = _vm;

            // Mostrar el overlay de carga ACÁ, sincrónico y antes del await — no alcanza
            // con dejar que ActualizarUI() lo resuelva: PedirActualizarUI() difiere el
            // barrido a DispatcherPriority.Background (ver su comentario), que corre
            // DESPUÉS de que WPF ya pintó el primer frame del control recién cargado.
            // Ese frame de más es justo el parpadeo: toda la grilla (camiones, productos,
            // pesajes) se ve un instante con sus Visibility por defecto de la primera
            // carga, antes de que la pantalla decida si corresponde el estado vacío.
            CargandoInicial.Visibility = Visibility.Visible;
            IniciarSpinnerCarga();

            try
            {
                // Se captura la instancia ANTES del await. Si el usuario cierra o navega a otra
                // pantalla mientras carga, Unloaded pone _vm = null y la continuación volvería
                // sobre una vista ya descargada (_vm fue null). Se compara por referencia
                // para cubrir también el abrir-cerrar-abrir rápido.
                var vm = _vm;
                await vm.CargarAsync();

                if (!ReferenceEquals(_vm, vm)) return;

                SincronizarSeleccion();
                ActualizarUI();

                // Si al entrar no hay camiones en el andén, abrir el registro de camión automáticamente
                if (_vm.Camiones.Count == 0 && SesionPermisos.Tiene(Permiso.RegistrarEntrada) && ModalOverlay.Visibility != Visibility.Visible)
                {
                    AbrirCamionModal(null, soloPlaca: false);
                }
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
            CerrarModalActivo();
            if (_vm == null) return;
            _vm.Toast -= MostrarToast;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            DataContext = null;
            (_vm as IDisposable)?.Dispose();
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

            // La grilla NO se oculta: el estado vacío es un overlay por encima, así los
            // encabezados quedan a la vista (ver "Empty State en DataGrid" en la bóveda).
            bool sinEntradas = _vm.FilasEntradas.Count == 0;
            EntEmpty.EstaVacio = sinEntradas;
            EntEmpty.Mensaje = !hayCamion
                ? "Selecciona un camión para ver sus pesajes"
                : "Aún no hay pesajes registrados";
            EntEmpty.Submensaje = hayCamion && _vm.HayProducto && !cerrado
                ? "Usá «Pesar» para registrar la primera pesada"
                : string.Empty;

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

            // Agregar/editar (y, adentro del modal de editar, quitar) un producto del
            // camión seleccionado — los dos abren ProductosCargaModal (la carga entera).
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
            // Quitar ya no está en la barra: es el basurero de cada fila, que se
            // habilita solo (ver la columna en DgEntradas).

            // Qué se está viendo, dicho siempre y sin mentir.
            //
            // Sin producto seleccionado el VM cae a "camion" (ver ModoEfectivo) y la tabla
            // muestra las pesadas de TODO el camión — pero el toggle seguía marcando
            // "Producto actual". Con un solo producto las dos vistas se ven igual, así que
            // la contradicción pasaba desapercibida hasta que el rótulo no aparecía.
            // Ahora el rótulo nombra el producto cuando hay uno, dice "Todo el camión"
            // cuando no, y el toggle se sincroniza con lo que de verdad está en pantalla.
            bool porProducto = _vm.ModoEfectivo == "producto" && _vm.SelectedProducto is not null;

            TxtProductoEnVista.Visibility = hayCamion ? Visibility.Visible : Visibility.Collapsed;
            TxtProductoEnVista.Text = porProducto
                ? $"· {_vm.SelectedProducto!.ProductoNombre}"
                : "· Todo el camión";

            _sync = true;
            if (porProducto) RbVistaProducto.IsChecked = true;
            else             RbVistaCamion.IsChecked   = true;
            _sync = false;

            // Elegir "Producto actual" sin un producto seleccionado no tiene a qué
            // filtrar: se deshabilita y se dice por qué, en vez de aceptarlo y mostrar
            // otra cosa.
            RbVistaProducto.IsEnabled = _vm.SelectedProducto is not null;
            RbVistaProducto.ToolTip = _vm.SelectedProducto is not null
                ? "Ver solo las pesadas del producto seleccionado"
                : "Seleccioná un producto de la tabla para filtrar sus pesadas";

            // El botón Pesar vive en la tabla de Entradas. Solo está activo cuando
            // el filtro está en «Producto actual» con un producto seleccionado y abierto,
            // y el camión no está cerrado. En «Todo el camión» o sin producto seleccionado
            // permanece inactivo/gris.
            bool puedePesar = porProducto && prodAbierto && !cerrado;
            BtnProdPesar.IsEnabled = puedePesar;

            if (!hayCamion)
            {
                BtnProdPesar.ToolTip = "Selecciona un camión para comenzar a pesar";
            }
            else if (!hayProducto)
            {
                BtnProdPesar.ToolTip = "Selecciona un producto de la tabla para registrar una pesada";
            }
            else if (_vm.ModoEfectivo != "producto")
            {
                BtnProdPesar.ToolTip = "Cambia a la vista «Producto actual» para registrar una pesada";
            }
            else if (!prodAbierto)
            {
                BtnProdPesar.ToolTip = "El producto seleccionado está cerrado; reábrelo para seguir pesando";
            }
            else if (cerrado)
            {
                BtnProdPesar.ToolTip = "El camión está cerrado";
            }
            else
            {
                BtnProdPesar.ToolTip = $"Registrar pesaje para {_vm.SelectedProducto!.ProductoNombre}";
            }

            AjustarLayoutEntradas();
            AjustarLayoutProductos();
            SincronizarColumnasTotales();

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

        // ── Selección y eventos de Camiones y Proveedores ─────────────────────
        private async void GrupoCamion_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_sync || _vm == null) return;
            if (sender is FrameworkElement fe && fe.Tag is GrupoCamionPesaje grupo)
            {
                if (e.ClickCount == 2)
                {
                    var camion = grupo.Recepciones.FirstOrDefault();
                    if (camion != null && !_vm.CamionCerrado)
                        AbrirCamionModal(camion, soloPlaca: true);
                    return;
                }

                var target = _vm.SelectedCamion != null && grupo.Recepciones.Contains(_vm.SelectedCamion)
                    ? _vm.SelectedCamion
                    : grupo.Recepciones.FirstOrDefault();
                if (target != null)
                {
                    await _vm.SeleccionarCamionAsync(target);
                }
            }
        }

        private async void RecepcionProveedor_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_sync || _vm == null) return;
            if (sender is FrameworkElement fe && fe.Tag is CamionPesaje camion)
            {
                if (e.ClickCount == 2)
                {
                    if (!_vm.CamionCerrado)
                        AbrirRegistroCamionesModal(placaFija: camion.Placa, enfocar: camion);
                    return;
                }

                await _vm.SeleccionarCamionAsync(camion);
            }
        }

        private void BtnQuitarGrupo_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionPermisos.Tiene(Permiso.CancelarPesaje)) return;
            if (sender is not Button b || b.Tag is not GrupoCamionPesaje grupo) return;
            if (!grupo.PuedeQuitar) return;

            var confirmar = MessageBox.Show(
                $"¿Quitar el camión {grupo.Placa} y todas sus recepciones asociadas?",
                "Quitar camión",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirmar != MessageBoxResult.Yes) return;

            _ = QuitarGrupoFlujo(grupo);
        }

        private async Task QuitarGrupoFlujo(GrupoCamionPesaje grupo)
        {
            var vm = _vm;
            if (vm == null) return;
            await vm.QuitarCamionCompletoAsync(grupo);
            if (!ReferenceEquals(_vm, vm)) return;
            SincronizarSeleccion();
            ActualizarUI();
        }

        private void BtnQuitarRecepcion_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionPermisos.Tiene(Permiso.CancelarPesaje)) return;
            if (sender is not Button b || b.Tag is not CamionPesaje camion) return;
            if (!camion.PuedeQuitar) return;

            var confirmar = MessageBox.Show(
                $"¿Quitar la recepción del proveedor {camion.Proveedor} (Placa {camion.Placa})?",
                "Quitar recepción",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirmar != MessageBoxResult.Yes) return;

            _ = QuitarRecepcionFlujo(camion);
        }

        private async Task QuitarRecepcionFlujo(CamionPesaje camion)
        {
            var vm = _vm;
            if (vm == null) return;
            await vm.QuitarCamionAsync(camion);
            if (!ReferenceEquals(_vm, vm)) return;
            SincronizarSeleccion();
            ActualizarUI();
        }

        // Mismo criterio: sin el ActualizarUI() extra acá, que ya lo agenda
        // el propio SeleccionarProducto vía PropertyChanged.
        private void DgProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sync || _vm == null) return;
            _vm.SeleccionarProducto(DgProductos.SelectedItem as ProductoCamion);
        }

        /// <summary>
        /// Doble clic sobre un producto abre la gestión de la carga parada en esa fila
        /// —el mismo modal que «Editar producto»—, no el de pesar. Editar es la acción
        /// natural del doble clic en una tabla; pesar tiene su propio botón.
        /// </summary>
        private void DgProductos_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.SelectedProducto is { } p && !_vm.CamionCerrado)
                AbrirProductosCargaModal(p);
        }

        // ── Teclado en las tablas ───────────────────────────────────────────────
        // Enter abre el mismo modal que el doble clic, igual que en las grillas CRUD.
        // Las flechas ya las mueve el propio DataGrid; acá solo se intercepta
        // Enter, con PreviewKeyDown para llegar antes de que el control lo consuma.

        private void DgProductos_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            if (_vm?.SelectedProducto is not { } p || _vm.CamionCerrado) return;
            e.Handled = true;
            AbrirProductosCargaModal(p);
        }

        private void DgEntradas_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            if (_vm?.SelectedEntrada is not { } ent || _vm.CamionCerrado) return;
            var prod = _vm.SelectedCamion?.Productos.FirstOrDefault(x => x.Id == ent.ProdId);
            if (prod == null) return;
            e.Handled = true;
            AbrirPesajeModal(prod, ent);
        }

        /// <summary>Doble clic sobre un pesaje: abre su modal de edición.</summary>
        private void DgEntradas_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.SelectedEntrada is not { } ent || _vm.CamionCerrado) return;
            var prod = _vm.SelectedCamion?.Productos.FirstOrDefault(p => p.Id == ent.ProdId);
            if (prod != null) AbrirPesajeModal(prod, ent);
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

            // Mantener la sincronización de anchos también al scrollear horizontalmente
            // (el extent puede variar si se activa/desactiva el scrollbar horizontal).
            SincronizarColumnasTotales();
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
            SincronizarColumnasTotales();
        }

        private void DgProductos_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AjustarLayoutProductos();
        }

        /// <summary>
        /// Copia el <c>ActualWidth</c> exacto (en píxeles) de cada columna de
        /// <see cref="DgEntradas"/> al <c>Width</c> absoluto de la columna
        /// equivalente en <see cref="TotalGrid"/>, garantizando alineación perfecta
        /// sin importar proporciones estrella, DPI, zoom o tamaño de ventana.<br/>
        /// Solo actualiza las columnas de ancho variable (estrella); las columnas
        /// fijas (#, FECHA/HORA, Basurero) ya coinciden por definición de XAML.
        /// La comparación de ε = 0.25 px evita invalidaciones de layout innecesarias
        /// cuando el valor no cambia o la variación es sub-píxel.
        /// </summary>
        private void SincronizarColumnasTotales()
        {
            if (DgEntradas is null || DgEntradas.Columns.Count < 9) return;
            if (TotalGrid is null) return;

            // Mapeo: índice columna DataGrid → ColumnDefinition de TotalGrid
            // Solo las columnas de ancho variable (estrella); las fijas (#, FECHA/HORA,
            // Basurero) ya coinciden por definición de XAML y no se tocan.
            (int dgCol, ColumnDefinition tcCol)[] mapa =
            [
                (1, TcCol1),   // PRODUCTO
                (2, TcCol2),   // BRUTO (KG)
                (3, TcCol3),   // TARA (KG)
                (4, TcCol4),   // TARA EXTRA
                (5, TcCol5),   // NETO (KG)
                (6, TcCol6),   // BULTOS
            ];

            // ε = 0.25 px: evita invalidaciones de layout sub-píxel que no se perciben
            // visualmente pero sí dispararían una pasada de medición.
            const double epsilon = 0.25;
            foreach (var (dgCol, tcCol) in mapa)
            {
                double w = DgEntradas.Columns[dgCol].ActualWidth;
                if (w <= 0) continue;
                if (Math.Abs(tcCol.Width.Value - w) > epsilon)
                    tcCol.Width = new GridLength(w, GridUnitType.Pixel);
            }
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

            // Suma de anchos mínimos de las columnas con la nueva distribución:
            // #(52) + PRODUCTO(180) + BRUTO(96) + TARA(90) + TARA_EXTRA(100) + NETO(96) + BULTOS(85) + FECHA/HORA(135, fija) + BASURERO(52) + margen scrollbar(~16)
            double minAncho = 52 + 180 + 96 + 90 + 100 + 96 + 85 + 135 + 52 + 16;

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
            // _sync: ActualizarUI sincroniza el toggle con el modo efectivo, y ese
            // IsChecked programático vuelve a entrar acá. Sin la guarda, sincronizar
            // pisaría la preferencia del usuario.
            if (_vm == null || _sync) return;
            string modo = RbVistaCamion.IsChecked == true ? "camion" : "producto";

            // La columna PRODUCTO (y su celda espejo TcCol1 en la fila TOTAL)
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

        /// <summary>Alta de camiones: abre el modal para registrar placa y proveedor inicial.</summary>
        private void BtnNuevoProceso_Click(object sender, RoutedEventArgs e)
        {
            if (SesionPermisos.Tiene(Permiso.RegistrarEntrada))
                AbrirCamionModal(null, soloPlaca: false);
        }

        /// <summary>Edita los datos del camión o sus proveedores asociados.
        /// Abre RegistroCamionesModal enfocado en ese camión y placa.</summary>
        private void BtnEditarProceso_Click(object sender, RoutedEventArgs e)
        {
            if (SesionPermisos.Tiene(Permiso.ModificarPesaje) && _vm.SelectedCamion != null && !_vm.CamionCerrado)
                AbrirRegistroCamionesModal(placaFija: _vm.SelectedCamion.Placa, enfocar: _vm.SelectedCamion);
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
            if (_vm.ModoEfectivo == "producto" && _vm.SelectedProducto is { } p && p.Estado == "Abierto" && !_vm.CamionCerrado)
                AbrirPesajeModal(p, null);
        }

        private void BtnProdAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (SesionPermisos.Tiene(Permiso.ModificarPesaje) && _vm.SelectedCamion != null && !_vm.CamionCerrado)
                AbrirProductosCargaModal(null);
        }

        private void BtnProdEditar_Click(object sender, RoutedEventArgs e)
        {
            if (SesionPermisos.Tiene(Permiso.ModificarPesaje) && _vm.SelectedProducto != null && !_vm.CamionCerrado)
                AbrirProductosCargaModal(_vm.SelectedProducto);
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

        /// <summary>
        /// Basurero de la fila de un pesaje. Reemplaza al botón «Quitar» de la barra: la
        /// acción vive en la fila sobre la que actúa, así no hace falta seleccionar primero
        /// y después buscar el botón.
        /// </summary>
        private void QuitarEntrada_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not EntradaPesaje ent) return;
            if (!SesionPermisos.Tiene(Permiso.CancelarPesaje)) return;
            if (_vm == null || _vm.CamionCerrado) return;

            // MessageBox nativo, igual que QuitarCamion_Click: el Popup anclado al botón
            // quedaba flotando sobre el panel de al lado.
            var confirmar = MessageBox.Show(
                "¿Quitar esta entrada de pesaje? Se recalculará lo recibido.",
                "Quitar pesaje",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirmar != MessageBoxResult.Yes) return;

            _ = QuitarEntradaFlujo(ent);
        }

        /// <summary>
        /// Basurero de la fila de un producto. El botón ya viene deshabilitado si el
        /// producto tiene pesajes (<see cref="ProductoCamion.PuedeQuitar"/>); el servidor
        /// vuelve a exigirlo igual.
        /// </summary>
        private void QuitarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not ProductoCamion prod) return;
            if (!SesionPermisos.Tiene(Permiso.CancelarPesaje)) return;
            if (_vm == null || _vm.CamionCerrado || !prod.PuedeQuitar) return;

            var confirmar = MessageBox.Show(
                $"¿Quitar «{prod.ProductoNombre}» de la carga?",
                "Quitar producto",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirmar != MessageBoxResult.Yes) return;

            _ = QuitarProductoFlujo(prod);
        }

        private async Task QuitarProductoFlujo(ProductoCamion prod)
        {
            var vm = _vm;
            if (vm == null) return;
            await vm.QuitarProductoAsync(prod);
            if (!ReferenceEquals(_vm, vm)) return;
            SincronizarSeleccion();
            ActualizarUI();
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
        /// <summary>
        /// Gestión integral de los camiones del andén: muestra los camiones ya abiertos para
        /// consultarlos o editarlos, y permite incorporar nuevos camiones al proceso de descarga.
        /// </summary>
        /// <summary>
        /// Abre el modal de camión para alta (placa + proveedor inicial) o para edición
        /// de vehículo/placa física (soloPlaca: true).
        /// </summary>
        private void AbrirCamionModal(CamionPesaje? camion = null, bool soloPlaca = false)
        {
            var abiertos = _vm.Camiones.Where(c => c.Estado == "Abierto").ToList();
            var modal = new CamionModal(camion, abiertos, soloPlaca);

            modal.Cerrado += CerrarModal;

            if (soloPlaca && camion != null)
            {
                modal.ConfirmadoPlaca += async resultado =>
                {
                    bool ok = await _vm.ActualizarPlacaCamionAsync(camion.Id, resultado.Placa, resultado.Observaciones);
                    if (!ok) return;

                    CerrarModal();
                    SincronizarSeleccion();
                    ActualizarUI();
                };
            }
            else
            {
                modal.Confirmado += async resultado =>
                {
                    if (camion == null)
                    {
                        if (!resultado.IdProveedor.HasValue) return;
                        var lote = new List<(string Placa, int IdProveedor, string Descripcion)>
                        {
                            (resultado.Placa, resultado.IdProveedor.Value, resultado.Observaciones)
                        };
                        int creados = await _vm.RegistrarCamionesAsync(lote);
                        if (creados > 0)
                        {
                            CerrarModal();
                            SincronizarSeleccion();
                            ActualizarUI();
                        }
                    }
                    else
                    {
                        bool ok = await _vm.GuardarCamionAsync(
                            camion, resultado.Placa, resultado.Proveedor, resultado.IdProveedor, resultado.Observaciones);
                        if (ok)
                        {
                            CerrarModal();
                            SincronizarSeleccion();
                            ActualizarUI();
                        }
                    }
                };
            }

            MostrarModal(modal);
        }

        private void AbrirRegistroCamionesModal(string? placaFija = null, CamionPesaje? enfocar = null)
        {
            var abiertos = _vm.Camiones.Where(c => c.Estado == "Abierto").ToList();
            var modal = new RegistroCamionesModal(abiertos, placaFija, enfocar);

            modal.Cerrado += CerrarModal;
            modal.Confirmado += async cambios =>
            {
                // 1. Quitar bajas
                foreach (var baja in cambios.Bajas)
                {
                    await _vm.QuitarCamionAsync(baja);
                }

                // 2. Modificaciones a camiones existentes
                foreach (var edit in cambios.Cambios)
                {
                    bool ok = await _vm.GuardarCamionAsync(edit.Camion, edit.Placa, edit.Proveedor, edit.IdProveedor, edit.Descripcion);
                    if (!ok) return false;
                }

                // 3. Altas de nuevos camiones en lote (RPC atómica)
                if (cambios.Altas.Count > 0)
                {
                    var lote = cambios.Altas
                        .Select(c => (c.Placa, c.IdProveedor, c.Descripcion))
                        .ToList();

                    int creados = await _vm.RegistrarCamionesAsync(lote);
                    if (creados != lote.Count)
                    {
                        modal.AplicarGuardadoParcial(
                            creados, _vm.Camiones.Where(c => c.Estado == "Abierto").ToList());
                        SincronizarSeleccion();
                        ActualizarUI();
                        return false;
                    }
                }

                SincronizarSeleccion();
                ActualizarUI();
                return true;
            };

            MostrarModal(modal);
        }

        /// <summary>
        /// Abre la gestión de TODA la carga de la recepción seleccionada: los productos
        /// que ya tiene, los que se agreguen y los que se quiten, en una sola tabla que se
        /// guarda de un saque.
        /// <para/>
        /// <paramref name="enfocar"/> solo decide en qué fila arranca el cursor — lo usa
        /// «Editar producto», que abre el mismo modal parado en el producto seleccionado.
        /// Quitar ya no pide confirmación por <see cref="MessageBox"/>: nada se escribe
        /// hasta «Finalizar», así que cerrar el modal ya es el "deshacer".
        /// </summary>
        private void AbrirProductosCargaModal(ProductoCamion? enfocar)
        {
            if (_vm.SelectedCamion is not { } camion) return;

            var modal = new ProductosCargaModal(_catalogos, camion, enfocar);

            modal.Cerrado += CerrarModal;
            modal.Confirmado += async cambios =>
            {
                // Guardar la carga es un viaje de red (RPC transaccional): sin este aviso
                // el modal se ve congelado durante la espera.
                modal.MostrarGuardando(true);
                try
                {
                    bool ok = await _vm.GuardarProductosCargaAsync(
                        camion, cambios.Altas, cambios.Cambios, cambios.Bajas);

                    if (!ok) return false;   // el VM ya avisó por Toast; el modal queda abierto
                }
                finally
                {
                    modal.MostrarGuardando(false);
                }

                SincronizarSeleccion();
                ActualizarUI();
                return true;
            };

            MostrarModal(modal);
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
            _vm.ActualizarSeleccionGrupos();
            if (_vm.SelectedProducto != null) DgProductos.SelectedItem = _vm.SelectedProducto;
            _sync = false;
        }

        // ── Overlay + animación fade/pop ───────────────────────────────────────
        /// <summary>
        /// Deja «aire» entre el modal y los bordes de la pantalla: lo limita a la medida
        /// del <c>ModalOverlay</c> menos 48 px (24 por lado).
        /// <para/>
        /// Va acá y no en el XAML del modal a propósito. Cada modal se lo ataba solo con
        /// <c>MaxWidth="{Binding ActualWidth, RelativeSource={RelativeSource
        /// AncestorType=Border}, ...}"</c>, y esa búsqueda de ancestro se escapa del
        /// control: en el diseñador de Visual Studio el modal cuelga del árbol visual del
        /// propio VS, que también tiene <c>Border</c>. El binding enganchaba uno de esos
        /// —así que <c>FallbackValue</c> nunca entraba—, en el primer measure ese Border
        /// mide 0, el converter devolvía <c>max(0, 0-48) = 0</c>, y con <c>MaxWidth=0</c>
        /// el modal colapsaba a 0×0: el lienzo mostraba el recuadro del artboard vacío.
        /// <para/>
        /// Atado acá contra <c>ModalOverlay</c> por referencia directa no hay ancestro que
        /// buscar, y además el tamaño lo decide quien hospeda, que es el contrato de
        /// layout natural de WPF. Ver ADR-028.
        /// </summary>
        private void LimitarAlOverlay(UserControl modal)
            // Delega en el helper compartido en vez de duplicarlo: asi Pesaje tambien
            // recibe la reevaluacion del binding cuando el overlay se hace visible.
            // Sin eso, la segunda apertura de cualquier modal quedaba en 0x0.
            => CapaUI.Core.ModalLayout.LimitarAlOverlay(modal, ModalOverlay);

        private void MostrarModal(UserControl modal)
        {
            _modalGen++;
            LimitarAlOverlay(modal);
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
                if (ModalContent.Content is IDisposable disp)
                {
                    disp.Dispose();
                }
                ModalContent.Content = null;
            };
            ModalOverlay.BeginAnimation(OpacityProperty, fade);
        }

        private void CerrarModalActivo()
        {
            _modalGen++;
            ModalOverlay.BeginAnimation(OpacityProperty, null);
            ModalOverlay.Visibility = Visibility.Collapsed;
            if (ModalContent.Content is IDisposable disp)
            {
                disp.Dispose();
            }
            ModalContent.Content = null;
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
