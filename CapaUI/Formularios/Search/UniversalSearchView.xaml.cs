using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CapaAplicacion.Search.Dtos;
using CapaUI.ViewModels.Search;

namespace CapaUI.Formularios.Search;

public partial class UniversalSearchView : UserControl
{
    private UniversalSearchViewModel? ViewModel => DataContext as UniversalSearchViewModel;

    public UniversalSearchView() => InitializeComponent();

    // Dispara búsqueda en cada KeyUp (ignora teclas de navegación)
    private void SearchBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up or Key.Enter or Key.Escape) return;

        if (ViewModel?.SearchCommand.CanExecute(null) == true)
            ViewModel.SearchCommand.Execute(null);
    }

    // ↓ desde el SearchBox → salta al primer resultado
    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            FocusListItem(0);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            FocusListItem(0);
            e.Handled = true;
        }
    }

    // ↑ / ↓ / Enter / Escape dentro del ListView
    private void ResultsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ResultsList.SelectedItem is SearchResultDto dto)
                ViewModel?.SelectResultCommand.Execute(dto);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape || (e.Key == Key.Up && ResultsList.SelectedIndex == 0))
        {
            // Vuelve al cuadro de búsqueda
            ResultsList.SelectedIndex = -1;
            SearchBox.Focus();
            SearchBox.CaretIndex = SearchBox.Text.Length;
            e.Handled = true;
        }
    }

    // Doble clic navega al detalle
    private void Result_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView { SelectedItem: SearchResultDto dto })
            ViewModel?.SelectResultCommand.Execute(dto);
    }

    // Mueve el foco visual al item del ListView en la posición indicada
    private void FocusListItem(int index)
    {
        ResultsList.UpdateLayout();
        if (ResultsList.ItemContainerGenerator.ContainerFromIndex(index)
            is ListViewItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
        }
    }
}
