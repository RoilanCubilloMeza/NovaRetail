/*
    Developer Changes
    Author: Roberto M.
    Date: 2026-03-30T12:00:00Z
    ECS-3001: Performance Changes
    Notes: Added ObservableRangeCollection to batch collection updates and reduce UI thrash.
*/
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace NovaRetail.Infrastructure
{
    // Minimal ObservableRangeCollection with AddRange and ReplaceRange to batch UI notifications.
    public class ObservableRangeCollection<T> : ObservableCollection<T>
    {
        public ObservableRangeCollection() { }

        public ObservableRangeCollection(IEnumerable<T> items) : base(items is null ? Enumerable.Empty<T>() : new List<T>(items)) { }

        public void AddRange(IEnumerable<T> items)
        {
            if (items is null) return;
            var list = items as IList<T> ?? items.ToList();
            if (list.Count == 0) return;

            foreach (var i in list)
                Items.Add(i);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void ReplaceRange(IEnumerable<T> items)
        {
            var list = items as IList<T> ?? items?.ToList() ?? new List<T>();
            Items.Clear();
            foreach (var i in list)
                Items.Add(i);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void RemoveRange(IEnumerable<T> items)
        {
            if (items is null) return;
            var list = items as IList<T> ?? items.ToList();
            if (list.Count == 0) return;

            var removed = false;
            foreach (var i in list)
            {
                removed = Items.Remove(i) || removed;
            }

            if (removed)
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}