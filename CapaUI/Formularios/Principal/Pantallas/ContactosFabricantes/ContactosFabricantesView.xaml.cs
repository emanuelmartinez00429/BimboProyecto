using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Contactos.Fabricantes.Dtos;
using CapaAplicacion.Contactos.Fabricantes.Interfaces;
using CapaAplicacion.Fabricantes.Dtos;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using Microsoft.Extensions.DependencyInjection;
using static CapaAplicacion.Common.EstadoRegistro;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;

namespace CapaUI.Formularios.Principal.Pantallas.ContactosFabricantes
{
    public partial class ContactosFabricantesView : System.Windows.Controls.UserControl
    {
        private ContactosFabricantesViewModel _vm = null!;
        private Storyboard? _spinnerStory;

        public ContactosFabricantesView() => InitializeComponent();

        // ── Ciclo de vida ───────────────────────────────────

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<ContactosFabricantesViewModel>();
            _vm.SolicitarNuevoContacto  += AbrirModalNuevo;
            _vm.SolicitarEditarContacto += AbrirModalEditar;
            _vm.PropertyChanged         += OnVmPropertyChanged;

            DataContext                 = _vm;
            DgFabricantes.ItemsSource   = _vm.PageRows;

            try
            {
                var vm = _vm;
                await vm.CargarDatosAsync();
            }
            catch (OperationCanceledException)
            {
                // Navegación rápida: la vista se descargó mientras cargaba.
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[ContactosFabricantes] Falló la carga inicial de la pantalla");
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SolicitarNuevoContacto  -= AbrirModalNuevo;
            _vm.SolicitarEditarContacto -= AbrirModalEditar;
            _vm.PropertyChanged         -= OnVmPropertyChanged;
            _vm.Dispose();
            DataContext = null;
            _vm = null!;
            DetenerSpinner();
        }

        // ── PropertyChanged handler ─────────────────────────

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(ContactosFabricantesViewModel.PageRows):
                    DgFabricantes.ItemsSource = _vm.PageRows;
                    RefrescarPaginacion();
                    break;
                // Realtime puede crecer TotalPages sin tocar PageRows (INSERT con el
                // usuario en la vieja última página) — sin este case los botones
                // numerados quedan viejos hasta recargar el módulo.
                case nameof(ContactosFabricantesViewModel.TotalPages):
                    RefrescarPaginacion();
                    break;
                case nameof(ContactosFabricantesViewModel.IsLoading):
                    ActualizarCargaFabricantes();
                    break;
                case nameof(ContactosFabricantesViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ContactosFabricantesViewModel.IsViewingContacts):
                    AlternarPaneles();
                    break;
                case nameof(ContactosFabricantesViewModel.IsLoadingContactos):
                    ActualizarCargaContactos();
                    break;
                case nameof(ContactosFabricantesViewModel.Contactos):
                    DgContactos.ItemsSource   = _vm.Contactos;
                    EmptyContactos.Visibility = _vm.Contactos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    DgContactos.Visibility    = _vm.Contactos.Count >  0 ? Visibility.Visible : Visibility.Collapsed;
                    TxtContactosCount.Text    = _vm.Contactos.Count.ToString();
                    break;
                case nameof(ContactosFabricantesViewModel.Titulo):
                    TxtContactosTitulo.Text = $"Contactos de {_vm.Titulo}";
                    TxtCrumbNombre.Text     = _vm.Titulo;
                    break;
            }
        }

        // ── Alternancia de paneles ──────────────────────────

        private void AlternarPaneles()
        {
            bool viewing = _vm.IsViewingContacts;

            PanelFabricantes.Visibility = viewing ? Visibility.Collapsed : Visibility.Visible;
            PaginacionBar.Visibility    = viewing ? Visibility.Collapsed : Visibility.Visible;
            ToolbarPanel.Visibility     = viewing ? Visibility.Collapsed : Visibility.Visible;
            PanelContactos.Visibility   = viewing ? Visibility.Visible   : Visibility.Collapsed;

            HeaderListMode.Visibility     = viewing ? Visibility.Collapsed : Visibility.Visible;
            HeaderContactsMode.Visibility = viewing ? Visibility.Visible   : Visibility.Collapsed;
            ContactsToolbar.Visibility    = viewing ? Visibility.Visible   : Visibility.Collapsed;
        }

        // ── Spinner / Loading ───────────────────────────────

        private void ActualizarCargaFabricantes()
        {
            if (_vm.IsLoading)
            {
                DgFabricantes.Visibility = Visibility.Collapsed;
                EmptyState.Visibility    = Visibility.Collapsed;
                LoadingPanel.Visibility  = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility  = Visibility.Collapsed;
                DetenerSpinner();
                DgFabricantes.Visibility = Visibility.Visible;
            }
        }

        private void ActualizarCargaContactos()
        {
            if (_vm.IsLoadingContactos)
            {
                LoadingContactos.Visibility = Visibility.Visible;
                DgContactos.Visibility      = Visibility.Collapsed;
                EmptyContactos.Visibility   = Visibility.Collapsed;
            }
            else
            {
                LoadingContactos.Visibility = Visibility.Collapsed;
            }
        }

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

        // ── Buscador (Nivel 1) ──────────────────────────────

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
            => _vm.SeleccionarSugerencia((FabricanteDto)e.Source);

        // ── Eventos DataGrid Fabricantes ────────────────────

        private void DgFabricantes_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void DgFabricantes_MouseDoubleClick(object sender, WpfMouseButton e)
        {
            var fab = DgFabricantes.SelectedItem as FabricanteDto;
            if (fab is null) return;
            _ = _vm.AbrirFabricanteAsync(fab);
        }

        // ── Eventos DataGrid Contactos ──────────────────────

        private void DgContactos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.ContactoSeleccionado = DgContactos.SelectedItem as ContactoFabricanteDto;
        }

        private void DgContactos_MouseDoubleClick(object sender, WpfMouseButton e)
        {
            if (_vm?.ContactoSeleccionado != null) AbrirModalEditar(_vm.ContactoSeleccionado);
        }

        // ── Botones de cabecera ─────────────────────────────

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            _vm.VolverCommand.Execute(null);
            DgFabricantes.ItemsSource = _vm.PageRows;
        }

        private void BtnNuevoContacto_Click(object sender, RoutedEventArgs e)
            => _vm.NuevoContactoCommand.Execute(null);

        // ── Botones inline de la tabla de contactos ─────────

        private void BtnEditarContacto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContactoFabricanteDto c) AbrirModalEditar(c);
        }

        private async void BtnEliminarContacto_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarFabricante)) return;
            if (sender is not Button btn || btn.Tag is not ContactoFabricanteDto c) return;
            var r = MessageBox.Show(
                $"¿Eliminar el contacto «{c.Nombre}»?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            _vm.ContactoSeleccionado = c;
            await _vm.EliminarContactoCommand.ExecuteAsync(null);
        }

        // ── Modales ─────────────────────────────────────────

        private void AbrirModalNuevo()
        {
            if (!SesionPermisos.Tiene(Permiso.CrearFabricante)) return;
            if (_vm.FabricanteSeleccionado is null) return;
            var repo  = App.Services.GetRequiredService<IContactoFabricanteRepository>();
            var modal = new ContactoFabricanteModal(repo, _vm.FabricanteSeleccionado, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnContactoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(ContactoFabricanteDto c)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarFabricante)) return;
            if (_vm.FabricanteSeleccionado is null) return;
            var repo  = App.Services.GetRequiredService<IContactoFabricanteRepository>();
            var modal = new ContactoFabricanteModal(repo, _vm.FabricanteSeleccionado, c);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnContactoGuardado;
            MostrarModal(modal);
        }

        private void MostrarModal(System.Windows.Controls.UserControl modal)
        {
            ModalContent.Content     = modal;
            ModalOverlay.Visibility  = Visibility.Visible;
        }

        private void CerrarModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalContent.Content    = null;
        }

        private async void OnContactoGuardado()
        {
            CerrarModal();
            await _vm.CargarContactosAsync(silencioso: true);
        }

        // ── Paginación ──────────────────────────────────────

        private void RefrescarPaginacion()
        {
            if (_vm == null) return;
            PaginacionPanel.Items.Clear();
            int total = _vm.TotalPages, current = _vm.Page;
            foreach (var p in CalcularPaginas(current, total))
            {
                if (p == -1)
                {
                    PaginacionPanel.Items.Add(new TextBlock
                    {
                        Text = "…", FontFamily = new FontFamily("Segoe UI"),
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
                        Style   = (Style)(p == current ? FindResource("ActivePageBtn") : FindResource("PageBtn")),
                        Tag     = p
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
            if (total <= 7) return Enumerable.Range(1, total);
            var pages = new List<int> { 1 };
            if (current > 3) pages.Add(-1);
            for (int i = Math.Max(2, current - 1); i <= Math.Min(total - 1, current + 1); i++) pages.Add(i);
            if (current < total - 2) pages.Add(-1);
            pages.Add(total);
            return pages;
        }
    }
}
