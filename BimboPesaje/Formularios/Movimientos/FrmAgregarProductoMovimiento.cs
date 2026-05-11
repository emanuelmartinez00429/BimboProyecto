using CapaDatos.Modelados.Pesajes;
using CapaDatos.Repositorios;
using CapaServicios;
using BimboPesaje.Formularios.Productos;

namespace BimboPesaje.Formularios.Movimientos
{
    /// <summary>
    /// Modal para agregar un producto a un camión (movimiento_producto).
    /// Usa GestionProductos como selector y persiste vía RepositorioMovimientoProducto.
    /// </summary>
    public class FrmAgregarProductoMovimiento : Form
    {
        private readonly int     _idMovimiento;
        private int              _idProductoSeleccionado = 0;

        private readonly Label           _lblProducto;
        private readonly Button          _btnSeleccionar;
        private readonly NumericUpDown   _nudPesoManifestado;
        private readonly NumericUpDown   _nudBultosTeóricos;
        private readonly TextBox         _txtObservaciones;
        private readonly Button          _btnGuardar;
        private readonly Button          _btnCancelar;

        public MovimientoProducto? MovProductoCreado { get; private set; }

        public FrmAgregarProductoMovimiento(int idMovimiento)
        {
            _idMovimiento = idMovimiento;

            Text            = "Agregar Producto al Camión";
            ClientSize      = new Size(460, 355);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.FromArgb(233, 238, 247);

            int lx = 20, cx = 185, y = 20, rh = 54;

            // Producto selector
            AddLabel("Producto:", lx, y);
            _lblProducto = new Label
            {
                Text     = "(ninguno seleccionado)",
                Location = new Point(cx, y + 4),
                Size     = new Size(240, 22),
                ForeColor = Color.Gray,
                Font     = new Font("Segoe UI", 9f)
            };
            Controls.Add(_lblProducto);
            y += 28;

            _btnSeleccionar = new Button
            {
                Text      = "Seleccionar Producto...",
                Location  = new Point(cx, y),
                Size      = new Size(240, 30),
                BackColor = Color.FromArgb(46, 90, 172),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnSeleccionar.FlatAppearance.BorderSize = 0;
            _btnSeleccionar.Click += BtnSeleccionar_Click;
            Controls.Add(_btnSeleccionar);
            y += rh;

            // Peso manifestado
            AddLabel("Peso manifestado (kg):", lx, y);
            _nudPesoManifestado = new NumericUpDown
            {
                Location      = new Point(cx, y),
                Size          = new Size(160, 28),
                DecimalPlaces = 2,
                Minimum       = 0,
                Maximum       = 999999,
                Increment     = 100
            };
            Controls.Add(_nudPesoManifestado);
            y += rh;

            // Bultos teóricos
            AddLabel("Bultos teóricos:", lx, y);
            _nudBultosTeóricos = new NumericUpDown
            {
                Location      = new Point(cx, y),
                Size          = new Size(160, 28),
                DecimalPlaces = 0,
                Minimum       = 0,
                Maximum       = 9999,
                Increment     = 1
            };
            Controls.Add(_nudBultosTeóricos);
            y += rh;

            // Observaciones
            AddLabel("Observaciones:", lx, y);
            _txtObservaciones = new TextBox
            {
                Location  = new Point(cx, y),
                Size      = new Size(240, 55),
                Multiline = true
            };
            Controls.Add(_txtObservaciones);
            y += 65;

            _btnCancelar = CreateButton("Cancelar", lx,  y, Color.Gray);
            _btnCancelar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancelar);

            _btnGuardar = CreateButton("Agregar", 320, y, Color.FromArgb(12, 92, 92));
            _btnGuardar.Click += BtnGuardar_Click;
            Controls.Add(_btnGuardar);
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

        private void BtnSeleccionar_Click(object sender, EventArgs e)
        {
            using var form = new GestionProductos();
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _idProductoSeleccionado  = form.IdProductoSeleccionado;
                _lblProducto.Text        = form.ProductoSeleccionado ?? "";
                _lblProducto.ForeColor   = Color.Black;
            }
        }

        private async void BtnGuardar_Click(object sender, EventArgs e)
        {
            if (_idProductoSeleccionado == 0)
            {
                MessageBox.Show("Seleccione un producto.", "Validación",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_nudPesoManifestado.Value <= 0)
            {
                MessageBox.Show("Ingrese el peso manifestado.", "Validación",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnGuardar.Enabled = false;
            try
            {
                var mp = new MovimientoProducto
                {
                    idMovimiento    = _idMovimiento,
                    idProducto      = _idProductoSeleccionado,
                    pesoManifestado = _nudPesoManifestado.Value,
                    bultosTeóricos  = (int)_nudBultosTeóricos.Value,
                    idEstado        = EstadosPesaje.Abierto,
                    observaciones   = _txtObservaciones.Text.Trim()
                };

                MovProductoCreado = await RepositorioMovimientoProducto.CrearAsync(mp);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agregar producto: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnGuardar.Enabled = true;
            }
        }
    }
}
