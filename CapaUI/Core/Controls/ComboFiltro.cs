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

    /// <summary>Id elegido, o null cuando está en "(Todos)".</summary>
    public event Action<int?>? SeleccionCambiada;

    public ComboFiltro(ComboBox combo, string textoTodos = "(Todos)")
    {
        _combo      = combo;
        TextoTodos  = textoTodos;

        _combo.SelectionChanged += OnSelectionChanged;
        _combo.PreviewKeyUp     += OnPreviewKeyUp;
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

        _silenciado = false;
    }

    /// <summary>Vuelve a "(Todos)" sin notificar (para "Limpiar filtros").</summary>
    public void Reiniciar()
    {
        _silenciado = true;
        if (_vista is not null) _vista.Filter = null;
        _combo.SelectedIndex = 0;
        _silenciado = false;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_silenciado) return;

        var elegido = _combo.SelectedItem as FiltroItem;

        // Quitar el filtro de texto para que el próximo despliegue muestre todo.
        if (_vista is not null) _vista.Filter = null;

        SeleccionCambiada?.Invoke(elegido?.Id);
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter or Key.Up or Key.Down or Key.Escape or Key.Tab)
            return;

        if (_vista is null) return;

        var texto = _combo.Text?.Trim() ?? string.Empty;
        _vista.Filter = string.IsNullOrEmpty(texto)
            ? null
            : o => o is FiltroItem f
                && f.Nombre?.Contains(texto, StringComparison.OrdinalIgnoreCase) == true;

        _combo.IsDropDownOpen = true;
    }
}
