using System.Collections.ObjectModel;
using CapaAplicacion.Conexion;
using CapaAplicacion.Notificaciones.Dtos;
using CapaAplicacion.Notificaciones.Interfaces;
using CapaAplicacion.Common.Interfaces;
using CapaUI.Navigation;
using CapaAplicacion.Realtime;
using CapaAplicacion.Usuarios.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Notificaciones;

public partial class NotificacionesViewModel : ObservableObject, IDisposable
{
    private readonly INotificacionRepository _repositorio;
    private readonly IRealtimeService _realtime;
    private readonly IConexionMonitor _conexion;
    private readonly IUsuarioSesionService _sesion;
    private IDisposable? _suscripcion;
    private CancellationTokenSource? _cts;
    private bool _inicializado;
    private bool _disposed;

    public ObservableCollection<NotificacionDto> Notificaciones { get; } = new();
    public IEnumerable<NotificacionDto> Recientes => Notificaciones.Take(5);
    public IReadOnlyList<string> Estados { get; } = ["todas", "no_leidas", "leidas", "archivadas"];
    public IReadOnlyList<string> Severidades { get; } = ["Todas", "informativa", "advertencia", "critica"];

    public INavegacionService? NavegacionService { get; set; }

    [ObservableProperty] private string _estadoSeleccionado = "todas";
    [ObservableProperty] private string _severidadSeleccionada = "Todas";
    [ObservableProperty] private bool _estaCargando;
    [ObservableProperty] private bool _servicioDisponible;
    [ObservableProperty] private string _mensajeEstado = "Cargando notificaciones…";
    [ObservableProperty] private long _noLeidas;
    [ObservableProperty] private bool _hayMas;

    public bool PuedeConsultar => _sesion.TienePermiso("NOTIFICACIONES_CONSULTAR");
    public bool TieneNoLeidas => NoLeidas > 0;
    public string TextoBadge => NoLeidas > 99 ? "99+" : NoLeidas.ToString();
    public string TextoNuevas => NoLeidas == 1 ? "1 nueva" : $"{NoLeidas} nuevas";

    public NotificacionesViewModel(INotificacionRepository repositorio, IRealtimeService realtime,
        IConexionMonitor conexion, IUsuarioSesionService sesion)
    {
        _repositorio = repositorio;
        _realtime = realtime;
        _conexion = conexion;
        _sesion = sesion;
        _conexion.EstadoCambiado += OnEstadoConexionCambiado;
        _conexion.Reconectado += OnReconectado;
    }

    partial void OnNoLeidasChanged(long value)
    {
        OnPropertyChanged(nameof(TieneNoLeidas));
        OnPropertyChanged(nameof(TextoBadge));
        OnPropertyChanged(nameof(TextoNuevas));
    }

    public async Task InicializarAsync()
    {
        if (_inicializado || !PuedeConsultar) return;
        _inicializado = true;
        await ReconectarYCargarAsync();
    }

    [RelayCommand]
    private async Task AplicarFiltrosAsync() => await CargarAsync(reemplazar: true);

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (!HayMas || Notificaciones.Count == 0) return;
        await CargarAsync(reemplazar: false);
    }

    [RelayCommand]
    private async Task MarcarLeidaAsync(NotificacionDto? item)
    {
        if (item is null || item.EstaLeida || !ServicioDisponible) return;
        var resultado = await _repositorio.MarcarLeidaAsync(item.IdNotificacion);
        if (resultado.Success) await RefrescarAsync(); else MensajeEstado = resultado.Error;
    }

    [RelayCommand]
    private async Task MarcarTodasLeidasAsync()
    {
        if (!ServicioDisponible) return;
        var resultado = await _repositorio.MarcarTodasLeidasAsync();
        if (resultado.Success) await RefrescarAsync(); else MensajeEstado = resultado.Error;
    }

    [RelayCommand(CanExecute = nameof(PuedeEjecutarAccion))]
    private void EjecutarAccion(NotificacionDto? item)
    {
        if (!PuedeEjecutarAccion(item) || NavegacionService is null) return;
        
        string? routeId = item!.TablaOrigen switch
        {
            "usuarios" => Routes.Usuarios,
            "roles" => Routes.Roles,
            "proveedores" => Routes.Proveedores,
            "fabricantes" => Routes.Fabricantes,
            "categorias" => Routes.Categorias,
            "productos" => Routes.Productos,
            "presentaciones" => Routes.Presentaciones,
            _ => null
        };
        
        if (routeId != null && !NavegacionService.TryNavigate(routeId, out var motivo))
        {
            MensajeEstado = motivo ?? "No fue posible abrir el módulo relacionado.";
        }
    }

    private static bool PuedeEjecutarAccion(NotificacionDto? item) =>
        item is { IdRegistroOrigen: > 0, TablaOrigen: "usuarios" or "roles" or "proveedores" or "fabricantes" or "categorias" or "productos" or "presentaciones" };

    [RelayCommand]
    private async Task ArchivarAsync(NotificacionDto? item)
    {
        if (item is null || !ServicioDisponible) return;
        var resultado = item.EstaArchivada
            ? await _repositorio.RestaurarAsync(item.IdNotificacion)
            : await _repositorio.ArchivarAsync(item.IdNotificacion);
        if (resultado.Success) await RefrescarAsync(); else MensajeEstado = resultado.Error;
    }

    private async Task ReconectarYCargarAsync()
    {
        if (_disposed || !PuedeConsultar) return;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _suscripcion?.Dispose();
        _suscripcion = null;

        if (_conexion.Estado is EstadoConexion.SinConexion or EstadoConexion.Degradado)
        {
            MostrarNoDisponible();
            return;
        }

        try
        {
            var idUsuario = _sesion.SesionActual?.IdUsuario
                ?? throw new InvalidOperationException("No existe una sesión activa.");
            _suscripcion = await _realtime.ObservarAsync(
                "notificaciones_usuario", $"id_usuario=eq.{idUsuario}", OnCambioRealtime, _cts.Token);
            await RefrescarAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ServicioDisponible = false;
            MensajeEstado = $"Servicio de notificaciones no disponible: {ex.Message}";
            Notificaciones.Clear();
            NoLeidas = 0;
        }
    }

    private async Task RefrescarAsync()
    {
        await CargarAsync(reemplazar: true);
        if (!ServicioDisponible) return;
        var contador = await _repositorio.ContarNoLeidasAsync(_cts?.Token ?? default);
        if (contador.Success) NoLeidas = contador.Value;
        else
        {
            ServicioDisponible = false;
            MensajeEstado = contador.Error;
        }
    }

    private async Task CargarAsync(bool reemplazar)
    {
        if (_disposed || EstaCargando) return;
        EstaCargando = true;
        try
        {
            DateTime? cursorFecha = null;
            long? cursorId = null;
            if (!reemplazar && Notificaciones.LastOrDefault() is { } ultimo)
            {
                cursorFecha = ultimo.FechaCreacion;
                cursorId = ultimo.IdNotificacion;
            }

            var resultado = await _repositorio.ListarAsync(new FiltroNotificacionesDto
            {
                EstadoBandeja = EstadoSeleccionado,
                Severidad = SeveridadSeleccionada == "Todas" ? null : SeveridadSeleccionada,
                Limite = 25,
                CursorFecha = cursorFecha,
                CursorId = cursorId,
            }, _cts?.Token ?? default);

            if (!resultado.Success)
            {
                ServicioDisponible = false;
                MensajeEstado = resultado.Error;
                if (reemplazar) Notificaciones.Clear();
                return;
            }

            var pagina = resultado.Value!;
            if (reemplazar) Notificaciones.Clear();
            foreach (var item in pagina.Where(x => Notificaciones.All(y => y.IdNotificacion != x.IdNotificacion)))
                Notificaciones.Add(item);
            HayMas = pagina.Count == 25;
            ServicioDisponible = true;
            MensajeEstado = Notificaciones.Count == 0 ? "No hay notificaciones para estos filtros." : string.Empty;
            OnPropertyChanged(nameof(Recientes));
        }
        finally { EstaCargando = false; }
    }

    private async void OnCambioRealtime(CambioRealtime cambio)
    {
        if (_disposed || cambio.Operacion is not ("Insert" or "Update" or "INSERT" or "UPDATE")) return;
        
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => 
            {
                await RefrescarAsync();
            });
        }
        else
        {
            await RefrescarAsync();
        }
    }

    private void OnEstadoConexionCambiado(object? sender, EstadoConexion estado)
    {
        if (estado is EstadoConexion.SinConexion or EstadoConexion.Degradado) MostrarNoDisponible();
    }

    private void OnReconectado(object? sender, EventArgs e) => System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await ReconectarYCargarAsync());

    private void MostrarNoDisponible()
    {
        ServicioDisponible = false;
        MensajeEstado = "Servicio de notificaciones no disponible. Se actualizará al recuperar la conexión.";
        Notificaciones.Clear();
        NoLeidas = 0;
        HayMas = false;
        OnPropertyChanged(nameof(Recientes));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _conexion.EstadoCambiado -= OnEstadoConexionCambiado;
        _conexion.Reconectado -= OnReconectado;
        _suscripcion?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
