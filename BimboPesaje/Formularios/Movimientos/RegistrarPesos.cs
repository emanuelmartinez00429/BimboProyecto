using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CapaServicios;

namespace BimboPesaje.Formularios.Productos
{
    public partial class RegistrarPesos : Form
    {
        public double PesoIngresado { get; set; }
        public bool SeguirPesando { get; set; }
        public double PesoBruto { get; set; }
        public RegistrarPesos(string placa, string producto, string proveedor)
        {
            InitializeComponent();
            ConfigurarFechas();
            CargarValoresDummy();

            txtPlaca.Text = placa;
            txtProducto.Text = producto;
            txtProveedor.Text = proveedor;


            CalcularDiferencia();
        }

        public RegistrarPesos(string codigo, string producto, string proveedor, double pesoNeto)
        {
            InitializeComponent();
            ConfigurarFechas();
            CargarValoresDummy(); 

            txtPlaca.Text = codigo;
            txtProducto.Text = producto;
            txtProveedor.Text = proveedor;
            txtPesoNeto.Text = pesoNeto.ToString("F2");
            txtPesoNetTotal.Text = pesoNeto.ToString("F2");
            
            CalcularDiferencia();
        }

        // ===== MÉTODOS COMPARTIDOS =====
        private void ConfigurarFechas()
        {
            dtpFechaEntrada.Format = DateTimePickerFormat.Custom;
            dtpFechaEntrada.CustomFormat = "dd/MM/yyyy HH:mm";
            dtpFechaSalida.Format = DateTimePickerFormat.Custom;
            dtpFechaSalida.CustomFormat = "dd/MM/yyyy HH:mm";
            dtpFechaEntrada.Value = DateTime.Now;
            dtpFechaSalida.Value = DateTime.Now.AddHours(2);
        }

        private void CargarValoresDummy()
        {
            txtReporteNo.Text = $"REP-{DateTime.Now:yyyyMMddHHmm}";
            txtPesoBruto.Text = "5000.00";
            txtPeseTaraInd.Text = "50.00";
            //txtPesoNetTotal.Text = "1400.00";
            txtPesoTaraExtra.Text = "25.00";
            txtNoBultos.Text = "10";
            txtObservaciones.Text = "Sin observaciones.";
        }

        private void RegistrarPesos_Load(object sender, EventArgs e)
        {

        }

        private void txtPesoBruto_TextChanged(object sender, EventArgs e)
        {
            CalcularDiferencia();
        }

        private void txtPesoNeto_TextChanged(object sender, EventArgs e)
        {
            CalcularDiferencia();
        }

        private void CalcularDiferencia()
        {
            if (decimal.TryParse(txtPesoBruto.Text, out decimal bruto) &&
                decimal.TryParse(txtPesoNeto.Text, out decimal neto))
            {
                if (bruto > 0)
                {
                    // Diferencia: (neto - bruto) / bruto × 100
                    // Negativo = faltante, positivo = excedente
                    decimal diferencia = PesoCalculator.DiferenciaPct(neto, bruto);
                    lblDiferencia.Text = $"{diferencia:F3}%";
                    lblDiferencia.ForeColor = diferencia >= 0 ? Color.Green : Color.Red;
                }
            }
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            if (double.TryParse(txtPesoNeto.Text, out double peso)&& double.TryParse(txtPesoBruto.Text, out double bruto))
            {
                PesoIngresado = peso;
                PesoBruto = bruto;
                SeguirPesando = false;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("Ingrese un peso válido.");
            }
        }
        
        private void btnSeguir_Click(object sender, EventArgs e)
        {
            if (double.TryParse(txtPesoNeto.Text, out double peso)&& double.TryParse(txtPesoBruto.Text, out double bruto))
            {
                PesoIngresado = peso;
                PesoBruto = bruto;
                SeguirPesando = true;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("Ingrese un peso válido.");
            }
        }

        private void btnVolver_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
