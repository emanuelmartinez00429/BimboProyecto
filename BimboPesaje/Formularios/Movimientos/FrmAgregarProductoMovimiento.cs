using CapaDatos.Modelados.Pesajes;
using CapaServicios;
using BimboPesaje.Formularios.Productos;
using CapaDatos.Repositorios.productos_movimientos;

namespace BimboPesaje.Formularios.Movimientos
{
    public class FrmAgregarProductoMovimiento : Form
    {
        private readonly int _idMovimiento;
        private int _idProductoSeleccionado = 0;

        private readonly Label _lblProducto;
        private readonly Button _btnSeleccionar;
        private readonly NumericUpDown _nudPesoManifestado;
        private readonly NumericUpDown _nudBultosTeóricos;
        private readonly TextBox _txtObservaciones;
        private readonly Button _btnGuardar;
        private readonly Button _btnCancelar;

        public MovimientoProducto? MovProductoCreado { get; private set; }

        public FrmAgregarProductoMovimiento(int idMovimiento)
        {
            _idMovimiento = idMovimiento;

            Text = "Agregar Producto al Camión";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(233, 238, 247);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Padding = new Padding(12);

            // ── TableLayoutPanel principal ──────────────────────────────
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };

            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int i = 0; i < 5; i++)
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // fila botones

            // ── Fila 0: label Producto ──────────────────────────────────
            table.Controls.Add(MakeLabel("Producto:"), 0, 0);

            _lblProducto = new Label
            {
                Text = "(ninguno seleccionado)",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(3, 8, 3, 2)
            };
            table.Controls.Add(_lblProducto, 1, 0);

            // ── Fila 1: botón Seleccionar ───────────────────────────────
            table.Controls.Add(new Label(), 0, 1); // celda vacía

            _btnSeleccionar = new Button
            {
                Text = "Seleccionar Producto...",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(200, 32),
                BackColor = Color.FromArgb(46, 90, 172),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(3, 2, 3, 10)
            };
            _btnSeleccionar.FlatAppearance.BorderSize = 0;
            _btnSeleccionar.Click += BtnSeleccionar_Click;
            table.Controls.Add(_btnSeleccionar, 1, 1);

            // ── Fila 2: Peso manifestado ────────────────────────────────
            table.Controls.Add(MakeLabel("Peso manifestado (kg):"), 0, 2);

            _nudPesoManifestado = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 999999,
                Increment = 100,
                Margin = new Padding(3, 6, 3, 6)
            };
            table.Controls.Add(_nudPesoManifestado, 1, 2);

            // ── Fila 3: Bultos teóricos ─────────────────────────────────
            table.Controls.Add(MakeLabel("Bultos teóricos:"), 0, 3);

            _nudBultosTeóricos = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                DecimalPlaces = 0,
                Minimum = 0,
                Maximum = 9999,
                Increment = 1,
                Margin = new Padding(3, 6, 3, 6)
            };
            table.Controls.Add(_nudBultosTeóricos, 1, 3);

            // ── Fila 4: Observaciones ───────────────────────────────────
            table.Controls.Add(MakeLabel("Observaciones:"), 0, 4);

            _txtObservaciones = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                MinimumSize = new Size(0, 60),
                Margin = new Padding(3, 6, 3, 6)
            };
            table.Controls.Add(_txtObservaciones, 1, 4);

            // ── Fila 5: Botones (ocupa ambas columnas) ──────────────────
            var panelBotones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Margin = new Padding(3, 10, 3, 3)
            };

            _btnGuardar = CreateButton("Agregar", Color.FromArgb(12, 92, 92));
            _btnGuardar.Click += BtnGuardar_Click;

            _btnCancelar = CreateButton("Cancelar", Color.Gray);
            _btnCancelar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            panelBotones.Controls.Add(_btnGuardar);
            panelBotones.Controls.Add(_btnCancelar);

            table.SetColumnSpan(panelBotones, 2);
            table.Controls.Add(panelBotones, 0, 5);

            Controls.Add(table);

            // Tamaño mínimo — el form crece con el contenido
            MinimumSize = new Size(400, 0);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        private static Label MakeLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 10f),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(3, 10, 8, 3)
        };

        private static Button CreateButton(string text, Color back)
        {
            var btn = new Button
            {
                Text = text,
                MinimumSize = new Size(115, 36),
                AutoSize = true,
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(6, 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void BtnSeleccionar_Click(object sender, EventArgs e)
        {
            using var form = new GestionProductos();
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _idProductoSeleccionado = form.IdProductoSeleccionado;
                _lblProducto.Text = form.ProductoSeleccionado ?? "";
                _lblProducto.ForeColor = Color.Black;
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
                    idMovimiento = _idMovimiento,
                    idProducto = _idProductoSeleccionado,
                    pesoManifestado = _nudPesoManifestado.Value,
                    bultosTeóricos = (int)_nudBultosTeóricos.Value,
                    idEstado = EstadosPesaje.Abierto,
                    observaciones = _txtObservaciones.Text.Trim()
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