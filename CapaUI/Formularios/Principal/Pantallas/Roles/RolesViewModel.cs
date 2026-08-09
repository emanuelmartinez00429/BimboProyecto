using System.Collections.ObjectModel;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Roles;

public partial class AccionRolItemViewModel : ObservableObject
{
    public int IdAccion { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _asignada;
}

public sealed class ModuloRolItemViewModel
{
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public ObservableCollection<AccionRolItemViewModel> Acciones { get; init; } = new();
}

public partial class RolesViewModel : ObservableObject
{
    private readonly IRolRepository _rolesRepo;
    private readonly IRolPermisoRepository _permisosRepo;
    private readonly IUsuarioSesionService _sesion;
    private IReadOnlyList<ModuloAccionesDto> _catalogo = Array.Empty<ModuloAccionesDto>();

    [ObservableProperty]
    private ObservableCollection<RolDto> _roles = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private RolDto? _rolSeleccionado;

    [ObservableProperty]
    private ObservableCollection<ModuloRolItemViewModel> _modulos = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private string _mensaje = string.Empty;

    [ObservableProperty]
    private bool _esError;

    public bool PuedeEditar => _sesion.TienePermiso("Modificar Configuración");

    public RolesViewModel(
        IRolRepository rolesRepo,
        IRolPermisoRepository permisosRepo,
        IUsuarioSesionService sesion)
    {
        _rolesRepo = rolesRepo;
        _permisosRepo = permisosRepo;
        _sesion = sesion;
    }

    public async Task CargarAsync()
    {
        IsLoading = true;
        Mensaje = string.Empty;

        var rolesTask = _rolesRepo.ObtenerTodosAsync();
        var catalogoTask = _permisosRepo.ObtenerCatalogoAsync();
        await Task.WhenAll(rolesTask, catalogoTask);

        var rolesResult = await rolesTask;
        var catalogoResult = await catalogoTask;

        if (!rolesResult.Success || !catalogoResult.Success)
        {
            MostrarError(!rolesResult.Success ? rolesResult.Error : catalogoResult.Error);
            IsLoading = false;
            return;
        }

        Roles = new ObservableCollection<RolDto>(rolesResult.Value!);
        _catalogo = catalogoResult.Value!;
        RolSeleccionado = Roles.FirstOrDefault();
        IsLoading = false;
    }

    partial void OnRolSeleccionadoChanged(RolDto? value)
    {
        if (value is not null)
            _ = CargarAsignacionesAsync(value.IdRol);
        else
            Modulos.Clear();
    }

    private async Task CargarAsignacionesAsync(int idRol)
    {
        IsLoading = true;
        Mensaje = string.Empty;

        var resultado = await _permisosRepo.ObtenerAccionesAsignadasAsync(idRol);
        if (!resultado.Success)
        {
            MostrarError(resultado.Error);
            IsLoading = false;
            return;
        }

        var asignadas = resultado.Value!;
        Modulos = new ObservableCollection<ModuloRolItemViewModel>(
            _catalogo.Select(modulo => new ModuloRolItemViewModel
            {
                Nombre = modulo.NombreModulo,
                Descripcion = modulo.DescripcionModulo ?? string.Empty,
                Acciones = new ObservableCollection<AccionRolItemViewModel>(
                    modulo.Acciones.Select(accion => new AccionRolItemViewModel
                    {
                        IdAccion = accion.IdAccion,
                        Nombre = accion.NombreAccion,
                        Descripcion = accion.DescripcionAccion ?? string.Empty,
                        Asignada = asignadas.Contains(accion.IdAccion),
                    })),
            }));

        IsLoading = false;
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (RolSeleccionado is null)
            return;

        IsLoading = true;
        Mensaje = string.Empty;

        var ids = Modulos
            .SelectMany(m => m.Acciones)
            .Where(a => a.Asignada)
            .Select(a => a.IdAccion)
            .ToList();

        var resultado = await _permisosRepo.GuardarAsignacionesAsync(
            RolSeleccionado.IdRol,
            ids);

        if (!resultado.Success)
            MostrarError(resultado.Error);
        else
        {
            EsError = false;
            Mensaje = "Permisos guardados. Los usuarios afectados deben volver a iniciar sesión.";
        }

        IsLoading = false;
    }

    private bool PuedeGuardar() => PuedeEditar && RolSeleccionado is not null && !IsLoading;

    private void MostrarError(string mensaje)
    {
        EsError = true;
        Mensaje = mensaje;
    }
}
