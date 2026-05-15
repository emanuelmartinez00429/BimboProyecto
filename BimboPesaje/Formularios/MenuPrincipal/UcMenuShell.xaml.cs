using CapaServicios;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace BimboPesaje.Formularios.MenuPrincipal
{
    public partial class UcMenuShell : System.Windows.Controls.UserControl
    {
        // ══════════════════════════════════════════════════════════════
        //  Eventos hacia el host WinForms
        // ══════════════════════════════════════════════════════════════
        public event Action<string>? NavigationRequested;
        public event Action?         LogoutRequested;
        public event Action?         DragMoveRequested;
        public event Action?         MinimizeRequested;
        public event Action?         MaximizeRequested;
        public event Action?         CloseRequested;

        // ══════════════════════════════════════════════════════════════
        //  Estado interno del sidebar
        // ══════════════════════════════════════════════════════════════
        private bool   _collapsed      = false;
        private string _activeSubId    = "";
        private string _activeModuleId = "";
        private bool   _showingMyUser  = false;

        private const double SidebarExpanded  = 226;
        private const double SidebarCollapsed = 72;
        private const int    SubItemHeight    = 48; // px por sub-item

        // Map: moduleId → (subMenuBorder, chevronRotate, indicadorBorder, labels[])
        private record ModuleEntry(
            Border SubMenu,
            RotateTransform Chevron,
            Border Indicator,
            System.Windows.Controls.TextBlock[] Labels);

        private Dictionary<string, ModuleEntry> _moduleMap = new();

        // Map: subId → (dotBorder, label, parentModuleId)
        private record SubEntry(Border Dot, System.Windows.Controls.TextBlock Label, string ParentModule);
        private Dictionary<string, SubEntry> _subMap = new();

        // ══════════════════════════════════════════════════════════════
        //  Constructor
        // ══════════════════════════════════════════════════════════════
        public UcMenuShell()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Registrar módulos
            _moduleMap["usuarios"]   = new(SubUsuarios,  ChevronUsuariosRot,  IndUsuarios,  new[] { LblUsuarios  });
            _moduleMap["productos"]  = new(SubProductos, ChevronProductosRot, IndProductos, new[] { LblProductos });
            _moduleMap["pesajes"]    = new(SubPesajes,   ChevronPesajesRot,   IndPesajes,   new[] { LblPesajes   });

            // Registrar sub-items
            _subMap["empleados"]       = new(DotEmpleados,       LblEmpleados,       "usuarios");
            _subMap["usuarios-sub"]    = new(DotUsuariosSub,     LblUsuariosSub,     "usuarios");
            _subMap["roles"]           = new(DotRoles,           LblRoles,           "usuarios");
            _subMap["bitacora"]        = new(DotBitacora,        LblBitacora,        "usuarios");
            _subMap["prod-productos"]  = new(DotProdProductos,   LblProdProductos,   "productos");
            _subMap["prod-proveedores"]= new(DotProdProveedores, LblProdProveedores, "productos");
            _subMap["prod-fabricantes"]= new(DotProdFabricantes, LblProdFabricantes, "productos");
            _subMap["prod-categorias"] = new(DotProdCategorias,  LblProdCategorias,  "productos");
            _subMap["pes-movs"]        = new(DotPesMov,          LblPesMov,          "pesajes");

            CargarPerfil();
        }

        // ══════════════════════════════════════════════════════════════
        //  Perfil de usuario
        // ══════════════════════════════════════════════════════════════
        private void CargarPerfil()
        {
            var perfil = ServicioPerfilUsuario.PerfilActual;
            if (perfil == null) return;

            AvatarInitials.Text  = perfil.Iniciales;
            CompactInitials.Text = perfil.Iniciales;
            UserCardName.Text    = perfil.NombreCompleto;
            UserCardRole.Text    = perfil.NombreRol;
        }

        // ══════════════════════════════════════════════════════════════
        //  TOP BAR: drag + chrome
        // ══════════════════════════════════════════════════════════════
        private void TopBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMoveRequested?.Invoke();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)  => MinimizeRequested?.Invoke();
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)  => MaximizeRequested?.Invoke();
        private void BtnClose_Click(object sender, RoutedEventArgs e)     => CloseRequested?.Invoke();
        private void BtnLogout_Click(object sender, RoutedEventArgs e)    => LogoutRequested?.Invoke();

        // ══════════════════════════════════════════════════════════════
        //  HAMBURGER: animar sidebar
        // ══════════════════════════════════════════════════════════════
        private void BtnHamburger_Click(object sender, RoutedEventArgs e)
        {
            if (_collapsed) ExpandSidebar();
            else            CollapseSidebar();
        }

        private void CollapseSidebar()
        {
            _collapsed = true;

            // Cerrar todos los submenús abiertos
            foreach (var entry in _moduleMap.Values)
                AnimateSubMenu(entry.SubMenu, false, 0);

            // Animar ancho sidebar
            AnimateWidth(Sidebar, SidebarCollapsed, 200);
            // Sincronizar columna del topbar
            AnimateColumnWidth(TopLeftCol, SidebarCollapsed, 200);

            // Ocultar etiquetas de módulos y nav label
            NavLabel.Visibility         = Visibility.Collapsed;
            HomeButtonContainer.Visibility = Visibility.Collapsed;
            UserCardButton.Visibility   = Visibility.Collapsed;
            CompactUserCard.Visibility  = Visibility.Visible;

            foreach (var entry in _moduleMap.Values)
            {
                foreach (var lbl in entry.Labels) lbl.Visibility = Visibility.Collapsed;
                entry.Chevron.Angle = 0;
            }
            LblReporteria.Visibility = Visibility.Collapsed;
            LogoContainer.Visibility = Visibility.Collapsed;
        }

        private void ExpandSidebar()
        {
            _collapsed = false;

            AnimateWidth(Sidebar, SidebarExpanded, 200);
            AnimateColumnWidth(TopLeftCol, SidebarExpanded, 200);

            NavLabel.Visibility         = Visibility.Visible;
            HomeButtonContainer.Visibility = Visibility.Visible;
            UserCardButton.Visibility   = Visibility.Visible;
            CompactUserCard.Visibility  = Visibility.Collapsed;

            foreach (var entry in _moduleMap.Values)
                foreach (var lbl in entry.Labels) lbl.Visibility = Visibility.Visible;

            LblReporteria.Visibility = Visibility.Visible;
            LogoContainer.Visibility = Visibility.Visible;

            // Re-abrir módulo activo si había uno
            if (!string.IsNullOrEmpty(_activeModuleId) && _moduleMap.TryGetValue(_activeModuleId, out var active))
            {
                int count = GetSubCount(_activeModuleId);
                AnimateSubMenu(active.SubMenu, true, count);
                AnimateChevron(active.Chevron, 180);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  MÓDULOS: accordion
        // ══════════════════════════════════════════════════════════════
        private void BtnModulo_Click(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            string id = (string)btn.Tag;

            if (_collapsed)
            {
                ExpandSidebar();
                OpenModule(id);
                return;
            }

            // Si está abierto, lo cierra; si no, cierra los demás y abre éste
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

        private void BtnModuloDirect_Click(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            string id = (string)btn.Tag;

            ClearActiveStates();
            _activeModuleId = id;
            _activeSubId    = id;
            IndReporteria.Visibility = Visibility.Visible;

            ShowWinFormsContent();
            NavigationRequested?.Invoke(id);
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            ClearActiveStates();
            ShowWinFormsContent();
            NavigationRequested?.Invoke("home");
        }

        // ══════════════════════════════════════════════════════════════
        //  SUBMÓDULOS
        // ══════════════════════════════════════════════════════════════
        private void BtnSub_Click(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            string subId = (string)btn.Tag;

            // Limpiar estado anterior de sub-items
            foreach (var kv in _subMap)
            {
                kv.Value.Dot.Visibility = Visibility.Collapsed;
                kv.Value.Label.FontWeight = FontWeights.Normal;
                kv.Value.Label.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D9FFFFFF"));
            }
            // Limpiar indicadores de módulos
            foreach (var kv in _moduleMap)
                kv.Value.Indicator.Visibility = Visibility.Collapsed;

            _activeSubId = subId;
            _showingMyUser = false;

            if (_subMap.TryGetValue(subId, out var sub))
            {
                sub.Dot.Visibility = Visibility.Visible;
                sub.Label.FontWeight = FontWeights.SemiBold;
                sub.Label.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6EE7B7"));
                _activeModuleId = sub.ParentModule;
                if (_moduleMap.TryGetValue(sub.ParentModule, out var parentEntry))
                    parentEntry.Indicator.Visibility = Visibility.Visible;
            }

            ShowWinFormsContent();
            NavigationRequested?.Invoke(subId);
        }

        // ══════════════════════════════════════════════════════════════
        //  MI USUARIO
        // ══════════════════════════════════════════════════════════════
        private void BtnMiUsuario_Click(object sender, RoutedEventArgs e)
        {
            ClearActiveStates();
            _showingMyUser = true;

            var miUsuario = new UcMiUsuario();
            WpfContent.Content  = miUsuario;
            WpfContent.Visibility = Visibility.Visible;
            WfHost.Visibility   = Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════════════════════
        //  Panel WinForms (center content)
        // ══════════════════════════════════════════════════════════════
        public void SetContentPanel(WinForms.Panel panel)
        {
            WfHost.Child = panel;
        }

        private void ShowWinFormsContent()
        {
            _showingMyUser = false;
            WfHost.Visibility   = Visibility.Visible;
            WpfContent.Visibility = Visibility.Collapsed;
            WpfContent.Content  = null;
        }

        private void ClearActiveStates()
        {
            foreach (var kv in _subMap)
            {
                kv.Value.Dot.Visibility = Visibility.Collapsed;
                kv.Value.Label.FontWeight = FontWeights.Normal;
                kv.Value.Label.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D9FFFFFF"));
            }
            foreach (var kv in _moduleMap)
                kv.Value.Indicator.Visibility = Visibility.Collapsed;
            IndReporteria.Visibility = Visibility.Collapsed;
            _activeSubId    = "";
            _showingMyUser  = false;
        }

        // ══════════════════════════════════════════════════════════════
        //  Animaciones
        // ══════════════════════════════════════════════════════════════
        private static void AnimateWidth(System.Windows.FrameworkElement target, double to, int ms)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            target.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, anim);
        }

        private static void AnimateColumnWidth(ColumnDefinition col, double to, int ms)
        {
            var anim = new GridLengthAnimation
            {
                To       = new GridLength(to),
                Duration = TimeSpan.FromMilliseconds(ms),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            col.BeginAnimation(ColumnDefinition.WidthProperty, anim);
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

        // ══════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════
        private int GetSubCount(string moduleId) => moduleId switch
        {
            "usuarios"  => 4,
            "productos" => 4,
            "pesajes"   => 1,
            _           => 0
        };

        private double GetCurrentSubMenuHeight(string moduleId)
        {
            if (_moduleMap.TryGetValue(moduleId, out var entry))
                return entry.SubMenu.MaxHeight;
            return 0;
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
            SearchBorder.CornerRadius = new CornerRadius(12, 12, 0, 0);
            if (TxtSearch.Foreground is System.Windows.Media.SolidColorBrush)
                TxtSearch.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A1F2E"));
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            SearchBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
            SearchBorder.CornerRadius = new CornerRadius(22);
            TxtSearch.Foreground = System.Windows.Media.Brushes.White;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  GridLengthAnimation helper (para animar ColumnDefinition.Width)
    // ══════════════════════════════════════════════════════════════
    public class GridLengthAnimation : AnimationTimeline
    {
        public GridLength To { get; set; }

        public IEasingFunction? EasingFunction { get; set; }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override System.Windows.Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double progress = animationClock.CurrentProgress ?? 0;
            if (EasingFunction != null)
                progress = EasingFunction.Ease(progress);

            double from = ((GridLength)defaultOriginValue).Value;
            double to   = To.Value;
            return new GridLength(from + (to - from) * progress);
        }
    }
}
