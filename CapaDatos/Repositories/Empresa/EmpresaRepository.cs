using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Empresa.Dtos;
using CapaAplicacion.Empresa.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados;
using ServicioConexión.Conexion;
using StorageFileOptions = Supabase.Storage.FileOptions;
using StorageSearchOptions = Supabase.Storage.SearchOptions;

namespace CapaDatos.Repositories.Empresa;

public sealed class EmpresaRepository : RepositorioBase, IEmpresaRepository
{
    private const string BucketLogos = "empresa-logos";
    private const string PermisoAdministrar = "Modificar Configuración";
    private const long MaxLogoBytes = 2 * 1024 * 1024;

    private readonly IUsuarioSesionService _sesion;

    public EmpresaRepository(IConexionMonitor conexion, IUsuarioSesionService sesion)
        : base(conexion)
    {
        _sesion = sesion;
    }

    public Task<Result<EmpresaDto?>> ObtenerAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.From<Modelados.Empresa>()
                .Order("id_empresa", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Limit(1)
                .Get(ct);

            var empresa = response?.Models.FirstOrDefault();
            return empresa is null ? null : Map(empresa);
        }, "Cargar configuración de empresa");

    public Task<Result<EmpresaGuardadaDto>> GuardarAsync(
        ActualizarEmpresaDto empresa,
        string? rutaLogoLocal,
        string? rutaIconoSidebarLocal,
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ExigirPermiso();

            var client = await ConexionSupabase.GetClientAsync();
            var actualResponse = await client.From<Modelados.Empresa>()
                .Where(x => x.IdEmpresa == empresa.IdEmpresa)
                .Limit(1)
                .Get(ct);
            var actual = actualResponse?.Models.FirstOrDefault()
                ?? throw new InvalidOperationException("No se encontró la empresa configurada.");

            string? rutaLogoNueva = null;
            string? rutaIconoNueva = null;
            if (!string.IsNullOrWhiteSpace(rutaLogoLocal))
            {
                ValidarLogo(rutaLogoLocal);
                rutaLogoNueva = CrearNombreImagen("logo", empresa.IdEmpresa, rutaLogoLocal);

                await client.Storage.From(BucketLogos).Upload(
                    rutaLogoLocal,
                    rutaLogoNueva,
                    new StorageFileOptions
                    {
                        CacheControl = "3600",
                        ContentType = ObtenerMime(rutaLogoLocal),
                        Upsert = false,
                    },
                    null,
                    true);
            }

            if (!string.IsNullOrWhiteSpace(rutaIconoSidebarLocal))
            {
                ValidarLogo(rutaIconoSidebarLocal);
                rutaIconoNueva = CrearNombreImagen("sidebar", empresa.IdEmpresa, rutaIconoSidebarLocal);

                try
                {
                    await client.Storage.From(BucketLogos).Upload(
                        rutaIconoSidebarLocal,
                        rutaIconoNueva,
                        new StorageFileOptions
                        {
                            CacheControl = "3600",
                            ContentType = ObtenerMime(rutaIconoSidebarLocal),
                            Upsert = false,
                        },
                        null,
                        true);
                }
                catch
                {
                    if (rutaLogoNueva is not null)
                        await EliminarMejorEsfuerzoAsync(client, rutaLogoNueva);
                    throw;
                }
            }

            try
            {
                var query = client.From<Modelados.Empresa>()
                    .Where(x => x.IdEmpresa == empresa.IdEmpresa)
                    .Set(x => x.NombreEmpresa, empresa.NombreEmpresa.Trim())
                    .Set(x => x.RtnEmpresa!, NormalizarOpcional(empresa.RtnEmpresa)!)
                    .Set(x => x.DireccionEmpresa!, NormalizarOpcional(empresa.DireccionEmpresa)!)
                    .Set(x => x.TelefonoEmpresa!, NormalizarOpcional(empresa.TelefonoEmpresa)!)
                    .Set(x => x.CorreoEmpresa!, NormalizarOpcional(empresa.CorreoEmpresa)!)
                    .Set(x => x.DominioCorreo!, NormalizarOpcional(empresa.DominioCorreo)!)
                    .Set(x => x.ColorEmpresa!, NormalizarOpcional(empresa.ColorEmpresa)!);

                if (rutaLogoNueva is not null)
                    query = query.Set(x => x.LogoEmpresa!, rutaLogoNueva);
                if (rutaIconoNueva is not null)
                    query = query.Set(x => x.IconoSidebar!, rutaIconoNueva);

                await query.Update(null, ct);
            }
            catch
            {
                if (rutaLogoNueva is not null)
                    await EliminarMejorEsfuerzoAsync(client, rutaLogoNueva);
                if (rutaIconoNueva is not null)
                    await EliminarMejorEsfuerzoAsync(client, rutaIconoNueva);
                throw;
            }

            var advertencias = new List<string>();
            string? logoVigente = rutaLogoNueva ?? actual.LogoEmpresa;
            string? iconoVigente = rutaIconoNueva ?? actual.IconoSidebar;

            await ReconciliarImagenesAsync(client, [logoVigente, iconoVigente], advertencias);

            var guardadaResponse = await client.From<Modelados.Empresa>()
                .Where(x => x.IdEmpresa == empresa.IdEmpresa)
                .Limit(1)
                .Get(ct);
            var guardadaModel = guardadaResponse?.Models.FirstOrDefault()
                ?? throw new InvalidOperationException("La empresa se guardó, pero no pudo recargarse.");
            var guardada = Map(guardadaModel);

            return new EmpresaGuardadaDto
            {
                Empresa = guardada,
                Advertencias = advertencias,
            };
        }, "Guardar configuración de empresa");

    public Task<Result> DescargarLogoAsync(
        string rutaStorage,
        string rutaLocalDestino,
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var descarga = client.Storage.From(BucketLogos)
                .DownloadPublicFile(rutaStorage, rutaLocalDestino, null, null);
            await descarga.WaitAsync(
                TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds), ct);
        }, "Descargar logo de empresa");

    private void ExigirPermiso()
    {
        if (!_sesion.TienePermiso(PermisoAdministrar))
            throw new UnauthorizedAccessException(
                "No tiene permiso para modificar la configuración de empresa.");
    }

    private static EmpresaDto Map(Modelados.Empresa e) => new()
    {
        IdEmpresa = e.IdEmpresa,
        NombreEmpresa = e.NombreEmpresa,
        RtnEmpresa = e.RtnEmpresa,
        DireccionEmpresa = e.DireccionEmpresa,
        TelefonoEmpresa = e.TelefonoEmpresa,
        CorreoEmpresa = e.CorreoEmpresa,
        LogoEmpresa = e.LogoEmpresa,
        IconoSidebar = e.IconoSidebar,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        DominioCorreo = e.DominioCorreo,
        ColorEmpresa = e.ColorEmpresa,
    };

    private static string? NormalizarOpcional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidarLogo(string ruta)
    {
        var info = new FileInfo(ruta);
        if (!info.Exists)
            throw new FileNotFoundException("No se encontró el logo seleccionado.", ruta);
        if (info.Length <= 0 || info.Length > MaxLogoBytes)
            throw new InvalidOperationException("El logo debe pesar entre 1 byte y 2 MB.");

        var extension = info.Extension.ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg")
            throw new InvalidOperationException("El logo debe ser PNG, JPG o JPEG.");
    }

    private static string CrearNombreImagen(string tipo, int idEmpresa, string rutaLocal)
    {
        var extension = Path.GetExtension(rutaLocal).ToLowerInvariant();
        return $"{tipo}_empresa_{idEmpresa}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
    }

    private static string ObtenerMime(string ruta) =>
        Path.GetExtension(ruta).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";

    private static async Task EliminarMejorEsfuerzoAsync(Supabase.Client client, string ruta)
    {
        try { await client.Storage.From(BucketLogos).Remove(ruta); }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "No se pudo revertir el logo {Logo}", ruta);
        }
    }

    private static async Task ReconciliarImagenesAsync(
        Supabase.Client client,
        IEnumerable<string?> rutasVigentes,
        List<string> advertencias)
    {
        var vigentes = rutasVigentes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.Ordinal);
        if (vigentes.Count == 0) return;

        try
        {
            var objetos = await client.Storage.From(BucketLogos)
                .List(string.Empty, new StorageSearchOptions { Limit = 1000 });
            var obsoletos = (objetos ?? [])
                .Where(x => !x.IsFolder && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name!)
                .Where(x => !vigentes.Contains(x))
                .ToList();

            if (obsoletos.Count > 0)
                await client.Storage.From(BucketLogos).Remove(obsoletos);
        }
        catch (Exception ex)
        {
            if (!advertencias.Any())
                advertencias.Add("La configuración se guardó, pero quedaron imágenes obsoletas pendientes de limpieza.");
            Serilog.Log.Warning(ex, "No se pudo reconciliar el bucket de imágenes de empresa");
        }
    }
}
