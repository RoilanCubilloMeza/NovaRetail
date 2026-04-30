using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace NovaRetail.Views;

public partial class ProductsPanel
{
    private double _savedVerticalOffset;

    partial void SaveNativeScrollPosition()
    {
        var scrollViewer = GetNativeScrollViewer();
        if (scrollViewer is not null)
            _savedVerticalOffset = scrollViewer.VerticalOffset;
    }

    async partial void RestoreNativeScrollPosition()
    {
        await Task.Delay(150);
        var scrollViewer = GetNativeScrollViewer();
        scrollViewer?.ChangeView(null, _savedVerticalOffset, null, disableAnimation: true);
    }

    private ScrollViewer? GetNativeScrollViewer()
    {
        var activeCollectionView = GetActiveCollectionView();
        if (activeCollectionView?.Handler?.PlatformView is not UIElement nativeView)
            return null;

        return FindDescendant<ScrollViewer>(nativeView);
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var result = FindDescendant<T>(child);
            if (result is not null)
                return result;
        }

        return null;
    }
}
