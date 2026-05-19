using CapaServicios;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinForms = System.Windows.Forms;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfFontFamily = System.Windows.Media.FontFamily;

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
        private const int    SubItemHeight    = 48;

        // Map: moduleId → (subMenuBorder, chevronRotate, indicadorBorder, expandedView, collapsedIcon)
        private record ModuleEntry(
            Border          SubMenu,
            RotateTransform Chevron,
            Border          Indicator,
            FrameworkElement ExpandedView,
            UIElement        CollapsedIcon);

        private Dictionary<string, ModuleEntry> _moduleMap = new();

        // Map: subId → (dotBorder, label, parentModuleId)
        private record SubEntry(Border Dot, System.Windows.Controls.TextBlock Label, string ParentModule);
        private Dictionary<string, SubEntry> _subMap = new();

        // ── Búsqueda ──────────────────────────────────────────────────
        private readonly ObservableCollection<EntradaBusqueda> _searchResults = new();

        // ══════════════════════════════════════════════════════════════
        //  Constructor
        // ══════════════════════════════════════════════════════════════
        public UcMenuShell()
        {
            InitializeComponent();
            SearchResultsList.ItemsSource = _searchResults;
            Loaded    += OnLoaded;
            Unloaded  += OnUnloaded;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Registrar módulos (expandedView = DockPanel, collapsedIcon = TextBlock centrado)
            _moduleMap["usuarios"]  = new(SubUsuarios,  ChevUsuariosRot,  IndUsuarios,  ExpUsuarios,  IcoUsuarios);
            _moduleMap["productos"] = new(SubProductos, ChevProductosRot, IndProductos, ExpProductos, IcoProductos);
            _moduleMap["pesajes"]   = new(SubPesajes,   ChevPesajesRot,   IndPesajes,   ExpPesajes,   IcoPesajes);

            // Registrar sub-items
            _subMap["empleados"]        = new(DotEmpleados,       LblEmpleados,       "usuarios");
            _subMap["usuarios-sub"]     = new(DotUsuariosSub,     LblUsuariosSub,     "usuarios");
            _subMap["roles"]            = new(DotRoles,           LblRoles,           "usuarios");
            _subMap["bitacora"]         = new(DotBitacora,        LblBitacora,        "usuarios");
            _subMap["prod-productos"]   = new(DotProdProductos,   LblProdProductos,   "productos");
            _subMap["prod-proveedores"] = new(DotProdProveedores, LblProdProveedores, "productos");
            _subMap["prod-fabricantes"] = new(DotProdFabricantes, LblProdFabricantes, "productos");
            _subMap["prod-categorias"]  = new(DotProdCategorias,  LblProdCategorias,  "productos");
            _subMap["pes-movs"]         = new(DotPesMov,          LblPesMov,          "pesajes");

            CargarPerfil();
            SuscribirNotificaciones();
            ActualizarBadge();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ServicioNotificaciones.Actualizado -= OnNotificacionesActualizadas;
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
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMoveRequested?.Invoke();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => MinimizeRequested?.Invoke();
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => MaximizeRequested?.Invoke();
        private void BtnClose_Click(object sender, RoutedEventArgs e)    => CloseRequested?.Invoke();
        private void BtnLogout_Click(object sender, RoutedEventArgs e)   => LogoutRequested?.Invoke();

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

            // Cerrar todos los submenús
            foreach (var entry in _moduleMap.Values)
                AnimateSubMenu(entry.SubMenu, false, 0);

            // Animar ancho sidebar (la columna del topbar sigue automáticamente con Width="Auto")
            AnimateWidth(Sidebar, SidebarCollapsed, 200);

            // Ocultar vistas expandidas, mostrar íconos centrados
            foreach (var entry in _moduleMap.Values)
            {
                entry.ExpandedView.Visibility  = Visibility.Collapsed;
                entry.CollapsedIcon.Visibility = Visibility.Visible;
                entry.Chevron.Angle = 0;
            }
            // Reportería (módulo directo, sin submenú)
            ExpReporteria.Visibility = Visibility.Collapsed;
            IcoReporteria.Visibility = Visibility.Visible;

            NavLabel.Visibility             = Visibility.Collapsed;
            HomeButtonContainer.Visibility  = Visibility.Collapsed;
            UserCardButton.Visibility       = Visibility.Collapsed;
            CompactUserCard.Visibility      = Visibility.Visible;
            LogoContainer.Visibility        = Visibility.Collapsed;
        }

        private void ExpandSidebar()
        {
            _collapsed = false;

            AnimateWidth(Sidebar, SidebarExpanded, 200);

            // Mostrar vistas expandidas, ocultar íconos centrados
            foreach (var entry in _moduleMap.Values)
            {
                entry.ExpandedView.Visibility  = Visibility.Visible;
                entry.CollapsedIcon.Visibility = Visibility.Collapsed;
            }
            ExpReporteria.Visibility = Visibility.Visible;
            IcoReporteria.Visibility = Visibility.Collapsed;

            NavLabel.Visibility             = Visibility.Visible;
            HomeButtonContainer.Visibility  = Visibility.Visible;
            UserCardButton.Visibility       = Visibility.Visible;
            CompactUserCard.Visibility      = Visibility.Collapsed;
            LogoContainer.Visibility        = Visibility.Visible;

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
            _activeModuleId          = id;
            _activeSubId             = id;
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

            foreach (var kv in _subMap)
            {
                kv.Value.Dot.Visibility   = Visibility.Collapsed;
                kv.Value.Label.FontWeight = FontWeights.Normal;
                kv.Value.Label.Foreground = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString("#D9FFFFFF"));
            }
            foreach (var kv in _moduleMap)
                kv.Value.Indicator.Visibility = Visibility.Collapsed;

            _activeSubId   = subId;
            _showingMyUser = false;

            if (_subMap.TryGetValue(subId, out var sub))
            {
                sub.Dot.Visibility   = Visibility.Visible;
                sub.Label.FontWeight = FontWeights.SemiBold;
                sub.Label.Foreground = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString("#6EE7B7"));
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

            WpfContent.Content    = new UcMiUsuario();
            WpfContent.Visibility = Visibility.Visible;
            WfHost.Visibility     = Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════════════════════
        //  Panel WinForms (centro)
        // ══════════════════════════════════════════════════════════════
        public void SetContentPanel(WinForms.Panel panel)
        {
            WfHost.Child = panel;
        }

        public void ShowWpfView(System.Windows.Controls.UserControl view)
        {
            _showingMyUser        = false;
            WpfContent.Content    = view;
            WpfContent.Visibility = Visibility.Visible;
            WfHost.Visibility     = Visibility.Collapsed;
        }

        public void ClearWpfView()
        {
            WpfContent.Content    = null;
            WpfContent.Visibility = Visibility.Collapsed;
            WfHost.Visibility     = Visibility.Visible;
        }

        private void ShowWinFormsContent()
        {
            _showingMyUser        = false;
            WfHost.Visibility     = Visibility.Visible;
            WpfContent.Visibility = Visibility.Collapsed;
            WpfContent.Content    = null;
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

            IndReporteria.Visibility = Visibility.Collapsed;
            _activeSubId             = "";
            _showingMyUser           = false;
        }

        // ══════════════════════════════════════════════════════════════
        //  BÚSQUEDA
        // ══════════════════════════════════════════════════════════════
        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchBorder.Background   = new SolidColorBrush(WpfColor.FromRgb(255, 255, 255));
            SearchBorder.CornerRadius = new CornerRadius(12, 12, 0, 0);
            TxtSearch.Foreground      = new SolidColorBrush(
                (WpfColor)WpfColorConverter.ConvertFromString("#1A1F2E"));
            TxtSearch.CaretBrush      = new SolidColorBrush(
                (WpfColor)WpfColorConverter.ConvertFromString("#1A1F2E"));

            RefrescarResultados(TxtSearch.Text);
            PopupBusqueda.IsOpen = true;
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            // Pequeño delay para permitir clic en los resultados antes de cerrar
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                if (!PopupBusqueda.IsKeyboardFocusWithin)
                {
                    CerrarBuscador();
                }
            });
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefrescarResultados(TxtSearch.Text);
            PopupBusqueda.IsOpen = true;
        }

        private void TxtSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CerrarBuscador();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _searchResults.Count > 0)
            {
                NavegaA(_searchResults[0].Id);
                e.Handled = true;
            }
        }

        // Ctrl+K global en el UserControl
        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.K &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                TxtSearch.Focus();
                TxtSearch.SelectAll();
                e.Handled = true;
            }
        }

        private void PopupBusqueda_Opened(object sender, EventArgs e)
        {
            RefrescarResultados(TxtSearch.Text);
        }

        private void SearchResult_Click(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            if (btn.Tag is string id)
                NavegaA(id);
        }

        private void RefrescarResultados(string texto)
        {
            _searchResults.Clear();
            foreach (var entrada in ServicioBuscador.Buscar(texto))
                _searchResults.Add(entrada);

            PopupBusqueda.IsOpen = _searchResults.Count > 0;
        }

        private void NavegaA(string id)
        {
            CerrarBuscador();

            // Abrir módulo padre si es un sub-item
            if (_subMap.TryGetValue(id, out var sub))
            {
                CloseAllModules();
                OpenModule(sub.ParentModule);
                BtnSub_ActivarSinNavegar(id);
                NavigationRequested?.Invoke(id);
            }
            else if (id == "reporteria")
            {
                ClearActiveStates();
                _activeModuleId          = id;
                _activeSubId             = id;
                IndReporteria.Visibility = Visibility.Visible;
                ShowWinFormsContent();
                NavigationRequested?.Invoke(id);
            }
        }

        private void BtnSub_ActivarSinNavegar(string subId)
        {
            foreach (var kv in _subMap)
            {
                kv.Value.Dot.Visibility   = Visibility.Collapsed;
                kv.Value.Label.FontWeight = FontWeights.Normal;
                kv.Value.Label.Foreground = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString("#D9FFFFFF"));
            }
            if (_subMap.TryGetValue(subId, out var sub))
            {
                sub.Dot.Visibility   = Visibility.Visible;
                sub.Label.FontWeight = FontWeights.SemiBold;
                sub.Label.Foreground = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString("#6EE7B7"));
                _activeSubId    = subId;
                _activeModuleId = sub.ParentModule;
                if (_moduleMap.TryGetValue(sub.ParentModule, out var parentEntry))
                    parentEntry.Indicator.Visibility = Visibility.Visible;
            }
            ShowWinFormsContent();
        }

        private void CerrarBuscador()
        {
            PopupBusqueda.IsOpen      = false;
            SearchBorder.Background   = new SolidColorBrush(WpfColor.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
            SearchBorder.CornerRadius = new CornerRadius(22);
            TxtSearch.Foreground      = WpfBrushes.White;
            TxtSearch.CaretBrush      = WpfBrushes.White;
            TxtSearch.Text            = "";
            Keyboard.ClearFocus();
        }

        // ══════════════════════════════════════════════════════════════
        //  NOTIFICACIONES
        // ══════════════════════════════════════════════════════════════
        private void SuscribirNotificaciones()
        {
            ServicioNotificaciones.Actualizado += OnNotificacionesActualizadas;
        }

        private void OnNotificacionesActualizadas()
        {
            // Se puede llamar desde otro hilo (Realtime / Task)
            Dispatcher.InvokeAsync(ActualizarBadge);
        }

        private void ActualizarBadge()
        {
            int sinLeer = ServicioNotificaciones.SinLeer;

            BadgeCount.Text         = sinLeer > 99 ? "99+" : sinLeer.ToString();
            BadgeNotif.Visibility   = sinLeer > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Si el popup está abierto, refresca su contenido también
            if (PopupNotificaciones.IsOpen)
                RefrescarPanelNotificaciones();
        }

        private void BtnBell_Click(object sender, RoutedEventArgs e)
        {
            if (PopupNotificaciones.IsOpen)
            {
                PopupNotificaciones.IsOpen = false;
                return;
            }

            RefrescarPanelNotificaciones();
            PopupNotificaciones.IsOpen = true;
        }

        private void BtnMarcarTodas_Click(object sender, RoutedEventArgs e)
        {
            ServicioNotificaciones.MarcarTodasLeidas();
            RefrescarPanelNotificaciones();
            ActualizarBadge();
        }

        private void RefrescarPanelNotificaciones()
        {
            NotifPanel.Children.Clear();

            var recientes = ServicioNotificaciones.Recientes.ToList();

            if (recientes.Count == 0)
            {
                NotifVacia.Visibility = Visibility.Visible;

                // Actualizar badge del encabezado del popup
                NotifBadgeHeader.Visibility = Visibility.Collapsed;
                return;
            }

            NotifVacia.Visibility = Visibility.Collapsed;

            int sinLeer = recientes.Count(n => !n.Leida);
            if (sinLeer > 0)
            {
                NotifBadgeHeader.Visibility         = Visibility.Visible;
                NotifBadgeHeaderCount.Text          = $"{sinLeer} nueva{(sinLeer > 1 ? "s" : "")}";
            }
            else
            {
                NotifBadgeHeader.Visibility = Visibility.Collapsed;
            }

            foreach (var notif in recientes)
                NotifPanel.Children.Add(CrearItemNotif(notif));
        }

        private UIElement CrearItemNotif(Notificacion notif)
        {
            var colorPunto = (WpfColor)WpfColorConverter.ConvertFromString(notif.ColorPunto);

            var btn = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("NotifItemButton"),
                Tag   = notif.Id,
            };
            btn.Click += (_, __) =>
            {
                ServicioNotificaciones.MarcarLeida(notif.Id);
                RefrescarPanelNotificaciones();
                ActualizarBadge();
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Punto de color
            var punto = new Border
            {
                Width             = 8,
                Height            = 8,
                CornerRadius      = new CornerRadius(4),
                Background        = new SolidColorBrush(colorPunto),
                VerticalAlignment = VerticalAlignment.Top,
                Margin            = new Thickness(0, 4, 12, 0),
            };
            Grid.SetColumn(punto, 0);

            // Texto
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text       = notif.Titulo,
                FontFamily = new WpfFontFamily("Segoe UI"),
                FontSize   = 13,
                FontWeight = notif.Leida ? FontWeights.Normal : FontWeights.SemiBold,
                Foreground = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString(notif.Leida ? "#6B7280" : "#1A1F2E")),
                TextWrapping = TextWrapping.Wrap,
            });
            if (!string.IsNullOrEmpty(notif.Descripcion))
                sp.Children.Add(new TextBlock
                {
                    Text       = notif.Descripcion,
                    FontFamily = new WpfFontFamily("Segoe UI"),
                    FontSize   = 12,
                    Foreground = new SolidColorBrush(
                        (WpfColor)WpfColorConverter.ConvertFromString("#6B7280")),
                    TextWrapping = TextWrapping.Wrap,
                    Margin     = new Thickness(0, 2, 0, 0),
                });
            Grid.SetColumn(sp, 1);

            // Tiempo relativo
            var tiempo = new TextBlock
            {
                Text           = notif.TiempoRelativo,
                FontFamily     = new WpfFontFamily("Segoe UI"),
                FontSize       = 11,
                Foreground     = new SolidColorBrush(
                    (WpfColor)WpfColorConverter.ConvertFromString("#9CA3AF")),
                VerticalAlignment = VerticalAlignment.Top,
                Margin         = new Thickness(8, 2, 0, 0),
            };
            Grid.SetColumn(tiempo, 2);

            grid.Children.Add(punto);
            grid.Children.Add(sp);
            grid.Children.Add(tiempo);

            btn.Content = grid;
            return btn;
        }

        // ══════════════════════════════════════════════════════════════
        //  Animaciones
        // ══════════════════════════════════════════════════════════════
        private static void AnimateWidth(FrameworkElement target, double to, int ms)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
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
    }
}
