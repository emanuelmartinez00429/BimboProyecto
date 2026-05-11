using CapaDatos.Modelados.Pesajes;
using CapaDatos.Repositorios;
using CapaServicios;

namespace BimboPesaje.Formularios.Productos
{
    public partial class RegistrarPesos : Form
    {
        private readonly int     _idMovProducto;
        private readonly int     _idProducto;
        private readonly decimal _pesoManifestado;
        private readonly int     _bultosTeóricos;

        public bool SeguirPesando { get; private set; }

        /// <summary>
        /// Constructor principal de la fase 4.
        /// Recibe el contexto del movimiento-producto y guarda la entrada directamente en Supabase.
        /// </summary>
        public RegistrarPesos(
            int     idMovProducto,
            int     idProducto,
            string  placa,
            string  producto,
            string  proveedor,
            decimal pesoManifestado,
            int     bultosTeóricos)
        {
            InitializeComponent();

            _idMovProducto   = idMovProducto;
            _idProducto      = idProducto;
            _pesoManifestado = pesoManifestado;
            _bultosTeóricos  = bultosTeóricos;

            // Contexto de solo lectura
            txtPlaca.Text       = placa;
            txtProducto.Text    = producto;
            txtProveedor.Text   = proveedor;
            txtReporteNo.Text   = $"MOV-PROD-{idMovProducto}";

            // Fecha/hora por defecto
            dtpFechaEntrada.Value = DateTime.Now;
            dtpFechaSalida.Value  = DateTime.Now;

            // Habilitar campos que el usuario debe llenar
            txtPesoBruto.Enabled      = true;
            txtPesoTaraExtra.Enabled  = true;
            txtNoBultos.Enabled       = true;
            dtpFechaSalida.Enabled    = true;

            // Campos calculados → solo lectura
            txtPesoNeto.ReadOnly      = true;
            txtPeseTaraInd.ReadOnly   = true;
            txtPeseTaraInd.Text       = "0.00";   // sin tara individual por defecto
            txtPesoNetTotal.ReadOnly  = true;
            txtPesoNetTotal.Text      = "0.00";

            // Valores por defecto editables
            txtPesoBruto.Text     = "";
            txtPesoTaraExtra.Text = "0.00";
            txtNoBultos.Text      = "0";
            txtObservaciones.Text = "";

            // Cargar total acumulado de entradas previas
            _ = CargarTotalPrevioAsync();

            ConfigurarFechas();
            CalcularPrevisualizacion();
        }

        private void ConfigurarFechas()
        {
            dtpFechaEntrada.Format       = DateTimePickerFormat.Custom;
            dtpFechaEntrada.CustomFormat = "dd/MM/yyyy HH:mm";
            dtpFechaSalida.Format        = DateTimePickerFormat.Custom;
            dtpFechaSalida.CustomFormat  = "dd/MM/yyyy HH:mm";
        }

        private async Task CargarTotalPrevioAsync()
        {
            try
            {
                var entradas = await RepositorioEntrada.ObtenerPorMovProductoAsync(_idMovProducto);
                decimal total = entradas.Sum(ep => ep.pesoNeto);
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke((MethodInvoker)(() => txtPesoNetTotal.Text = total.ToString("F2")));
            }
            catch { }
        }

        private void RegistrarPesos_Load(object sender, EventArgs e) { }

        // ── Preview en tiempo real ────────────────────────────────────────────

        private void txtPesoBruto_TextChanged(object sender, EventArgs e)
            => CalcularPrevisualizacion();

        private void txtPesoNeto_TextChanged(object sender, EventArgs e) { }

        private void txtPesoTaraExtra_TextChanged(object sender, EventArgs e)
            => CalcularPrevisualizacion();

        private void CalcularPrevisualizacion()
        {
            if (!decimal.TryParse(txtPesoBruto.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal bruto))
            {
                txtPesoNeto.Text = "";
                lblDiferencia.Text = "";
                return;
            }

            decimal taraInd   = decimal.TryParse(txtPeseTaraInd.Text, out var ti) ? ti : 0;
            decimal taraExtra = decimal.TryParse(txtPesoTaraExtra.Text.Replace(",", "."),
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var te) ? te : 0;

            decimal taraTotal = taraInd + taraExtra;
            decimal neto      = bruto - taraTotal;

            txtPesoNeto.Text = neto.ToString("F2");

            if (_pesoManifestado > 0)
            {
                decimal diferencia = PesoCalculator.DiferenciaPct(neto, _pesoManifestado);
                lblDiferencia.Text      = $"{diferencia:F3}%";
                lblDiferencia.ForeColor = diferencia >= 0 ? Color.LimeGreen : Color.OrangeRed;
            }
            else
            {
                lblDiferencia.Text = "—";
            }
        }

        // ── Guardar ───────────────────────────────────────────────────────────

        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            if (!ValidarCampos(out decimal bruto, out decimal taraExtra, out int bultos)) return;

            btnGuardar.Enabled = false;
            btnSeguir.Enabled  = false;
            try
            {
                await GuardarEntradaAsync(bruto, taraExtra, bultos);
                SeguirPesando = false;
                DialogResult  = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar pesaje: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnGuardar.Enabled = true;
                btnSeguir.Enabled  = true;
            }
        }

        private async void btnSeguir_Click(object sender, EventArgs e)
        {
            if (!ValidarCampos(out decimal bruto, out decimal taraExtra, out int bultos)) return;

            btnGuardar.Enabled = false;
            btnSeguir.Enabled  = false;
            try
            {
                await GuardarEntradaAsync(bruto, taraExtra, bultos);
                SeguirPesando = true;
                DialogResult  = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar pesaje: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnGuardar.Enabled = true;
                btnSeguir.Enabled  = true;
            }
        }

        private bool ValidarCampos(out decimal bruto, out decimal taraExtra, out int bultos)
        {
            bruto     = 0;
            taraExtra = 0;
            bultos    = 0;

            if (!decimal.TryParse(txtPesoBruto.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out bruto) || bruto <= 0)
            {
                MessageBox.Show("Ingrese un peso bruto válido mayor a cero.", "Validación",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPesoBruto.Focus();
                return false;
            }

            decimal.TryParse(txtPesoTaraExtra.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out taraExtra);

            int.TryParse(txtNoBultos.Text, out bultos);
            return true;
        }

        private async Task GuardarEntradaAsync(decimal bruto, decimal taraExtra, int bultos)
        {
            var entrada = new EntradaProducto
            {
                idMovProducto        = _idMovProducto,
                idProducto           = _idProducto,
                pesoBruto            = bruto,
                pesoTaraExtra        = taraExtra,
                numeroBultosRecibido = bultos > 0 ? bultos : null,
                fechaEntrada         = DateOnly.FromDateTime(dtpFechaEntrada.Value),
                horaEntrada          = TimeOnly.FromDateTime(dtpFechaEntrada.Value),
                fechaSalida          = DateOnly.FromDateTime(dtpFechaSalida.Value),
                horaSalida           = TimeOnly.FromDateTime(dtpFechaSalida.Value),
                idUsuario            = SesionActual.IdUsuario,
                idEstado             = EstadosPesaje.Activo,
                idTara               = null,
                idTarima             = null,
                observaciones        = txtObservaciones.Text.Trim()
            };

            await RepositorioEntrada.CrearAsync(entrada);
        }

        private void btnVolver_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
