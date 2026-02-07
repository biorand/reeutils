using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using IntelOrca.Biohazard.REE.Textures;

namespace RszViewer
{
    public partial class TextureViewer : UserControl
    {
        private string _filePath = "";
        private ReTextureFile? _texFile;
        private byte[]? _rawRgba; // Raw decompressed RGBA data (from BCn)
        private int _width;
        private int _height;

        private bool _showR = true, _showG = true, _showB = true, _showA = true;

        public TextureViewer()
        {
            InitializeComponent();
        }

        public void LoadTexture(string path)
        {
            _filePath = path;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                _texFile = new ReTextureFile();
                _texFile.Read(fs);

                TxtInfo.Text = $"{_texFile.Header.Width}x{_texFile.Header.Height} | Mips: {_texFile.Mips.Count} | Version: {_texFile.Version}";

                // Decompress
                fs.Seek(0, SeekOrigin.Begin); // Reset for data read
                _rawRgba = _texFile.GetDecompressedData(fs, out _width, out _height);
                
                UpdatePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load texture: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePreview()
        {
            if (_rawRgba == null) return;

            // We need to swap R/B for Bgra32 if input is Rgba
            // And apply channel masks
            
            byte[] displayData = new byte[_rawRgba.Length];
            
            for (int i = 0; i < _rawRgba.Length; i += 4)
            {
                 byte r = _rawRgba[i]; // R
                 byte g = _rawRgba[i+1]; // G
                 byte b = _rawRgba[i+2]; // B
                 byte a = _rawRgba[i+3]; // A

                 if (!_showR) r = 0;
                 if (!_showG) g = 0;
                 if (!_showB) b = 0;
                 if (!_showA) a = 255; // If Alpha is off, force full opacity? Or 0? Usually force opacity to see RGB clearly.
                 
                 // WPF Bgra32 expects B, G, R, A
                 displayData[i] = b;
                 displayData[i+1] = g;
                 displayData[i+2] = r;
                 displayData[i+3] = a;
            }

            var bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, _width, _height), displayData, _width * 4, 0);
            ImgPreview.Source = bitmap;
        }

        private void Channel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Content is string content)
            {
                bool val = cb.IsChecked == true;
                if (content == "R") _showR = val;
                if (content == "G") _showG = val;
                if (content == "B") _showB = val;
                if (content == "A") _showA = val;
                UpdatePreview();
            }
        }

        public static readonly DependencyProperty TexturePathProperty =
            DependencyProperty.Register("TexturePath", typeof(string), typeof(TextureViewer), new PropertyMetadata(null, OnTexturePathChanged));

        public string TexturePath
        {
            get { return (string)GetValue(TexturePathProperty); }
            set { SetValue(TexturePathProperty, value); }
        }

        private static void OnTexturePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextureViewer viewer && e.NewValue is string path)
            {
                viewer.LoadTexture(path);
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Allow zoom without Ctrl if desired, or keep Ctrl. User asked to "zoom in/out with scrolling with your mouse".
            // Usually this implies direct scrolling zooms if there are no scrollbars, or Ctrl+Scroll.
            // If scrollbars are present, Wheel usually scrolls.
            // Let's support BOTH Ctrl+Wheel and Buttons.
            // If user wants direct wheel zoom, we might annoy them if they can't scroll.
            // Let's stick to Ctrl+Wheel for now, but add buttons.
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                ApplyZoom(e.Delta > 0 ? 0.2 : -0.2);
                e.Handled = true;
            }
        }

        private void ApplyZoom(double delta)
        {
            double newScale = ImageScale.ScaleX + delta;
            if (newScale < 0.1) newScale = 0.1;
            if (newScale > 10) newScale = 10;
            
            ImageScale.ScaleX = newScale;
            ImageScale.ScaleY = newScale;
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e) => ApplyZoom(0.2);
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => ApplyZoom(-0.2);

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_texFile == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "TGA Image|*.tga",
                FileName = Path.GetFileNameWithoutExtension(_filePath) + ".tga"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
                    _texFile.ExportToTga(sfd.FileName, fs);
                    MessageBox.Show("Export successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
