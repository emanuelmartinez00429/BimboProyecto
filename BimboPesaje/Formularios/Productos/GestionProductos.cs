using CapaDatos.Repositorios;
using CapaServicios;
using Supabase.Realtime.PostgresChanges;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using productosLista = CapaDatos.Modelados.Productos.Productos;
using CapaDatos.Modelados.Productos;

namespace BimboPesaje.Formularios.Productos
{
    public partial class GestionProductos : Form
    {
        private List<productosLista> _listaOriginal = new List<productosLista>();
        private Action<PostgresChangesResponse> _handlerProductos;
        private bool _cerrando = false;

        public int    IdProductoSeleccionado { get; private set; }
        public string CodigoSeleccionado     { get; private set; }
        public string ProductoSeleccionado   { get; private set; }
        public string ProveedorSeleccionado  { get; private set; }
        public string EstadoSeleccionado     { get; private set; }

        public GestionProductos()
        {
            InitializeComponent();
            dgvProductos.AutoGenerateColumns = false;
        }

        private async void GestionProductos_Load(object sender, EventArgs e)
        {
            await CargarDatos();
            await CargarComboBoxes();
            await cargarPaises.CargarPaises(cmbPais);
            ConfigurarRealtime();
        }

        private void ConfigurarRealtime()
        {
            _handlerProductos = (c) => RecargarSafe();
            GestorRealtime.OnProductosChanged += _handlerProductos;
        }

        private void RecargarSafe()
        {
            if (_cerrando || this.IsDisposed || !this.IsHandleCreated) return;

            try
            {
                this.BeginInvoke((MethodInvoker)(async () =>
                {
                    if (_cerrando || this.IsDisposed) return;
                    await CargarDatos();
                }));
            }
            catch { }
        }

        private async Task CargarDatos()
        {
            try
            {
                _listaOriginal = await RepositorioProducto.obtenerProductosJoin();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar productos: " + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected async Task CargarComboBoxes()
        {
            cmbMarca.Items.Clear();
            cmbMarca.Items.AddRange(new string[]
            {
                "Bimbo", "Marinela", "Tía Rosa", "Ricolino",
                "Barcel", "Wonder", "Entenmann's", "Thomas'",
                "Sara Lee", "Oroweat"
            });

            cmbPais.SelectedIndex = -1;
            cmbMarca.SelectedIndex = -1;
        }

        private void txtBusqueda_TextChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void dgvProductos_DoubleClick(object sender, EventArgs e)
        {
            if (dgvProductos.SelectedRows.Count == 0) return;

            if (dgvProductos.SelectedRows[0].DataBoundItem is productosLista p)
                IdProductoSeleccionado = p.idProducto;

            CodigoSeleccionado   = dgvProductos.SelectedRows[0].Cells["Codigo"].Value?.ToString();
            ProductoSeleccionado = dgvProductos.SelectedRows[0].Cells["Producto"].Value?.ToString();
            ProveedorSeleccionado = dgvProductos.SelectedRows[0].Cells["Fabricante"].Value?.ToString();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void cmbPaises_SelectedIndexChanged(object sender, EventArgs e) { }
        private void cmbCategoria_SelectedIndexChanged(object sender, EventArgs e) { }
        private void cmbProveedor_SelectedIndexChanged(object sender, EventArgs e) { }

        private void AplicarFiltros()
        {
            var query = _listaOriginal.AsEnumerable();

            if (rbHabilitados.Checked)
                query = query.Where(p => p.idEstado == 1);
            else if (rbDeshabilitados.Checked)
                query = query.Where(p => p.idEstado != 1);

            if (cmbPais.SelectedItem is Paises paisSeleccionado)
                query = query.Where(p => p.idPais == paisSeleccionado.idPais);

            if (cmbMarca.SelectedIndex != -1)
                query = query.Where(p => p.Fabricante?.nombreFabricante == cmbMarca.SelectedItem.ToString());

            string texto = txtBusqueda.Text.Trim();
            if (!string.IsNullOrWhiteSpace(texto))
                query = query.Where(p =>
                    (p.nombreProducto?.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.codigoProducto?.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0));

            dgvProductos.DataSource = query.ToList();
        }

        private void btnLimpiar_Click(object sender, EventArgs e)
        {
            cmbPais.SelectedIndex = -1;
            cmbMarca.SelectedIndex = -1;
            rbTodos.Checked = true;
            AplicarFiltros();
        }

        private void rbHabilitados_CheckedChanged(object sender, EventArgs e)
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

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            agregarEditarProducto formAgregar = new agregarEditarProducto();
            formAgregar.ShowDialog();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _cerrando = true;

            try
            {
                if (_handlerProductos != null)
                    GestorRealtime.OnProductosChanged -= _handlerProductos;
            }
            catch { }

            base.OnFormClosed(e);
        }
    }
}