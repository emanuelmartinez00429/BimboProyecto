using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapaDatos.Modelados.Usuarios
{
    // Vista SQL (security_invoker) que aplana el nombre del empleado para poder
    // buscar por alias O nombre en un solo OR server-side (P-021). PostgREST no
    // permite OR mezclando columnas del padre con columnas del JOIN.
    // Ver ADR-005 y "Supabase - Vistas SQL, RLS y security_invoker" en la bóveda.
    [Table("vista_usuarios_busqueda")]
    public class usuarioVista : BaseModel
    {
        [PrimaryKey("id_usuario")]
        public int idUsuario { get; set; }

        [Column("alias_usuario")]
        public string aliasUsuario { get; set; } = string.Empty;

        [Column("id_empleado")]
        public int idEmpleado { get; set; }

        [Column("id_rol")]
        public int idRol { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        [Column("uuid_usuario")]
        public string? uuidUsuario { get; set; }

        [Column("ultimo_acceso")]
        public DateTime? ultimoAcceso { get; set; }

        /// <summary>Nombre + apellido del empleado, aplanado por la vista para búsqueda.</summary>
        [Column("nombre_completo")]
        public string? nombreCompleto { get; set; }

        /// <summary>Columna generada en vista para búsqueda sin tildes (ADR-018 / P-039).</summary>
        [Column("busqueda_usuario")]
        public string? busquedaUsuario { get; set; }

        [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
        public DateTime? createdAt { get; set; }

        [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
        public DateTime? updatedAt { get; set; }

        public Roles? roles { get; set; }

        public Empleados? empleados { get; set; }

        public string nombre_Rol => roles?.nombreRol ?? "Sin rol";
        public string nombre_Empleado => empleados?.nombreEmpleado ?? "Sin empleado";
    }
}
