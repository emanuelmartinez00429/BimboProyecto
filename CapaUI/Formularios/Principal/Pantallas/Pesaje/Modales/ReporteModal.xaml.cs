using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    public partial class ReporteModal : UserControl
    {
        public event Action? Cerrado;

        public ReporteModal(List<CamionPesaje> camiones)
        {
            InitializeComponent();
            LstCamiones.ItemsSource = camiones;
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
    }
}
