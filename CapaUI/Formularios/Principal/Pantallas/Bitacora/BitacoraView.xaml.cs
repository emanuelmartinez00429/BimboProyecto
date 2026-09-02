using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Bitacora.Dtos;
using CapaAplicacion.Productos.Dtos;
using CapaUI.Core.Controls;
using CapaDominio.Reportes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace CapaUI.Formularios.Principal.Pantallas.Bitacora
{
    public partial class BitacoraView : System.Windows.Controls.UserControl
    {
        private BitacoraViewModel _vm = null!;
        private Storyboard? _spinnerStory;
        private bool _suppressFilterChange;
        private bool _suppressSelectionChange;
        private int _dragAnchorIndex = -1;
        private int _lastDragIndex = -1;
        private bool _isSelectingByDrag;

        public BitacoraView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<BitacoraViewModel>();
            _vm.FiltrosLimpiados   += OnFiltrosLimpiados;
            _vm.AccionesRecargadas += PoblarAcciones;
            _vm.SolicitarCrearReporte += AbrirModalFormatoReporte;
            _vm.ReporteCreado      += OnReporteCreado;
            _vm.PropertyChanged    += OnVmPropertyChanged;

            DataContext = _vm;
            DgBitacora.ItemsSource = _vm.PageRows;

            // Se captura la instancia ANTES del await: si el usuario cierra la
            // pantalla mientras carga, Unloaded pone _vm = null y la continuación
            // volvería sobre una vista ya descargada. Se compara por referencia
            // para cubrir también el abrir-cerrar-abrir rápido.
            var vm = _vm;
            await vm.CargarDatosAsync();
            if (!ReferenceEquals(_vm, vm)) return;

            PoblarFiltros();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.FiltrosLimpiados   -= OnFiltrosLimpiados;
            _vm.AccionesRecargadas -= PoblarAcciones;
            _vm.SolicitarCrearReporte -= AbrirModalFormatoReporte;
            _vm.ReporteCreado      -= OnReporteCreado;
            _vm.PropertyChanged    -= OnVmPropertyChanged;
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
                case nameof(BitacoraViewModel.PageRows):  RefrescarPaginacion(); break;
                case nameof(BitacoraViewModel.IsLoading): ActualizarCarga();     break;
                case nameof(BitacoraViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(BitacoraViewModel.ErrorCarga):
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
                case nameof(BitacoraViewModel.Seleccionado):    SeleccionarEnTabla();    break;
            }
        }

        // ── Loading state ──────────────────────────────────────────────

        private void ActualizarCarga()
        {
            if (_vm.IsLoading)
            {
                DgBitacora.Visibility   = Visibility.Collapsed;
                EmptyState.Visibility   = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                DetenerSpinner();
                DgBitacora.Visibility   = Visibility.Visible;
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

        // ── Filtros ────────────────────────────────────────────────────

        private void PoblarFiltros()
        {
            if (_vm == null) return;

            _suppressFilterChange = true;

            CmbModulo.Items.Clear();
            CmbModulo.Items.Add(new ComboBoxItem { Content = "(Todos)", Tag = (int?)null });
            foreach (var m in _vm.Modulos)
                CmbModulo.Items.Add(new ComboBoxItem { Content = m.Nombre, Tag = m.Id });
            CmbModulo.SelectedIndex = 0;

            CmbUsuario.Items.Clear();
            CmbUsuario.Items.Add(new ComboBoxItem { Content = "(Todos)", Tag = (int?)null });
            foreach (var u in _vm.Usuarios)
                CmbUsuario.Items.Add(new ComboBoxItem { Content = u.Nombre, Tag = u.Id });
            CmbUsuario.SelectedIndex = 0;

            _suppressFilterChange = false;
            PoblarAcciones();
        }

        /// <summary>Repuebla Acción — se llama al cargar y cada vez que cambia el Módulo (cascada).</summary>
        private void PoblarAcciones()
        {
            _suppressFilterChange = true;
            CmbAccion.Items.Clear();
            CmbAccion.Items.Add(new ComboBoxItem { Content = "(Todas)", Tag = (int?)null });
            foreach (var a in _vm.Acciones)
                CmbAccion.Items.Add(new ComboBoxItem { Content = a.Nombre, Tag = a.Id });
            CmbAccion.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        private static int? TagDe(object? item) =>
            item is ComboBoxItem ci && ci.Tag is int id ? id : null;

        private async void CmbModulo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            _vm.AccionFiltro = null;               // el filtro de acción viejo puede no existir en el módulo nuevo
            _vm.ModuloFiltro = TagDe(CmbModulo.SelectedItem);
            await _vm.RecargarAccionesAsync();
        }

        private void CmbAccion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            _vm.AccionFiltro = TagDe(CmbAccion.SelectedItem);
        }

        private void CmbUsuario_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            _vm.UsuarioFiltro = TagDe(CmbUsuario.SelectedItem);
        }

        private void DpDesde_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            _vm.FechaDesde = DpDesde.SelectedDate;
        }

        private void DpHasta_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            _vm.FechaHasta = DpHasta.SelectedDate;
        }

        private void OnFiltrosLimpiados()
        {
            _suppressFilterChange = true;
            CmbModulo.SelectedIndex  = 0;
            CmbUsuario.SelectedIndex = 0;
            CmbAccion.SelectedIndex  = 0;
            DpDesde.SelectedDate     = null;
            DpHasta.SelectedDate     = null;
            _suppressFilterChange = false;
        }

        // ── Search ─────────────────────────────────────────────────────

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((BitacoraDto)e.Source);
            SeleccionarEnTabla();
        }

        private void SeleccionarEnTabla()
        {
            if (_vm.Seleccionado == null) return;
            if (DgBitacora.SelectedItem == _vm.Seleccionado) return;
            DgBitacora.SelectedItem = _vm.Seleccionado;
            DgBitacora.ScrollIntoView(_vm.Seleccionado);
        }

        // ── Selección y reportes ─────────────────────────────────────────

        private void DgBitacora_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressSelectionChange) return;
            SincronizarSeleccionReporte();
        }

        private void DgBitacora_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm == null || ObtenerFila(e.OriginalSource as DependencyObject) is not { } fila)
                return;

            int index = DgBitacora.Items.IndexOf(fila.Item);
            if (index < 0) return;

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                _suppressSelectionChange = true;
                fila.IsSelected = !fila.IsSelected;
                _suppressSelectionChange = false;
                SincronizarSeleccionReporte();
                DgBitacora.Focus();
                e.Handled = true;
                ReiniciarArrastre();
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ReiniciarArrastre();
                return;
            }

            _dragAnchorIndex = index;
            _lastDragIndex = index;
            _isSelectingByDrag = true;
        }

        private void DgBitacora_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelectingByDrag) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ReiniciarArrastre();
                return;
            }

            if (ObtenerFila(e.OriginalSource as DependencyObject) is not { } fila) return;

            int currentIndex = DgBitacora.Items.IndexOf(fila.Item);
            if (currentIndex < 0 || currentIndex == _lastDragIndex) return;

            _lastDragIndex = currentIndex;
            SeleccionarRango(_dragAnchorIndex, currentIndex);
            e.Handled = true;
        }

        private void DgBitacora_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
            ReiniciarArrastre();

        private void BtnSeleccionarPagina_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null || DgBitacora.Items.Count == 0) return;

            _suppressSelectionChange = true;
            if (DgBitacora.SelectedItems.Count == DgBitacora.Items.Count)
                DgBitacora.UnselectAll();
            else
                DgBitacora.SelectAll();
            _suppressSelectionChange = false;

            SincronizarSeleccionReporte();
        }

        private void SeleccionarRango(int startIndex, int endIndex)
        {
            if (startIndex < 0 || endIndex < 0) return;

            int from = Math.Min(startIndex, endIndex);
            int to = Math.Max(startIndex, endIndex);

            _suppressSelectionChange = true;
            DgBitacora.UnselectAll();
            for (int i = from; i <= to; i++)
                DgBitacora.SelectedItems.Add(DgBitacora.Items[i]);
            _suppressSelectionChange = false;

            SincronizarSeleccionReporte();
        }

        private void SincronizarSeleccionReporte()
        {
            if (_vm == null) return;

            var idsSeleccionados = DgBitacora.SelectedItems
                .Cast<BitacoraDto>()
                .Select(x => x.IdBitacora)
                .ToHashSet();
            var seleccionOrdenada = DgBitacora.Items
                .Cast<BitacoraDto>()
                .Where(x => idsSeleccionados.Contains(x.IdBitacora))
                .ToList();

            _vm.ActualizarSeleccionReporte(seleccionOrdenada);

            bool paginaCompleta = DgBitacora.Items.Count > 0 &&
                                  DgBitacora.SelectedItems.Count == DgBitacora.Items.Count;
            BtnSeleccionarPagina.Content = paginaCompleta
                ? "Limpiar selección"
                : "Seleccionar página";
            BtnSeleccionarPagina.IsEnabled = DgBitacora.Items.Count > 0;
        }

        private static DataGridRow? ObtenerFila(DependencyObject? source)
        {
            while (source is not null && source is not DataGridRow)
            {
                source = source is FrameworkContentElement content
                    ? content.Parent
                    : VisualTreeHelper.GetParent(source);
            }
            return source as DataGridRow;
        }

        private void ReiniciarArrastre()
        {
            _dragAnchorIndex = -1;
            _lastDragIndex = -1;
            _isSelectingByDrag = false;
        }

        private void AbrirModalFormatoReporte()
        {
            var modal = new FormatoReporteModal();
            modal.Cerrado += CerrarModal;
            modal.FormatoSeleccionado += async formato =>
            {
                CerrarModal();
                await ElegirRutaYGenerarAsync(formato);
            };
            ModalContent.Content = modal;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void CerrarModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalContent.Content = null;
        }

        private async Task ElegirRutaYGenerarAsync(ReportFormat formato)
        {
            string extension = formato == ReportFormat.Pdf ? ".pdf" : ".xlsx";
            var dialog = new SaveFileDialog
            {
                Title = "Guardar reporte de bitácora",
                FileName = $"Reporte_Bitacora_{DateTime.Now:yyyyMMdd_HHmmss}{extension}",
                DefaultExt = extension,
                AddExtension = true,
                OverwritePrompt = true,
                Filter = formato == ReportFormat.Pdf
                    ? "Documento PDF (*.pdf)|*.pdf"
                    : "Libro de Excel (*.xlsx)|*.xlsx",
            };

            if (dialog.ShowDialog() != true) return;
            await _vm.GenerarReporteAsync(formato, dialog.FileName);
        }

        private static void OnReporteCreado(string ruta)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"El reporte se guardó correctamente, pero no pudo abrirse automáticamente.\n\n{ex.Message}",
                    "Reporte creado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // ── Pagination ─────────────────────────────────────────────────

        private void RefrescarPaginacion()
        {
            if (_vm == null) return;
            _suppressSelectionChange = true;
            DgBitacora.UnselectAll();
            _suppressSelectionChange = false;
            DgBitacora.ItemsSource = _vm.PageRows;
            ReiniciarArrastre();
            SincronizarSeleccionReporte();

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
    }
}
