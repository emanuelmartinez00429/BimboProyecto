using System.Collections.ObjectModel;
using CapaAplicacion.Search.Dtos;
using CapaAplicacion.Search.Queries;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;

namespace CapaUI.ViewModels.Search;

public partial class UniversalSearchViewModel : ObservableObject
{
    private readonly IMediator         _mediator;
    private CancellationTokenSource?   _cts;

    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasResults;
    [ObservableProperty] private string _statusText = string.Empty;

    public ObservableCollection<SearchResultDto> Results { get; } = [];

    /// <summary>
    /// Se dispara cuando el usuario selecciona un resultado.
    /// El consumidor (MainViewModel) decide cómo navegar según EntityType.
    /// </summary>
    public event Action<SearchResultDto>? ResultSelected;

    public UniversalSearchViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    public void TriggerSearch(string term)
    {
        SearchTerm = term;
        if (SearchCommand.CanExecute(null))
            SearchCommand.Execute(null);
    }

    [RelayCommand]
    private async Task SearchAsync(CancellationToken externalCt)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

        try
        {
            await Task.Delay(300, _cts.Token);

            IsLoading = true;
            Results.Clear();
            HasResults = false;

            var result = await _mediator.Send(
                new UniversalSearchQuery(SearchTerm), _cts.Token);

            foreach (var item in result.Items)
                Results.Add(item);

            HasResults = Results.Count > 0;
            StatusText = Results.Count > 0
                ? $"{Results.Count} resultado(s) en {result.Elapsed.TotalMilliseconds:F0} ms"
                : "Sin resultados";
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectResult(SearchResultDto result)
        => ResultSelected?.Invoke(result);

    partial void OnSearchTermChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) return;
        Results.Clear();
        HasResults = false;
        StatusText = string.Empty;
    }
}
