namespace BimboPesaje.Formularios.Productos
{
    partial class GestionFabricantes
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
            pnContenedorHijo = new Panel();
            pnDgv = new Panel();
            dgvFabricante = new DataGridView();
            IdFabricante = new DataGridViewTextBoxColumn();
            RtnFabricante = new DataGridViewTextBoxColumn();
            NombreFabricante = new DataGridViewTextBoxColumn();
            TelefonoFabricante = new DataGridViewTextBoxColumn();
            CorreoFabricante = new DataGridViewTextBoxColumn();
            DireccionFabricante = new DataGridViewTextBoxColumn();
            EstadoFabricante = new DataGridViewTextBoxColumn();
            pnFiltros = new Panel();
            rbTodos = new RadioButton();
            rbDeshabilitados = new RadioButton();
            label2 = new Label();
            cmbProveedor = new ComboBox();
            label1 = new Label();
            cmbPaises = new ComboBox();
            rbHabilitados = new RadioButton();
            tableLayoutPanel1 = new TableLayoutPanel();
            btnNuevo = new Button();
            btnEditar = new Button();
            btnSalir = new Button();
            pnFiltro = new Panel();
            btnLimpiar = new Button();
            pnBuscar = new Panel();
            txtBusqueda = new TextBox();
            btnLupa = new Button();
            pnContenedorHijo.SuspendLayout();
            pnDgv.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFabricante).BeginInit();
            pnFiltros.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            pnFiltro.SuspendLayout();
            pnBuscar.SuspendLayout();
            SuspendLayout();
            // 
            // pnContenedorHijo
            // 
            pnContenedorHijo.BackColor = Color.FromArgb(220, 230, 241);
            pnContenedorHijo.Controls.Add(pnDgv);
            pnContenedorHijo.Controls.Add(pnFiltros);
            pnContenedorHijo.Controls.Add(tableLayoutPanel1);
            pnContenedorHijo.Controls.Add(pnFiltro);
            pnContenedorHijo.Controls.Add(pnBuscar);
            pnContenedorHijo.Dock = DockStyle.Fill;
            pnContenedorHijo.Location = new Point(0, 0);
            pnContenedorHijo.Name = "pnContenedorHijo";
            pnContenedorHijo.Size = new Size(914, 726);
            pnContenedorHijo.TabIndex = 0;
            // 
            // pnDgv
            // 
            pnDgv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnDgv.Controls.Add(dgvFabricante);
            pnDgv.Location = new Point(12, 175);
            pnDgv.Name = "pnDgv";
            pnDgv.Size = new Size(890, 399);
            pnDgv.TabIndex = 14;
            // 
            // dgvFabricante
            // 
            dgvFabricante.AllowUserToAddRows = false;
            dgvFabricante.AllowUserToDeleteRows = false;
            dgvFabricante.AllowUserToResizeColumns = false;
            dgvFabricante.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(220, 230, 241);
            dataGridViewCellStyle1.Font = new Font("Itim", 12F);
            dataGridViewCellStyle1.ForeColor = Color.Black;
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(65, 92, 107);
            dataGridViewCellStyle1.SelectionForeColor = Color.White;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvFabricante.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvFabricante.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
            dgvFabricante.BackgroundColor = Color.White;
            dgvFabricante.BorderStyle = BorderStyle.None;
            dgvFabricante.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvFabricante.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle2.Font = new Font("Itim", 12F);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dgvFabricante.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvFabricante.ColumnHeadersHeight = 36;
            dgvFabricante.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvFabricante.Columns.AddRange(new DataGridViewColumn[] { IdFabricante, RtnFabricante, NombreFabricante, TelefonoFabricante, CorreoFabricante, DireccionFabricante, EstadoFabricante });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = Color.White;
            dataGridViewCellStyle3.Font = new Font("Itim", 12F);
            dataGridViewCellStyle3.ForeColor = Color.Black;
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(65, 92, 107);
            dataGridViewCellStyle3.SelectionForeColor = Color.White;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.True;
            dgvFabricante.DefaultCellStyle = dataGridViewCellStyle3;
            dgvFabricante.Dock = DockStyle.Fill;
            dgvFabricante.EnableHeadersVisualStyles = false;
            dgvFabricante.GridColor = Color.White;
            dgvFabricante.Location = new Point(0, 0);
            dgvFabricante.Margin = new Padding(4);
            dgvFabricante.Name = "dgvFabricante";
            dgvFabricante.ReadOnly = true;
            dgvFabricante.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle4.Font = new Font("Itim", 12F);
            dataGridViewCellStyle4.ForeColor = Color.Transparent;
            dataGridViewCellStyle4.SelectionBackColor = Color.FromArgb(65, 92, 107);
            dataGridViewCellStyle4.SelectionForeColor = Color.Transparent;
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.False;
            dgvFabricante.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            dgvFabricante.RowHeadersWidth = 30;
            dgvFabricante.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgvFabricante.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFabricante.Size = new Size(890, 399);
            dgvFabricante.TabIndex = 2;
            dgvFabricante.UseWaitCursor = true;
            dgvFabricante.DoubleClick += dgvFabricante_DoubleClick;
            // 
            // IdFabricante
            // 
            IdFabricante.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            IdFabricante.DataPropertyName = "IdFabricante";
            IdFabricante.HeaderText = "Id";
            IdFabricante.MinimumWidth = 6;
            IdFabricante.Name = "IdFabricante";
            IdFabricante.ReadOnly = true;
            IdFabricante.Width = 55;
            // 
            // RtnFabricante
            // 
            RtnFabricante.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            RtnFabricante.DataPropertyName = "RtnFabricante";
            RtnFabricante.HeaderText = "RTN";
            RtnFabricante.MinimumWidth = 6;
            RtnFabricante.Name = "RtnFabricante";
            RtnFabricante.ReadOnly = true;
            RtnFabricante.Width = 74;
            // 
            // NombreFabricante
            // 
            NombreFabricante.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            NombreFabricante.DataPropertyName = "NombreFabricante";
            NombreFabricante.HeaderText = "Fabricante";
            NombreFabricante.MinimumWidth = 6;
            NombreFabricante.Name = "NombreFabricante";
            NombreFabricante.ReadOnly = true;
            NombreFabricante.Width = 126;
            // 
            // TelefonoFabricante
            // 
            TelefonoFabricante.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            TelefonoFabricante.DataPropertyName = "TelefonoFabricante";
            TelefonoFabricante.HeaderText = "Teléfono";
            TelefonoFabricante.MinimumWidth = 6;
            TelefonoFabricante.Name = "TelefonoFabricante";
            TelefonoFabricante.ReadOnly = true;
            TelefonoFabricante.Width = 111;
            // 
            // CorreoFabricante
            // 
            CorreoFabricante.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            CorreoFabricante.DataPropertyName = "CorreoFabricante";
            CorreoFabricante.HeaderText = "Correo";
            CorreoFabricante.MinimumWidth = 6;
            CorreoFabricante.Name = "CorreoFabricante";
            CorreoFabricante.ReadOnly = true;
            CorreoFabricante.Width = 96;
            // 
            // DireccionFabricante
            // 
            DireccionFabricante.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            DireccionFabricante.DataPropertyName = "DireccionFabricante";
            DireccionFabricante.HeaderText = "Dirección";
            DireccionFabricante.MinimumWidth = 6;
            DireccionFabricante.Name = "DireccionFabricante";
            DireccionFabricante.ReadOnly = true;
            DireccionFabricante.Width = 117;
            // 
            // EstadoFabricante
            // 
            EstadoFabricante.DataPropertyName = "EstadoFabricante";
            EstadoFabricante.HeaderText = "Estado";
            EstadoFabricante.MinimumWidth = 6;
            EstadoFabricante.Name = "EstadoFabricante";
            EstadoFabricante.ReadOnly = true;
            EstadoFabricante.Width = 125;
            // 
            // pnFiltros
            // 
            pnFiltros.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnFiltros.BackColor = Color.White;
            pnFiltros.Controls.Add(rbTodos);
            pnFiltros.Controls.Add(rbDeshabilitados);
            pnFiltros.Controls.Add(label2);
            pnFiltros.Controls.Add(cmbProveedor);
            pnFiltros.Controls.Add(label1);
            pnFiltros.Controls.Add(cmbPaises);
            pnFiltros.Controls.Add(rbHabilitados);
            pnFiltros.Location = new Point(23, 93);
            pnFiltros.Name = "pnFiltros";
            pnFiltros.Size = new Size(879, 39);
            pnFiltros.TabIndex = 13;
            // 
            // rbTodos
            // 
            rbTodos.AutoSize = true;
            rbTodos.Dock = DockStyle.Left;
            rbTodos.Font = new Font("Itim", 11F);
            rbTodos.Location = new Point(273, 0);
            rbTodos.Name = "rbTodos";
            rbTodos.Size = new Size(78, 39);
            rbTodos.TabIndex = 25;
            rbTodos.Text = "Todos";
            rbTodos.UseVisualStyleBackColor = true;
            rbTodos.CheckedChanged += rbTodos_CheckedChanged;
            // 
            // rbDeshabilitados
            // 
            rbDeshabilitados.AutoSize = true;
            rbDeshabilitados.Dock = DockStyle.Left;
            rbDeshabilitados.Font = new Font("Itim", 11F);
            rbDeshabilitados.Location = new Point(124, 0);
            rbDeshabilitados.Name = "rbDeshabilitados";
            rbDeshabilitados.Size = new Size(149, 39);
            rbDeshabilitados.TabIndex = 24;
            rbDeshabilitados.Text = "Deshabilitados";
            rbDeshabilitados.UseVisualStyleBackColor = true;
            rbDeshabilitados.CheckedChanged += rbDeshabilitados_CheckedChanged_1;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Dock = DockStyle.Right;
            label2.Font = new Font("Itim", 11F);
            label2.Location = new Point(393, 0);
            label2.Margin = new Padding(3);
            label2.Name = "label2";
            label2.Padding = new Padding(3, 7, 3, 3);
            label2.Size = new Size(100, 33);
            label2.TabIndex = 22;
            label2.Text = "Proveedor";
            // 
            // cmbProveedor
            // 
            cmbProveedor.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbProveedor.Dock = DockStyle.Right;
            cmbProveedor.Font = new Font("Itim", 11F);
            cmbProveedor.FormattingEnabled = true;
            cmbProveedor.Location = new Point(493, 0);
            cmbProveedor.Margin = new Padding(3, 6, 3, 3);
            cmbProveedor.Name = "cmbProveedor";
            cmbProveedor.Size = new Size(186, 30);
            cmbProveedor.TabIndex = 21;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Dock = DockStyle.Right;
            label1.Font = new Font("Itim", 11F);
            label1.Location = new Point(679, 0);
            label1.Margin = new Padding(3);
            label1.Name = "label1";
            label1.Padding = new Padding(3, 7, 3, 3);
            label1.Size = new Size(49, 33);
            label1.TabIndex = 20;
            label1.Text = "Pais";
            // 
            // cmbPaises
            // 
            cmbPaises.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbPaises.Dock = DockStyle.Right;
            cmbPaises.Font = new Font("Itim", 11F);
            cmbPaises.FormattingEnabled = true;
            cmbPaises.Location = new Point(728, 0);
            cmbPaises.Margin = new Padding(3, 3, 3, 0);
            cmbPaises.Name = "cmbPaises";
            cmbPaises.Size = new Size(151, 30);
            cmbPaises.TabIndex = 19;
            // 
            // rbHabilitados
            // 
            rbHabilitados.AutoSize = true;
            rbHabilitados.Checked = true;
            rbHabilitados.Dock = DockStyle.Left;
            rbHabilitados.Font = new Font("Itim", 11F);
            rbHabilitados.Location = new Point(0, 0);
            rbHabilitados.Name = "rbHabilitados";
            rbHabilitados.Size = new Size(124, 39);
            rbHabilitados.TabIndex = 9;
            rbHabilitados.TabStop = true;
            rbHabilitados.Text = "Habilitados";
            rbHabilitados.UseVisualStyleBackColor = true;
            rbHabilitados.CheckedChanged += rbHabilitados_CheckedChanged;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(btnNuevo, 0, 0);
            tableLayoutPanel1.Controls.Add(btnEditar, 1, 0);
            tableLayoutPanel1.Controls.Add(btnSalir, 0, 1);
            tableLayoutPanel1.Location = new Point(12, 589);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(273, 125);
            tableLayoutPanel1.TabIndex = 11;
            // 
            // btnNuevo
            // 
            btnNuevo.BackColor = Color.FromArgb(12, 92, 92);
            btnNuevo.Dock = DockStyle.Fill;
            btnNuevo.FlatAppearance.BorderSize = 0;
            btnNuevo.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnNuevo.FlatStyle = FlatStyle.Flat;
            btnNuevo.Font = new Font("Itim", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnNuevo.ForeColor = Color.White;
            btnNuevo.Location = new Point(3, 3);
            btnNuevo.Name = "btnNuevo";
            btnNuevo.Size = new Size(130, 56);
            btnNuevo.TabIndex = 0;
            btnNuevo.Text = "Nuevo Fabricante";
            btnNuevo.UseVisualStyleBackColor = false;
            // 
            // btnEditar
            // 
            btnEditar.BackColor = Color.FromArgb(46, 90, 172);
            btnEditar.FlatAppearance.BorderSize = 0;
            btnEditar.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnEditar.FlatStyle = FlatStyle.Flat;
            btnEditar.Font = new Font("Itim", 12F);
            btnEditar.ForeColor = Color.White;
            btnEditar.Location = new Point(139, 3);
            btnEditar.Name = "btnEditar";
            btnEditar.Size = new Size(131, 56);
            btnEditar.TabIndex = 1;
            btnEditar.Text = "Editar";
            btnEditar.UseVisualStyleBackColor = false;
            // 
            // btnSalir
            // 
            btnSalir.BackColor = Color.Gray;
            btnSalir.Dock = DockStyle.Fill;
            btnSalir.FlatAppearance.BorderSize = 0;
            btnSalir.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnSalir.FlatStyle = FlatStyle.Flat;
            btnSalir.Font = new Font("Itim", 12F);
            btnSalir.ForeColor = Color.White;
            btnSalir.Location = new Point(3, 65);
            btnSalir.Name = "btnSalir";
            btnSalir.Size = new Size(130, 57);
            btnSalir.TabIndex = 2;
            btnSalir.Text = "Salir";
            btnSalir.UseVisualStyleBackColor = false;
            btnSalir.Click += btnSalir_Click;
            // 
            // pnFiltro
            // 
            pnFiltro.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnFiltro.BackColor = Color.White;
            pnFiltro.Controls.Add(btnLimpiar);
            pnFiltro.Location = new Point(685, 27);
            pnFiltro.Name = "pnFiltro";
            pnFiltro.Size = new Size(217, 34);
            pnFiltro.TabIndex = 9;
            // 
            // btnLimpiar
            // 
            btnLimpiar.BackColor = Color.FromArgb(31, 60, 136);
            btnLimpiar.Dock = DockStyle.Right;
            btnLimpiar.FlatAppearance.BorderSize = 0;
            btnLimpiar.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnLimpiar.FlatStyle = FlatStyle.Flat;
            btnLimpiar.Font = new Font("Itim", 12F);
            btnLimpiar.ForeColor = Color.White;
            btnLimpiar.Image = Properties.Resources.escoba;
            btnLimpiar.ImageAlign = ContentAlignment.MiddleLeft;
            btnLimpiar.Location = new Point(0, 0);
            btnLimpiar.Name = "btnLimpiar";
            btnLimpiar.Size = new Size(217, 34);
            btnLimpiar.TabIndex = 0;
            btnLimpiar.Text = "Limpiar Filtros";
            btnLimpiar.UseVisualStyleBackColor = false;
            // 
            // pnBuscar
            // 
            pnBuscar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnBuscar.BackColor = Color.White;
            pnBuscar.Controls.Add(txtBusqueda);
            pnBuscar.Controls.Add(btnLupa);
            pnBuscar.Font = new Font("Itim", 12F);
            pnBuscar.ForeColor = Color.Transparent;
            pnBuscar.Location = new Point(23, 27);
            pnBuscar.Name = "pnBuscar";
            pnBuscar.Size = new Size(516, 32);
            pnBuscar.TabIndex = 8;
            // 
            // txtBusqueda
            // 
            txtBusqueda.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtBusqueda.BackColor = Color.White;
            txtBusqueda.BorderStyle = BorderStyle.None;
            txtBusqueda.Font = new Font("Itim", 14F);
            txtBusqueda.ForeColor = Color.Black;
            txtBusqueda.Location = new Point(40, 0);
            txtBusqueda.Name = "txtBusqueda";
            txtBusqueda.PlaceholderText = "Buscar fabricante...";
            txtBusqueda.Size = new Size(473, 28);
            txtBusqueda.TabIndex = 3;
            // 
            // btnLupa
            // 
            btnLupa.BackColor = Color.FromArgb(31, 60, 136);
            btnLupa.Dock = DockStyle.Left;
            btnLupa.FlatAppearance.BorderSize = 0;
            btnLupa.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnLupa.FlatStyle = FlatStyle.Flat;
            btnLupa.ForeColor = Color.Transparent;
            btnLupa.Image = Properties.Resources.buscar_24;
            btnLupa.Location = new Point(0, 0);
            btnLupa.Margin = new Padding(3, 4, 3, 3);
            btnLupa.Name = "btnLupa";
            btnLupa.Size = new Size(40, 32);
            btnLupa.TabIndex = 2;
            btnLupa.UseVisualStyleBackColor = false;
            // 
            // GestionFabricantes
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(914, 726);
            Controls.Add(pnContenedorHijo);
            Name = "GestionFabricantes";
            Text = "GestionFabricantes";
            Load += GestionFabricantes_Load;
            pnContenedorHijo.ResumeLayout(false);
            pnDgv.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvFabricante).EndInit();
            pnFiltros.ResumeLayout(false);
            pnFiltros.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            pnFiltro.ResumeLayout(false);
            pnBuscar.ResumeLayout(false);
            pnBuscar.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnContenedorHijo;
        private Panel pnBuscar;
        private TextBox txtBusqueda;
        private Button btnLupa;
        private Panel pnFiltro;
        private Button btnLimpiar;
        private TableLayoutPanel tableLayoutPanel1;
        private Button btnNuevo;
        private Button btnEditar;
        private Button btnSalir;
        private Panel pnFiltros;
        private Label label2;
        private Label label1;
        private ComboBox cmbPaises;
        private ComboBox cmbProveedor;
        private Panel pnDgv;
        private DataGridView dgvFabricante;
        private DataGridViewTextBoxColumn IdFabricante;
        private DataGridViewTextBoxColumn RtnFabricante;
        private DataGridViewTextBoxColumn NombreFabricante;
        private DataGridViewTextBoxColumn TelefonoFabricante;
        private DataGridViewTextBoxColumn CorreoFabricante;
        private DataGridViewTextBoxColumn DireccionFabricante;
        private DataGridViewTextBoxColumn EstadoFabricante;
        private RadioButton rbTodos;
        private RadioButton rbDeshabilitados;
        private RadioButton rbHabilitados;
    }
}