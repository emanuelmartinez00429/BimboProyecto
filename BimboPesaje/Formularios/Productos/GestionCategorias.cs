using CapaDatos.Modelados.Productos;
using CapaDatos.Repositorios.productos_movimientos;
using CapaServicios;
using Supabase.Realtime.PostgresChanges;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using categoriaLista = CapaDatos.Modelados.Productos.Categoria;

namespace BimboPesaje.Formularios.Productos
{
    public partial class GestionCategorias : Form
    {
        private List<categoriaLista> _listaOriginal = new List<categoriaLista>();
        private Action<PostgresChangesResponse> _handlerCategoria;
        private bool _cerrando = false;
        public GestionCategorias()
        {
            InitializeComponent();
            dgvCategorias.AutoGenerateColumns = false;
        }

        private async void GestionCategorias_Load(object sender, EventArgs e)
        {
            await CargarDatos();
            ConfigurarRealtime();

        }

        private void ConfigurarRealtime()
        {
            _handlerCategoria = (c) => RecargarSafe();
            GestorRealtime.OnCategoriasChanged += _handlerCategoria;
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
                _listaOriginal = await RepositorioCategoria.ObtenerCategorias();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar productos: " + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void AplicarFiltros()
        {
            var query = _listaOriginal.AsEnumerable();

            if (rbHabilitados.Checked)
                query = query.Where(c => c.estadoCategoria == true);
            else if (rbDeshabilitados.Checked)
                query = query.Where(c => c.estadoCategoria == false);

            string texto = txtBusqueda.Text.Trim();
            if (!string.IsNullOrWhiteSpace(texto))
                query = query.Where(p =>
                    (p.nombreCategoria?.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0));

            dgvCategorias.DataSource = query.ToList();
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            agregarEditarCategoria form = new agregarEditarCategoria();

            form.ShowDialog();
        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
