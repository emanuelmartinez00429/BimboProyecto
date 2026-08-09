using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using CapaUI.Formularios.Principal.Pantallas.Usuarios;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Empleados
{
    public partial class EmpleadosView : System.Windows.Controls.UserControl
    {
        private EmpleadosViewModel _vm = null!;
        private Storyboard? _spinnerStory;
        private bool _suppressFilterChange;

        public EmpleadosView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<EmpleadosViewModel>();
            _vm.SolicitarNuevo       += AbrirModalNuevo;
            _vm.SolicitarEditar      += AbrirModalEditar;
            _vm.SolicitarCrearUsuario += AbrirModalCrearUsuario;
            _vm.FiltrosLimpiados     += OnFiltrosLimpiados;
            _vm.PropertyChanged      += OnVmPropertyChanged;

            DataContext = _vm;
            DgEmpleados.ItemsSource = _vm.PageRows;

            await _vm.CargarDatosAsync();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SolicitarNuevo       -= AbrirModalNuevo;
            _vm.SolicitarEditar      -= AbrirModalEditar;
            _vm.SolicitarCrearUsuario -= AbrirModalCrearUsuario;
            _vm.FiltrosLimpiados     -= OnFiltrosLimpiados;
            _vm.PropertyChanged      -= OnVmPropertyChanged;
            _vm.Dispose();
            DataContext = null;
            _vm = null!;
            DetenerSpinner();
        }

        // ── VM property changes ────────────────────────────────────────

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(EmpleadosViewModel.PageRows):        RefrescarPaginacion();   break;
                case nameof(EmpleadosViewModel.IsLoading):       ActualizarCarga();       break;
                case nameof(EmpleadosViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(EmpleadosViewModel.ErrorCarga):
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
                case nameof(EmpleadosViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(EmpleadosViewModel.Seleccionado):      SeleccionarEnTabla();    break;
            }
        }

        // ── Loading state ──────────────────────────────────────────────

        private void ActualizarCarga()
        {
            if (_vm.IsLoading)
            {
                DgEmpleados.Visibility  = Visibility.Collapsed;
                EmptyState.Visibility   = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                DetenerSpinner();
                DgEmpleados.Visibility  = Visibility.Visible;
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
                _vm.EstadoFiltro = EstadoEmpleadoFilter.Activos;
            else if (RbInactivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoEmpleadoFilter.Inactivos;
            else
                _vm.EstadoFiltro = EstadoEmpleadoFilter.Todos;
        }

        private void OnFiltrosLimpiados()
        {
            _suppressFilterChange = true;
            RbActivos.IsChecked = true;
            _suppressFilterChange = false;
        }

        // ── Search ─────────────────────────────────────────────────────

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((EmpleadoDto)e.Source);
            SeleccionarEnTabla();
        }

        // ── Table ──────────────────────────────────────────────────────

        private void DgEmpleados_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgEmpleados.SelectedItem as EmpleadoDto;
        }

        private void DgEmpleados_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.Seleccionado != null)
                AbrirModalEditar(_vm.Seleccionado);
        }

        private void SeleccionarEnTabla()
        {
            if (_vm.Seleccionado == null) return;
            if (DgEmpleados.SelectedItem == _vm.Seleccionado) return;
            DgEmpleados.SelectedItem = _vm.Seleccionado;
            DgEmpleados.ScrollIntoView(_vm.Seleccionado);
        }

        // ── Pagination ─────────────────────────────────────────────────

        private void RefrescarPaginacion()
        {
            if (_vm == null) return;
            DgEmpleados.ItemsSource = _vm.PageRows;

            PaginacionPanel.Items.Clear();
            int total   = _vm.TotalPages;
            int current = _vm.Page;

            foreach (var p in CalcularPaginas(current, total))
            {
                if (p == -1)
                {
                    PaginacionPanel.Items.Add(new TextBlock
                    {
                        Text = "…",
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

        private void AbrirModalNuevo()
        {
            if (!SesionPermisos.Tiene(Permiso.CrearEmpleado)) return;
            var repo  = App.Services.GetRequiredService<IEmpleadoRepository>();
            var modal = new EmpleadoModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnEmpleadoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(EmpleadoDto empleado)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarEmpleado)) return;
            var repo  = App.Services.GetRequiredService<IEmpleadoRepository>();
            var modal = new EmpleadoModal(repo, empleado);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnEmpleadoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalCrearUsuario(EmpleadoDto emp)
        {
            if (!SesionPermisos.Tiene(Permiso.CrearUsuario)) return;
            var rolRepo     = App.Services.GetRequiredService<IRolRepository>();
            var usuarioRepo = App.Services.GetRequiredService<IUsuarioRepository>();
            var modal = new UsuarioModal(usuarioRepo, rolRepo,
                emp.IdEmpleado, emp.NombreEmpleado, emp.CorreoEmpleado);
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
            _vm.RefrescarDatos();
        }

        private void OnEmpleadoGuardado()
        {
            CerrarModal();
            _vm.RefrescarDatos();
        }
    }
}
