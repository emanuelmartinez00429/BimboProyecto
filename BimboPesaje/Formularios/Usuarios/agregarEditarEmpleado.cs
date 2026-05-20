using CapaDatos.Modelados.Usuarios;
using CapaDatos.Repositorios;
using CapaDatos.Repositorios.Usuario;
using CapaDominio;
using ServicioConexión.Conexion;
using System;
using System.Windows.Forms;

namespace BimboPesaje.Formularios.Usuarios
{
    public partial class agregarEditarEmpleado : Form
    {
        // Guarda el empleado recibido — null significa modo agregar
        private readonly Empleados? _empleadoEditar = null;

        public agregarEditarEmpleado()
        {
            InitializeComponent();
        }

        public agregarEditarEmpleado(Empleados empleado)
        {
            InitializeComponent();

            _empleadoEditar = empleado;

            txtDNI.Text = empleado.numeroIdentidad;
            txtNombre.Text = empleado.nombreEmpleado;
            txtApellido.Text = empleado.apellidoEmpleado;
            txtTelefono.Text = empleado.telefonoEmpleado;
            txtCorreo.Text = empleado.correoEmpleado;

            rbActivo.Enabled = true;
            rbInactivo.Enabled = true;

            if (empleado.idEstado == 1)
                rbActivo.Checked = true;
            else
                rbInactivo.Checked = true;

            lblTitulo.Text = "Editar Empleado";
        }

        private void agregarEditarEmpleado_Load(object sender, EventArgs e)
        {
            // Solo aplica en modo agregar — en edición ya se cargaron los valores
            if (_empleadoEditar == null)
            {
                rbActivo.Checked = true;
                rbActivo.Enabled = false;
                rbInactivo.Checked = false;
                rbInactivo.Enabled = false;
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
                btnGuardar.Enabled = false;

                // ── MODO EDITAR ──────────────────────────────────────────
                if (_empleadoEditar != null)
                {
                    // Obtener id_usuario de la sesión actual
                    var client = await ConexionSupabase.GetClientAsync();
                    var idUsuario = SesionActual.IdUsuario; // <- tu servicio de sesión

                    // Actualiza los campos del empleado recibido
                    _empleadoEditar.nombreEmpleado = txtNombre.Text.Trim();
                    _empleadoEditar.apellidoEmpleado = txtApellido.Text.Trim();
                    _empleadoEditar.telefonoEmpleado = txtTelefono.Text.Trim();
                    _empleadoEditar.correoEmpleado = txtCorreo.Text.Trim();
                    _empleadoEditar.numeroIdentidad = txtDNI.Text.Trim();
                    _empleadoEditar.idEstado = rbActivo.Checked ? 1 : 2;

                    await RepositorioEmpleado.actualizarEmpleados(_empleadoEditar);

                    MessageBox.Show("Empleado actualizado exitosamente",
                        "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Close();
                    return;
                }

                // ── MODO AGREGAR ─────────────────────────────────────────
                var insert = await ConexionSupabase.GetClientAsync();

                await insert.Rpc("ingresar_empleado_tabla_bitacora", new
                {
                    p_dni = txtDNI.Text.Trim(),
                    p_nombre_empleado = txtNombre.Text.Trim(),
                    p_apellido_empleado = txtApellido.Text.Trim(),
                    p_numero_telefonico = txtTelefono.Text.Trim(),
                    p_correo_empleado = txtCorreo.Text.Trim(),
                    p_estado_empleado = rbActivo.Checked ? 1 : 2
                });

                MessageBox.Show("Empleado guardado exitosamente",
                    "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el empleado: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnGuardar.Enabled = true;
            }
        }

        private void pnFill_Paint(object sender, PaintEventArgs e) { }
    }
}