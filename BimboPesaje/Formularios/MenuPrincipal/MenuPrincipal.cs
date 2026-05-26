using BimboPesaje.Formularios.Movimientos;
using BimboPesaje.Formularios.Productos;
using BimboPesaje.Formularios.Usuarios;
using System.Threading.Tasks;

namespace BimboPesaje
{
    public partial class MenuPrincipal : Form
    {
        #region Variables
        private Form _formActual = null;
        private bool _menuAnimando = false; // ← AGREGADO: evita clics rápidos

        /// <summary>
        /// Variable para guardar los textos de los botones
        /// </summary>
        private Dictionary<string, string> _textosOriginales = new Dictionary<string, string>();

        /// <summary>
        /// variables para el ui del menu vertical
        /// </summary>
        private bool menuExpandido = true;
        private const int MENU_EXPANDIDO = 300;
        private const int MENU_COLAPSADO = 80;
        #endregion

        #region Form
        public MenuPrincipal()
        {
            InitializeComponent();
        }

        private void MenuPrincipal_Load(object sender, EventArgs e) // ← quitado async, no hay awaits reales
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            EscalarImagenesBotones();
            EscalarPictureBox();

            lblHeader.Text = "Menú principal";
            cerraSubmenu(); // ← quitado await

            foreach (Control ctrl in menuVertical.Controls)
            {
                if (ctrl is Button btn)
                {
                    _textosOriginales[btn.Name] = btn.Text;
                }
            }
        }
        #endregion

        #region uiHelpers
        protected void cerraSubmenu() // ← quitado async, no tenía ningún await
        {
            pnPesajes.Visible = false;
            pnProductos.Visible = false;
            pnUsers.Visible = false;
        }

        /// <summary>
        /// Si el menu esta colapsado tiene un width de 80px.
        /// Los textos de los botones se cambian para no mostrar nada.
        /// </summary>
        private void ColapsarMenu() // ← quitado async Task, no hay awaits reales
        {
            menuExpandido = false;
            menuVertical.Width = MENU_COLAPSADO;
            cerraSubmenu(); // ← ya no duplicado, una sola llamada

            foreach (Control ctrl in menuVertical.Controls)
            {
                if (ctrl is Button btn)
                    btn.Text = "";
            }
        }

        /// <summary>
        /// Si el menu esta expandido y el width es de 300, se devuelven los textos guardados a los botones.
        /// </summary>
        private void ExpandirMenuySubmenu(Panel panelNombre) // ← quitado async
        {
            menuExpandido = true;
            menuVertical.Width = MENU_EXPANDIDO;

            foreach (Control ctrl in menuVertical.Controls)
            {
                if (ctrl is Button btn)
                    btn.Text = _textosOriginales[btn.Name];
            }

            if (panelNombre.Visible == false)
            {
                cerraSubmenu(); // ← quitado await
                panelNombre.Visible = true;
            }
            else
            {
                panelNombre.Visible = false;
            }
        }

        private void ExpandirMenu() // ← quitado async
        {
            menuExpandido = true;
            menuVertical.Width = MENU_EXPANDIDO;

            foreach (Control ctrl in menuVertical.Controls)
            {
                if (ctrl is Button btn)
                    btn.Text = _textosOriginales[btn.Name];
            }
        }

        protected void abrirFormHijo(Form _formHijo) // ← quitado async Task, no hay awaits reales
        {
            if (_formActual != null)
                _formActual.Close();

            _formActual = _formHijo;
            _formHijo.TopLevel = false;
            _formHijo.FormBorderStyle = FormBorderStyle.None;
            _formHijo.Dock = DockStyle.Fill;
            pnContenedor.Controls.Add(_formHijo);
            pnContenedor.Tag = _formHijo;
            _formHijo.BringToFront();
            _formHijo.Show();
        }

        /// <summary>
        /// Lógica de la burger
        /// </summary>
        private void btnSlide_Click(object sender, EventArgs e) // ← quitado async
        {
            if (_menuAnimando) return; // ← bloquea clics rápidos

            _menuAnimando = true;
            btnSlide.Enabled = false;

            try
            {
                if (menuExpandido)
                    ColapsarMenu();
                else
                    ExpandirMenu();
            }
            finally
            {
                _menuAnimando = false;
                btnSlide.Enabled = true;
            }
        }
        #endregion

        #region EscaladoDPI
        private void EscalarImagenesBotones()
        {
            //float escala = DeviceDpi / 96f;
            //int tamano = (int)(48 * escala);

            var botones = new (Button btn, Image imagen)[]
            {
                (btnReporteria, Properties.Resources.informe),
                (btnPesaje,     Properties.Resources.camion),
                (btnProductos,  Properties.Resources.cajas),
                (btnUsuarios,   Properties.Resources.avatar),
            };

            foreach (var (btn, imagen) in botones)
            {
                int tamano = (int)(Math.Min(btn.Width, btn.Height) * 0.7f); 
                btn.Image = new Bitmap(imagen, tamano, tamano);
            }
        }

        private void EscalarPictureBox()
        {
            float escala = DeviceDpi / 96f;
            int tamano = (int)(Math.Min(btnSlide.Width, btnSlide.Height) * 0.7f);
            btnSlide.Image = new Bitmap(Properties.Resources.menu_hamburguesa_80, tamano, tamano);
            btnSlide.SizeMode = PictureBoxSizeMode.CenterImage;
        }
        #endregion

        #region BotonesSubmenus
        private void btnUsuarios_Click(object sender, EventArgs e) // ← quitado async
        {
            if (menuVertical.Width != MENU_EXPANDIDO)
                menuVertical.Width = MENU_EXPANDIDO;

            ExpandirMenuySubmenu(pnUsers);
        }

        private void btnProductos_Click(object sender, EventArgs e) // ← quitado async
        {
            if (menuVertical.Width != MENU_EXPANDIDO)
                menuVertical.Width = MENU_EXPANDIDO;

            ExpandirMenuySubmenu(pnProductos);
        }

        private void btnPesaje_Click(object sender, EventArgs e) // ← quitado async
        {
            if (menuVertical.Width != MENU_EXPANDIDO)
                menuVertical.Width = MENU_EXPANDIDO;

            ExpandirMenuySubmenu(pnPesajes);
        }

        private void btnBitacora_Click(object sender, EventArgs e) // ← quitado async
        {
            ColapsarMenu();
        }
        #endregion

        #region navegacion_entre_forms
        private void btnGestionEmpleados_Click(object sender, EventArgs e) // ← quitado async
        {
            abrirFormHijo(new GestiónEmpleados());
            lblHeader.Text = "Gestión de empleados";
            ColapsarMenu();
        }

        private void btnGestionUsuarios_Click(object sender, EventArgs e)
        {
            abrirFormHijo(new GestionUsuarios());
            lblHeader.Text = "Gestión de usuarios";
            ColapsarMenu();
        }

        private void btnGestionRoles_Click(object sender, EventArgs e)
        {
            ColapsarMenu();
        }

        private void btnGestionProductos_Click(object sender, EventArgs e)
        {
            abrirFormHijo(new GestionProductos());
            lblHeader.Text = "Gestión de productos";
            ColapsarMenu();
        }

        private void btnGestionProveedores_Click(object sender, EventArgs e)
        {
            abrirFormHijo(new GestionProveedores());
            lblHeader.Text = "Gestión de proveedores";
            ColapsarMenu();
        }

        private void btnGestionFabricantes_Click(object sender, EventArgs e)
        {
            abrirFormHijo(new GestionFabricantes());
            lblHeader.Text = "Gestión de fabricantes";
            ColapsarMenu();
        }

        private void btnMovimientosEntradas_Click(object sender, EventArgs e)
        {
            abrirFormHijo(new MovimientosyEntradas());
            lblHeader.Text = "Entradas y movimientos";
            ColapsarMenu();
        }
        #endregion

        private void button2_Click_1(object sender, EventArgs e)
        {
            ColapsarMenu();
        }

        private void btnCategorias_Click(object sender, EventArgs e)
        {
            abrirFormHijo(new GestionCategorias());
            lblHeader.Text = "Gestión de categorías";
            ColapsarMenu();
        }
    }
}