using System.Data;
using CapaDatos.Modelados.Pesajes;
using CapaDatos.Repositorios.productos_movimientos;

namespace BimboPesaje.Formularios.Productos
{
    public partial class GestionProveedores : Form
    {
        private List<Proveedores> _listaOriginal = new();
        private DataTable _tablaOriginal = new();

        public GestionProveedores()
        {
            InitializeComponent();
            txtBusqueda.TextChanged += (s, e) => AplicarFiltros();
            btnLimpiar.Click        += BtnLimpiar_Click;
        }

        private async void GestionProveedores_Load(object sender, EventArgs e)
        {
            await CargarDatos();
            CargarComboBoxes();
        }

        private async Task CargarDatos()
        {
            try
            {
                _listaOriginal = await RepositorioProveedor.ObtenerTodosAsync();
                _tablaOriginal = ProyectarTabla(_listaOriginal);
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar proveedores: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static DataTable ProyectarTabla(List<Proveedores> lista)
        {
            var tabla = new DataTable();
            tabla.Columns.Add("IdProveedor",      typeof(int));
            tabla.Columns.Add("RtnProveedor");
            tabla.Columns.Add("NombreProveedor");
            tabla.Columns.Add("TelefonoProveedor");
            tabla.Columns.Add("CorreoProveedor");
            tabla.Columns.Add("DireccionProveedor");
            tabla.Columns.Add("EstadoProveedor");

            foreach (var p in lista)
            {
                string estado = p.idEstado == EstadosPesaje.Activo ? "Activo" : "Inactivo";
                tabla.Rows.Add(
                    p.idProveedor,
                    p.rtnProveedor        ?? "",
                    p.nombreProveedor,
                    p.telefonoProveedor   ?? "",
                    p.correoProveedor     ?? "",
                    p.direccionProveedor  ?? "",
                    estado
                );
            }
            return tabla;
        }

        private void AplicarFiltros()
        {
            if (_tablaOriginal.Rows.Count == 0)
            {
                dgvProveedor.AutoGenerateColumns = false;
                dgvProveedor.DataSource = null;
                return;
            }

            IEnumerable<DataRow> query = _tablaOriginal.AsEnumerable();

            if (rbHabilitados.Checked)
                query = query.Where(r => r["EstadoProveedor"].ToString() == "Activo");
            else if (rbDeshabilitados.Checked)
                query = query.Where(r => r["EstadoProveedor"].ToString() == "Inactivo");

            string texto = txtBusqueda.Text.Trim();
            if (!string.IsNullOrWhiteSpace(texto))
                query = query.Where(r =>
                    r["NombreProveedor"].ToString()!.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r["RtnProveedor"].ToString()!.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0);

            dgvProveedor.AutoGenerateColumns = false;
            dgvProveedor.DataSource = query.Any() ? query.CopyToDataTable() : _tablaOriginal.Clone();
        }

        private void CargarComboBoxes()
        {
            cmbPais.Items.Clear();
            cmbPais.Items.AddRange(new string[]
            {
                "México", "Honduras", "Guatemala", "El Salvador",
                "Nicaragua", "Costa Rica", "Panamá", "Colombia", "Chile", "Argentina"
            });
            cmbPais.SelectedIndex = -1;
        }

        private void BtnLimpiar_Click(object sender, EventArgs e)
        {
            txtBusqueda.Clear();
            cmbPais.SelectedIndex = -1;
            rbHabilitados.Checked = true;
            AplicarFiltros();
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (rbHabilitados.Checked) AplicarFiltros();
        }

        private void rbDeshabilitados_CheckedChanged(object sender, EventArgs e)
        {
            if (rbDeshabilitados.Checked) AplicarFiltros();
        }

        private void rbTodos_CheckedChanged(object sender, EventArgs e)
        {
            if (rbTodos.Checked) AplicarFiltros();
        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
