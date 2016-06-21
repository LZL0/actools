﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace AcManager.Controls.Pages.Dialogs {
    public partial class ImageViewer { 
        public ImageViewer(ImageSource imageSource) : this(new[] { imageSource }) { }

        public ImageViewer(string image) : this(new[] { image }) { }

        public ImageViewer(IEnumerable<object> images, int position = 0, bool oneTrueDpi = true, int maxWidth = -1, int maxHeight = -1) {
            var model = new ImageViewerViewModel(images, position);
            DataContext = model;
            InitializeComponent();
            Buttons = new Button[] { };

            if (maxWidth > 0) {
                model.MaxImageWidth = maxWidth;
                model.MaxImageHeight = maxHeight;
            }

            Image.SetBinding(Image.SourceProperty, new Binding {
                Source = model,
                Path = new PropertyPath(nameof(ImageViewerViewModel.CurrentImage)),
                Mode = BindingMode.OneWay,
                Converter = new UriToCachedImageConverter(),
                ConverterParameter = oneTrueDpi ? "oneTrueDpi" : (maxWidth > 0 ? $"{maxWidth}×" : null)
            });
        }

        private void ImageViewer_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                Close();
            }
        }

        private void ImageViewer_OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                Close();
            } else if (e.Key == Key.Left || e.Key == Key.K) {
                Model.CurrentPosition--;
            } else if (e.Key == Key.Right || e.Key == Key.J) {
                Model.CurrentPosition++;
            } else if (e.Key == Key.Enter) {
                IsSelected = true;
                Close();
            }
        }

        public bool IsSelected;

        public int? ShowDialogInSelectMode() {
            Model.SelectionMode = true;
            ShowDialog();
            return IsSelected ? Model.CurrentPosition : (int?)null;
        }

        [CanBeNull]
        public string ShowDialogInSelectFileMode() {
            Model.SelectionMode = true;
            ShowDialog();
            return IsSelected ? Model.CurrentImage as string : null;
        }

        private void ApplyButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            IsSelected = true;
            Close();
        }

        private void CloseButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            Close();
        }

        private void ImageViewer_OnLoaded(object sender, RoutedEventArgs e) {
            if (double.IsInfinity(Model.MaxImageHeight)) {
                Model.MaxImageHeight = Wrapper.Height;
            }

            if (double.IsInfinity(Model.MaxImageWidth)) {
                Model.MaxImageWidth = Wrapper.Width;
            }
        }

        public ImageViewerViewModel Model => (ImageViewerViewModel)DataContext;

        public class ImageViewerViewModel : NotifyPropertyChanged {
            /* TODO: cache & preload for better UX? */

            private readonly IReadOnlyList<object> _images;

            private int _currentPosition;

            public int CurrentPosition {
                get { return _currentPosition; }
                set {
                    if (Equals(value, _currentPosition) || value < 0 || value >= _images.Count) return;
                    var oldPosition = _currentPosition;
                    _currentPosition = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentImage));

                    if (oldPosition == 0 || value == 0) {
                        _previousCommand?.OnCanExecuteChanged();
                    }

                    var last = _images.Count - 1;
                    if (oldPosition == last || value == last) {
                        _nextCommand?.OnCanExecuteChanged();
                    }
                }
            }

            private bool _saveable;

            public bool Saveable {
                get { return _saveable; }
                set {
                    if (Equals(value, _saveable)) return;
                    _saveable = value;
                    OnPropertyChanged();
                }
            }

            private string _saveDirectory;

            public string SaveDirectory {
                get { return _saveDirectory; }
                set {
                    if (Equals(value, _saveDirectory)) return;
                    _saveDirectory = value;
                    OnPropertyChanged();
                }
            }

            private double _maxImageWidth = double.MaxValue;

            public double MaxImageWidth {
                get { return _maxImageWidth; }
                set {
                    if (value.Equals(_maxImageWidth)) return;
                    _maxImageWidth = value;
                    OnPropertyChanged();
                }
            }

            private double _maxImageHeight = double.MaxValue;

            public double MaxImageHeight {
                get { return _maxImageHeight; }
                set {
                    if (value.Equals(_maxImageHeight)) return;
                    _maxImageHeight = value;
                    OnPropertyChanged();
                }
            }

            public object CurrentImage => _images[_currentPosition];

            public string CurrentImageName => Path.GetFileName(CurrentImage as string ?? "Image");

            private bool _selectionMode;

            public bool SelectionMode {
                get { return _selectionMode; }
                set {
                    if (Equals(value, _selectionMode)) return;
                    _selectionMode = value;
                    OnPropertyChanged();
                }
            }

            public ImageViewerViewModel(IEnumerable<object> images, int position) {
                _images = images.ToList();
                CurrentPosition = position;
            }

            private RelayCommand _previousCommand;

            public RelayCommand PreviousCommand => _previousCommand ?? (_previousCommand = new RelayCommand(o => {
                CurrentPosition--;
            }, o => CurrentPosition > 0));

            private RelayCommand _nextCommand;

            public RelayCommand NextCommand => _nextCommand ?? (_nextCommand = new RelayCommand(o => {
                CurrentPosition++;
            }, o => CurrentPosition < _images.Count - 1));

            private AsyncCommand _saveCommand;

            public AsyncCommand SaveCommand => _saveCommand ?? (_saveCommand = new AsyncCommand(async o => {
                var origin = CurrentImage as string;
                if (origin == null) {
                    throw new NotSupportedException();
                }

                var dialog = new SaveFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = "Export Texture Mapping",
                    DefaultExt = Path.GetExtension(origin)
                };

                if (SaveDirectory != null) {
                    dialog.InitialDirectory = SaveDirectory;
                }

                if (dialog.ShowDialog() != true) return;

                try {
                    await Task.Run(() => File.Copy(origin, dialog.FileName));
                } catch (Exception ex) {
                    NonfatalError.Notify("Can’t export texture", ex);
                }
            }, o => CurrentImage is string));
        }
    }
}
