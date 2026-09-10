using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Bitacora.Interfaces;
using CapaAplicacion.Categorias.Interfaces;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Fabricantes.Interfaces;
using CapaAplicacion.Contactos.Proveedores.Interfaces;
using CapaAplicacion.Fabricantes.Interfaces;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Notificaciones.Interfaces;
using CapaAplicacion.Perfil;
using CapaAplicacion.Presentaciones.Interfaces;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Proveedores.Interfaces;
using CapaAplicacion.Realtime;
using CapaAplicacion.Reportes.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Auth;
using CapaDatos.Conexion;
using CapaDatos.Perfil;
using CapaDatos.Realtime;
using CapaAplicacion.Empleados.Interfaces;
using CapaAplicacion.Empresa.Interfaces;
using CapaDatos.Repositories.Categorias;
using CapaDatos.Repositories.Contactos;
using CapaDatos.Repositories.GestionEmpleados;
using CapaDatos.Repositories.Empresa;
using CapaDatos.Repositories.Fabricantes;
using CapaDatos.Repositories.Pesaje;
using CapaDatos.Repositories.Notificaciones;
using CapaDatos.Repositories.Presentaciones;
using CapaDatos.Repositories.Productos;
using CapaDatos.Repositories.Proveedores;
using CapaDatos.Repositories.Search;
using CapaDatos.Repositories.Reportes;
using CapaDatos.Repositories.Usuarios;
using CapaDatos.Reportes;
using CapaDominio.Entities;
using CapaDominio.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace CapaDatos;

public static class DependencyInjection
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services)
    {
        // Autenticación
        services.AddTransient<IAuthService, AuthService>();

        // Recuperacion de contrasena por OTP. Antes los tres paneles de la UI le
        // hablaban directo a client.Auth (P-058); ahora pasan por este contrato.
        services.AddTransient<IRecuperacionPasswordService, RecuperacionPasswordService>();

        // Perfil del usuario — singleton porque mantiene estado entre login y logout
        services.AddSingleton<IPerfilUsuarioService, PerfilUsuarioService>();

        // Realtime — singleton: una sola conexión WebSocket, canales on-demand
        services.AddSingleton<IRealtimeService, RealtimeService>();

        // Monitor de conexión — singleton: una sola vigilancia de red para toda la app
        services.AddSingleton<IConexionMonitor, ConexionMonitor>();

        // Buscador universal (entidad de dominio)
        // Transient: repos sin estado mutable — Scoped era engañoso en WPF (sin scopes = singleton de facto)
        services.AddTransient<IRepository<Producto>, ProductoSearchRepository>();
        services.AddTransient<IRepository<Empleado>, EmpleadoRepository>();
        services.AddTransient<IRepository<Cliente>,  ClienteRepository>();

        // Formulario de productos (DTO con FKs)
        services.AddTransient<IProductoRepository, ProductoCrudRepository>();

        // ── Caché L1 en memoria ─────────────────────────────────────────────
        // Sin nivel distribuido ni backplane, a propósito: esto es una aplicación de
        // escritorio contra Supabase, no hay un tier compartido donde alojar un Redis,
        // y Supabase Realtime ya cumple el papel de backplane (ver InvalidadorCacheRealtime).
        //
        // Tampoco se configura SizeLimit en MemoryCache: activarlo obliga a que TODA
        // entrada declare su Size o el runtime lanza InvalidOperationException. Los 8
        // catálogos completos no llegan a 2 MB, y el límite real ya lo pone el
        // decorador, que solo retiene lo que entró completo en una página.
        services.AddFusionCache()
                .WithDefaultEntryOptions(new ZiggyCreatures.Caching.Fusion.FusionCacheEntryOptions
                {
                    Duration                 = TimeSpan.FromMinutes(30),
                    JitterMaxDuration        = TimeSpan.FromMinutes(5),
                    IsFailSafeEnabled        = true,
                    FailSafeMaxDuration      = TimeSpan.FromHours(6),
                    FailSafeThrottleDuration = TimeSpan.FromSeconds(30),
                    EagerRefreshThreshold    = 0.85f,
                    FactorySoftTimeout       = TimeSpan.FromMilliseconds(1500),
                    FactoryHardTimeout       = TimeSpan.FromSeconds(20),
                });

        // Singleton obligatorio: como Transient cada consumidor recibiría su propio
        // almacén vacío, la tasa de aciertos sería cero y no lo denunciaría ninguna
        // excepción — compila, corre y no hace nada.
        services.AddSingleton<CapaAplicacion.Common.Cache.ICacheService, Cache.FusionCacheService>();

        // Suscriptor de vida larga que traduce eventos de Realtime en purgas.
        // MainWindow debe invocar Suscribir() en CADA sesión: al cerrar sesión,
        // RealtimeService vacía su diccionario de suscriptores y este singleton
        // sobrevive, así que sin esa llamada el segundo login de la máquina se
        // quedaría sin invalidación reactiva, en silencio.
        services.AddSingleton<CapaAplicacion.Common.Cache.IInvalidadorCacheRealtime,
                              Cache.InvalidadorCacheRealtime>();

        // Lectura uniforme de catálogos chicos (alimenta el selector genérico de lupa).
        // El concreto se registra POR SU TIPO y la interfaz devuelve el decorador: si el
        // concreto se registrara por la interfaz, el GetRequiredService de la fábrica se
        // resolvería a sí mismo y sería StackOverflowException — que en .NET no se puede
        // capturar y cierra el proceso sin dejar rastro en el log.
        services.AddTransient<Repositories.Catalogos.CatalogoRepository>();
        services.AddTransient<CapaAplicacion.Common.Catalogos.ICatalogoRepository>(sp =>
            new Repositories.Catalogos.CachedCatalogoRepository(
                sp.GetRequiredService<Repositories.Catalogos.CatalogoRepository>(),
                sp.GetRequiredService<CapaAplicacion.Common.Cache.ICacheService>()));

        // Formularios de catálogo
        services.AddTransient<IProveedorRepository, ProveedorCrudRepository>();
        services.AddTransient<IFabricanteRepository, FabricanteCrudRepository>();
        services.AddTransient<ICategoriaRepository, CategoriaCrudRepository>();
        services.AddTransient<IPresentacionRepository, PresentacionCrudRepository>();

        // Contactos de fabricantes y proveedores
        services.AddTransient<IContactoFabricanteRepository, ContactoFabricanteCrudRepository>();
        services.AddTransient<IContactoProveedorRepository, ContactoProveedorCrudRepository>();

        // Pesaje — selector de productos por proveedor (extensión aditiva del buscador)
        services.AddTransient<IPickerProductoRepository, PickerProductoRepository>();

        // Pesaje — persistencia real (movimientos / movimiento_productos / entradas_producto)
        services.AddTransient<IPesajeRepository, PesajeRepository>();

        // Notificaciones internas — Supabase es la única fuente de verdad.
        services.AddTransient<INotificacionRepository, NotificacionRepository>();

        // Usuarios — CRUD, consulta de roles, y servicio de sesion
        services.AddTransient<IUsuarioRepository, UsuarioRepository>();
        services.AddTransient<IRolRepository, RolRepository>();
        services.AddSingleton<IUsuarioSesionService, UsuarioSesionService>();
        services.AddTransient<IRolPermisoRepository, RolPermisoRepository>();

        // Configuración de la empresa y logo corporativo
        services.AddTransient<IEmpresaRepository, EmpresaRepository>();

        // Empleados — solo lectura por ahora (módulo en revisión)
        services.AddTransient<IEmpleadoRepository, EmpleadoCrudRepository>();

        // Bitácora — solo lectura por diseño (la escribe el sistema, no la UI)
        services.AddTransient<IBitacoraRepository, Repositories.Bitacora.BitacoraCrudRepository>();

        // Reportes PDF/Excel y registro auditado mediante RPC
        services.AddTransient<IReportStrategy, PdfReportStrategy>();
        services.AddTransient<IReportStrategy, ExcelReportStrategy>();
        services.AddTransient<IReporteRepository, ReporteRepository>();
        services.AddTransient<IReporteConsultaRepository, ReporteConsultaRepository>();

        return services;
    }
}
