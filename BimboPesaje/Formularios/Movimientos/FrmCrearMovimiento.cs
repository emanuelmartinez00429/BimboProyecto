using CapaDatos.Modelados.Pesajes;
using CapaDatos.Repositorios.productos_movimientos;
using CapaServicios;

namespace BimboPesaje.Formularios.Movimientos
{
    /// <summary>
    /// Modal para registrar un nuevo camión (movimiento de entrada).
    /// Carga proveedores activos desde Supabase y persiste vía RepositorioMovimiento.
    /// </summary>
    public class FrmCrearMovimiento : Form
    {
        private readonly ComboBox    _cmbProveedor;
        private readonly TextBox     _txtPlaca;
        private readonly TextBox     _txtObservaciones;
        private readonly DateTimePicker _dtpFecha;
        private readonly Button      _btnGuardar;
        private readonly Button      _btnCancelar;

        public Movimiento? MovimientoCreado { get; private set; }

        public FrmCrearMovimiento()
        {
            Text              = "Registrar Camión";
            ClientSize        = new Size(440, 330);
            StartPosition     = FormStartPosition.CenterParent;
            FormBorderStyle   = FormBorderStyle.FixedDialog;
            MaximizeBox       = false;
            MinimizeBox       = false;
            BackColor         = Color.FromArgb(233, 238, 247);

            int lx = 20, cx = 175, y = 25, rh = 52;

            AddLabel("Proveedor:",          lx, y);
            _cmbProveedor = new ComboBox { Location = new Point(cx, y), Size = new Size(240, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_cmbProveedor);
            y += rh;

            AddLabel("Placa del vehículo:", lx, y);
            _txtPlaca = new TextBox { Location = new Point(cx, y), Size = new Size(240, 28), MaxLength = 20, CharacterCasing = CharacterCasing.Upper };
            Controls.Add(_txtPlaca);
            y += rh;

            AddLabel("Fecha asignación:",   lx, y);
            _dtpFecha = new DateTimePicker { Location = new Point(cx, y), Size = new Size(240, 28), Format = DateTimePickerFormat.Short, Value = DateTime.Today };
            Controls.Add(_dtpFecha);
            y += rh;

            AddLabel("Observaciones:",      lx, y);
            _txtObservaciones = new TextBox { Location = new Point(cx, y), Size = new Size(240, 60), Multiline = true };
            Controls.Add(_txtObservaciones);
            y += 70;

            _btnCancelar = CreateButton("Cancelar", lx,  y, Color.Gray);
            _btnCancelar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancelar);

            _btnGuardar = CreateButton("Guardar", 300, y, Color.FromArgb(12, 92, 92));
            _btnGuardar.Click += BtnGuardar_Click;
            Controls.Add(_btnGuardar);

            Load += FrmCrearMovimiento_Load;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text     = text,
                Location = new Point(x, y + 4),
                AutoSize = true,
                Font     = new Font("Segoe UI", 10f)
            });
        }

        private static Button CreateButton(string text, int x, int y, Color back)
        {
            var btn = new Button
            {
                Text      = text,
                Location  = new Point(x, y),
                Size      = new Size(115, 36),
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private async void FrmCrearMovimiento_Load(object sender, EventArgs e)
        {
            try
            {
                var proveedores = await RepositorioProveedor.ObtenerActivosAsync();
                _cmbProveedor.DisplayMember = "nombreProveedor";
                _cmbProveedor.ValueMember   = "idProveedor";
                _cmbProveedor.DataSource    = proveedores;
                _cmbProveedor.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar proveedores: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnGuardar_Click(object sender, EventArgs e)
        {
            if (_cmbProveedor.SelectedItem is not Proveedores prov)
            {
                MessageBox.Show("Seleccione un proveedor.", "Validación",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtPlaca.Text))
            {
                MessageBox.Show("Ingrese la placa del vehículo.", "Validación",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnGuardar.Enabled = false;
            try
            {
                var mov = new Movimiento
                {
                    idProveedor     = prov.idProveedor,
                    placaVehiculo   = _txtPlaca.Text.Trim(),
                    fechaAsignacion = DateOnly.FromDateTime(_dtpFecha.Value),
                    idUsuario       = SesionActual.IdUsuario,
                    idEstado        = EstadosPesaje.Abierto,
                    observaciones   = _txtObservaciones.Text.Trim()
                };

                MovimientoCreado = await RepositorioMovimiento.CrearAsync(mov);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnGuardar.Enabled = true;
            }
        }
    }
}
