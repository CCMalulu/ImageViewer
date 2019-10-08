﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GongSolutions.Wpf.DragDrop;
using ImageFramework.Annotations;
using ImageFramework.Model;
using ImageFramework.Model.Filter;
using ImageViewer.Commands;
using ImageViewer.Models;
using ImageViewer.ViewModels.Filter;
using ImageViewer.Views.Filter;

namespace ImageViewer.ViewModels
{
    public class FiltersViewModel : INotifyPropertyChanged, IDropTarget
    {
        private readonly ModelsEx models;
        private readonly SolidColorBrush changesBrush;
        private readonly SolidColorBrush noChangesBrush;
        private class FilterItem : IDisposable
        {
            public FilterModel Model { get; }
            public FilterListBoxItem ListView { get; }
            public FilterParametersViewModel Parameters { get; }

            public FilterItem(FiltersViewModel parent, FilterModel model, ImagesModel images)
            {
                Model = model;
                Parameters = new FilterParametersViewModel(model, images);
                ListView = new FilterListBoxItem(parent, model, Parameters);
            }

            /// <summary>
            /// returns true if this filter will be visible for the equation after applying
            /// </summary>
            /// <param name="id"></param>
            /// <returns></returns>
            public bool WillBeVisibleFor(int id)
            {
                return Parameters.IsVisible && Parameters.IsEquationVisible[id];
            }

            public void Dispose()
            {
                Model?.Dispose();
                Parameters?.Dispose();
            }
        }

        private List<FilterItem> items = new List<FilterItem>();

        public FiltersViewModel(ModelsEx models)
        {
            this.models = models;
            this.ApplyCommand = new ApplyFiltersCommand(this);
            this.CancelCommand = new CancelFiltersCommand(this);

            changesBrush = new SolidColorBrush(Color.FromRgb(237, 28, 36));
            noChangesBrush = (SolidColorBrush)models.Window.Window.FindResource("FontBrush");
        }

        private List<FilterListBoxItem> availableFilter = new List<FilterListBoxItem>();
        public List<FilterListBoxItem> AvailableFilter
        {
            get => availableFilter;
            set
            {
                availableFilter = value;
                OnPropertyChanged(nameof(AvailableFilter));
            }
        }

        private void UpdateAvailableFilter()
        {
            var res = new List<FilterListBoxItem>();
            foreach (var filterItem in items)
            {
                res.Add(filterItem.ListView);
            }

            AvailableFilter = res;
        }

        private ListBoxItem selectedFilter = null;
        public ListBoxItem SelectedFilter
        {
            get => selectedFilter;
            set
            {
                if (Equals(selectedFilter, value)) return;
                selectedFilter = value;
                OnPropertyChanged(nameof(SelectedFilter));
                OnPropertyChanged(nameof(SelectedFilterProperties));
            }
        }

        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }

        public ObservableCollection<object> SelectedFilterProperties
        {
            get
            {
                foreach (var filterItem in items)
                {
                    if (ReferenceEquals(filterItem.ListView, selectedFilter))
                        return filterItem.Parameters.View;
                }

                //Debug.Assert(false);
                return null;
            }
        }

        private bool hasChanges = false;
        public bool HasChanges
        {
            get => hasChanges;
            set
            {
                if (value == hasChanges) return;
                hasChanges = value;
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(TabItemColor));
            }
        }

        public Brush TabItemColor => HasChanges ? changesBrush : noChangesBrush;

        public void AddFilter(FilterModel filter)
        {
            var item = new FilterItem(this, filter, models.Images);
            items.Add(item);
            UpdateAvailableFilter();

            // select the added element
            SelectedFilter = item.ListView;
            UpdateHasChanges();

            // register on changed for apply and cancel button
            item.Parameters.Changed += (sender, args) => UpdateHasChanges();
        }

        public void RemoveFilter(FilterModel filter)
        {
            var index = items.FindIndex(item => item.Model.Equals(filter));
            var removeItem = items[index];
            items.RemoveAt(index);
            UpdateAvailableFilter();
            UpdateHasChanges();

            // dispose of shader data
            if (!models.Filter.IsUsed(removeItem.Model))
                removeItem.Dispose();
        }

        /// <summary>
        /// applies all pending changes from the parameters
        /// </summary>
        public void Apply()
        {
            // fill the has changed array
            //bool[] changed = new bool[4];
            //for (var i = 0; i < models.NumPipelines; ++i)
            //    changed[i] = HasEquationChanged(i);

            // apply the current parameters
            foreach (var filterItem in items)
            {
                filterItem.Parameters.Apply();
            }

            // exchange model lists
            var newModels = new List<FilterModel>();
            foreach (var filterItem in items)
            {
                newModels.Add(filterItem.Model);
            }

            //models.Filter.Apply(newModels, statisticsPoint, models.GlContext, changed);
            models.Filter.SetFilter(newModels);

            UpdateHasChanges();
        }

        private bool HasEquationChanged(int id)
        {
            var oldVisible = models.Filter.Filter.Where(filterModel => filterModel.IsEnabledFor(id)).ToList();
            var newVisible = items.Where(filterItem => filterItem.WillBeVisibleFor(id)).ToList();

            if (oldVisible.Count != newVisible.Count) return true;

            for (var i = 0; i < newVisible.Count; ++i)
            {
                // same model?
                if (!ReferenceEquals(oldVisible[i], newVisible[i].Model)) return true;

                // parameters changed?
                if (newVisible[i].Parameters.HasParameterChanges()) return true;
            }

            return false;
        }

        /// <summary>
        /// reverts all changes since the last apply
        /// </summary>
        public void Cancel()
        {
            // restore old state
            var filters = new List<FilterModel>();
            foreach (var filterItem in items)
            {
                filters.Add(filterItem.Model);
            }

            // dispose all filter which were never used after import
            FiltersModel.DisposeUnusedFilter(models.Filter.Filter, filters);

            // restore list
            var newItems = new List<FilterItem>();
            foreach (var filterModel in models.Filter.Filter)
            {
                // find the correspinging FilterItem if it still exists
                var filterItem = items.Find(fi => ReferenceEquals(fi.Model, filterModel));
                if (filterItem != null)
                {
                    newItems.Add(filterItem);
                    filterItem.Parameters.Cancel();
                }
                else
                {
                    // create a new filter item
                    var item = new FilterItem(this, filterModel, models.Images);
                    newItems.Add(item);
                    // register on changed for apply and cancel button
                    item.Parameters.Changed += (sender, args) => UpdateHasChanges();
                }
            }

            items = newItems;

            UpdateAvailableFilter();
            UpdateHasChanges();
        }

        /// <summary>
        /// compares the view model with the model data to determine if anything changed.
        /// Sets HasChanges
        /// </summary>
        private void UpdateHasChanges()
        {
            HasChanges = CalculateHasChanges();
        }

        /// <summary>
        /// compares the view model with the model data to determine if anything changed
        /// </summary>
        private bool CalculateHasChanges()
        {
            if (models.Filter.Filter.Count != items.Count) return true;

            // same amount of filter. do they match?
            for (int i = 0; i < items.Count; i++)
            {
                if (!ReferenceEquals(models.Filter.Filter[i], items[i].Model)) return true;
            }

            // have the parameters changed?
            foreach (var filterItem in items)
            {
                if (filterItem.Parameters.HasChanged) return true;
            }

            return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static bool IsValidDropItem(object o)
        {
            return o is FilterListBoxItem;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            // enable drop if both items are filter list box items
            if (IsValidDropItem(dropInfo.Data) && IsValidDropItem(dropInfo.TargetItem))
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            var idx1 = AvailableFilter.FindIndex(i => ReferenceEquals(i, dropInfo.Data));
            var idx2 = dropInfo.InsertIndex;
            if (idx1 < 0 || idx2 < 0) return;

            // put item from idx1 into the position it was dragged to
            items.Insert(idx2, items[idx1]);

            // remove the old items (duplicate)
            if (idx1 > idx2) ++idx1;
            items.RemoveAt(idx1);

            UpdateAvailableFilter();
            UpdateHasChanges();
        }

        public bool HasKeyToInvoke(Key key)
        {
            return items.Any(f => f.Parameters.HasKeyToInvoke(key));
        }

        public void InvokeKey(Key key)
        {
            foreach (var filterItem in items)
            {
                filterItem.Parameters.InvokeKey(key);
            }
        }
    }
}
