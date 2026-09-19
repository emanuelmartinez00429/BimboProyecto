using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CapaDominio.Reglas;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Controles
{
    /// <summary>
    /// Gráfico "Pesadas (kg neto)" del modal de pesaje: una línea con el neto de cada pesada
    /// guardada y el punto amarillo "Ahora" de la pesada que se está tecleando.
    /// <para/>
    /// Se dibuja a mano en <see cref="OnRender"/> en vez de usar una librería de gráficos:
    /// son una docena de puntos, y así sale igual al diseño (etiquetas del eje Y a ambos
    /// lados, nodo "Ahora") sin sumar dependencias. Las cuentas (escala, raleo del eje X)
    /// viven en <see cref="ReglasPanelPesaje"/> para poder testearlas.
    /// </summary>
    public sealed class PesadasChart : FrameworkElement
    {
        private const double MargenIzq = 34, MargenDer = 34, MargenSup = 20, MargenInf = 20;
        private const double InsetX = 12;           // los nodos de las puntas no pisan los ejes
        private const int    MaxConValores = 8;      // con más nodos, la cifra pasa a tooltip

        private static readonly Brush Verde    = Congelar(new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)));
        private static readonly Brush Amarillo = Congelar(new SolidColorBrush(Color.FromRgb(0xFA, 0xCC, 0x15)));
        private static readonly Brush TextoEje = Congelar(new SolidColorBrush(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF)));
        private static readonly Brush TextoValor = Brushes.White;

        private static readonly Pen PenLinea    = Congelar(new Pen(Verde, 2));
        private static readonly Pen PenAro      = Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF))), 2));
        private static readonly Pen PenEje      = Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))), 1));
        private static readonly Pen PenGuia     = Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF))), 1)
                                                           { DashStyle = new DashStyle(new double[] { 2, 4 }, 0) });
        private static readonly Pen PenPromedio = Congelar(new Pen(Congelar(new SolidColorBrush(Color.FromArgb(0xB3, 0x93, 0xC5, 0xFD))), 1.2)
                                                           { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) });

        private static readonly Typeface Normal = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface Negrita = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private IReadOnlyList<double> _confirmados = Array.Empty<double>();
        private int _indiceAhora;
        private double? _netoAhora;

        // Posición de cada nodo en el último render, para el tooltip del modo compacto.
        private readonly List<(Point Pt, string Texto)> _nodos = new();

        /// <summary>Escala del eje Y que usó el último render (para el encabezado "máx").</summary>
        public double EscalaY { get; private set; }

        /// <summary>
        /// Carga los datos y redibuja.
        /// </summary>
        /// <param name="confirmados">Netos de las pesadas guardadas, en orden, SIN la que se edita.</param>
        /// <param name="indiceAhora">Lugar del nodo "Ahora": al final si es una pesada nueva, el
        /// de la entrada original si se está corrigiendo una.</param>
        /// <param name="netoAhora">Neto de la pesada en curso, o null si todavía no es válido
        /// (el hueco "Ahora" queda en el eje pero sin punto).</param>
        public void Actualizar(IReadOnlyList<double> confirmados, int indiceAhora, double? netoAhora)
        {
            _confirmados = confirmados;
            _indiceAhora = Math.Clamp(indiceAhora, 0, confirmados.Count);
            _netoAhora   = netoAhora > 0 ? netoAhora : null;

            // "Ahora" también cuenta: si el operador teclea algo fuera de rango, la escala lo sigue.
            IReadOnlyCollection<double> valores = _netoAhora is double n ? confirmados.Append(n).ToList() : confirmados;
            EscalaY = ReglasPanelPesaje.EscalaMaxima(valores);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            _nodos.Clear();
            double w = ActualWidth, h = ActualHeight;
            double plotW = w - MargenIzq - MargenDer, plotH = h - MargenSup - MargenInf;
            if (plotW <= 20 || plotH <= 20) return;

            // Fondo transparente pero presente: sin esto el MouseMove del tooltip solo
            // dispara encima de lo dibujado.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

            double dip  = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double yMax = EscalaY > 0 ? EscalaY : ReglasPanelPesaje.EscalaMaxima(_confirmados);
            double izq = MargenIzq, der = w - MargenDer, top = MargenSup, baseY = MargenSup + plotH;
            double Y(double v) => baseY - plotH * Math.Clamp(v / yMax, 0, 1);

            // Guías al 0/25/50/75/100 % y ejes laterales.
            for (int i = 1; i <= 4; i++)
                dc.DrawLine(PenGuia, new Point(izq, baseY - plotH * i / 4), new Point(der, baseY - plotH * i / 4));
            dc.DrawLine(PenEje, new Point(izq, top), new Point(izq, baseY));
            dc.DrawLine(PenEje, new Point(der, top), new Point(der, baseY));
            dc.DrawLine(PenEje, new Point(izq, baseY), new Point(der, baseY));

            // Etiquetas del eje Y a los dos lados: el operador la lee mire donde mire.
            foreach (double v in new[] { yMax, yMax / 2, 0 })
            {
                string t = v.ToString("0.##", CultureInfo.InvariantCulture);
                Texto(dc, t, izq - 6, Y(v), Alinear.Derecha, TextoEje, 10.5, Normal, dip);
                Texto(dc, t, der + 6, Y(v), Alinear.Izquierda, TextoEje, 10.5, Normal, dip);
            }
            Texto(dc, "kg", izq - 6, baseY + 13, Alinear.Derecha, TextoEje, 10, Normal, dip);

            // Slots: las pesadas guardadas más el hueco "Ahora".
            int slots = _confirmados.Count + 1;
            double X(int i) => slots == 1
                ? (izq + der) / 2
                : izq + InsetX + (plotW - 2 * InsetX) * i / (slots - 1);

            if (_confirmados.Count > 0)
            {
                double prom = _confirmados.Average();
                dc.DrawLine(PenPromedio, new Point(izq, Y(prom)), new Point(der, Y(prom)));
            }

            var puntos = new List<(Point Pt, bool EsAhora, double Valor, int Slot)>(slots);
            for (int s = 0, c = 0; s < slots; s++)
            {
                if (s == _indiceAhora)
                {
                    if (_netoAhora is double n) puntos.Add((new Point(X(s), Y(n)), true, n, s));
                }
                else
                {
                    double v = _confirmados[c++];
                    puntos.Add((new Point(X(s), Y(v)), false, v, s));
                }
            }

            if (puntos.Count > 1)
            {
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(puntos[0].Pt, false, false);
                    for (int i = 1; i < puntos.Count; i++) ctx.LineTo(puntos[i].Pt, true, true);
                }
                geo.Freeze();
                dc.DrawGeometry(null, PenLinea, geo);
            }

            bool conValores = slots <= MaxConValores;
            foreach (var p in puntos)
            {
                dc.DrawEllipse(p.EsAhora ? Amarillo : Verde, PenAro, p.Pt, 4.5, 4.5);
                string valor = p.Valor.ToString("0.##", CultureInfo.InvariantCulture);
                if (conValores)
                    Texto(dc, valor, p.Pt.X, p.Pt.Y - 13, Alinear.Centro, TextoValor, 11, Negrita, dip);
                _nodos.Add((p.Pt, $"{EtiquetaSlot(p.Slot)} · {p.Valor.ToString("N0", CultureInfo.InvariantCulture)} kg"));
            }

            // Eje X: "#n" por pesada guardada y "Ahora" en amarillo, raleadas si son muchas.
            for (int s = 0; s < slots; s++)
            {
                if (!ReglasPanelPesaje.MostrarEtiquetaEjeX(s, slots) && s != _indiceAhora) continue;
                bool ahora = s == _indiceAhora;
                Texto(dc, EtiquetaSlot(s), X(s), baseY + 11, Alinear.Centro,
                      ahora ? Amarillo : TextoEje, 10.5, ahora ? Negrita : Normal, dip);
            }
        }

        /// <summary>"#n" con el número de la pesada, o "Ahora" para el hueco en curso.</summary>
        private string EtiquetaSlot(int slot) => slot == _indiceAhora ? "Ahora" : $"#{slot + 1}";

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            // Solo en modo compacto: con pocos nodos la cifra ya está dibujada encima.
            if (_confirmados.Count + 1 <= MaxConValores) { ToolTip = null; return; }
            var pos = e.GetPosition(this);
            var cerca = _nodos.Where(n => Math.Abs(n.Pt.X - pos.X) < 10)
                              .OrderBy(n => Math.Abs(n.Pt.X - pos.X))
                              .Select(n => n.Texto).FirstOrDefault();
            ToolTip = cerca;
        }

        private enum Alinear { Izquierda, Centro, Derecha }

        private static void Texto(DrawingContext dc, string texto, double x, double yCentro, Alinear al,
                                  Brush brocha, double tam, Typeface tipo, double dip)
        {
            var ft = new FormattedText(texto, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tipo, tam, brocha, dip);
            double px = al switch
            {
                Alinear.Derecha => x - ft.Width,
                Alinear.Centro  => x - ft.Width / 2,
                _               => x,
            };
            dc.DrawText(ft, new Point(px, yCentro - ft.Height / 2));
        }

        private static T Congelar<T>(T f) where T : Freezable { f.Freeze(); return f; }
    }
}
