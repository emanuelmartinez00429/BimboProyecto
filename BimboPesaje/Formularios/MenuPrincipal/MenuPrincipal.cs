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

        /// <summary>
        /// Variable para guardar los textos de los botones
        /// </summary>
        private Dictionary<string, string> _textosOriginales = new Dictionary<string, string>();

        /// <summary>
        /// varaibles para el ui del menu vertical
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
        private async void MenuPrincipal_Load(object sender, EventArgs e)
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            lblHeader.Text = "Men� principal";
            await cerraSubmenu();
            //recibiendo los nombres de los botones para guardarlos en la variable
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
        protected async Task cerraSubmenu()
        {
            pnPesajes.Visible = false;
            pnProductos.Visible = false;
            pnUsers.Visible = false;
        }
        /// <summary>
        ///  si el menu esta como false (esta colapsado) y tiene un width de 80 px.
        /// los textos de los botones se cambian para no mostrar nada 
        /// </summary>
        private async Task ColapsarMenu()
        {
            menuExpandido = false;
            menuVertical.Width = MENU_COLAPSADO;
            await cerraSubmenu();

            // Ocultar textos de los botones
            foreach (Control ctrl in menuVertical.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.Text = "";
                }
            }
        }
        /// <summary>
        /// Si el menu esta expandido, y el width es de 300, se devuelven los textos guardados a los botones
        /// </summary>
        private async void ExpandirMenuySubmenu(Panel panelNombre)
        {
            menuExpandido = true;
            menuVertical.Width = MENU_EXPANDIDO;

            foreach (Control ctrl in menuVertical.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.Text = _textosOriginales[btn.Name];
                }
            }
            if (panelNombre.Visible == false)
            {
                await cerraSubmenu();
                panelNombre.Visible = true;
            }
            else
            {
                panelNombre.Visible = false;
            }
        }
        private async void ExpandirMenu()
        {
            menuExpandido = true;
            menuVertical.Width = MENU_EXPANDIDO;

            foreach (Control ctrl in menuVertical.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.Text = _textosOriginales[btn.Name];
                }
            }
        }

        protected async Task abrirFormHijo(Form _formHijo)
        {
            if (_formActual != null)
            {
                _formActual.Close();
            }

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
        /// Aqu� lo que estoy haciendo es que estoy comprobando que el submenu este abierto, si esta cerrado, se cierra
        /// cualquiera que haya estado abierto y se abre
        /// </summary>
        /// <param name="subMenu"></param>
        /// <returns></returns>
        protected async Task mostrarSubmenu(Panel subMenu)
        {

            if (subMenu.Visible == false)
            {
                await cerraSubmenu();
                subMenu.Visible = true;
            }
            else
            {
                subMenu.Visible = false;
            }
        }

        /// <summary>
        /// Logica de la burger
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnSlide_Click(object sender, EventArgs e)
        {
            if (menuExpandido)
            {
                await ColapsarMenu();
                await cerraSubmenu();
            }
            else
                ExpandirMenu();
        }
        #endregion

        #region BotonesSubmenus
        /// <summary>
        /// Si se presiona un boton dentro de este bloque, el menu vertical siempre va a cambiar a 300 px aun que este 
        /// cerrado
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// 
        private async void btnUsuarios_Click(object sender, EventArgs e)
        {
            if (menuVertical.Width == 300)
            {
                ExpandirMenuySubmenu(pnUsers);
            }
            else
            {
                menuVertical.Width = 300;
                ExpandirMenuySubmenu(pnUsers);

            }
        }

        private async void btnProductos_Click(object sender, EventArgs e)
        {
            if (menuVertical.Width == 300)
            {
                ExpandirMenuySubmenu(pnProductos);
            }
            else
            {
                menuVertical.Width = 300;
                ExpandirMenuySubmenu(pnProductos);

            }
        }

        private async void btnPesaje_Click(object sender, EventArgs e)
        {
            if (menuVertical.Width == 300)
            {
                ExpandirMenuySubmenu(pnPesajes);
            }
            else
            {
                menuVertical.Width = 300;
                ExpandirMenuySubmenu(pnPesajes);

            }
        }
        private async void btnBitacora_Click(object sender, EventArgs e)
        {
            await ColapsarMenu();
        }
        #endregion

        #region navegacion_entre_forms
        private async void btnGestionEmpleados_Click(object sender, EventArgs e)
        {
            await abrirFormHijo(new GestiónEmpleados());
            lblHeader.Text = "Gestión de empleados";
            await ColapsarMenu();
        }

        private async void btnGestionUsuarios_Click(object sender, EventArgs e)
        {
            await abrirFormHijo(new GestionUsuarios());
            lblHeader.Text = "Gestión de usuarios";
            await ColapsarMenu();
        }

        private async void btnGestionRoles_Click(object sender, EventArgs e)
        {
            await ColapsarMenu();
        }



        private async void btnGestionProductos_Click(object sender, EventArgs e)
        {
            await abrirFormHijo(new GestionProductos());
            lblHeader.Text = "Gestión de productos";
            await ColapsarMenu();

        }

        private async void btnGestionProveedores_Click(object sender, EventArgs e)
        {
            await abrirFormHijo(new GestionProveedores());
            lblHeader.Text = "Gestión de proveedores";
            await ColapsarMenu();
        }

        private async void btnGestionFabricantes_Click(object sender, EventArgs e)
        {
            await abrirFormHijo(new GestionFabricantes());
            lblHeader.Text = "Gestión de fabricantes";
            await ColapsarMenu();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            await ColapsarMenu();
        }

        private async void btnMovimientosEntradas_Click(object sender, EventArgs e)
        {
            await abrirFormHijo(new MovimientosyEntradas());
            lblHeader.Text = "Entradas y movimientos";
            await ColapsarMenu();
        }
        #endregion

        private void button2_Click_1(object sender, EventArgs e)
        {
            ColapsarMenu();
        }

        private async void btnCategorias_Click(object sender, EventArgs e)
        {
            await abrirFormHijo(new GestionCategorias());
            lblHeader.Text = "Gestión de categorías";
            await ColapsarMenu();
        }
    }
}
