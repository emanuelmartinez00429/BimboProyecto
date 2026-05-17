using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CapaUI.Core.Permisos
{
    /// <summary>
    /// Mantiene el conjunto de permisos del usuario logueado durante la sesión.
    /// Se carga al hacer login según el id_rol.
    /// Cuando se conecte la tabla rol_permisos en BD, reemplazar CargarAsync con la consulta real.
    /// </summary>
    public static class SesionPermisos
    {
        private static readonly HashSet<Permiso> _permisos = new();
        public static int IdRolActual { get; private set; }

        public static Task CargarAsync(int idRol)
        {
            IdRolActual = idRol;
            _permisos.Clear();

            switch (idRol)
            {
                case 1: // Administrador → todos
                    foreach (Permiso p in Enum.GetValues<Permiso>())
                        _permisos.Add(p);
                    break;

                case 3: // Supervisor → ver + modificar
                    AgregarConSufijo("_Ver");
                    AgregarConSufijo("_Modificar");
                    break;

                case 2: // Operador → ver + crear pesajes
                    AgregarConSufijo("_Ver");
                    _permisos.Add(Permiso.Pesajes_Crear);
                    break;

                default: // Consulta → solo ver
                    AgregarConSufijo("_Ver");
                    break;
            }

            return Task.CompletedTask;
        }

        private static void AgregarConSufijo(string sufijo)
        {
            foreach (Permiso p in Enum.GetValues<Permiso>())
                if (p.ToString().EndsWith(sufijo, StringComparison.Ordinal))
                    _permisos.Add(p);
        }

        public static void Limpiar()
        {
            _permisos.Clear();
            IdRolActual = 0;
        }

        /// <summary>Lógica OR: visible si tiene AL MENOS uno de los permisos.</summary>
        public static bool TieneAlguno(params Permiso[] requeridos)
            => requeridos.Any(_permisos.Contains);

        /// <summary>Lógica AND: visible solo si tiene TODOS los permisos.</summary>
        public static bool TieneTodos(params Permiso[] requeridos)
            => requeridos.All(_permisos.Contains);

        public static bool Tiene(Permiso permiso)
            => _permisos.Contains(permiso);
    }
}
