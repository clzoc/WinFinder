using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace WinFinder {
    public partial class MainWindow {
        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) {
            if (!(sender is ListView view)) {
                return;
            }
            if (e.OriginalSource is GridViewColumnHeader header) {
                GridViewColumn clickedColumn = header.Column;
                TextBlock bt = header.Content as TextBlock;
                if (clickedColumn != null) {
                    TextBlock dt;
                    if (clickedColumn.CellTemplate.LoadContent().ToString() == "System.Windows.Controls.StackPanel") {
                        StackPanel sk = clickedColumn.CellTemplate.LoadContent() as StackPanel;
                        dt = sk.Children[1] as TextBlock;
                    } else if (clickedColumn.CellTemplate.LoadContent().ToString() == "System.Windows.Controls.Grid") {
                        Grid sk = clickedColumn.CellTemplate.LoadContent() as Grid;
                        dt = sk.Children[0] as TextBlock;
                    } else {
                        dt = clickedColumn.CellTemplate.LoadContent() as TextBlock;
                    }
                    System.Windows.Data.Binding myBinding;
                    if (bt.Text == "大小") {
                        myBinding = BindingOperations.GetBinding(dt, TagProperty);
                    } else {
                        myBinding = BindingOperations.GetBinding(dt, TextBlock.TextProperty);
                    }
                    string bindingProperty = myBinding?.Path.Path;
                    if (bindingProperty == null) {
                        bindingProperty = header.Tag.ToString();
                        if (string.IsNullOrEmpty(bindingProperty)) {
                            return;
                        }
                    }

                    SortDescriptionCollection sdc = view.Items.SortDescriptions;

                    ListSortDirection sortDirection = ListSortDirection.Descending;
                    foreach (var sd in sdc) {
                        if (sd.PropertyName.Equals(bindingProperty)) {
                            sortDirection = (ListSortDirection)(((int)sd.Direction) ^ 1);
                            sdc.Remove(sd);
                            break;
                        }
                    }

                    sdc.Insert(0, new SortDescription(bindingProperty, sortDirection));

                    if (CollectionViewSource.GetDefaultView(view.ItemsSource) is ListCollectionView collectionView) {
                        collectionView.SortDescriptions.Clear();
                        collectionView.CustomSort = NaturalStringComparer.CreateComparerForProperty(bindingProperty, sortDirection);

                        if (timer.IsEnabled) {
                            timer.Stop();
                        }
                        timer.Start();
                    }
                }
            }
        }

        private void ApplyNaturalSort(string property, ListSortDirection direction) {
            if (CollectionViewSource.GetDefaultView(ListInfo) is ListCollectionView lcv) {
                lcv.CustomSort = NaturalStringComparer.CreateComparerForProperty(property, direction);
            }
        }

        private void ViewListView_Click(object sender, RoutedEventArgs e) {
            GridViewContainer.Visibility = Visibility.Collapsed;
            FILEINFOMATION.Visibility = Visibility.Visible;
            headerline.Visibility = Visibility.Visible;

            EnsureSelectedItemsVisible(FILEINFOMATION);
        }

        private void ViewGridView_Click(object sender, RoutedEventArgs e) {
            FILEINFOMATION.Visibility = Visibility.Collapsed;
            GridViewContainer.Visibility = Visibility.Visible;
            headerline.Visibility = Visibility.Collapsed;

            EnsureSelectedItemsVisible(GridViewContainer);
        }

        private void EnsureSelectedItemsVisible(System.Windows.Controls.ListView listView) {
            if (listView.Items.Count == 0) return;

            var selectedItem = ListInfo.FirstOrDefault(item => item.IsSelected);

            if (selectedItem != null) {
                try {
                    listView.ScrollIntoView(selectedItem);
                    if (listView.ItemContainerGenerator.ContainerFromItem(selectedItem) is System.Windows.Controls.ListViewItem item) {
                        item.BringIntoView();
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"滚动到选中项错误: {ex.Message}");
                }
            } else {
                ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(listView);
                scrollViewer?.ScrollToTop();
            }
        }

        private static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T result) {
                    return result;
                }

                T childItem = FindVisualChild<T>(child);
                if (childItem != null) return childItem;
            }
            return null;
        }

        private static T GetVisualChild<T>(DependencyObject parent) where T : Visual {
            T child = default(T);

            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++) {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null) {
                    child = GetVisualChild<T>(v);
                }
                if (child != null) {
                    break;
                }
            }
            return child;
        }
    }
}
