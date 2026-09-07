using System.Windows;
using Microsoft.Win32;

namespace RszViewer
{
    public partial class SettingsWindow : Window
    {
        private AppConfig _config;

        public SettingsWindow(AppConfig config)
        {
            InitializeComponent();
            _config = config;
            
            TxtRszPath.Text = _config.RszRepoPath ?? "";
            TxtSheetPath.Text = _config.SpreadsheetPath ?? "";
        }

        private void BtnBrowseRsz_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "RSZ JSON|*.json" };
            if (dlg.ShowDialog() == true)
            {
                TxtRszPath.Text = dlg.FileName;
            }
        }

        private async void BtnTestDownload_Click(object sender, RoutedEventArgs e)
        {
            var url = TxtSheetPath.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please enter a URL first.", "No URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var client = new System.Net.Http.HttpClient();
                var response = await client.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
                var length = response.Content.Headers.ContentLength;
                var sizeStr = length.HasValue ? $"{length.Value / 1024.0:F1} KB" : "unknown size";
                
                MessageBox.Show($"✓ Download successful!\n\nContent-Type: {contentType}\nSize: {sizeStr}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"✗ Download failed:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _config.RszRepoPath = TxtRszPath.Text;
            _config.SpreadsheetPath = TxtSheetPath.Text;
            _config.Save();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
