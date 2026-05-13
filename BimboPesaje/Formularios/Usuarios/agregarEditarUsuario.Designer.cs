namespace BimboPesaje.Formularios.Usuarios
{
    partial class agregarEditarUsuario
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
            rbInactivo = new RadioButton();
            panel4 = new Panel();
            btnVolver = new Button();
            btnGuardar = new Button();
            rbActivo = new RadioButton();
            cmbRol = new ComboBox();
            txtContrasenia = new TextBox();
            txtCorreo = new TextBox();
            label5 = new Label();
            label4 = new Label();
            label3 = new Label();
            label2 = new Label();
            pictureBox1 = new PictureBox();
            panel2 = new Panel();
            label1 = new Label();
            panel1.SuspendLayout();
            panel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.BackColor = Color.FromArgb(79, 125, 209);
            panel1.Controls.Add(rbInactivo);
            panel1.Controls.Add(panel4);
            panel1.Controls.Add(rbActivo);
            panel1.Controls.Add(cmbRol);
            panel1.Controls.Add(txtContrasenia);
            panel1.Controls.Add(txtCorreo);
            panel1.Controls.Add(label5);
            panel1.Controls.Add(label4);
            panel1.Controls.Add(label3);
            panel1.Controls.Add(label2);
            panel1.Controls.Add(pictureBox1);
            panel1.Controls.Add(panel2);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(692, 466);
            panel1.TabIndex = 0;
            // 
            // rbInactivo
            // 
            rbInactivo.AutoSize = true;
            rbInactivo.Font = new Font("Microsoft Sans Serif", 14F);
            rbInactivo.Location = new Point(428, 296);
            rbInactivo.Name = "rbInactivo";
            rbInactivo.Size = new Size(115, 33);
            rbInactivo.TabIndex = 3;
            rbInactivo.TabStop = true;
            rbInactivo.Text = "Inactivo";
            rbInactivo.UseVisualStyleBackColor = true;
            // 
            // panel4
            // 
            panel4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel4.Controls.Add(btnVolver);
            panel4.Controls.Add(btnGuardar);
            panel4.Location = new Point(240, 353);
            panel4.Name = "panel4";
            panel4.Size = new Size(248, 40);
            panel4.TabIndex = 15;
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
            // 
            // rbActivo
            // 
            rbActivo.AutoSize = true;
            rbActivo.Font = new Font("Microsoft Sans Serif", 14F);
            rbActivo.Location = new Point(240, 296);
            rbActivo.Name = "rbActivo";
            rbActivo.Size = new Size(98, 33);
            rbActivo.TabIndex = 2;
            rbActivo.TabStop = true;
            rbActivo.Text = "Activo";
            rbActivo.UseVisualStyleBackColor = true;
            // 
            // cmbRol
            // 
            cmbRol.FormattingEnabled = true;
            cmbRol.Location = new Point(240, 250);
            cmbRol.Name = "cmbRol";
            cmbRol.Size = new Size(303, 28);
            cmbRol.TabIndex = 11;
            // 
            // txtContrasenia
            // 
            txtContrasenia.Location = new Point(240, 200);
            txtContrasenia.Name = "txtContrasenia";
            txtContrasenia.Size = new Size(303, 27);
            txtContrasenia.TabIndex = 8;
            // 
            // txtCorreo
            // 
            txtCorreo.Location = new Point(240, 150);
            txtCorreo.Name = "txtCorreo";
            txtCorreo.Size = new Size(303, 27);
            txtCorreo.TabIndex = 7;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Microsoft Sans Serif", 14F);
            label5.Location = new Point(135, 300);
            label5.Name = "label5";
            label5.Size = new Size(88, 29);
            label5.TabIndex = 6;
            label5.Text = "Estado";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Microsoft Sans Serif", 14F);
            label4.Location = new Point(48, 250);
            label4.Name = "label4";
            label4.Size = new Size(175, 29);
            label4.TabIndex = 5;
            label4.Text = "Rol del usuario";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Microsoft Sans Serif", 14F);
            label3.Location = new Point(87, 200);
            label3.Name = "label3";
            label3.Size = new Size(136, 29);
            label3.TabIndex = 4;
            label3.Text = "Contraseña";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft Sans Serif", 14F);
            label2.Location = new Point(135, 150);
            label2.Name = "label2";
            label2.Size = new Size(88, 29);
            label2.TabIndex = 3;
            label2.Text = "Correo";
            // 
            // pictureBox1
            // 
            pictureBox1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            pictureBox1.Image = Properties.Resources.bimbo_no_bg;
            pictureBox1.Location = new Point(0, 370);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(153, 93);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 2;
            pictureBox1.TabStop = false;
            // 
            // panel2
            // 
            panel2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            panel2.BackColor = Color.FromArgb(31, 60, 136);
            panel2.Controls.Add(label1);
            panel2.Location = new Point(0, 40);
            panel2.Name = "panel2";
            panel2.Size = new Size(692, 79);
            panel2.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft Sans Serif", 22.2F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.White;
            label1.Location = new Point(210, 20);
            label1.Name = "label1";
            label1.Size = new Size(257, 42);
            label1.TabIndex = 0;
            label1.Text = "Crear usuario";
            // 
            // agregarEditarUsuario
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(692, 466);
            Controls.Add(panel1);
            FormBorderStyle = FormBorderStyle.None;
            Name = "agregarEditarUsuario";
            StartPosition = FormStartPosition.CenterParent;
            Text = "agregarEditarUsuario";
            Load += agregarEditarUsuario_Load;
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panel4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panel1;
        private Label label1;
        private PictureBox pictureBox1;
        private Panel panel2;
        private Label label5;
        private Label label4;
        private Label label3;
        private Label label2;
        private TextBox txtContrasenia;
        private TextBox txtCorreo;
        private ComboBox cmbRol;
        private Panel panel4;
        private Button btnVolver;
        private Button btnGuardar;
        private RadioButton rbInactivo;
        private RadioButton rbActivo;
    }
}