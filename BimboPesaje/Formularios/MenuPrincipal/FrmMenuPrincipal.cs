using BimboPesaje.Formularios.Movimientos;
using BimboPesaje.Formularios.Productos;
using BimboPesaje.Formularios.Usuarios;
using CapaServicios;
using System.Runtime.InteropServices;
using WpfIntegration = System.Windows.Forms.Integration;

namespace BimboPesaje.Formularios.MenuPrincipal
{
    public class FrmMenuPrincipal : Form
    {
        // ══════════════════════════════════════════════════════════════
        //  P/Invoke para drag nativo
        // ══════════════════════════════════════════════════════════════
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        // ══════════════════════════════════════════════════════════════
        //  Controles
        // ══════════════════════════════════════════════════════════════
        private readonly WpfIntegration.ElementHost _host;
        private readonly UcMenuShell                _shell;
        private readonly Panel                      _contenedor;
        private Form?                               _formActual;

        // ══════════════════════════════════════════════════════════════
        //  Constructor
        // ══════════════════════════════════════════════════════════════
        public FrmMenuPrincipal()
        {
            // Asegurar Application WPF
            if (System.Windows.Application.Current == null)
                _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };

            // Configurar formulario
            Text            = "Bimbo — Portal Interno";
            WindowState     = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize     = new Size(1280, 760);
            Icon            = SystemIcons.Application;

            // Panel WinForms que se pasa al WindowsFormsHost dentro del shell WPF
            _contenedor = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(244, 245, 247) };

            // Shell WPF
            _shell = new UcMenuShell();
            _shell.SetContentPanel(_contenedor);

            // Suscribir eventos del shell
            _shell.NavigationRequested += OnNavigationRequested;
            _shell.LogoutRequested     += OnLogoutRequested;
            _shell.DragMoveRequested   += OnDragMoveRequested;
            _shell.MinimizeRequested   += () => WindowState = FormWindowState.Minimized;
            _shell.MaximizeRequested   += () => WindowState = WindowState == FormWindowState.Maximized
                                                    ? FormWindowState.Normal
                                                    : FormWindowState.Maximized;
            _shell.CloseRequested      += () => Close();

            // ElementHost: ocupa todo el formulario
            _host = new WpfIntegration.ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _shell,
            };

            Controls.Add(_host);
        }

        // ══════════════════════════════════════════════════════════════
        //  Navegación
        // ══════════════════════════════════════════════════════════════
        private void OnNavigationRequested(string moduleId)
        {
            switch (moduleId)
            {
                case "empleados":
                    AbrirFormHijo(new GestiónEmpleados());
                    break;
                case "usuarios-sub":
                    AbrirFormHijo(new GestionUsuarios());
                    break;
                case "roles":
                    // pendiente
                    break;
                case "bitacora":
                    // pendiente
                    break;
                case "prod-productos":
                    AbrirFormHijo(new GestionProductos());
                    break;
                case "prod-proveedores":
                    AbrirFormHijo(new GestionProveedores());
                    break;
                case "prod-fabricantes":
                    AbrirFormHijo(new GestionFabricantes());
                    break;
                case "prod-categorias":
                    AbrirFormHijo(new GestionCategorias());
                    break;
                case "pes-movs":
                    AbrirFormHijo(new MovimientosyEntradas());
                    break;
                case "home":
                    CerrarFormActual();
                    break;
            }
        }

        private void AbrirFormHijo(Form hijo)
        {
            CerrarFormActual();
            _formActual            = hijo;
            hijo.TopLevel          = false;
            hijo.FormBorderStyle   = FormBorderStyle.None;
            hijo.Dock              = DockStyle.Fill;
            _contenedor.Controls.Add(hijo);
            hijo.BringToFront();
            hijo.Show();
        }

        private void CerrarFormActual()
        {
            if (_formActual == null) return;
            _formActual.Close();
            _contenedor.Controls.Clear();
            _formActual = null;
        }

        // ══════════════════════════════════════════════════════════════
        //  Logout
        // ══════════════════════════════════════════════════════════════
        private void OnLogoutRequested()
        {
            var result = MessageBox.Show(
                "¿Deseas cerrar sesión?",
                "Cerrar sesión",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            servicioSesionActual.Cerrar();
            ServicioPerfilUsuario.Limpiar();
            Application.Exit();
        }

        // ══════════════════════════════════════════════════════════════
        //  Drag nativo
        // ══════════════════════════════════════════════════════════════
        private void OnDragMoveRequested()
        {
            if (WindowState == FormWindowState.Maximized) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }

        // ══════════════════════════════════════════════════════════════
        //  Resize nativo (Bug 2: borderless no tiene asas de redimensión)
        // ══════════════════════════════════════════════════════════════
        private const int WM_NCHITTEST   = 0x0084;
        private const int HTLEFT         = 10;
        private const int HTRIGHT        = 11;
        private const int HTTOP          = 12;
        private const int HTTOPLEFT      = 13;
        private const int HTTOPRIGHT     = 14;
        private const int HTBOTTOM       = 15;
        private const int HTBOTTOMLEFT   = 16;
        private const int HTBOTTOMRIGHT  = 17;
        private const int ResizeBorder   = 8;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                int x  = (short)(m.LParam.ToInt32() & 0xFFFF);
                int y  = (short)(m.LParam.ToInt32() >> 16);
                var pt = PointToClient(new Point(x, y));

                bool l = pt.X < ResizeBorder;
                bool r = pt.X >= ClientSize.Width  - ResizeBorder;
                bool t = pt.Y < ResizeBorder;
                bool b = pt.Y >= ClientSize.Height - ResizeBorder;

                if (t && l) { m.Result = (IntPtr)HTTOPLEFT;     return; }
                if (t && r) { m.Result = (IntPtr)HTTOPRIGHT;    return; }
                if (b && l) { m.Result = (IntPtr)HTBOTTOMLEFT;  return; }
                if (b && r) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                if (l)      { m.Result = (IntPtr)HTLEFT;        return; }
                if (r)      { m.Result = (IntPtr)HTRIGHT;       return; }
                if (t)      { m.Result = (IntPtr)HTTOP;         return; }
                if (b)      { m.Result = (IntPtr)HTBOTTOM;      return; }
            }
            base.WndProc(ref m);
        }
    }
}
