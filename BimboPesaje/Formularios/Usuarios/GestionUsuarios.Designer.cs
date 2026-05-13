namespace BimboPesaje.Formularios.Usuarios
{
    partial class GestionUsuarios
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
            panel1 = new Panel();
            tableLayoutPanel1 = new TableLayoutPanel();
            btnSalir = new Button();
            btnEditar = new Button();
            pnDgv = new Panel();
            dgvUsuarios = new DataGridView();
            pnFiltro = new Panel();
            btnLimpiar = new Button();
            pnBuscar = new Panel();
            txtBusqueda = new TextBox();
            btnBuscar = new Button();
            pnFiltros = new Panel();
            rbHabilitados = new RadioButton();
            rbDeshabilitados = new RadioButton();
            rbTodos = new RadioButton();
            Codigo = new DataGridViewTextBoxColumn();
            Usuario = new DataGridViewTextBoxColumn();
            idEmpleado = new DataGridViewTextBoxColumn();
            idRol = new DataGridViewTextBoxColumn();
            estadoEmpleado = new DataGridViewCheckBoxColumn();
            panel1.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            pnDgv.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvUsuarios).BeginInit();
            pnFiltro.SuspendLayout();
            pnBuscar.SuspendLayout();
            pnFiltros.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.BackColor = Color.FromArgb(220, 230, 241);
            panel1.Controls.Add(tableLayoutPanel1);
            panel1.Controls.Add(pnDgv);
            panel1.Controls.Add(pnFiltro);
            panel1.Controls.Add(pnBuscar);
            panel1.Controls.Add(pnFiltros);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(914, 726);
            panel1.TabIndex = 0;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(btnSalir, 1, 1);
            tableLayoutPanel1.Controls.Add(btnEditar, 0, 0);
            tableLayoutPanel1.Location = new Point(12, 589);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(273, 125);
            tableLayoutPanel1.TabIndex = 12;
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
            btnSalir.Location = new Point(139, 65);
            btnSalir.Name = "btnSalir";
            btnSalir.Size = new Size(131, 57);
            btnSalir.TabIndex = 3;
            btnSalir.Text = "Salir";
            btnSalir.UseVisualStyleBackColor = false;
            // 
            // btnEditar
            // 
            btnEditar.BackColor = Color.FromArgb(46, 90, 172);
            btnEditar.FlatAppearance.BorderSize = 0;
            btnEditar.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnEditar.FlatStyle = FlatStyle.Flat;
            btnEditar.Font = new Font("Itim", 12F);
            btnEditar.ForeColor = Color.White;
            btnEditar.Location = new Point(3, 3);
            btnEditar.Name = "btnEditar";
            btnEditar.Size = new Size(130, 56);
            btnEditar.TabIndex = 1;
            btnEditar.Text = "Editar";
            btnEditar.UseVisualStyleBackColor = false;
            // 
            // pnDgv
            // 
            pnDgv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnDgv.Controls.Add(dgvUsuarios);
            pnDgv.Location = new Point(12, 163);
            pnDgv.Name = "pnDgv";
            pnDgv.Size = new Size(890, 399);
            pnDgv.TabIndex = 11;
            // 
            // dgvUsuarios
            // 
            dgvUsuarios.AllowUserToAddRows = false;
            dgvUsuarios.AllowUserToDeleteRows = false;
            dgvUsuarios.AllowUserToResizeColumns = false;
            dgvUsuarios.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(220, 230, 241);
            dataGridViewCellStyle1.Font = new Font("Itim", 12F);
            dataGridViewCellStyle1.ForeColor = Color.Black;
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle1.SelectionForeColor = Color.White;
            dgvUsuarios.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvUsuarios.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
            dgvUsuarios.BackgroundColor = Color.White;
            dgvUsuarios.BorderStyle = BorderStyle.None;
            dgvUsuarios.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvUsuarios.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle2.Font = new Font("Itim", 12F);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dgvUsuarios.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvUsuarios.ColumnHeadersHeight = 36;
            dgvUsuarios.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvUsuarios.Columns.AddRange(new DataGridViewColumn[] { Codigo, Usuario, idEmpleado, idRol, estadoEmpleado });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = Color.White;
            dataGridViewCellStyle3.Font = new Font("Itim", 12F);
            dataGridViewCellStyle3.ForeColor = Color.Black;
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dgvUsuarios.DefaultCellStyle = dataGridViewCellStyle3;
            dgvUsuarios.Dock = DockStyle.Fill;
            dgvUsuarios.EnableHeadersVisualStyles = false;
            dgvUsuarios.GridColor = Color.White;
            dgvUsuarios.Location = new Point(0, 0);
            dgvUsuarios.Margin = new Padding(4);
            dgvUsuarios.Name = "dgvUsuarios";
            dgvUsuarios.ReadOnly = true;
            dgvUsuarios.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle4.Font = new Font("Itim", 12F);
            dataGridViewCellStyle4.ForeColor = Color.Transparent;
            dataGridViewCellStyle4.SelectionBackColor = Color.FromArgb(31, 60, 136);
            dataGridViewCellStyle4.SelectionForeColor = Color.Transparent;
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.False;
            dgvUsuarios.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            dgvUsuarios.RowHeadersWidth = 30;
            dgvUsuarios.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgvUsuarios.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvUsuarios.Size = new Size(890, 399);
            dgvUsuarios.TabIndex = 2;
            dgvUsuarios.UseWaitCursor = true;
            // 
            // pnFiltro
            // 
            pnFiltro.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnFiltro.BackColor = Color.White;
            pnFiltro.Controls.Add(btnLimpiar);
            pnFiltro.Location = new Point(685, 27);
            pnFiltro.Name = "pnFiltro";
            pnFiltro.Size = new Size(217, 34);
            pnFiltro.TabIndex = 10;
            // 
            // btnLimpiar
            // 
            btnLimpiar.BackColor = Color.FromArgb(31, 60, 136);
            btnLimpiar.Dock = DockStyle.Fill;
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
            btnLimpiar.TabIndex = 1;
            btnLimpiar.Text = "Limpiar Filtros";
            btnLimpiar.UseVisualStyleBackColor = false;
            // 
            // pnBuscar
            // 
            pnBuscar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnBuscar.BackColor = Color.White;
            pnBuscar.Controls.Add(txtBusqueda);
            pnBuscar.Controls.Add(btnBuscar);
            pnBuscar.Font = new Font("Itim", 12F);
            pnBuscar.ForeColor = Color.Transparent;
            pnBuscar.Location = new Point(23, 27);
            pnBuscar.Name = "pnBuscar";
            pnBuscar.Size = new Size(516, 32);
            pnBuscar.TabIndex = 9;
            // 
            // txtBusqueda
            // 
            txtBusqueda.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtBusqueda.BackColor = Color.White;
            txtBusqueda.BorderStyle = BorderStyle.None;
            txtBusqueda.Font = new Font("Itim", 14F);
            txtBusqueda.ForeColor = Color.Black;
            txtBusqueda.Location = new Point(40, 0);
            txtBusqueda.Multiline = true;
            txtBusqueda.Name = "txtBusqueda";
            txtBusqueda.PlaceholderText = "Buscar usuario...";
            txtBusqueda.Size = new Size(473, 32);
            txtBusqueda.TabIndex = 3;
            // 
            // btnBuscar
            // 
            btnBuscar.BackColor = Color.FromArgb(31, 60, 136);
            btnBuscar.Dock = DockStyle.Left;
            btnBuscar.FlatAppearance.BorderSize = 0;
            btnBuscar.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnBuscar.FlatStyle = FlatStyle.Flat;
            btnBuscar.ForeColor = Color.Transparent;
            btnBuscar.Image = Properties.Resources.buscar_24;
            btnBuscar.Location = new Point(0, 0);
            btnBuscar.Margin = new Padding(3, 4, 3, 3);
            btnBuscar.Name = "btnBuscar";
            btnBuscar.Size = new Size(40, 32);
            btnBuscar.TabIndex = 2;
            btnBuscar.UseVisualStyleBackColor = false;
            // 
            // pnFiltros
            // 
            pnFiltros.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnFiltros.BackColor = Color.White;
            pnFiltros.Controls.Add(rbHabilitados);
            pnFiltros.Controls.Add(rbDeshabilitados);
            pnFiltros.Controls.Add(rbTodos);
            pnFiltros.Location = new Point(23, 93);
            pnFiltros.Name = "pnFiltros";
            pnFiltros.Size = new Size(879, 39);
            pnFiltros.TabIndex = 7;
            // 
            // rbHabilitados
            // 
            rbHabilitados.AutoSize = true;
            rbHabilitados.Checked = true;
            rbHabilitados.Font = new Font("Itim", 11F);
            rbHabilitados.Location = new Point(4, 7);
            rbHabilitados.Name = "rbHabilitados";
            rbHabilitados.Size = new Size(124, 27);
            rbHabilitados.TabIndex = 9;
            rbHabilitados.TabStop = true;
            rbHabilitados.Text = "Habilitados";
            rbHabilitados.UseVisualStyleBackColor = true;
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
            // 
            // rbTodos
            // 
            rbTodos.AutoSize = true;
            rbTodos.Font = new Font("Itim", 11F);
            rbTodos.Location = new Point(289, 7);
            rbTodos.Name = "rbTodos";
            rbTodos.Size = new Size(78, 27);
            rbTodos.TabIndex = 11;
            rbTodos.Text = "Todos";
            rbTodos.UseVisualStyleBackColor = true;
            // 
            // Codigo
            // 
            Codigo.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            Codigo.DataPropertyName = "idUsuario";
            Codigo.HeaderText = "Código";
            Codigo.MinimumWidth = 6;
            Codigo.Name = "Codigo";
            Codigo.ReadOnly = true;
            Codigo.Visible = false;
            Codigo.Width = 96;
            // 
            // Usuario
            // 
            Usuario.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            Usuario.DataPropertyName = "correoUsuario";
            Usuario.HeaderText = "Usuario";
            Usuario.MinimumWidth = 6;
            Usuario.Name = "Usuario";
            Usuario.ReadOnly = true;
            // 
            // idEmpleado
            // 
            idEmpleado.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            idEmpleado.DataPropertyName = "nombre_Empleado";
            idEmpleado.HeaderText = "Nombre empleado";
            idEmpleado.MinimumWidth = 6;
            idEmpleado.Name = "idEmpleado";
            idEmpleado.ReadOnly = true;
            // 
            // idRol
            // 
            idRol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            idRol.DataPropertyName = "nombre_Rol";
            idRol.HeaderText = "Rol";
            idRol.MinimumWidth = 6;
            idRol.Name = "idRol";
            idRol.ReadOnly = true;
            // 
            // estadoEmpleado
            // 
            estadoEmpleado.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            estadoEmpleado.DataPropertyName = "idEstado";
            estadoEmpleado.HeaderText = "Estado";
            estadoEmpleado.MinimumWidth = 6;
            estadoEmpleado.Name = "estadoEmpleado";
            estadoEmpleado.ReadOnly = true;
            estadoEmpleado.Resizable = DataGridViewTriState.True;
            estadoEmpleado.Width = 72;
            // 
            // GestionUsuarios
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(914, 726);
            Controls.Add(panel1);
            Name = "GestionUsuarios";
            Text = "GestionUsuarios";
            Load += GestionUsuarios_Load;
            panel1.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            pnDgv.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvUsuarios).EndInit();
            pnFiltro.ResumeLayout(false);
            pnBuscar.ResumeLayout(false);
            pnBuscar.PerformLayout();
            pnFiltros.ResumeLayout(false);
            pnFiltros.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panel1;
        private TableLayoutPanel tableLayoutPanel1;
        private Button btnSalir;
        private Button btnEditar;
        private Panel pnDgv;
        private DataGridView dgvUsuarios;
        private Panel pnFiltro;
        private Button btnLimpiar;
        private Panel pnBuscar;
        private TextBox txtBusqueda;
        private Button btnBuscar;
        private Panel pnFiltros;
        private RadioButton rbHabilitados;
        private RadioButton rbDeshabilitados;
        private RadioButton rbTodos;
        private DataGridViewTextBoxColumn Codigo;
        private DataGridViewTextBoxColumn Usuario;
        private DataGridViewTextBoxColumn idEmpleado;
        private DataGridViewTextBoxColumn idRol;
        private DataGridViewCheckBoxColumn estadoEmpleado;
    }
}