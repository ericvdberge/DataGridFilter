﻿#region (c) 2022 Gilles Macabies All right reserved

// Author     : Gilles Macabies
// Solution   : FilterDataGrid
// Projet     : FilterDataGrid.Net5.0
// File       : FilterDataGrid.cs
// Created    : 06/03/2022
//

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace FilterDataGrid
{
    /// <summary>
    ///     Implementation of Datagrid
    /// </summary>
    public class FilterDataGrid : DataGrid, INotifyPropertyChanged
    {
        #region Constructors

        /// <summary>
        ///     FilterDataGrid constructor
        /// </summary>
        public FilterDataGrid()
        {
            DefaultStyleKey = typeof(FilterDataGrid);

            Debug.WriteLineIf(DebugMode, "Constructor");

            // load resources
            var resourceDictionary = new ResourceDictionary
            {
                Source = new Uri("/FilterDataGrid;component/Themes/FilterDataGrid.xaml", UriKind.Relative)
            };

            Resources.MergedDictionaries.Add(resourceDictionary);

            // initial popup size
            popUpSize = new Point
            {
                X = (double)TryFindResource("PopupWidth"),
                Y = (double)TryFindResource("PopupHeight")
            };

            CommandBindings.Add(new CommandBinding(ShowFilter, ShowFilterCommand, CanShowFilter));
            CommandBindings.Add(new CommandBinding(ApplyFilter, ApplyFilterCommand, CanApplyFilter)); // Ok
            CommandBindings.Add(new CommandBinding(CancelFilter, CancelFilterCommand));
            CommandBindings.Add(new CommandBinding(RemoveFilter, RemoveFilterCommand, CanRemoveFilter));
            CommandBindings.Add(new CommandBinding(IsChecked, CheckedAllCommand));
            CommandBindings.Add(new CommandBinding(ClearSearchBox, ClearSearchBoxClick));
            CommandBindings.Add(new CommandBinding(RemoveAllFilter, RemoveAllFilterCommand, CanRemoveAllFilter));
        }

        #endregion Constructors

        #region Command

        public static readonly ICommand ApplyFilter = new RoutedCommand();
        public static readonly ICommand CancelFilter = new RoutedCommand();
        public static readonly ICommand ClearSearchBox = new RoutedCommand();
        public static readonly ICommand IsChecked = new RoutedCommand();
        public static readonly ICommand RemoveAllFilter = new RoutedCommand();
        public static readonly ICommand RemoveFilter = new RoutedCommand();
        public static readonly ICommand ShowFilter = new RoutedCommand();

        #endregion Command

        #region Public DependencyProperty

        /// <summary>
        ///     Included Fields on AutoColumn
        /// </summary>
        public static readonly DependencyProperty IncludeFieldsProperty =
            DependencyProperty.Register("IncludeFieldsProperty",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata("*"));

        /// <summary>
        ///     date format displayed
        /// </summary>
        public static readonly DependencyProperty DateFormatStringProperty =
            DependencyProperty.Register("DateFormatString",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata("d"));

        /// <summary>
        ///     Language displayed
        /// </summary>
        public static readonly DependencyProperty FilterLanguageProperty =
            DependencyProperty.Register("FilterLanguage",
                typeof(Local),
                typeof(FilterDataGrid),
                new PropertyMetadata(Local.English));

        /// <summary>
        ///     Show elapsed time in status bar
        /// </summary>
        public static readonly DependencyProperty ShowElapsedTimeProperty =
            DependencyProperty.Register("ShowElapsedTime",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Show status bar
        /// </summary>
        public static readonly DependencyProperty ShowStatusBarProperty =
            DependencyProperty.Register("ShowStatusBar",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Show Rows Count
        /// </summary>
        public static readonly DependencyProperty ShowRowsCountProperty =
            DependencyProperty.Register("ShowRowsCount",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        #endregion Public DependencyProperty

        #region Public Event

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler Sorted;

        #endregion Public Event

        #region Private Fields

        private Stopwatch stopWatchFilter = new Stopwatch();
        private DataGridColumnHeadersPresenter columnHeadersPresenter;
        private bool pending;
        private bool search;
        private const bool DebugMode = false;
        private Cursor cursor;
        private int searchLength;
        private double minHeight;
        private double minWidth;
        private double sizableContentHeight;
        private double sizableContentWidth;
        private Grid sizableContentGrid;

        private List<string> includeFields;
        private List<FilterItemDate> treeView;
        private List<FilterItem> listBoxItems;

        private Point popUpSize;
        private Popup popup;

        private string fieldName;
        private string lastFilter;
        private string searchText;
        private TextBox searchTextBox;
        private Thumb thumb;

        private TimeSpan elapsed;

        private Type collectionType;
        private Type fieldType;

        private bool startsWith;

        #endregion Private Fields

        #region Public Properties

        /// <summary>
        ///     Included Fields
        /// </summary>
        public string IncludeFields
        {
            get => (string)GetValue(IncludeFieldsProperty);
            set
            {
                SetValue(IncludeFieldsProperty, value);
                ResetFields();
                
            }
        }

        /// <summary>
        ///     String begins with the specified character. Used in popup searchBox
        /// </summary>
        public bool StartsWith
        {
            get => startsWith;
            set
            {
                startsWith = value;
                OnPropertyChanged();

                // refresh filter
                if (!string.IsNullOrEmpty(searchText)) ItemCollectionView.Refresh();
            }
        }

        /// <summary>
        ///     Date format displayed
        /// </summary>
        public string DateFormatString
        {
            get => (string)GetValue(DateFormatStringProperty);
            set => SetValue(DateFormatStringProperty, value);
        }

        /// <summary>
        ///     Elapsed time
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get => elapsed;
            set
            {
                elapsed = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Language
        /// </summary>
        public Local FilterLanguage
        {
            get => (Local)GetValue(FilterLanguageProperty);
            set => SetValue(FilterLanguageProperty, value);
        }

        /// <summary>
        ///     Display items count
        /// </summary>
        public int ItemsSourceCount { get; set; }

        /// <summary>
        ///     Show elapsed time in status bar
        /// </summary>
        public bool ShowElapsedTime
        {
            get => (bool)GetValue(ShowElapsedTimeProperty);
            set => SetValue(ShowElapsedTimeProperty, value);
        }

        /// <summary>
        ///     Show status bar
        /// </summary>
        public bool ShowStatusBar
        {
            get => (bool)GetValue(ShowStatusBarProperty);
            set => SetValue(ShowStatusBarProperty, value);
        }

        /// <summary>
        ///     Show rows count
        /// </summary>
        public bool ShowRowsCount
        {
            get => (bool)GetValue(ShowRowsCountProperty);
            set => SetValue(ShowRowsCountProperty, value);
        }

        /// <summary>
        ///     Instance of Loc
        /// </summary>
        public Loc Translate { get; private set; }

        /// <summary>
        /// Tree View ItemsSource
        /// </summary>
        public List<FilterItemDate> TreeViewItems
        {
            get => treeView ?? new List<FilterItemDate>();
            set
            {
                treeView = value;
                OnPropertyChanged(nameof(TreeViewItems));
            }
        }

        /// <summary>
        /// ListBox ItemsSource
        /// </summary>
        public List<FilterItem> ListBoxItems
        {
            get => listBoxItems ?? new List<FilterItem>();
            set
            {
                listBoxItems = value;
                OnPropertyChanged(nameof(ListBoxItems));
            }
        }

        public Type FieldType
        {
            get => fieldType;
            set
            {
                fieldType = value;
                OnPropertyChanged();
            }
        }

        public List<FilterCommon> FilterPreset
        {
            get  => GlobalFilterList;
            set {
                OnFilterPresetChanged(value);
            }
        }

        #endregion Public Properties

        #region Private Properties

        private FilterCommon CurrentFilter { get; set; }
        private ICollectionView CollectionViewSource { get; set; }
        private ICollectionView ItemCollectionView { get; set; }

        private readonly List<FilterCommon> GlobalFilterList = new List<FilterCommon>();

        /// <summary>
        /// Popup filtered items (ListBox/TreeView)
        /// </summary>
        private IEnumerable<FilterItem> PopupViewItems =>
            ItemCollectionView?.OfType<FilterItem>().Where(c => c.Level != 0) ?? new List<FilterItem>();

        /// <summary>
        /// Popup source collection (ListBox/TreeView)
        /// </summary>
        private IEnumerable<FilterItem> SourcePopupViewItems =>
            ItemCollectionView?.SourceCollection.OfType<FilterItem>().Where(c => c.Level != 0) ?? new List<FilterItem>();

        #endregion Private Properties

        #region Protected Methods

        // CALL ORDER :
        // Constructor
        // OnInitialized
        // OnItemsSourceChanged

        /// <summary>
        ///     Initialize datagrid
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "OnInitialized");

            base.OnInitialized(e);

            try
            {
                // FilterLanguage : default : 0 (english)
                Translate = new Loc { Language = FilterLanguage };

                includeFields = IncludeFields.Split(',').Select(p => p.Trim().Replace(" ", "")).ToList();

                // sorting event
                Sorted += OnSorted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnInitialized : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Auto generated column, set templateHeader
        /// </summary>
        /// <param name="e"></param>
        protected override void OnAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "OnAutoGeneratingColumn");

            base.OnAutoGeneratingColumn(e);

            try
            {
                // get the columns that can be filtered
                var isColumnGeneratable = e.Column is System.Windows.Controls.DataGridTextColumn;
                if (!isColumnGeneratable) return;

                var column = new DataGridTextColumn
                {
                    Binding = new Binding(e.PropertyName) { ConverterCulture = Translate.Culture /* StringFormat */ },
                    FieldName = e.PropertyName,
                    Header = e.Column.Header.ToString(),
                    IsColumnFiltered = false
                };

                // get type
                fieldType = Nullable.GetUnderlyingType(e.PropertyType) ?? e.PropertyType;

                // apply the format string provided
                if (fieldType == typeof(DateTime) && !string.IsNullOrEmpty(DateFormatString))
                    column.Binding.StringFormat = DateFormatString;

                bool includeColumn = includeFields.Any(c => string.Equals(c, column.Header.ToString().Replace(" ", ""), StringComparison.CurrentCultureIgnoreCase)) ||
                                     includeFields.Contains("*"); // * = include all fields, and is the default value

                if (!includeColumn)
                {
                    column.Visibility = Visibility.Collapsed;
                }
                if (includeColumn)
                {
                    column.Visibility = Visibility.Visible;
                }

                e.Column = column;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnAutoGeneratingColumn : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     The source of the Data grid items has been changed (refresh or on loading)
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            Debug.WriteLineIf(DebugMode, "OnItemsSourceChanged");

            base.OnItemsSourceChanged(oldValue, newValue);

            try
            {
                if (newValue == null) return;

                if (oldValue != null)
                {

                    includeFields = IncludeFields.Split(',').Select(p => p.Trim().Replace(" ", "")).ToList();
                    Sorted += OnSorted;

                    // reset current filter, !important
                    CurrentFilter = null;

                    // reset GlobalFilterList list
                    GlobalFilterList.Clear();

                    // free previous resource
                    CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());

                    // scroll to top on reload collection
                    var scrollViewer = GetTemplateChild("DG_ScrollViewer") as ScrollViewer;
                    scrollViewer?.ScrollToTop();
                }

                CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(ItemsSource);

                // set Filter, contribution : STEFAN HEIMEL
                if (CollectionViewSource.CanFilter) CollectionViewSource.Filter = Filter;

                ItemsSourceCount = Items.Count;
                ElapsedTime = new TimeSpan(0, 0, 0);
                OnPropertyChanged(nameof(ItemsSourceCount));
               
                // Calculate row header width
                if (ShowRowsCount)
                {
                    var txt = new TextBlock
                    {
                        Text = ItemsSourceCount.ToString(),
                        FontSize = FontSize,
                        FontFamily = FontFamily,
                        Padding = new Thickness(0, 0, 4, 0),
                        Margin = new Thickness(2.0)
                    };
                    txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    RowHeaderWidth =  Math.Max(Math.Ceiling(txt.DesiredSize.Width), RowHeaderWidth >=0 ? RowHeaderWidth:0);
                }
                else
                {
                    RowHeaderWidth = 0;
                }

                // get collection type
                if (ItemsSourceCount > 0)
                    // contribution : APFLKUACHA
                    collectionType = ItemsSource is ICollectionView collectionView
                        ? collectionView.SourceCollection?.GetType().GenericTypeArguments.FirstOrDefault()
                        : ItemsSource?.GetType().GenericTypeArguments.FirstOrDefault();

                // generating custom columns
                if (!AutoGenerateColumns && collectionType != null) GeneratingCustomsColumn();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnItemsSourceChanged : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Set the cursor to "Cursors.Wait" during a long sorting operation
        ///     https://stackoverflow.com/questions/8416961/how-can-i-be-notified-if-a-datagrid-column-is-sorted-and-not-sorting
        /// </summary>
        /// <param name="eventArgs"></param>
        protected override void OnSorting(DataGridSortingEventArgs eventArgs)
        {
            if (pending || (popup?.IsOpen ?? false)) return;

            Mouse.OverrideCursor = Cursors.Wait;
            base.OnSorting(eventArgs);
            Sorted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     Adding Rows count
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoadingRow(DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        #endregion Protected Methods

        #region Private Methods

        /// <summary>
        ///     Resets all the filtered items in the datagrid
        /// </summary>
        /// <param name="filterPreset">all the saved filters from a datagrid</param>
        private void OnFilterPresetChanged(List<FilterCommon> filterPreset)
        {
            RemoveAllFilterCommand(null, null);

            foreach (FilterCommon filter in filterPreset.ToList())
            {

                PropertyInfo fieldProperty = ItemsSource.Cast<object>()?
                                               .FirstOrDefault()?
                                               .GetType()
                                               .GetProperty(filter.FieldName);
                if (fieldProperty != null)
                    fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ?? fieldProperty.PropertyType;


                //set current filter
                CurrentFilter = new FilterCommon()
                {
                    FieldName = filter.FieldName,
                    FieldProperty = fieldProperty,
                    FieldType = fieldType,
                    FilteredItems = filter.FilteredItems,
                    IsFiltered = false,
                    Translate = Translate
                };

                //apply current filter
                ApplyFilterCommand(null, null);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        private void ResetFields()
        {
            OnInitialized(new EventArgs());
            if (AutoGenerateColumns)
            {
                if (includeFields != null)
                {
                    if (includeFields.Contains("*"))
                        includeFields = Columns.Select(c => c.Header.ToString()).ToList();

                    foreach(DataGridColumn column in Columns)
                    {
                        column.Visibility = Visibility.Collapsed;
                    }

                    foreach (var propertyName in includeFields)
                    {
                        var column = new DataGridTextColumn {  Header = propertyName, Binding = new Binding(propertyName) };
                        OnAutoGeneratingColumn(new DataGridAutoGeneratingColumnEventArgs(propertyName, column.GetType(), column));
                        column.Visibility = Visibility.Visible;
                    }

                    // Refresh the UI by updating the ItemsSource property
                    var itemsSource = ItemsSource;
                    ItemsSource = null;
                    ItemsSource = itemsSource;
                }
            }
            else
            {
                //re-initialize the custom columns
                GeneratingCustomsColumn();
            }
        }

        /// <summary>
        ///     Build the item tree
        /// </summary>
        /// <param name="dates"></param>
        /// <returns></returns>
        private List<FilterItemDate> BuildTree(IEnumerable<FilterItem> dates)
        {
            try
            {
                var tree = new List<FilterItemDate>
                {
                    new FilterItemDate
                    {
                       Label = Translate.All, Level = 0, Initialize = true, FieldType = fieldType
                    }
                };

                if (dates == null) return tree;

                // iterate over all items that are not null
                // INFO:
                // Initialize   : does not call the SetIsChecked method
                // IsChecked    : call the SetIsChecked method
                // (see the FilterItem class for more information)

                var dateTimes = dates.ToList();

                foreach (var y in dateTimes.Where(c => c.Level == 1)
                             .Select(filterItem => new
                             {
                                 DateTime.Parse(filterItem.Content.ToString()).Date,
                                 Item = filterItem
                             })
                             .GroupBy(g => g.Date.Year)
                             .Select(year => new FilterItemDate
                             {
                                 Level = 1,
                                 Content = year.Key,
                                 Label = year.FirstOrDefault()?.Date.ToString("yyyy", Translate.Culture),
                                 Initialize = true, // default state
                                 FieldType = fieldType,

                                 Children = year.GroupBy(date => date.Date.Month)
                                     .Select(month => new FilterItemDate
                                     {
                                         Level = 2,
                                         Content = month.Key,
                                         Label = month.FirstOrDefault()?.Date.ToString("MMMM", Translate.Culture),
                                         Initialize = true, // default state
                                         FieldType = fieldType,

                                         Children = month.GroupBy(date => date.Date.Day)
                                             .Select(day => new FilterItemDate
                                             {
                                                 Level = 3,
                                                 Content = day.Key,
                                                 Label = day.FirstOrDefault()?.Date.ToString("dd", Translate.Culture),
                                                 Initialize = true, // default state
                                                 FieldType = fieldType,

                                                 // filter Item linked to the day, it propagates the states changes
                                                 Item = day.FirstOrDefault()?.Item,

                                                 Children = new List<FilterItemDate>()
                                             }).ToList()
                                     }).ToList()
                             }))
                {
                    // set parent and IsChecked property if uncheck Previous items
                    y.Children.ForEach(m =>
                    {
                        m.Parent = y;

                        m.Children.ForEach(d =>
                        {
                            d.Parent = m;

                            // set the state of the "IsChecked" property based on the items already filtered (unchecked)
                            if (d.Item.IsChecked) return;

                            // call the SetIsChecked method of the FilterItemDate class
                            d.IsChecked = false;

                            // reset with new state (isChanged == false)
                            d.Initialize = d.IsChecked;
                        });
                        // reset with new state
                        m.Initialize = m.IsChecked;
                    });
                    // reset with new state
                    y.Initialize = y.IsChecked;
                    tree.Add(y);
                }
                // last empty item if exist in collection
                if (dateTimes.Any(d => d.Level == -1))
                {
                    var empty = dateTimes.FirstOrDefault(x => x.Level == -1);
                    if (empty != null)
                        tree.Add(
                            new FilterItemDate
                            {
                                Label = Translate.Empty, // translation
                                Content = null,
                                Level = -1,
                                FieldType = fieldType,
                                Initialize = empty.IsChecked,
                                Item = empty,
                                Children = new List<FilterItemDate>()
                            }
                        );
                }
                tree.First().Tree = tree;
                return tree;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterCommon.BuildTree : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Handle Mousedown, contribution : WORDIBOI
        /// </summary>
        private readonly MouseButtonEventHandler onMousedown = (o, eArgs) => { eArgs.Handled = true; };

        /// <summary>
        ///     Generate custom columns that can be filtered
        /// </summary>
        private void GeneratingCustomsColumn()
        {
            Debug.WriteLineIf(DebugMode, "GeneratingCustomColumn");

            try
            {
                // get the columns that can be filtered
                var columns = Columns
                    .Where(c => (c is DataGridTextColumn dtx && dtx.IsColumnFiltered)
                                || (c is DataGridTemplateColumn dtp && dtp.IsColumnFiltered)
                                || (c is DataGridCheckBoxColumn dcb && dcb.IsColumnFiltered)
                                )
                    .Select(c => c)
                    .ToList();

                // set header template
                foreach (var col in columns)
                {
                    var columnType = col.GetType();

                    bool includeColumn = includeFields.Any(c => string.Equals(c, col.Header.ToString().Replace(" ", ""), StringComparison.CurrentCultureIgnoreCase)) || 
                                         includeFields.Contains("*"); // * = include all fields, and is the default value
                    
                    if (!includeColumn)
                    {
                        col.Visibility = Visibility.Collapsed;
                        continue;
                    }
                    if(includeColumn)
                    {
                        col.Visibility = Visibility.Visible;
                    }

                    if (col.HeaderTemplate != null)
                    {
                        // reset filter Button
                        var button = VisualTreeHelpers.GetHeader(col, this)
                            ?.FindVisualChild<Button>("FilterButton");
                        if (button != null) FilterState.SetIsFiltered(button, CurrentFilter?.IsFiltered ?? false);
                    }
                    else
                    {
                        if (columnType == typeof(DataGridTextColumn))
                        {
                            var column = (DataGridTextColumn)col;

                            // template
                            column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");

                            fieldType = null;
                            var fieldProperty = collectionType.GetProperty(((Binding)column.Binding).Path.Path);

                            // get type or underlying type if nullable
                            if (fieldProperty != null)
                                fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ??
                                            fieldProperty.PropertyType;

                            // apply DateFormatString when StringFormat for column is not provided or empty
                            if (fieldType == typeof(DateTime) && !string.IsNullOrEmpty(DateFormatString))
                                if (string.IsNullOrEmpty(column.Binding.StringFormat))
                                    column.Binding.StringFormat = DateFormatString;

                            // culture
                            if (((Binding)column.Binding).ConverterCulture == null)
                                ((Binding)column.Binding).ConverterCulture = Translate.Culture;

                            column.FieldName = ((Binding)column.Binding).Path.Path;
                        }

                        if (columnType == typeof(DataGridTemplateColumn))
                        {
                            // DataGridTemplateColumn has no culture property
                            var column = (DataGridTemplateColumn)col;

                            // template
                            column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");
                        }

                        if (columnType == typeof(DataGridCheckBoxColumn))
                        {
                            // DataGridCheckBoxColumn has no culture property
                            var column = (DataGridCheckBoxColumn)col;

                            column.FieldName = ((Binding)column.Binding).Path.Path;

                            if (((Binding)column.Binding).ConverterCulture == null)
                                ((Binding)column.Binding).ConverterCulture = Translate.Culture;

                            // template
                            column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.GeneratingCustomColumn : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Reset the cursor at the end of the sort
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSorted(object sender, EventArgs e)
        {
            ResetCursor();
        }

        /// <summary>
        ///     Reset cursor
        /// </summary>
        private async void ResetCursor()
        {
            // reset cursor
            await Dispatcher.BeginInvoke((Action)(() => { Mouse.OverrideCursor = null; }),
                DispatcherPriority.ContextIdle);
        }

        /// <summary>
        ///     Can Apply filter (popup Ok button)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanApplyFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            // CanExecute only when the popup is open
            if ((popup?.IsOpen ?? false) == false)
            {
                e.CanExecute = false;
            }
            else
            {
                if (search)
                    e.CanExecute = PopupViewItems.Any(f => f?.IsChecked == true);
                else
                    e.CanExecute = PopupViewItems.Any(f => f.IsChanged) &&
                                   PopupViewItems.Any(f => f?.IsChecked == true);
            }
        }

        /// <summary>
        ///     Cancel button, close popup
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (popup == null) return;
            popup.IsOpen = false; // raise EventArgs PopupClosed
        }

        /// <summary>
        /// Can remove all filter when GlobalFilterList.Count > 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanRemoveAllFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = GlobalFilterList.Count > 0;
        }

        /// <summary>
        ///     Can remove filter when current column (CurrentFilter) filtered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanRemoveFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CurrentFilter?.IsFiltered ?? false;
        }

        /// <summary>
        ///     Can show filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanShowFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CollectionViewSource?.CanFilter == true && (!popup?.IsOpen ?? true) && !pending;
        }

        /// <summary>
        ///     Check/uncheck all item when the action is (select all)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckedAllCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (FilterItem)e.Parameter;

            // only when the item[0] (select all) is checked or unchecked
            if (item?.Level != 0 || ItemCollectionView == null) return;

            foreach (var obj in PopupViewItems.ToList()
                         .Where(f => f.IsChecked != item.IsChecked))
                obj.IsChecked = item.IsChecked;
        }

        /// <summary>
        ///     Clear Search Box text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="routedEventArgs"></param>
        private void ClearSearchBoxClick(object sender, RoutedEventArgs routedEventArgs)
        {
            search = false;
            searchTextBox.Text = string.Empty; // raises TextChangedEventArgs
        }

        /// <summary>
        ///     Aggregate list of predicate as filter
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private bool Filter(object o)
        {
            var criteria = GlobalFilterList.SelectMany(f => f.Criteria);
            return criteria.Aggregate(true, (prevValue, predicate) => prevValue && predicate(o));
        }

        /// <summary>
        ///     OnPropertyChange
        /// </summary>
        /// <param name="propertyName"></param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        ///     On Resize Thumb Drag Completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            Cursor = cursor;
        }

        /// <summary>
        ///     Get delta on drag thumb
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            // initialize the first Actual size Width/Height
            if (sizableContentHeight <= 0)
            {
                sizableContentHeight = sizableContentGrid.ActualHeight;
                sizableContentWidth = sizableContentGrid.ActualWidth;
            }

            var yAdjust = sizableContentGrid.Height + e.VerticalChange;
            var xAdjust = sizableContentGrid.Width + e.HorizontalChange;

            //make sure not to resize to negative width or height
            xAdjust = sizableContentGrid.ActualWidth + xAdjust > minWidth ? xAdjust : minWidth;
            yAdjust = sizableContentGrid.ActualHeight + yAdjust > minHeight ? yAdjust : minHeight;

            xAdjust = xAdjust < minWidth ? minWidth : xAdjust;
            yAdjust = yAdjust < minHeight ? minHeight : yAdjust;

            // set size of grid
            sizableContentGrid.Width = xAdjust;
            sizableContentGrid.Height = yAdjust;
        }

        /// <summary>
        ///     On Resize Thumb DragStarted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragStarted(object sender, DragStartedEventArgs e)
        {
            cursor = Cursor;
            Cursor = Cursors.SizeNWSE;
        }

        /// <summary>
        ///     Reset the size of popup to original size
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PopupClosed(object sender, EventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "PopupClosed");

            var pop = (Popup)sender;

            // free the resources if the popup is closed without filtering
            if (!pending)
            {
                // clear resources
                ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());
                CurrentFilter = null;
               // ReactivateSorting();
                ResetCursor();
            }

            // unsubscribe from event and re-enable datagrid
            pop.Closed -= PopupClosed;
            pop.MouseDown -= onMousedown;
            searchTextBox.TextChanged -= SearchTextBoxOnTextChanged;
            thumb.DragCompleted -= OnResizeThumbDragCompleted;
            thumb.DragDelta -= OnResizeThumbDragDelta;
            thumb.DragStarted -= OnResizeThumbDragStarted;

            sizableContentGrid.Width = sizableContentWidth;
            sizableContentGrid.Height = sizableContentHeight;
            Cursor = cursor;

            ListBoxItems = new List<FilterItem>();
            TreeViewItems = new List<FilterItemDate>();

            searchText = string.Empty;
            search = false;

            // re-enable columnHeadersPresenter
            if (columnHeadersPresenter != null)
                columnHeadersPresenter.IsEnabled = true;
        }


        /// <summary>
        /// Remove All Filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveAllFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {

            //remove all existing filters and clean up the cache
            try
            {
                foreach (var col in Columns)
                {

                    // ReSharper disable MergeIntoPattern

                    // .NET Framework all C# 7.3
                    // 'recursive patterns' is not available on .NET Framework 4.8. Only in version C# 8.0 or greater.

                    switch (col)
                    {
                        case DataGridTextColumn column:
                            fieldName = column.FieldName;
                            break;

                        case DataGridTemplateColumn column:
                            fieldName = column.FieldName;
                            break;

                        case DataGridCheckBoxColumn column:
                            fieldName = column.FieldName;
                            break;

                        case null:
                            continue;
                    }

                    if (string.IsNullOrEmpty(fieldName)) continue;

                    CurrentFilter = GlobalFilterList.FirstOrDefault(c => c.FieldName == fieldName);
                    if (CurrentFilter != null) RemoveCurrentFilter();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLineIf(DebugMode, $"FilterDataGrid.RemoveAllFilterCommand error : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Remove current filter
        /// </summary>
        private void RemoveCurrentFilter()
        {
            Debug.WriteLineIf(DebugMode, "RemoveCurrentFilter");

            if (CurrentFilter == null) return;

            if(popup != null) popup.IsOpen = false; // raise PopupClosed event

            // set button icon (filtered or not)
            var col = Columns.FirstOrDefault(c => (c is DataGridTextColumn dtx && dtx.IsColumnFiltered && dtx.FieldName == CurrentFilter?.FieldName)
                                || (c is DataGridTemplateColumn dtp && dtp.IsColumnFiltered && dtp.FieldName == CurrentFilter?.FieldName)
                                || (c is DataGridCheckBoxColumn dcb && dcb.IsColumnFiltered && dcb.FieldName == CurrentFilter?.FieldName)
                                );
            if (col == null) return;
            Button button = VisualTreeHelpers.GetHeader(col, this)
                ?.FindVisualChild<Button>("FilterButton");
            FilterState.SetIsFiltered(button, false);

            ElapsedTime = new TimeSpan(0, 0, 0);
            stopWatchFilter = Stopwatch.StartNew();

            Mouse.OverrideCursor = Cursors.Wait;

            if (CurrentFilter.IsFiltered && CurrentFilter.RemoveFilter())
                CollectionViewSource.Refresh();

            if (GlobalFilterList.Contains(CurrentFilter))
                _ = GlobalFilterList.Remove(CurrentFilter);

            // set the last filter applied
            lastFilter = GlobalFilterList.LastOrDefault()?.FieldName;

            CurrentFilter.IsFiltered = false;
            CurrentFilter = null;
            ResetCursor();

            stopWatchFilter.Stop();
            ElapsedTime = stopWatchFilter.Elapsed;
        }

        /// <summary>
        ///     remove current filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            RemoveCurrentFilter();
        }

        /// <summary>
        ///     Filter current list in popup
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private bool SearchFilter(object obj)
        {
            var item = (FilterItem)obj;
            if (string.IsNullOrEmpty(searchText) || item == null || item.Level == 0) return true;

            var content = Convert.ToString(item.Content, Translate.Culture);

            // Contains
            if (!StartsWith)
                return Translate.Culture.CompareInfo.IndexOf(content ?? string.Empty, searchText, CompareOptions.OrdinalIgnoreCase) >= 0;

            // StartsWith preserve RangeOverflow
            if (searchLength > item.ContentLength) return false;

            return Translate.Culture.CompareInfo.IndexOf(content ?? string.Empty, searchText, 0, searchLength, CompareOptions.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        ///     Search TextBox Text Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;

            // fix TextChanged event fires twice I did not find another solution
            if (textBox == null || textBox.Text == searchText) return;

            searchText = textBox.Text;
            searchLength = searchText.Length;
            search = !string.IsNullOrEmpty(searchText);

            // apply filter
            ItemCollectionView.Refresh();

            if (CurrentFilter.FieldType == typeof(DateTime))
            {
                if (string.IsNullOrEmpty(searchText))
                {
                    // fill the tree with the elements of the list of the original items
                    TreeViewItems = BuildTree(SourcePopupViewItems);
                }
                else
                {
                    // fill the tree only with the items found by the search
                    var items = PopupViewItems.Where(i => i.IsChecked).ToList();
                    // if at least one item is not null, fill in the tree structure otherwise the tree structure contains only the item (select all).
                    TreeViewItems = BuildTree(items.Any() ? items : null);
                }
            }
            else
            {
                // rebuild listboxitems
                if (string.IsNullOrEmpty(searchText))
                {
                    ListBoxItems = SourcePopupViewItems.ToList();
                }
                else
                {
                    ListBoxItems = PopupViewItems.Where(i => i.IsChecked).ToList();
                }
            }
        }

        /// <summary>
        ///     Open a pop-up window, Click on the header button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ShowFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "\r\nShowFilterCommand");

            // reset previous elapsed time
            ElapsedTime = new TimeSpan(0, 0, 0);
            stopWatchFilter = Stopwatch.StartNew();

            // clear search text (!important)
            searchText = string.Empty;
            search = false;

            try
            {
                // filter button
                Button button = (Button)e.OriginalSource;
                if (button == null) return;

                // contribution : OTTOSSON
                // for the moment this functionality is not tested, I do not know if it can cause unexpected effects
                _ = CommitEdit(DataGridEditingUnit.Row, true);

                // navigate up to the current header and get column type
                var header = VisualTreeHelpers.FindAncestor<DataGridColumnHeader>(button);
                var columnType = header.Column.GetType();

                // then down to the current popup
                popup = VisualTreeHelpers.FindChild<Popup>(header, "FilterPopup");
                columnHeadersPresenter = VisualTreeHelpers.FindAncestor<DataGridColumnHeadersPresenter>(header);

                if (popup == null || columnHeadersPresenter == null) return;

                // disable columnHeadersPresenter while popup is open
                if (columnHeadersPresenter != null)
                    columnHeadersPresenter.IsEnabled = false;

                // popup handle event
                popup.Closed += PopupClosed;

                // disable popup background click-through, contribution : WORDIBOI
                popup.MouseDown += onMousedown;

                // resizable grid
                sizableContentGrid = VisualTreeHelpers.FindChild<Grid>(popup.Child, "SizableContentGrid");

                // search textbox
                searchTextBox = VisualTreeHelpers.FindChild<TextBox>(popup.Child, "SearchBox");
                searchTextBox.Text = string.Empty;
                searchTextBox.TextChanged += SearchTextBoxOnTextChanged;
                searchTextBox.Focusable = true;

                // thumb resize grip
                thumb = VisualTreeHelpers.FindChild<Thumb>(sizableContentGrid, "PopupThumb");

                // minimum size of Grid
                sizableContentHeight = 0;
                sizableContentWidth = 0;

                sizableContentGrid.Height = popUpSize.Y;
                sizableContentGrid.MinHeight = popUpSize.Y;

                minHeight = sizableContentGrid.MinHeight;
                minWidth = sizableContentGrid.MinWidth;

                // thumb handle event
                thumb.DragCompleted += OnResizeThumbDragCompleted;
                thumb.DragDelta += OnResizeThumbDragDelta;
                thumb.DragStarted += OnResizeThumbDragStarted;

                // get field name from binding Path
                if (columnType == typeof(DataGridTextColumn))
                {
                    var column = (DataGridTextColumn)header.Column;
                    fieldName = column.FieldName;
                    //column.CanUserSort = false;
                    //currentColumn = column;
                }

                if (columnType == typeof(DataGridTemplateColumn))
                {
                    var column = (DataGridTemplateColumn)header.Column;
                    fieldName = column.FieldName;
                    //column.CanUserSort = false;
                    //currentColumn = column;
                }

                if (columnType == typeof(DataGridCheckBoxColumn))
                {
                    var column = (DataGridCheckBoxColumn)header.Column;
                    fieldName = column.FieldName;
                    //column.CanUserSort = false;
                    //currentColumn = column;
                }

                // invalid fieldName
                if (string.IsNullOrEmpty(fieldName)) return;
                
                // get type of field
                fieldType = null;
                var fieldProperty = Extensions.GetPropertyInfo(collectionType, fieldName);

                // get type or underlying type if nullable
                if (fieldProperty != null)
                    FieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ?? fieldProperty.PropertyType;

                if (CurrentFilter is null)
                {
                    // If no filter, add filter to GlobalFilterList list
                    CurrentFilter = GlobalFilterList.FirstOrDefault(f => f.FieldName == fieldName) ??
                                    new FilterCommon
                                    {
                                        FieldName = fieldName,
                                        FieldType = fieldType,
                                        Translate = Translate,
                                        FieldProperty = fieldProperty
                                    };
                }

                // set cursor
                Mouse.OverrideCursor = Cursors.Wait;
                
                await Task.Run(() =>
                {
                    // gather all values that are in the column of a certain fieldName
                    List<object> sourceObjectList = null;
                    
                    if (fieldType == typeof(DateTime))
                    {
                        sourceObjectList = ItemsSource.Cast<object>()
                                .Select(x => (object)FilterHelper.GetPropertyValue<DateTime?>(x, fieldName)?.Date)
                                .Where(x => x != null)
                                .Distinct()
                                .ToList();
                    }
                    else
                    {
                        sourceObjectList = ItemsSource.Cast<object>()
                                .Select(x => fieldProperty?.GetValue(x, null))
                                .Where(x => x != null)
                                .Distinct()
                                .ToList();

                        if (fieldType == typeof(string))
                            //remove empty strings
                            sourceObjectList = sourceObjectList
                                .Where(s => !string.IsNullOrEmpty(s.ToString()))
                                .ToList();
                    }

                    // sorting is a very slow operation, using ParallelQuery
                    sourceObjectList = sourceObjectList.AsParallel().OrderBy(x => x).ToList();

                    //convert the sourceObjectList to a filterItem that is used in the popup
                    List<FilterItem> filterItemList = new List<FilterItem>();
                    if (fieldType == typeof(bool))
                    {
                        filterItemList = new List<FilterItem>(sourceObjectList.Count + 1);
                    }
                    else
                    {
                        // add the first element (select all) at the top of list
                        filterItemList = new List<FilterItem>(sourceObjectList.Count + 2)
                        {
                            new FilterItem { Label = Translate.All, IsChecked = true, Level = 0 }
                        };
                    }

                    // add all items (not null) to the filterItemList,
                    // the list of dates is calculated by BuildTree from this list
                    filterItemList.AddRange(sourceObjectList.Select(item => 
                        new FilterItem
                        {
                            Content = item,
                            ContentLength = item?.ToString()?.Length ?? 0,
                            FieldType = fieldType,
                            Label = item,
                            Level = 1,
                            Initialize = CurrentFilter.FilteredItems.Count <= 0 ? true : CurrentFilter.FilteredItems?.Contains(item) == true
                        }
                    ));

                    if (fieldType == typeof(bool))
                        filterItemList.ToList().ForEach(c =>
                        {
                            c.Label = bool.Parse(c.Content.ToString()) ? Translate.IsTrue : Translate.IsFalse;
                        });
                    else
                    {
                        filterItemList.Add(new FilterItem
                        {
                            FieldType = fieldType,
                            Content = null,
                            Label = Translate.Empty,
                            Level = -1,
                            Initialize = ItemsSource.Cast<object>()
                                                    .Select(x => fieldProperty?.GetValue(x, null))
                                                    .Contains(null)
                        });
                    }

                    // ItemsSource (ListBow/TreeView)
                    if (fieldType == typeof(DateTime))
                        TreeViewItems = BuildTree(filterItemList);
                    else
                        ListBoxItems = filterItemList;

                    // Set ICollectionView for filtering in the pop-up window
                    ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(filterItemList);

                    // set filter in popup
                    if (ItemCollectionView.CanFilter)
                        ItemCollectionView.Filter = SearchFilter;

                });

                // set the placement and offset of the PopUp in relation to the header and the main window of the application
                // i.e (placement : bottom left or bottom right)
                PopupPlacement(sizableContentGrid, header);

                popup.UpdateLayout();

                // open popup
                popup.IsOpen = true;

                // set focus on searchTextBox
                searchTextBox.Focus();
                Keyboard.Focus(searchTextBox);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.ShowFilterCommand error : {ex.Message}");
                throw;
            }
            finally
            {
                // reset cursor
                ResetCursor();

                stopWatchFilter.Stop();

                // show open popup elapsed time in UI
                ElapsedTime = stopWatchFilter.Elapsed;

                Debug.WriteLineIf(DebugMode, $"FilterDataGrid.ShowFilterCommand Elapsed time : {ElapsedTime:mm\\:ss\\.ff}");
            }
        }

        /// <summary>
        ///     Click OK Button when Popup is Open, apply filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ApplyFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "\r\nApplyFilterCommand");

            stopWatchFilter.Start();

            pending = true;
            if(popup != null) popup.IsOpen = false; // raise PopupClosed event if popup was opened

            // set cursor wait
            Mouse.OverrideCursor = Cursors.Wait;

            lock(CurrentFilter)
            {
                try
                {
                    Task.Run(() =>
                    {
                        bool frontendFilterChanged = PopupViewItems.Any(c => c.IsChanged);
                        if (frontendFilterChanged || search)
                        {
                            CurrentFilter.FilteredItems = PopupViewItems.Where(c => c.IsChecked)
                                                                        .Select(c => c.Content)
                                                                        .ToHashSet();
                        }

                        // add a filter if it is not already added previously
                        if (!CurrentFilter.IsFiltered) CurrentFilter.AddCriteria();

                        // add current filter to GlobalFilterList
                        if (GlobalFilterList.All(f => f.FieldName != CurrentFilter.FieldName))
                            GlobalFilterList.Add(CurrentFilter);

                        // set the current field name as the last filter name
                        lastFilter = CurrentFilter.FieldName;
                    }).Wait();

                    // apply filter
                    CollectionViewSource.Refresh();

                    // remove the current filter if there is no items to filter
                    if (!CurrentFilter.FilteredItems.Any())
                        RemoveCurrentFilter();

                    var col = Columns.FirstOrDefault(c => (c is DataGridTextColumn dtx && dtx.IsColumnFiltered && dtx.FieldName == CurrentFilter?.FieldName)
                                || (c is DataGridTemplateColumn dtp && dtp.IsColumnFiltered && dtp.FieldName == CurrentFilter?.FieldName)
                                || (c is DataGridCheckBoxColumn dcb && dcb.IsColumnFiltered && dcb.FieldName == CurrentFilter?.FieldName)
                                );
                    if (col == null) return;
                    Button button = VisualTreeHelpers.GetHeader(col, this)
                        ?.FindVisualChild<Button>("FilterButton");
                    FilterState.SetIsFiltered(button, CurrentFilter?.IsFiltered ?? false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FilterDataGrid.ApplyFilterCommand error : {ex.Message}");
                    throw;
                }
                finally
                {
                    //ReactivateSorting();
                    ResetCursor();
                    ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());
                    pending = false;
                    CurrentFilter = null;

                    stopWatchFilter.Stop();
                    ElapsedTime = stopWatchFilter.Elapsed;

                    Debug.WriteLineIf(DebugMode, $"FilterDataGrid.ApplyFilterCommand Elapsed time : {ElapsedTime:mm\\:ss\\.ff}");
                }
            }
        }

        /// <summary>
        ///     PopUp placement and offset
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="header"></param>
        private void PopupPlacement(FrameworkElement grid, FrameworkElement header)
        {
            try
            {
                popup.PlacementTarget = header;
                popup.HorizontalOffset = 0d;
                popup.VerticalOffset = -1d;
                popup.Placement = PlacementMode.Bottom;

                // get the host window of the datagrid, contribution : STEFAN HEIMEL
                var hostingWindow = Window.GetWindow(this);

                if (hostingWindow != null)
                {
                    // greater than or equal to 0.0
                    double MaxSize(double size) => (size >= 0.0d) ? size : 0.0d;

                    const double border = 1d;

                    // get the ContentPresenter from the hostingWindow
                    var contentPresenter = VisualTreeHelpers.FindChild<ContentPresenter>(hostingWindow);

                    var hostSize = new Point
                    {
                        X = contentPresenter.ActualWidth,
                        Y = contentPresenter.ActualHeight
                    };

                    // get the X, Y position of the header
                    var headerContentOrigin = header.TransformToVisual(contentPresenter).Transform(new Point(0, 0));
                    var headerDataGridOrigin = header.TransformToVisual(this).Transform(new Point(0, 0));

                    var headerSize = new Point { X = header.ActualWidth, Y = header.ActualHeight };
                    var offset = popUpSize.X - headerSize.X + border;

                    // the popup must stay in the DataGrid, move it to the left of the header, because it overflows on the right.
                    if (headerDataGridOrigin.X + headerSize.X > popUpSize.X) popup.HorizontalOffset -= offset;

                    // delta for max size popup
                    var delta = new Point
                    {
                        X = hostSize.X - (headerContentOrigin.X + headerSize.X),
                        Y = hostSize.Y - (headerContentOrigin.Y + headerSize.Y + popUpSize.Y)
                    };

                    // max size
                    grid.MaxWidth = MaxSize(popUpSize.X + delta.X - border);
                    grid.MaxHeight = MaxSize(popUpSize.Y + delta.Y - border);


                    // remove offset
                    // contributing to the fix : VASHBALDEUS
                    if (popup.HorizontalOffset == 0)
                        grid.MaxWidth = MaxSize(Math.Abs(grid.MaxWidth - offset));
                    
                    // the height of popup is too large, reduce it, because it overflows down.
                    if (delta.Y <= 0d)
                    {
                        grid.MaxHeight = MaxSize(popUpSize.Y - Math.Abs(delta.Y) - border);
                        grid.Height = grid.MaxHeight;

                        // contributing to the fix : VASHBALDEUS
                        grid.MinHeight = grid.MaxHeight == 0 ? grid.MinHeight : grid.MaxHeight; 
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.PopupPlacement error : {ex.Message}");
                throw;
            }
        }

        #endregion Private Methods
    }
}