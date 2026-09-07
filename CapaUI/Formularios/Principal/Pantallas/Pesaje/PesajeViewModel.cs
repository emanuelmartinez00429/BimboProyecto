using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapaAplicacion.Pesaje.Dtos;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDominio.Reportes;
using CapaUI.Core.Empresa;
using CapaUI.Core.MVVM;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje
{
    /// <summary>
    /// Estado y lógica de "Recepción de Materia Prima" (Fase 2, persistencia real).
    /// Lee/escribe en movimientos / movimiento_productos / entradas_producto vía IPesajeRepository.
    /// </summary>
    public partial class PesajeViewModel : ObservableObject, IDisposable
    {
        /// <summary>
        /// Camiones físicos simultáneos en el andén. Se cuenta por <b>placa distinta</b>,
        /// no por movimiento: un camión que trae carga de dos proveedores son dos
        /// recepciones en la base pero un solo camión en la puerta.
        /// </summary>
        public const int MaxCamiones = 5;

        private readonly IPesajeRepository _repo;
        private readonly IUsuarioSesionService _sesionService;
        private readonly IReportGeneratorService _reportGenerator;
        private readonly IReporteRepository _reporteRepository;
        private readonly LogoEmpresaCache _logoCache;

        public ObservableCollection<CamionPesaje> Camiones { get; } = new();

        /// <summary>
        /// RangeObservableCollection y no ObservableCollection: se reconstruye entera en
        /// cada cambio de producto/camión (ver <see cref="RecalcularFilas"/>), y con
        /// Clear()+Add() por fila cada reconstrucción disparaba un CollectionChanged por
        /// fila — visible como demora al cambiar de producto con varias filas.
        /// </summary>
        public RangeObservableCollection<EntradaPesaje> FilasEntradas { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CamionCerrado))]
        [NotifyPropertyChangedFor(nameof(HayCamion))]
        private CamionPesaje? _selectedCamion;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HayProducto))]
        private ProductoCamion? _selectedProducto;

        [ObservableProperty] private EntradaPesaje? _selectedEntrada;
        [ObservableProperty] private string _vistaEntradas = "producto";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MostrarEstadoVacio))]
        private bool _isLoading;

        [ObservableProperty] private bool _isGeneratingReport;

        /// <summary>
        /// Hay al menos un camión (placa) con 2+ recepciones abiertas en la lista. Controla
        /// que TODOS los encabezados de placa se muestren en <see cref="Camiones"/> —
        /// incluidos los camiones de 1 sola recepción — apenas exista alguno agrupado, en
        /// vez de decidirlo grupo por grupo.
        /// </summary>
        [ObservableProperty] private bool _hayPlacaCompartida;

        /// <summary>
        /// Cambiar de camión sí pega a la base (<see cref="CargarProductosAsync"/>) — a
        /// diferencia de cambiar de producto, que es en memoria. Sin este flag la tabla de
        /// productos/entradas se quedaba mostrando el camión anterior hasta que respondía
        /// la consulta, sin ningún indicio de que algo estaba pasando.
        /// </summary>
        [ObservableProperty] private bool _cargandoProductos;

        public bool HayCamion     => SelectedCamion is not null;
        public bool HayProducto   => SelectedProducto is not null;
        public bool CamionCerrado => SelectedCamion?.Estado == "Cerrado";
        public string ModoEfectivo => SelectedProducto is not null ? VistaEntradas : "camion";

        /// <summary>Placas distintas abiertas — camiones físicos, no movimientos.</summary>
        private int PlacasAbiertas => Camiones
            .Where(c => c.Estado == "Abierto")
            .Select(c => (c.Placa ?? "").Trim().ToUpperInvariant())
            .Distinct()
            .Count();

        public int    CamionesCount      => PlacasAbiertas;
        public string CamionesTexto      => $"{PlacasAbiertas}/{MaxCamiones}";
        public bool   PuedeAgregarCamion => PlacasAbiertas < MaxCamiones;

        /// <summary>
        /// Movimientos abiertos, no placas: "Cerrar todos" cierra recepción por
        /// recepción, así que acá sí se cuenta de a una.
        /// </summary>
        public int    CamionesActivos    => Camiones.Count(c => c.Estado == "Abierto");

        // ── Totales de la fila al pie de Entradas ─────────────────────────────
        // Suman lo que ya está en memoria (FilasEntradas), que es exactamente lo
        // que la grilla tiene a la vista: cambian solos al alternar entre
        // "Producto actual" y "Todo el camión". No consultan al repositorio.
        public double TotalBruto        => FilasEntradas.Sum(e => e.Bruto);
        public double TotalTara         => FilasEntradas.Sum(e => e.TaraTotal);
        public double TotalTaraExtra    => FilasEntradas.Sum(e => e.TaraExtra);
        public double TotalNeto         => FilasEntradas.Sum(e => e.Neto);

        /// <summary>El dato real cuando existe, si no la estimación — igual que <see cref="EntradaPesaje.BultosTexto"/>.</summary>
        public double TotalBultos       => FilasEntradas.Sum(e => e.BultosCapturados ?? e.BultosTeoricos ?? 0);

        public string TotalPesajesTexto => $"{FilasEntradas.Count} pesaje(s)";

        /// <summary>
        /// No hay ninguna descarga en curso: la pantalla muestra el estado vacío con el
        /// botón para iniciar el proceso, en vez de tres paneles vacíos sin contexto.
        /// "Descargándose" = camión abierto.
        /// <para/>
        /// El <c>!IsLoading</c> es obligatorio: mientras se consultan los camiones la
        /// colección está vacía, así que sin esa guarda el estado vacío aparecía un
        /// instante en cada carga aunque sí hubiera camiones abiertos — se veía como
        /// si se abriera solo el formulario de iniciar descarga y después cambiara.
        /// </summary>
        public bool MostrarEstadoVacio => !IsLoading && CamionesActivos == 0;

        public event Action<string>? Toast;

        public PesajeViewModel(
            IPesajeRepository repo, 
            IUsuarioSesionService sesionService,
            IReportGeneratorService reportGenerator,
            IReporteRepository reporteRepository,
            LogoEmpresaCache logoCache)
        {
            _repo = repo;
            _sesionService = sesionService;
            _reportGenerator = reportGenerator;
            _reporteRepository = reporteRepository;
            _logoCache = logoCache;
        }

        public async Task<bool> GenerarReportePesajesAsync(
            ReportFormat formato,
            string rutaFinal,
            IReadOnlyList<CamionPesaje> camionesAExportar,
            CancellationToken ct = default)
        {
            if (camionesAExportar == null || camionesAExportar.Count == 0)
            {
                Toast?.Invoke("No hay camiones seleccionados para generar el reporte.");
                return false;
            }

            var sesion = _sesionService.SesionActual;
            if (sesion is null)
            {
                Toast?.Invoke("No hay una sesión activa; no se puede generar el reporte.");
                return false;
            }

            string? directorio = Path.GetDirectoryName(rutaFinal);
            if (string.IsNullOrWhiteSpace(directorio) || !Directory.Exists(directorio))
            {
                Toast?.Invoke("La ubicación elegida para el reporte no es válida.");
                return false;
            }

            IsGeneratingReport = true;
            string rutaTemporal = Path.Combine(
                directorio,
                $".{Path.GetFileName(rutaFinal)}.{Guid.NewGuid():N}.tmp");

            try
            {
                DateTime generado = DateTime.Now;
                string tipo = formato == ReportFormat.Pdf ? "PDF" : "Excel";
                string nombre = $"Pesado de Insumos BES - {generado:yyyyMMdd-HHmmss}";

                byte[]? logoBytes = null;
                var rutaLogo = _logoCache.ObtenerRutaCacheadaSinRed();
                if (rutaLogo != null && File.Exists(rutaLogo))
                {
                    try { logoBytes = await File.ReadAllBytesAsync(rutaLogo, ct); } catch { }
                }

                // Construcción de filas consolidadas (un producto = una fila consolidada)
                var rows = new List<IReadOnlyList<object?>>();
                double totalManifestado = 0;
                double totalBruto = 0;
                double totalTara = 0;
                double totalNeto = 0;
                double totalBultos = 0;

                var metadataFiltros = new List<ReportMetadataDto>();
                if (camionesAExportar.Count == 1)
                {
                    var c = camionesAExportar[0];
                    metadataFiltros.Add(new("Placa del camión", c.Placa));
                    metadataFiltros.Add(new("Proveedor", c.Proveedor));
                    metadataFiltros.Add(new("Estado del camión", c.Estado));
                    if (!string.IsNullOrWhiteSpace(c.FechaAsignacion))
                        metadataFiltros.Add(new("Fecha de asignación", c.FechaAsignacion));
                }
                else
                {
                    metadataFiltros.Add(new("Alcance", $"{camionesAExportar.Count} camiones seleccionados"));
                    metadataFiltros.Add(new("Placas", string.Join(", ", camionesAExportar.Select(c => c.Placa).Distinct())));
                }

                var idsMovimientos = new List<int>();

                // P-031 G6: Si hay camiones sin productos cargados, consultarlos en paralelo
                // en vez de hacer N viajes de red en serie (Task.WhenAll).
                var tareasCarga = camionesAExportar
                    .Where(c => c.Productos == null || c.Productos.Count == 0)
                    .Select(async c =>
                    {
                        var prodRes = await _repo.GetProductosAsync(c.Id, ct);
                        if (prodRes.Success && prodRes.Value != null)
                        {
                            foreach (var pDto in prodRes.Value)
                            {
                                var pModel = MapProducto(pDto, c.Proveedor);
                                c.Productos.Add(pModel);
                            }
                        }
                    });
                await Task.WhenAll(tareasCarga);

                foreach (var camion in camionesAExportar)
                {
                    idsMovimientos.Add(camion.Id);

                    foreach (var prod in camion.Productos)
                    {
                        // Totales del producto consolidado
                        double pesoBrutoProd = prod.Entradas.Sum(e => e.Bruto);
                        double pesoTaraProd  = prod.Entradas.Sum(e => e.TaraTotal);
                        double pesoNetoProd  = prod.Entradas.Sum(e => e.Neto); // o prod.PesoRecibido
                        double pesoManifestadoProd = prod.PesoManifestado;
                        double bultosProd = prod.Entradas.Sum(e => e.BultosCapturados ?? e.BultosTeoricos ?? 0);
                        if (bultosProd <= 0 && prod.BultosRecibidos > 0)
                            bultosProd = prod.BultosRecibidos;

                        double difKg = pesoNetoProd - pesoManifestadoProd;
                        double difPct = pesoManifestadoProd > 0 
                            ? (difKg / pesoManifestadoProd) 
                            : 0;

                        totalManifestado += pesoManifestadoProd;
                        totalBruto       += pesoBrutoProd;
                        totalTara        += pesoTaraProd;
                        totalNeto        += pesoNetoProd;
                        totalBultos      += bultosProd;

                        // Columnas según Figura 28 ("Pesado de Insumos BES"):
                        // 1. Fecha Asignación
                        // 2. Placa (si son varios camiones) o Producto
                        // 3. Producto
                        // 4. Proveedor
                        // 5. Bultos Recibidos (Aprox.)
                        // 6. Peso Teórico / Manifestado
                        // 7. Peso Bruto
                        // 8. Peso Tara
                        // 9. Peso Recibido / Neto
                        // 10. Diferencia (KG)
                        // 11. Diferencia (%)
                        rows.Add(new object?[]
                        {
                            camion.FechaAsignacion,
                            camion.Placa,
                            $"{prod.ProductoCodigo} - {prod.ProductoNombre}".Trim(' ', '-'),
                            string.IsNullOrWhiteSpace(prod.ProveedorNombre) ? camion.Proveedor : prod.ProveedorNombre,
                            bultosProd,
                            pesoManifestadoProd,
                            pesoBrutoProd,
                            pesoTaraProd,
                            pesoNetoProd,
                            difKg,
                            difPct
                        });
                    }
                }

                double difTotalKg = totalNeto - totalManifestado;
                double difTotalPct = totalManifestado > 0 ? (difTotalKg / totalManifestado) : 0;

                var documento = new TabularReportDto
                {
                    Title = "Pesado de Insumos BES",
                    GeneratedAt = generado,
                    Author = new ReportAuthorDto
                    {
                        Email = sesion.Email,
                        Rol = sesion.NombreRol,
                    },
                    Branding = new ReportBrandingDto
                    {
                        CompanyName = "Bimbo Honduras",
                        LogoBytes = logoBytes,
                    },
                    SheetName = "Pesaje de Insumos",
                    Landscape = true,
                    Filters = metadataFiltros,
                    Columns = new List<ReportColumnDto>
                    {
                        new("FECHA ASIG.", "dd/MM/yyyy", 2.2),
                        new("PLACA", null, 1.8),
                        new("PRODUCTO", null, 4.2),
                        new("PROVEEDOR", null, 3.2),
                        new("BULTOS (APROX)", "N2", 2.2),
                        new("PESO MANIFESTADO", "N2", 2.5),
                        new("PESO BRUTO", "N2", 2.2),
                        new("PESO TARA", "N2", 2.0),
                        new("PESO RECIBIDO", "N2", 2.4),
                        new("DIF. (KG)", "N2", 2.0),
                        new("DIF. (%)", "P2", 1.8),
                    },
                    Rows = rows,
                    Totals = new List<ReportTotalDto>
                    {
                        new("Total Bultos Recibidos", totalBultos, "N2"),
                        new("Total Peso Manifestado", totalManifestado, "N2"),
                        new("Total Peso Bruto", totalBruto, "N2"),
                        new("Total Peso Tara", totalTara, "N2"),
                        new("Total Peso Recibido (Neto)", totalNeto, "N2"),
                        new("Diferencia Total (KG)", difTotalKg, "N2"),
                        new("Diferencia Total (%)", difTotalPct, "P2"),
                    }
                };

                var generadoResult = await _reportGenerator.GenerateAsync(documento, formato, ct);
                if (!generadoResult.Success)
                {
                    Toast?.Invoke($"Error al generar reporte: {generadoResult.Error}");
                    return false;
                }

                await File.WriteAllBytesAsync(rutaTemporal, generadoResult.Value!, ct);

                // Auditoría en base de datos vía RPC
                string parametrosJson = JsonConvert.SerializeObject(new
                {
                    origen = "pesaje_materia_prima",
                    ids_movimientos = idsMovimientos.ToArray(),
                    cantidad_camiones = camionesAExportar.Count,
                    cantidad_productos = rows.Count,
                    total_neto = totalNeto,
                    total_manifestado = totalManifestado,
                    diferencia_kg = difTotalKg
                });

                var registro = await _reporteRepository.RegistrarAsync(new ReporteRegistroDto
                {
                    NombreReporte = nombre,
                    TipoReporte = tipo,
                    Descripcion = $"Reporte Pesado de Insumos BES ({camionesAExportar.Count} camión/camiones, {rows.Count} producto(s)).",
                    FechaDesde = generado.Date,
                    FechaHasta = generado.Date,
                    ParametrosJson = parametrosJson,
                    UsuarioIngresando = sesion.IdUsuario,
                }, ct);

                if (!registro.Success)
                {
                    if (File.Exists(rutaTemporal)) File.Delete(rutaTemporal);
                    Toast?.Invoke($"No se pudo registrar la auditoría del reporte: {registro.Error}");
                    return false;
                }

                // Mover archivo temporal a ruta definitiva
                if (File.Exists(rutaFinal)) File.Delete(rutaFinal);
                File.Move(rutaTemporal, rutaFinal);
                Toast?.Invoke("Reporte de pesajes generado correctamente.");
                return true;
            }
            catch (Exception ex)
            {
                if (File.Exists(rutaTemporal))
                {
                    try { File.Delete(rutaTemporal); } catch { }
                }
                Toast?.Invoke($"Error inesperado al generar reporte: {ex.Message}");
                return false;
            }
            finally
            {
                IsGeneratingReport = false;
            }
        }

        /// <summary>
        /// Id del usuario autenticado. Fail-loud: si no hay sesión activa lanza
        /// excepción en vez de retornar 0 — un pesaje jamás debe registrarse con
        /// usuario fantasma (id_usuario = 0). Los llamadores validan antes con
        /// <see cref="HaySesionActiva"/> para dar feedback amigable vía Toast.
        /// </summary>
        private int UsuarioActual => _sesionService.SesionActual?.IdUsuario
            ?? throw new InvalidOperationException(
                "No hay sesión activa; no se puede registrar la operación de pesaje.");

        /// <summary>Guarda amigable: true si hay sesión; si no, loguea y notifica.</summary>
        private bool HaySesionActiva(string operacion)
        {
            if (_sesionService.SesionActual is not null) return true;
            Serilog.Log.Warning("PesajeVM: intento de {Operacion} sin sesión activa", operacion);
            Toast?.Invoke("Sesión expirada. Vuelve a iniciar sesión.");
            return false;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Carga
        // ══════════════════════════════════════════════════════════════════════
        public async Task CargarAsync()
        {
            IsLoading = true;
            var r = await _repo.GetCamionesActivosAsync();
            if (!r.Success) { IsLoading = false; Toast?.Invoke(r.Error ?? "Error al cargar camiones"); return; }

            Camiones.Clear();
            foreach (var c in r.Value!) Camiones.Add(MapCamion(c));
            RecalcularRecepcionesPorPlaca();
            NotificarStats();

            SelectedCamion   = Camiones.FirstOrDefault();
            SelectedProducto = null;
            SelectedEntrada  = null;
            if (SelectedCamion != null) await CargarProductosAsync(SelectedCamion);
            RecalcularFilas();
            IsLoading = false;
        }

        private async Task CargarProductosAsync(CamionPesaje camion)
        {
            CargandoProductos = true;
            var r = await _repo.GetProductosAsync(camion.Id);
            camion.Productos.Clear();
            if (!r.Success)
            {
                CargandoProductos = false;
                Toast?.Invoke(r.Error ?? "Error al cargar productos");
                return;
            }
            foreach (var p in r.Value!) camion.Productos.Add(MapProducto(p, camion.Proveedor));

            // Los totales de tara extra del camión son la suma de la de sus productos,
            // que recién se conoce con los productos ya cargados.
            camion.NotificarTotales();
            CargandoProductos = false;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Selección
        // ══════════════════════════════════════════════════════════════════════
        public async Task SeleccionarCamionAsync(CamionPesaje? c)
        {
            SelectedCamion   = c;
            SelectedProducto = null;
            SelectedEntrada  = null;
            if (c != null) await CargarProductosAsync(c);
            RecalcularFilas();
            NotificarStats();
        }

        public void SeleccionarProducto(ProductoCamion? p)
        {
            SelectedProducto = p;
            SelectedEntrada  = null;
            RecalcularFilas();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Camiones
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Persiste los datos del camión (alta o edición) — nada más. Los productos ya
        /// no viajan acá: se agregan/editan/quitan uno a la vez desde
        /// <see cref="AgregarProductoAsync"/>/<see cref="ActualizarProductoAsync"/>/
        /// <see cref="QuitarProductoAsync"/>, disparados desde <c>ProductoCamionModal</c>
        /// en la pantalla principal.
        /// <para/>
        /// La tara extra NO se toca acá: se pesa por entrada, no por camión.
        /// </summary>
        public async Task<bool> GuardarCamionAsync(
            CamionPesaje? camionExistente, string placa, string proveedor, int? idProveedor, string obs)
        {
            if (idProveedor is null) { Toast?.Invoke("Selecciona un proveedor válido"); return false; }
            if (!HaySesionActiva("guardar el camión")) return false;

            int idCamion;

            if (camionExistente is null)
            {
                var rNuevo = await _repo.CrearCamionAsync(idProveedor.Value, placa, obs, UsuarioActual);
                if (!rNuevo.Success) { Toast?.Invoke(rNuevo.Error ?? "No se pudo registrar el camión"); return false; }
                idCamion = rNuevo.Value;
            }
            else
            {
                var rEdit = await _repo.ActualizarCamionAsync(camionExistente.Id, idProveedor.Value, placa, obs);
                if (!rEdit.Success) { Toast?.Invoke(rEdit.Error ?? "No se pudo actualizar el camión"); return false; }
                idCamion = camionExistente.Id;
            }

            await RecargarCamionesAsync(seleccionarId: idCamion);
            Toast?.Invoke(camionExistente is null ? "Camión registrado" : "Cambios guardados");
            return true;
        }

        /// <summary>
        /// Alta atómica de VARIOS camiones en una sola transacción en el servidor — lo que confirma
        /// <c>RegistroCamionesModal</c>. Devuelve cuántos quedaron registrados.
        /// </summary>
        /// <remarks>
        /// Delegado a <see cref="IPesajeRepository.RegistrarCamionesLoteAsync"/> mediante la
        /// RPC atómica <c>registrar_camiones_lote_seguro</c>. Si cualquier inserción falla, la
        /// transacción se aborta completamente en el servidor y ningún camión es persistido.
        /// </remarks>
        public async Task<int> RegistrarCamionesAsync(
            IReadOnlyList<(string Placa, int IdProveedor, string Observaciones)> camiones,
            CancellationToken ct = default)
        {
            if (camiones.Count == 0) return 0;
            if (!HaySesionActiva("registrar los camiones")) return 0;

            var lote = camiones
                .Select(c => (c.Placa, c.IdProveedor, (string?)c.Observaciones))
                .ToList();

            var idSolicitud = Guid.NewGuid();
            var r = await _repo.RegistrarCamionesLoteAsync(lote, idSolicitud, ct);

            if (!r.Success)
            {
                string error = r.Error ?? "No se pudo registrar el lote de camiones";
                Serilog.Log.Warning("PesajeVM: error al registrar lote de camiones: {Error}", error);
                Toast?.Invoke(error);
                return 0;
            }

            var resultado = r.Value!;
            int creados = resultado.Creados;
            int? primerId = resultado.PrimerIdMovimiento > 0 ? resultado.PrimerIdMovimiento : null;

            if (creados > 0)
            {
                await RecargarCamionesAsync(seleccionarId: primerId);
                Toast?.Invoke(creados == 1 ? "Camión registrado" : $"{creados} camiones registrados");
            }
            else
            {
                Toast?.Invoke("No se registraron camiones");
            }

            return creados;
        }

        /// <summary>
        /// Recibe el camión explícito (no <see cref="SelectedCamion"/>): lo dispara el
        /// ícono de basurero de su propia fila en la lista, así que borra el que se tocó
        /// sin depender de que ese clic también haya cambiado la selección.
        /// </summary>
        public async Task QuitarCamionAsync(CamionPesaje camion)
        {
            var r = await _repo.AnularCamionAsync(camion.Id);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo quitar"); return; }

            await RecargarCamionesAsync();
            Toast?.Invoke("Camión eliminado");
        }

        public async Task<bool> DescargarCamionAsync()
        {
            if (SelectedCamion is null || CamionCerrado) return false;
            var r = await _repo.CerrarCamionAsync(SelectedCamion.Id);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo cerrar"); return false; }

            SelectedCamion.Estado = "Cerrado";
            OnPropertyChanged(nameof(CamionCerrado));
            NotificarStats();
            return true;
        }

        public async Task<System.Collections.Generic.List<CamionPesaje>> CerrarTodosAsync()
        {
            var abiertos = Camiones.Where(c => c.Estado == "Abierto").ToList();
            foreach (var c in abiertos)
            {
                var r = await _repo.CerrarCamionAsync(c.Id);
                if (r.Success) c.Estado = "Cerrado";
                else Toast?.Invoke(r.Error ?? $"No se pudo cerrar {c.Placa}");
            }
            OnPropertyChanged(nameof(CamionCerrado));
            NotificarStats();
            return abiertos;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Productos
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Guarda la carga completa de una recepción de un saque: los productos nuevos,
        /// las correcciones de manifiesto y las bajas. Lo dispara <c>ProductosCargaModal</c>
        /// al tocar «Finalizar».
        /// </summary>
        /// <remarks>
        /// Delegado a la RPC atómica <c>registrar_productos_lote_seguro</c>: si una sola
        /// fila viola una restricción, el servidor aborta la transacción entera y no
        /// persiste nada. Mismo criterio que el alta de camiones (P-053) y el reparto de
        /// tara extra (P-032).
        /// <para/>
        /// Al terminar recarga <b>solo esta recepción</b> (<see cref="CargarProductosAsync"/>),
        /// no todos los camiones: el flujo viejo hacía un refetch completo por CADA producto
        /// agregado, que con N productos eran N recargas de toda la pantalla.
        /// </remarks>
        public async Task<bool> GuardarProductosCargaAsync(
            CamionPesaje camion,
            IReadOnlyList<ProductoCargaAlta> altas,
            IReadOnlyList<ProductoCargaCambio> cambios,
            IReadOnlyList<int> bajas,
            CancellationToken ct = default)
        {
            if (altas.Count == 0 && cambios.Count == 0 && bajas.Count == 0) return true;
            if (!HaySesionActiva("guardar los productos de la carga")) return false;

            // El id_solicitud se genera UNA vez por intención del usuario, fuera de
            // cualquier reintento: es lo que hace que reintentar no duplique la carga.
            var idSolicitud = Guid.NewGuid();
            var r = await _repo.GuardarProductosLoteAsync(camion.Id, altas, cambios, bajas, idSolicitud, ct);

            if (!r.Success)
            {
                string error = r.Error ?? "No se pudieron guardar los productos de la carga";
                Serilog.Log.Warning("PesajeVM: error al guardar la carga del movimiento {Id}: {Error}", camion.Id, error);
                Toast?.Invoke(error);
                return false;
            }

            var resultado = r.Value!;

            await CargarProductosAsync(camion);
            SelectedProducto = camion.Productos.FirstOrDefault();
            RecalcularFilas();
            NotificarStats();

            Toast?.Invoke(ResumenCarga(resultado));
            return true;
        }

        /// <summary>
        /// Redacta el Toast del guardado con solo las partes que ocurrieron: decir
        /// «0 quitados» cuando no se quitó nada es ruido.
        /// </summary>
        private static string ResumenCarga(ResultadoLoteProductos r)
        {
            var partes = new List<string>();
            if (r.Creados > 0)      partes.Add(r.Creados == 1 ? "1 producto agregado" : $"{r.Creados} productos agregados");
            if (r.Actualizados > 0) partes.Add(r.Actualizados == 1 ? "1 actualizado" : $"{r.Actualizados} actualizados");
            if (r.Anulados > 0)     partes.Add(r.Anulados == 1 ? "1 quitado" : $"{r.Anulados} quitados");

            return partes.Count == 0 ? "No hubo cambios en la carga" : string.Join(" · ", partes);
        }

        /// <summary>
        /// Escribe en cada camión cuántas recepciones abiertas comparten su placa. Es lo
        /// que le permite a la lista mostrar un camión con dos proveedores como UN camión
        /// agrupado y no como dos registros sueltos.
        /// </summary>
        private void RecalcularRecepcionesPorPlaca()
        {
            var porPlaca = Camiones
                .Where(c => c.Estado == "Abierto")
                .GroupBy(c => (c.Placa ?? "").Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var c in Camiones)
                c.RecepcionesEnPlaca = porPlaca.TryGetValue((c.Placa ?? "").Trim().ToUpperInvariant(), out var n)
                    ? n : 1;

            HayPlacaCompartida = porPlaca.Values.Any(n => n > 1);
        }

        // RecepcionesDePlaca, ActualizarProductoAsync y QuitarProductoAsync se
        // eliminaron con ProductoCamionModal: eran sus tres únicos llamadores.
        // Editar y quitar productos ahora viajan dentro del lote de
        // GuardarProductosCargaAsync, en una sola transacción y con bitácora.

        public async Task ToggleEstadoProductoAsync(ProductoCamion p)
        {
            if (CamionCerrado) return;
            bool cerrar = p.Estado == "Abierto";
            var r = await _repo.SetEstadoProductoAsync(p.Id, cerrar);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo cambiar estado"); return; }
            p.Estado = cerrar ? "Cerrado" : "Abierto";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Pesajes
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Registra un pesaje y refleja el resultado SIN recargar el camión.
        /// <para/>
        /// El INSERT ya devuelve la fila con los derivados que calculó el trigger de BD, así
        /// que aplicar ese DTO a la colección en memoria deja la pantalla exactamente igual
        /// que un refetch — pero con un solo round trip en vez de cuatro. Con la latencia de
        /// la red de planta esa diferencia es la que hacía que "Seguir pesando" se sintiera
        /// colgado.
        /// <para/>
        /// No es un guardado optimista: nada se muestra hasta que la BD confirmó, y los
        /// números que se muestran son los que la BD devolvió, no los que calculó el modal.
        /// </summary>
        /// <returns><c>true</c> si el pesaje quedó guardado.</returns>
        public async Task<bool> GuardarEntradaAsync(ProductoCamion producto, EntradaPesaje snapshot, EntradaPesaje? editando)
        {
            if (SelectedCamion is null) return false;
            if (!HaySesionActiva("guardar pesaje")) return false;

            if (editando is not null)
            {
                var ra = await _repo.AnularEntradaAsync(editando.Id);
                if (!ra.Success) { Toast?.Invoke(ra.Error ?? "No se pudo editar"); return false; }
                producto.Entradas.Remove(editando);
            }

            // La tara extra es un peso real que se captura en el modal (las tarimas que
            // vinieron con esta pesada). Ya no se prorratea nada: si no se pesó acá, se
            // carga después el total y el sistema lo reparte entre las pesadas.
            var r = await _repo.CrearEntradaAsync(
                producto.Id, producto.IdProducto, snapshot.Bruto, snapshot.TaraExtra,
                snapshot.Observaciones, UsuarioActual);

            if (!r.Success)
            {
                Toast?.Invoke(r.Error ?? "No se pudo guardar el pesaje");

                // Si veníamos de una edición, la entrada vieja ya se anuló en la BD y se quitó
                // de la colección: sin el INSERT que la reemplace, la memoria y la BD podrían
                // no coincidir. Acá sí vale el refetch completo — es el camino de error.
                if (editando is not null) await CargarProductosAsync(SelectedCamion);
                RecalcularFilas();
                return false;
            }

            AgregarEntradaEnMemoria(producto, r.Value!);
            RecalcularFilas();
            Toast?.Invoke("Pesaje guardado");
            return true;
        }

        /// <summary>
        /// Inserta la pesada recién confirmada por la BD en la colección del producto y
        /// refresca los agregados derivados (recibido, tara extra, bultos estimados).
        /// <para/>
        /// Replica el mapeo de <see cref="MapProducto"/> para una sola entrada: los bultos
        /// teóricos son lo único que no viaja en el DTO porque no se persiste, se estima
        /// desde el peso del producto.
        /// </summary>
        private void AgregarEntradaEnMemoria(ProductoCamion producto, EntradaDto e)
        {
            producto.Entradas.Add(new EntradaPesaje
            {
                Id = e.Id, Bruto = e.Bruto, TaraInd = e.TaraInd, TaraExtra = e.TaraExtra,
                TaraTotal = e.TaraTotal, Neto = e.Neto, Fecha = e.Fecha, Hora = e.Hora,
                BultosCapturados = e.BultosCapturados,
                BultosTeoricos = PesajeCalc.BultosTeoricos(
                    e.Bruto, e.TaraExtra, producto.PesoTeorico, producto.TaraUnitaria),
                Observaciones = e.Observaciones,
                ProdId = producto.Id, ProdNombre = producto.ProductoNombre,
            });

            producto.NotificarAgregados();
            SelectedCamion?.NotificarTotales();
        }

        /// <summary>
        /// Anula una pesada y la quita de la colección en memoria.
        /// <para/>
        /// Mismo criterio que <see cref="GuardarEntradaAsync"/>: anular es un solo round trip
        /// y ya sabemos exactamente qué fila desapareció, así que recargar el camión entero
        /// solo agregaría tres viajes para llegar al mismo estado.
        /// </summary>
        public async Task QuitarEntradaAsync(EntradaPesaje entrada)
        {
            if (SelectedCamion is null) return;
            var r = await _repo.AnularEntradaAsync(entrada.Id);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo quitar"); return; }

            // La entrada sabe a qué producto pertenece (RecalcularFilas le setea ProdId), así
            // que se ubica sin depender de cuál esté seleccionado: en vista "Todo el camión"
            // se puede borrar una pesada de un producto distinto al seleccionado.
            var producto = SelectedCamion.Productos.FirstOrDefault(p => p.Id == entrada.ProdId);
            if (producto is not null)
            {
                producto.Entradas.Remove(entrada);
                producto.NotificarAgregados();
                SelectedCamion.NotificarTotales();
            }

            SelectedEntrada = null;
            RecalcularFilas();
            Toast?.Invoke("Entrada eliminada");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tara extra — reparto de un total ya pesado
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Reparte una tara extra TOTAL ya pesada entre las pesadas del alcance elegido
        /// (un producto o el camión entero) y la escribe en cada entrada.
        /// <para/>
        /// Es un UPDATE en el lugar, NO el patrón anular+insertar que usa la edición de un
        /// pesaje: reemitir N ids, fechas y usuarios destruiría la auditoría de pesadas que
        /// no se están corrigiendo — sólo se les está completando un dato.
        /// <para/>
        /// Pre-vuelo obligatorio en cliente: si alguna pesada quedaría con neto ≤ 0 se muestra
        /// mensaje amigable antes de ir al servidor.
        /// La escritura es ATÓMICA en el servidor (RPC <c>repartir_tara_extra_pesaje_tabla_bitacora</c>):
        /// si alguna pesada viola restricciones, la transacción se aborta y ningún UPDATE persiste.
        /// </summary>
        public async Task<bool> RepartirTaraExtraAsync(IReadOnlyList<EntradaPesaje> entradas, double totalKg)
        {
            if (SelectedCamion is null) return false;
            if (!HaySesionActiva("repartir la tara extra")) return false;

            if (entradas.Count == 0)
            {
                Toast?.Invoke("Registrá al menos una pesada antes de cargar la tara extra.");
                return false;
            }
            if (totalKg < 0) { Toast?.Invoke("La tara extra no puede ser negativa."); return false; }

            var cuotas = PesajeCalc.RepartirTaraExtra(totalKg, entradas.Count);

            // Pre-vuelo: validación en cliente para mensaje amigable antes de ir al servidor.
            for (int i = 0; i < entradas.Count; i++)
            {
                double neto = entradas[i].Bruto - entradas[i].TaraInd - cuotas[i];
                if (neto <= 0)
                {
                    double max = PesajeCalc.TaraExtraMaximaRepartible(
                        entradas.Select(x => (x.Bruto, x.TaraInd)));
                    Toast?.Invoke(
                        $"No se puede repartir {totalKg:N2} kg: una pesada quedaría con neto {neto:N2} kg. " +
                        $"Máximo repartible: {max:N2} kg.");
                    return false;
                }
            }

            // Reparto atómico en una sola llamada al servidor (P-032).
            var idsPesaje   = entradas.Select(e => e.Id).ToList();
            var idSolicitud = Guid.NewGuid();
            var resultado   = await _repo.RepartirTaraExtraLoteAsync(idsPesaje, totalKg, idSolicitud);

            if (!resultado.Success)
            {
                Serilog.Log.Error("PesajeVM: falló el reparto atómico de tara extra: {Error}", resultado.Error);
                Toast?.Invoke($"Error al repartir la tara extra: {resultado.Error}");
                return false;
            }

            int? idProd = SelectedProducto?.Id;
            await CargarProductosAsync(SelectedCamion);
            SelectedProducto = idProd.HasValue
                ? SelectedCamion.Productos.FirstOrDefault(x => x.Id == idProd.Value)
                : null;
            RecalcularFilas();

            Toast?.Invoke($"Tara extra repartida entre {entradas.Count} pesada(s)");
            return true;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Filas de entradas
        // ══════════════════════════════════════════════════════════════════════
        public void RecalcularFilas()
        {
            if (SelectedCamion is null)
            {
                FilasEntradas.ReplaceAll(Enumerable.Empty<EntradaPesaje>());
                OnPropertyChanged(nameof(ModoEfectivo));
                NotificarTotales();
                return;
            }

            // Se arma aparte y se vuelca con ReplaceAll (un solo Reset) en vez de
            // Clear()+Add() por fila (un CollectionChanged por fila) — con varias
            // decenas de filas eso se sentía como demora al cambiar de producto.
            var filas = new List<EntradaPesaje>();
            if (ModoEfectivo == "producto" && SelectedProducto is not null)
            {
                foreach (var e in SelectedProducto.Entradas)
                {
                    e.ProdId = SelectedProducto.Id; e.ProdNombre = SelectedProducto.ProductoNombre;
                    filas.Add(e);
                }
            }
            else
            {
                foreach (var p in SelectedCamion.Productos)
                    foreach (var e in p.Entradas)
                    {
                        e.ProdId = p.Id; e.ProdNombre = p.ProductoNombre;
                        filas.Add(e);
                    }
            }
            FilasEntradas.ReplaceAll(filas);
            OnPropertyChanged(nameof(ModoEfectivo));
            NotificarTotales();
        }

        /// <summary>
        /// Los totales derivan de FilasEntradas, que se reconstruye entera en cada
        /// RecalcularFilas: no hay forma de notificarlos por cambio de item, así que
        /// se avisan acá, en el único punto que la rearma.
        /// </summary>
        private void NotificarTotales()
        {
            OnPropertyChanged(nameof(TotalBruto));
            OnPropertyChanged(nameof(TotalTara));
            OnPropertyChanged(nameof(TotalTaraExtra));
            OnPropertyChanged(nameof(TotalNeto));
            OnPropertyChanged(nameof(TotalBultos));
            OnPropertyChanged(nameof(TotalPesajesTexto));
        }

        public void CambiarVista(string modo) { VistaEntradas = modo; RecalcularFilas(); }

        // ══════════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════════
        private async Task RecargarCamionesAsync(int? seleccionarId = null)
        {
            var r = await _repo.GetCamionesActivosAsync();
            if (!r.Success) { Toast?.Invoke(r.Error ?? "Error al recargar"); return; }

            Camiones.Clear();
            foreach (var c in r.Value!) Camiones.Add(MapCamion(c));
            RecalcularRecepcionesPorPlaca();
            NotificarStats();

            SelectedCamion = seleccionarId.HasValue
                ? Camiones.FirstOrDefault(c => c.Id == seleccionarId.Value) ?? Camiones.FirstOrDefault()
                : Camiones.FirstOrDefault();
            SelectedProducto = null;
            SelectedEntrada  = null;
            if (SelectedCamion != null) await CargarProductosAsync(SelectedCamion);
            RecalcularFilas();
        }

        private void NotificarStats()
        {
            OnPropertyChanged(nameof(CamionesCount));
            OnPropertyChanged(nameof(CamionesActivos));
            OnPropertyChanged(nameof(CamionesTexto));
            OnPropertyChanged(nameof(PuedeAgregarCamion));
            OnPropertyChanged(nameof(MostrarEstadoVacio));
        }

        private static CamionPesaje MapCamion(CamionDto c) => new()
        {
            Id = c.Id, Placa = c.Placa, Proveedor = c.Proveedor, IdProveedor = c.IdProveedor,
            FechaAsignacion = c.FechaAsignacion, Observaciones = c.Observaciones,
            Estado = c.Cerrado ? "Cerrado" : "Abierto",
            TaraExtraLegado = c.TaraExtraLegado,
        };

        private static ProductoCamion MapProducto(MovProductoDto p, string proveedor)
        {
            var pc = new ProductoCamion
            {
                Id = p.Id, IdProducto = p.IdProducto, ProductoCodigo = p.Codigo, ProductoNombre = p.Nombre,
                TaraUnitaria = p.TaraUnitaria, PesoTeorico = p.PesoTeorico,
                PesoManifestado = p.PesoManifestado, BultosDeclarados = p.BultosDeclarados,
                Observaciones = p.Observaciones, Estado = p.Cerrado ? "Cerrado" : "Abierto", ProveedorNombre = proveedor,
            };
            // Único punto donde conviven la entrada y los datos del producto: acá se calcula
            // la estimación de bultos, que no se persiste.
            foreach (var e in p.Entradas)
                pc.Entradas.Add(new EntradaPesaje
                {
                    Id = e.Id, Bruto = e.Bruto, TaraInd = e.TaraInd, TaraExtra = e.TaraExtra,
                    TaraTotal = e.TaraTotal, Neto = e.Neto, Fecha = e.Fecha, Hora = e.Hora,
                    BultosCapturados = e.BultosCapturados,
                    BultosTeoricos = PesajeCalc.BultosTeoricos(
                        e.Bruto, e.TaraExtra, p.PesoTeorico, p.TaraUnitaria),
                    Observaciones = e.Observaciones, ProdId = p.Id, ProdNombre = p.Nombre,
                });
            pc.NotificarAgregados();
            return pc;
        }

        /// <summary>
        /// P-031 G5: <c>PesajeView</c> es Transient y crea una instancia nueva por cada
        /// visita a la pantalla (<c>App.Services.GetRequiredService&lt;PesajeViewModel&gt;()</c>
        /// en <c>Loaded</c>); antes no había ningún <c>Dispose()</c> al que <c>Unloaded</c>
        /// pudiera llamar. Hoy este ViewModel no suscribe Realtime ni mantiene un
        /// <see cref="CancellationTokenSource"/> propio, así que no hay nada que liberar —
        /// pero implementarlo evita que una futura suscripción (siguiendo el patrón de
        /// P-029) quede huérfana por falta de un Dispose real al que engancharse.
        /// </summary>
        public void Dispose() { }
    }
}
