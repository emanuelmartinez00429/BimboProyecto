using CapaDatos.Modelados.Usuarios;
using CapaDatos.Repositorios.Usuario;
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
using empleadosLista = CapaDatos.Modelados.Usuarios.Empleados;

namespace BimboPesaje.Formularios.Usuarios
{
    public partial class GestiónEmpleados : Form
    {

        private List<empleadosLista> _listaOriginal = new List<empleadosLista>();
        private Action<PostgresChangesResponse> _handlerEmpleados;
        private bool _cerrando = false;
        public GestiónEmpleados()
        {
            InitializeComponent();
            dgvEmpleados.AutoGenerateColumns = false;
        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void GestiónEmpleados_Load(object sender, EventArgs e)
        {
            await CargarDatos();
            ConfigurarRealtime();
        }
        private void ConfigurarRealtime()
        {
            _handlerEmpleados = (c) => RecargarSafe();
            GestorRealtime.OnEmpleadosChanged += _handlerEmpleados;
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
                _listaOriginal = await RepositorioEmpleado.obtenerEmpleados();
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
                query = query.Where(e => e.idEstado == 1);
            else if (rbDeshabilitados.Checked)
                query = query.Where(e => e.idEstado == 2);

            string texto = txtBusqueda.Text.Trim();
            if (!string.IsNullOrWhiteSpace(texto))
                query = query.Where(e =>
                    (e.nombreEmpleado?.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0));

            dgvEmpleados.DataSource = query.ToList();
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            agregarEditarEmpleado formAgregar = new agregarEditarEmpleado();
            formAgregar.ShowDialog();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            try
            {

                var seleccionado = dgvEmpleados.CurrentRow?.DataBoundItem as empleadosLista;
                if (seleccionado == null) return;

                // Ya no necesitas construir el objeto — seleccionado ES el empleado
                using var formEditar = new agregarEditarEmpleado(seleccionado);
                formEditar.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir el formulario de edición: " + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnCrearUsuario_Click(object sender, EventArgs e)
        {
            try
            {

                var seleccionado = dgvEmpleados.CurrentRow?.DataBoundItem as empleadosLista;
                if (seleccionado == null) return;

                if (seleccionado.idEstado == 0)
                {
                    MessageBox.Show("No se puede crear un usuario para un empleado inactivo.",
                        "Empleado Inactivo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var formEditar = new agregarEditarUsuario(seleccionado);
                formEditar.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir el formulario de creación de usuario: " + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
