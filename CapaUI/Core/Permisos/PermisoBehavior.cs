using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CapaUI.Core.Permisos
{
    public enum LogicaPermiso { OR, AND }

    /// <summary>
    /// Attached property que controla la Visibility de un UIElement según permisos.
    ///
    /// Uso en XAML:
    ///   &lt;!-- OR: accede si tiene al menos uno --&gt;
    ///   &lt;Button permisos:PermisoBehavior.Requiere="Consultar Empleado,Crear Empleado"
    ///           permisos:PermisoBehavior.Logica="OR" /&gt;
    ///
    ///   &lt;!-- AND: necesita todos --&gt;
    ///   &lt;Button permisos:PermisoBehavior.Requiere="Consultar Empleado,Modificar Empleado"
    ///           permisos:PermisoBehavior.Logica="AND" /&gt;
    /// </summary>
    public static class PermisoBehavior
    {
        // ── Requiere ─────────────────────────────────────────────────────────
        public static readonly DependencyProperty RequiereProperty =
            DependencyProperty.RegisterAttached(
                "Requiere", typeof(string), typeof(PermisoBehavior),
                new PropertyMetadata(null, OnChanged));

        public static void   SetRequiere(UIElement e, string v) => e.SetValue(RequiereProperty, v);
        public static string GetRequiere(UIElement e)           => (string)e.GetValue(RequiereProperty);

        // ── Logica ───────────────────────────────────────────────────────────
        public static readonly DependencyProperty LogicaProperty =
            DependencyProperty.RegisterAttached(
                "Logica", typeof(LogicaPermiso), typeof(PermisoBehavior),
                new PropertyMetadata(LogicaPermiso.OR, OnChanged));

        public static void          SetLogica(UIElement e, LogicaPermiso v) => e.SetValue(LogicaProperty, v);
        public static LogicaPermiso GetLogica(UIElement e)                  => (LogicaPermiso)e.GetValue(LogicaProperty);

        // ── Evaluación ───────────────────────────────────────────────────────
        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement el) Evaluar(el);
        }

        public static void Evaluar(UIElement element)
        {
            string? raw = GetRequiere(element);
            if (string.IsNullOrWhiteSpace(raw)) { element.Visibility = Visibility.Visible; return; }

            var permisos = raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => PermisoCatalogo.IntentarResolver(s, out var p) ? (Permiso?)p : null)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .ToArray();

            if (permisos.Length == 0)
            {
                Serilog.Log.Error("Configuración RBAC inválida en XAML: {Permisos}", raw);
                element.Visibility = Visibility.Collapsed;
                return;
            }

            bool tiene = GetLogica(element) == LogicaPermiso.OR
                ? SesionPermisos.TieneAlguno(permisos)
                : SesionPermisos.TieneTodos(permisos);

            element.Visibility = tiene ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Re-evalúa todos los elementos con permisos dentro de un visual tree.</summary>
        public static void EvaluarArbol(DependencyObject root)
        {
            if (root is UIElement ui && GetRequiere(ui) != null)
                Evaluar(ui);

            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
                EvaluarArbol(VisualTreeHelper.GetChild(root, i));
        }
    }
}
