using CapaDatos.Modelados.Productos;
using CapaDatos.Repositorios.productos_movimientos;
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
    public partial class agregarEditarCategoria : Form
    {
        public agregarEditarCategoria()
        {
            InitializeComponent();
        }

        private void btnVolver_Click(object sender, EventArgs e)
        {
            this.Close();

        }

        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                Categoria nuevaCategoria = new Categoria
                {
                    nombreCategoria = txtnombreCategoria.Text.Trim(),
                    descripcionCategoria = txtDescripcion.Text.Trim(),
                    estadoCategoria = rbActivo.Checked,
                };

                await RepositorioCategoria.InsertarCategoria(nuevaCategoria);
                MessageBox.Show("Categoría guardada exitosamente.",
                                "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar la categoría: " + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }
}
