using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Pesaje.Interfaces;
using CapaDominio.Reglas;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaUI.Core.Validacion;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>
    /// Lo que el modal devuelve al confirmar: la carga entera resuelta en tres listas.
    /// Viajan juntas porque se guardan juntas, en una sola transacción.
    /// </summary>
    public record CambiosCarga(
        IReadOnlyList<ProductoCargaAlta> Altas,
        IReadOnlyList<ProductoCargaCambio> Cambios,
        IReadOnlyList<int> Bajas);

    /// <summary>
    /// Gestión de <b>toda</b> la carga de una recepción: los productos que ya tiene, los
    /// que se suman y los que se quitan, en una sola tabla y un solo guardado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reemplaza a <c>ProductoCamionModal</c>, que era un formulario de un producto por
    /// vez: cargar cinco productos eran cinco pasadas del modal y cinco viajes de red,
    /// cada uno con una recarga completa de la pantalla detrás.
    /// </para>
    /// <para>
    /// El proveedor <b>no se elige acá</b>: la recepción (placa + proveedor) ya viene
    /// elegida de la tabla de camiones de la pantalla, y se muestra como contexto. Un
    /// camión con carga de dos proveedores son dos recepciones distintas en la lista, así
    /// que para cargarle productos al otro proveedor se selecciona la otra recepción.
    /// </para>
    /// <para>
    /// <b>Nada se escribe hasta «Finalizar».</b> Editar un peso o tocar el basurero solo
    /// mueve estado en memoria, así que «Cancelar» descarta todo. El guardado es atómico
    /// del lado del servidor (<c>registrar_productos_lote_seguro</c>): si una fila falla,
    /// no se persiste ninguna.
    /// </para>
    /// </remarks>
    public partial class ProductosCargaModal : UserControl, IDisposable
    {
        private readonly ICatalogoRepository _catalogos;
        private readonly CamionPesaje _camion;
        private System.Windows.Media.Animation.Storyboard? _spinnerGuardar;

        private readonly ObservableCollection<FilaProducto> _filas = new();

        /// <summary>
        /// Productos que estaban en la carga y el operador quitó. Se acumulan acá en vez
        /// de borrarse en el acto: hasta «Finalizar» no se escribe nada.
        /// </summary>
        private readonly List<int> _bajas = new();

        private SelectorCatalogoModal? _selectorCatalogo;

        private readonly double _anchoPropio;
        private readonly double _altoPropio;

        private bool _guardando;

        public event Action? Cerrado;

        /// <summary>
        /// Devuelve <c>true</c> si el guardado salió bien (el modal se cierra) y
        /// <c>false</c> si falló (el modal queda abierto con los datos para corregir).
        /// </summary>
        public event Func<CambiosCarga, System.Threading.Tasks.Task<bool>>? Confirmado;

        /// <summary>
        /// Constructor sin parámetros. La app nunca lo usa — siempre entra por el que
        /// recibe el repositorio y el camión.
        /// <para/>
        /// Existe porque WPF exige que un tipo con <c>x:Class</c> sea instanciable sin
        /// argumentos. <b>El diseñador de Visual Studio no lo ejecuta</b>: comprobado el
        /// 2026-09-09 — arma el árbol parseando el XAML y no corre el code-behind del
        /// documento raíz, así que todo lo que se asigne acá (título, placa, filas) sale
        /// vacío en el lienzo. Por eso no siembra datos de muestra: sería código muerto
        /// disfrazado de ayuda al diseño. Ver ADR-028.
        /// </summary>
        public ProductosCargaModal()
        {
            // Solo se usan al OPERAR el modal (agregar producto, guardar). Van explícitos
            // en null! en vez de inventarles dobles: este camino no opera nada.
            _catalogos = null!;
            _camion    = null!;

            InitializeComponent();
        }

        public ProductosCargaModal(ICatalogoRepository catalogos, CamionPesaje camion, ProductoCamion? enfocar = null)
        {
            InitializeComponent();

            _catalogos   = catalogos;
            _camion      = camion;
            _anchoPropio = Width;
            _altoPropio  = Height;

            TxtTitulo.Text    = $"Camión {camion.Placa}";
            TxtPlaca.Text     = camion.Placa;
            TxtProveedor.Text = camion.Proveedor;

            // Los productos ya cargados entran como filas existentes: la tabla es la
            // carga completa, no solo lo que se está por agregar.
            foreach (var p in camion.Productos)
                _filas.Add(FilaProducto.Existente(p, _filas.Count));

            FilasHost.ItemsSource = _filas;
            ActualizarContadores();

            if (enfocar is not null)
                EnfocarFila(_filas.FirstOrDefault(f => f.IdMovProducto == enfocar.Id));
        }

        // ── Filas ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Abre el catálogo de productos en <b>multiselección</b>, acotado al proveedor de
        /// la recepción y excluyendo los que ya están en la tabla: el operador tilda de a
        /// varios y cada uno entra como una fila.
        /// </summary>
        private void AgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (_guardando) return;

            var cfg = Catalogos.Productos(
                _catalogos,
                idProveedor: _camion.IdProveedor,
                permiteMultiple: true,
                estaYaElegido: id => id.HasValue && _filas.Any(f => f.IdProducto == id.Value));

            var selector = new SelectorCatalogoModal(cfg);
            selector.Cerrado += CerrarSelectorCatalogo;

            // El selector NO se cierra desde acá: se cierra solo (evento Cerrado) al
            // terminar de emitir. Disponerlo dentro de su propio bucle de emisión revienta
            // la app — mismo comentario que en RegistroCamionesModal.
            selector.Seleccionado += item =>
            {
                if (item.Id is not int id) return;
                if (_filas.Any(f => f.IdProducto == id)) return;

                // El catálogo de productos trae el código en Descripcion
                // (ver Catalogos.Productos: DescripcionPrimero + TituloDescripcion "Código").
                _filas.Add(FilaProducto.Nuevo(id, item.Descripcion, item.Nombre, _filas.Count));
            };

            _selectorCatalogo               = selector;
            CatalogoSelectorHost.Content    = selector;
            CatalogoSelectorHost.Visibility = Visibility.Visible;
            ContenidoPrincipal.Visibility   = Visibility.Collapsed;
            AplicarMarcoSelector(true);
        }

        private void CerrarSelectorCatalogo()
        {
            _selectorCatalogo?.Dispose();
            _selectorCatalogo               = null;
            CatalogoSelectorHost.Content    = null;
            CatalogoSelectorHost.Visibility = Visibility.Collapsed;
            ContenidoPrincipal.Visibility   = Visibility.Visible;
            AplicarMarcoSelector(false);

            PanelError.Visibility = Visibility.Collapsed;
            ActualizarContadores();
            EnfocarFila(_filas.LastOrDefault(f => f.EsNuevo));
        }

        private void AplicarMarcoSelector(bool abierto)
        {
            Width  = abierto ? 880 : _anchoPropio;
            Height = abierto ? 780 : _altoPropio;
        }

        /// <summary>
        /// Quita una fila. Si el producto ya estaba guardado se anota como baja para que
        /// «Finalizar» lo anule en el servidor; si era nuevo simplemente desaparece.
        /// </summary>
        private void QuitarFila_Click(object sender, RoutedEventArgs e)
        {
            if (_guardando) return;
            if (sender is not FrameworkElement el || el.DataContext is not FilaProducto fila) return;
            if (!fila.PuedeQuitar) return;

            if (fila.IdMovProducto is int id) _bajas.Add(id);
            _filas.Remove(fila);

            RenumerarAlternas();
            PanelError.Visibility = Visibility.Collapsed;
            ActualizarContadores();
        }

        /// <summary>
        /// El rayado en zigzag depende de la posición, no de la fila: al quitar una del
        /// medio hay que recalcularlo o la tabla queda con dos filas grises pegadas.
        /// </summary>
        private void RenumerarAlternas()
        {
            for (int i = 0; i < _filas.Count; i++)
                _filas[i].EsAlterna = i % 2 == 1;
        }

        private void EnfocarFila(FilaProducto? fila)
        {
            if (fila is null) return;

            // Diferido: el contenedor de la fila recién agregada todavía no pasó por
            // layout, y Focus() sobre un elemento no medido devuelve false en silencio.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                fila.CajaPeso?.Focus();
                fila.CajaPeso?.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        // ── Enganche de las cajas de cada fila ──────────────────────────────────
        // Los TextBox viven dentro del DataTemplate, así que no son visibles por nombre
        // desde acá: cada uno se registra en su fila al cargarse, y cuando la fila tiene
        // las tres arma su validador.

        private void CajaPeso_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaPeso = caja);

        private void CajaBultos_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaBultos = caja);

        private void CajaObs_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaObs = caja);

        private static void Enganchar(object sender, Action<FilaProducto, TextBox> asignar)
        {
            if (sender is not TextBox caja || caja.DataContext is not FilaProducto fila) return;
            asignar(fila, caja);
            fila.SembrarSiHaceFalta(caja);
            fila.ArmarValidadorSiEstaCompleto();
        }

        private void Peso_Changed(object sender, TextChangedEventArgs e) =>
            Espejar(sender, (fila, texto) => fila.PesoTexto = texto);

        private void Bultos_Changed(object sender, TextChangedEventArgs e) =>
            Espejar(sender, (fila, texto) => fila.BultosTexto = texto);

        private void Obs_Changed(object sender, TextChangedEventArgs e) =>
            Espejar(sender, (fila, texto) => fila.Observaciones = texto);

        /// <summary>
        /// Las cajas no están bindeadas (ver <see cref="FilaProducto.SembrarSiHaceFalta"/>):
        /// el modelo se mantiene al día espejando lo tecleado. No se tocan los contadores
        /// acá — ninguno depende del texto, solo de cuántas filas hay.
        /// </summary>
        private void Espejar(object sender, Action<FilaProducto, string> asignar)
        {
            if (sender is not TextBox caja || caja.DataContext is not FilaProducto fila) return;
            asignar(fila, caja.Text);

            // Corregir un campo es la señal de que el mensaje de error ya se leyó.
            PanelError.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Al salir del campo, la vista vuelve al principio del texto: si no, el recorte
        /// con «…» de arriba muestra la cola en vez del arranque de la observación.
        /// </summary>
        private void Obs_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox caja) return;
            caja.CaretIndex = 0;
            caja.ScrollToHome();
        }

        // ── Contadores y estado ─────────────────────────────────────────────────

        private void ActualizarContadores()
        {
            int total  = _filas.Count;
            int nuevos = _filas.Count(f => f.EsNuevo);

            TxtContador.Text = total == 1 ? "1 producto" : $"{total} producto(s)";

            var partes = new List<string> { total == 1 ? "1 producto en la carga" : $"{total} producto(s) en la carga" };
            if (nuevos > 0)      partes.Add(nuevos == 1 ? "1 nuevo" : $"{nuevos} nuevos");
            if (_bajas.Count > 0) partes.Add(_bajas.Count == 1 ? "1 por quitar" : $"{_bajas.Count} por quitar");
            TxtPie.Text = string.Join(" · ", partes);

            PanelSinProductos.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Guardado ────────────────────────────────────────────────────────────

        /// <summary>
        /// Aviso de guardado en curso. Lo llama quien escucha <see cref="Confirmado"/>,
        /// que es el que hace el viaje de red y sabe cuándo arranca y cuándo termina.
        /// </summary>
        public void MostrarGuardando(bool activo)
        {
            _guardando = activo;

            BtnGuardar.IsEnabled         = !activo;
            BtnCancelar.IsEnabled        = !activo;
            BtnAgregarProducto.IsEnabled = !activo;

            TxtBtnGuardar.Text        = activo ? "Guardando..." : "Finalizar";
            IconoGuardar.Visibility   = activo ? Visibility.Collapsed : Visibility.Visible;
            SpinnerGuardar.Visibility = activo ? Visibility.Visible : Visibility.Collapsed;
            if (activo) IniciarSpinnerGuardar(); else DetenerSpinnerGuardar();
        }

        private void IniciarSpinnerGuardar()
        {
            if (_spinnerGuardar != null) return;
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
            { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
            System.Windows.Media.Animation.Storyboard.SetTarget(anim, SpinnerGuardar);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(anim,
                new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _spinnerGuardar = new System.Windows.Media.Animation.Storyboard();
            _spinnerGuardar.Children.Add(anim);
            _spinnerGuardar.Begin();
        }

        private void DetenerSpinnerGuardar()
        {
            if (_spinnerGuardar is null) return;
            _spinnerGuardar.Stop();
            _spinnerGuardar.Remove();
            _spinnerGuardar.Children.Clear();
            _spinnerGuardar = null;
        }

        private async void Guardar_Click(object sender, RoutedEventArgs e)
        {
            if (_guardando) return;

            if (_filas.Count == 0 && _bajas.Count == 0)
            {
                MostrarError("Agregá al menos un producto a la carga.");
                return;
            }

            // Reglas de campo, fila por fila. La primera que falla se queda con el foco y
            // su mensaje — de eso se encarga el validador.
            foreach (var fila in _filas)
                if (fila.Validador is not null && !fila.Validador.Validar()) return;

            // Los números son lo único que el validador de campos no cubre: exige que
            // estén, no que sean positivos.
            foreach (var fila in _filas)
            {
                if (!fila.PesoValido)
                {
                    MostrarError($"El peso manifestado de «{fila.Nombre}» tiene que ser un número mayor que cero.");
                    fila.CajaPeso?.Focus();
                    fila.CajaPeso?.SelectAll();
                    return;
                }
                if (!fila.BultosValidos)
                {
                    MostrarError($"Los bultos de «{fila.Nombre}» tienen que ser un número entero mayor que cero.");
                    fila.CajaBultos?.Focus();
                    fila.CajaBultos?.SelectAll();
                    return;
                }
            }

            PanelError.Visibility = Visibility.Collapsed;

            var altas = _filas.Where(f => f.EsNuevo)
                .Select(f => new ProductoCargaAlta(f.IdProducto, f.Peso, f.Bultos, f.Observaciones))
                .ToList();

            // Solo viajan las filas que realmente cambiaron: reenviar las intactas
            // ensuciaría la bitácora con "cambios" que no cambiaron nada.
            var cambios = _filas.Where(f => !f.EsNuevo && f.Modificado)
                .Select(f => new ProductoCargaCambio(f.IdMovProducto!.Value, f.Peso, f.Bultos, f.Observaciones))
                .ToList();

            if (altas.Count == 0 && cambios.Count == 0 && _bajas.Count == 0)
            {
                Cerrado?.Invoke();
                return;
            }

            if (Confirmado is null) return;

            bool ok = await Confirmado(new CambiosCarga(altas, cambios, _bajas.ToList()));
            if (ok) Cerrado?.Invoke();
            // Si falló, el modal queda abierto con todo cargado: el VM ya avisó por Toast.
        }

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            if (_selectorCatalogo is not null) { CerrarSelectorCatalogo(); return; }
            if (_guardando) return;
            Cerrado?.Invoke();
        }

        public void Dispose()
        {
            CerrarSelectorCatalogo();
        }

        // ── Fila ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Una fila de la tabla. Guarda los valores originales para poder distinguir lo
        /// que el operador tocó de lo que solo miró — solo lo tocado viaja como cambio.
        /// </summary>
        public partial class FilaProducto : ObservableObject
        {
            /// <summary><c>null</c> ⇒ producto nuevo, todavía sin fila en la base.</summary>
            public int? IdMovProducto { get; init; }

            public int    IdProducto { get; init; }
            public string Codigo     { get; init; } = "";
            public string Nombre     { get; init; } = "";

            /// <summary>Un producto con pesajes registrados es historial: no se quita.</summary>
            public bool TienePesajes { get; init; }

            /// <summary>
            /// El producto ya se cerró en la pantalla («Cerrar producto»). El servidor
            /// rechaza modificar un producto que no está abierto, así que su fila se
            /// muestra pero no se edita: si se dejara editable, corregirle el peso
            /// abortaría el lote entero al guardar — incluidas las filas buenas.
            /// </summary>
            public bool Cerrado { get; init; }

            public bool   PuedeQuitar  => !TienePesajes;
            public string MotivoQuitar => TienePesajes
                ? "Este producto ya tiene pesajes registrados y no se puede quitar"
                : "Quitar este producto de la carga";

            public bool EsNuevo    => IdMovProducto is null;
            public bool EsEditable => EsNuevo || !Cerrado;

            public string MotivoNoEditable => EsEditable
                ? ""
                : "Este producto está cerrado: reabrilo desde la pantalla para poder corregirlo";

            /// <summary>Fila de índice impar: fondo gris clarito, para leer en zigzag.</summary>
            [ObservableProperty] private bool _esAlterna;

            // Texto y no números: mientras se escribe "1" de "12.5" el valor es parcial y
            // eso es legal. La conversión ocurre al validar, no en cada tecla.
            public string PesoTexto     { get; set; } = "";
            public string BultosTexto   { get; set; } = "";
            public string Observaciones { get; set; } = "";

            private string _pesoOriginal   = "";
            private string _bultosOriginal = "";
            private string _obsOriginal    = "";

            public TextBox? CajaPeso   { get; set; }
            public TextBox? CajaBultos { get; set; }
            public TextBox? CajaObs    { get; set; }

            public ValidadorFormulario? Validador { get; private set; }

            public static FilaProducto Existente(ProductoCamion p, int indice) => new()
            {
                IdMovProducto  = p.Id,
                IdProducto     = p.IdProducto,
                Codigo         = p.ProductoCodigo,
                Nombre         = p.ProductoNombre,
                TienePesajes   = p.TienePesajes,
                Cerrado        = p.Estado == "Cerrado",
                EsAlterna      = indice % 2 == 1,
                PesoTexto      = p.PesoManifestado.ToString("0.##", CultureInfo.InvariantCulture),
                BultosTexto    = p.BultosDeclarados.ToString(CultureInfo.InvariantCulture),
                Observaciones  = p.Observaciones ?? "",
                _pesoOriginal   = p.PesoManifestado.ToString("0.##", CultureInfo.InvariantCulture),
                _bultosOriginal = p.BultosDeclarados.ToString(CultureInfo.InvariantCulture),
                _obsOriginal    = p.Observaciones ?? "",
            };

            public static FilaProducto Nuevo(int idProducto, string codigo, string nombre, int indice) => new()
            {
                IdProducto = idProducto,
                Codigo     = codigo,
                Nombre     = nombre,
                EsAlterna  = indice % 2 == 1,
            };

            public bool PesoValido =>
                double.TryParse(PesoTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) && p > 0;

            public bool BultosValidos =>
                int.TryParse(BultosTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) && b > 0;

            public double Peso =>
                double.TryParse(PesoTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0;

            public int Bultos =>
                int.TryParse(BultosTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : 0;

            public bool Modificado =>
                !string.Equals(PesoTexto.Trim(),     _pesoOriginal,   StringComparison.Ordinal) ||
                !string.Equals(BultosTexto.Trim(),   _bultosOriginal, StringComparison.Ordinal) ||
                !string.Equals(Observaciones.Trim(), _obsOriginal.Trim(), StringComparison.Ordinal);

            /// <summary>
            /// Vuelca el valor del modelo al TextBox recién creado. Las cajas no están
            /// bindeadas (el modelo las espeja por TextChanged), así que el valor inicial
            /// de un producto ya guardado hay que sembrarlo a mano.
            /// </summary>
            public void SembrarSiHaceFalta(TextBox caja)
            {
                if (ReferenceEquals(caja, CajaPeso)   && caja.Text.Length == 0) caja.Text = PesoTexto;
                if (ReferenceEquals(caja, CajaBultos) && caja.Text.Length == 0) caja.Text = BultosTexto;
                if (ReferenceEquals(caja, CajaObs)    && caja.Text.Length == 0) caja.Text = Observaciones;
            }

            /// <summary>
            /// La etiqueta nombra el producto y no un número de fila: en una carga de diez
            /// productos, «El peso es obligatorio» no dice cuál.
            /// </summary>
            public void ArmarValidadorSiEstaCompleto()
            {
                if (Validador is not null) return;
                if (CajaPeso is null || CajaBultos is null || CajaObs is null) return;

                Validador = ValidadorFormulario.Nuevo()
                    .Campo(CajaPeso,   $"El peso manifestado de {Nombre}").Segun(ReglasProductoCamion.Cantidad)
                    .Campo(CajaBultos, $"Los bultos de {Nombre}").Segun(ReglasProductoCamion.Cantidad)
                    .Campo(CajaObs,    $"Las observaciones de {Nombre}").Segun(ReglasProductoCamion.Observaciones)
                    .ValidarAlSalirDelCampo();
            }
        }
    }
}
