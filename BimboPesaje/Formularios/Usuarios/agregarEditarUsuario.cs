using CapaDatos.Modelados.Usuarios;
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
    public partial class agregarEditarUsuario : Form
    {
        public agregarEditarUsuario(Empleados empleados)
        {
            InitializeComponent();
            txtCorreo.Text = empleados.correoEmpleado;
            rbInactivo.Enabled = false;
            rbActivo.Enabled = false;
            rbActivo.Checked = empleados.idEstado == 1;
        }

        private void btnVolver_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void agregarEditarUsuario_Load(object sender, EventArgs e)
        {
            
        }
    }
}
