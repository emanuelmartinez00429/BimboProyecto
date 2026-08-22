using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CapaUI.Core.MVVM;

/// <summary>
/// <see cref="ObservableCollection{T}"/> con un <see cref="ReplaceAll"/> que reconstruye
/// todo el contenido disparando UN solo <see cref="NotifyCollectionChangedAction.Reset"/>,
/// en vez de un <see cref="NotifyCollectionChangedAction.Add"/> por cada item.
/// <para/>
/// Pensada para listas atadas a un DataGrid/ListBox que se reconstruyen enteras seguido
/// (ej. al cambiar de filtro o de selección): con <c>Clear()</c> + <c>Add()</c> en loop de
/// la base, cada reconstrucción de N filas dispara N eventos que el control tiene que
/// procesar uno por uno de forma síncrona en el hilo de UI — con <see cref="ReplaceAll"/>
/// es un solo evento, sea cual sea N.
/// </summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items) Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
