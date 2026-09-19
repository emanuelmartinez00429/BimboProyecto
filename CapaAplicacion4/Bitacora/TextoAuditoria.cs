using System.Globalization;
using System.Text.Json;

namespace CapaAplicacion.Bitacora;

/// <summary>Compatibilidad de lectura para auditoría histórica; nunca modifica el original.</summary>
public static class TextoAuditoria
{
    public static string Formatear(string? texto, string? tabla = null)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto ?? string.Empty;
        var inicio = texto.TrimStart();
        if (!inicio.StartsWith('{') && !inicio.StartsWith('[')) return texto;
        try
        {
            using var documento = JsonDocument.Parse(texto);
            return Renderizar(documento.RootElement, tabla, null);
        }
        catch (JsonException)
        {
            return "Detalle histórico incompleto; consulte el registro original.";
        }
    }

    private static string Renderizar(JsonElement valor, string? tabla, string? clave)
    {
        if (valor.ValueKind == JsonValueKind.Object)
        {
            var campos = valor.EnumerateObject()
                .Where(p => !p.Name.StartsWith("busqueda_", StringComparison.Ordinal))
                .OrderBy(p => p.Name.StartsWith("nombre_", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{Etiqueta(p.Name)}: {Renderizar(p.Value, tabla, p.Name)}");
            var resultado = string.Join("; ", campos);
            return resultado.Length == 0 ? "Sin registro" : resultado;
        }
        if (valor.ValueKind == JsonValueKind.Array)
        {
            var resultado = string.Join(", ", valor.EnumerateArray().Select(v => Renderizar(v, tabla, clave)));
            return resultado.Length == 0 ? "Ninguno" : resultado;
        }
        if (valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return "Sin dato";
        if (clave == "id_estado") return Estado(valor.ToString(), tabla);
        if (clave == "estado_categoria") return valor.ValueKind == JsonValueKind.True ? "Activa" : "Inactiva";
        if (valor.ValueKind == JsonValueKind.True) return "Sí";
        if (valor.ValueKind == JsonValueKind.False) return "No";
        return valor.ValueKind == JsonValueKind.String ? valor.GetString() ?? "Sin dato" : valor.ToString();
    }

    private static string Estado(string estado, string? tabla) => (tabla, estado) switch
    {
        ("movimientos", "7") => "Recepción abierta",
        ("movimientos", "8") => "Recepción cerrada",
        ("movimientos", "9") => "Recepción anulada",
        ("movimiento_productos", "7") => "Producto abierto",
        ("movimiento_productos", "8") => "Producto cerrado",
        ("movimiento_productos", "9") => "Producto anulado",
        ("entradas_producto", "1") => "Pesaje activo",
        ("entradas_producto", "9") => "Pesaje anulado",
        (_, "1") => "Activo",
        (_, "2") => "Inactivo",
        _ => $"Estado sin descripción (referencia {estado})"
    };

    public static string Etiqueta(string clave)
    {
        if (Etiquetas.TryGetValue(clave, out var etiqueta)) return etiqueta;
        var texto = clave.StartsWith("id_", StringComparison.Ordinal)
            ? "Referencia de " + clave[3..].Replace('_', ' ')
            : clave.Replace('_', ' ');
        return texto.Length == 0 ? "Dato" : char.ToUpper(texto[0], CultureInfo.InvariantCulture) + texto[1..];
    }

    private static readonly IReadOnlyDictionary<string, string> Etiquetas = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["id_estado"] = "Estado", ["estado_categoria"] = "Estado",
        ["nombre_producto"] = "Producto", ["codigo_producto"] = "Código",
        ["nombre_proveedor"] = "Proveedor", ["nombre_fabricante"] = "Fabricante",
        ["nombre_categoria"] = "Categoría", ["nombre_presentacion"] = "Presentación",
        ["nombre_rol"] = "Rol", ["alias_usuario"] = "Usuario",
        ["peso_tara"] = "Tara (kg)", ["peso_teorico"] = "Peso teórico (kg)",
        ["precio_por_kg"] = "Precio por kg", ["acciones"] = "Referencias de permisos",
        ["created_at"] = "Creado", ["updated_at"] = "Actualizado", ["es_sistema"] = "Rol del sistema",
        ["rtn_proveedor"] = "RTN", ["correo_proveedor"] = "Correo",
        ["telefono_proveedor"] = "Teléfono", ["direccion_proveedor"] = "Dirección",
        ["descripcion_categoria"] = "Descripción", ["descripcion_presentacion"] = "Descripción",
        ["descripcion_fabricante"] = "Descripción", ["uuid_usuario"] = "Referencia de autenticación"
    };
}
