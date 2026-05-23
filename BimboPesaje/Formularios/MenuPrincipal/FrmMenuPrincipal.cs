using BimboPesaje.Formularios.Movimientos;
using BimboPesaje.Formularios.Usuarios;
using CapaUI.Formularios.Principal.Pantallas.Productos;
using BimboPesajeProductos = BimboPesaje.Formularios.Productos;
using CapaDominio;
using System.Diagnostics;
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
        private bool                                _cerrando = false;

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
            _shell.LogoutRequested     += () => Close();
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
            try
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
                    var productosView = new ProductosView();
                    productosView.SalirSolicitado += () => {
                        _shell.ClearWpfView();
                        CerrarFormActual();
                    };
                    CerrarFormActual();
                    _shell.ShowWpfView(productosView);
                    break;
                case "prod-proveedores":
                    AbrirFormHijo(new BimboPesajeProductos.GestionProveedores());
                    break;
                case "prod-fabricantes":
                    AbrirFormHijo(new BimboPesajeProductos.GestionFabricantes());
                    break;
                case "prod-categorias":
                    AbrirFormHijo(new BimboPesajeProductos.GestionCategorias());
                    break;
                case "pes-movs":
                    AbrirFormHijo(new MovimientosyEntradas());
                    break;
                case "home":
                    CerrarFormActual();
                    break;
            }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir módulo:\n" + ex.ToString(), "Error de navegación",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        //  Cierre — punto único para X, Alt+F4, botón de logout y
        //  cualquier llamada programática a Close()
        // ══════════════════════════════════════════════════════════════
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Segunda pasada (re-entry): ya hicimos cleanup, dejar pasar
            if (_cerrando) { base.OnFormClosing(e); return; }

            // Apagado de Windows: cleanup sincrónico, sin diálogos
            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                servicioSesionActual.Cerrar();
                //ServicioPerfilUsuario.Limpiar();
                base.OnFormClosing(e);
                return;
            }

            // Primera pasada: cancelar el cierre y delegar al flujo async
            e.Cancel = true;
            HandleCierreAsync();
        }

        private async void HandleCierreAsync()
        {
            var confirmar = MessageBox.Show(
                this,
                "¿Deseas cerrar sesión?", "Cerrar sesión",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmar != DialogResult.Yes) return;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
                await client.Auth.SignOut();
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Cierre] SignOut timeout — continuando de todas formas");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cierre] Error en SignOut: {ex.Message}");
            }
            finally
            {
                servicioSesionActual.Cerrar();
                //ServicioPerfilUsuario.Limpiar();
                _cerrando = true;
                Close();
                Application.Exit();
            }
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
        //  Resize nativo via WS_THICKFRAME
        //  ElementHost captura los mensajes de mouse, por lo que WndProc
        //  no recibe WM_NCHITTEST a tiempo. Agregar WS_THICKFRAME en
        //  CreateParams delega el resize al sistema operativo antes de
        //  que WPF/ElementHost intervenga.
        // ══════════════════════════════════════════════════════════════
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_THICKFRAME  = 0x00040000;
                const int WS_MINIMIZEBOX = 0x00020000;
                const int WS_MAXIMIZEBOX = 0x00010000;
                var cp = base.CreateParams;
                cp.Style |= WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
                return cp;
            }
        }
    }
}
