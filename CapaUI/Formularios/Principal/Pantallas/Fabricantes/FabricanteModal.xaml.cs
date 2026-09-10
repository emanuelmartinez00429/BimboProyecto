using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
using CapaUI.Core.Seguridad;
using CapaAplicacion.Common;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Fabricantes.Dtos;
using CapaAplicacion.Fabricantes.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CapaUI.Formularios.Principal.Pantallas.Fabricantes
{
    public partial class FabricanteModal : System.Windows.Controls.UserControl
    {
        private readonly IFabricanteRepository _repo = null!;
        private readonly ICatalogoRepository   _catalogos = null!;
        private readonly FabricanteDto?        _fabricante;
        private readonly bool                  _esNuevo;
        private ValidadorFormulario            _validador = null!;
        private readonly SolicitudIdempotente  _solicitud = new();

        // El texto de TxtProveedor es solo la etiqueta visible; lo que se
        // persiste es este id. Nullable: proveedor es opcional por diseño.
        private int? _idProveedor;

        private SelectorCatalogoModal? _selectorAbierto;

        /// <summary>
        /// Qué tenía el foco antes de abrir la tabla de selección (normalmente la
        /// propia lupa). Al cerrarla hay que devolvérselo porque el elemento
        /// enfocado vivía dentro del selector, que se destruye.
        /// </summary>
        private IInputElement? _focoPrevio;

        public event Action? Cerrado;
        public event Action? Guardado;

        /// <summary>
        /// Constructor sin parámetros solo para el diseñador de Visual Studio, que
        /// instancia el control por acá. Deja los servicios en <c>null!</c> porque este
        /// camino no opera el modal. Ver <c>ProductosCargaModal</c> y ADR-028.
        /// </summary>
        public FabricanteModal()
        {
            _repo      = null!;
            _catalogos = null!;
            InitializeComponent();
        }

        public FabricanteModal(IFabricanteRepository repo, FabricanteDto? fabricante)
        {
            InitializeComponent();

            _repo       = repo;
            _catalogos  = App.Services.GetRequiredService<ICatalogoRepository>();
            _fabricante = fabricante;
            _esNuevo    = fabricante == null;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtNombre, "El nombre").Segun(ReglasFabricante.Nombre)
                .Campo(TxtDescripcion, "La descripción").Segun(ReglasFabricante.Descripcion)
                .Catalogo(TxtProveedor, "El proveedor", () => _idProveedor).Obligatorio()
                .ValidarAlSalirDelCampo();

            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear fabricante"  : "Editar fabricante";

            var rPaises = await _repo.GetPaisesAsync();
            if (rPaises.Success)
            {
                CmbPais.Items.Clear();
                CmbPais.Items.Add(new ComboBoxItem { Content = "(Ninguno)", Tag = (int?)null });
                foreach (var p in rPaises.Value!)
                    CmbPais.Items.Add(new ComboBoxItem { Content = p.Nombre, Tag = p.Id });
            }
            else
            {
                MessageBox.Show($"No se pudieron cargar los países.\n{rPaises.Error}",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnGuardar.IsEnabled = false;
            }

            if (!_esNuevo && _fabricante != null)
            {
                TxtNombre.Text       = _fabricante.Nombre;
                TxtDescripcion.Text  = _fabricante.Descripcion;

                _idProveedor      = _fabricante.IdProveedor;
                TxtProveedor.Text = _fabricante.NombreProveedor;

                foreach (ComboBoxItem item in CmbPais.Items)
                    if (item.Tag is int paisId && paisId == _fabricante.IdPais) { CmbPais.SelectedItem = item; break; }

                RbActivo.IsChecked   = _fabricante.IdEstado == EstadoRegistro.Activo;
                RbInactivo.IsChecked = _fabricante.IdEstado != EstadoRegistro.Activo;
            }
            else
            {
                CmbPais.SelectedIndex = 0;
            }

            // Foco en el primer campo al abrir: el usuario no tiene que
            // clickear nada para empezar a escribir.
            TxtNombre.Focus();
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            // Con el selector abierto, "Volver"/✕ cierra la tabla y vuelve al
            // formulario, no el modal entero.
            if (_selectorAbierto is not null) { CerrarSelector(); return; }
            Cerrado?.Invoke();
        }

        // ── Selector de proveedor (lupa + tabla paginada con buscador) ─────────

        private void BuscarProveedor_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Proveedores(_catalogos), item =>
            {
                _idProveedor      = item.Id;
                TxtProveedor.Text = item.Nombre;
            });

        /// <summary>
        /// Cambia el contenido del modal por la tabla del catálogo. No se
        /// superpone: el formulario se colapsa y el marco toma un alto fijo para
        /// la tabla; al elegir un item vuelve el formulario a su alto natural.
        /// Mismo patrón que ProductoModal / CamionModal.
        /// </summary>
        private void AbrirSelector(CatalogoConfig cfg, Action<FiltroItem> alSeleccionar)
        {
            CerrarSelector();

            var selector = new SelectorCatalogoModal(cfg);
            selector.Cerrado      += CerrarSelector;
            selector.Seleccionado += item => alSeleccionar(item);

            _focoPrevio             = Keyboard.FocusedElement;
            _selectorAbierto        = selector;
            SelectorHost.Content    = selector;
            SelectorHost.Visibility = Visibility.Visible;
            FormHost.Visibility     = Visibility.Collapsed;
            RootGrid.Height         = 760;   // alto estable para la tabla; el form vuelve a auto al cerrar
        }

        private void CerrarSelector()
        {
            _selectorAbierto?.Dispose();
            _selectorAbierto        = null;
            SelectorHost.Content    = null;
            SelectorHost.Visibility = Visibility.Collapsed;
            FormHost.Visibility     = Visibility.Visible;
            RootGrid.Height         = double.NaN;

            if (_focoPrevio is UIElement anterior && anterior.Focus()) return;
            TxtNombre.Focus();
        }

        /// <summary>Doble clic en el campo de solo lectura abre el selector (mouse).</summary>
        private void TxtCatalogo_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement campo && campo.Tag is Button lupa)
            {
                e.Handled = true;
                lupa.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        /// <summary>Lo mismo por teclado: al llegar con Tab, Enter o Espacio abren el selector.</summary>
        private void TxtCatalogo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            if (sender is FrameworkElement campo && campo.Tag is Button lupa)
            {
                e.Handled = true;
                lupa.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!_validador.Validar()) return;

            if (!ConfirmacionEstado.Confirmar(
                    esNuevo:        _esNuevo,
                    estabaActivo:   !_esNuevo && _fabricante!.IdEstado == EstadoRegistro.Activo,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "fabricante",
                    nombreRegistro: TxtNombre.Text.Trim()))
                return;

            // Guardar es un viaje de red: sin este aviso la espera se lee como
            // que la aplicacion se colgo.
            var etiquetaGuardar  = BtnGuardar.Content;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";
            try
            {
                int? idProveedor = _idProveedor;
                int? idPais      = CmbPais.SelectedItem is ComboBoxItem ci && ci.Tag is int cv ? cv : null;

                var dto = new FabricanteDto
                {
                    Id          = _esNuevo ? 0 : _fabricante!.Id,
                    Nombre      = TxtNombre.Text.Trim(),
                    Descripcion = TxtDescripcion.Text.Trim(),
                    IdProveedor = idProveedor,
                    IdPais      = idPais,
                    IdEstado    = RbActivo.IsChecked == true ? EstadoRegistro.Activo : EstadoRegistro.Inactivo,
                };

                bool exito;
                string error;

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto, _solicitud.Obtener("crear_fabricante", dto), CancellationToken.None);
                    (exito, error) = (r.Success, r.Error);
                }
                else
                {
                    var r = await _repo.UpdateAsync(dto, _solicitud.Obtener("actualizar_fabricante", dto), CancellationToken.None);
                    (exito, error) = (r.Success, r.Error);

                    if (exito && _fabricante != null && dto.IdEstado != _fabricante.IdEstado)
                    {
                        var rEstado = await _repo.CambiarEstadoAsync(
                            dto.Id,
                            dto.IdEstado,
                            _solicitud.Obtener("cambiar_estado_fabricante", new { dto.Id, dto.IdEstado }),
                            CancellationToken.None);
                        (exito, error) = (rEstado.Success, rEstado.Error);
                    }
                }

                if (!exito)
                {
                    ErroresRepositorio.Mostrar(error,
                        "Ya existe un fabricante con ese nombre.", TxtNombre);
                    return;
                }
                _solicitud.Confirmar();

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                ErroresRepositorio.MostrarInesperado(ex);
            }
            finally
            {
                BtnGuardar.Content   = etiquetaGuardar;
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
