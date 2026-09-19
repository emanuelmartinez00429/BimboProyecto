using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaDominio.Reglas;
using CapaDominio.Reportes;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>
    /// Elige formato y alcance del reporte «Pesado de Insumos BES».
    /// </summary>
    /// <remarks>
    /// El alcance se elige por <b>placa</b>, no por recepción: un camión con carga de tres
    /// proveedores son tres filas en la pantalla pero un solo camión, y el reporte lo saca
    /// unificado. Se pueden marcar una, varias o todas las placas; todo va a un archivo.
    /// </remarks>
    public partial class ReporteModal : UserControl
    {
        public event Action<ReportFormat, List<CamionPesaje>>? FormatoSeleccionado;
        public event Action? Cerrado;

        private readonly List<OpcionPlacaReporte> _placas = new();

        /// <summary>Constructor de diseño (el diseñador de VS instancia por acá). Ver ADR-028.</summary>
        public ReporteModal()
        {
            InitializeComponent();
        }

        /// <param name="recepciones">Recepciones en pantalla (abiertas y las recién cerradas).</param>
        /// <param name="placasMarcadas">Placas que arrancan tildadas.</param>
        public ReporteModal(IReadOnlyList<CamionPesaje> recepciones, IEnumerable<string> placasMarcadas)
        {
            InitializeComponent();

            var marcadas = placasMarcadas.Select(ReglasCamion.NormalizarPlaca).ToHashSet(StringComparer.Ordinal);

            foreach (var grupo in recepciones
                         .GroupBy(r => ReglasCamion.NormalizarPlaca(r.Placa), StringComparer.Ordinal)
                         .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var opcion = new OpcionPlacaReporte(grupo.Key, grupo.ToList()) { Incluida = marcadas.Contains(grupo.Key) };
                opcion.PropertyChanged += Opcion_PropertyChanged;
                _placas.Add(opcion);
            }

            LstPlacas.ItemsSource = _placas;
            ActualizarResumen();
        }

        private void Opcion_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OpcionPlacaReporte.Incluida)) ActualizarResumen();
        }

        private List<CamionPesaje> RecepcionesIncluidas() =>
            _placas.Where(p => p.Incluida).SelectMany(p => p.Recepciones).ToList();

        private void ActualizarResumen()
        {
            int placas = _placas.Count(p => p.Incluida);
            int recepciones = _placas.Where(p => p.Incluida).Sum(p => p.Recepciones.Count);

            TxtResumenAlcance.Text = placas == 0
                ? "Marcá al menos un camión para generar el reporte."
                : $"{(placas == 1 ? "Se incluye 1 camión" : $"Se incluyen {placas} camiones")} " +
                  $"({(recepciones == 1 ? "1 recepción" : $"{recepciones} recepciones")}) en un solo reporte.";

            BtnPdf.IsEnabled   = placas > 0;
            BtnExcel.IsEnabled = placas > 0;
            BtnMarcarTodas.IsEnabled   = placas < _placas.Count;
            BtnMarcarNinguna.IsEnabled = placas > 0;
        }

        private void BtnMarcarTodas_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in _placas) p.Incluida = true;
        }

        private void BtnMarcarNinguna_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in _placas) p.Incluida = false;
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e) => Emitir(ReportFormat.Pdf);

        private void BtnExcel_Click(object sender, RoutedEventArgs e) => Emitir(ReportFormat.Excel);

        private void Emitir(ReportFormat formato)
        {
            var lista = RecepcionesIncluidas();
            if (lista.Count > 0) FormatoSeleccionado?.Invoke(formato, lista);
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        /// <summary>Una placa de la lista: todas sus recepciones entran o salen juntas.</summary>
        public sealed partial class OpcionPlacaReporte : ObservableObject
        {
            public OpcionPlacaReporte(string placa, IReadOnlyList<CamionPesaje> recepciones)
            {
                Placa = placa;
                Recepciones = recepciones;
            }

            public string Placa { get; }

            public IReadOnlyList<CamionPesaje> Recepciones { get; }

            /// <summary>"3 proveedores · A, B, C" — o solo el nombre si es uno.</summary>
            public string Detalle
            {
                get
                {
                    var proveedores = Recepciones.Select(r => r.Proveedor).Distinct().ToList();
                    return proveedores.Count == 1
                        ? proveedores[0]
                        : $"{proveedores.Count} proveedores · {string.Join(", ", proveedores)}";
                }
            }

            [ObservableProperty] private bool _incluida;
        }
    }
}
