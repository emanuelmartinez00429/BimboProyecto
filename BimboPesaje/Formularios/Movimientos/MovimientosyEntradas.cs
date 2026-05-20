using System.Data;
using CapaDatos.Modelados.Pesajes;
using CapaDominio;
using Supabase.Realtime.PostgresChanges;
using BimboPesaje.Formularios.Productos;
using CapaDatos.Repositorios.productos_movimientos;

namespace BimboPesaje.Formularios.Movimientos
{
    public partial class MovimientosyEntradas : Form
    {
        // Estado de selección actual
        private int     _idMovimientoSeleccionado  = 0;
        private int     _idMovProductoSeleccionado = 0;
        private int     _idProductoSeleccionado    = 0;
        private decimal _pesoManifestadoActual     = 0;
        private int     _bultosTeóricosActual      = 0;

        // Bandera para evitar reload recursivo al asignar DataSource
        private bool _cargandoCamiones  = false;
        private bool _cargandoProductos = false;
        private bool _cerrando          = false;

        // Handlers de realtime (guardados para poder desuscribir)
        private Action<PostgresChangesResponse>? _handlerMov;
        private Action<PostgresChangesResponse>? _handlerMovProd;

        public MovimientosyEntradas()
        {
            InitializeComponent();

            // Eventos no conectados en el Designer
            button4.Click                    += Button4_Click;
            btnDescargar.Click               += BtnDescargar_Click;
            btnQuitarMovimiento.Click        += BtnQuitarMovimiento_Click;
            btnEditarMoviento.Click          += BtnEditarMovimento_Click;
            btnEditar.Click                  += BtnEditarCamion_Click;
            btnQuitarEntrada.Click           += btnQuitarEntrada_Click;
            dgvMateriaPrima.SelectionChanged += DgvMateriaPrima_SelectionChanged;
        }

        // ── Carga inicial ─────────────────────────────────────────────────────

        private async void MovimientosyEntradas_Load(object sender, EventArgs e)
        {
            ConfigurarGrids();
            await CargarMovimientosAsync();
            ConfigurarRealtime();
        }

        private void ConfigurarGrids()
        {
            // dgvEntradas
            dgvEntradas.AutoGenerateColumns = false;
            dgvEntradas.Columns.Clear();
            dgvEntradas.Columns.Add(ColTexto("IdPesaje", "IdPesaje", "Id"));
            dgvEntradas.Columns.Add(ColTexto("PesoBruto", "PesoBruto", "Bruto (kg)"));
            dgvEntradas.Columns.Add(ColTexto("TaraTotal", "TaraTotal", "Tara (kg)"));
            dgvEntradas.Columns.Add(ColTexto("PesoNeto", "PesoNeto", "Neto (kg)"));
            dgvEntradas.Columns.Add(ColTexto("NoBultos", "NoBultos", "Bultos"));
            dgvEntradas.Columns.Add(ColTexto("FechaEntrada", "FechaEntrada", "Fecha"));
            dgvEntradas.Columns.Add(ColTexto("HoraEntrada", "HoraEntrada", "Hora"));

            // dgvMateriaPrima
            dgvMateriaPrima.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "BultosRestantes",
                DataPropertyName = "BultosRestantes",
                HeaderText = "Bultos Rest.",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });
            dgvMateriaPrima.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PctPesoRestante",
                DataPropertyName = "PctPesoRestante",
                HeaderText = "% Restante",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });

            // dgvCamiones
            dgvCamiones.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NombreProveedor",
                DataPropertyName = "NombreProveedor",
                HeaderText = "Proveedor",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });
        }

        // Helper único — Fill en todas las columnas
        private static DataGridViewTextBoxColumn ColTexto(string name, string prop, string header)
            => new()
            {
                Name = name,
                DataPropertyName = prop,
                HeaderText = header,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 60,
                ReadOnly = true
            };
        /*
        private static DataGridViewTextBoxColumn ColTexto(string name, string prop, string header, int width)
            => new() { Name = name, DataPropertyName = prop, HeaderText = header, Width = width, ReadOnly = true };

        private static DataGridViewTextBoxColumn ColTextoFill(string name, string prop, string header)
            => new() { Name = name, DataPropertyName = prop, HeaderText = header,
                       AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true };
        */
        // ── Realtime ─────────────────────────────────────────────────────────

        private void ConfigurarRealtime()
        {
            _handlerMov     = _ => RecargarMovimientosSafe();
            _handlerMovProd = _ => RecargarProductosSafe();
            GestorRealtime.OnMovimientosChanged  += _handlerMov;
            GestorRealtime.OnMovProductosChanged += _handlerMovProd;
        }

        private void RecargarMovimientosSafe()
        {
            if (_cerrando || IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)(async () =>
                {
                    if (!_cerrando) await CargarMovimientosAsync();
                }));
            }
            catch { }
        }

        private void RecargarProductosSafe()
        {
            if (_cerrando || IsDisposed || !IsHandleCreated || _idMovimientoSeleccionado == 0) return;
            try
            {
                BeginInvoke((MethodInvoker)(async () =>
                {
                    if (!_cerrando) await CargarProductosAsync(_idMovimientoSeleccionado);
                }));
            }
            catch { }
        }

        // ── Carga de datos ───────────────────────────────────────────────────

        private async Task CargarMovimientosAsync()
        {
            _cargandoCamiones = true;
            try
            {
                var lista = await RepositorioMovimiento.ObtenerAbiertosAsync();

                var tabla = new DataTable();
                tabla.Columns.Add("IdEntrada",       typeof(int));
                tabla.Columns.Add("PlacaCamion");
                tabla.Columns.Add("NombreProveedor");
                tabla.Columns.Add("FechaAsignacion");

                foreach (var m in lista)
                    tabla.Rows.Add(
                        m.idMovimiento,
                        m.placaVehiculo ?? "",
                        m.nombreProveedor,
                        m.fechaAsignacion.ToString("dd/MM/yyyy")
                    );

                dgvCamiones.AutoGenerateColumns = false;
                dgvCamiones.DataSource          = tabla;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MYE] CargarMovimientos: {ex.Message}");
            }
            finally
            {
                _cargandoCamiones = false;
            }
        }

        private async Task CargarProductosAsync(int idMovimiento)
        {
            _cargandoProductos      = true;
            _idMovProductoSeleccionado = 0;
            dgvEntradas.DataSource  = null;

            try
            {
                // Vista de resumen (indicadores) + tabla base (para idProducto)
                var resumen   = await RepositorioMovimientoProducto.ObtenerResumenPorMovimientoAsync(idMovimiento);
                var productos = await RepositorioMovimientoProducto.ObtenerPorMovimientoAsync(idMovimiento);
                var idProdLookup = productos.ToDictionary(p => p.idMovProducto, p => p.idProducto);

                var tabla = new DataTable();
                tabla.Columns.Add("IdMovProducto",     typeof(int));
                tabla.Columns.Add("IdProducto",        typeof(int));
                tabla.Columns.Add("CodigoInternoProducto");
                tabla.Columns.Add("NombreProducto");
                tabla.Columns.Add("Proveedor_Producto");
                tabla.Columns.Add("EstadoProceso");
                tabla.Columns.Add("BultosRestantes",   typeof(decimal));
                tabla.Columns.Add("PctPesoRestante",   typeof(decimal));
                tabla.Columns.Add("PesoManifestado",   typeof(decimal));
                tabla.Columns.Add("BultosTeóricos",    typeof(int));

                foreach (var r in resumen)
                {
                    int idProd = idProdLookup.TryGetValue(r.idMovProducto, out var pid) ? pid : 0;
                    tabla.Rows.Add(
                        r.idMovProducto,
                        idProd,
                        r.codigoProducto,
                        r.nombreProducto,
                        r.nombreProveedor,
                        r.estado,
                        r.bultosRestantes,
                        r.pctPesoRestante,
                        r.pesoManifestado,
                        r.bultosTeóricos
                    );
                }

                dgvMateriaPrima.AutoGenerateColumns = false;
                dgvMateriaPrima.DataSource          = tabla;
                dgvMateriaPrima.Visible             = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MYE] CargarProductos: {ex.Message}");
            }
            finally
            {
                _cargandoProductos = false;
            }
        }

        private async Task CargarEntradasAsync(int idMovProducto)
        {
            try
            {
                var lista = await RepositorioEntrada.ObtenerPorMovProductoAsync(idMovProducto);

                var tabla = new DataTable();
                tabla.Columns.Add("IdPesaje",     typeof(int));
                tabla.Columns.Add("PesoBruto",    typeof(decimal));
                tabla.Columns.Add("TaraTotal",    typeof(decimal));
                tabla.Columns.Add("PesoNeto",     typeof(decimal));
                tabla.Columns.Add("NoBultos",     typeof(int));
                tabla.Columns.Add("FechaEntrada");
                tabla.Columns.Add("HoraEntrada");

                foreach (var ep in lista)
                    tabla.Rows.Add(
                        ep.idPesaje,
                        ep.pesoBruto,
                        ep.pesoTaraTotal,
                        ep.pesoNeto,
                        ep.numeroBultosRecibido ?? 0,
                        ep.fechaEntrada.ToString("dd/MM/yyyy"),
                        ep.horaEntrada.ToString("HH:mm")
                    );

                dgvEntradas.DataSource = tabla;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MYE] CargarEntradas: {ex.Message}");
            }
        }

        // ── Selección de camión ───────────────────────────────────────────────

        private async void dgvCamiones_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargandoCamiones || dgvCamiones.CurrentRow == null) return;

            var drv = dgvCamiones.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null) return;

            int id = Convert.ToInt32(drv["IdEntrada"]);
            if (id == _idMovimientoSeleccionado) return;

            _idMovimientoSeleccionado  = id;
            _idMovProductoSeleccionado = 0;
            _idProductoSeleccionado    = 0;
            dgvEntradas.DataSource     = null;

            await CargarProductosAsync(id);
        }

        // ── Selección de producto ─────────────────────────────────────────────

        private async void DgvMateriaPrima_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargandoProductos || dgvMateriaPrima.CurrentRow == null) return;

            var drv = dgvMateriaPrima.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null) return;

            int idMovProd = Convert.ToInt32(drv["IdMovProducto"]);
            if (idMovProd == _idMovProductoSeleccionado) return;

            _idMovProductoSeleccionado = idMovProd;
            _idProductoSeleccionado    = Convert.ToInt32(drv["IdProducto"]);
            _pesoManifestadoActual     = Convert.ToDecimal(drv["PesoManifestado"]);
            _bultosTeóricosActual      = Convert.ToInt32(drv["BultosTeóricos"]);

            await CargarEntradasAsync(idMovProd);
        }

        // ── Botones de camión (pnVehiculo) ───────────────────────────────────

        // Agregar nuevo camión
        private async void btnAgregar_Click(object sender, EventArgs e)
        {
            using var form = new FrmCrearMovimiento();
            if (form.ShowDialog(this) == DialogResult.OK)
                await CargarMovimientosAsync();
        }

        // Descargar = cerrar el movimiento completo
        private async void BtnDescargar_Click(object sender, EventArgs e)
        {
            if (_idMovimientoSeleccionado == 0)
            {
                MessageBox.Show("Seleccione un camión.", "Advertencia",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(
                    "¿Cerrar el movimiento completo de este camión?\nTodos sus productos deben estar cerrados.",
                    "Confirmar cierre",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                await RepositorioMovimiento.CerrarAsync(_idMovimientoSeleccionado);
                _idMovimientoSeleccionado  = 0;
                _idMovProductoSeleccionado = 0;
                dgvMateriaPrima.DataSource = null;
                dgvEntradas.DataSource     = null;
                await CargarMovimientosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cerrar movimiento: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Quitar = anular movimiento
        private async void Button4_Click(object sender, EventArgs e)
        {
            if (_idMovimientoSeleccionado == 0)
            {
                MessageBox.Show("Seleccione un camión.", "Advertencia",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(
                    "¿Anular este movimiento?\nEsta acción no se puede deshacer.",
                    "Confirmar anulación",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                await RepositorioMovimiento.AnularAsync(_idMovimientoSeleccionado);
                _idMovimientoSeleccionado  = 0;
                _idMovProductoSeleccionado = 0;
                dgvMateriaPrima.DataSource = null;
                dgvEntradas.DataSource     = null;
                await CargarMovimientosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al anular movimiento: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Editar camión (sin funcionalidad aún)
        private void BtnEditarCamion_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Edición de camión no disponible en esta versión.",
                "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Botones de producto (pnMovimiento) ───────────────────────────────

        // Agregar producto al camión seleccionado
        private async void btnAgregarMovimiento_Click(object sender, EventArgs e)
        {
            if (_idMovimientoSeleccionado == 0)
            {
                MessageBox.Show("Seleccione un camión primero.", "Advertencia",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new FrmAgregarProductoMovimiento(_idMovimientoSeleccionado);
            if (form.ShowDialog(this) == DialogResult.OK)
                await CargarProductosAsync(_idMovimientoSeleccionado);
        }

        // Pesar producto seleccionado
        private async void btnPesar_Click(object sender, EventArgs e)
        {
            if (_idMovProductoSeleccionado == 0)
            {
                MessageBox.Show("Seleccione un producto para pesar.", "Advertencia",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string placa    = (dgvCamiones.CurrentRow?.DataBoundItem as DataRowView)?["PlacaCamion"]?.ToString()    ?? "";
            string producto = (dgvMateriaPrima.CurrentRow?.DataBoundItem as DataRowView)?["NombreProducto"]?.ToString() ?? "";
            string proveedor = (dgvMateriaPrima.CurrentRow?.DataBoundItem as DataRowView)?["Proveedor_Producto"]?.ToString() ?? "";

            // Bucle sin recursión
            bool continuar = true;
            while (continuar)
            {
                using var form = new RegistrarPesos(
                    _idMovProductoSeleccionado,
                    _idProductoSeleccionado,
                    placa, producto, proveedor,
                    _pesoManifestadoActual,
                    _bultosTeóricosActual);

                if (form.ShowDialog(this) == DialogResult.OK)
                    continuar = form.SeguirPesando;
                else
                    break;
            }

            await CargarEntradasAsync(_idMovProductoSeleccionado);
            await CargarProductosAsync(_idMovimientoSeleccionado);
        }

        // Cerrar pesaje del producto seleccionado
        private async void btnGuardarMovimiento_Click(object sender, EventArgs e)
        {
            await CerrarProductoActualAsync();
        }

        // Quitar producto (solo si sin entradas aún)
        private void BtnQuitarMovimiento_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Para quitar un producto, primero elimine todos sus pesajes.",
                "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Editar movimiento-producto (sin funcionalidad aún)
        private void BtnEditarMovimento_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Edición de producto no disponible en esta versión.",
                "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Botones de entradas (panel1 en pnDgv) ────────────────────────────

        // Guardar Entrada = cerrar el producto (mismo que btnGuardarMovimiento)
        private async void btnGuardarEntrada_Click(object sender, EventArgs e)
        {
            await CerrarProductoActualAsync();
        }

        // Eliminar pesaje seleccionado
        private async void btnQuitarEntrada_Click(object sender, EventArgs e)
        {
            var drv = dgvEntradas.CurrentRow?.DataBoundItem as DataRowView;
            if (drv == null)
            {
                MessageBox.Show("Seleccione un pesaje.", "Advertencia",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("¿Eliminar este pesaje?", "Confirmar eliminación",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                int idPesaje = Convert.ToInt32(drv["IdPesaje"]);
                await RepositorioEntrada.EliminarAsync(idPesaje);
                await CargarEntradasAsync(_idMovProductoSeleccionado);
                await CargarProductosAsync(_idMovimientoSeleccionado);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al eliminar pesaje: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Editar pesaje (sin funcionalidad en esta versión)
        private void btnEditarEntrada_Click(object sender, EventArgs e)
        {
            MessageBox.Show("La edición de pesajes no está disponible en esta versión.",
                "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task CerrarProductoActualAsync()
        {
            if (_idMovProductoSeleccionado == 0)
            {
                MessageBox.Show("Seleccione un producto.", "Advertencia",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show("¿Cerrar el pesaje de este producto?", "Confirmar cierre",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                await RepositorioMovimientoProducto.CerrarAsync(_idMovProductoSeleccionado);
                await CargarProductosAsync(_idMovimientoSeleccionado);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cerrar producto: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _cerrando = true;
            try
            {
                if (_handlerMov     != null) GestorRealtime.OnMovimientosChanged  -= _handlerMov;
                if (_handlerMovProd != null) GestorRealtime.OnMovProductosChanged -= _handlerMovProd;
            }
            catch { }
            base.OnFormClosed(e);
        }
    }
}
