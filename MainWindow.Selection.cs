using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WinFinder {
    public partial class MainWindow {
        private Point selectionStart;
        private Point _selectionEndPoint;
        private bool isSelecting;
        private Grid selectionArea = new Grid();

        private DispatcherTimer _autoScrollTimer;
        private double _autoScrollSpeed;
        private Point _lastMousePosition;
        private const double ScrollEdgeThreshold = 30;
        private const double ScrollSpeedFactor = 5.0;

        private void ContentGrid_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) return;

            if (!(e.OriginalSource is FrameworkElement fe) || fe.DataContext == null || fe.DataContext is MyStruct) {
                selectionStart = e.GetPosition(ContentArea);
                _selectionEndPoint = selectionStart;
                isSelecting = true;

                selectionArea = new Grid {
                    Background = new SolidColorBrush(Color.FromArgb(127, 154, 186, 232)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                ContentArea.CaptureMouse();
                ContentArea.Children.Add(selectionArea);

                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) {
                    foreach (MyStruct item in ListInfo.Where(i => i.IsSelected)) {
                        item.IsSelected = false;
                    }
                }
            }
        }

        private void ContentGrid_MouseMove(object sender, MouseEventArgs e) {
            if (!isSelecting) return;

            Trace.WriteLine("multiple thin");

            _lastMousePosition = e.GetPosition(ContentArea);
            _selectionEndPoint = _lastMousePosition;

            UpdateSelectionBox();

            double scrollRef = 0;
            bool shouldScroll = false;
            if (_lastMousePosition.Y > ContentArea.ActualHeight - ScrollEdgeThreshold) {
                scrollRef = _lastMousePosition.Y - (ContentArea.ActualHeight - ScrollEdgeThreshold);
                shouldScroll = true;
            } else if (_lastMousePosition.Y < ScrollEdgeThreshold) {
                scrollRef = _lastMousePosition.Y - ScrollEdgeThreshold;
                shouldScroll = true;
            }

            _autoScrollSpeed = scrollRef * ScrollSpeedFactor;

            if (shouldScroll) {
                if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
            } else {
                if (_autoScrollTimer.IsEnabled) _autoScrollTimer.Stop();
            }

            UpdateSelectedItems();
        }

        private void UpdateSelectionBox() {
            double left = Math.Min(selectionStart.X, _selectionEndPoint.X);
            double top = Math.Min(selectionStart.Y, _selectionEndPoint.Y);
            double width = Math.Abs(selectionStart.X - _selectionEndPoint.X);
            double height = Math.Abs(selectionStart.Y - _selectionEndPoint.Y);

            selectionArea.Margin = new Thickness(left, top, 0, 0);
            selectionArea.Width = width;
            selectionArea.Height = height;

            if (selectionArea.ActualWidth > 0 && selectionArea.ActualHeight > 0) {
                selectionArea.Clip = Geometry.Parse(Window_Corner(selectionArea.ActualHeight, selectionArea.ActualWidth,
                    Math.Min(Math.Min(selectionArea.ActualHeight * 0.3, selectionArea.ActualWidth * 0.3), squircle_radius), 1));
            }
        }

        private void UpdateSelectedItems() {
            ListView activeListView = FILEINFOMATION.Visibility == Visibility.Visible ? FILEINFOMATION : GridViewContainer;
            Rect selectionRect = new Rect(selectionArea.Margin.Left, selectionArea.Margin.Top, selectionArea.Width, selectionArea.Height);

            foreach (MyStruct item in activeListView.ItemsSource) {
                var container = activeListView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;

                if (container != null) {
                    Point itemTopLeft = container.TranslatePoint(new Point(0, 0), ContentArea);
                    Rect itemRect = new Rect(itemTopLeft, new Size(container.ActualWidth, container.ActualHeight));

                    if (selectionRect.IntersectsWith(itemRect)) {
                        if (!item.IsSelected) item.IsSelected = true;
                    } else {
                        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) {
                            if (item.IsSelected) item.IsSelected = false;
                        }
                    }
                }
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject depObj) {
            if (depObj is ScrollViewer) return depObj as ScrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void AutoScrollTimer_Tick(object sender, EventArgs e) {
            if (!isSelecting) {
                _autoScrollTimer.Stop();
                return;
            }

            ListView activeListView = FILEINFOMATION.Visibility == Visibility.Visible ? FILEINFOMATION : GridViewContainer;
            ScrollViewer scrollViewer = GetScrollViewer(activeListView);
            if (scrollViewer == null) return;

            double scrollAmount = _autoScrollSpeed * (_autoScrollTimer.Interval.TotalSeconds);
            double newOffset = scrollViewer.VerticalOffset + scrollAmount;

            newOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, newOffset));
            double actualScrolledDistance = newOffset - scrollViewer.VerticalOffset;
            if (Math.Abs(actualScrolledDistance) < 1) return;

            scrollViewer.ScrollToVerticalOffset(newOffset);
            selectionStart.Y -= actualScrolledDistance;

            UpdateSelectionBox();
            UpdateSelectedItems();
        }

        private void ContentGrid_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!isSelecting || selectionArea == null) return;

            ContentArea.ReleaseMouseCapture();
            isSelecting = false;

            if (_autoScrollTimer.IsEnabled) {
                _autoScrollTimer.Stop();
            }

            ContentArea.Children.Remove(selectionArea);
        }

        private void ContentArea_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (!isSelecting) {
                return;
            }

            ListView activeListView = FILEINFOMATION.Visibility == Visibility.Visible ? FILEINFOMATION : GridViewContainer;
            ScrollViewer scrollViewer = GetScrollViewer(activeListView);
            if (scrollViewer == null) return;

            double newOffset = scrollViewer.VerticalOffset - e.Delta;
            newOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, newOffset));
            double actualScrolledDistance = newOffset - scrollViewer.VerticalOffset;
            if (Math.Abs(actualScrolledDistance) < 1) return;

            scrollViewer.ScrollToVerticalOffset(newOffset);

            selectionStart.Y -= actualScrolledDistance;
            UpdateSelectionBox();
            UpdateSelectedItems();

            e.Handled = true;
        }
    }
}
