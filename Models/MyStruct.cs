using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinFinder {
    public class MyStruct : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        private bool _thumbLoaded;
        public bool ThumbLoaded {
            get => _thumbLoaded;
            set {
                if (_thumbLoaded != value) {
                    _thumbLoaded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbLoaded)));
                }
            }
        }

        private string _x0;
        private string _x1;
        private string _x2;
        private string[] _x3;
        private long _x4;

        public string X0 {
            get => _x0;
            set {
                _x0 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X0)));
            }
        }

        public string X1 {
            get => _x1;
            set {
                _x1 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X1)));
            }
        }

        public string X2 {
            get => _x2;
            set {
                _x2 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X2)));
            }
        }

        public string[] X3 {
            get => _x3;
            set {
                _x3 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X3)));
            }
        }

        public long X4 {
            get => _x4;
            set {
                _x4 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X4)));
            }
        }

        private BitmapSource _s0;
        public BitmapSource S0 {
            get => _s0;
            set {
                _s0 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(S0)));
            }
        }
    }
}
