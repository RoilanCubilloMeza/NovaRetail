using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NovaRetail.Data;

/// <summary>
/// ObservableCollection that supports replacing all items with a single
/// <see cref="NotifyCollectionChangedAction.Reset"/> notification instead
/// of N+1 (Clear + N×Add) notifications.
/// </summary>
public sealed class BatchObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Replaces every item in the collection with <paramref name="items"/>,
    /// raising exactly one <see cref="NotifyCollectionChangedAction.Reset"/>
    /// notification after the swap.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
    }
}
