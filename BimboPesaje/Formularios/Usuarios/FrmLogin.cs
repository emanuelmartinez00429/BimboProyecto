using CapaDatos.Modelados.Usuarios;
using CapaDatos.Repositorios.Usuario;
using CapaDominio;
using ServicioConexión.Conexion;
using Usuario = CapaDatos.Modelados.Usuarios.Usuarios;  

namespace BimboPesaje.Formularios.Usuarios
{
    public class FrmLogin : Form
    {
        private TextBox txtEmail    = null!;
        private TextBox txtPassword = null!;
        private Button  btnIngresar = null!;
        private Label   lblError    = null!;

        public FrmLogin()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            Text              = "BimboPesaje — Iniciar sesión";
            Size              = new Size(400, 260);
            StartPosition     = FormStartPosition.CenterScreen;
            FormBorderStyle   = FormBorderStyle.FixedDialog;
            MaximizeBox       = false;
            MinimizeBox       = false;

            var lblEmailLbl = new Label { Text = "Correo electrónico:", Left = 20, Top = 25, Width = 340, AutoSize = true };
            txtEmail        = new TextBox { Left = 20, Top = 45, Width = 340 };

            var lblPassLbl  = new Label { Text = "Contraseña:", Left = 20, Top = 80, Width = 340, AutoSize = true };
            txtPassword     = new TextBox { Left = 20, Top = 100, Width = 340, UseSystemPasswordChar = true };

            lblError = new Label { Left = 20, Top = 136, Width = 340, ForeColor = Color.Red, Text = "", AutoSize = false };

            btnIngresar = new Button
            {
                Text      = "Ingresar",
                Left      = 240, Top = 170, Width = 120, Height = 38,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            btnIngresar.FlatAppearance.BorderSize = 0;
            btnIngresar.Click += BtnIngresar_Click;

            // Enter en el campo contraseña también dispara el login
            txtPassword.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) BtnIngresar_Click(s, e);
            };

            Controls.AddRange(new Control[]
                { lblEmailLbl, txtEmail, lblPassLbl, txtPassword, lblError, btnIngresar });
        }

        private async void BtnIngresar_Click(object sender, EventArgs e)
        {
            string email    = txtEmail.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                lblError.ForeColor = Color.Red;
                lblError.Text      = "Ingrese correo y contraseña.";
                return;
            }

            btnIngresar.Enabled = false;
            lblError.ForeColor  = Color.Gray;
            lblError.Text       = "Autenticando...";

            try
            {
                var client  = await ConexionSupabase.GetClientAsync();
                var session = await client.Auth.SignInWithPassword(email, password);

               

                if (session?.User == null)
                {
                    lblError.ForeColor  = Color.Red;
                    lblError.Text       = "Credenciales incorrectas.";
                    btnIngresar.Enabled = true;
                    return;
                }

                // Buscar el registro interno por UUID de Auth
                var usuario = await RepositorioUsuario.ObtenerPorUuidAsync(session.User.Id!);
                if (usuario == null)
                {
                    lblError.ForeColor  = Color.Red;
                    lblError.Text       = "El usuario no está registrado en el sistema.";
                    await client.Auth.SignOut();
                    btnIngresar.Enabled = true;
                    return;
                }

                SesionActual.IdUsuario     = usuario.idUsuario;
                SesionActual.NombreUsuario = email;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                lblError.ForeColor  = Color.Red;
                lblError.Text       = "Error: " + ex.Message;
                btnIngresar.Enabled = true;
            }
        }
    }
}
