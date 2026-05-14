using System.Runtime.InteropServices;
using System.Windows.Forms.Integration;

namespace BimboPesaje.Formularios.InicioSesion
{
    public class FrmInicioSesion : Form
    {
        private ElementHost _host = null!;
        private UcLoginShell _shell = null!;

        public FrmInicioSesion()
        {
            // WPF Application object is required for pack:// URIs and ResourceDictionary loading
            if (System.Windows.Application.Current == null)
            {
                var app = new System.Windows.Application();
                app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            }

            InitForm();
        }

        private void InitForm()
        {
            Text            = "BimboPesaje — Iniciar sesión";
            ClientSize      = new Size(900, 560);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox     = false;
            BackColor       = Color.FromArgb(30, 58, 138); // fallback during load

            _shell = new UcLoginShell();

            _shell.MinimizeRequested += (_, _) => WindowState = FormWindowState.Minimized;
            _shell.CloseRequested    += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _shell.LoginSucceeded    += (_, _) => { DialogResult = DialogResult.OK;     Close(); };
            _shell.DragMoveRequested += (_, _) =>
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, 0x2 /*HTCAPTION*/, 0);
            };

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _shell
            };

            Controls.Add(_host);
        }

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
    }
}
