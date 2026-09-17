using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Realtime;
using CapaAplicacion.Usuarios.Interfaces;
using CapaAplicacion.Empresa.Dtos;
using CapaAplicacion.Empresa.Interfaces;
using CapaUI.Core.Empresa;
using CapaUI.Core.Permisos;
using CapaUI.Formularios.Principal.Pantallas.Configuracion;
using CapaUI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace CapaUI.Formularios.Principal
{
    public partial class MainWindow : Window
    {
        public event EventHandler? SesionCerrada;

        private MainViewModel Vm => (MainViewModel)DataContext;
        private bool _cerrando = false;
        private readonly IUsuarioSesionService _sesionService;
        private readonly IRealtimeService      _realtimeService;
        private readonly IConexionMonitor      _conexionMonitor;
        private readonly IEmpresaRepository    _empresaRepository;
        private readonly IconoSidebarCache     _iconoSidebarCache;
        private readonly ICacheService              _cache;
        private readonly IInvalidadorCacheRealtime  _invalidadorCache;

        // ── Estado del sidebar ────────────────────────────────────────────
        private bool   _collapsed      = false;
        private bool   _animating      = false;
        private string _activeModuleId = "";
        private string _activeSubId    = "";

        private HwndSource? _hwndSource;

        private const double SidebarExpanded  = 256;
        private const double SidebarCollapsed = 72;
        private const int    SubItemHeight    = 40;

        // ── Duraciones de animación del sidebar (ms) ────────────────────────
        private const int SidebarWidthAnimMs        = 160; // Sidebar/BrandBlock width tween (AnimateWidth)
        private const int ContentFadeOutMs          = 50;  // ContentAreaBorder fade-out, Collapse fase 0
        private const int ContentHideDelayMs        = 55;  // espera tras fade-out antes de Visibility.Collapsed
        private const int ChromeFadeOutMs           = 70;  // labels/chevrones fade-out, Collapse fase 1
        private const int ChromeCollapseDelayMs     = 75;  // espera tras fade-out antes de Visibility.Collapsed (chrome)
        private const int CompactCardFadeMs         = 60;  // CompactUserCard fade in/out
        private const int CompactCardHideDelayMs    = 65;  // espera fade-out CompactUserCard antes de Collapsed (Expand)
        private const int SidebarWidthSettleDelayMs = 100; // espera tras iniciar el width tween del sidebar
        private const int ChromeFadeInMs            = 80;  // labels/chevrones fade-in, Expand fase 2
        private const int ChromeFadeInSettleMs      = 80;  // espera tras el fade-in del chrome antes de restaurar contenido (Expand)
        private const int ContentFadeInMs           = 80;  // ContentAreaBorder fade-in tras el width tween
        private const int ContentFadeInDelayMs      = 80;  // espera tras iniciar el fade-in de contenido antes de _animating = false

        private const int SubMenuOpenMs   = 220; // AnimateSubMenu(open: true)
        private const int SubMenuCloseMs  = 160; // AnimateSubMenu(open: false)
        private const int ChevronRotateMs = 180; // AnimateChevron duration

        private record ModuleEntry(
            Border           SubMenu,
            RotateTransform  Chevron,
            Border           Indicator,
            FrameworkElement ExpandedView,
            UIElement        CollapsedIcon);

        private record SubEntry(Border Dot, TextBlock Label, string ParentModule);

        private Dictionary<string, ModuleEntry> _moduleMap = new();
        private Dictionary<string, SubEntry>    _subMap    = new();

        // Elementos de "chrome" del sidebar (labels + chevrones + contenedores) que se
        // desvanecen/ocultan en bloque durante CollapseSidebar()/ExpandSidebar(). Definidos una sola
        // vez para que ambos métodos iteren la misma lista y no puedan desincronizarse — la causa
        // raíz de P-009 fue justamente un elemento (BrandBlock) viviendo fuera de esta lista.
        private readonly UIElement[] _sidebarChromeElements;

        // ── DWM API ───────────────────────────────────────────────────────
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR             = 34;
        private const int DWMWCP_ROUNDSMALL              = 3;
        private const int DWMWA_COLOR_NONE               = unchecked((int)0xFFFFFFFE);

        // ── Win32: área de trabajo del monitor ───────────────────────────
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT  { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int  cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }
        // ─────────────────────────────────────────────────────────────────

        public MainWindow(MainViewModel vm, IUsuarioSesionService sesionService,
                          IRealtimeService realtimeService, IConexionMonitor conexionMonitor,
                          IEmpresaRepository empresaRepository, IconoSidebarCache iconoSidebarCache,
                          ICacheService cache, IInvalidadorCacheRealtime invalidadorCache)
        {
            _sesionService   = sesionService;
            _realtimeService = realtimeService;
            _conexionMonitor = conexionMonitor;
            _empresaRepository = empresaRepository;
            _iconoSidebarCache = iconoSidebarCache;
            _cache             = cache;
            _invalidadorCache  = invalidadorCache;
            DataContext    = vm;
            InitializeComponent();

            _sidebarChromeElements = new UIElement[]
            {
                LblModuloUsuarios,  ChevUsuarios,
                LblModuloProductos, ChevProductos,
                LblModuloPesajes,   ChevPesajes,
                LblModuloReportes,  ChevReportes,
                NavLabel, HomeButtonContainer, UserCardButton, LogoContainer,
            };

            Vm.CierreRequerido += OnCierreRequerido;
            Vm.Notificaciones.SolicitarDetalle += MostrarDetalleNotificacion;

            Loaded            += OnLoaded;
            SourceInitialized += OnSourceInitialized;

            // Ctrl+K → enfocar buscador
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    TxtSearch.Focus();
                    e.Handled = true;
                }
            };
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            int round   = DWMWCP_ROUNDSMALL;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

            int noColor = DWMWA_COLOR_NONE;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref noColor, sizeof(int));

            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                var mmi     = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var info    = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                GetMonitorInfo(monitor, ref info);

                var work = info.rcWork;
                var full = info.rcMonitor;

                mmi.ptMaxPosition.X  = Math.Abs(work.Left - full.Left);
                mmi.ptMaxPosition.Y  = Math.Abs(work.Top  - full.Top);
                mmi.ptMaxSize.X      = Math.Abs(work.Right  - work.Left);
                mmi.ptMaxSize.Y      = Math.Abs(work.Bottom - work.Top);

                // ptMinTrackSize es en píxeles físicos; MinWidth/MinHeight de WPF son
                // DIPs (1/96"). Hay que escalar por el DPI real del monitor actual —
                // sin esto, en pantallas >100% el mínimo nativo quedaría más chico
                // que el que pide el XAML, y se podría volver a achicar de más.
                var dpi = VisualTreeHelper.GetDpi(this);
                mmi.ptMinTrackSize.X = (int)(MinWidth  * dpi.DpiScaleX);
                mmi.ptMinTrackSize.Y = (int)(MinHeight * dpi.DpiScaleY);

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Módulos
            _moduleMap["usuarios"]  = new(SubUsuarios,  ChevUsuariosRot,  IndUsuarios,  ExpUsuarios,  IcoUsuarios);
            _moduleMap["productos"] = new(SubProductos, ChevProductosRot, IndProductos, ExpProductos, IcoProductos);
            _moduleMap["pesajes"]   = new(SubPesajes,   ChevPesajesRot,   IndPesajes,   ExpPesajes,   IcoPesajes);
            _moduleMap["reportes"]  = new(SubReportes,  ChevReportesRot,  IndReportes,  ExpReportes,  IcoReportes);

            // Sub-items — Usuarios
            _subMap["usuarios-sub"] = new(DotUsuariosSub, LblUsuariosSub, "usuarios");
            _subMap["empleados"]    = new(DotEmpleados,   LblEmpleados,   "usuarios");
            _subMap["roles"]        = new(DotRoles,       LblRoles,       "usuarios");
            _subMap["bitacora"]     = new(DotBitacora,    LblBitacora,    "usuarios");

            // Sub-items — Productos
            _subMap["productos-sub"]         = new(DotProductosSub,         LblProductosSub,         "productos");
            _subMap["proveedores"]           = new(DotProveedores,          LblProveedores,          "productos");
            _subMap["fabricantes"]           = new(DotFabricantes,          LblFabricantes,          "productos");
            _subMap["categorias"]            = new(DotCategorias,           LblCategorias,           "productos");
            _subMap["presentaciones"]        = new(DotPresentaciones,       LblPresentaciones,       "productos");
            _subMap["contactos-proveedores"] = new(DotContactosProveedores, LblContactosProveedores, "productos");
            _subMap["contactos-fabricantes"] = new(DotContactosFabricantes, LblContactosFabricantes, "productos");

            // Sub-items — Pesajes
            _subMap["pesajes-sub"] = new(DotPesajes, LblPesajes, "pesajes");

            // Sub-items — Reportería
            _subMap["dashboard"]      = new(DotDashboard,     LblDashboard,     "reportes");
            _subMap["crear-reportes"] = new(DotCrearReportes, LblCrearReportes, "reportes");

            // Arrancar el monitor de conexión (ya estamos logueados y en el hilo de UI)
            _conexionMonitor.Iniciar();

            // Suscribir el invalidador de caché EN CADA SESIÓN. Al cerrar sesión,
            // RealtimeService vacía su diccionario de suscriptores, y este servicio es
            // singleton: su constructor no vuelve a correr. Sin esta llamada, a partir
            // del segundo login de la máquina la invalidación reactiva quedaría muerta
            // en silencio y la caché serviría datos viejos hasta que venza el TTL.
            // Va antes que las notificaciones para que quede escuchando desde el
            // primer instante de la sesión.
            _invalidadorCache.Suscribir();

            await Vm.Notificaciones.InicializarAsync();
            await CargarIconoSidebarAsync();
        }

        private void MostrarDetalleNotificacion(CapaAplicacion.Notificaciones.Dtos.NotificacionDto notificacion)
            => new Pantallas.Notificaciones.NotificacionDetalleWindow(notificacion) { Owner = this }.ShowDialog();

        private async Task CargarIconoSidebarAsync(string? rutaStorage = null)
        {
            try
            {
                if (rutaStorage is null)
                {
                    var resultado = await _empresaRepository.ObtenerAsync();
                    if (!resultado.Success) return;
                    rutaStorage = resultado.Value?.IconoSidebar;
                }

                var rutaLocal = await _iconoSidebarCache.ObtenerRutaLocalAsync(rutaStorage);
                if (!string.IsNullOrWhiteSpace(rutaLocal))
                    ImgIconoSidebar.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(rutaLocal));
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "No se pudo aplicar el ícono dinámico del sidebar");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  HAMBURGER
        // ══════════════════════════════════════════════════════════════════
        private async void BtnHamburger_Click(object sender, RoutedEventArgs e)
        {
            if (_animating) return;
            if (_collapsed) await ExpandSidebar();
            else            await CollapseSidebar();
        }

        private async Task CollapseSidebar()
        {
            _animating = true;
            _collapsed = true;

            // Si hay un modal abierto, el telón se pone oscuro para que el hueco de
            // ContentAreaBorder (fases 0/3 más abajo) no se vea como flash blanco.
            var modalBrush = ObtenerModalOverlayBrush();
            if (modalBrush != null) ContentBackdrop.Background = modalBrush;

            // Cerrar submenús y chevrones al instante
            foreach (var entry in _moduleMap.Values)
            {
                entry.Chevron.Angle = 0;
                AnimateSubMenu(entry.SubMenu, false, 0);
            }

            // Fase 0 — ocultar contenido pesado (DataGrids) antes de animar
            AnimateOpacity(ContentAreaBorder, 0, ContentFadeOutMs);
            await Task.Delay(ContentHideDelayMs);
            ContentAreaBorder.Visibility = Visibility.Collapsed;

            // Fase 1 — desvanecer etiquetas (70ms) + Sidebar.Width (160ms)
            // Con el contenido oculto, el layout tree es ligero → animación suave
            foreach (var el in _sidebarChromeElements)
                AnimateOpacity(el, 0, ChromeFadeOutMs);

            foreach (var entry in _moduleMap.Values)
                AnimateOpacity(entry.ExpandedView, 0, ChromeFadeOutMs);

            AnimateSidebarWidth(SidebarCollapsed);

            // Fase 2 — tras el fade, colapsar con Visibility (ya invisibles, sin salto)
            await Task.Delay(ChromeCollapseDelayMs);

            foreach (var el in _sidebarChromeElements)
                el.Visibility = Visibility.Collapsed;

            foreach (var entry in _moduleMap.Values)
            {
                entry.ExpandedView.Visibility = Visibility.Collapsed;
                entry.ExpandedView.Opacity    = 1;

                entry.CollapsedIcon.Opacity    = 0;
                entry.CollapsedIcon.Visibility = Visibility.Visible;
                AnimateOpacity(entry.CollapsedIcon, 1, CompactCardFadeMs);
            }

            // Restituir opacidad para la próxima expansión
            foreach (var el in _sidebarChromeElements)
                el.Opacity = 1;

            // Mostrar tarjeta compacta con fade-in
            CompactUserCard.Opacity    = 0;
            CompactUserCard.Visibility = Visibility.Visible;
            AnimateOpacity(CompactUserCard, 1, CompactCardFadeMs);

            // Fase 3 — restaurar contenido tras la animación de ancho
            await Task.Delay(SidebarWidthSettleDelayMs);
            ContentAreaBorder.Visibility = Visibility.Visible;
            ContentAreaBorder.Opacity    = 0;
            AnimateOpacity(ContentAreaBorder, 1, ContentFadeInMs);

            await Task.Delay(ContentFadeInDelayMs);
            if (modalBrush != null) ContentBackdrop.Background = Brushes.Transparent;
            _animating = false;
        }

        private async Task ExpandSidebar()
        {
            _animating = true;
            _collapsed = false;

            var modalBrush = ObtenerModalOverlayBrush();
            if (modalBrush != null) ContentBackdrop.Background = modalBrush;

            // Fase 0 — ocultar contenido pesado antes de animar
            ContentAreaBorder.Visibility = Visibility.Collapsed;

            // Fase 1 — desvanecer tarjeta compacta e iconos colapsados (60 ms)
            AnimateOpacity(CompactUserCard, 0, CompactCardFadeMs);
            foreach (var entry in _moduleMap.Values)
                AnimateOpacity(entry.CollapsedIcon, 0, CompactCardFadeMs);

            await Task.Delay(CompactCardHideDelayMs);
            CompactUserCard.Visibility = Visibility.Collapsed;

            // Hacer visibles los elementos pero en opacidad 0 (sin salto al hacer Visible)
            foreach (var el in _sidebarChromeElements)
            {
                el.Visibility = Visibility.Visible;
                el.Opacity    = 0;
            }

            foreach (var entry in _moduleMap.Values)
            {
                entry.CollapsedIcon.Visibility = Visibility.Collapsed;
                entry.ExpandedView.Visibility  = Visibility.Visible;
                entry.ExpandedView.Opacity     = 0;
            }

            AnimateSidebarWidth(SidebarExpanded);

            // Fase 2 — fade-in del contenido del sidebar
            await Task.Delay(SidebarWidthSettleDelayMs);

            foreach (var el in _sidebarChromeElements)
                AnimateOpacity(el, 1, ChromeFadeInMs);

            foreach (var entry in _moduleMap.Values)
                AnimateOpacity(entry.ExpandedView, 1, ChromeFadeInMs);

            // Reabrir el submenú activo tras la expansión
            if (!string.IsNullOrEmpty(_activeModuleId) &&
                _moduleMap.TryGetValue(_activeModuleId, out var active))
            {
                int count = GetSubCount(_activeModuleId);
                AnimateSubMenu(active.SubMenu, true, count);
                AnimateChevron(active.Chevron, 180);
            }

            // Fase 3 — restaurar contenido tras la animación de ancho
            await Task.Delay(ChromeFadeInSettleMs);
            ContentAreaBorder.Visibility = Visibility.Visible;
            ContentAreaBorder.Opacity    = 0;
            AnimateOpacity(ContentAreaBorder, 1, ContentFadeInMs);

            await Task.Delay(ContentFadeInDelayMs);
            if (modalBrush != null) ContentBackdrop.Background = Brushes.Transparent;
            _animating = false;
        }

        // ══════════════════════════════════════════════════════════════════
        //  MÓDULOS — acordeón
        // ══════════════════════════════════════════════════════════════════
        private async void BtnModulo_Click(object sender, RoutedEventArgs e)
        {
            if (_animating) return;

            var btn = (Button)sender;
            string id = (string)btn.Tag;

            if (_collapsed)
            {
                await ExpandSidebar();
                OpenModule(id);
                return;
            }

            bool isOpen = _activeModuleId == id && GetCurrentSubMenuHeight(id) > 0;
            CloseAllModules();
            if (!isOpen) OpenModule(id);
        }

        private void OpenModule(string id)
        {
            if (!_moduleMap.TryGetValue(id, out var entry)) return;

            _activeModuleId = id;
            int count = GetSubCount(id);
            AnimateSubMenu(entry.SubMenu, true, count);
            AnimateChevron(entry.Chevron, 180);
        }

        private void CloseAllModules()
        {
            bool activeSubHasParent = _subMap.TryGetValue(_activeSubId, out var activeSub);

            foreach (var kv in _moduleMap)
            {
                AnimateSubMenu(kv.Value.SubMenu, false, 0);
                AnimateChevron(kv.Value.Chevron, 0);

                // Mantener el indicador del módulo que tiene el subitem activo seleccionado
                bool ownsActiveSub = activeSubHasParent && activeSub!.ParentModule == kv.Key;
                if (!ownsActiveSub)
                    kv.Value.Indicator.Visibility = Visibility.Collapsed;
            }
        }

        // Módulos directos (Reportería)
        private void BtnModuloDirect_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            string id = (string)btn.Tag;

            ClearActiveStates();
            _activeModuleId        = id;
            _activeSubId           = id;
            IndReportes.Visibility = Visibility.Visible;

            Vm.NavigateCommand.Execute(id);
        }

        // ══════════════════════════════════════════════════════════════════
        //  SUBMÓDULOS
        // ══════════════════════════════════════════════════════════════════
        private void BtnSub_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            string subId = (string)btn.Tag;

            foreach (var kv in _subMap)
            {
                kv.Value.Dot.Visibility   = Visibility.Collapsed;
                kv.Value.Label.FontWeight = FontWeights.Normal;
                kv.Value.Label.Foreground = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString("#D9FFFFFF"));
            }
            foreach (var kv in _moduleMap)
                kv.Value.Indicator.Visibility = Visibility.Collapsed;

            _activeSubId = subId;

            if (_subMap.TryGetValue(subId, out var sub))
            {
                sub.Dot.Visibility   = Visibility.Visible;
                sub.Label.FontWeight = FontWeights.SemiBold;
                sub.Label.Foreground = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString("#6EE7B7"));
                _activeModuleId = sub.ParentModule;
                if (_moduleMap.TryGetValue(sub.ParentModule, out var parent))
                    parent.Indicator.Visibility = Visibility.Visible;
            }

            Vm.NavigateCommand.Execute(subId);
        }

        private void ClearActiveStates()
        {
            foreach (var kv in _subMap)
            {
                kv.Value.Dot.Visibility   = Visibility.Collapsed;
                kv.Value.Label.FontWeight = FontWeights.Normal;
                kv.Value.Label.Foreground = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString("#D9FFFFFF"));
            }
            foreach (var kv in _moduleMap)
                kv.Value.Indicator.Visibility = Visibility.Collapsed;

            IndReportes.Visibility = Visibility.Collapsed;
            _activeSubId           = "";
        }

        // ══════════════════════════════════════════════════════════════════
        //  HOME / MI USUARIO
        // ══════════════════════════════════════════════════════════════════
        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            ClearActiveStates();
            _activeModuleId = "";
            Vm.NavigateCommand.Execute(Routes.Bienvenida);
        }

        private void UserCard_Click(object sender, MouseButtonEventArgs e)
        {
            ClearActiveStates();
            _activeModuleId = Routes.MiUsuario;
            Vm.NavigateCommand.Execute(Routes.MiUsuario);
        }

        // ══════════════════════════════════════════════════════════════════
        //  NOTIFICACIONES
        // ══════════════════════════════════════════════════════════════════
        private const int DebounceNotifMs = 300;
        private DateTime _ultimoClicNotif = DateTime.MinValue;
        private DateTime _momentoCierrePopupNotif = DateTime.MinValue;

        private void NotifPopup_Closed(object? sender, EventArgs e)
        {
            // Registramos el momento de cierre sin depender de IsMouseOver,
            // garantizando compatibilidad con pantallas táctiles donde el contacto
            // del dedo se levanta antes de que el Dispatcher procese el evento Closed.
            _momentoCierrePopupNotif = DateTime.UtcNow;
        }

        private void BtnNotif_Click(object sender, RoutedEventArgs e)
        {
            var ahora = DateTime.UtcNow;

            // 1. Debounce contra martilleo y multitoques rápidos (pantalla táctil o doble clic accidental):
            // Si ocurren toques sucesivos en menos de 300 ms, se descartan para evitar
            // saturar la cola de mensajes y la creación/destrucción de ventanas Win32 (HWND).
            if ((ahora - _ultimoClicNotif).TotalMilliseconds < DebounceNotifMs)
            {
                return;
            }
            _ultimoClicNotif = ahora;

            // 2. Si el popup se acaba de cerrar a raíz del TouchDown/MouseDown de este mismo toque
            // (comportamiento StaysOpen="False" de WPF), evitamos invertir el estado y reabrirlo.
            if ((ahora - _momentoCierrePopupNotif).TotalMilliseconds < DebounceNotifMs)
            {
                _momentoCierrePopupNotif = DateTime.MinValue;
                return;
            }

            NotifPopup.IsOpen = !NotifPopup.IsOpen;
        }

        private void BtnVerNotificaciones_Click(object sender, RoutedEventArgs e)
        {
            NotifPopup.IsOpen = false;
            ClearActiveStates();
            Vm.NavigateCommand.Execute(Routes.Notificaciones);
        }

        private void BtnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarConfiguracion) ||
                ConfiguracionOverlay.Visibility == Visibility.Visible)
                return;

            NotifPopup.IsOpen = false;
            var vm = App.CrearVm<ConfiguracionEmpresaViewModel>();
            var modal = new ConfiguracionEmpresaModal(vm);
            vm.SolicitarCierre += CerrarConfiguracion;
            vm.Guardado += OnConfiguracionGuardada;

            ConfiguracionContent.Content = modal;
            ConfiguracionOverlay.Visibility = Visibility.Visible;
        }

        private void CerrarConfiguracion()
        {
            if (ConfiguracionContent.Content is ConfiguracionEmpresaModal modal)
            {
                modal.ViewModel.SolicitarCierre -= CerrarConfiguracion;
                modal.ViewModel.Guardado -= OnConfiguracionGuardada;
            }

            ConfiguracionOverlay.Visibility = Visibility.Collapsed;
            ConfiguracionContent.Content = null;
        }

        private async void OnConfiguracionGuardada(EmpresaGuardadaDto resultado)
        {
            await CargarIconoSidebarAsync(resultado.Empresa.IconoSidebar);
            CerrarConfiguracion();
            if (resultado.Advertencias.Count > 0)
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine, resultado.Advertencias),
                    "Configuración guardada con advertencias",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  BÚSQUEDA
        // ══════════════════════════════════════════════════════════════════
        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (string.IsNullOrWhiteSpace(TxtSearch.Text)) return;
            Vm.BuscarCommand.Execute(TxtSearch.Text);
            e.Handled = true;
        }

        private void TxtSearch_Changed(object sender, TextChangedEventArgs e)
            => SearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
               ? Visibility.Visible : Visibility.Collapsed;

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchPlaceholder.Visibility = Visibility.Collapsed;
            // Cerrar popup de notificaciones al enfocar
            NotifPopup.IsOpen = false;
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtSearch.Text))
                SearchPlaceholder.Visibility = Visibility.Visible;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Chrome
        // ══════════════════════════════════════════════════════════════════
        private void BtnMinimizar_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximizar_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                           ? WindowState.Normal
                           : WindowState.Maximized;

        private void BtnCerrarVentana_Click(object sender, RoutedEventArgs e) => Close();

        private void OnCierreRequerido(object? s, EventArgs e) => HandleCerrarSesionAsync();

        // ══════════════════════════════════════════════════════════════════
        //  Cierre — X / Alt+F4 → salir app; botón "Cerrar sesión" → logout
        // ══════════════════════════════════════════════════════════════════
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_cerrando) { base.OnClosing(e); return; }

            e.Cancel = true;
            HandleSalirAplicacionAsync();
        }

        // Cerrar sesión: confirma → limpia → vuelve al login
        private async void HandleCerrarSesionAsync()
        {
            var resultado = MessageBox.Show(
                "¿Deseas cerrar sesión?", "Cerrar sesión",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (resultado != MessageBoxResult.Yes) return;

            await LimpiarRecursosAsync();

            _cerrando = true;
            SesionCerrada?.Invoke(this, EventArgs.Empty);
            Close();
        }

        // Salir de la aplicación: advertencia → limpia → apaga app
        private async void HandleSalirAplicacionAsync()
        {
            var resultado = MessageBox.Show(
                "Asegúrese de guardar todos los cambios, de lo contrario podría perderlos.\n\n¿Desea salir del sistema?",
                "Salir del sistema",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (resultado != MessageBoxResult.Yes) return;

            await LimpiarRecursosAsync();

            _cerrando = true;
            Application.Current.Shutdown();
        }

        // Cleanup compartido por ambos caminos
        private async Task LimpiarRecursosAsync()
        {
            try
            {
                var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();

                // SignOut() no acepta CancellationToken, así que el tope se impone por fuera.
                // Antes había un CancellationTokenSource(5s) que no se le pasaba a nada: el
                // "timeout" era decorativo y con la red colgada el cierre se iba hasta el
                // timeout por defecto de HttpClient (100 s) con la UI congelada.
                //
                // Es best-effort: invalida el refresh token en el servidor —importante en
                // terminal compartida— pero no puede bloquear el cierre de sesión. El timer
                // de refresco lo corta igual Auth.Shutdown() dentro de ConexionSupabase.ResetAsync().
                var signOut = client.Auth.SignOut();
                if (await Task.WhenAny(signOut, Task.Delay(TimeSpan.FromSeconds(5))) != signOut)
                {
                    Serilog.Log.Warning("[Cierre] SignOut no respondió en 5s — se continúa con el cierre local");
                    // El SignOut abandonado sigue vivo: si falla más tarde nadie estaría
                    // mirando su excepción. Se observa acá para que no quede sin manejar.
                    _ = signOut.ContinueWith(
                        t => Serilog.Log.Debug(t.Exception, "[Cierre] SignOut tardío falló"),
                        TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[Cierre] Error en SignOut — se continúa con el cierre local");
            }

            CapaUI.Core.Permisos.SesionPermisos.Limpiar();
            _sesionService.CerrarSesion();

            // Liberar hook, desuscribir eventos, disponer VM
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
            Vm.CierreRequerido -= OnCierreRequerido;
            Vm.Notificaciones.SolicitarDetalle -= MostrarDetalleNotificacion;
            Vm.Dispose();

            // Purga total de la caché. NO es opcional: App.Services es un contenedor
            // raíz que nunca se reconstruye, así que el logout devuelve al login dentro
            // del mismo proceso. Sin esto, los catálogos que cacheó un supervisor se le
            // sirven al operario que entra después en la misma terminal — RLS filtra en
            // el servidor, y la caché es justamente lo que evita ir al servidor.
            await _cache.LimpiarTodoAsync();

            // Dar de baja los observadores ANTES de desconectar, para soltarlos de forma
            // limpia en vez de dejarlos huérfanos cuando se vacíe el diccionario.
            _invalidadorCache.Desuscribir();

            // Detener el monitor de conexión (deja de vigilar la red entre sesiones)
            _conexionMonitor.Detener();

            // Cerrar todos los canales Realtime y desconectar WebSocket
            await _realtimeService.DesconectarAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Animaciones
        // ══════════════════════════════════════════════════════════════════
        // No es static como el resto: necesita los campos de instancia Sidebar/BrandBlock.
        // Une ambos anchos en un único call site para que no puedan volver a desincronizarse (P-009).
        private void AnimateSidebarWidth(double to)
        {
            AnimateWidth(Sidebar,    to, SidebarWidthAnimMs);
            AnimateWidth(BrandBlock, to, SidebarWidthAnimMs);
        }

        private static void AnimateWidth(FrameworkElement target, double to, int ms)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            target.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }

        private static void AnimateSubMenu(Border border, bool open, int itemCount)
        {
            double from = border.ActualHeight;
            double target;

            if (open)
            {
                // Medir el contenido real en vez de asumir 40px/ítem: con el escalado
                // de texto de Windows cada subítem crece y el alto fijo cortaba el
                // último ("Contacto Fabricante" quedaba recortado por ClipToBounds).
                var content = (FrameworkElement)border.Child;
                double w = border.ActualWidth > 0 ? border.ActualWidth : double.PositiveInfinity;
                content.Measure(new Size(w, double.PositiveInfinity));
                target = content.DesiredSize.Height;
            }
            else
            {
                target = 0;
            }

            // EaseOut: el acordeón arranca de golpe y desacelera al final (más natural).
            // From explícito: tras abrir dejamos MaxHeight = PositiveInfinity como valor
            // base, así que sin From el cierre animaría desde infinito.
            var anim = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(open ? SubMenuOpenMs : SubMenuCloseMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            if (open)
            {
                // Al terminar, liberar el tope: si cambia la escala mientras el
                // submenú está abierto (o hay ítems ocultos por permisos), el
                // StackPanel puede crecer sin volver a quedar recortado.
                anim.Completed += (_, _) =>
                {
                    border.BeginAnimation(Border.MaxHeightProperty, null);
                    border.MaxHeight = double.PositiveInfinity;
                };
            }

            border.BeginAnimation(Border.MaxHeightProperty, anim);
        }

        private static void AnimateChevron(RotateTransform rt, double angle)
        {
            var anim = new DoubleAnimation(angle, TimeSpan.FromMilliseconds(ChevronRotateMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            rt.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        private static void AnimateOpacity(UIElement target, double to, int ms)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };
            target.BeginAnimation(UIElement.OpacityProperty, anim);
        }


        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════
        private static int GetSubCount(string moduleId) => moduleId switch
        {
            "usuarios"  => 4,
            "productos" => 7,
            "pesajes"   => 1,
            "reportes"  => 2,
            _           => 0
        };

        private double GetCurrentSubMenuHeight(string moduleId)
        {
            if (_moduleMap.TryGetValue(moduleId, out var entry))
                return entry.SubMenu.MaxHeight;
            return 0;
        }

        // ContentArea.Content es el ViewModel (la View real la genera el DataTemplate), así que
        // para saber si la View activa tiene su "ModalOverlay" visible hay que buscarlo en el
        // árbol visual ya renderizado en vez de mirar el Content directamente. Devuelve el mismo
        // Brush semitransparente que ya usa esa View (varía: "#990F172A", "#8C0F172A", …) para que
        // el backdrop anti-flash se vea idéntico a la sombra real del modal, no un tono inventado.
        private Brush? ObtenerModalOverlayBrush()
            => FindNamedChild(ContentArea, "ModalOverlay") is Border { Visibility: Visibility.Visible } overlay
               ? overlay.Background
               : null;

        private static FrameworkElement? FindNamedChild(DependencyObject parent, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name)
                    return fe;

                var found = FindNamedChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
