namespace BimboPesaje.Formularios.Usuarios
{
    partial class agregarEditarEmpleado
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
            pnFill = new Panel();
            panel2 = new Panel();
            panel4 = new Panel();
            btnVolver = new Button();
            btnGuardar = new Button();
            rbInactivo = new RadioButton();
            rbActivo = new RadioButton();
            label7 = new Label();
            txtCorreo = new TextBox();
            label6 = new Label();
            txtTelefono = new TextBox();
            label5 = new Label();
            txtApellido = new TextBox();
            label4 = new Label();
            txtNombre = new TextBox();
            label3 = new Label();
            txtDNI = new TextBox();
            label2 = new Label();
            panel1 = new Panel();
            lblTitulo = new Label();
            pictureBox1 = new PictureBox();
            pnFill.SuspendLayout();
            panel2.SuspendLayout();
            panel4.SuspendLayout();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // pnFill
            // 
            pnFill.BackColor = Color.FromArgb(79, 125, 209);
            pnFill.Controls.Add(panel2);
            pnFill.Controls.Add(panel1);
            pnFill.Dock = DockStyle.Fill;
            pnFill.Location = new Point(0, 0);
            pnFill.Name = "pnFill";
            pnFill.Size = new Size(678, 569);
            pnFill.TabIndex = 0;
            pnFill.Paint += pnFill_Paint;
            // 
            // panel2
            // 
            panel2.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel2.Controls.Add(pictureBox1);
            panel2.Controls.Add(panel4);
            panel2.Controls.Add(rbInactivo);
            panel2.Controls.Add(rbActivo);
            panel2.Controls.Add(label7);
            panel2.Controls.Add(txtCorreo);
            panel2.Controls.Add(label6);
            panel2.Controls.Add(txtTelefono);
            panel2.Controls.Add(label5);
            panel2.Controls.Add(txtApellido);
            panel2.Controls.Add(label4);
            panel2.Controls.Add(txtNombre);
            panel2.Controls.Add(label3);
            panel2.Controls.Add(txtDNI);
            panel2.Controls.Add(label2);
            panel2.Location = new Point(3, 139);
            panel2.Name = "panel2";
            panel2.Size = new Size(672, 427);
            panel2.TabIndex = 1;
            // 
            // panel4
            // 
            panel4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel4.Controls.Add(btnVolver);
            panel4.Controls.Add(btnGuardar);
            panel4.Location = new Point(254, 336);
            panel4.Name = "panel4";
            panel4.Size = new Size(248, 40);
            panel4.TabIndex = 14;
            // 
            // btnVolver
            // 
            btnVolver.BackColor = Color.Gray;
            btnVolver.Dock = DockStyle.Right;
            btnVolver.FlatAppearance.BorderSize = 0;
            btnVolver.FlatAppearance.MouseDownBackColor = Color.Gray;
            btnVolver.FlatStyle = FlatStyle.Flat;
            btnVolver.Font = new Font("Microsoft Sans Serif", 10F);
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
            btnGuardar.Font = new Font("Microsoft Sans Serif", 10F);
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
            rbInactivo.AutoSize = true;
            rbInactivo.Font = new Font("Microsoft Sans Serif", 12F);
            rbInactivo.Location = new Point(453, 285);
            rbInactivo.Name = "rbInactivo";
            rbInactivo.Size = new Size(100, 29);
            rbInactivo.TabIndex = 13;
            rbInactivo.TabStop = true;
            rbInactivo.Text = "Inactivo";
            rbInactivo.UseVisualStyleBackColor = true;
            // 
            // rbActivo
            // 
            rbActivo.AutoSize = true;
            rbActivo.Font = new Font("Microsoft Sans Serif", 12F);
            rbActivo.Location = new Point(254, 283);
            rbActivo.Name = "rbActivo";
            rbActivo.Size = new Size(87, 29);
            rbActivo.TabIndex = 12;
            rbActivo.TabStop = true;
            rbActivo.Text = "Activo";
            rbActivo.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Microsoft Sans Serif", 12F);
            label7.Location = new Point(156, 287);
            label7.Name = "label7";
            label7.Size = new Size(73, 25);
            label7.TabIndex = 11;
            label7.Text = "Estado";
            // 
            // txtCorreo
            // 
            txtCorreo.Location = new Point(254, 230);
            txtCorreo.Name = "txtCorreo";
            txtCorreo.Size = new Size(299, 27);
            txtCorreo.TabIndex = 10;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Microsoft Sans Serif", 12F);
            label6.Location = new Point(157, 232);
            label6.Name = "label6";
            label6.Size = new Size(72, 25);
            label6.TabIndex = 9;
            label6.Text = "Correo";
            // 
            // txtTelefono
            // 
            txtTelefono.Location = new Point(254, 179);
            txtTelefono.Name = "txtTelefono";
            txtTelefono.Size = new Size(299, 27);
            txtTelefono.TabIndex = 8;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Microsoft Sans Serif", 12F);
            label5.Location = new Point(140, 181);
            label5.Name = "label5";
            label5.Size = new Size(89, 25);
            label5.TabIndex = 7;
            label5.Text = "Teléfono";
            // 
            // txtApellido
            // 
            txtApellido.Location = new Point(254, 125);
            txtApellido.Name = "txtApellido";
            txtApellido.Size = new Size(299, 27);
            txtApellido.TabIndex = 6;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Microsoft Sans Serif", 12F);
            label4.Location = new Point(147, 127);
            label4.Name = "label4";
            label4.Size = new Size(82, 25);
            label4.TabIndex = 5;
            label4.Text = "Apellido";
            // 
            // txtNombre
            // 
            txtNombre.Location = new Point(254, 73);
            txtNombre.Name = "txtNombre";
            txtNombre.Size = new Size(299, 27);
            txtNombre.TabIndex = 4;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Microsoft Sans Serif", 12F);
            label3.Location = new Point(148, 75);
            label3.Name = "label3";
            label3.Size = new Size(81, 25);
            label3.TabIndex = 3;
            label3.Text = "Nombre";
            // 
            // txtDNI
            // 
            txtDNI.Location = new Point(254, 22);
            txtDNI.Name = "txtDNI";
            txtDNI.Size = new Size(299, 27);
            txtDNI.TabIndex = 2;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft Sans Serif", 12F);
            label2.Location = new Point(184, 24);
            label2.Name = "label2";
            label2.Size = new Size(45, 25);
            label2.TabIndex = 1;
            label2.Text = "DNI";
            // 
            // panel1
            // 
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel1.BackColor = Color.FromArgb(31, 60, 136);
            panel1.Controls.Add(lblTitulo);
            panel1.Location = new Point(0, 35);
            panel1.Name = "panel1";
            panel1.Size = new Size(678, 106);
            panel1.TabIndex = 0;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(181, 28);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(324, 46);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "Crear Empleado";
            // 
            // pictureBox1
            // 
            pictureBox1.Image = Properties.Resources.bimbo_no_bg;
            pictureBox1.Location = new Point(-3, 337);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(153, 93);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 9;
            pictureBox1.TabStop = false;
            // 
            // agregarEditarEmpleado
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(678, 569);
            Controls.Add(pnFill);
            FormBorderStyle = FormBorderStyle.None;
            Name = "agregarEditarEmpleado";
            StartPosition = FormStartPosition.CenterParent;
            Text = "agregarEditarEmpleado";
            Load += agregarEditarEmpleado_Load;
            pnFill.ResumeLayout(false);
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            panel4.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnFill;
        private Panel panel1;
        private Panel panel2;
        private TextBox txtDNI;
        private Label label2;
        private Label lblTitulo;
        private TextBox txtCorreo;
        private Label label6;
        private TextBox txtTelefono;
        private Label label5;
        private TextBox txtApellido;
        private Label label4;
        private TextBox txtNombre;
        private Label label3;
        private RadioButton rbInactivo;
        private RadioButton rbActivo;
        private Label label7;
        private Panel panel4;
        private Button btnVolver;
        private Button btnGuardar;
        private PictureBox pictureBox1;
    }
}