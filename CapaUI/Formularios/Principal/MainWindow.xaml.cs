using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace CapaUI.Formularios.Principal
{
    public partial class MainWindow : Window
    {
        public event EventHandler? SesionCerrada;

        private MainViewModel Vm => (MainViewModel)DataContext;

        // ── Estado del sidebar ────────────────────────────────────────────
        private bool   _collapsed      = false;
        private string _activeModuleId = "";
        private string _activeSubId    = "";

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

        public MainWindow()
        {
            InitializeComponent();

            MinWidth  = SystemParameters.PrimaryScreenWidth  / 2;
            MinHeight = SystemParameters.PrimaryScreenHeight / 2;

            Vm.SesionCerrada += (_, _) => SesionCerrada?.Invoke(this, EventArgs.Empty);

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

            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
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

                mmi.ptMaxPosition.X = Math.Abs(work.Left - full.Left);
                mmi.ptMaxPosition.Y = Math.Abs(work.Top  - full.Top);
                mmi.ptMaxSize.X     = Math.Abs(work.Right  - work.Left);
                mmi.ptMaxSize.Y     = Math.Abs(work.Bottom - work.Top);

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
        }

        // ══════════════════════════════════════════════════════════════════
        //  HAMBURGER
        // ══════════════════════════════════════════════════════════════════
        private void BtnHamburger_Click(object sender, RoutedEventArgs e)
        {
            if (_collapsed) ExpandSidebar();
            else            CollapseSidebar();
        }

        private void CollapseSidebar()
        {
            _collapsed = true;

            foreach (var entry in _moduleMap.Values)
                AnimateSubMenu(entry.SubMenu, false, 0);

            AnimateWidth(Sidebar,    SidebarCollapsed, 200);
            AnimateWidth(BrandBlock, SidebarCollapsed, 200);

            // Ocultar solo etiquetas y chevrones, mantener iconos visibles
            foreach (var entry in _moduleMap.Values)
            {
                entry.Chevron.Angle = 0;
            }

            // Usuarios
            LblModuloUsuarios.Visibility = Visibility.Collapsed;
            ChevUsuarios.Visibility     = Visibility.Collapsed;
            ExpUsuarios.Visibility       = Visibility.Visible;

            // Productos
            LblModuloProductos.Visibility = Visibility.Collapsed;
            ChevProductos.Visibility      = Visibility.Collapsed;
            ExpProductos.Visibility      = Visibility.Visible;

            // Pesajes
            LblModuloPesajes.Visibility  = Visibility.Collapsed;
            ChevPesajes.Visibility      = Visibility.Collapsed;
            ExpPesajes.Visibility       = Visibility.Visible;

            // Reportería
            LblModuloReportes.Visibility = Visibility.Collapsed;
            ExpReportes.Visibility       = Visibility.Visible;
            IcoReportes.Visibility       = Visibility.Collapsed;

            NavLabel.Visibility            = Visibility.Collapsed;
            HomeButtonContainer.Visibility = Visibility.Collapsed;
            UserCardButton.Visibility      = Visibility.Collapsed;
            CompactUserCard.Visibility     = Visibility.Visible;
            LogoContainer.Visibility       = Visibility.Collapsed;
        }

        private void ExpandSidebar()
        {
            _collapsed = false;

            AnimateWidth(Sidebar,    SidebarExpanded, 200);
            AnimateWidth(BrandBlock, SidebarExpanded, 200);

            // Restaurar etiquetas y chevrones
            LblModuloUsuarios.Visibility = Visibility.Visible;
            ChevUsuarios.Visibility     = Visibility.Visible;
            ExpUsuarios.Visibility      = Visibility.Visible;

            LblModuloProductos.Visibility = Visibility.Visible;
            ChevProductos.Visibility     = Visibility.Visible;
            ExpProductos.Visibility      = Visibility.Visible;

            LblModuloPesajes.Visibility  = Visibility.Visible;
            ChevPesajes.Visibility      = Visibility.Visible;
            ExpPesajes.Visibility       = Visibility.Visible;

            LblModuloReportes.Visibility = Visibility.Visible;
            ExpReportes.Visibility      = Visibility.Visible;
            IcoReportes.Visibility       = Visibility.Collapsed;

            foreach (var entry in _moduleMap.Values)
            {
                entry.CollapsedIcon.Visibility = Visibility.Collapsed;
            }

            NavLabel.Visibility            = Visibility.Visible;
            HomeButtonContainer.Visibility = Visibility.Visible;
            UserCardButton.Visibility      = Visibility.Visible;
            CompactUserCard.Visibility     = Visibility.Collapsed;
            LogoContainer.Visibility       = Visibility.Visible;

            if (!string.IsNullOrEmpty(_activeModuleId) &&
                _moduleMap.TryGetValue(_activeModuleId, out var active))
            {
                int count = GetSubCount(_activeModuleId);
                AnimateSubMenu(active.SubMenu, true, count);
                AnimateChevron(active.Chevron, 180);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  MÓDULOS — acordeón
        // ══════════════════════════════════════════════════════════════════
        private void BtnModulo_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            string id = (string)btn.Tag;

            if (_collapsed)
            {
                ExpandSidebar();
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
            entry.Indicator.Visibility = Visibility.Visible;
        }

        private void CloseAllModules()
        {
            foreach (var kv in _moduleMap)
            {
                AnimateSubMenu(kv.Value.SubMenu, false, 0);
                AnimateChevron(kv.Value.Chevron, 0);
                if (kv.Key != _activeModuleId)
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

            Navigate(id);
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

            Navigate(subId);
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
        //  Navegación
        // ══════════════════════════════════════════════════════════════════
        private void Navigate(string id)
        {
            switch (id)
            {
                case "usuarios-sub":          Vm.NavUsuariosCommand.Execute(null);               break;
                case "empleados":             Vm.NavEmpleadosCommand.Execute(null);              break;
                case "roles":                 Vm.NavRolesCommand.Execute(null);                  break;
                case "bitacora":              Vm.NavBitacoraCommand.Execute(null);               break;
                case "productos-sub":         Vm.NavProductosCommand.Execute(null);              break;
                case "proveedores":           Vm.NavProveedoresCommand.Execute(null);            break;
                case "fabricantes":           Vm.NavFabricantesCommand.Execute(null);            break;
                case "categorias":            Vm.NavCategoriasCommand.Execute(null);             break;
                case "contactos-proveedores": Vm.NavContactosProveedoresCommand.Execute(null);   break;
                case "contactos-fabricantes": Vm.NavContactosFabricantesCommand.Execute(null);   break;
                case "pesajes-sub":           Vm.NavPesajesCommand.Execute(null);               break;
                case "reportes":             Vm.NavReportesCommand.Execute(null);               break;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  HOME / MI USUARIO
        // ══════════════════════════════════════════════════════════════════
        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            ClearActiveStates();
            _activeModuleId = "";
            Vm.NavBienvenidaCommand.Execute(null);
        }

        private void UserCard_Click(object sender, MouseButtonEventArgs e)
        {
            ClearActiveStates();
            _activeModuleId = "mi-usuario";
            Vm.NavMiUsuarioCommand.Execute(null);
        }

        // ══════════════════════════════════════════════════════════════════
        //  NOTIFICACIONES
        // ══════════════════════════════════════════════════════════════════
        private void BtnNotif_Click(object sender, RoutedEventArgs e)
            => NotifPopup.IsOpen = !NotifPopup.IsOpen;

        // ══════════════════════════════════════════════════════════════════
        //  BÚSQUEDA
        // ══════════════════════════════════════════════════════════════════
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

        private void BtnCerrarVentana_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();

        // ══════════════════════════════════════════════════════════════════
        //  Animaciones
        // ══════════════════════════════════════════════════════════════════
        private static void AnimateWidth(FrameworkElement target, double to, int ms)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            target.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }

        private static void AnimateSubMenu(Border border, bool open, int itemCount)
        {
            double target = open ? itemCount * SubItemHeight + 8 : 0;
            var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            border.BeginAnimation(Border.MaxHeightProperty, anim);
        }

        private static void AnimateChevron(RotateTransform rt, double angle)
        {
            var anim = new DoubleAnimation(angle, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            rt.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════
        private static int GetSubCount(string moduleId) => moduleId switch
        {
            "usuarios"  => 4,
            "productos" => 6,
            "pesajes"   => 1,
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
