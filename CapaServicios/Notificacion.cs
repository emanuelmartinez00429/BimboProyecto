namespace CapaDominio
{
    public enum TipoNotificacion { Info, Advertencia, Error, Exito }

    public class Notificacion
    {
        public Guid   Id          { get; init; } = Guid.NewGuid();
        public string Titulo      { get; init; } = "";
        public string Descripcion { get; init; } = "";
        public DateTime Fecha     { get; init; } = DateTime.Now;
        public bool   Leida       { get; set;  } = false;
        public TipoNotificacion Tipo { get; init; } = TipoNotificacion.Info;

        // Color del indicador visual según tipo
        public string ColorPunto => Tipo switch
        {
            TipoNotificacion.Advertencia => "#EF4444",
            TipoNotificacion.Error       => "#DC2626",
            TipoNotificacion.Exito       => "#34D399",
            _                            => "#60A5FA"
        };

        public string TiempoRelativo
        {
            get
            {
                var diff = DateTime.Now - Fecha;
                if (diff.TotalSeconds < 60)  return "ahora mismo";
                if (diff.TotalMinutes < 60)  return $"hace {(int)diff.TotalMinutes} min";
                if (diff.TotalHours   < 24)  return $"hace {(int)diff.TotalHours} h";
                if (diff.TotalDays    < 7)   return $"hace {(int)diff.TotalDays} d";
                return Fecha.ToString("dd MMM");
            }
        }
    }
}
