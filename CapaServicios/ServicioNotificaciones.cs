namespace CapaServicios
{
    // Servicio singleton estático para notificaciones en tiempo real.
    // Cualquier módulo puede llamar Agregar(); la UI se suscribe a Actualizado.
    public static class ServicioNotificaciones
    {
        private static readonly List<Notificacion> _lista = new();

        // Se dispara en el hilo que llama — el suscriptor debe hacer Dispatcher.Invoke si actualiza UI
        public static event Action? Actualizado;

        public static IReadOnlyList<Notificacion> Todas    => _lista.AsReadOnly();
        public static IEnumerable<Notificacion>  Recientes => _lista.Take(10);
        public static int                         SinLeer  => _lista.Count(n => !n.Leida);

        public static void Agregar(Notificacion notif)
        {
            _lista.Insert(0, notif);
            if (_lista.Count > 100) _lista.RemoveAt(_lista.Count - 1);
            Actualizado?.Invoke();
        }

        public static void MarcarLeida(Guid id)
        {
            var n = _lista.FirstOrDefault(x => x.Id == id);
            if (n is null) return;
            n.Leida = true;
            Actualizado?.Invoke();
        }

        public static void MarcarTodasLeidas()
        {
            foreach (var n in _lista) n.Leida = true;
            Actualizado?.Invoke();
        }
    }
}
