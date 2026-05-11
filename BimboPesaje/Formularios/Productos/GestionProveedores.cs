using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BimboPesaje.Formularios.Productos
{
    public partial class GestionProveedores : Form
    {
        private DataTable _tablaOriginal;
        public GestionProveedores()
        {
            InitializeComponent();
        }

        private async void GestionProveedores_Load(object sender, EventArgs e)
        {
            await CargarDatosDummy();
            await CargarComboBoxes();
        }

        private async Task CargarDatosDummy()
        {
            DataTable tabla = new DataTable();
            tabla.Columns.Add("IdProveedor");
            tabla.Columns.Add("RTNProveedor");
            tabla.Columns.Add("NombreProveedor");
            tabla.Columns.Add("TelefonoProveedor");
            tabla.Columns.Add("CorreoProveedor");
            tabla.Columns.Add("DireccionProveedor");
            tabla.Columns.Add("EstadoProveedor");

            tabla.Rows.Add("001", "08019999123456", "AGROTOR/JAREMAR", "+504 2232-1100", "ventas@jaremar.com", "Col. Las Lomas, Tegucigalpa", "Activo");
            tabla.Rows.Add("002", "08019998765432", "ALPEZZI", "+52 81 8888-0101", "contacto@alpezzi.com.mx", "Monterrey, Nuevo León, México", "Activo");
            tabla.Rows.Add("003", "08019997654321", "ALTEX FLEXPORT", "+52 55 5555-2233", "pedidos@altexflexport.mx", "Ciudad de México, México", "Activo");
            tabla.Rows.Add("004", "08019996543210", "BARRY CALLEBAUT", "+52 55 4444-9988", "mx.ventas@barry-callebaut.com", "Tlalnepantla, México", "Activo");
            tabla.Rows.Add("005", "08019995432109", "BIMBO CENTROAMERICA", "+502 2223-4400", "proveedores@bimboca.com", "Ciudad de Guatemala, Guatemala", "Activo");
            tabla.Rows.Add("006", "08019994321098", "CISA", "+504 2220-3300", "contacto@cisa.hn", "San Pedro Sula, Cortés", "Activo");
            tabla.Rows.Add("007", "08019993210987", "COLOR SENSIENT", "+52 55 6677-8899", "sensient.mx@sensient.com", "Querétaro, México", "Activo");
            tabla.Rows.Add("008", "08019992109876", "DANISCO", "+52 55 3344-5566", "danisco.ventas@dupont.com", "Monterrey, Nuevo León, México", "Activo");
            tabla.Rows.Add("009", "08019991098765", "LEVAPAN", "+57 1 800-0123", "ventas@levapan.com", "Bogotá, Colombia", "Activo");
            tabla.Rows.Add("010", "08019990987654", "PURATOS", "+32 3 890-0200", "info@puratos.com", "Groot-Bijgaarden, Bélgica", "Inactivo");

            dgvProveedor.AutoGenerateColumns = false;
            dgvProveedor.DataSource = tabla;
        }

        protected async Task CargarComboBoxes()
        {
            // === PAÍSES ===
            cmbPais.Items.Clear();
            cmbPais.Items.AddRange(new string[]
            {
                "México",
                "Honduras",
                "Guatemala",
                "El Salvador",
                "Nicaragua",
                "Costa Rica",
                "Panamá",
                "Colombia",
                "Chile",
                "Argentina"
            });

            // Opcional: texto por defecto
            cmbPais.SelectedIndex = -1;

        }

        private void FiltrarPorEstado(string estado)
        {
            var filasFiltradas = _tablaOriginal.AsEnumerable()
                                               .Where(row => row["EstadoFabricante"]
                                               .ToString() == estado);

            dgvProveedor.DataSource = filasFiltradas.Any()
                ? filasFiltradas.CopyToDataTable()
                : _tablaOriginal.Clone();
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (rbHabilitados.Checked)
                FiltrarPorEstado("Activo");
        }

        private void rbDeshabilitados_CheckedChanged(object sender, EventArgs e)
        {
            if (rbDeshabilitados.Checked)
                FiltrarPorEstado("Inactivo");
        }

        private void rbTodos_CheckedChanged(object sender, EventArgs e)
        {
            if (rbTodos.Checked)
                dgvProveedor.DataSource = _tablaOriginal;
        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
