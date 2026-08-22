using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaDominio.Reportes;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    public partial class ReporteModal : UserControl
    {
        public event Action<ReportFormat, List<CamionPesaje>>? FormatoSeleccionado;
        public event Action? Cerrado;

        private readonly List<CamionPesaje> _camionesSeleccionados;
        private readonly List<CamionPesaje> _todosLosCamiones;

        public ReporteModal(List<CamionPesaje> camionesSeleccionados, List<CamionPesaje>? todosLosCamiones = null)
        {
            InitializeComponent();
            _camionesSeleccionados = camionesSeleccionados ?? new();
            _todosLosCamiones = todosLosCamiones ?? _camionesSeleccionados;

            // Si solo hay 1 camión en total o los seleccionados son todos, deshabilitar o no marcar
            if (_todosLosCamiones.Count <= 1 || _camionesSeleccionados.Count == _todosLosCamiones.Count)
            {
                ChkTodosCamiones.Visibility = Visibility.Collapsed;
            }
            else
            {
                ChkTodosCamiones.Visibility = Visibility.Visible;
                ChkTodosCamiones.IsChecked = false;
            }

            ActualizarListaVisual();
        }

        private void ActualizarListaVisual()
        {
            var lista = ChkTodosCamiones.IsChecked == true ? _todosLosCamiones : _camionesSeleccionados;
            LstCamiones.ItemsSource = lista;
        }

        private void ChkTodosCamiones_Changed(object sender, RoutedEventArgs e)
        {
            ActualizarListaVisual();
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            var lista = ChkTodosCamiones.IsChecked == true ? _todosLosCamiones : _camionesSeleccionados;
            FormatoSeleccionado?.Invoke(ReportFormat.Pdf, lista);
        }

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            var lista = ChkTodosCamiones.IsChecked == true ? _todosLosCamiones : _camionesSeleccionados;
            FormatoSeleccionado?.Invoke(ReportFormat.Excel, lista);
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
    }
}
