using CopyGIF.App.Views.Pages;
using CopyGIF.Presentation.Search;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace CopyGIF.App.Services;

internal sealed class SearchPageBehavior
{
    private readonly SearchPage _page;
    private readonly GridView _grid;
    private readonly Border _attribution;
    private readonly TextBlock _attributionText;
    public SearchPageBehavior(SearchPage page, GridView grid, Border attribution, TextBlock attributionText)
    {
        _page = page; _grid = grid; _attribution = attribution; _attributionText = attributionText;
        page.Loaded += PageLoaded;
        page.Unloaded += PageUnloaded;
        grid.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ResultsPointerWheelChanged), true);
        grid.KeyDown += ResultsKeyDown;
        grid.BringIntoViewRequested += (_, args) =>
        {
            if (_paginationOffset is not null || _restoringScroll) args.Handled = true;
        };
    }
    private ScrollViewer? _scrollViewer;
    private SearchViewModel? _observedModel;
    private bool _restoringScroll;
    private double? _paginationOffset;
    private void PageLoaded(object sender, RoutedEventArgs args)
    {
        _scrollViewer = FindScrollViewer(_grid);
        if (_scrollViewer is not null) _scrollViewer.ViewChanged += ScrollChanged;
        _observedModel = _page.DataContext as SearchViewModel;
        if (_observedModel is not null) _observedModel.PropertyChanged += ModelChanged;
        RefreshAttribution();
        RestoreTrendingScroll();
    }
    private void PageUnloaded(object sender, RoutedEventArgs args)
    {
        if (_scrollViewer is not null) _scrollViewer.ViewChanged -= ScrollChanged;
        if (_observedModel is not null) _observedModel.PropertyChanged -= ModelChanged;
        _observedModel = null;
    }
    private void ScrollChanged(object? sender, ScrollViewerViewChangedEventArgs args)
    {
        if (!_restoringScroll && _paginationOffset is null && _observedModel is { Mode: GifSearchMode.Trending, IsBusy: false, ActiveProviderId: "klipy" })
            _observedModel.TrendingScrollOffset = _scrollViewer?.VerticalOffset ?? 0;
    }
    private void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SearchViewModel.ActiveProviderId)) RefreshAttribution();
        if (args.PropertyName == nameof(SearchViewModel.IsLoadingMore))
        {
            if (_observedModel?.IsLoadingMore == true)
            {
                _scrollViewer ??= FindScrollViewer(_grid);
                _paginationOffset = _scrollViewer?.VerticalOffset ?? 0;
            }
            else if (_paginationOffset is double offset)
            {
                _restoringScroll = true;
                _page.DispatcherQueue.TryEnqueue(() =>
                {
                    _grid.UpdateLayout();
                    _scrollViewer?.ChangeView(null, offset, null, true);
                    _page.DispatcherQueue.TryEnqueue(() =>
                    {
                        _scrollViewer?.ChangeView(null, offset, null, true);
                        _paginationOffset = null;
                        _restoringScroll = false;
                    });
                });
            }
        }
        if (args.PropertyName == nameof(SearchViewModel.OperationState) && _observedModel?.IsBusy == false &&
            _paginationOffset is null)
            RestoreTrendingScroll();
    }
    private void RefreshAttribution()
    {
        bool giphy = _observedModel?.IsGiphy == true;
        _attribution.Visibility = giphy ? Visibility.Visible : Visibility.Collapsed;
        _attributionText.Visibility = giphy ? Visibility.Collapsed : Visibility.Visible;
    }
    private void RestoreTrendingScroll()
    {
        if (_observedModel is not { Mode: GifSearchMode.Trending, ActiveProviderId: "klipy" } model) return;
        double offset = model.TrendingScrollOffset;
        _restoringScroll = true;
        _page.DispatcherQueue.TryEnqueue(() =>
        {
            _grid.UpdateLayout();
            _scrollViewer?.ChangeView(null, offset, null, true);
            _page.DispatcherQueue.TryEnqueue(() => _restoringScroll = false);
        });
    }
    private void ResultsPointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        if (args.GetCurrentPoint(_grid).Properties.MouseWheelDelta < 0) LoadNextPageAtBottom();
    }
    private void ResultsKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is Windows.System.VirtualKey.PageDown or Windows.System.VirtualKey.Down or Windows.System.VirtualKey.End)
            LoadNextPageAtBottom();
    }
    private void LoadNextPageAtBottom()
    {
        if (_page.DataContext is not SearchViewModel { AutoLoadMoreResults: true } || _page.IsBusy || !_page.CanLoadMore) return;
        _scrollViewer ??= FindScrollViewer(_grid);
        if (_scrollViewer is null || _scrollViewer.ScrollableHeight - _scrollViewer.VerticalOffset > 24) return;
        if (_page.LoadMoreCommand?.CanExecute(null) == true) _page.LoadMoreCommand.Execute(null);
    }
    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }

}
