namespace CapaDominio
{
    // Representa una entrada navegable en el buscador.
    // Rellena PalabrasClave con sinónimos relevantes para tu negocio.
    public record EntradaBusqueda(
        string   Id,
        string   Titulo,
        string   Modulo,
        string   IconoSegoe,
        string[] PalabrasClave);

    public static class ServicioBuscador
    {
        // ── Catálogo de módulos navegables ─────────────────────────────
        // Para agregar entradas: añade un new EntradaBusqueda(…) a esta lista.
        private static readonly List<EntradaBusqueda> _catalogo = new()
        {
            new("empleados",
                "Gestión de Empleados",   "Usuarios",  "",
                new[]{ "empleados","empleado","personal","trabajador","staff","rrhh","recursos humanos" }),

            new("usuarios-sub",
                "Gestión de Usuarios",    "Usuarios",  "",
                new[]{ "usuarios","usuario","cuenta","cuentas","acceso","credenciales","login","perfil" }),

            new("roles",
                "Gestión de Roles",       "Usuarios",  "",
                new[]{ "roles","rol","permisos","permiso","acceso","autorización" }),

            new("bitacora",
                "Bitácora",               "Usuarios",  "",
                new[]{ "bitacora","bitácora","log","logs","auditoria","auditoría","historial","registro","actividad" }),

            new("prod-productos",
                "Gestión de Productos",   "Productos", "",
                new[]{ "productos","producto","inventario","catálogo","catalogo","stock","artículos","articulos" }),

            new("prod-proveedores",
                "Gestión de Proveedores", "Productos", "",
                new[]{ "proveedores","proveedor","supplier","suministrador","compras","abastecedor" }),

            new("prod-fabricantes",
                "Gestión de Fabricantes", "Productos", "",
                new[]{ "fabricantes","fabricante","manufacturer","marca","marcas","producción","produccion" }),

            new("prod-categorias",
                "Gestión de Categorías",  "Productos", "",
                new[]{ "categorías","categorias","categoría","categoria","tipo","tipos","clasificación","clasificacion" }),

            new("pes-movs",
                "Movimientos y Entradas", "Pesajes",   "",
                new[]{ "movimientos","pesajes","pesaje","entrada","entradas","movimiento","peso","báscula","bascula","línea","linea" }),

            new("reporteria",
                "Reportería",             "Módulo",    "",
                new[]{ "reportería","reporteria","reporte","reportes","informe","informes","estadística","estadistica","análisis","analisis","dashboard" }),
        };

        // Devuelve hasta 6 entradas que coincidan con el texto.
        // Si el texto está vacío devuelve las primeras 6 (sugerencias por defecto).
        public static IEnumerable<EntradaBusqueda> Buscar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return _catalogo.Take(6);

            var q = NormalizeQuery(texto);

            return _catalogo
                .Select(e => (Entrada: e, Score: Puntuar(e, q)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(6)
                .Select(x => x.Entrada);
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static string NormalizeQuery(string s) =>
            s.ToLowerInvariant().Trim();

        private static int Puntuar(EntradaBusqueda e, string q)
        {
            var titulo  = e.Titulo.ToLowerInvariant();
            var modulo  = e.Modulo.ToLowerInvariant();

            if (titulo == q  || e.Id == q)          return 100;
            if (titulo.StartsWith(q))               return  80;
            if (titulo.Contains(q))                 return  60;
            if (modulo.Contains(q))                 return  40;
            if (e.PalabrasClave.Any(p => p == q))   return  70;
            if (e.PalabrasClave.Any(p => p.StartsWith(q))) return 50;
            if (e.PalabrasClave.Any(p => p.Contains(q)))   return 30;
            return 0;
        }
    }
}
