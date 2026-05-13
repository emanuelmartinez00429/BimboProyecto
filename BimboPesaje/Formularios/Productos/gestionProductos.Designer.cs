namespace BimboPesaje.Formularios.Productos
{
    partial class GestionProductos
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
            pnBuscar = new Panel();
            txtBusqueda = new TextBox();
            button1 = new Button();
            pnFiltro = new Panel();
            btnLimpiar = new Button();
            pnFiltros = new Panel();
            rbHabilitados = new RadioButton();
            rbDeshabilitados = new RadioButton();
            rbTodos = new RadioButton();
            label5 = new Label();
            cmbMarca = new ComboBox();
            label6 = new Label();
            cmbPais = new ComboBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            btnNuevo = new Button();
            btnEditar = new Button();
            btnSalir = new Button();
            pnDgv = new Panel();
            dgvProductos = new DataGridView();
            Codigo = new DataGridViewTextBoxColumn();
            Producto = new DataGridViewTextBoxColumn();
            Fabricante = new DataGridViewTextBoxColumn();
            Pais = new DataGridViewTextBoxColumn();
            Contenido = new DataGridViewTextBoxColumn();
            Presentacion = new DataGridViewTextBoxColumn();
            Categoria = new DataGridViewTextBoxColumn();
            EstadoProducto = new DataGridViewCheckBoxColumn();
            CodigoInterno = new DataGridViewTextBoxColumn();
            pnContenedorHijo.SuspendLayout();
            pnBuscar.SuspendLayout();
            pnFiltro.SuspendLayout();
            pnFiltros.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            pnDgv.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvProductos).BeginInit();
            SuspendLayout();
            // 
            // pnContenedorHijo
            // 
            pnContenedorHijo.BackColor = Color.FromArgb(220, 230, 241);
            pnContenedorHijo.Controls.Add(pnBuscar);
            pnContenedorHijo.Controls.Add(pnFiltro);
            pnContenedorHijo.Controls.Add(pnFiltros);
            pnContenedorHijo.Controls.Add(tableLayoutPanel1);
            pnContenedorHijo.Controls.Add(pnDgv);
            pnContenedorHijo.Dock = DockStyle.Fill;
            pnContenedorHijo.Location = new Point(0, 0);
            pnContenedorHijo.Name = "pnContenedorHijo";
            pnContenedorHijo.Size = new Size(914, 726);
            pnContenedorHijo.TabIndex = 0;
            // 
            // pnBuscar
            // 
            pnBuscar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnBuscar.BackColor = Color.White;
            pnBuscar.Controls.Add(txtBusqueda);
            pnBuscar.Controls.Add(button1);
            pnBuscar.Font = new Font("Itim", 12F);
            pnBuscar.ForeColor = Color.Transparent;
            pnBuscar.Location = new Point(23, 27);
            pnBuscar.Name = "pnBuscar";
            pnBuscar.Size = new Size(516, 32);
            pnBuscar.TabIndex = 7;
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
            txtBusqueda.PlaceholderText = "Buscar producto...";
            txtBusqueda.Size = new Size(476, 28);
            txtBusqueda.TabIndex = 3;
            // 
            // button1
            // 
            button1.BackColor = Color.FromArgb(31, 60, 136);
            button1.Dock = DockStyle.Left;
            button1.FlatAppearance.BorderSize = 0;
            button1.FlatAppearance.MouseOverBackColor = Color.Gray;
            button1.FlatStyle = FlatStyle.Flat;
            button1.ForeColor = Color.Transparent;
            button1.Image = Properties.Resources.buscar_24;
            button1.Location = new Point(0, 0);
            button1.Margin = new Padding(3, 4, 3, 3);
            button1.Name = "button1";
            button1.Size = new Size(40, 32);
            button1.TabIndex = 2;
            button1.UseVisualStyleBackColor = false;
            // 
            // pnFiltro
            // 
            pnFiltro.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnFiltro.BackColor = Color.White;
            pnFiltro.Controls.Add(btnLimpiar);
            pnFiltro.Location = new Point(685, 27);
            pnFiltro.Name = "pnFiltro";
            pnFiltro.Size = new Size(217, 34);
            pnFiltro.TabIndex = 6;
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
            btnLimpiar.Click += btnLimpiar_Click;
            // 
            // pnFiltros
            // 
            pnFiltros.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnFiltros.BackColor = Color.White;
            pnFiltros.Controls.Add(rbHabilitados);
            pnFiltros.Controls.Add(rbDeshabilitados);
            pnFiltros.Controls.Add(rbTodos);
            pnFiltros.Controls.Add(label5);
            pnFiltros.Controls.Add(cmbMarca);
            pnFiltros.Controls.Add(label6);
            pnFiltros.Controls.Add(cmbPais);
            pnFiltros.Location = new Point(23, 93);
            pnFiltros.Name = "pnFiltros";
            pnFiltros.Size = new Size(879, 39);
            pnFiltros.TabIndex = 5;
            // 
            // rbHabilitados
            // 
            rbHabilitados.AutoSize = true;
            rbHabilitados.Font = new Font("Itim", 11F);
            rbHabilitados.Location = new Point(4, 7);
            rbHabilitados.Name = "rbHabilitados";
            rbHabilitados.Size = new Size(124, 27);
            rbHabilitados.TabIndex = 9;
            rbHabilitados.Text = "Habilitados";
            rbHabilitados.UseVisualStyleBackColor = true;
            rbHabilitados.CheckedChanged += rbHabilitados_CheckedChanged;
            // 
            // rbDeshabilitados
            // 
            rbDeshabilitados.AutoSize = true;
            rbDeshabilitados.Font = new Font("Itim", 11F);
            rbDeshabilitados.Location = new Point(134, 7);
            rbDeshabilitados.Name = "rbDeshabilitados";
            rbDeshabilitados.Size = new Size(149, 27);
            rbDeshabilitados.TabIndex = 10;
            rbDeshabilitados.Text = "Deshabilitados";
            rbDeshabilitados.UseVisualStyleBackColor = true;
            rbDeshabilitados.CheckedChanged += rbDeshabilitados_CheckedChanged;
            // 
            // rbTodos
            // 
            rbTodos.AutoSize = true;
            rbTodos.Checked = true;
            rbTodos.Font = new Font("Itim", 11F);
            rbTodos.Location = new Point(289, 7);
            rbTodos.Name = "rbTodos";
            rbTodos.Size = new Size(78, 27);
            rbTodos.TabIndex = 11;
            rbTodos.TabStop = true;
            rbTodos.Text = "Todos";
            rbTodos.UseVisualStyleBackColor = true;
            rbTodos.CheckedChanged += rbTodos_CheckedChanged;
            // 
            // label5
            // 
            label5.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label5.AutoSize = true;
            label5.Font = new Font("Itim", 11F);
            label5.Location = new Point(522, 7);
            label5.Margin = new Padding(3);
            label5.Name = "label5";
            label5.Size = new Size(97, 23);
            label5.TabIndex = 14;
            label5.Text = "Fabricante";
            // 
            // cmbMarca
            // 
            cmbMarca.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbMarca.Font = new Font("Itim", 11F);
            cmbMarca.FormattingEnabled = true;
            cmbMarca.Location = new Point(622, 4);
            cmbMarca.Margin = new Padding(0, 0, 3, 0);
            cmbMarca.Name = "cmbMarca";
            cmbMarca.Size = new Size(93, 30);
            cmbMarca.TabIndex = 15;
            cmbMarca.SelectedIndexChanged += cmbProveedor_SelectedIndexChanged;
            // 
            // label6
            // 
            label6.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label6.AutoSize = true;
            label6.Font = new Font("Itim", 11F);
            label6.Location = new Point(731, 7);
            label6.Margin = new Padding(3);
            label6.Name = "label6";
            label6.Size = new Size(43, 23);
            label6.TabIndex = 16;
            label6.Text = "Pais";
            // 
            // cmbPais
            // 
            cmbPais.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbPais.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbPais.AutoCompleteSource = AutoCompleteSource.ListItems;
            cmbPais.Font = new Font("Itim", 11F);
            cmbPais.FormattingEnabled = true;
            cmbPais.Location = new Point(777, 4);
            cmbPais.Margin = new Padding(0, 0, 3, 0);
            cmbPais.Name = "cmbPais";
            cmbPais.Size = new Size(97, 30);
            cmbPais.TabIndex = 17;
            cmbPais.SelectedIndexChanged += cmbPaises_SelectedIndexChanged;
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
            tableLayoutPanel1.TabIndex = 4;
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
            btnNuevo.Text = "Nuevo Producto";
            btnNuevo.UseVisualStyleBackColor = false;
            btnNuevo.Click += btnNuevo_Click;
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
            // pnDgv
            // 
            pnDgv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnDgv.Controls.Add(dgvProductos);
            pnDgv.Location = new Point(12, 175);
            pnDgv.Name = "pnDgv";
            pnDgv.Size = new Size(890, 399);
            pnDgv.TabIndex = 0;
            // 
            // dgvProductos
            // 
            dgvProductos.AllowUserToAddRows = false;
            dgvProductos.AllowUserToDeleteRows = false;
            dgvProductos.AllowUserToResizeColumns = false;
            dgvProductos.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(220, 230, 241);
            dataGridViewCellStyle1.Font = new Font("Itim", 12F);
            dataGridViewCellStyle1.ForeColor = Color.Black;
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle1.SelectionForeColor = Color.White;
            dgvProductos.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvProductos.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
            dgvProductos.BackgroundColor = Color.White;
            dgvProductos.BorderStyle = BorderStyle.None;
            dgvProductos.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvProductos.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle2.Font = new Font("Itim", 12F);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dgvProductos.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvProductos.ColumnHeadersHeight = 36;
            dgvProductos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvProductos.Columns.AddRange(new DataGridViewColumn[] { Codigo, Producto, Fabricante, Pais, Contenido, Presentacion, Categoria, EstadoProducto, CodigoInterno });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = Color.White;
            dataGridViewCellStyle3.Font = new Font("Itim", 12F);
            dataGridViewCellStyle3.ForeColor = Color.Black;
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dgvProductos.DefaultCellStyle = dataGridViewCellStyle3;
            dgvProductos.Dock = DockStyle.Fill;
            dgvProductos.EnableHeadersVisualStyles = false;
            dgvProductos.GridColor = Color.White;
            dgvProductos.Location = new Point(0, 0);
            dgvProductos.Margin = new Padding(4);
            dgvProductos.Name = "dgvProductos";
            dgvProductos.ReadOnly = true;
            dgvProductos.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle4.Font = new Font("Itim", 12F);
            dataGridViewCellStyle4.ForeColor = Color.Transparent;
            dataGridViewCellStyle4.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle4.SelectionForeColor = Color.Transparent;
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.False;
            dgvProductos.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            dgvProductos.RowHeadersWidth = 30;
            dgvProductos.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgvProductos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvProductos.Size = new Size(890, 399);
            dgvProductos.TabIndex = 2;
            dgvProductos.UseWaitCursor = true;
            dgvProductos.DoubleClick += dgvProductos_DoubleClick;
            // 
            // Codigo
            // 
            Codigo.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            Codigo.DataPropertyName = "codigoProducto";
            Codigo.HeaderText = "Código";
            Codigo.MinimumWidth = 6;
            Codigo.Name = "Codigo";
            Codigo.ReadOnly = true;
            Codigo.Width = 96;
            // 
            // Producto
            // 
            Producto.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            Producto.DataPropertyName = "nombreProducto";
            Producto.HeaderText = "Producto";
            Producto.MinimumWidth = 6;
            Producto.Name = "Producto";
            Producto.ReadOnly = true;
            Producto.Width = 115;
            // 
            // Fabricante
            // 
            Fabricante.DataPropertyName = "nombre_Fabricante";
            Fabricante.HeaderText = "Fabricante";
            Fabricante.MinimumWidth = 6;
            Fabricante.Name = "Fabricante";
            Fabricante.ReadOnly = true;
            Fabricante.Width = 125;
            // 
            // Pais
            // 
            Pais.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            Pais.DataPropertyName = "nombre_Pais";
            Pais.HeaderText = "País";
            Pais.MinimumWidth = 6;
            Pais.Name = "Pais";
            Pais.ReadOnly = true;
            Pais.Width = 71;
            // 
            // Contenido
            // 
            Contenido.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            Contenido.DataPropertyName = "contenidoProducto";
            Contenido.HeaderText = "Contenido";
            Contenido.MinimumWidth = 6;
            Contenido.Name = "Contenido";
            Contenido.ReadOnly = true;
            Contenido.Width = 125;
            // 
            // Presentacion
            // 
            Presentacion.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            Presentacion.DataPropertyName = "nombre_Presentacion";
            Presentacion.HeaderText = "Presentacion";
            Presentacion.MinimumWidth = 6;
            Presentacion.Name = "Presentacion";
            Presentacion.ReadOnly = true;
            // 
            // Categoria
            // 
            Categoria.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            Categoria.DataPropertyName = "nombre_Categoria";
            Categoria.HeaderText = "Categoria";
            Categoria.MinimumWidth = 6;
            Categoria.Name = "Categoria";
            Categoria.ReadOnly = true;
            Categoria.Width = 121;
            // 
            // EstadoProducto
            // 
            EstadoProducto.DataPropertyName = "idEstado";
            EstadoProducto.HeaderText = "Estado";
            EstadoProducto.MinimumWidth = 6;
            EstadoProducto.Name = "EstadoProducto";
            EstadoProducto.ReadOnly = true;
            EstadoProducto.Resizable = DataGridViewTriState.True;
            EstadoProducto.SortMode = DataGridViewColumnSortMode.Automatic;
            EstadoProducto.Width = 125;
            // 
            // CodigoInterno
            // 
            CodigoInterno.DataPropertyName = "idProducto";
            CodigoInterno.HeaderText = "Codigo";
            CodigoInterno.MinimumWidth = 6;
            CodigoInterno.Name = "CodigoInterno";
            CodigoInterno.ReadOnly = true;
            CodigoInterno.Visible = false;
            CodigoInterno.Width = 125;
            // 
            // GestionProductos
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(914, 726);
            Controls.Add(pnContenedorHijo);
            Name = "GestionProductos";
            Text = "Gestion de productos";
            Load += GestionProductos_Load;
            pnContenedorHijo.ResumeLayout(false);
            pnBuscar.ResumeLayout(false);
            pnBuscar.PerformLayout();
            pnFiltro.ResumeLayout(false);
            pnFiltros.ResumeLayout(false);
            pnFiltros.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            pnDgv.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvProductos).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnContenedorHijo;
        private Panel pnDgv;
        private DataGridView dgvProductos;
        private Button btnNuevo;
        private TableLayoutPanel tableLayoutPanel1;
        private Button btnEditar;
        private Button btnSalir;
        private Panel pnFiltros;
        private RadioButton rbHabilitados;
        private RadioButton rbDeshabilitados;
        private RadioButton rbTodos;
        private Label label5;
        private ComboBox cmbMarca;
        private Label label6;
        private ComboBox cmbPais;
        private Panel pnFiltro;
        private Button btnLimpiar;
        private Panel pnBuscar;
        private TextBox txtBusqueda;
        private Button button1;
        private DataGridViewTextBoxColumn Codigo;
        private DataGridViewTextBoxColumn Producto;
        private DataGridViewTextBoxColumn Fabricante;
        private DataGridViewTextBoxColumn Pais;
        private DataGridViewTextBoxColumn Contenido;
        private DataGridViewTextBoxColumn Presentacion;
        private DataGridViewTextBoxColumn Categoria;
        private DataGridViewCheckBoxColumn EstadoProducto;
        private DataGridViewTextBoxColumn CodigoInterno;
    }
}