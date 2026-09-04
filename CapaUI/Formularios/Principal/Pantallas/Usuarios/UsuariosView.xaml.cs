using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Usuarios
{
    public partial class UsuariosView : System.Windows.Controls.UserControl
    {
        private UsuariosViewModel _vm = null!;
        private Storyboard? _spinnerStory;
        private bool _suppressFilterChange;

        public UsuariosView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<UsuariosViewModel>();
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;
            DgUsuarios.ItemsSource = _vm.PageRows;

            // Se captura la instancia ANTES del await. Si el usuario cierra la
            // pantalla mientras carga, Unloaded pone _vm = null y la continuación
            // del await volvería sobre una vista ya descargada.
            // Se compara por referencia y no contra null para cubrir también el
            // abrir-cerrar-abrir rápido: ahí _vm no es null, pero es OTRO VM.
            try
            {
                var vm = _vm;
                await vm.CargarDatosAsync();
                if (!ReferenceEquals(_vm, vm)) return;

                if (Window.GetWindow(this)?.DataContext is MainViewModel principal &&
                    principal.ConsumirRegistroNotificacionPendiente("usuarios") is int idUsuario)
                    await vm.NavegarARegistroAsync(idUsuario);

                PoblarRoles();
            }
            catch (OperationCanceledException)
            {
                // Navegación rápida: la vista se descargó mientras cargaba. Salida limpia sin error.
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Usuarios] Falló la carga inicial de la pantalla");
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SolicitarEditar  -= AbrirModalEditar;
            _vm.FiltrosLimpiados -= OnFiltrosLimpiados;
            _vm.PropertyChanged  -= OnVmPropertyChanged;
            _vm.Dispose();
            DataContext = null;
            _vm = null!;
            DetenerSpinner();
        }

        // ── VM property changes ────────────────────────────────────────

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            // Segunda línea de defensa: un PropertyChanged emitido justo durante
            // el Unloaded llegaría con _vm ya anulado.
            if (_vm == null) return;

            switch (ev.PropertyName)
            {
                case nameof(UsuariosViewModel.PageRows):      RefrescarPaginacion();   break;
                case nameof(UsuariosViewModel.IsLoading):     ActualizarCarga();       break;
                case nameof(UsuariosViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(UsuariosViewModel.ErrorCarga):
                    if (!string.IsNullOrEmpty(_vm.ErrorCarga))
                    {
                        ErrorText.Text = _vm.ErrorCarga;
                        ErrorPanel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ErrorPanel.Visibility = Visibility.Collapsed;
                    }
                    break;
                case nameof(UsuariosViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(UsuariosViewModel.Seleccionado):  SeleccionarEnTabla();    break;
                case nameof(UsuariosViewModel.Roles):         PoblarRoles();           break;
            }
        }

        // ── Loading state ──────────────────────────────────────────────

        private void ActualizarCarga()
        {
            if (_vm.IsLoading)
            {
                DgUsuarios.Visibility  = Visibility.Collapsed;
                EmptyState.Visibility  = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                DetenerSpinner();
                DgUsuarios.Visibility  = Visibility.Visible;
            }
        }

        // ── Spinner ────────────────────────────────────────────────────

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
            _spinnerStory.Remove();
            _spinnerStory.Children.Clear();
            _spinnerStory = null;
        }

        // ── Filters ────────────────────────────────────────────────────

        private void EstadoFiltro_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            if (RbActivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoUsuarioFilter.Activos;
            else if (RbInactivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoUsuarioFilter.Inactivos;
            else
                _vm.EstadoFiltro = EstadoUsuarioFilter.Todos;
        }

        private void CmbRol_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            if (CmbRol.SelectedItem is ComboBoxItem ci && ci.Tag is int rolId)
                _vm.RolFiltro = rolId;
            else
                _vm.RolFiltro = null;
        }

        private void PoblarRoles()
        {
            if (_vm == null) return;

            _suppressFilterChange = true;
            CmbRol.Items.Clear();
            CmbRol.Items.Add(new ComboBoxItem { Content = "(Todos)", Tag = (int?)null });
            foreach (var rol in _vm.Roles)
                CmbRol.Items.Add(new ComboBoxItem { Content = rol.NombreRol, Tag = (int?)rol.IdRol });
            CmbRol.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        private void OnFiltrosLimpiados()
        {
            _suppressFilterChange = true;
            RbActivos.IsChecked = true;
            CmbRol.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        // ── Search ─────────────────────────────────────────────────────

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((UsuarioVistaDto)e.Source);
            SeleccionarEnTabla();
        }

        // ── Table ──────────────────────────────────────────────────────

        private void DgUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgUsuarios.SelectedItem as UsuarioVistaDto;
        }

        private void DgUsuarios_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.EditarCommand.CanExecute(null) == true)
                _vm.EditarCommand.Execute(null);
        }

        private void SeleccionarEnTabla()
        {
            if (_vm.Seleccionado == null) return;
            if (DgUsuarios.SelectedItem == _vm.Seleccionado) return;
            DgUsuarios.SelectedItem = _vm.Seleccionado;
            DgUsuarios.ScrollIntoView(_vm.Seleccionado);
        }

        // ── Pagination ─────────────────────────────────────────────────

        private void RefrescarPaginacion()
        {
            if (_vm == null) return;
            DgUsuarios.ItemsSource = _vm.PageRows;

            PaginacionPanel.Items.Clear();
            int total   = _vm.TotalPages;
            int current = _vm.Page;

            foreach (var p in CalcularPaginas(current, total))
            {
                if (p == -1)
                {
                    PaginacionPanel.Items.Add(new TextBlock
                    {
                        Text = "\u2026",
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0),
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"))
                    });
                }
                else
                {
                    var btn = new Button
                    {
                        Content = p.ToString(),
                        Margin  = new Thickness(2, 0, 2, 0),
                        Style   = (Style)(p == current
                            ? FindResource("ActivePageBtn")
                            : FindResource("PageBtn")),
                        Tag = p
                    };
                    btn.Click += (s, ev) =>
                    {
                        if (_vm.IsLoading) return;
                        if (s is Button b && b.Tag is int pg) _vm.Page = pg;
                    };
                    PaginacionPanel.Items.Add(btn);
                }
            }
        }

        private static IEnumerable<int> CalcularPaginas(int current, int total)
        {
            if (total <= 7)
                return Enumerable.Range(1, total);

            var pages = new List<int> { 1 };
            if (current > 3) pages.Add(-1);
            for (int i = Math.Max(2, current - 1); i <= Math.Min(total - 1, current + 1); i++)
                pages.Add(i);
            if (current < total - 2) pages.Add(-1);
            pages.Add(total);
            return pages;
        }

        // ── Modal ──────────────────────────────────────────────────────

        private void AbrirModalEditar(UsuarioVistaDto u)
        {
            if (!SesionPermisos.TieneAlguno(Permiso.ModificarUsuario, Permiso.EliminarUsuario, Permiso.AsignarRolUsuario) ||
                _vm.EsUsuarioSesionActual) return;
            var rolRepo       = App.Services.GetRequiredService<IRolRepository>();
            var usuarioRepo   = App.Services.GetRequiredService<IUsuarioRepository>();
            var modal = new UsuarioModal(usuarioRepo, rolRepo, u);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnUsuarioGuardado;
            MostrarModal(modal);
        }

        private void MostrarModal(UserControl modal)
        {
            ModalContent.Content    = modal;
            ModalOverlay.Opacity    = 1;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void CerrarModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalContent.Content    = null;
        }

        private void OnUsuarioGuardado()
        {
            CerrarModal();
            _vm.RefrescarTrasGuardar();
        }
    }
}
