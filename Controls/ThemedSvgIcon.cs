using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SharpVectors.Converters;

namespace WinFinder.Controls {
    public class ThemedSvgIcon : Grid {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(Uri),
            typeof(ThemedSvgIcon),
            new PropertyMetadata(null, OnSourceChanged));

        public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
            nameof(IconBrush),
            typeof(Brush),
            typeof(ThemedSvgIcon),
            new PropertyMetadata(Brushes.Black, OnIconBrushChanged));

        private readonly Border fillHost;
        private readonly SvgViewbox maskViewbox;

        public ThemedSvgIcon() {
            fillHost = new Border();
            maskViewbox = new SvgViewbox { Stretch = Stretch.Uniform };
            fillHost.Background = IconBrush;
            fillHost.OpacityMask = new VisualBrush(maskViewbox) {
                AutoLayoutContent = true,
                Stretch = Stretch.Uniform,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            Children.Add(fillHost);
        }

        public Uri Source {
            get => (Uri)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public Brush IconBrush {
            get => (Brush)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ThemedSvgIcon icon) {
                icon.maskViewbox.Source = e.NewValue as Uri;
            }
        }

        private static void OnIconBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ThemedSvgIcon icon) {
                icon.fillHost.Background = e.NewValue as Brush ?? Brushes.Transparent;
            }
        }
    }
}