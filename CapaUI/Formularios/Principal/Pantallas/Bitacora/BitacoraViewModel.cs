using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CapaAplicacion.Bitacora.Dtos;
using CapaAplicacion.Bitacora.Interfaces;
using CapaAplicacion.Bitacora.Queries;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.Controls;
using CapaDominio.Reportes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;

namespace CapaUI.Formularios.Principal.Pantallas.Bitacora;

/// <summary>
/// ViewModel de la Bitácora de auditoría. Solo lectura: no hay comandos
/// Nuevo/Editar/Cambiar Estado — la bitácora la escribe el sistema al ejecutar
/// acciones, nunca esta pantalla. Orden por fecha descendente (más reciente
/// primero), paginación server-side y filtros combinables.
/// </summary>
public partial class BitacoraViewModel : ObservableObject, IDisposable
{
    private readonly IBitacoraRepository _repo;
    private readonly IReportGeneratorService _reportGenerator;
    private readonly IReporteRepository _reporteRepository;
    private readonly IUsuarioSesionService _sesionService;
    private readonly SuggestionDebouncer    _buscador = new();
    private IReadOnlyList<BitacoraDto> _seleccionReporte = Array.Empty<BitacoraDto>();
    private bool _disposed;

    private string    _query = "";
    private int?      _usuarioFiltro;
    private int?      _moduloFiltro;
    private int?      _accionFiltro;
    private DateTime? _fechaDesde;
    private DateTime? _fechaHasta;
    private int       _page = 1;
    private int       _filteredCount;
    private int?      _pendingSelectionId;
    private int       _loadGeneration;

    public const int PageSize = 50;

    [ObservableProperty] private ObservableCollection<BitacoraDto> _pageRows    = new();
    /// <summary>Sugerencias ya mapeadas para el <c>SuggestionSearchBox</c> (binding directo). <c>null</c> o vacía = popup cerrado.</summary>
    [ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;

    /// <summary>Solo para resaltar la fila elegida desde el buscador — sin acción asociada.</summary>
    [ObservableProperty] private BitacoraDto? _seleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    private bool _isLoading;

    [ObservableProperty] private int    _highlightIndex = -1;
    [ObservableProperty] private int    _totalCount;
    [ObservableProperty] private string _errorCarga = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearReporteCommand))]
    private int _cantidadSeleccionada;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearReporteCommand))]
    private bool _isGeneratingReport;

    // Lookups de los dropdowns de filtro
    [ObservableProperty] private List<FiltroItem> _modulos  = new();
    [ObservableProperty] private List<FiltroItem> _acciones = new();
    [ObservableProperty] private List<FiltroItem> _usuarios = new();

    public int  TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)PageSize));
    public bool NoResults  => !IsLoading && _filteredCount == 0;

    public string PageInfo
    {
        get
        {
            if (_filteredCount == 0) return "Sin resultados";
            int from = (_page - 1) * PageSize + 1;
            int to   = Math.Min(_page * PageSize, _filteredCount);
            return $"Mostrando {from}–{to} de {_filteredCount} registros";
        }
    }

    public string Query
    {
        get => _query;
        set
        {
            if (_query == value) return;
            _query = value;
            OnPropertyChanged();
            _ = RefrescarSugerenciasAsync();
        }
    }

    public int? UsuarioFiltro
    {
        get => _usuarioFiltro;
        set { if (_usuarioFiltro == value) return; _usuarioFiltro = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public int? ModuloFiltro
    {
        get => _moduloFiltro;
        set { if (_moduloFiltro == value) return; _moduloFiltro = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public int? AccionFiltro
    {
        get => _accionFiltro;
        set { if (_accionFiltro == value) return; _accionFiltro = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public DateTime? FechaDesde
    {
        get => _fechaDesde;
        set { if (_fechaDesde == value) return; _fechaDesde = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public DateTime? FechaHasta
    {
        get => _fechaHasta;
        set { if (_fechaHasta == value) return; _fechaHasta = value; OnPropertyChanged(); ReiniciarYCargar(); }
    }

    public int Page
    {
        get => _page;
        set
        {
            if (_page == value) return;
            _page = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(TotalPages));
            NotifyPaginationCanExecuteChanged();
            _ = CargarPaginaAsync();
        }
    }

    // ── Eventos ────────────────────────────────────────────────────────
    public event Action? FiltrosLimpiados;

    /// <summary>La View repuebla el ComboBox de Acción cuando cambia el Módulo.</summary>
    public event Action? AccionesRecargadas;
    public event Action? SolicitarCrearReporte;
    public event Action<string>? ReporteCreado;

    // ── Constructor ────────────────────────────────────────────────────
    public BitacoraViewModel(
        IBitacoraRepository repo,
        IReportGeneratorService reportGenerator,
        IReporteRepository reporteRepository,
        IUsuarioSesionService sesionService)
    {
        _repo = repo;
        _reportGenerator = reportGenerator;
        _reporteRepository = reporteRepository;
        _sesionService = sesionService;
    }

    // ── Carga ──────────────────────────────────────────────────────────
    public async Task CargarDatosAsync()
    {
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var modulosTask  = _repo.ObtenerModulosAsync();
        var usuariosTask = _repo.ObtenerUsuariosAsync();
        var accionesTask = _repo.ObtenerAccionesAsync(null);
        await Task.WhenAll(modulosTask, usuariosTask, accionesTask);

        if (modulosTask.Result.Success)  Modulos  = modulosTask.Result.Value!.ToList();
        if (usuariosTask.Result.Success) Usuarios = usuariosTask.Result.Value!.ToList();
        if (accionesTask.Result.Success) Acciones = accionesTask.Result.Value!.ToList();

        await CargarPaginaAsync();
    }

    public void RefrescarDatos() => _ = CargarPaginaAsync();

    private void ReiniciarYCargar()
    {
        _page = 1;
        _ = CargarPaginaAsync();
    }

    /// <summary>Recarga las acciones disponibles según el módulo seleccionado (cascada).</summary>
    public async Task RecargarAccionesAsync()
    {
        var r = await _repo.ObtenerAccionesAsync(_moduloFiltro);
        if (!r.Success) return;
        Acciones = r.Value!.ToList();
        AccionesRecargadas?.Invoke();
    }

    private const int TimeoutMs = 10_000;

    private BitacoraFiltros BuildFiltros() => new()
    {
        IdUsuario  = _usuarioFiltro,
        IdModulo   = _moduloFiltro,
        IdAccion   = _accionFiltro,
        FechaDesde = _fechaDesde,
        FechaHasta = _fechaHasta,
    };

    private async Task CargarPaginaAsync()
    {
        int myGen  = ++_loadGeneration;
        IsLoading  = true;
        ErrorCarga = string.Empty;

        var task = _repo.GetPagedAsync(_page, PageSize, BuildFiltros());
        if (await Task.WhenAny(task, Task.Delay(TimeoutMs)) != task)
        {
            if (myGen != _loadGeneration) return;
            ErrorCarga = "La carga tardó demasiado. Intente de nuevo.";
            IsLoading  = false;
            return;
        }

        var r = await task;
        if (myGen != _loadGeneration) return;

        if (!r.Success)
        {
            ErrorCarga = r.Error;
            IsLoading  = false;
            return;
        }

        var pagina = r.Value!;

        TotalCount     = pagina.Total;
        _filteredCount = pagina.Total;

        LimpiarSeleccionReporte();
        PageRows = new ObservableCollection<BitacoraDto>(pagina.Items);

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        NotifyPaginationCanExecuteChanged();
        IsLoading = false;

        if (_pendingSelectionId.HasValue)
        {
            Seleccionado        = PageRows.FirstOrDefault(x => x.IdBitacora == _pendingSelectionId.Value);
            _pendingSelectionId = null;
        }
    }

    // ── Búsqueda con debounce ──────────────────────────────────────────
    private Task RefrescarSugerenciasAsync() => _buscador.EjecutarAsync(
        _query,
        async (q, ct) =>
        {
            var r = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), ct);
            return r.Success ? r.Value!.Select(Map).ToList() : null;
        },
        items => { SuggestItems = items; HighlightIndex = -1; });

    private static SuggestionItemData Map(BitacoraDto b) => new()
    {
        Nombre = b.CampoAfectado,
        Meta   = $"{b.FechaHora:dd/MM/yyyy HH:mm} · {b.AliasUsuario} · {b.NombreAccion}",
        Activo = true,
        Source = b,
    };

    public void SeleccionarSugerencia(BitacoraDto b)
    {
        // Cancelar el debounce en vuelo: como se asigna al campo _query y no a la
        // propiedad, no se pasa por el setter y nadie mas cancelaria la busqueda. Sin
        // esto, una busqueda en curso termina despues de la seleccion y reabre el popup.
        _buscador.Cancelar();

        _query = "";
        OnPropertyChanged(nameof(Query));
        SuggestItems = null;

        var enPagina = PageRows.FirstOrDefault(x => x.IdBitacora == b.IdBitacora);
        if (enPagina is not null) { Seleccionado = enPagina; return; }

        _pendingSelectionId = b.IdBitacora;
        _ = NavegarAPaginaDeRegistroAsync(b.IdBitacora);
    }

    private async Task NavegarAPaginaDeRegistroAsync(int id)
    {
        var r = await _repo.GetPaginaDeRegistroAsync(id, PageSize, BuildFiltros());
        if (!r.Success) { _pendingSelectionId = null; ErrorCarga = r.Error; return; }

        _page = r.Value;
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(TotalPages));
        NotifyPaginationCanExecuteChanged();
        await CargarPaginaAsync();
    }

    // ── Filtros ────────────────────────────────────────────────────────
    [RelayCommand]
    private void LimpiarFiltros()
    {
        LimpiarSeleccionReporte();
        _usuarioFiltro = null;
        _moduloFiltro  = null;
        _accionFiltro  = null;
        _fechaDesde    = null;
        _fechaHasta    = null;
        _page          = 1;
        FiltrosLimpiados?.Invoke();
        _ = RecargarAccionesAsync();
        _ = CargarPaginaAsync();
    }

    // ── Reportes ─────────────────────────────────────────────────────────

    public void ActualizarSeleccionReporte(IEnumerable<BitacoraDto> seleccion)
    {
        _seleccionReporte = seleccion
            .DistinctBy(x => x.IdBitacora)
            .ToList();
        CantidadSeleccionada = _seleccionReporte.Count;
    }

    private void LimpiarSeleccionReporte()
    {
        _seleccionReporte = Array.Empty<BitacoraDto>();
        CantidadSeleccionada = 0;
    }

    private bool PuedeCrearReporte() => CantidadSeleccionada > 0 && !IsGeneratingReport;

    [RelayCommand(CanExecute = nameof(PuedeCrearReporte))]
    private void CrearReporte() => SolicitarCrearReporte?.Invoke();

    public async Task GenerarReporteAsync(
        ReportFormat format,
        string rutaFinal,
        CancellationToken ct = default)
    {
        if (!PuedeCrearReporte()) return;

        var sesion = _sesionService.SesionActual;
        if (sesion is null)
        {
            ErrorCarga = "No hay una sesión activa; no se puede generar el reporte.";
            return;
        }

        string? directorio = Path.GetDirectoryName(rutaFinal);
        if (string.IsNullOrWhiteSpace(directorio) || !Directory.Exists(directorio))
        {
            ErrorCarga = "La ubicación elegida para el reporte no es válida.";
            return;
        }

        IsGeneratingReport = true;
        ErrorCarga = string.Empty;
        string rutaTemporal = Path.Combine(
            directorio,
            $".{Path.GetFileName(rutaFinal)}.{Guid.NewGuid():N}.tmp");
        bool reporteRegistrado = false;

        try
        {
            var seleccion = _seleccionReporte.ToList();
            DateTime generado = DateTime.Now;
            string tipo = format == ReportFormat.Pdf ? "PDF" : "Excel";
            string nombre = $"Reporte de Bitácora - {generado:yyyyMMdd-HHmmss}";

            var documento = new TabularReportDto
            {
                Title = "Reporte de Bitácora",
                GeneratedAt = generado,
                Author = new ReportAuthorDto
                {
                    Email = sesion.Email,
                    Rol = sesion.NombreRol,
                },
                SheetName = "Bitácora",
                Columns = [new("FECHA / HORA", "dd/MM/yyyy HH:mm"), new("USUARIO"), new("MÓDULO"), new("ACCIÓN"), new("CAMPO AFECTADO"), new("DETALLE")],
                Rows = seleccion.Select(b => (IReadOnlyList<object?>)new object?[]
                    {
                        b.FechaHora,
                        b.AliasUsuario,
                        b.NombreModulo,
                        b.NombreAccion,
                        b.CampoAfectado,
                        b.EstadoActual,
                    }).ToList(),
            };

            var generadoResult = await _reportGenerator.GenerateAsync(documento, format, ct);
            if (!generadoResult.Success)
            {
                ErrorCarga = generadoResult.Error;
                return;
            }

            await File.WriteAllBytesAsync(rutaTemporal, generadoResult.Value!, ct);

            var fechas = seleccion
                .Where(x => x.FechaHora.HasValue)
                .Select(x => x.FechaHora!.Value.Date)
                .ToList();

            string parametrosJson = JsonConvert.SerializeObject(new
            {
                origen = "bitacora",
                ids_bitacora = seleccion.Select(x => x.IdBitacora).ToArray(),
                cantidad_registros = seleccion.Count,
                filtros = new
                {
                    id_usuario = _usuarioFiltro,
                    id_modulo = _moduloFiltro,
                    id_accion = _accionFiltro,
                    fecha_desde = _fechaDesde?.ToString("yyyy-MM-dd"),
                    fecha_hasta = _fechaHasta?.ToString("yyyy-MM-dd"),
                },
                columnas = new[] { "fecha_hora", "usuario", "modulo", "accion", "campo_afectado", "detalle" },
            });

            var registro = await _reporteRepository.RegistrarAsync(new ReporteRegistroDto
            {
                NombreReporte = nombre,
                TipoReporte = tipo,
                Descripcion = $"Reporte de bitácora con {seleccion.Count} registro(s) seleccionado(s).",
                FechaDesde = fechas.Count > 0 ? fechas.Min() : null,
                FechaHasta = fechas.Count > 0 ? fechas.Max() : null,
                ParametrosJson = parametrosJson,
                UsuarioIngresando = sesion.IdUsuario,
            }, ct);

            if (!registro.Success)
            {
                EliminarTemporal(rutaTemporal);
                ErrorCarga = registro.Error;
                return;
            }

            reporteRegistrado = true;
            File.Move(rutaTemporal, rutaFinal, true);
            ReporteCreado?.Invoke(rutaFinal);
        }
        catch (OperationCanceledException)
        {
            EliminarTemporal(rutaTemporal);
            throw;
        }
        catch (Exception ex)
        {
            EliminarTemporal(rutaTemporal);
            ErrorCarga = reporteRegistrado
                ? $"El reporte fue registrado, pero no pudo guardarse en la ubicación elegida: {ex.Message}"
                : $"No se pudo finalizar el reporte: {ex.Message}";
        }
        finally
        {
            IsGeneratingReport = false;
        }
    }

    private static void EliminarTemporal(string ruta)
    {
        try
        {
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch { /* best-effort: nunca ocultar el error original */ }
    }

    // ── Paginación ─────────────────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(PuedePaginaAnterior))]
    private void PrimeraPagina() => Page = 1;

    [RelayCommand(CanExecute = nameof(PuedePaginaAnterior))]
    private void PaginaAnterior() => Page--;

    [RelayCommand(CanExecute = nameof(PuedePaginaSiguiente))]
    private void PaginaSiguiente() => Page++;

    [RelayCommand(CanExecute = nameof(PuedePaginaSiguiente))]
    private void UltimaPagina() => Page = TotalPages;

    private bool PuedePaginaAnterior()  => !IsLoading && _page > 1;
    private bool PuedePaginaSiguiente() => !IsLoading && _page < TotalPages;

    private void NotifyPaginationCanExecuteChanged()
    {
        PrimeraPaginaCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        UltimaPaginaCommand.NotifyCanExecuteChanged();
    }

    // ── IDisposable ────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _buscador.Dispose();
    }
}
