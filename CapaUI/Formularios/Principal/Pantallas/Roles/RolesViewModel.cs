using System.Collections.ObjectModel;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Roles;

public enum FiltroEstadoPermiso { Activos, Inactivos, Todos }
public enum FiltroEstadoRol { Activos, Inactivos, Todos }
public enum DecisionCambiosPendientes { Guardar, Descartar, SeguirEditando }

public sealed partial class AccionItemVm : ObservableObject
{
    public int IdAccion { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public bool EsCritica { get; init; }
    public System.Windows.Input.ICommand? Alternar { get; init; }
    public bool PuedeAlternar => PuedeEditar && !EsProtegida;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAlternar))]
    private bool _puedeEditar;
    [ObservableProperty] private bool _asignada;
    [ObservableProperty] private bool _visible = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAlternar))]
    private bool _esProtegida;
}

public sealed partial class ModuloItemVm : ObservableObject
{
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string IconoKey { get; init; } = "modulo";
    public int Span { get; init; } = 1;
    public int ColumnasInternas { get; init; } = 1;
    public IReadOnlyList<AccionItemVm> Acciones { get; init; } = Array.Empty<AccionItemVm>();
    public int Total => Acciones.Count;
    public System.Windows.Input.ICommand? Alternar { get; init; }

    [ObservableProperty] private bool _puedeEditar;
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
    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsActivo), nameof(EstadoTexto))]
    private int _idEstado = 1;
    public bool EsSistema { get; init; }
    public bool PuedeEditarNombre { get; init; }
    public bool PuedeCambiarEstado { get; init; }
    [ObservableProperty] private int _usuariosAsignados;
    [ObservableProperty] private int _permisos;
    [ObservableProperty] private bool _visible = true;

    public bool EsActivo => IdEstado == 1;
    public string EstadoTexto => EsActivo ? "Activo" : "Inactivo";
}

public sealed class OpcionSegmento
{
    public string Etiqueta { get; init; } = string.Empty;
    public string? ColorPunto { get; init; }
    public object? Valor { get; init; }
}

public sealed partial class RolesViewModel : ObservableObject, IDisposable
{
    private readonly IRolPermisoRepository _permisosRepo;
    private readonly IRolRepository _rolesRepo;
    private readonly IUsuarioSesionService _sesion;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<int, HashSet<int>> _guardadoPorRol = new();
    private const int FiltroDebounceMs = 180;
    private CancellationTokenSource? _ctsFiltro;
    private bool _cargado;
    private bool _disposed;

    public event Action<string>? Toast;
    public event Action<RolItemVm?>? SolicitarEdicionRol;
    public event Func<RolItemVm, bool>? ConfirmarCambioEstado;
    public event Func<DecisionCambiosPendientes>? ConfirmarCambiosPendientes;

    public RolesViewModel(
        IRolPermisoRepository permisosRepo,
        IRolRepository rolesRepo,
        IUsuarioSesionService sesion)
    {
        _permisosRepo = permisosRepo;
        _rolesRepo = rolesRepo;
        _sesion = sesion;
        OpcionEstado = OpcionesEstado[2];
        OpcionEstadoRol = OpcionesEstadoRol[2];
    }

    public ObservableCollection<RolItemVm> Roles { get; } = new();
    public ObservableCollection<ModuloItemVm> Modulos { get; } = new();

    public IReadOnlyList<OpcionSegmento> OpcionesEstado { get; } =
    [
        new() { Etiqueta = "Activos", ColorPunto = "#10B981", Valor = FiltroEstadoPermiso.Activos },
        new() { Etiqueta = "Inactivos", ColorPunto = "#94A3B8", Valor = FiltroEstadoPermiso.Inactivos },
        new() { Etiqueta = "Todos", Valor = FiltroEstadoPermiso.Todos },
    ];

    public IReadOnlyList<OpcionSegmento> OpcionesEstadoRol { get; } =
    [
        new() { Etiqueta = "Activos", ColorPunto = "#10B981", Valor = FiltroEstadoRol.Activos },
        new() { Etiqueta = "Inactivos", ColorPunto = "#94A3B8", Valor = FiltroEstadoRol.Inactivos },
        new() { Etiqueta = "Todos", Valor = FiltroEstadoRol.Todos },
    ];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje), nameof(TextoAlerta), nameof(TonoAlerta), nameof(HayAlerta))]
    private string _mensaje = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TonoAlerta))]
    private bool _esError;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsSistemaSeleccionado), nameof(PuedeEditarPermisosSeleccionados), nameof(TextoAlerta), nameof(TonoAlerta), nameof(HayAlerta))]
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
    [ObservableProperty] private bool _mostrandoDetalle;
    [ObservableProperty] private string _queryRoles = string.Empty;
    [ObservableProperty] private OpcionSegmento? _opcionEstadoRol;
    [ObservableProperty] private bool _sinRoles;

    public bool PuedeConsultar => _sesion.TienePermiso("Consultar Rol");
    public bool PuedeCrearRol => _sesion.TienePermiso("Crear Rol");
    public bool PuedeModificarRol => _sesion.TienePermiso("Modificar Rol");
    public bool PuedeEliminarRol => _sesion.TienePermiso("Eliminar Rol");
    public bool PuedeEditar => _sesion.TienePermiso("Asignar Permisos a Rol");
    public bool PuedeEditarPermisosSeleccionados => PuedeEditar && RolSeleccionado?.EsActivo == true && !EsSistemaSeleccionado;
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    public bool HayQuery => Query.Length > 0;
    public bool HayCambios => Cambios > 0;
    public int Inactivos => TotalAcciones - Activos;
    public int TotalRoles => Roles.Count;
    public int RolesActivos => Roles.Count(x => x.EsActivo);
    public int RolesInactivos => Roles.Count(x => !x.EsActivo);
    public int ColumnasGrilla => 4;
    public bool EsSistemaSeleccionado => RolSeleccionado?.EsSistema == true;
    public bool Filtrando => !string.IsNullOrWhiteSpace(Query)
        || (OpcionEstado?.Valor is FiltroEstadoPermiso f && f != FiltroEstadoPermiso.Todos);

    public string TextoCambios => Cambios switch
    {
        0 => "Sin cambios pendientes",
        1 => "1 cambio sin guardar",
        _ => $"{Cambios} cambios sin guardar",
    };

    public string? TextoAlerta
    {
        get
        {
            if (HayMensaje) return Mensaje;
            if (HayCambios && RolSeleccionado is not null)
                return $"El cambio afectará a {RolSeleccionado.UsuariosAsignados} usuario(s) con el rol {RolSeleccionado.Nombre}. Deben iniciar sesión de nuevo.";
            if (EsSistemaSeleccionado)
                return "Rol de sistema. Su nombre, estado y todos sus permisos son inmutables.";
            return null;
        }
    }

    public string TonoAlerta => EsError ? "error" : (HayMensaje || HayCambios) ? "warn" : "info";
    public bool HayAlerta => TextoAlerta is not null;

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
                PuedeEditar = PuedeEditar,
                Acciones = modulo.Acciones.Select(a => new AccionItemVm
                {
                    IdAccion = a.IdAccion,
                    Nombre = a.NombreAccion,
                    Descripcion = a.DescripcionAccion ?? string.Empty,
                    EsCritica = EsAccionCritica(a.NombreAccion),
                    Alternar = AlternarAccionCommand,
                    PuedeEditar = PuedeEditar,
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
            Roles.Add(CrearRolItem(rol, asignadas.Count));
        }

        NotificarConteosRoles();
        _cargado = true;
        IsLoading = false;
        RolSeleccionado = null;
        MostrandoDetalle = false;
        AplicarFiltroRoles();
    }

    private RolItemVm CrearRolItem(RolDto rol, int permisos) => new()
    {
        IdRol = rol.IdRol,
        Nombre = rol.NombreRol,
        IdEstado = rol.IdEstado,
        EsSistema = rol.EsSistema,
        PuedeEditarNombre = PuedeModificarRol && !rol.EsSistema,
        PuedeCambiarEstado = PuedeEliminarRol && !rol.EsSistema,
        UsuariosAsignados = rol.UsuariosAsignados,
        Permisos = permisos,
    };

    partial void OnRolSeleccionadoChanged(RolItemVm? value)
    {
        if (value is null) return;
        Mensaje = string.Empty;
        Query = string.Empty;
        RestaurarDesdeGuardado();
    }

    partial void OnQueryChanged(string value) => _ = FiltrarConEsperaAsync(false);
    partial void OnQueryRolesChanged(string value) => _ = FiltrarConEsperaAsync(true);
    partial void OnOpcionEstadoChanged(OpcionSegmento? value) => AplicarFiltro();
    partial void OnOpcionEstadoRolChanged(OpcionSegmento? value) => AplicarFiltroRoles();

    private async Task FiltrarConEsperaAsync(bool roles)
    {
        _ctsFiltro?.Cancel();
        _ctsFiltro?.Dispose();
        _ctsFiltro = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var token = _ctsFiltro.Token;
        try
        {
            await Task.Delay(FiltroDebounceMs, token);
            if (token.IsCancellationRequested || _disposed) return;
            if (roles) AplicarFiltroRoles(); else AplicarFiltro();
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private void NuevoRol()
    {
        if (PuedeCrearRol) SolicitarEdicionRol?.Invoke(null);
    }

    [RelayCommand]
    private void EditarRol(RolItemVm? rol)
    {
        if (rol is not null && PuedeModificarRol && !rol.EsSistema)
            SolicitarEdicionRol?.Invoke(rol);
    }

    public async Task<bool> GuardarRolAsync(RolItemVm? rol, string nombre)
    {
        if (_disposed) return false;
        IsLoading = true;
        Mensaje = string.Empty;
        var resultado = rol is null
            ? await _rolesRepo.CrearAsync(nombre.Trim(), _cts.Token)
            : await _rolesRepo.ActualizarAsync(rol.IdRol, nombre.Trim(), _cts.Token);
        if (_disposed) return false;
        IsLoading = false;

        if (!resultado.Success)
        {
            MostrarError(resultado.Error);
            return false;
        }

        if (rol is null)
        {
            var nuevo = CrearRolItem(resultado.Value!, 0);
            Roles.Add(nuevo);
            _guardadoPorRol[nuevo.IdRol] = new HashSet<int>();
            AbrirDetalleRol(nuevo.IdRol);
            Toast?.Invoke($"Rol {nuevo.Nombre} creado");
        }
        else
        {
            rol.Nombre = resultado.Value!.NombreRol;
            Toast?.Invoke($"Rol {rol.Nombre} actualizado");
        }

        NotificarConteosRoles();
        AplicarFiltroRoles();
        return true;
    }

    [RelayCommand]
    private async Task AlternarEstadoRolAsync(RolItemVm? rol)
    {
        if (rol is null || rol.EsSistema || !PuedeEliminarRol || _disposed) return;
        if (rol.EsActivo && rol.UsuariosAsignados > 0)
        {
            MostrarError($"No se puede desactivar {rol.Nombre}: tiene {rol.UsuariosAsignados} usuario(s) asignado(s).");
            return;
        }
        if (ConfirmarCambioEstado?.Invoke(rol) == false) return;

        IsLoading = true;
        var resultado = await _rolesRepo.CambiarEstadoAsync(rol.IdRol, rol.EsActivo ? 2 : 1, _cts.Token);
        if (_disposed) return;
        IsLoading = false;
        if (!resultado.Success)
        {
            MostrarError(resultado.Error);
            return;
        }

        rol.IdEstado = resultado.Value!.IdEstado;
        if (ReferenceEquals(rol, RolSeleccionado))
        {
            OnPropertyChanged(nameof(PuedeEditarPermisosSeleccionados));
            RestaurarDesdeGuardado();
        }
        EsError = false;
        Mensaje = $"Rol {rol.Nombre} {rol.EstadoTexto.ToLowerInvariant()}.";
        Toast?.Invoke(Mensaje);
        NotificarConteosRoles();
        AplicarFiltroRoles();
    }

    [RelayCommand]
    private void AlternarAccion(AccionItemVm? accion)
    {
        if (accion is null || !PuedeEditarPermisosSeleccionados || accion.EsProtegida) return;
        accion.Asignada = !accion.Asignada;
        Recalcular();
        AplicarFiltro();
    }

    [RelayCommand]
    private void AlternarModulo(ModuloItemVm? modulo)
    {
        if (modulo is null || !PuedeEditarPermisosSeleccionados) return;
        bool activar = !modulo.TodasActivas;
        foreach (var accion in modulo.Acciones)
            if (!accion.EsProtegida || activar)
                accion.Asignada = activar;
        Recalcular();
        AplicarFiltro();
    }

    [RelayCommand] private void ActivarTodo() => AsignarTodas(true);
    [RelayCommand] private void QuitarTodo() => AsignarTodas(false);

    private void AsignarTodas(bool valor)
    {
        if (!PuedeEditarPermisosSeleccionados) return;
        foreach (var accion in Modulos.SelectMany(m => m.Acciones))
            if (!accion.EsProtegida || valor)
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
    private void LimpiarFiltrosRoles()
    {
        QueryRoles = string.Empty;
        OpcionEstadoRol = OpcionesEstadoRol[2];
    }

    [RelayCommand] private void LimpiarBusqueda() => Query = string.Empty;

    [RelayCommand]
    private void AbrirDetalleRol(int idRol)
    {
        if (!PuedeConsultar) return;
        var rol = Roles.FirstOrDefault(r => r.IdRol == idRol);
        if (rol is null) return;
        RolSeleccionado = rol;
        MostrandoDetalle = true;
    }

    [RelayCommand]
    private async Task VolverAListaAsync()
    {
        if (!MostrandoDetalle) return;
        if (HayCambios)
        {
            var decision = ConfirmarCambiosPendientes?.Invoke() ?? DecisionCambiosPendientes.SeguirEditando;
            if (decision == DecisionCambiosPendientes.SeguirEditando) return;
            if (decision == DecisionCambiosPendientes.Guardar && !await GuardarPermisosAsync()) return;
            if (decision == DecisionCambiosPendientes.Descartar) RestaurarDesdeGuardado();
        }

        Mensaje = string.Empty;
        RolSeleccionado = null;
        MostrandoDetalle = false;
    }

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
    private Task GuardarAsync() => GuardarPermisosAsync();

    private async Task<bool> GuardarPermisosAsync()
    {
        if (RolSeleccionado is null || !PuedeEditarPermisosSeleccionados || _disposed) return false;
        IsLoading = true;
        Mensaje = string.Empty;
        var ids = Modulos.SelectMany(m => m.Acciones).Where(a => a.Asignada).Select(a => a.IdAccion).ToList();
        var resultado = await _permisosRepo.GuardarAsignacionesAsync(RolSeleccionado.IdRol, ids, _cts.Token);
        if (_disposed) return false;
        IsLoading = false;
        if (!resultado.Success)
        {
            MostrarError(resultado.Error);
            return false;
        }

        _guardadoPorRol[RolSeleccionado.IdRol] = new HashSet<int>(ids);
        RolSeleccionado.Permisos = ids.Count;
        EsError = false;
        Mensaje = $"Los usuarios con el rol {RolSeleccionado.Nombre} deben iniciar sesión de nuevo para que los cambios apliquen.";
        Toast?.Invoke($"Permisos de {RolSeleccionado.Nombre} actualizados");
        Recalcular();
        return true;
    }

    private bool PuedeGuardar() => PuedeEditarPermisosSeleccionados && HayCambios && !IsLoading;

    private void RestaurarDesdeGuardado()
    {
        if (RolSeleccionado is null) return;
        var asignadas = _guardadoPorRol.TryGetValue(RolSeleccionado.IdRol, out var set) ? set : [];
        foreach (var modulo in Modulos)
        {
            modulo.PuedeEditar = PuedeEditarPermisosSeleccionados;
            foreach (var accion in modulo.Acciones)
            {
                accion.PuedeEditar = PuedeEditarPermisosSeleccionados;
                accion.EsProtegida = RolSeleccionado.EsSistema;
                accion.Asignada = asignadas.Contains(accion.IdAccion) || accion.EsProtegida;
            }
        }
        Recalcular();
        AplicarFiltro();
    }

    private void Recalcular()
    {
        var baseRol = RolSeleccionado is not null && _guardadoPorRol.TryGetValue(RolSeleccionado.IdRol, out var s) ? s : null;
        int activos = 0, cambios = 0;
        foreach (var modulo in Modulos)
        {
            int activasModulo = 0;
            foreach (var accion in modulo.Acciones)
            {
                if (accion.Asignada) { activasModulo++; activos++; }
                if (baseRol is not null && accion.Asignada != baseRol.Contains(accion.IdAccion)) cambios++;
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
        bool alguno = false;
        foreach (var modulo in Modulos)
        {
            bool visibleModulo = false;
            foreach (var accion in modulo.Acciones)
            {
                bool visible = (estado != FiltroEstadoPermiso.Activos || accion.Asignada)
                    && (estado != FiltroEstadoPermiso.Inactivos || !accion.Asignada)
                    && (q.Length == 0 || accion.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || accion.Descripcion.Contains(q, StringComparison.OrdinalIgnoreCase));
                accion.Visible = visible;
                visibleModulo |= visible;
            }
            modulo.Visible = visibleModulo;
            alguno |= visibleModulo;
        }
        SinResultados = _cargado && !alguno;
    }

    private void AplicarFiltroRoles()
    {
        string q = QueryRoles.Trim();
        var estado = OpcionEstadoRol?.Valor as FiltroEstadoRol? ?? FiltroEstadoRol.Todos;
        bool alguno = false;
        foreach (var rol in Roles)
        {
            rol.Visible = (estado != FiltroEstadoRol.Activos || rol.EsActivo)
                && (estado != FiltroEstadoRol.Inactivos || !rol.EsActivo)
                && (q.Length == 0 || rol.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase));
            alguno |= rol.Visible;
        }
        SinRoles = _cargado && !alguno;
    }

    private void NotificarConteosRoles()
    {
        OnPropertyChanged(nameof(TotalRoles));
        OnPropertyChanged(nameof(RolesActivos));
        OnPropertyChanged(nameof(RolesInactivos));
    }

    private static bool EsAccionCritica(string nombre) =>
        nombre.StartsWith("Eliminar", StringComparison.OrdinalIgnoreCase)
        || nombre.StartsWith("Cancelar", StringComparison.OrdinalIgnoreCase)
        || nombre.Equals("Modificar Configuración", StringComparison.Ordinal);

    private static string ResolverIcono(string nombre)
    {
        if (Contiene(nombre, "config")) return "gear";
        if (Contiene(nombre, "emplead")) return "users";
        if (Contiene(nombre, "inventario") || Contiene(nombre, "producto")) return "box";
        if (Contiene(nombre, "pesaje")) return "scale";
        if (Contiene(nombre, "proveedor") || Contiene(nombre, "fabricante")) return "truck";
        if (Contiene(nombre, "rol") || Contiene(nombre, "permiso")) return "shield";
        return "modulo";
        static bool Contiene(string texto, string parte) => texto.Contains(parte, StringComparison.OrdinalIgnoreCase);
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
        _cts.Cancel();
        _cts.Dispose();
        _ctsFiltro?.Dispose();
        Toast = null;
        SolicitarEdicionRol = null;
        ConfirmarCambioEstado = null;
        ConfirmarCambiosPendientes = null;
        Modulos.Clear();
        Roles.Clear();
        _guardadoPorRol.Clear();
    }
}
