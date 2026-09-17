using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using CapaAplicacion.Empresa.Interfaces;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDominio.Reportes;
using CapaUI.Core.Empresa;
using CapaUI.Core.Permisos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;

namespace CapaUI.Formularios.Principal.Pantallas.Reporteria;

public enum ReporteOperativoTipo { EntradaMateriaPrima, PorProveedor, ProductosConMerma, PrimerosProductos }

public partial class ReporteriaViewModel : ObservableObject
{
    private readonly IReporteConsultaRepository _consultas;
    private readonly IReporteRepository _registro;
    private readonly IReportGeneratorService _generador;
    private readonly IUsuarioSesionService _sesion;
    private readonly IEmpresaRepository _empresa;
    private readonly LogoEmpresaCache _logoCache;
    private IReadOnlyList<IReadOnlyList<object?>> _snapshot = Array.Empty<IReadOnlyList<object?>>();
    private IReadOnlyList<ReportColumnDto> _columnas = Array.Empty<ReportColumnDto>();
    private IReadOnlyList<ReportTotalDto> _totales = Array.Empty<ReportTotalDto>();
    private IReadOnlyList<ReportMetadataDto> _filtros = Array.Empty<ReportMetadataDto>();
    private IReadOnlyList<ReportMetadataDto> _pie = Array.Empty<ReportMetadataDto>();
    private IReadOnlyList<int> _ids = Array.Empty<int>();
    private bool _entradaIncluyePlaca;
    private bool _entradaIncluyePesador;

    public const int PageSize = 25;
    public bool EnMenu => TipoActual is null;
    public bool EsEntrada => TipoActual == ReporteOperativoTipo.EntradaMateriaPrima;
    public bool EsProveedor => TipoActual == ReporteOperativoTipo.PorProveedor;
    public bool EsMermas => TipoActual == ReporteOperativoTipo.ProductosConMerma;
    public bool EsPrueba => TipoActual == ReporteOperativoTipo.PrimerosProductos;
    public bool RequiereProducto => EsEntrada;
    public bool RequiereProveedor => EsEntrada || EsProveedor;
    public bool RequiereFechas => EsEntrada || EsProveedor || EsMermas;
    public bool PermiteCategoria => EsMermas;
    public bool PuedeSeleccionarProducto => EsEntrada && ProveedorSeleccionado?.Id is not null && !IsBusy;
    public DateTime FechaMaxima => DateTime.Today;
    public bool PuedeExportar => _snapshot.Count > 0 && !IsBusy && SesionPermisos.Tiene(Permiso.GenerarReporte);
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_snapshot.Count / (double)PageSize));
    public string PageInfo => _snapshot.Count == 0 ? "Sin resultados" : $"Página {Pagina} de {TotalPages} · {_snapshot.Count} registros";
    public bool SinResultados => _snapshot.Count == 0 && !IsBusy && TipoActual is not null && VistaPrevia is not null;
    public bool HayResultados => _snapshot.Count > 0 && !IsBusy;
    public bool HayMetadatosPie => _pie.Count > 0 && HayResultados;
    public IReadOnlyList<ReportMetadataDto> MetadatosVistaPrevia => _filtros;
    public IReadOnlyList<ReportMetadataDto> MetadatosPieVistaPrevia => _pie;
    public string SubtituloBreadcrumb => EnMenu ? "Reportes Operativos" : TituloActual;
    public string TituloEncabezado => EnMenu ? "Reportes del sistema" : TituloActual;

    [ObservableProperty] private ReporteOperativoTipo? _tipoActual;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtituloBreadcrumb))]
    [NotifyPropertyChangedFor(nameof(TituloEncabezado))]
    private string _tituloActual = string.Empty;
    [ObservableProperty] private FiltroItem? _productoSeleccionado;
    [ObservableProperty] private FiltroItem? _proveedorSeleccionado;
    [ObservableProperty] private FiltroItem? _categoriaSeleccionada;
    [ObservableProperty] private DateTime? _fechaDesde;
    [ObservableProperty] private DateTime? _fechaHasta;
    [ObservableProperty] private DataView? _vistaPrevia;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _mensaje = string.Empty;
    [ObservableProperty] private int _pagina = 1;

    public event Action<string>? SelectorSolicitado;
    public event Action? FormatoSolicitado;
    public event Action<string>? ReporteCreado;

    public ReporteriaViewModel(IReporteConsultaRepository consultas, IReporteRepository registro,
        IReportGeneratorService generador, IUsuarioSesionService sesion, IEmpresaRepository empresa, LogoEmpresaCache logoCache)
        => (_consultas, _registro, _generador, _sesion, _empresa, _logoCache) = (consultas, registro, generador, sesion, empresa, logoCache);

    [RelayCommand]
    private void SeleccionarReporte(ReporteOperativoTipo tipo)
    {
        TipoActual = tipo;
        TituloActual = tipo switch
        {
            ReporteOperativoTipo.EntradaMateriaPrima => "Reporte de entrada de materia prima",
            ReporteOperativoTipo.PorProveedor => "Reporte por proveedor",
            ReporteOperativoTipo.ProductosConMerma => "Reporte de productos con más merma",
            _ => "Primeros 10 productos — Reporte de prueba",
        };
        LimpiarFiltrosInterno(); NotificarModo();
    }

    [RelayCommand] private void Volver() { TipoActual = null; LimpiarFiltrosInterno(); NotificarModo(); }
    [RelayCommand(CanExecute = nameof(PuedeSeleccionarProducto))]
    private void AbrirProducto() => SelectorSolicitado?.Invoke("producto");
    [RelayCommand] private void AbrirProveedor() => SelectorSolicitado?.Invoke("proveedor");
    [RelayCommand] private void AbrirCategoria() => SelectorSolicitado?.Invoke("categoria");
    [RelayCommand] private void LimpiarProducto() => ProductoSeleccionado = null;
    [RelayCommand] private void LimpiarProveedor() => ProveedorSeleccionado = null;
    [RelayCommand] private void LimpiarCategoria() => CategoriaSeleccionada = null;
    [RelayCommand(CanExecute = nameof(PuedeExportar))] private void Exportar() => FormatoSolicitado?.Invoke();
    [RelayCommand] private void PrimeraPagina() { if (Pagina > 1) { Pagina = 1; CrearPagina(); } }
    [RelayCommand] private void PaginaAnterior() { if (Pagina > 1) { Pagina--; CrearPagina(); } }
    [RelayCommand] private void PaginaSiguiente() { if (Pagina < TotalPages) { Pagina++; CrearPagina(); } }
    [RelayCommand] private void UltimaPagina() { if (Pagina < TotalPages) { Pagina = TotalPages; CrearPagina(); } }
    [RelayCommand] private void IrAPagina(int p) { if (p >= 1 && p <= TotalPages && p != Pagina) { Pagina = p; CrearPagina(); } }

    partial void OnProductoSeleccionadoChanged(FiltroItem? value) => Invalidar();
    partial void OnProveedorSeleccionadoChanged(FiltroItem? oldValue, FiltroItem? newValue)
    {
        // En Entrada de materia prima el producto depende del proveedor. Si cambia
        // el catálogo padre, una selección anterior ya no es válida.
        if (EsEntrada && oldValue?.Id != newValue?.Id)
            ProductoSeleccionado = null;

        OnPropertyChanged(nameof(PuedeSeleccionarProducto));
        AbrirProductoCommand.NotifyCanExecuteChanged();
        Invalidar();
    }
    partial void OnCategoriaSeleccionadaChanged(FiltroItem? value) => Invalidar();
    partial void OnFechaDesdeChanged(DateTime? value) => Invalidar();
    partial void OnFechaHastaChanged(DateTime? value) => Invalidar();
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(PuedeSeleccionarProducto));
        AbrirProductoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SinResultados));
        OnPropertyChanged(nameof(HayResultados));
        OnPropertyChanged(nameof(HayMetadatosPie));
    }

    [RelayCommand]
    private async Task ConsultarAsync()
    {
        if (_sesion.SesionActual is not { } sesion) { Mensaje = "No hay una sesión activa."; return; }
        if (!SesionPermisos.Tiene(Permiso.ConsultarReporte)) { Mensaje = "No tiene permiso para consultar reportes."; return; }
        if (RequiereProducto && ProductoSeleccionado?.Id is null) { Mensaje = "Seleccione un producto activo."; return; }
        if (RequiereProveedor && ProveedorSeleccionado?.Id is null) { Mensaje = "Seleccione un proveedor activo."; return; }
        if (RequiereFechas && (FechaDesde is null || FechaHasta is null || FechaDesde.Value.Date > FechaHasta.Value.Date)) { Mensaje = "Seleccione un rango de fechas válido."; return; }
        if (RequiereFechas && FechaHasta!.Value.Date > FechaMaxima) { Mensaje = "La fecha hasta no puede ser posterior a la fecha actual."; return; }

        IsBusy = true; Mensaje = "Consultando información..."; Invalidar(sinMensaje: true);
        try
        {
            switch (TipoActual)
            {
                case ReporteOperativoTipo.EntradaMateriaPrima:
                    await CargarEntradaAsync(sesion.IdUsuario); break;
                case ReporteOperativoTipo.PorProveedor:
                    await CargarProveedorAsync(sesion.IdUsuario); break;
                case ReporteOperativoTipo.ProductosConMerma:
                    await CargarMermasAsync(sesion.IdUsuario); break;
                case ReporteOperativoTipo.PrimerosProductos:
                    await CargarPruebaAsync(sesion.IdUsuario); break;
            }
        }
        finally { IsBusy = false; OnPropertyChanged(nameof(PuedeExportar)); ExportarCommand.NotifyCanExecuteChanged(); }
    }

    private async Task CargarEntradaAsync(int usuario)
    {
        var r = await _consultas.ConsultarEntradaMateriaPrimaAsync(new(
            ProductoSeleccionado!.Id!.Value,
            ProveedorSeleccionado!.Id!.Value,
            FechaDesde!.Value,
            FechaHasta!.Value,
            usuario));
        if (!r.Success) { Mensaje = r.Error; return; }
        var rows = r.Value!;
        var placas = ValoresUnicos(rows.Select(x => x.Placa));
        var pesadores = ValoresUnicos(rows.Select(x => x.Pesador));
        _entradaIncluyePlaca = placas.Count > 1;
        _entradaIncluyePesador = pesadores.Count > 1;

        var columnas = new List<ReportColumnDto> { new("Fecha y hora", "dd/MM/yyyy HH:mm") };
        if (_entradaIncluyePlaca) columnas.Add(new("Placa"));
        if (_entradaIncluyePesador) columnas.Add(new("Pesador"));
        columnas.AddRange([new("Peso bruto (kg)", "N3"), new("Peso tara (kg)", "N3"), new("Peso neto (kg)", "N3")]);
        _columnas = columnas;

        _snapshot = rows.Select(x =>
        {
            var fila = new List<object?> { x.FechaHora };
            if (_entradaIncluyePlaca) fila.Add(ValorNormalizado(x.Placa));
            if (_entradaIncluyePesador) fila.Add(ValorNormalizado(x.Pesador));
            fila.AddRange([x.PesoBruto, x.PesoTara, x.PesoNeto]);
            return (IReadOnlyList<object?>)fila;
        }).ToList();
        _ids = rows.Select(x => x.IdPesaje).ToList();
        var filtros = new List<ReportMetadataDto>
        {
            new("Producto", ProductoSeleccionado.Nombre),
            new("Proveedor", ProveedorSeleccionado.Nombre),
        };
        if (!_entradaIncluyePlaca) filtros.Add(new("Placa", placas.SingleOrDefault() ?? "—"));
        if (!_entradaIncluyePesador) filtros.Add(new("Pesador", pesadores.SingleOrDefault() ?? "—"));
        _filtros = filtros;
        _pie = PieFechas();
        _totales = [new("Total peso neto (kg)", rows.Sum(x => x.PesoNeto), "N3")]; FinalizarConsulta();
    }

    private async Task CargarProveedorAsync(int usuario)
    {
        var r = await _consultas.ConsultarProveedorAsync(new(ProveedorSeleccionado!.Id!.Value, FechaDesde!.Value, FechaHasta!.Value, usuario));
        if (!r.Success) { Mensaje = r.Error; return; } var rows = r.Value!;
        _columnas = [new("Fecha de ingreso","dd/MM/yyyy"),new("Producto"),new("Proveedor"),new("Bultos recibidos (estim.)","N3"),new("Peso teórico (kg)","N3"),new("Peso recibido (kg)","N3"),new("Diferencia (kg)","N3"),new("Diferencia monetaria (USD)","N2")];
        string proveedor = ProveedorSeleccionado!.Nombre;
        _snapshot = rows.Select(x => (IReadOnlyList<object?>)[x.FechaIngreso,x.Producto,proveedor,x.BultosEstimados,x.PesoTeorico,x.PesoRecibido,x.DiferenciaKg,x.DiferenciaMonetaria]).ToList();
        _ids = rows.Select(x => x.IdMovimientoProducto).ToList(); _filtros = []; _pie = PieFechas(); _totales = []; FinalizarConsulta();
    }

    private async Task CargarMermasAsync(int usuario)
    {
        var r = await _consultas.ConsultarMermasAsync(new(FechaDesde!.Value, FechaHasta!.Value, CategoriaSeleccionada?.Id, usuario));
        if (!r.Success) { Mensaje = r.Error; return; } var rows = r.Value!;
        _columnas = [new("#"),new("Producto"),new("Proveedor"),new("Categoría"),new("Entradas"),new("Peso teórico (kg)","N3"),new("Peso recibido (kg)","N3"),new("Diferencia (kg)","N3"),new("Merma (%)","N2")];
        int n=0; _snapshot = rows.Select(x => (IReadOnlyList<object?>)[++n,x.Producto,x.Proveedor,x.Categoria,x.CantidadEntradas,x.PesoTeorico,x.PesoRecibido,x.DiferenciaKg,x.MermaPorcentaje]).ToList();
        _ids = rows.Select(x => x.IdProducto).ToList(); _filtros = [new("Categoría",CategoriaSeleccionada is null?"Todas las categorías":Etiqueta(CategoriaSeleccionada))]; _pie = PieFechas();
        decimal teorico=rows.Sum(x=>x.PesoTeorico), recibido=rows.Sum(x=>x.PesoRecibido), diferencia=teorico-recibido;
        _totales=[new("Total de entradas",rows.Sum(x=>x.CantidadEntradas)),new("Peso teórico total (kg)",teorico,"N3"),new("Peso recibido total (kg)",recibido,"N3"),new("Diferencia total (kg)",diferencia,"N3"),new("Merma global (%)",teorico==0?null:diferencia/teorico*100m,"N2")]; FinalizarConsulta();
    }

    private async Task CargarPruebaAsync(int usuario)
    {
        var r=await _consultas.ConsultarPrimerosProductosAsync(usuario); if(!r.Success){Mensaje=r.Error;return;}var rows=r.Value!;
        _columnas=[new("ID"),new("Código"),new("Nombre"),new("Categoría"),new("Proveedor"),new("Estado")];
        _snapshot=rows.Select(x=>(IReadOnlyList<object?>)[x.IdProducto,x.Codigo,x.Nombre,x.Categoria,x.Proveedor,x.Estado]).ToList();
        _ids=rows.Select(x=>x.IdProducto).ToList();_filtros=[new("Criterio","10 productos activos con menor ID")];_pie=[];_totales=[];FinalizarConsulta();
    }

    public async Task ExportarAsync(ReportFormat formato, string ruta, CancellationToken ct=default)
    {
        if(!PuedeExportar||_sesion.SesionActual is not{} sesion)return; IsBusy=true; int? id=null; string? temporal=null;
        try
        {
            var parametros=JsonConvert.SerializeObject(CrearParametrosRegistro());
            var registro=await _registro.RegistrarAsync(new(){NombreReporte=$"{TituloDocumento()} - {DateTime.Now:yyyyMMdd-HHmmss}",TipoReporte=formato==ReportFormat.Pdf?"PDF":"Excel",Descripcion=$"{TituloDocumento()} con {_snapshot.Count} registro(s).",FechaDesde=RequiereFechas?FechaDesde:null,FechaHasta=RequiereFechas?FechaHasta:null,ParametrosJson=parametros,UsuarioIngresando=sesion.IdUsuario},ct);
            if(!registro.Success){Mensaje=registro.Error;return;} id=registro.Value;
            var documento=await CrearDocumentoAsync(sesion);
            var generado=await _generador.GenerateAsync(documento,formato,ct);if(!generado.Success){Mensaje=$"El reporte #{id} quedó registrado, pero el archivo no pudo generarse: {generado.Error}";return;}
            var directorio=Path.GetDirectoryName(ruta)??Directory.GetCurrentDirectory();
            temporal=Path.Combine(directorio,$".{Path.GetFileName(ruta)}.{Guid.NewGuid():N}.tmp");
            await File.WriteAllBytesAsync(temporal,generado.Value!,ct);
            File.Move(temporal,ruta,true);temporal=null;Mensaje=$"Reporte #{id} guardado en {ruta}";ReporteCreado?.Invoke(ruta);
        }
        catch(Exception ex){Mensaje=id.HasValue?$"El reporte #{id} quedó registrado, pero el archivo falló: {ex.Message}":ex.Message;}
        finally{if(temporal is not null&&File.Exists(temporal))File.Delete(temporal);IsBusy=false;OnPropertyChanged(nameof(PuedeExportar));ExportarCommand.NotifyCanExecuteChanged();}
    }

    private async Task<TabularReportDto> CrearDocumentoAsync(CapaDominio.Entities.UsuarioSesion sesion)
    {
        var empresa=await _empresa.ObtenerAsync(); string nombre=empresa.Success?empresa.Value?.NombreEmpresa??"Bimbo Honduras":"Bimbo Honduras";byte[]?logo=null;
        if(empresa.Success&&empresa.Value is not null){var ruta=await _logoCache.ObtenerRutaLocalAsync(empresa.Value.LogoEmpresa);if(!string.IsNullOrWhiteSpace(ruta)&&File.Exists(ruta))logo=await File.ReadAllBytesAsync(ruta);}
        return new(){Title=TituloDocumento(),SheetName=ClaveReporte(),Landscape=_columnas.Count>6,GeneratedAt=DateTime.Now,Branding=new(){CompanyName=nombre,LogoBytes=logo},Author=new(){Email=sesion.Email,Rol=sesion.NombreRol},Columns=_columnas,Rows=_snapshot,Filters=_filtros,Totals=_totales,FooterMetadata=_pie};
    }

    private void FinalizarConsulta()
    {
        Pagina = 1;
        CrearPagina();
        Mensaje = _snapshot.Count == 0 ? "No se encontraron resultados." : $"Vista previa lista: {_snapshot.Count} registro(s).";
        OnPropertyChanged(nameof(PuedeExportar));
        OnPropertyChanged(nameof(SinResultados));
        OnPropertyChanged(nameof(HayResultados));
        OnPropertyChanged(nameof(MetadatosVistaPrevia));
        OnPropertyChanged(nameof(MetadatosPieVistaPrevia));
        OnPropertyChanged(nameof(HayMetadatosPie));
        ExportarCommand.NotifyCanExecuteChanged();
    }

    private void CrearPagina()
    {
        var table = new DataTable();
        foreach (var c in _columnas) table.Columns.Add(c.Header);
        foreach (var row in _snapshot.Skip((Pagina - 1) * PageSize).Take(PageSize))
            table.Rows.Add(row.Select((x, i) => Mostrar(x, _columnas[i].NumberFormat)).ToArray());
        VistaPrevia = table.DefaultView;
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(SinResultados));
        OnPropertyChanged(nameof(HayResultados));
        PrimeraPaginaCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        UltimaPaginaCommand.NotifyCanExecuteChanged();
    }

    private static object Mostrar(object? x, string? formato) => x switch
    {
        null => "—",
        DateTime d => d.ToString(formato ?? "dd/MM/yyyy HH:mm"),
        decimal n => n.ToString(formato ?? "N3", System.Globalization.CultureInfo.GetCultureInfo("es-HN")),
        _ => x
    };

    private IReadOnlyList<ReportMetadataDto> PieFechas() =>
        [new("Desde", FechaDesde!.Value.ToString("dd/MM/yyyy")), new("Hasta", FechaHasta!.Value.ToString("dd/MM/yyyy"))];

    private static string Etiqueta(FiltroItem? x) => x is null ? string.Empty : $"{x.Descripcion} {x.Nombre}".Trim();

    private static IReadOnlyList<string> ValoresUnicos(IEnumerable<string> valores) =>
        valores.Select(ValorNormalizado).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string ValorNormalizado(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "—" : valor.Trim();

    private IReadOnlyList<string> ColumnasEntradaAuditoria()
    {
        var columnas = new List<string> { "fecha_hora" };
        if (_entradaIncluyePlaca) columnas.Add("placa");
        if (_entradaIncluyePesador) columnas.Add("pesador");
        columnas.AddRange(["peso_bruto", "peso_tara", "peso_neto"]);
        return columnas;
    }

    private string TituloDocumento() => TipoActual switch
    {
        ReporteOperativoTipo.EntradaMateriaPrima => "Detalle - Entrada de Materia Prima",
        ReporteOperativoTipo.PorProveedor => "Entrada de productos por proveedor",
        ReporteOperativoTipo.ProductosConMerma => "Reporte de productos con más merma",
        _ => "Primeros 10 productos — Reporte de prueba"
    };

    private string ClaveReporte() => TipoActual switch
    {
        ReporteOperativoTipo.EntradaMateriaPrima => "entrada_materia_prima",
        ReporteOperativoTipo.PorProveedor => "por_proveedor",
        ReporteOperativoTipo.ProductosConMerma => "productos_con_mas_merma",
        _ => "primeros_10_productos"
    };

    private object CrearParametrosRegistro() => TipoActual switch
    {
        ReporteOperativoTipo.EntradaMateriaPrima => new { origen = "pesajes", reporte = "entrada_materia_prima", filtros = new { id_producto = ProductoSeleccionado!.Id, id_proveedor = ProveedorSeleccionado!.Id, fecha_desde = FechaDesde!.Value.ToString("yyyy-MM-dd"), fecha_hasta = FechaHasta!.Value.ToString("yyyy-MM-dd") }, columnas = ColumnasEntradaAuditoria(), ids_registros = _ids, cantidad_registros = _snapshot.Count },
        ReporteOperativoTipo.PorProveedor => new { origen = "pesajes", reporte = "por_proveedor", filtros = new { id_proveedor = ProveedorSeleccionado!.Id, fecha_desde = FechaDesde!.Value.ToString("yyyy-MM-dd"), fecha_hasta = FechaHasta!.Value.ToString("yyyy-MM-dd") }, columnas = new[] { "fecha_ingreso", "nombre_producto", "bultos_estimados", "peso_teorico", "peso_recibido", "diferencia_kg", "diferencia_monetaria" }, ids_registros = _ids, cantidad_registros = _snapshot.Count },
        ReporteOperativoTipo.ProductosConMerma => new { origen = "pesajes", reporte = "productos_con_mas_merma", filtros = new { id_categoria = CategoriaSeleccionada?.Id, todas_las_categorias = CategoriaSeleccionada is null, fecha_desde = FechaDesde!.Value.ToString("yyyy-MM-dd"), fecha_hasta = FechaHasta!.Value.ToString("yyyy-MM-dd") }, columnas = new[] { "producto", "proveedor", "categoria", "entradas", "peso_teorico", "peso_recibido", "diferencia_kg", "merma_porcentaje" }, ids_registros = _ids, cantidad_registros = _snapshot.Count },
        _ => new { origen = "productos", reporte = "primeros_10_productos", filtros = new { solo_activos = true, orden = "id_producto_asc", limite = 10 }, columnas = new[] { "id_producto", "codigo", "nombre", "categoria", "proveedor", "estado" }, ids_registros = _ids, cantidad_registros = _snapshot.Count }
    };

    private void Invalidar(bool sinMensaje = false)
    {
        _snapshot = [];
        _columnas = [];
        _totales = [];
        _filtros = [];
        _pie = [];
        _ids = [];
        _entradaIncluyePlaca = false;
        _entradaIncluyePesador = false;
        VistaPrevia = null;
        Pagina = 1;
        if (!sinMensaje) Mensaje = "Los filtros cambiaron. Consulte nuevamente.";
        OnPropertyChanged(nameof(PuedeExportar));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(SinResultados));
        OnPropertyChanged(nameof(HayResultados));
        OnPropertyChanged(nameof(MetadatosVistaPrevia));
        OnPropertyChanged(nameof(MetadatosPieVistaPrevia));
        OnPropertyChanged(nameof(HayMetadatosPie));
        ExportarCommand.NotifyCanExecuteChanged();
        PrimeraPaginaCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        UltimaPaginaCommand.NotifyCanExecuteChanged();
    }

    private void LimpiarFiltrosInterno()
    {
        ProductoSeleccionado = null;
        ProveedorSeleccionado = null;
        CategoriaSeleccionada = null;
        FechaDesde = null;
        FechaHasta = null;
        Invalidar(true);
        Mensaje = string.Empty;
    }

    private void NotificarModo()
    {
        OnPropertyChanged(nameof(EnMenu));
        OnPropertyChanged(nameof(EsEntrada));
        OnPropertyChanged(nameof(EsProveedor));
        OnPropertyChanged(nameof(EsMermas));
        OnPropertyChanged(nameof(EsPrueba));
        OnPropertyChanged(nameof(RequiereProducto));
        OnPropertyChanged(nameof(RequiereProveedor));
        OnPropertyChanged(nameof(RequiereFechas));
        OnPropertyChanged(nameof(PermiteCategoria));
        OnPropertyChanged(nameof(PuedeSeleccionarProducto));
        AbrirProductoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SinResultados));
        OnPropertyChanged(nameof(HayResultados));
        OnPropertyChanged(nameof(SubtituloBreadcrumb));
        OnPropertyChanged(nameof(TituloEncabezado));
    }
}
