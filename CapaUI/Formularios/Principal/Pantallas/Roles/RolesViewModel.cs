using System.Collections.ObjectModel;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Roles;

public enum FiltroEstadoPermiso { Activos, Inactivos, Todos }

/// <summary>
/// Una acción del catálogo. Se construye UNA sola vez por sesión de pantalla:
/// cambiar de rol solo mueve <see cref="Asignada"/>, nunca recrea la lista.
/// </summary>
public sealed partial class AccionItemVm : ObservableObject
{
    public int IdAccion { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Marca visual de riesgo. Es criterio de UI, no una columna de la BD.</summary>
    public bool EsCritica { get; init; }

    /// <summary>
    /// El comando y el permiso viajan en el propio item para que la plantilla
    /// bindee contra su DataContext. Antes se alcanzaban con
    /// <c>RelativeSource AncestorType=UserControl</c>, que recorre ~20 niveles
    /// del árbol visual por binding y se repetía en las 28 filas.
    /// </summary>
    public System.Windows.Input.ICommand? Alternar { get; init; }
    public bool PuedeEditar { get; init; }

    [ObservableProperty] private bool _asignada;
    [ObservableProperty] private bool _visible = true;
}

public sealed partial class ModuloItemVm : ObservableObject
{
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string IconoKey { get; init; } = "modulo";

    /// <summary>Columnas que ocupa la tarjeta en la grilla exterior.</summary>
    public int Span { get; init; } = 1;

    /// <summary>Columnas de la grilla interna de permisos.</summary>
    public int ColumnasInternas { get; init; } = 1;

    public IReadOnlyList<AccionItemVm> Acciones { get; init; } = Array.Empty<AccionItemVm>();
    public int Total => Acciones.Count;

    /// <summary>Ver la nota en <see cref="AccionItemVm.Alternar"/>.</summary>
    public System.Windows.Input.ICommand? Alternar { get; init; }
    public bool PuedeEditar { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Conteo), nameof(TodasActivas), nameof(NingunaActiva))]
    private int _activas;

    [ObservableProperty] private bool _visible = true;

    public string Conteo => $"{Activas}/{Total}";
    public bool TodasActivas => Total > 0 && Activas == Total;
    public bool NingunaActiva => Activas == 0;
}

public sealed partial class RolItemVm : ObservableObject
{
    public int IdRol { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public bool EsSistema { get; init; }

    [ObservableProperty] private int _permisos;
}

/// <summary>Opción de un selector segmentado (filtro de estado / modo de vista).</summary>
public sealed class OpcionSegmento
{
    public string Etiqueta { get; init; } = string.Empty;
    public string? ColorPunto { get; init; }
    public object? Valor { get; init; }
}

public sealed partial class RolesViewModel : ObservableObject, IDisposable
{
    private readonly IRolPermisoRepository _permisosRepo;
    private readonly IUsuarioSesionService _sesion;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Debounce del filtro de permisos. Cada <c>AplicarFiltro</c> escribe
    /// <c>Visible</c> en 28 acciones + 6 módulos, y esa propiedad está bindeada a
    /// <c>ContentPresenter.Visibility</c>, que invalida el measure del padre: sin
    /// espera, cada tecla dispara una pasada de layout completa.
    ///
    /// No se reutiliza <c>SuggestionDebouncer</c> porque está tipado a
    /// sugerencias y corta en seco con la query vacía — acá vaciar el buscador
    /// tiene que volver a mostrar todo.
    /// </summary>
    private const int FiltroDebounceMs = 180;
    private CancellationTokenSource? _ctsFiltro;

    /// <summary>Última versión guardada en BD por rol. Es la base contra la que se cuentan los cambios.</summary>
    private readonly Dictionary<int, HashSet<int>> _guardadoPorRol = new();

    private bool _cargado;
    private bool _disposed;

    public event Action<string>? Toast;

    public RolesViewModel(IRolPermisoRepository permisosRepo, IUsuarioSesionService sesion)
    {
        _permisosRepo = permisosRepo;
        _sesion = sesion;

        // Se fijan acá y no en CargarAsync: si se asignaran después del await, los
        // selectores segmentados vivirían sin selección hasta que responda la BD y
        // luego saltarían a su estado real, que es parte del parpadeo de maquetado.
        OpcionEstado = OpcionesEstado[2];   // Todos
    }

    // ── Colecciones ────────────────────────────────────────────────────────
    public ObservableCollection<RolItemVm> Roles { get; } = new();
    public ObservableCollection<ModuloItemVm> Modulos { get; } = new();

    public IReadOnlyList<OpcionSegmento> OpcionesEstado { get; } = new[]
    {
        new OpcionSegmento { Etiqueta = "Activos",   ColorPunto = "#10B981", Valor = FiltroEstadoPermiso.Activos },
        new OpcionSegmento { Etiqueta = "Inactivos", ColorPunto = "#94A3B8", Valor = FiltroEstadoPermiso.Inactivos },
        new OpcionSegmento { Etiqueta = "Todos",     Valor = FiltroEstadoPermiso.Todos },
    };

    // ── Estado ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje), nameof(TextoAlerta), nameof(TonoAlerta), nameof(HayAlerta))]
    private string _mensaje = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TonoAlerta))]
    private bool _esError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsSistemaSeleccionado), nameof(TextoAlerta), nameof(TonoAlerta), nameof(HayAlerta))]
    private RolItemVm? _rolSeleccionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Inactivos))]
    private int _activos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayCambios), nameof(TextoCambios), nameof(TextoAlerta), nameof(TonoAlerta), nameof(HayAlerta))]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(DescartarCommand))]
    private int _cambios;

    [ObservableProperty] private int _totalAcciones;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Filtrando), nameof(HayQuery))]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Filtrando))]
    private OpcionSegmento? _opcionEstado;

    [ObservableProperty] private bool _sinResultados;

    // ── Derivados ──────────────────────────────────────────────────────────
    public bool PuedeEditar => _sesion.TienePermiso("Modificar Configuración");
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    public bool HayQuery => Query.Length > 0;
    public bool HayCambios => Cambios > 0;
    public int Inactivos => TotalAcciones - Activos;
    /// <summary>
    /// La pantalla tiene UN solo maquetado. Antes había modo Compacta y Detalle,
    /// y eso era la causa del parpadeo: los estilos de la tarjeta se resolvían
    /// con <c>RelativeSource AncestorType</c>, que tarda un frame, mientras que
    /// esta propiedad se bindea directo al DataContext y resuelve al instante.
    /// La pantalla llegaba a dibujarse en un estado híbrido antes de asentarse.
    /// </summary>
    public int ColumnasGrilla => 4;
    public bool EsSistemaSeleccionado => RolSeleccionado?.EsSistema == true;
    public bool Filtrando =>
        !string.IsNullOrWhiteSpace(Query) || (OpcionEstado?.Valor is FiltroEstadoPermiso f && f != FiltroEstadoPermiso.Todos);

    public string TextoCambios => Cambios switch
    {
        0 => "Sin cambios pendientes",
        1 => "1 cambio sin guardar",
        _ => $"{Cambios} cambios sin guardar",
    };

    /// <summary>
    /// Una sola franja de aviso, con prioridad: error de la BD &gt; resultado del
    /// guardado &gt; advertencia por cambios sin aplicar &gt; nota del rol de sistema.
    /// </summary>
    public string? TextoAlerta
    {
        get
        {
            if (HayMensaje) return Mensaje;
            if (HayCambios && RolSeleccionado is not null)
                return $"Los usuarios con el rol {RolSeleccionado.Nombre} deben iniciar sesión de nuevo para que los cambios apliquen.";
            if (EsSistemaSeleccionado)
                return "Rol del sistema. Quitar permisos de Configuración puede dejar la instalación sin administrador.";
            return null;
        }
    }

    public string TonoAlerta => EsError ? "error" : (HayMensaje || HayCambios) ? "warn" : "info";
    public bool HayAlerta => TextoAlerta is not null;

    // ── Carga ──────────────────────────────────────────────────────────────
    public async Task CargarAsync()
    {
        if (_cargado || _disposed) return;

        IsLoading = true;
        Mensaje = string.Empty;

        var resultado = await _permisosRepo.ObtenerResumenAsync(_cts.Token);
        if (_disposed) return;

        if (!resultado.Success)
        {
            MostrarError(resultado.Error);
            IsLoading = false;
            return;
        }

        var datos = resultado.Value!;

        bool puedeEditar = PuedeEditar;

        foreach (var modulo in datos.Modulos)
        {
            bool ancho = modulo.Acciones.Count >= 6;
            Modulos.Add(new ModuloItemVm
            {
                Nombre = modulo.NombreModulo,
                Descripcion = modulo.DescripcionModulo ?? string.Empty,
                IconoKey = ResolverIcono(modulo.NombreModulo),
                Span = ancho ? 2 : 1,
                ColumnasInternas = ancho ? 2 : 1,
                Alternar = AlternarModuloCommand,
                PuedeEditar = puedeEditar,
                Acciones = modulo.Acciones.Select(a => new AccionItemVm
                {
                    IdAccion = a.IdAccion,
                    Nombre = a.NombreAccion,
                    Descripcion = a.DescripcionAccion ?? string.Empty,
                    EsCritica = EsAccionCritica(a.NombreAccion),
                    Alternar = AlternarAccionCommand,
                    PuedeEditar = puedeEditar,
                }).ToArray(),
            });
        }

        TotalAcciones = Modulos.Sum(m => m.Total);

        foreach (var rol in datos.Roles)
        {
            var asignadas = datos.AccionesPorRol.TryGetValue(rol.IdRol, out var set)
                ? new HashSet<int>(set)
                : new HashSet<int>();
            _guardadoPorRol[rol.IdRol] = asignadas;

            Roles.Add(new RolItemVm
            {
                IdRol = rol.IdRol,
                Nombre = rol.NombreRol,
                EsSistema = rol.NombreRol.Contains("admin", StringComparison.OrdinalIgnoreCase),
                Permisos = asignadas.Count,
            });
        }

        _cargado = true;
        IsLoading = false;

        RolSeleccionado = Roles.FirstOrDefault();
    }

    partial void OnRolSeleccionadoChanged(RolItemVm? oldValue, RolItemVm? newValue)
    {
        if (newValue is null) return;

        Mensaje = string.Empty;
        Query = string.Empty;
        RestaurarDesdeGuardado();
    }

    /// <summary>
    /// Vuelve el tablero a la última versión guardada del rol activo.
    /// No recrea nada ni toca la red: solo mueve el booleano de cada acción
    /// que ya está en pantalla.
    /// </summary>
    private void RestaurarDesdeGuardado()
    {
        if (RolSeleccionado is null) return;

        var asignadas = _guardadoPorRol.TryGetValue(RolSeleccionado.IdRol, out var set)
            ? set
            : new HashSet<int>();

        foreach (var modulo in Modulos)
            foreach (var accion in modulo.Acciones)
                accion.Asignada = asignadas.Contains(accion.IdAccion);

        Recalcular();
        AplicarFiltro();
    }

    partial void OnQueryChanged(string value) => _ = FiltrarConEsperaAsync();

    private async Task FiltrarConEsperaAsync()
    {
        _ctsFiltro?.Cancel();
        _ctsFiltro?.Dispose();
        _ctsFiltro = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var token = _ctsFiltro.Token;

        try
        {
            await Task.Delay(FiltroDebounceMs, token);
            if (token.IsCancellationRequested || _disposed) return;
            AplicarFiltro();
        }
        // El usuario siguió escribiendo: la pasada anterior se descarta.
        catch (OperationCanceledException) { }
    }

    partial void OnOpcionEstadoChanged(OpcionSegmento? value) => AplicarFiltro();

    // ── Comandos ───────────────────────────────────────────────────────────

    /// <summary>
    /// Toda mutación pasa por acá. Es deliberado: así el padre mantiene los
    /// contadores exactos sin suscribirse al PropertyChanged de las 28 acciones
    /// (28 suscripciones que además habría que desenganchar al salir).
    /// </summary>
    [RelayCommand]
    private void AlternarAccion(AccionItemVm? accion)
    {
        if (accion is null || !PuedeEditar) return;
        accion.Asignada = !accion.Asignada;
        Recalcular();
        AplicarFiltro();
    }

    [RelayCommand]
    private void AlternarModulo(ModuloItemVm? modulo)
    {
        if (modulo is null || !PuedeEditar) return;
        bool activar = !modulo.TodasActivas;
        foreach (var accion in modulo.Acciones)
            accion.Asignada = activar;
        Recalcular();
        AplicarFiltro();
    }

    [RelayCommand]
    private void ActivarTodo() => AsignarTodas(true);

    [RelayCommand]
    private void QuitarTodo() => AsignarTodas(false);

    private void AsignarTodas(bool valor)
    {
        if (!PuedeEditar) return;
        foreach (var modulo in Modulos)
            foreach (var accion in modulo.Acciones)
                accion.Asignada = valor;
        Recalcular();
        AplicarFiltro();
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        Query = string.Empty;
        OpcionEstado = OpcionesEstado[2];
    }

    [RelayCommand]
    private void LimpiarBusqueda() => Query = string.Empty;

    /// <summary>Lo llama el ComboBox de la vista con el id que trae en el Tag.</summary>
    public void SeleccionarRol(int idRol)
    {
        var rol = Roles.FirstOrDefault(r => r.IdRol == idRol);
        if (rol is not null && rol != RolSeleccionado)
            RolSeleccionado = rol;
    }

    /// <summary>Deja la pantalla en un estado legible si la carga inicial falla.</summary>
    public void MostrarErrorCarga(string mensaje)
    {
        MostrarError(mensaje);
        IsLoading = false;
    }

    [RelayCommand(CanExecute = nameof(HayCambios))]
    private void Descartar()
    {
        Mensaje = string.Empty;
        RestaurarDesdeGuardado();
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (RolSeleccionado is null || _disposed) return;

        IsLoading = true;
        Mensaje = string.Empty;

        var ids = Modulos.SelectMany(m => m.Acciones)
            .Where(a => a.Asignada)
            .Select(a => a.IdAccion)
            .ToList();

        var resultado = await _permisosRepo.GuardarAsignacionesAsync(RolSeleccionado.IdRol, ids, _cts.Token);
        if (_disposed) return;

        if (!resultado.Success)
        {
            MostrarError(resultado.Error);
            IsLoading = false;
            return;
        }

        _guardadoPorRol[RolSeleccionado.IdRol] = new HashSet<int>(ids);
        RolSeleccionado.Permisos = ids.Count;

        EsError = false;
        Mensaje = $"Los usuarios con el rol {RolSeleccionado.Nombre} deben iniciar sesión de nuevo para que los cambios apliquen.";
        Toast?.Invoke($"Permisos de {RolSeleccionado.Nombre} actualizados");

        IsLoading = false;
        Recalcular();
    }

    private bool PuedeGuardar() => PuedeEditar && HayCambios && !IsLoading;

    // ── Recálculo y filtrado ───────────────────────────────────────────────

    /// <summary>
    /// Una sola pasada sobre las acciones actualiza contadores por módulo,
    /// total de activos y cantidad de cambios sin guardar. Son ~28 elementos:
    /// recalcular entero es más barato (y más difícil de romper) que llevar
    /// contadores incrementales.
    /// </summary>
    private void Recalcular()
    {
        var baseRol = RolSeleccionado is not null && _guardadoPorRol.TryGetValue(RolSeleccionado.IdRol, out var s)
            ? s
            : null;

        int activos = 0, cambios = 0;

        foreach (var modulo in Modulos)
        {
            int activasModulo = 0;
            foreach (var accion in modulo.Acciones)
            {
                if (accion.Asignada)
                {
                    activasModulo++;
                    activos++;
                }
                if (baseRol is not null && accion.Asignada != baseRol.Contains(accion.IdAccion))
                    cambios++;
            }
            modulo.Activas = activasModulo;
        }

        Activos = activos;
        Cambios = cambios;
    }

    private void AplicarFiltro()
    {
        string q = Query.Trim();
        var estado = OpcionEstado?.Valor as FiltroEstadoPermiso? ?? FiltroEstadoPermiso.Todos;
        bool algunoVisible = false;

        foreach (var modulo in Modulos)
        {
            bool moduloVisible = false;
            foreach (var accion in modulo.Acciones)
            {
                bool coincide = Coincide(accion, q, estado);
                accion.Visible = coincide;
                moduloVisible |= coincide;
            }
            modulo.Visible = moduloVisible;
            algunoVisible |= moduloVisible;
        }

        SinResultados = _cargado && !algunoVisible;
    }

    private static bool Coincide(AccionItemVm accion, string query, FiltroEstadoPermiso estado)
    {
        if (estado == FiltroEstadoPermiso.Activos && !accion.Asignada) return false;
        if (estado == FiltroEstadoPermiso.Inactivos && accion.Asignada) return false;
        if (query.Length == 0) return true;

        return accion.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase)
            || accion.Descripcion.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    // ── Criterios de presentación ──────────────────────────────────────────

    /// <summary>
    /// Heurística de UI para resaltar acciones destructivas o de configuración.
    /// El catálogo de <c>public.acciones</c> no tiene una columna de criticidad,
    /// así que se deduce del verbo del nombre.
    /// </summary>
    private static bool EsAccionCritica(string nombreAccion) =>
        nombreAccion.StartsWith("Eliminar", StringComparison.OrdinalIgnoreCase)
        || nombreAccion.StartsWith("Cancelar", StringComparison.OrdinalIgnoreCase)
        || nombreAccion.Equals("Modificar Configuración", StringComparison.Ordinal);

    private static string ResolverIcono(string nombreModulo)
    {
        if (Contiene(nombreModulo, "config")) return "gear";
        if (Contiene(nombreModulo, "emplead")) return "users";
        if (Contiene(nombreModulo, "inventario") || Contiene(nombreModulo, "producto")) return "box";
        if (Contiene(nombreModulo, "pesaje")) return "scale";
        if (Contiene(nombreModulo, "proveedor") || Contiene(nombreModulo, "fabricante")) return "truck";
        if (Contiene(nombreModulo, "rol") || Contiene(nombreModulo, "permiso")) return "shield";
        return "modulo";

        static bool Contiene(string texto, string parte) =>
            texto.Contains(parte, StringComparison.OrdinalIgnoreCase);
    }

    private void MostrarError(string mensaje)
    {
        EsError = true;
        Mensaje = mensaje;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Corta cualquier consulta en vuelo: sin esto, la continuación podría
        // escribir sobre un ViewModel que ya no está en pantalla.
        _cts.Cancel();
        _cts.Dispose();
        _ctsFiltro?.Dispose();

        Toast = null;
        Modulos.Clear();
        Roles.Clear();
        _guardadoPorRol.Clear();
    }
}
