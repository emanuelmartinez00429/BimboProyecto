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
using CapaAplicacion.Conexion;
using CapaAplicacion.Perfil;
using CapaAplicacion.Realtime;
using CapaDominio;
using CapaUI.Navigation;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace CapaUI.Formularios.Principal
{
    public partial class MainWindow : Window
    {
        public event EventHandler? SesionCerrada;

        private MainViewModel Vm => (MainViewModel)DataContext;
        private bool _cerrando = false;
        private readonly IPerfilUsuarioService _perfilService;
        private readonly IRealtimeService      _realtimeService;
        private readonly IConexionMonitor      _conexionMonitor;

        // ── Estado del sidebar ────────────────────────────────────────────
        private bool   _collapsed      = false;
        private bool   _animating      = false;
        private string _activeModuleId = "";
        private string _activeSubId    = "";

        private HwndSource? _hwndSource;

        private const double SidebarExpanded  = 226;
        private const double SidebarCollapsed = 72;
        private const int    SubItemHeight    = 40;

        private record ModuleEntry(
            Border           SubMenu,
            RotateTransform  Chevron,
            Border           Indicator,
            FrameworkElement ExpandedView,
            UIElement        CollapsedIcon);

        private record SubEntry(Border Dot, TextBlock Label, string ParentModule);

        private Dictionary<string, ModuleEntry> _moduleMap = new();
        private Dictionary<string, SubEntry>    _subMap    = new();

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

        public MainWindow(MainViewModel vm, IPerfilUsuarioService perfilService,
                          IRealtimeService realtimeService, IConexionMonitor conexionMonitor)
        {
            _perfilService   = perfilService;
            _realtimeService = realtimeService;
            _conexionMonitor = conexionMonitor;
            DataContext    = vm;
            InitializeComponent();

            MinWidth  = 600;
            MinHeight = 400;

            Vm.CierreRequerido += OnCierreRequerido;

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
                mmi.ptMinTrackSize.X = 600;
                mmi.ptMinTrackSize.Y = 400;

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
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
            _subMap["contactos-proveedores"] = new(DotContactosProveedores, LblContactosProveedores, "productos");
            _subMap["contactos-fabricantes"] = new(DotContactosFabricantes, LblContactosFabricantes, "productos");

            // Sub-items — Pesajes
            _subMap["pesajes-sub"] = new(DotPesajes, LblPesajes, "pesajes");

            // Sub-items — Reportería
            _subMap["dashboard"]      = new(DotDashboard,     LblDashboard,     "reportes");
            _subMap["crear-reportes"] = new(DotCrearReportes, LblCrearReportes, "reportes");

            // Arrancar el monitor de conexión (ya estamos logueados y en el hilo de UI)
            _conexionMonitor.Iniciar();
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

            // Pinchar al tamaño FINAL: ContentArea ya ocupa su destino desde el frame 0,
            // así el layout no recalcula en cada frame y no hay salto al terminar.
            ContentArea.Width = ContentArea.ActualWidth + Sidebar.ActualWidth - SidebarCollapsed;

            // Cerrar submenús y chevrones al instante
            foreach (var entry in _moduleMap.Values)
            {
                entry.Chevron.Angle = 0;
                AnimateSubMenu(entry.SubMenu, false, 0);
            }

            // Fase 1 — desvanecer etiquetas, chevrones y elementos extra (70 ms)
            // Los iconos de módulo dentro del DockPanel NO se tocan — quedan visibles
            AnimateOpacity(LblModuloUsuarios,   0, 70);
            AnimateOpacity(ChevUsuarios,         0, 70);
            AnimateOpacity(LblModuloProductos,   0, 70);
            AnimateOpacity(ChevProductos,        0, 70);
            AnimateOpacity(LblModuloPesajes,     0, 70);
            AnimateOpacity(ChevPesajes,          0, 70);
            AnimateOpacity(LblModuloReportes,    0, 70);
            AnimateOpacity(ChevReportes,         0, 70);
            AnimateOpacity(NavLabel,             0, 70);
            AnimateOpacity(HomeButtonContainer,  0, 70);
            AnimateOpacity(LogoContainer,        0, 70);
            AnimateOpacity(UserCardButton,       0, 70);

            // Animar ancho simultáneamente — QuarticEase.EaseOut arranca rápido
            AnimateWidth(Sidebar,    SidebarCollapsed, 160);
            AnimateWidth(BrandBlock, SidebarCollapsed, 160);

            // Fase 2 — tras el fade, colapsar con Visibility (ya invisibles, sin salto)
            await Task.Delay(75);

            LblModuloUsuarios.Visibility  = Visibility.Collapsed;
            ChevUsuarios.Visibility       = Visibility.Collapsed;
            LblModuloProductos.Visibility = Visibility.Collapsed;
            ChevProductos.Visibility      = Visibility.Collapsed;
            LblModuloPesajes.Visibility   = Visibility.Collapsed;
            ChevPesajes.Visibility        = Visibility.Collapsed;
            LblModuloReportes.Visibility  = Visibility.Collapsed;
            ChevReportes.Visibility       = Visibility.Collapsed;
            NavLabel.Visibility           = Visibility.Collapsed;
            HomeButtonContainer.Visibility = Visibility.Collapsed;
            UserCardButton.Visibility     = Visibility.Collapsed;
            LogoContainer.Visibility      = Visibility.Collapsed;

            // Restituir opacidad para la próxima expansión
            LblModuloUsuarios.Opacity   = 1;
            ChevUsuarios.Opacity        = 1;
            LblModuloProductos.Opacity  = 1;
            ChevProductos.Opacity       = 1;
            LblModuloPesajes.Opacity    = 1;
            ChevPesajes.Opacity         = 1;
            LblModuloReportes.Opacity   = 1;
            ChevReportes.Opacity        = 1;
            NavLabel.Opacity            = 1;
            HomeButtonContainer.Opacity = 1;
            UserCardButton.Opacity      = 1;
            LogoContainer.Opacity       = 1;

            // Mostrar tarjeta compacta con fade-in
            CompactUserCard.Opacity    = 0;
            CompactUserCard.Visibility = Visibility.Visible;
            AnimateOpacity(CompactUserCard, 1, 60);

            // Esperar a que termine la animación de ancho antes de liberar el guard
            await Task.Delay(90);
            ContentArea.Width = double.NaN; // liberar: ContentControl vuelve a Width="*"
            _animating = false;
        }

        private async Task ExpandSidebar()
        {
            _animating = true;
            _collapsed = false;

            // Pinchar al tamaño FINAL
            ContentArea.Width = ContentArea.ActualWidth + Sidebar.ActualWidth - SidebarExpanded;

            // Fase 1 — desvanecer tarjeta compacta (60 ms)
            AnimateOpacity(CompactUserCard, 0, 60);
            await Task.Delay(65);
            CompactUserCard.Visibility = Visibility.Collapsed;

            // Hacer visibles los elementos pero en opacidad 0 (sin salto al hacer Visible)
            LblModuloUsuarios.Visibility  = Visibility.Visible;  LblModuloUsuarios.Opacity  = 0;
            ChevUsuarios.Visibility       = Visibility.Visible;  ChevUsuarios.Opacity       = 0;
            LblModuloProductos.Visibility = Visibility.Visible;  LblModuloProductos.Opacity = 0;
            ChevProductos.Visibility      = Visibility.Visible;  ChevProductos.Opacity      = 0;
            LblModuloPesajes.Visibility   = Visibility.Visible;  LblModuloPesajes.Opacity   = 0;
            ChevPesajes.Visibility        = Visibility.Visible;  ChevPesajes.Opacity        = 0;
            LblModuloReportes.Visibility  = Visibility.Visible;  LblModuloReportes.Opacity  = 0;
            ChevReportes.Visibility       = Visibility.Visible;  ChevReportes.Opacity       = 0;
            NavLabel.Visibility            = Visibility.Visible; NavLabel.Opacity            = 0;
            HomeButtonContainer.Visibility = Visibility.Visible; HomeButtonContainer.Opacity = 0;
            UserCardButton.Visibility      = Visibility.Visible; UserCardButton.Opacity      = 0;
            LogoContainer.Visibility       = Visibility.Visible; LogoContainer.Opacity       = 0;

            foreach (var entry in _moduleMap.Values)
                entry.CollapsedIcon.Visibility = Visibility.Collapsed;
            IcoReportes.Visibility = Visibility.Collapsed;

            // Animar ancho
            AnimateWidth(Sidebar,    SidebarExpanded, 160);
            AnimateWidth(BrandBlock, SidebarExpanded, 160);

            // Fase 2 — cuando el sidebar ya casi completó la expansión, fade-in del contenido
            await Task.Delay(100);

            AnimateOpacity(LblModuloUsuarios,   1, 80);
            AnimateOpacity(ChevUsuarios,         1, 80);
            AnimateOpacity(LblModuloProductos,   1, 80);
            AnimateOpacity(ChevProductos,        1, 80);
            AnimateOpacity(LblModuloPesajes,     1, 80);
            AnimateOpacity(ChevPesajes,          1, 80);
            AnimateOpacity(LblModuloReportes,    1, 80);
            AnimateOpacity(ChevReportes,         1, 80);
            AnimateOpacity(NavLabel,             1, 80);
            AnimateOpacity(HomeButtonContainer,  1, 80);
            AnimateOpacity(UserCardButton,       1, 80);
            AnimateOpacity(LogoContainer,        1, 80);

            // Reabrir el submenú activo tras la expansión
            if (!string.IsNullOrEmpty(_activeModuleId) &&
                _moduleMap.TryGetValue(_activeModuleId, out var active))
            {
                int count = GetSubCount(_activeModuleId);
                AnimateSubMenu(active.SubMenu, true, count);
                AnimateChevron(active.Chevron, 180);
            }

            await Task.Delay(80);
            ContentArea.Width = double.NaN; // liberar: ContentControl vuelve a Width="*"
            _animating = false;
        }

        // ══════════════════════════════════════════════════════════════════
        //  MÓDULOS — acordeón
        // ══════════════════════════════════════════════════════════════════
        private async void BtnModulo_Click(object sender, RoutedEventArgs e)
        {
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
        private void BtnNotif_Click(object sender, RoutedEventArgs e)
            => NotifPopup.IsOpen = !NotifPopup.IsOpen;

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
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
                await client.Auth.SignOut();
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Cierre] SignOut timeout — continuando de todas formas");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cierre] Error en SignOut: {ex.Message}");
            }

            CapaUI.Core.Permisos.SesionPermisos.Limpiar();
            _perfilService.Limpiar();
            servicioSesionActual.Cerrar();

            // Liberar hook, desuscribir eventos, disponer VM
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
            Vm.CierreRequerido -= OnCierreRequerido;
            Vm.Dispose();

            // Detener el monitor de conexión (deja de vigilar la red entre sesiones)
            _conexionMonitor.Detener();

            // Cerrar todos los canales Realtime y desconectar WebSocket
            await _realtimeService.DesconectarAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Animaciones
        // ══════════════════════════════════════════════════════════════════
        private static void AnimateWidth(FrameworkElement target, double to, int ms)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            {
                // QuarticEase.EaseOut: arranca rápido y frena suave — elimina el "trabado" inicial
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            target.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }

        private static void AnimateSubMenu(Border border, bool open, int itemCount)
        {
            double target = open ? itemCount * SubItemHeight + 8 : 0;
            // EaseOut: el acordeón arranca de golpe y desacelera al final (más natural)
            var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(open ? 220 : 160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            border.BeginAnimation(Border.MaxHeightProperty, anim);
        }

        private static void AnimateChevron(RotateTransform rt, double angle)
        {
            var anim = new DoubleAnimation(angle, TimeSpan.FromMilliseconds(180))
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
            "productos" => 6,
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
    }
}
