#if WINDOWS
using System.ComponentModel;
using Lyrava.Sources.YouTube;

namespace Lyrava.App.Windows;

/// <summary>Windows M1-01 搜索与浏览页面；队列接入由后续 M1-02 组合器完成。</summary>
public sealed class WindowsSearchPage : ContentPage
{
    private readonly WindowsSearchViewModel _viewModel;
    private readonly Entry _query = new()
    {
        Placeholder = "Search songs, artists, albums or videos",
        MaxLength = 200,
        Keyboard = Keyboard.Text,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
        AutomationId = "WindowsSearchQuery"
    };
    private readonly Button _search = new()
    {
        Text = "Search",
        AutomationId = "WindowsSearchButton"
    };
    private readonly Label _status = new()
    {
        AutomationId = "WindowsSearchStatus"
    };
    private readonly CollectionView _results = new()
    {
        SelectionMode = SelectionMode.Single,
        AutomationId = "WindowsSearchResults"
    };

    public WindowsSearchPage(WindowsSearchViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Title = "Lyrava · Search";
        BindingContext = _viewModel;

        _results.ItemsSource = _viewModel.Results;
        _results.ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label { FontSize = 16 };
            title.SetBinding(Label.TextProperty, nameof(WindowsSearchResult.Title));
            var id = new Label { FontSize = 11, Opacity = 0.65 };
            id.SetBinding(Label.TextProperty, nameof(WindowsSearchResult.VideoId));
            return new Border
            {
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 2),
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { title, id }
                }
            };
        });

        _search.Clicked += SearchClickedAsync;
        _query.Completed += SearchClickedAsync;
        _results.SelectionChanged += OnSelectionChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateView();

        Content = new Grid
        {
            Padding = new Thickness(24, 32),
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new Label { Text = "Windows search", FontSize = 26, FontAttributes = FontAttributes.Bold },
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { _query, _search }
                },
                _status,
                _results
            }
        };
        Grid.SetRow((VisualElement)((Grid)Content).Children[1], 1);
        Grid.SetRow((VisualElement)((Grid)Content).Children[2], 2);
        Grid.SetRow((VisualElement)((Grid)Content).Children[3], 3);
    }

    public event EventHandler<WindowsSearchResultSelectedEventArgs>? ResultSelected;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateView();
    }

    protected override void OnDisappearing()
    {
        _viewModel.CancelSearch();
        base.OnDisappearing();
    }

    private async void SearchClickedAsync(object? sender, EventArgs e)
    {
        await _viewModel.SearchAsync(_query.Text);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not WindowsSearchResult result)
        {
            return;
        }

        ResultSelected?.Invoke(this, new WindowsSearchResultSelectedEventArgs(result));
        _results.SelectedItem = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateView();

    private void UpdateView()
    {
        _status.Text = _viewModel.StatusText;
        _search.IsEnabled = !_viewModel.IsBusy;
        _query.IsEnabled = !_viewModel.IsBusy;
    }
}
#endif
