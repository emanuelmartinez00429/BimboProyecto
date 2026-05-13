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

namespace BimboPesaje.Formularios.Usuarios
{
    public partial class GestionUsuarios : Form
    {
        private List<usuarioVista> _listaOriginal = new List<usuarioVista>();
        private Action<PostgresChangesResponse> _handlerUsuarios;
        private bool _cerrando = false;

        public GestionUsuarios()
        {
            InitializeComponent();
            dgvUsuarios.AutoGenerateColumns = false;
        }

        private async void GestionUsuarios_Load(object sender, EventArgs e)
        {
            await CargarDatos();
            ConfigurarRealtime();
        }

        private void ConfigurarRealtime()
        {
            _handlerUsuarios = (c) => RecargarSafe();
            GestorRealtime.OnUsuariosChanged += _handlerUsuarios;
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
                _listaOriginal = await RepositorioUsuario.obtenerUsuarios();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar usuarios: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void AplicarFiltros()
        {
            var query = _listaOriginal.AsEnumerable();

            if (rbHabilitados.Checked)
                query = query.Where(u => u.idEstado == 1);
            else if (rbDeshabilitados.Checked)
                query = query.Where(u => u.idEstado == 2);

            string texto = txtBusqueda.Text.Trim();

            if (!string.IsNullOrWhiteSpace(texto))
            {
                query = query.Where(u =>
                    (u.correoUsuario?.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0)
                    ||
                    (u.nombre_Empleado?.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0)
                    ||
                    (u.nombre_Rol?.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            dgvUsuarios.DataSource = query.ToList();
        }

        private void rbHabilitados_CheckedChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void rbDeshabilitados_CheckedChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void rbTodos_CheckedChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void txtBusqueda_TextChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        
    }
}