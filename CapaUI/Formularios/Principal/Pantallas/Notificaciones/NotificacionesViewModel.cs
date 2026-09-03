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

public sealed record OpcionFiltroNotificacion(string Valor, string Etiqueta);

public partial class NotificacionesViewModel : ObservableObject, IDisposable
{
    private static readonly OpcionFiltroNotificacion EstadoBandeja = new("todas", "Bandeja");
    private static readonly OpcionFiltroNotificacion SeveridadTodas = new(string.Empty, "Todas");
    private readonly INotificacionRepository _repositorio;
    private readonly IRealtimeService _realtime;
    private readonly IConexionMonitor _conexion;
    private readonly IUsuarioSesionService _sesion;
    private IDisposable? _suscripcion;
    private CancellationTokenSource? _cts;
    private bool _inicializado;
    private bool _disposed;

    public ObservableCollection<NotificacionDto> Notificaciones { get; } = new();
    public ObservableCollection<NotificacionDto> Recientes { get; } = new();
    public IReadOnlyList<OpcionFiltroNotificacion> Estados { get; } =
    [
        EstadoBandeja,
        new("no_leidas", "No leídas"),
        new("leidas", "Leídas"),
        new("archivadas", "Archivadas"),
    ];
    public IReadOnlyList<OpcionFiltroNotificacion> Severidades { get; } =
    [
        SeveridadTodas,
        new("informativa", "Informativa"),
        new("advertencia", "Advertencia"),
        new("critica", "Crítica"),
    ];

    public INotificacionNavigationService? NavegacionService { get; set; }
    public event Action<NotificacionDto>? SolicitarDetalle;

    [ObservableProperty] private OpcionFiltroNotificacion _estadoSeleccionado = EstadoBandeja;
    [ObservableProperty] private OpcionFiltroNotificacion _severidadSeleccionada = SeveridadTodas;
    [ObservableProperty] private bool _estaCargando;
    [ObservableProperty] private bool _servicioDisponible;
    [ObservableProperty] private string _mensajeEstado = "Cargando notificaciones…";
    [ObservableProperty] private long _noLeidas;
    [ObservableProperty] private bool _hayMas;

    public bool PuedeConsultar => _sesion.TienePermiso("NOTIFICACIONES_CONSULTAR");
    public bool TieneNoLeidas => NoLeidas > 0;
    public string TextoBadge => NoLeidas > 99 ? "99+" : NoLeidas.ToString();
    public string TextoNuevas => NoLeidas == 1 ? "1 nueva" : $"{NoLeidas} nuevas";
    public string MensajeRecientes => !ServicioDisponible
        ? MensajeEstado
        : Recientes.Count == 0 ? "No hay notificaciones en Bandeja." : string.Empty;

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

    partial void OnServicioDisponibleChanged(bool value) => OnPropertyChanged(nameof(MensajeRecientes));
    partial void OnMensajeEstadoChanged(string value) => OnPropertyChanged(nameof(MensajeRecientes));

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
    private async Task AbrirDetalleAsync(NotificacionDto? item)
    {
        if (item is null) return;
        if (!item.EstaLeida && ServicioDisponible)
        {
            var resultado = await _repositorio.MarcarLeidaAsync(item.IdNotificacion);
            if (!resultado.Success) { MensajeEstado = resultado.Error; return; }
            await RefrescarAsync();
            item = Recientes.FirstOrDefault(x => x.IdNotificacion == item.IdNotificacion)
                ?? Notificaciones.FirstOrDefault(x => x.IdNotificacion == item.IdNotificacion)
                ?? item;
        }
        SolicitarDetalle?.Invoke(item);
    }

    [RelayCommand]
    private async Task MarcarTodasLeidasYArchivarAsync()
    {
        if (!ServicioDisponible) return;
        var resultado = await _repositorio.MarcarTodasLeidasYArchivarAsync();
        if (!resultado.Success) { MensajeEstado = resultado.Error; return; }

        EstadoSeleccionado = EstadoBandeja;
        await RefrescarAsync();
    }

    [RelayCommand(CanExecute = nameof(PuedeEjecutarAccion))]
    private void EjecutarAccion(NotificacionDto? item)
    {
        if (!PuedeEjecutarAccion(item) || NavegacionService is null) return;
        if (!NavegacionService.TryNavegar(item!.TablaOrigen!, item.IdRegistroOrigen!.Value, out var motivo))
            MensajeEstado = motivo ?? "No fue posible abrir el registro relacionado.";
    }

    private bool PuedeEjecutarAccion(NotificacionDto? item) => NavegacionService?.PuedeNavegar(item?.TablaOrigen, item?.IdRegistroOrigen) == true;

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
            Recientes.Clear();
            OnPropertyChanged(nameof(MensajeRecientes));
            NoLeidas = 0;
        }
    }

    private async Task RefrescarAsync()
    {
        await CargarAsync(reemplazar: true);
        if (!ServicioDisponible) return;
        await CargarRecientesAsync();
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
                EstadoBandeja = EstadoSeleccionado.Valor,
                Severidad = string.IsNullOrEmpty(SeveridadSeleccionada.Valor) ? null : SeveridadSeleccionada.Valor,
                Limite = 25,
                CursorFecha = cursorFecha,
                CursorId = cursorId,
            }, _cts?.Token ?? default);

            if (!resultado.Success)
            {
                ServicioDisponible = false;
                MensajeEstado = resultado.Error;
                if (reemplazar)
                {
                    Notificaciones.Clear();
                    Recientes.Clear();
                    OnPropertyChanged(nameof(MensajeRecientes));
                }
                return;
            }

            var pagina = resultado.Value!;
            if (reemplazar) Notificaciones.Clear();
            foreach (var item in pagina.Where(x => Notificaciones.All(y => y.IdNotificacion != x.IdNotificacion)))
                Notificaciones.Add(item);
            HayMas = pagina.Count == 25;
            ServicioDisponible = true;
            MensajeEstado = Notificaciones.Count == 0 ? "No hay notificaciones para estos filtros." : string.Empty;
        }
        finally { EstaCargando = false; }
    }

    private async Task CargarRecientesAsync()
    {
        var resultado = await _repositorio.ListarAsync(new FiltroNotificacionesDto
        {
            EstadoBandeja = EstadoBandeja.Valor,
            Limite = 5,
        }, _cts?.Token ?? default);

        if (!resultado.Success)
        {
            ServicioDisponible = false;
            MensajeEstado = resultado.Error;
            Recientes.Clear();
            OnPropertyChanged(nameof(MensajeRecientes));
            return;
        }

        Recientes.Clear();
        foreach (var item in resultado.Value!) Recientes.Add(item);
        OnPropertyChanged(nameof(MensajeRecientes));
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
        Recientes.Clear();
        OnPropertyChanged(nameof(MensajeRecientes));
        NoLeidas = 0;
        HayMas = false;
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
