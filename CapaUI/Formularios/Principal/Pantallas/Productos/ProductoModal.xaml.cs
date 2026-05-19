using CapaDatos.Modelados.Productos;
using CapaDatos.Repositorios.productos_movimientos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Producto = CapaDatos.Modelados.Productos.Productos;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public partial class ProductoModal : System.Windows.Controls.UserControl
    {
        private readonly Producto? _producto;
        private readonly bool _esNuevo;

        public event Action? Cerrado;
        public event Action? Guardado;

        public ProductoModal(Producto? producto)
        {
            InitializeComponent();
            _producto = producto;
            _esNuevo  = producto == null;
            Loaded   += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICI\u00D3N";
            TxtModalTitle.Text   = _esNuevo ? "Crear producto"  : "Editar producto";

            try
            {
                var uri = new Uri("pack://application:,,,/CapaUI;component/Resources/bimbo_no_bg.png");
                ModalLogo.Source = new BitmapImage(uri);
            }
            catch { }

            try
            {
                var prods = await RepositorioProducto.obtenerProductosJoin();
                var fabricantes = prods
                    .Where(p => p.Fabricante != null)
                    .Select(p => p.Fabricante!)
                    .GroupBy(f => f.idFabricante)
                    .Select(g => g.First())
                    .OrderBy(f => f.nombreFabricante)
                    .ToList();
                CmbFabricanteModal.Items.Clear();
                foreach (var f in fabricantes)
                    CmbFabricanteModal.Items.Add(new ComboBoxItem { Content = f.nombreFabricante, Tag = f.idFabricante });
            }
            catch { }

            try
            {
                var cats = await RepositorioCategoria.ObtenerCategorias();
                CmbCategoriaModal.Items.Clear();
                foreach (var c in cats.OrderBy(x => x.nombreCategoria))
                    CmbCategoriaModal.Items.Add(new ComboBoxItem { Content = c.nombreCategoria, Tag = c.idCategoria });
            }
            catch { }

            if (!_esNuevo && _producto != null)
            {
                TxtCodigo.Text       = _producto.codigoProducto ?? "";
                TxtNombre.Text       = _producto.nombreProducto ?? "";
                TxtPresentacion.Text = _producto.nombre_Presentacion;
                TxtContenido.Text    = _producto.contenidoProducto ?? "";

                foreach (ComboBoxItem item in CmbFabricanteModal.Items)
                    if (item.Tag is int id && id == _producto.idFabricante) { CmbFabricanteModal.SelectedItem = item; break; }

                foreach (ComboBoxItem item in CmbCategoriaModal.Items)
                    if (item.Tag is int id && id == _producto.idCategoria) { CmbCategoriaModal.SelectedItem = item; break; }

                foreach (ComboBoxItem item in CmbPaisModal.Items)
                    if (item.Content?.ToString() == _producto.nombre_Pais) { CmbPaisModal.SelectedItem = item; break; }

                RbActivo.IsChecked   = _producto.idEstado == 1;
                RbInactivo.IsChecked = _producto.idEstado != 1;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCodigo.Text) || string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                System.Windows.MessageBox.Show("C\u00F3digo y nombre son obligatorios.", "Validaci\u00F3n",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            BtnGuardar.IsEnabled = false;
            try
            {
                int idFabricante = 0;
                if (CmbFabricanteModal.SelectedItem is ComboBoxItem fi && fi.Tag is int fid) idFabricante = fid;
                int idCategoria = 0;
                if (CmbCategoriaModal.SelectedItem is ComboBoxItem ci && ci.Tag is int cid) idCategoria = cid;

                var datos = new ProductosInsertar
                {
                    idProducto        = _esNuevo ? 0 : _producto!.idProducto,
                    codigoProducto    = TxtCodigo.Text.Trim(),
                    nombreProducto    = TxtNombre.Text.Trim(),
                    contenidoProducto = TxtContenido.Text.Trim(),
                    idFabricante      = idFabricante,
                    idCategoria       = idCategoria,
                    idEstado          = RbActivo.IsChecked == true ? 1 : 0,
                    idPais            = 0,
                    idPresentacion    = _esNuevo ? 0 : _producto!.idPresentacion,
                    pesoTeorico       = 0,
                    idTara            = 0,
                };

                if (_esNuevo)
                {
                    await RepositorioProducto.ingresarProducto(datos);
                }
                else
                {
                    var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
                    await client.From<ProductosInsertar>()
                                .Where(p => p.idProducto == datos.idProducto)
                                .Set(p => p.codigoProducto!, datos.codigoProducto)
                                .Set(p => p.nombreProducto!, datos.nombreProducto)
                                .Set(p => p.contenidoProducto!, datos.contenidoProducto)
                                .Set(p => p.idFabricante, datos.idFabricante)
                                .Set(p => p.idCategoria, datos.idCategoria)
                                .Set(p => p.idEstado, datos.idEstado)
                                .Update();
                }

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error al guardar: " + ex.Message, "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
