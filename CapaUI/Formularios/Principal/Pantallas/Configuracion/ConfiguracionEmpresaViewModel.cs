using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapaAplicacion.Conexion;
using CapaAplicacion.Empresa.Dtos;
using CapaAplicacion.Empresa.Interfaces;
using CapaAplicacion.Realtime;
using CapaDominio.Reglas;
using CapaUI.Core.Empresa;
using CapaUI.Core.MVVM;
using CapaUI.Core.Validacion;
using CapaUI.Services.Empresa;

namespace CapaUI.Formularios.Principal.Pantallas.Configuracion;

public partial class ConfiguracionEmpresaViewModel : RealtimeAwareViewModel
{
    private readonly IEmpresaRepository _repo;
    private readonly LogoEmpresaCache _logoCache;
    private readonly IconoSidebarCache _iconoCache;
    private readonly EmpresaThemeService _theme;

    private int _idEmpresa;
    private string? _logoStorageActual;
    private string? _iconoStorageActual;
    private ChangeTracker<EmpresaSnapshot> _tracker = new(null);

    public sealed record EmpresaSnapshot(
        string NombreEmpresa,
        string RtnEmpresa,
        string DireccionEmpresa,
        string TelefonoEmpresa,
        string CorreoEmpresa,
        string DominioCorreo,
        string ColorEmpresa,
        string? RutaLogoSeleccionado,
        string? RutaIconoSidebarSeleccionado);

    [ObservableProperty] private string _nombreEmpresa = string.Empty;
    [ObservableProperty] private string? _rtnEmpresa;
    [ObservableProperty] private string? _direccionEmpresa;
    [ObservableProperty] private string? _telefonoEmpresa;
    [ObservableProperty] private string? _correoEmpresa;
    [ObservableProperty] private string? _dominioCorreo;
    [ObservableProperty] private string _colorEmpresa = EmpresaThemeService.ColorPredeterminado;

    [ObservableProperty] private string? _rutaLogoSeleccionado;
    [ObservableProperty] private ImageSource? _logoVistaPrevia;

    [ObservableProperty] private string? _rutaIconoSidebarSeleccionado;
    [ObservableProperty] private ImageSource? _iconoSidebarVistaPrevia;

    [ObservableProperty] private bool _cargando;
    [ObservableProperty] private bool _guardando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? _error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayExito))]
    private string? _mensajeExito;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayNotificacionRealtime))]
    private string? _mensajeNotificacionRealtime;

    public bool HayError => !string.IsNullOrWhiteSpace(Error);
    public bool HayExito => !string.IsNullOrWhiteSpace(MensajeExito);
    public bool HayNotificacionRealtime => !string.IsNullOrWhiteSpace(MensajeNotificacionRealtime);

    public bool TieneLogoVistaPrevia => LogoVistaPrevia != null;
    public bool TieneIconoSidebarVistaPrevia => IconoSidebarVistaPrevia != null;

    public bool TieneCambios => _idEmpresa > 0 && _tracker.HasInitialSnapshot && _tracker.IsDirty(CrearSnapshotActual());
    public bool PuedeGuardar => TieneCambios && !Guardando && !Cargando;

    public static event Action<EmpresaGuardadaDto>? EmpresaActualizada;

    public ConfiguracionEmpresaViewModel(
        IEmpresaRepository repo,
        LogoEmpresaCache logoCache,
        IconoSidebarCache iconoCache,
        EmpresaThemeService theme,
        IRealtimeService realtime,
        IConexionMonitor conexionMonitor)
        : base(realtime, conexionMonitor)
    {
        _repo = repo;
        _logoCache = logoCache;
        _iconoCache = iconoCache;
        _theme = theme;
    }

    /// <summary>
    /// Delega en <see cref="ReglasFormato.NormalizarColorHex"/> (CapaDominio, sin WPF)
    /// para que la regla real sea la misma que ejercitan los tests de caja blanca.
    /// </summary>
    public static string NormalizarHex(string? valor) => ReglasFormato.NormalizarColorHex(valor);

    public Brush ColorVistaPrevia
    {
        get
        {
            try
            {
                var hex = NormalizarHex(ColorEmpresa);
                var value = ColorConverter.ConvertFromString(hex);
                if (value is Color color)
                {
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
                return EmpresaThemeService.ObtenerBrushPrincipalActual();
            }
            catch
            {
                return EmpresaThemeService.ObtenerBrushPrincipalActual();
            }
        }
    }

    public EmpresaSnapshot CrearSnapshotActual() =>
        new(
            (NombreEmpresa ?? string.Empty).Trim(),
            (RtnEmpresa ?? string.Empty).Trim(),
            (DireccionEmpresa ?? string.Empty).Trim(),
            (TelefonoEmpresa ?? string.Empty).Trim(),
            (CorreoEmpresa ?? string.Empty).Trim(),
            (DominioCorreo ?? string.Empty).Trim(),
            NormalizarHex(ColorEmpresa),
            RutaLogoSeleccionado,
            RutaIconoSidebarSeleccionado);

    private void NotificarCambioEstado()
    {
        OnPropertyChanged(nameof(TieneCambios));
        OnPropertyChanged(nameof(PuedeGuardar));
        GuardarCommand.NotifyCanExecuteChanged();
    }

    partial void OnNombreEmpresaChanged(string value) => NotificarCambioEstado();
    partial void OnRtnEmpresaChanged(string? value) => NotificarCambioEstado();
    partial void OnDireccionEmpresaChanged(string? value) => NotificarCambioEstado();
    partial void OnTelefonoEmpresaChanged(string? value) => NotificarCambioEstado();
    partial void OnCorreoEmpresaChanged(string? value) => NotificarCambioEstado();
    partial void OnDominioCorreoChanged(string? value) => NotificarCambioEstado();

    partial void OnColorEmpresaChanged(string value)
    {
        OnPropertyChanged(nameof(ColorVistaPrevia));
        NotificarCambioEstado();
    }

    partial void OnRutaLogoSeleccionadoChanged(string? value) => NotificarCambioEstado();
    partial void OnRutaIconoSidebarSeleccionadoChanged(string? value) => NotificarCambioEstado();

    partial void OnLogoVistaPreviaChanged(ImageSource? value) =>
        OnPropertyChanged(nameof(TieneLogoVistaPrevia));

    partial void OnIconoSidebarVistaPreviaChanged(ImageSource? value) =>
        OnPropertyChanged(nameof(TieneIconoSidebarVistaPrevia));

    partial void OnCargandoChanged(bool value) => NotificarCambioEstado();
    partial void OnGuardandoChanged(bool value) => NotificarCambioEstado();

    /// <summary>
    /// Carga las imágenes a 0 ms desde la caché local sin tocar la red,
    /// permitiendo que la pantalla inicialice de inmediato con los recursos ya persistidos.
    /// </summary>
    public void CargarCacheLocalSinRed()
    {
        try
        {
            if (LogoVistaPrevia == null)
            {
                var rutaLogo = _logoCache.ObtenerRutaCacheadaSinRed();
                if (!string.IsNullOrWhiteSpace(rutaLogo))
                    LogoVistaPrevia = CargarBitmapCongelado(rutaLogo);
            }

            if (IconoSidebarVistaPrevia == null)
            {
                var rutaIcono = _iconoCache.ObtenerRutaCacheadaSinRed();
                if (!string.IsNullOrWhiteSpace(rutaIcono))
                    IconoSidebarVistaPrevia = CargarBitmapCongelado(rutaIcono);
            }

            if (string.IsNullOrWhiteSpace(ColorEmpresa) ||
                ColorEmpresa.Equals(EmpresaThemeService.ColorPredeterminado, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(_theme.ColorActual))
                    ColorEmpresa = _theme.ColorActual;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "ConfiguracionEmpresaVM: error al precargar caché local sin red");
        }
    }

    public async Task InicializarAsync()
    {
        // 1. Carga inmediata a 0 ms desde caché local de disco
        CargarCacheLocalSinRed();

        if (Cargando || _idEmpresa > 0) return;

        Cargando = true;
        Error = null;
        MensajeExito = null;

        try
        {
            var resultado = await _repo.ObtenerAsync();
            if (!resultado.Success)
            {
                Error = resultado.Error;
                return;
            }

            var empresa = resultado.Value;
            if (empresa is null)
            {
                Error = "No existe una empresa configurada.";
                return;
            }

            _idEmpresa = empresa.IdEmpresa;
            _logoStorageActual = empresa.LogoEmpresa;
            _iconoStorageActual = empresa.IconoSidebar;
            NombreEmpresa = empresa.NombreEmpresa ?? string.Empty;
            RtnEmpresa = empresa.RtnEmpresa;
            DireccionEmpresa = empresa.DireccionEmpresa;
            TelefonoEmpresa = empresa.TelefonoEmpresa;
            CorreoEmpresa = empresa.CorreoEmpresa;
            DominioCorreo = empresa.DominioCorreo;
            ColorEmpresa = string.IsNullOrWhiteSpace(empresa.ColorEmpresa) ||
                           empresa.ColorEmpresa.Equals("sin_color", StringComparison.OrdinalIgnoreCase)
                ? EmpresaThemeService.ColorPredeterminado
                : empresa.ColorEmpresa;

            // 2. Suscripción a cambios Realtime de la tabla 'empresa' — recién ahora, con
            // todos los campos que OnCambioEmpresaRealtime/ValoresCoincidenConActual leen ya
            // poblados. Suscribir antes (como al principio del método) deja una ventana donde
            // un evento WAL llegaría contra NombreEmpresa/etc. todavía vacíos y dispararía un
            // falso aviso de "cambios desde otra terminal" apenas se abre la pantalla.
            Observar("empresa", OnCambioEmpresaRealtime);

            // 3. Verificación silenciosa en background contra Supabase:
            // Si el archivo en Storage coincide con la caché local, No-Op total (cero parpadeo, cero descarga)
            await SincronizarLogoAsync(empresa.LogoEmpresa);
            await SincronizarIconoSidebarAsync(empresa.IconoSidebar);

            // Snapshot inicial para ChangeTracker<EmpresaSnapshot>
            _tracker = new ChangeTracker<EmpresaSnapshot>(CrearSnapshotActual());
            NotificarCambioEstado();
        }
        finally
        {
            Cargando = false;
        }
    }

    private async Task SincronizarLogoAsync(string? rutaStorage)
    {
        if (string.IsNullOrWhiteSpace(rutaStorage))
        {
            LogoVistaPrevia = null;
            return;
        }

        var nombreStorage = Path.GetFileName(rutaStorage);
        var cachedPath = _logoCache.ObtenerRutaCacheadaSinRed();
        var nombreCached = !string.IsNullOrWhiteSpace(cachedPath) ? Path.GetFileName(cachedPath) : null;

        if (string.Equals(nombreStorage, nombreCached, StringComparison.OrdinalIgnoreCase))
        {
            if (LogoVistaPrevia == null && cachedPath != null)
                LogoVistaPrevia = CargarBitmapCongelado(cachedPath);
            return; // No-op
        }

        var rutaDescargada = await _logoCache.ObtenerRutaLocalAsync(rutaStorage);
        if (!string.IsNullOrWhiteSpace(rutaDescargada))
            LogoVistaPrevia = CargarBitmapCongelado(rutaDescargada);
    }

    private async Task SincronizarIconoSidebarAsync(string? rutaStorage)
    {
        if (string.IsNullOrWhiteSpace(rutaStorage))
        {
            IconoSidebarVistaPrevia = null;
            return;
        }

        var nombreStorage = Path.GetFileName(rutaStorage);
        var cachedPath = _iconoCache.ObtenerRutaCacheadaSinRed();
        var nombreCached = !string.IsNullOrWhiteSpace(cachedPath) ? Path.GetFileName(cachedPath) : null;

        if (string.Equals(nombreStorage, nombreCached, StringComparison.OrdinalIgnoreCase))
        {
            if (IconoSidebarVistaPrevia == null && cachedPath != null)
                IconoSidebarVistaPrevia = CargarBitmapCongelado(cachedPath);
            return; // No-op
        }

        var rutaDescargada = await _iconoCache.ObtenerRutaLocalAsync(rutaStorage);
        if (!string.IsNullOrWhiteSpace(rutaDescargada))
            IconoSidebarVistaPrevia = CargarBitmapCongelado(rutaDescargada);
    }

    public void SeleccionarLogo(string ruta)
    {
        if (!ValidarImagenSeleccionada(ruta)) return;

        RutaLogoSeleccionado = ruta;
        LogoVistaPrevia = CargarBitmapCongelado(ruta);
        NotificarCambioEstado();
    }

    public void SeleccionarIconoSidebar(string ruta)
    {
        if (!ValidarImagenSeleccionada(ruta)) return;

        RutaIconoSidebarSeleccionado = ruta;
        IconoSidebarVistaPrevia = CargarBitmapCongelado(ruta);
        NotificarCambioEstado();
    }

    /// <summary>
    /// Carga una imagen en memoria con BitmapCacheOption.OnLoad y llama a .Freeze()
    /// mediante MemoryStream para liberar inmediatamente el descriptor de archivo en disco.
    /// Delega a <see cref="CapaUI.Core.Helpers.BitmapHelper.CargarBitmapCongelado"/>.
    /// </summary>
    public static BitmapImage? CargarBitmapCongelado(string? rutaLocal)
        => CapaUI.Core.Helpers.BitmapHelper.CargarBitmapCongelado(rutaLocal);

    private bool ValidarImagenSeleccionada(string ruta)
    {
        try
        {
            var info = new FileInfo(ruta);
            if (!info.Exists || info.Length <= 0 || info.Length > 2 * 1024 * 1024)
                throw new InvalidOperationException("La imagen debe pesar entre 1 byte y 2 MB.");

            if (info.Extension.ToLowerInvariant() is not ".png" and not ".jpg" and not ".jpeg")
                throw new InvalidOperationException("La imagen debe ser PNG, JPG o JPEG.");

            using var stream = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            _ = decoder.Frames.FirstOrDefault()
                ?? throw new InvalidOperationException("El archivo no contiene una imagen válida.");

            Error = null;
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private void ElegirColor(string color)
    {
        ColorEmpresa = color;
        MensajeExito = null;
    }

    [RelayCommand]
    private void DescartarNotificacionRealtime()
    {
        MensajeNotificacionRealtime = null;
    }

    private DateTime _momentoUltimoGuardadoLocal = DateTime.MinValue;

    public void OnCambioEmpresaRealtime(CambioRealtime cambio)
    {
        if (Disposed || Guardando) return;

        // Si se acaba de guardar localmente en esta terminal (menos de 3 segundos),
        // o los valores recibidos coinciden exactamente con la configuración actual,
        // no es un cambio externo de otra terminal (evita falsas alertas al guardar).
        if ((DateTime.UtcNow - _momentoUltimoGuardadoLocal).TotalSeconds < 3.0) return;

        if (cambio.Valores is not null && ValoresCoincidenConActual(cambio.Valores))
            return;

        MensajeNotificacionRealtime =
            "Se detectaron cambios en la configuración del sistema desde otra terminal. Los cambios se aplicarán al reiniciar la aplicación.";
    }

    /// <summary>
    /// La comparación por campo (texto plano vs. color) vive en
    /// <see cref="ReglasFormato.ValoresRealtimeCoinciden"/> / <see cref="ReglasFormato.ColoresRealtimeCoinciden"/>
    /// (CapaDominio, sin WPF) para que los tests de caja blanca ejerciten la regla real
    /// en vez de una copia local.
    /// </summary>
    public bool ValoresCoincidenConActual(IReadOnlyDictionary<string, string?> valores)
    {
        bool Coincide(string clave, string? valorActual)
        {
            if (!valores.TryGetValue(clave, out var valorRecibido)) return true;
            return ReglasFormato.ValoresRealtimeCoinciden(valorRecibido, valorActual);
        }

        bool CoincideColor(string clave, string? valorActual)
        {
            if (!valores.TryGetValue(clave, out var valorRecibido)) return true;
            return ReglasFormato.ColoresRealtimeCoinciden(valorRecibido, valorActual);
        }

        return Coincide("nombre_empresa", NombreEmpresa)
            && Coincide("rtn_empresa", RtnEmpresa)
            && Coincide("direccion_empresa", DireccionEmpresa)
            && Coincide("telefono_empresa", TelefonoEmpresa)
            && Coincide("correo_empresa", CorreoEmpresa)
            && Coincide("dominio_correo", DominioCorreo)
            && CoincideColor("color_empresa", ColorEmpresa)
            && Coincide("logo_empresa", _logoStorageActual)
            && Coincide("icono_sidebar", _iconoStorageActual);
    }

    private bool DatosValidos()
    {
        if (!ReglasFormato.TieneContenido(NombreEmpresa))
        {
            Error = "El nombre de la empresa es obligatorio.";
            return false;
        }

        if (!ReglasFormato.NoExcedeLargo(NombreEmpresa, ReglasEmpresa.Nombre.LargoMaximo ?? 200))
        {
            Error = $"El nombre de la empresa no puede superar los {ReglasEmpresa.Nombre.LargoMaximo ?? 200} caracteres.";
            return false;
        }

        if (!ReglasFormato.EsRtn(RtnEmpresa))
        {
            Error = "El RTN debe tener 14 dígitos.";
            return false;
        }

        if (!ReglasFormato.NoExcedeLargo(RtnEmpresa, ReglasEmpresa.Rtn.LargoMaximo ?? 20))
        {
            Error = "El RTN no puede superar los 20 caracteres.";
            return false;
        }

        if (!ReglasFormato.NoExcedeLargo(DireccionEmpresa, ReglasEmpresa.Direccion.LargoMaximo ?? 500))
        {
            Error = $"La dirección no puede superar los {ReglasEmpresa.Direccion.LargoMaximo ?? 500} caracteres.";
            return false;
        }

        if (!ReglasFormato.EsTelefono(TelefonoEmpresa))
        {
            Error = "El teléfono debe tener entre 8 y 15 dígitos.";
            return false;
        }

        if (!ReglasFormato.NoExcedeLargo(TelefonoEmpresa, ReglasEmpresa.Telefono.LargoMaximo ?? 20))
        {
            Error = "El teléfono no puede superar los 20 caracteres.";
            return false;
        }

        if (!ReglasFormato.EsCorreo(CorreoEmpresa))
        {
            Error = "El correo no tiene un formato válido.";
            return false;
        }

        if (!ReglasFormato.NoExcedeLargo(CorreoEmpresa, ReglasEmpresa.Correo.LargoMaximo ?? 100))
        {
            Error = "El correo no puede superar los 100 caracteres.";
            return false;
        }

        if (!ReglasFormato.NoExcedeLargo(DominioCorreo, 100))
        {
            Error = "El dominio de correo no puede superar los 100 caracteres.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ColorEmpresa) && !EmpresaThemeService.EsColorValido(ColorEmpresa))
        {
            Error = "El color debe tener formato hexadecimal válido (#RRGGBB).";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ColorEmpresa))
        {
            var hexNormalizado = NormalizarHex(ColorEmpresa);
            if (hexNormalizado != ColorEmpresa)
                ColorEmpresa = hexNormalizado;
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (Guardando || !PuedeGuardar || _idEmpresa <= 0) return;

        Error = null;
        MensajeExito = null;
        if (!DatosValidos()) return;

        Guardando = true;
        try
        {
            var resultado = await _repo.GuardarAsync(
                new ActualizarEmpresaDto
                {
                    IdEmpresa = _idEmpresa,
                    NombreEmpresa = NombreEmpresa,
                    RtnEmpresa = RtnEmpresa,
                    DireccionEmpresa = DireccionEmpresa,
                    TelefonoEmpresa = TelefonoEmpresa,
                    CorreoEmpresa = CorreoEmpresa,
                    DominioCorreo = DominioCorreo,
                    ColorEmpresa = ColorEmpresa,
                },
                RutaLogoSeleccionado,
                RutaIconoSidebarSeleccionado);

            if (!resultado.Success || resultado.Value is null)
            {
                Error = resultado.Error;
                return;
            }

            var guardada = resultado.Value;
            _logoStorageActual = guardada.Empresa.LogoEmpresa;
            _iconoStorageActual = guardada.Empresa.IconoSidebar;

            if (!string.IsNullOrWhiteSpace(RutaLogoSeleccionado) &&
                !string.IsNullOrWhiteSpace(_logoStorageActual) &&
                File.Exists(RutaLogoSeleccionado))
            {
                var rutaLocalCacheada = _logoCache.ActualizarDesdeArchivoLocal(
                    _logoStorageActual,
                    RutaLogoSeleccionado);
                LogoVistaPrevia = CargarBitmapCongelado(rutaLocalCacheada);
                RutaLogoSeleccionado = null;
            }

            if (!string.IsNullOrWhiteSpace(RutaIconoSidebarSeleccionado) &&
                !string.IsNullOrWhiteSpace(_iconoStorageActual) &&
                File.Exists(RutaIconoSidebarSeleccionado))
            {
                var rutaLocalCacheada = _iconoCache.ActualizarDesdeArchivoLocal(
                    _iconoStorageActual,
                    RutaIconoSidebarSeleccionado);
                IconoSidebarVistaPrevia = CargarBitmapCongelado(rutaLocalCacheada);
                RutaIconoSidebarSeleccionado = null;
            }

            _theme.Aplicar(guardada.Empresa.ColorEmpresa);

            // Registrar momento de guardado local para descartar ecos de Supabase Realtime propios
            _momentoUltimoGuardadoLocal = DateTime.UtcNow;

            // Reiniciar tracker con el estado guardado exitosamente
            _tracker = new ChangeTracker<EmpresaSnapshot>(CrearSnapshotActual());
            NotificarCambioEstado();

            MensajeExito = guardada.Advertencias.Count > 0
                ? $"Configuración guardada. Nota: {string.Join(" ", guardada.Advertencias)}"
                : "Configuración de la empresa guardada exitosamente.";
            EmpresaActualizada?.Invoke(guardada);
        }
        finally
        {
            Guardando = false;
        }
    }
}
