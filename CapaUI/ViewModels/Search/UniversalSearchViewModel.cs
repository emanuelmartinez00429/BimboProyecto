using System.Collections.ObjectModel;
using System.Windows;
using CapaAplicacion.Search.Dtos;
using CapaAplicacion.Search.Queries;
using CapaUI.Services.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;

namespace CapaUI.ViewModels.Search;

public partial class UniversalSearchViewModel : ObservableObject
{
    private readonly IMediator          _mediator;
    private readonly INavigationService _navigation;
    private CancellationTokenSource?    _cts;

    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasResults;
    [ObservableProperty] private string _statusText = string.Empty;

    public ObservableCollection<SearchResultDto> Results { get; } = [];

    public UniversalSearchViewModel(IMediator mediator, INavigationService navigation)
    {
        _mediator   = mediator;
        _navigation = navigation;
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
        => _navigation.NavigateTo(result.EntityType, result.NavigationParam);

    partial void OnSearchTermChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) return;
        Results.Clear();
        HasResults = false;
        StatusText = string.Empty;
    }
}
