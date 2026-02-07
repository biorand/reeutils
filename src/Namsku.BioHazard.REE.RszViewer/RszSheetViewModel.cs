using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ExcelDataReader;

namespace RszViewer
{
    public class RszSheetViewModel : RszTabViewModel
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        
        public ObservableCollection<RszSheetTab> Sheets { get; } = new ObservableCollection<RszSheetTab>();
        private string _status = "Ready";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }

        public async Task LoadSheet(string pathOrUrl)
        {
            try
            {
                Sheets.Clear();
                Status = "Loading...";

                Stream dataStream;
                bool isUrl = pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                if (isUrl)
                {
                    Status = "Downloading spreadsheet...";
                    var response = await _httpClient.GetAsync(pathOrUrl);
                    response.EnsureSuccessStatusCode();
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    dataStream = new MemoryStream(bytes);
                }
                else if (File.Exists(pathOrUrl))
                {
                    dataStream = File.OpenRead(pathOrUrl);
                }
                else
                {
                    Status = "File not found: " + pathOrUrl;
                    return;
                }

                Status = "Parsing spreadsheet...";
                
                // Register code page for ExcelDataReader
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                using (dataStream)
                using (var reader = ExcelReaderFactory.CreateReader(dataStream))
                {
                    var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                    });

                    foreach (DataTable table in dataSet.Tables)
                    {
                        var sheetTab = new RszSheetTab(table.TableName);
                        
                        foreach (DataRow row in table.Rows)
                        {
                            var rowValues = new StringBuilder();
                            for (int i = 0; i < table.Columns.Count; i++)
                            {
                                if (i > 0) rowValues.Append(" | ");
                                rowValues.Append(row[i]?.ToString() ?? "");
                            }
                            
                            var firstCol = row[0]?.ToString() ?? $"Row {table.Rows.IndexOf(row)}";
                            sheetTab.Nodes.Add(new RszNodeViewModel(firstCol, rowValues.ToString(), "Row"));
                        }
                        
                        Sheets.Add(sheetTab);
                    }
                }

                Status = $"Loaded {Sheets.Count} sheet(s)";
            }
            catch (HttpRequestException ex)
            {
                Status = $"Download failed: {ex.Message}";
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
        }
    }

    public class RszSheetTab : RszNodeViewModel
    {
        public ObservableCollection<RszNodeViewModel> Nodes { get; } = new ObservableCollection<RszNodeViewModel>();

        public RszSheetTab(string name) : base(name, "", "Sheet")
        {
            Name = name;
            Icon = "\uE8A5"; // Document icon
        }
    }
}
