using CapaDatos.Modelados;
using CapaDatos.Modelados.Productos;
using CapaDatos.Repositorios.productos_movimientos;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BimboPesaje.Formularios.Productos
{
    public partial class agregarEditarProducto : Form
    {
        private List<Paises> _listaPaises = new List<Paises>();
        private int idFabricanteSeleccionado;
        private int idCategoriaSeleccionada;
        private int idPresentacionSeleccionada;

        public agregarEditarProducto()
        {
            InitializeComponent();
        }

        public agregarEditarProducto(int idFabricante, int idCategoria, int idPresentacion)
        {
            InitializeComponent();
            this.idFabricanteSeleccionado = idFabricante;
            this.idCategoriaSeleccionada = idCategoria;
            this.idPresentacionSeleccionada = idPresentacion;
        }

        private async void agregarEditarProducto_Load(object sender, EventArgs e)
        {
            await cargarPaises.CargarPaises(cmbPaisImportado);
        }

        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {

                ProductosInsertar productos = new ProductosInsertar
                {
                    codigoProducto = txtCodigoInterno.Text.Trim(),
                    nombreProducto = txtNombreProducto.Text.Trim(),
                    idFabricante = Convert.ToInt32(txtFabricante.Text.Trim()),
                    idCategoria = Convert.ToInt32(txtCategoria.Text.Trim()),
                    idPresentacion = Convert.ToInt32(txtPresentacion.Text.Trim()),
                    idTara = 1, // Asignar un valor predeterminado o permitir que el usuario lo seleccione
                    contenidoProducto = txtContenido.Text.Trim(),
                    idPais = (cmbPaisImportado.SelectedItem as Paises)?.idPais ?? 0,
                    idEstado = rbActivo.Checked ? 1 : 0,
                    pesoTeorico = Convert.ToDecimal(txtPeso.Text.Trim())
                };


                await RepositorioProducto.ingresarProducto(productos);

                MessageBox.Show("Producto guardado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar el producto: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void btnVolver_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnPre_Click(object sender, EventArgs e)
        {
            GestionProveedores gestion = new GestionProveedores();
            gestion.ShowDialog();
        }

        private void btnFab_Click(object sender, EventArgs e)
        {
            using var frm = new GestionFabricantes();

            if (frm.ShowDialog() == DialogResult.OK)
            {
                idFabricanteSeleccionado = frm.IdFabricanteSeleccionado;
                txtFabricante.Text = frm.NombreFabricanteSeleccionado;
            }
        }
    }
}
