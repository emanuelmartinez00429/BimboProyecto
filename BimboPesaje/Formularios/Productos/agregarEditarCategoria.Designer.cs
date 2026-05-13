namespace BimboPesaje.Formularios.Productos
{
    partial class agregarEditarCategoria
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
            panel1 = new Panel();
            pictureBox1 = new PictureBox();
            panel3 = new Panel();
            panel4 = new Panel();
            btnVolver = new Button();
            btnGuardar = new Button();
            rbInactivo = new RadioButton();
            rbActivo = new RadioButton();
            txtDescripcion = new TextBox();
            txtnombreCategoria = new TextBox();
            label3 = new Label();
            label2 = new Label();
            label1 = new Label();
            panel2 = new Panel();
            lblTexto = new Label();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            panel3.SuspendLayout();
            panel4.SuspendLayout();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(pictureBox1);
            panel1.Controls.Add(panel3);
            panel1.Controls.Add(panel2);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(800, 473);
            panel1.TabIndex = 0;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = Properties.Resources.bimbo_no_bg;
            pictureBox1.Location = new Point(0, 377);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(153, 93);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 8;
            pictureBox1.TabStop = false;
            // 
            // panel3
            // 
            panel3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel3.Controls.Add(panel4);
            panel3.Controls.Add(rbInactivo);
            panel3.Controls.Add(rbActivo);
            panel3.Controls.Add(txtDescripcion);
            panel3.Controls.Add(txtnombreCategoria);
            panel3.Controls.Add(label3);
            panel3.Controls.Add(label2);
            panel3.Controls.Add(label1);
            panel3.Location = new Point(85, 140);
            panel3.Name = "panel3";
            panel3.Size = new Size(606, 312);
            panel3.TabIndex = 1;
            // 
            // panel4
            // 
            panel4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel4.Controls.Add(btnVolver);
            panel4.Controls.Add(btnGuardar);
            panel4.Location = new Point(228, 269);
            panel4.Name = "panel4";
            panel4.Size = new Size(248, 40);
            panel4.TabIndex = 7;
            // 
            // btnVolver
            // 
            btnVolver.BackColor = Color.Gray;
            btnVolver.Dock = DockStyle.Right;
            btnVolver.FlatAppearance.BorderSize = 0;
            btnVolver.FlatAppearance.MouseDownBackColor = Color.Gray;
            btnVolver.FlatStyle = FlatStyle.Flat;
            btnVolver.Font = new Font("Itim", 10F);
            btnVolver.ForeColor = Color.White;
            btnVolver.Location = new Point(154, 0);
            btnVolver.Name = "btnVolver";
            btnVolver.Size = new Size(94, 40);
            btnVolver.TabIndex = 1;
            btnVolver.Text = "Volver";
            btnVolver.UseVisualStyleBackColor = false;
            btnVolver.Click += btnVolver_Click;
            // 
            // btnGuardar
            // 
            btnGuardar.BackColor = Color.FromArgb(12, 92, 92);
            btnGuardar.Dock = DockStyle.Left;
            btnGuardar.FlatAppearance.BorderSize = 0;
            btnGuardar.FlatAppearance.MouseOverBackColor = Color.LightGray;
            btnGuardar.FlatStyle = FlatStyle.Flat;
            btnGuardar.Font = new Font("Itim", 10F);
            btnGuardar.ForeColor = Color.White;
            btnGuardar.Location = new Point(0, 0);
            btnGuardar.Name = "btnGuardar";
            btnGuardar.Size = new Size(94, 40);
            btnGuardar.TabIndex = 0;
            btnGuardar.Text = "Guardar";
            btnGuardar.UseVisualStyleBackColor = false;
            btnGuardar.Click += btnGuardar_Click;
            // 
            // rbInactivo
            // 
            rbInactivo.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rbInactivo.AutoSize = true;
            rbInactivo.Font = new Font("Microsoft Sans Serif", 12F);
            rbInactivo.Location = new Point(348, 219);
            rbInactivo.Name = "rbInactivo";
            rbInactivo.Size = new Size(100, 29);
            rbInactivo.TabIndex = 6;
            rbInactivo.Text = "Inactivo";
            rbInactivo.UseVisualStyleBackColor = true;
            // 
            // rbActivo
            // 
            rbActivo.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rbActivo.AutoSize = true;
            rbActivo.Checked = true;
            rbActivo.Font = new Font("Microsoft Sans Serif", 12F);
            rbActivo.Location = new Point(228, 219);
            rbActivo.Name = "rbActivo";
            rbActivo.Size = new Size(87, 29);
            rbActivo.TabIndex = 5;
            rbActivo.TabStop = true;
            rbActivo.Text = "Activo";
            rbActivo.UseVisualStyleBackColor = true;
            // 
            // txtDescripcion
            // 
            txtDescripcion.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtDescripcion.Font = new Font("Microsoft Sans Serif", 12F);
            txtDescripcion.Location = new Point(228, 92);
            txtDescripcion.Multiline = true;
            txtDescripcion.Name = "txtDescripcion";
            txtDescripcion.ScrollBars = ScrollBars.Vertical;
            txtDescripcion.Size = new Size(316, 100);
            txtDescripcion.TabIndex = 4;
            // 
            // txtnombreCategoria
            // 
            txtnombreCategoria.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtnombreCategoria.Font = new Font("Microsoft Sans Serif", 12F);
            txtnombreCategoria.Location = new Point(228, 40);
            txtnombreCategoria.Name = "txtnombreCategoria";
            txtnombreCategoria.Size = new Size(316, 30);
            txtnombreCategoria.TabIndex = 3;
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label3.AutoSize = true;
            label3.Font = new Font("Microsoft Sans Serif", 12F);
            label3.ForeColor = Color.Black;
            label3.Location = new Point(124, 221);
            label3.Name = "label3";
            label3.Size = new Size(73, 25);
            label3.TabIndex = 2;
            label3.Text = "Estado";
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft Sans Serif", 12F);
            label2.ForeColor = Color.Black;
            label2.Location = new Point(83, 94);
            label2.Name = "label2";
            label2.Size = new Size(114, 25);
            label2.TabIndex = 1;
            label2.Text = "Descripción";
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft Sans Serif", 12F);
            label1.ForeColor = Color.Black;
            label1.Location = new Point(31, 42);
            label1.Name = "label1";
            label1.Size = new Size(166, 25);
            label1.TabIndex = 0;
            label1.Text = "Nombre categoria";
            // 
            // panel2
            // 
            panel2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel2.BackColor = Color.FromArgb(31, 60, 136);
            panel2.Controls.Add(lblTexto);
            panel2.Location = new Point(0, 33);
            panel2.Name = "panel2";
            panel2.Size = new Size(800, 101);
            panel2.TabIndex = 0;
            // 
            // lblTexto
            // 
            lblTexto.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblTexto.AutoSize = true;
            lblTexto.Font = new Font("Microsoft Sans Serif", 24F);
            lblTexto.ForeColor = Color.White;
            lblTexto.Location = new Point(227, 27);
            lblTexto.Name = "lblTexto";
            lblTexto.Size = new Size(345, 46);
            lblTexto.TabIndex = 0;
            lblTexto.Text = "Agregar Categoria";
            // 
            // agregarEditarCategoria
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(79, 125, 209);
            ClientSize = new Size(800, 473);
            Controls.Add(panel1);
            FormBorderStyle = FormBorderStyle.None;
            Name = "agregarEditarCategoria";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Agregar/Editar Categoria";
            panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            panel3.ResumeLayout(false);
            panel3.PerformLayout();
            panel4.ResumeLayout(false);
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panel1;
        private Panel panel2;
        private Label lblTexto;
        private Panel panel3;
        private RadioButton rbInactivo;
        private RadioButton rbActivo;
        private TextBox txtDescripcion;
        private TextBox txtnombreCategoria;
        private Label label3;
        private Label label2;
        private Label label1;
        private PictureBox pictureBox1;
        private Panel panel4;
        private Button btnVolver;
        private Button btnGuardar;
    }
}