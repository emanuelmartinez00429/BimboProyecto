using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapaAplicacion.Empresa.Dtos;
using CapaAplicacion.Empresa.Interfaces;
using CapaUI.Core.Empresa;
using CapaUI.Core.Validacion;
using CapaUI.Services.Empresa;

namespace CapaUI.Formularios.Principal.Pantallas.Configuracion;

public partial class ConfiguracionEmpresaViewModel : ObservableObject
{
    private readonly IEmpresaRepository _repo;
    private readonly LogoEmpresaCache _logoCache;
    private readonly IconoSidebarCache _iconoCache;
    private readonly EmpresaThemeService _theme;

    private int _idEmpresa;
    private string? _logoStorageActual;
    private string? _iconoStorageActual;

    [ObservableProperty] private string _nombreEmpresa = string.Empty;
    [ObservableProperty] private string? _rtnEmpresa;
    [ObservableProperty] private string? _direccionEmpresa;
    [ObservableProperty] private string? _telefonoEmpresa;
    [ObservableProperty] private string? _correoEmpresa;
    [ObservableProperty] private string? _dominioCorreo;
    [ObservableProperty] private string _colorEmpresa = EmpresaThemeService.ColorPredeterminado;
    [ObservableProperty] private string? _rutaLogoSeleccionado;
    [ObservableProperty] private string? _rutaLogoVistaPrevia;
    [ObservableProperty] private string? _rutaIconoSidebarSeleccionado;
    [ObservableProperty] private string? _rutaIconoSidebarVistaPrevia;
    [ObservableProperty] private bool _cargando;
    [ObservableProperty] private bool _guardando;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? _error;

    public bool HayError => !string.IsNullOrWhiteSpace(Error);

    public event Action? SolicitarCierre;
    public event Action<EmpresaGuardadaDto>? Guardado;

    public ConfiguracionEmpresaViewModel(
        IEmpresaRepository repo,
        LogoEmpresaCache logoCache,
        IconoSidebarCache iconoCache,
        EmpresaThemeService theme)
    {
        _repo = repo;
        _logoCache = logoCache;
        _iconoCache = iconoCache;
        _theme = theme;
    }

    public Brush ColorVistaPrevia
    {
        get
        {
            try
            {
                var value = ColorConverter.ConvertFromString(ColorEmpresa);
                return value is Color color
                    ? new SolidColorBrush(color)
                    : EmpresaThemeService.ObtenerBrushPrincipalActual();
            }
            catch
            {
                return EmpresaThemeService.ObtenerBrushPrincipalActual();
            }
        }
    }

    partial void OnColorEmpresaChanged(string value) =>
        OnPropertyChanged(nameof(ColorVistaPrevia));

    public async Task InicializarAsync()
    {
        if (Cargando || _idEmpresa > 0) return;

        Cargando = true;
        Error = null;
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
            NombreEmpresa = empresa.NombreEmpresa;
            RtnEmpresa = empresa.RtnEmpresa;
            DireccionEmpresa = empresa.DireccionEmpresa;
            TelefonoEmpresa = empresa.TelefonoEmpresa;
            CorreoEmpresa = empresa.CorreoEmpresa;
            DominioCorreo = empresa.DominioCorreo;
            ColorEmpresa = string.IsNullOrWhiteSpace(empresa.ColorEmpresa) ||
                           empresa.ColorEmpresa.Equals("sin_color", StringComparison.OrdinalIgnoreCase)
                ? EmpresaThemeService.ColorPredeterminado
                : empresa.ColorEmpresa;

            RutaLogoVistaPrevia = await _logoCache.ObtenerRutaLocalAsync(empresa.LogoEmpresa);
            RutaIconoSidebarVistaPrevia = await _iconoCache.ObtenerRutaLocalAsync(empresa.IconoSidebar);
        }
        finally
        {
            Cargando = false;
        }
    }

    public void SeleccionarLogo(string ruta)
    {
        if (!ValidarImagenSeleccionada(ruta)) return;

        RutaLogoSeleccionado = ruta;
        RutaLogoVistaPrevia  = ruta;
    }

    public void SeleccionarIconoSidebar(string ruta)
    {
        if (!ValidarImagenSeleccionada(ruta)) return;

        RutaIconoSidebarSeleccionado = ruta;
        RutaIconoSidebarVistaPrevia  = ruta;
    }

    /// <summary>
    /// Valida que el archivo elegido sea una imagen usable: existe, pesa algo pero
    /// no más de 2 MB, tiene extensión soportada y se puede decodificar de verdad.
    /// </summary>
    /// <remarks>
    /// Antes había dos copias de esto —una para el logo y otra para el ícono— con
    /// los mismos controles y mensajes distintos ("El logo debe pesar…" contra "La
    /// imagen debe pesar…"). Quedó una sola: el mensaje dice "La imagen" porque
    /// ahora sirve para los dos.
    ///
    /// El chequeo de decodificación es el que importa: la extensión se puede
    /// renombrar, así que un .txt renombrado a .png pasaría todo lo demás y recién
    /// fallaría al mostrarlo.
    /// </remarks>
    private bool ValidarImagenSeleccionada(string ruta)
    {
        try
        {
            var info = new FileInfo(ruta);
            if (!info.Exists || info.Length <= 0 || info.Length > 2 * 1024 * 1024)
                throw new InvalidOperationException("La imagen debe pesar entre 1 byte y 2 MB.");

            if (info.Extension.ToLowerInvariant() is not ".png" and not ".jpg" and not ".jpeg")
                throw new InvalidOperationException("La imagen debe ser PNG, JPG o JPEG.");

            using var stream = File.OpenRead(ruta);
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
    private void ElegirColor(string color) => ColorEmpresa = color;

    /// <summary>
    /// Valida los datos de la empresa antes de guardarlos.
    /// </summary>
    /// <remarks>
    /// Este ViewModel no tiene controles que pasarle al validador de formularios
    /// —es MVVM, los datos llegan por binding— así que consume directamente
    /// <see cref="ReglasCampo"/>, la capa de reglas puras. Ese es justamente el
    /// motivo por el que las reglas están separadas de la UI: acá sirven igual.
    /// El error se reporta por la propiedad <c>Error</c> que la vista ya muestra.
    /// </remarks>
    private bool DatosValidos()
    {
        if (!ReglasCampo.TieneContenido(NombreEmpresa))
        {
            Error = "El nombre de la empresa es obligatorio.";
            return false;
        }

        if (!ReglasCampo.EsRtn(RtnEmpresa))
        {
            Error = "El RTN debe tener 14 dígitos.";
            return false;
        }

        if (!ReglasCampo.EsTelefono(TelefonoEmpresa))
        {
            Error = "El teléfono debe tener entre 8 y 15 dígitos.";
            return false;
        }

        if (!ReglasCampo.EsCorreo(CorreoEmpresa))
        {
            Error = "El correo no tiene un formato válido.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (Guardando || _idEmpresa <= 0) return;

        Error = null;
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
                RutaLogoVistaPrevia = _logoCache.ActualizarDesdeArchivoLocal(
                    _logoStorageActual,
                    RutaLogoSeleccionado);
            }

            if (!string.IsNullOrWhiteSpace(RutaIconoSidebarSeleccionado) &&
                !string.IsNullOrWhiteSpace(_iconoStorageActual) &&
                File.Exists(RutaIconoSidebarSeleccionado))
            {
                RutaIconoSidebarVistaPrevia = _iconoCache.ActualizarDesdeArchivoLocal(
                    _iconoStorageActual,
                    RutaIconoSidebarSeleccionado);
            }

            _theme.Aplicar(guardada.Empresa.ColorEmpresa);
            Guardado?.Invoke(guardada);
        }
        finally
        {
            Guardando = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => SolicitarCierre?.Invoke();
}
