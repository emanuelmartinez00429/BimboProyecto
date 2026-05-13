namespace BimboPesaje.Formularios.Movimientos
{
    partial class MovimientosyEntradas
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle7 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle8 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle9 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle10 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle11 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle12 = new DataGridViewCellStyle();
            pnMovimiento = new Panel();
            pnBotonesMovimiento = new Panel();
            btnGuardarMovimiento = new Button();
            btnQuitarMovimiento = new Button();
            btnEditarMoviento = new Button();
            btnPesar = new Button();
            btnAgregarMovimiento = new Button();
            label1 = new Label();
            dgvMateriaPrima = new DataGridView();
            pnVehiculo = new Panel();
            pnBotones = new Panel();
            button4 = new Button();
            btnEditar = new Button();
            btnDescargar = new Button();
            btnAgregar = new Button();
            label2 = new Label();
            dgvCamiones = new DataGridView();
            IdEntrada = new DataGridViewTextBoxColumn();
            PlacaCamion = new DataGridViewTextBoxColumn();
            pnDgv = new Panel();
            panel1 = new Panel();
            btnQuitarEntrada = new Button();
            btnGuardarEntrada = new Button();
            btnEditarEntrada = new Button();
            dgvEntradas = new DataGridView();
            label3 = new Label();
            CodigoInterno = new DataGridViewTextBoxColumn();
            Nombre = new DataGridViewTextBoxColumn();
            ProveedorProducto = new DataGridViewTextBoxColumn();
            Estado = new DataGridViewTextBoxColumn();
            pnMovimiento.SuspendLayout();
            pnBotonesMovimiento.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvMateriaPrima).BeginInit();
            pnVehiculo.SuspendLayout();
            pnBotones.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvCamiones).BeginInit();
            pnDgv.SuspendLayout();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvEntradas).BeginInit();
            SuspendLayout();
            // 
            // pnMovimiento
            // 
            pnMovimiento.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnMovimiento.BackColor = Color.FromArgb(233, 238, 247);
            pnMovimiento.Controls.Add(pnBotonesMovimiento);
            pnMovimiento.Controls.Add(label1);
            pnMovimiento.Controls.Add(dgvMateriaPrima);
            pnMovimiento.Location = new Point(424, 37);
            pnMovimiento.Name = "pnMovimiento";
            pnMovimiento.Padding = new Padding(3);
            pnMovimiento.Size = new Size(478, 315);
            pnMovimiento.TabIndex = 0;
            // 
            // pnBotonesMovimiento
            // 
            pnBotonesMovimiento.BackColor = Color.FromArgb(233, 238, 247);
            pnBotonesMovimiento.Controls.Add(btnGuardarMovimiento);
            pnBotonesMovimiento.Controls.Add(btnQuitarMovimiento);
            pnBotonesMovimiento.Controls.Add(btnEditarMoviento);
            pnBotonesMovimiento.Controls.Add(btnPesar);
            pnBotonesMovimiento.Controls.Add(btnAgregarMovimiento);
            pnBotonesMovimiento.Location = new Point(0, 275);
            pnBotonesMovimiento.Name = "pnBotonesMovimiento";
            pnBotonesMovimiento.Size = new Size(472, 40);
            pnBotonesMovimiento.TabIndex = 3;
            // 
            // btnGuardarMovimiento
            // 
            btnGuardarMovimiento.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnGuardarMovimiento.BackColor = Color.FromArgb(12, 92, 92);
            btnGuardarMovimiento.FlatAppearance.BorderSize = 0;
            btnGuardarMovimiento.FlatStyle = FlatStyle.Flat;
            btnGuardarMovimiento.Font = new Font("Itim", 9F);
            btnGuardarMovimiento.ForeColor = Color.White;
            btnGuardarMovimiento.Location = new Point(392, 0);
            btnGuardarMovimiento.Name = "btnGuardarMovimiento";
            btnGuardarMovimiento.Size = new Size(80, 40);
            btnGuardarMovimiento.TabIndex = 4;
            btnGuardarMovimiento.Text = "Guardar";
            btnGuardarMovimiento.UseVisualStyleBackColor = false;
            btnGuardarMovimiento.Click += btnGuardarMovimiento_Click;
            // 
            // btnQuitarMovimiento
            // 
            btnQuitarMovimiento.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnQuitarMovimiento.BackColor = Color.LightGray;
            btnQuitarMovimiento.FlatAppearance.BorderSize = 0;
            btnQuitarMovimiento.FlatStyle = FlatStyle.Flat;
            btnQuitarMovimiento.Font = new Font("Itim", 9F);
            btnQuitarMovimiento.Location = new Point(303, 0);
            btnQuitarMovimiento.Name = "btnQuitarMovimiento";
            btnQuitarMovimiento.Size = new Size(80, 40);
            btnQuitarMovimiento.TabIndex = 3;
            btnQuitarMovimiento.Text = "Quitar";
            btnQuitarMovimiento.UseVisualStyleBackColor = false;
            // 
            // btnEditarMoviento
            // 
            btnEditarMoviento.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            btnEditarMoviento.BackColor = Color.FromArgb(46, 90, 172);
            btnEditarMoviento.FlatAppearance.BorderSize = 0;
            btnEditarMoviento.FlatAppearance.MouseDownBackColor = Color.Gray;
            btnEditarMoviento.FlatStyle = FlatStyle.Flat;
            btnEditarMoviento.Font = new Font("Itim", 9F);
            btnEditarMoviento.ForeColor = Color.White;
            btnEditarMoviento.Location = new Point(172, 1);
            btnEditarMoviento.Name = "btnEditarMoviento";
            btnEditarMoviento.Size = new Size(125, 39);
            btnEditarMoviento.TabIndex = 2;
            btnEditarMoviento.Text = "Editar";
            btnEditarMoviento.UseVisualStyleBackColor = false;
            // 
            // btnPesar
            // 
            btnPesar.BackColor = Color.LightGray;
            btnPesar.FlatAppearance.BorderSize = 0;
            btnPesar.FlatStyle = FlatStyle.Flat;
            btnPesar.Font = new Font("Itim", 9F);
            btnPesar.Location = new Point(86, 0);
            btnPesar.Name = "btnPesar";
            btnPesar.Size = new Size(80, 40);
            btnPesar.TabIndex = 1;
            btnPesar.Text = "Pesar";
            btnPesar.UseVisualStyleBackColor = false;
            btnPesar.Click += btnPesar_Click;
            // 
            // btnAgregarMovimiento
            // 
            btnAgregarMovimiento.BackColor = Color.FromArgb(46, 90, 172);
            btnAgregarMovimiento.FlatAppearance.BorderSize = 0;
            btnAgregarMovimiento.FlatStyle = FlatStyle.Flat;
            btnAgregarMovimiento.Font = new Font("Itim", 9F);
            btnAgregarMovimiento.ForeColor = Color.White;
            btnAgregarMovimiento.Location = new Point(0, 0);
            btnAgregarMovimiento.Name = "btnAgregarMovimiento";
            btnAgregarMovimiento.Size = new Size(80, 40);
            btnAgregarMovimiento.TabIndex = 0;
            btnAgregarMovimiento.Text = "Agregar";
            btnAgregarMovimiento.UseVisualStyleBackColor = false;
            btnAgregarMovimiento.Click += btnAgregarMovimiento_Click;
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label1.AutoSize = true;
            label1.Font = new Font("Itim", 12F);
            label1.Location = new Point(17, 12);
            label1.Name = "label1";
            label1.Size = new Size(340, 24);
            label1.TabIndex = 2;
            label1.Text = "Tabla de Movimiento de Materia Prima\r\n";
            // 
            // dgvMateriaPrima
            // 
            dgvMateriaPrima.AllowUserToAddRows = false;
            dgvMateriaPrima.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = Color.White;
            dataGridViewCellStyle1.Font = new Font("Itim", 12F);
            dataGridViewCellStyle1.ForeColor = Color.Black;
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(90, 226, 138);
            dataGridViewCellStyle1.SelectionForeColor = Color.Black;
            dgvMateriaPrima.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvMateriaPrima.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvMateriaPrima.BackgroundColor = Color.White;
            dgvMateriaPrima.BorderStyle = BorderStyle.None;
            dgvMateriaPrima.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvMateriaPrima.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle2.Font = new Font("Itim", 12F);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.True;
            dgvMateriaPrima.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvMateriaPrima.ColumnHeadersHeight = 40;
            dgvMateriaPrima.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvMateriaPrima.Columns.AddRange(new DataGridViewColumn[] { CodigoInterno, Nombre, ProveedorProducto, Estado });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = SystemColors.Window;
            dataGridViewCellStyle3.Font = new Font("Itim", 12F);
            dataGridViewCellStyle3.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(90, 226, 138);
            dataGridViewCellStyle3.SelectionForeColor = Color.White;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dgvMateriaPrima.DefaultCellStyle = dataGridViewCellStyle3;
            dgvMateriaPrima.EnableHeadersVisualStyles = false;
            dgvMateriaPrima.GridColor = Color.White;
            dgvMateriaPrima.Location = new Point(17, 39);
            dgvMateriaPrima.MultiSelect = false;
            dgvMateriaPrima.Name = "dgvMateriaPrima";
            dgvMateriaPrima.ReadOnly = true;
            dgvMateriaPrima.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle4.Font = new Font("Itim", 12F);
            dataGridViewCellStyle4.ForeColor = Color.White;
            dataGridViewCellStyle4.SelectionBackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle4.SelectionForeColor = Color.White;
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.True;
            dgvMateriaPrima.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            dgvMateriaPrima.RowHeadersVisible = false;
            dgvMateriaPrima.RowHeadersWidth = 51;
            dgvMateriaPrima.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMateriaPrima.Size = new Size(444, 206);
            dgvMateriaPrima.TabIndex = 1;
            // 
            // pnVehiculo
            // 
            pnVehiculo.BackColor = Color.FromArgb(233, 238, 247);
            pnVehiculo.Controls.Add(pnBotones);
            pnVehiculo.Controls.Add(label2);
            pnVehiculo.Controls.Add(dgvCamiones);
            pnVehiculo.Location = new Point(12, 37);
            pnVehiculo.Name = "pnVehiculo";
            pnVehiculo.Padding = new Padding(3);
            pnVehiculo.Size = new Size(406, 315);
            pnVehiculo.TabIndex = 1;
            // 
            // pnBotones
            // 
            pnBotones.BackColor = Color.FromArgb(233, 238, 247);
            pnBotones.Controls.Add(button4);
            pnBotones.Controls.Add(btnEditar);
            pnBotones.Controls.Add(btnDescargar);
            pnBotones.Controls.Add(btnAgregar);
            pnBotones.Dock = DockStyle.Bottom;
            pnBotones.Location = new Point(3, 272);
            pnBotones.Name = "pnBotones";
            pnBotones.Size = new Size(400, 40);
            pnBotones.TabIndex = 2;
            // 
            // button4
            // 
            button4.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            button4.BackColor = Color.LightGray;
            button4.FlatAppearance.BorderSize = 0;
            button4.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 90, 172);
            button4.FlatStyle = FlatStyle.Flat;
            button4.Font = new Font("Segoe UI", 9F);
            button4.ForeColor = Color.Black;
            button4.Location = new Point(282, 0);
            button4.Margin = new Padding(6, 3, 3, 3);
            button4.Name = "button4";
            button4.Size = new Size(84, 40);
            button4.TabIndex = 3;
            button4.Text = "Quitar";
            button4.UseVisualStyleBackColor = false;
            // 
            // btnEditar
            // 
            btnEditar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnEditar.BackColor = Color.FromArgb(46, 90, 172);
            btnEditar.FlatAppearance.BorderSize = 0;
            btnEditar.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnEditar.FlatStyle = FlatStyle.Flat;
            btnEditar.Font = new Font("Itim", 9F);
            btnEditar.ForeColor = Color.White;
            btnEditar.Location = new Point(189, 0);
            btnEditar.Margin = new Padding(6, 3, 3, 3);
            btnEditar.Name = "btnEditar";
            btnEditar.Size = new Size(84, 40);
            btnEditar.TabIndex = 2;
            btnEditar.Text = "Editar";
            btnEditar.UseVisualStyleBackColor = false;
            // 
            // btnDescargar
            // 
            btnDescargar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnDescargar.BackColor = Color.LightGray;
            btnDescargar.FlatAppearance.BorderSize = 0;
            btnDescargar.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 90, 172);
            btnDescargar.FlatStyle = FlatStyle.Flat;
            btnDescargar.Font = new Font("Segoe UI", 9F);
            btnDescargar.ForeColor = Color.Black;
            btnDescargar.Location = new Point(96, -2);
            btnDescargar.Margin = new Padding(6, 3, 3, 3);
            btnDescargar.Name = "btnDescargar";
            btnDescargar.Size = new Size(88, 40);
            btnDescargar.TabIndex = 1;
            btnDescargar.Text = "Descargar";
            btnDescargar.UseVisualStyleBackColor = false;
            // 
            // btnAgregar
            // 
            btnAgregar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnAgregar.BackColor = Color.FromArgb(46, 90, 172);
            btnAgregar.FlatAppearance.BorderSize = 0;
            btnAgregar.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnAgregar.FlatStyle = FlatStyle.Flat;
            btnAgregar.Font = new Font("Itim", 9F);
            btnAgregar.ForeColor = Color.White;
            btnAgregar.Location = new Point(3, 0);
            btnAgregar.Name = "btnAgregar";
            btnAgregar.Size = new Size(84, 40);
            btnAgregar.TabIndex = 0;
            btnAgregar.Text = "Agregar";
            btnAgregar.UseVisualStyleBackColor = false;
            btnAgregar.Click += btnAgregar_Click;
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.AutoSize = true;
            label2.Font = new Font("Itim", 12F);
            label2.Location = new Point(17, 12);
            label2.Name = "label2";
            label2.Size = new Size(194, 24);
            label2.TabIndex = 1;
            label2.Text = "Camiones de Entrega";
            // 
            // dgvCamiones
            // 
            dgvCamiones.AllowUserToAddRows = false;
            dgvCamiones.AllowUserToDeleteRows = false;
            dataGridViewCellStyle5.BackColor = Color.White;
            dataGridViewCellStyle5.Font = new Font("Itim", 12F);
            dataGridViewCellStyle5.ForeColor = Color.Black;
            dataGridViewCellStyle5.SelectionBackColor = Color.FromArgb(90, 226, 138);
            dataGridViewCellStyle5.SelectionForeColor = Color.Black;
            dgvCamiones.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle5;
            dgvCamiones.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvCamiones.BackgroundColor = Color.White;
            dgvCamiones.BorderStyle = BorderStyle.None;
            dgvCamiones.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvCamiones.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle6.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle6.BackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle6.Font = new Font("Itim", 12F);
            dataGridViewCellStyle6.ForeColor = Color.White;
            dataGridViewCellStyle6.SelectionBackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle6.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = DataGridViewTriState.True;
            dgvCamiones.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle6;
            dgvCamiones.ColumnHeadersHeight = 35;
            dgvCamiones.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvCamiones.Columns.AddRange(new DataGridViewColumn[] { IdEntrada, PlacaCamion });
            dataGridViewCellStyle7.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle7.BackColor = SystemColors.Window;
            dataGridViewCellStyle7.Font = new Font("Itim", 12F);
            dataGridViewCellStyle7.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle7.SelectionBackColor = Color.FromArgb(90, 226, 138);
            dataGridViewCellStyle7.SelectionForeColor = Color.Black;
            dataGridViewCellStyle7.WrapMode = DataGridViewTriState.False;
            dgvCamiones.DefaultCellStyle = dataGridViewCellStyle7;
            dgvCamiones.EnableHeadersVisualStyles = false;
            dgvCamiones.GridColor = Color.White;
            dgvCamiones.Location = new Point(6, 39);
            dgvCamiones.MultiSelect = false;
            dgvCamiones.Name = "dgvCamiones";
            dgvCamiones.ReadOnly = true;
            dgvCamiones.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle8.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle8.Font = new Font("Itim", 12F);
            dataGridViewCellStyle8.ForeColor = Color.White;
            dataGridViewCellStyle8.SelectionBackColor = Color.FromArgb(90, 226, 138);
            dataGridViewCellStyle8.SelectionForeColor = Color.Black;
            dataGridViewCellStyle8.WrapMode = DataGridViewTriState.True;
            dgvCamiones.RowHeadersDefaultCellStyle = dataGridViewCellStyle8;
            dgvCamiones.RowHeadersWidth = 51;
            dgvCamiones.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCamiones.Size = new Size(390, 206);
            dgvCamiones.TabIndex = 0;
            dgvCamiones.SelectionChanged += dgvCamiones_SelectionChanged;
            // 
            // IdEntrada
            // 
            IdEntrada.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            IdEntrada.DataPropertyName = "IdEntrada";
            IdEntrada.HeaderText = "Id";
            IdEntrada.MinimumWidth = 6;
            IdEntrada.Name = "IdEntrada";
            IdEntrada.ReadOnly = true;
            IdEntrada.Width = 55;
            // 
            // PlacaCamion
            // 
            PlacaCamion.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            PlacaCamion.DataPropertyName = "PlacaCamion";
            PlacaCamion.HeaderText = "Placa";
            PlacaCamion.MinimumWidth = 6;
            PlacaCamion.Name = "PlacaCamion";
            PlacaCamion.ReadOnly = true;
            // 
            // pnDgv
            // 
            pnDgv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnDgv.BackColor = Color.FromArgb(233, 238, 247);
            pnDgv.Controls.Add(panel1);
            pnDgv.Controls.Add(dgvEntradas);
            pnDgv.Controls.Add(label3);
            pnDgv.Location = new Point(12, 367);
            pnDgv.Name = "pnDgv";
            pnDgv.Size = new Size(890, 347);
            pnDgv.TabIndex = 1;
            // 
            // panel1
            // 
            panel1.BackColor = Color.FromArgb(233, 238, 247);
            panel1.Controls.Add(btnQuitarEntrada);
            panel1.Controls.Add(btnGuardarEntrada);
            panel1.Controls.Add(btnEditarEntrada);
            panel1.Dock = DockStyle.Bottom;
            panel1.Location = new Point(0, 307);
            panel1.Name = "panel1";
            panel1.Size = new Size(890, 40);
            panel1.TabIndex = 4;
            // 
            // btnQuitarEntrada
            // 
            btnQuitarEntrada.BackColor = Color.LightGray;
            btnQuitarEntrada.Dock = DockStyle.Left;
            btnQuitarEntrada.FlatAppearance.BorderSize = 0;
            btnQuitarEntrada.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 90, 172);
            btnQuitarEntrada.FlatStyle = FlatStyle.Flat;
            btnQuitarEntrada.Font = new Font("Segoe UI", 9F);
            btnQuitarEntrada.ForeColor = Color.Black;
            btnQuitarEntrada.Location = new Point(104, 0);
            btnQuitarEntrada.Margin = new Padding(6, 3, 3, 3);
            btnQuitarEntrada.Name = "btnQuitarEntrada";
            btnQuitarEntrada.Size = new Size(114, 40);
            btnQuitarEntrada.TabIndex = 3;
            btnQuitarEntrada.Text = "Quitar";
            btnQuitarEntrada.UseVisualStyleBackColor = false;
            // 
            // btnGuardarEntrada
            // 
            btnGuardarEntrada.BackColor = Color.FromArgb(12, 92, 92);
            btnGuardarEntrada.Dock = DockStyle.Right;
            btnGuardarEntrada.FlatAppearance.BorderSize = 0;
            btnGuardarEntrada.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnGuardarEntrada.FlatStyle = FlatStyle.Flat;
            btnGuardarEntrada.Font = new Font("Itim", 9F);
            btnGuardarEntrada.ForeColor = Color.White;
            btnGuardarEntrada.Location = new Point(755, 0);
            btnGuardarEntrada.Margin = new Padding(6, 3, 3, 3);
            btnGuardarEntrada.Name = "btnGuardarEntrada";
            btnGuardarEntrada.Size = new Size(135, 40);
            btnGuardarEntrada.TabIndex = 2;
            btnGuardarEntrada.Text = "Guardar Entrada";
            btnGuardarEntrada.UseVisualStyleBackColor = false;
            btnGuardarEntrada.Click += btnGuardarEntrada_Click;
            // 
            // btnEditarEntrada
            // 
            btnEditarEntrada.BackColor = Color.FromArgb(46, 90, 172);
            btnEditarEntrada.Dock = DockStyle.Left;
            btnEditarEntrada.FlatAppearance.BorderSize = 0;
            btnEditarEntrada.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnEditarEntrada.FlatStyle = FlatStyle.Flat;
            btnEditarEntrada.Font = new Font("Itim", 9F);
            btnEditarEntrada.ForeColor = Color.White;
            btnEditarEntrada.Location = new Point(0, 0);
            btnEditarEntrada.Name = "btnEditarEntrada";
            btnEditarEntrada.Size = new Size(104, 40);
            btnEditarEntrada.TabIndex = 0;
            btnEditarEntrada.Text = "Editar";
            btnEditarEntrada.UseVisualStyleBackColor = false;
            btnEditarEntrada.Click += btnEditarEntrada_Click;
            // 
            // dgvEntradas
            // 
            dgvEntradas.AllowUserToAddRows = false;
            dgvEntradas.AllowUserToDeleteRows = false;
            dataGridViewCellStyle9.BackColor = Color.White;
            dataGridViewCellStyle9.Font = new Font("Itim", 12F);
            dataGridViewCellStyle9.ForeColor = Color.Black;
            dataGridViewCellStyle9.SelectionBackColor = Color.FromArgb(90, 226, 138);
            dataGridViewCellStyle9.SelectionForeColor = Color.Black;
            dgvEntradas.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle9;
            dgvEntradas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvEntradas.BackgroundColor = Color.White;
            dgvEntradas.BorderStyle = BorderStyle.None;
            dgvEntradas.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvEntradas.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle10.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle10.BackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle10.Font = new Font("Itim", 12F);
            dataGridViewCellStyle10.ForeColor = Color.White;
            dataGridViewCellStyle10.SelectionBackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle10.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle10.WrapMode = DataGridViewTriState.True;
            dgvEntradas.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle10;
            dgvEntradas.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle11.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle11.BackColor = SystemColors.Window;
            dataGridViewCellStyle11.Font = new Font("Itim", 12F);
            dataGridViewCellStyle11.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle11.SelectionBackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle11.SelectionForeColor = Color.White;
            dataGridViewCellStyle11.WrapMode = DataGridViewTriState.False;
            dgvEntradas.DefaultCellStyle = dataGridViewCellStyle11;
            dgvEntradas.EnableHeadersVisualStyles = false;
            dgvEntradas.GridColor = Color.FromArgb(233, 238, 247);
            dgvEntradas.Location = new Point(17, 45);
            dgvEntradas.MultiSelect = false;
            dgvEntradas.Name = "dgvEntradas";
            dgvEntradas.ReadOnly = true;
            dgvEntradas.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle12.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle12.BackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle12.Font = new Font("Itim", 12F);
            dataGridViewCellStyle12.ForeColor = Color.White;
            dataGridViewCellStyle12.SelectionBackColor = Color.FromArgb(79, 125, 209);
            dataGridViewCellStyle12.SelectionForeColor = Color.White;
            dataGridViewCellStyle12.WrapMode = DataGridViewTriState.True;
            dgvEntradas.RowHeadersDefaultCellStyle = dataGridViewCellStyle12;
            dgvEntradas.RowHeadersVisible = false;
            dgvEntradas.RowHeadersWidth = 51;
            dgvEntradas.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvEntradas.Size = new Size(856, 236);
            dgvEntradas.TabIndex = 3;
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label3.AutoSize = true;
            label3.Font = new Font("Itim", 12F);
            label3.Location = new Point(17, 18);
            label3.Name = "label3";
            label3.Size = new Size(241, 24);
            label3.TabIndex = 2;
            label3.Text = "Entradas de materia prima";
            // 
            // CodigoInterno
            // 
            CodigoInterno.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            CodigoInterno.DataPropertyName = "CodigoInternoProducto";
            CodigoInterno.HeaderText = "Código";
            CodigoInterno.MinimumWidth = 6;
            CodigoInterno.Name = "CodigoInterno";
            CodigoInterno.ReadOnly = true;
            CodigoInterno.Width = 96;
            // 
            // Nombre
            // 
            Nombre.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            Nombre.DataPropertyName = "NombreProducto";
            Nombre.HeaderText = "Producto";
            Nombre.MinimumWidth = 6;
            Nombre.Name = "Nombre";
            Nombre.ReadOnly = true;
            // 
            // ProveedorProducto
            // 
            ProveedorProducto.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ProveedorProducto.DataPropertyName = "Proveedor_Producto";
            ProveedorProducto.HeaderText = "Proveedor";
            ProveedorProducto.MinimumWidth = 6;
            ProveedorProducto.Name = "ProveedorProducto";
            ProveedorProducto.ReadOnly = true;
            // 
            // Estado
            // 
            Estado.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            Estado.DataPropertyName = "EstadoProceso";
            Estado.HeaderText = "Estado";
            Estado.MinimumWidth = 6;
            Estado.Name = "Estado";
            Estado.ReadOnly = true;
            Estado.Width = 95;
            // 
            // MovimientosyEntradas
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(914, 726);
            Controls.Add(pnDgv);
            Controls.Add(pnVehiculo);
            Controls.Add(pnMovimiento);
            Name = "MovimientosyEntradas";
            Text = "MovimientosyEntradas";
            Load += MovimientosyEntradas_Load;
            pnMovimiento.ResumeLayout(false);
            pnMovimiento.PerformLayout();
            pnBotonesMovimiento.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvMateriaPrima).EndInit();
            pnVehiculo.ResumeLayout(false);
            pnVehiculo.PerformLayout();
            pnBotones.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvCamiones).EndInit();
            pnDgv.ResumeLayout(false);
            pnDgv.PerformLayout();
            panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvEntradas).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnMovimiento;
        private Panel pnVehiculo;
        private DataGridView dgvCamiones;
        private Panel pnDgv;
        private Label label1;
        private DataGridView dgvMateriaPrima;
        private Label label2;
        private Panel pnBotones;
        private Button button4;
        private Button btnEditar;
        private Button btnDescargar;
        private Button btnAgregar;
        private Panel pnBotonesMovimiento;
        private Button btnAgregarMovimiento;
        private Button btnGuardarMovimiento;
        private Button btnQuitarMovimiento;
        private Button btnEditarMoviento;
        private Button btnPesar;
        private DataGridView dgvEntradas;
        private Label label3;
        private Panel panel1;
        private Button btnQuitarEntrada;
        private Button btnGuardarEntrada;
        private Button btnEditarEntrada;
        private DataGridViewTextBoxColumn IdEntrada;
        private DataGridViewTextBoxColumn PlacaCamion;
        private DataGridViewTextBoxColumn CodigoInterno;
        private DataGridViewTextBoxColumn Nombre;
        private DataGridViewTextBoxColumn ProveedorProducto;
        private DataGridViewTextBoxColumn Estado;
    }
}