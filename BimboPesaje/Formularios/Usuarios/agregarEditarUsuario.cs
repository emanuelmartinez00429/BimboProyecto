using CapaDatos.Modelados.Usuarios;
using CapaDatos.Repositorios.Usuario;
using CapaDominio;
using ServicioConexión.Conexion;
using Supabase.Gotrue;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Usuario = CapaDatos.Modelados.Usuarios.Usuarios;

namespace BimboPesaje.Formularios.Usuarios
{
    public partial class agregarEditarUsuario : Form
    {
        /// <summary>
        /// Variables globales
        /// </summary>
        private Empleados _empleadoRegistrar;
        private Session _sessionOriginal;

        public agregarEditarUsuario(Empleados empleados)
        {
            InitializeComponent();
            _empleadoRegistrar = empleados;
        }

        private async void agregarEditarUsuario_Load(object sender, EventArgs e)
        {
            var roles = await RepositorioUsuario.obtenerRoles();

            cmbRol.DataSource = roles;
            cmbRol.DisplayMember = "nombreRol";
            cmbRol.ValueMember = "idRol";

            if (_empleadoRegistrar != null)
            {
                txtCorreo.Text = _empleadoRegistrar.correoEmpleado;
            }
        }

        private void btnVolver_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                //variables
                btnGuardar.Enabled = false;
                var client = await ConexionSupabase.GetClientAsync();
                int idEmpleado = _empleadoRegistrar.idEmpleado;
                string correo = txtCorreo.Text.Trim();
                int rolSeleccionado = (int)cmbRol.SelectedValue;

                // 1. Guardar el token de la sesión actual ANTES del signup
                var tokenOriginal = servicioSesionActual.Sesion?.AccessToken;
                var refreshOriginal = servicioSesionActual.Sesion?.RefreshToken;

                // 2. Hacer el signup del nuevo usuario
                var nuevaSesion = await client.Auth.SignUp(txtCorreo.Text.Trim(), txtContrasenia.Text.Trim());

                // 3. Restaurar la sesión original inmediatamente
                if (tokenOriginal != null && refreshOriginal != null)
                {
                    await client.Auth.SetSession(tokenOriginal, refreshOriginal);
                }

                // 4. Guardar en tabla interna con la RPC
                await client.Rpc("crear_usuario_empleado_seguro", new
                {
                    p_id_empleado = idEmpleado,
                    p_email = correo,
                    p_rol = rolSeleccionado
                });

                MessageBox.Show("Usuario creado exitosamente.", "Éxito",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al guardar el usuario: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}