using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CapaAplicacion.Productos.Dtos;

namespace CapaUI.Core.Controls;

/// <summary>
/// Combo de filtro de catálogo con opción "(Todos)" y autocompletado en memoria.
///
/// La mecánica (sentinela, ICollectionView, filtro por texto al tipear, limpiar
/// el filtro al elegir) estaba copiada por cada combo de cada pantalla. Vive acá
/// una sola vez: dar de alta un filtro nuevo es instanciar esto.
/// </summary>
public sealed class ComboFiltro
{
    private readonly ComboBox          _combo;
    private List<FiltroItem>           _items = new();
    private ICollectionView?           _vista;
    private bool                       _silenciado;
    private bool                       _filtrando;
    private int?                       _idConfirmado;

    /// <summary>Id elegido, o null cuando está en "(Todos)".</summary>
    public event Action<int?>? SeleccionCambiada;

    public ComboFiltro(ComboBox combo, string textoTodos = "(Todos)")
    {
        _combo      = combo;
        TextoTodos  = textoTodos;

        _combo.PreviewKeyDown   += OnPreviewKeyDown;
        _combo.PreviewKeyUp     += OnPreviewKeyUp;
        _combo.SelectionChanged += OnSelectionChanged;
        _combo.DropDownClosed   += OnDropDownClosed;
    }

    public string TextoTodos { get; }

    /// <summary>Reemplaza el contenido del combo y lo deja en "(Todos)".</summary>
    public void Poblar(IEnumerable<FiltroItem> items)
    {
        _silenciado = true;

        _items = new List<FiltroItem> { new() { Id = null, Nombre = TextoTodos } };
        _items.AddRange(items);

        _vista = CollectionViewSource.GetDefaultView(_items);
        _combo.ItemsSource   = _vista;
        _combo.SelectedIndex = 0;
        _idConfirmado        = null;

        _silenciado = false;
    }

    /// <summary>Vuelve a "(Todos)" sin notificar (para "Limpiar filtros").</summary>
    public void Reiniciar()
    {
        _silenciado = true;
        if (_vista is not null) _vista.Filter = null;
        _combo.SelectedIndex = 0;
        _idConfirmado        = null;
        _silenciado          = false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            ConfirmarSeleccion();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Escape)
        {
            CancelarSeleccion();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            if (!_combo.IsDropDownOpen)
            {
                _combo.IsDropDownOpen = true;
            }
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_silenciado || _filtrando) return;

        // Si el desplegable está abierto, la selección puede ser por navegación con flechas;
        // no disparamos el filtro hasta que el usuario confirme con Enter o cierre el desplegable.
        if (!_combo.IsDropDownOpen)
        {
            var elegido = _combo.SelectedItem as FiltroItem;
            if (elegido != null && elegido.Id != _idConfirmado)
            {
                _idConfirmado = elegido.Id;
                SeleccionCambiada?.Invoke(_idConfirmado);
            }
        }
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter or Key.Up or Key.Down or Key.Escape or Key.Tab)
            return;

        if (_vista is null) return;

        var textBox = _combo.Template?.FindName("PART_EditableTextBox", _combo) as TextBox;
        var texto = textBox?.Text ?? _combo.Text ?? string.Empty;
        int caretPos = textBox?.CaretIndex ?? texto.Length;

        _filtrando = true;
        try
        {
            var textoFiltro = texto.Trim();
            _vista.Filter = string.IsNullOrEmpty(textoFiltro)
                ? null
                : o => o is FiltroItem f
                    && f.Nombre?.Contains(textoFiltro, StringComparison.OrdinalIgnoreCase) == true;

            _combo.IsDropDownOpen = true;

            if (textBox != null && textBox.Text != texto)
            {
                textBox.Text = texto;
                textBox.CaretIndex = Math.Min(caretPos, textBox.Text.Length);
            }
        }
        finally
        {
            _filtrando = false;
        }
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        if (_silenciado) return;
        ConfirmarSeleccion();
    }

    private void ConfirmarSeleccion()
    {
        var elegido = _combo.SelectedItem as FiltroItem;

        _silenciado = true;
        if (_vista is not null) _vista.Filter = null;
        if (elegido != null) _combo.SelectedItem = elegido;
        _combo.IsDropDownOpen = false;
        _silenciado = false;

        _idConfirmado = elegido?.Id;
        SeleccionCambiada?.Invoke(_idConfirmado);
    }

    private void CancelarSeleccion()
    {
        _silenciado = true;
        if (_vista is not null) _vista.Filter = null;
        var previo = _items.FirstOrDefault(i => i.Id == _idConfirmado) ?? _items.FirstOrDefault();
        if (previo != null) _combo.SelectedItem = previo;
        _combo.IsDropDownOpen = false;
        _silenciado = false;
    }
}
