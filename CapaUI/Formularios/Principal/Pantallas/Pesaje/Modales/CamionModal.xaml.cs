using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    public record CamionResult(string Placa, string Proveedor, int? IdProveedor, string Observaciones);

    public partial class CamionModal : UserControl
    {
        public event Action? Cerrado;
        public event Action<CamionResult>? Guardado;

        public CamionModal(string mode, CamionPesaje? initial, List<ProveedorItem> proveedores)
        {
            InitializeComponent();

            bool edit = mode == "edit";
            TxtEyebrow.Text = edit ? "EDICIÓN · CAMIÓN" : "NUEVO · CAMIÓN";
            TxtTitle.Text   = edit ? "Editar camión" : "Registrar camión";
            TxtGuardar.Text = edit ? "Guardar cambios" : "Registrar";
            TxtHint.Text    = edit ? "Editando camión existente" : "Máximo 3 camiones simultáneos";

            CmbProveedor.ItemsSource = proveedores;
            TxtFecha.Text = initial?.FechaAsignacion ?? PesajeCalc.FechaHoy();

            if (initial != null)
            {
                TxtPlaca.Text = initial.Placa;
                TxtObs.Text   = initial.Observaciones;
                CmbProveedor.SelectedItem = proveedores.FirstOrDefault(
                    p => (initial.IdProveedor.HasValue && p.Id == initial.IdProveedor.Value)
                         || p.Nombre == initial.Proveedor);
            }

            Loaded += (_, __) => { TxtPlaca.Focus(); Validar(this, null!); };
        }

        private void Placa_Changed(object sender, TextChangedEventArgs e)
        {
            int caret = TxtPlaca.CaretIndex;
            TxtPlaca.Text = TxtPlaca.Text.ToUpperInvariant();
            TxtPlaca.CaretIndex = Math.Min(caret, TxtPlaca.Text.Length);
            Validar(sender, e);
        }

        private void Validar(object sender, RoutedEventArgs e)
        {
            bool ok = !string.IsNullOrWhiteSpace(TxtPlaca.Text)
                      && CmbProveedor.SelectedItem is ProveedorItem;
            if (BtnGuardar != null) BtnGuardar.IsEnabled = ok;
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            if (CmbProveedor.SelectedItem is not ProveedorItem prov) return;
            Guardado?.Invoke(new CamionResult(
                TxtPlaca.Text.Trim(), prov.Nombre, prov.Id, TxtObs.Text?.Trim() ?? ""));
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
    }
}
