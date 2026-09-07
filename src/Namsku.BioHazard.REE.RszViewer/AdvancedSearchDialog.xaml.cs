using System;
using System.Windows;
using System.Windows.Controls;

namespace RszViewer
{
    public partial class AdvancedSearchDialog : Window
    {
        public bool Confirmed { get; private set; }
        public SearchManager Manager { get; private set; }

        public AdvancedSearchDialog(SearchManager existingManager)
        {
            InitializeComponent();
            Manager = existingManager;
            
            // Populate fields from manager if possible
            TxtQuery.Text = Manager.Query;
            ChkMatchCase.IsChecked = Manager.MatchCase;
            ChkRegex.IsChecked = Manager.UseRegex;

            if (Manager.Type == SearchManager.SearchType.PropertyName) RadPropName.IsChecked = true;
            else if (Manager.Type == SearchManager.SearchType.PropertyValue) RadPropValue.IsChecked = true;
            else RadBoth.IsChecked = true;
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem is TabItem ti)
            {
                string header = ti.Header.ToString() ?? "";
                if (header == "Text")
                {
                    Manager.DataType = SearchManager.SearchDataType.Text;
                    Manager.Query = TxtQuery.Text;
                    Manager.MatchCase = ChkMatchCase.IsChecked == true;
                    Manager.UseRegex = ChkRegex.IsChecked == true;
                    
                    if (RadPropName.IsChecked == true) Manager.Type = SearchManager.SearchType.PropertyName;
                    else if (RadPropValue.IsChecked == true) Manager.Type = SearchManager.SearchType.PropertyValue;
                    else Manager.Type = SearchManager.SearchType.Both;
                }
                else if (header == "Number")
                {
                    if (RadInt.IsChecked == true)
                    {
                        Manager.DataType = SearchManager.SearchDataType.Integer;
                        if (long.TryParse(TxtMin.Text, out long min)) Manager.MinInt = min; else Manager.MinInt = null;
                        if (long.TryParse(TxtMax.Text, out long max)) Manager.MaxInt = max; else Manager.MaxInt = null;
                    }
                    else
                    {
                        Manager.DataType = SearchManager.SearchDataType.Float;
                        if (double.TryParse(TxtMin.Text, out double min)) Manager.MinFloat = min; else Manager.MinFloat = null;
                        if (double.TryParse(TxtMax.Text, out double max)) Manager.MaxFloat = max; else Manager.MaxFloat = null;
                    }
                    Manager.Type = SearchManager.SearchType.PropertyValue;
                }
                else if (header == "Hex")
                {
                    Manager.DataType = SearchManager.SearchDataType.Hex;
                    try
                    {
                        string hex = TxtHex.Text.Replace(" ", "").Replace("-", "");
                        byte[] bytes = new byte[hex.Length / 2];
                        for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                        Manager.HexPattern = bytes;
                    }
                    catch
                    {
                        MessageBox.Show("Invalid Hex String", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Manager.Type = SearchManager.SearchType.PropertyValue;
                }
                else if (header == "GUID")
                {
                    Manager.DataType = SearchManager.SearchDataType.Guid;
                    if (Guid.TryParse(TxtGuid.Text, out Guid g)) Manager.GuidTarget = g;
                    else
                    {
                        MessageBox.Show("Invalid GUID", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Manager.Type = SearchManager.SearchType.PropertyValue;
                }
            }

            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnPasteGuid_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                TxtGuid.Text = Clipboard.GetText();
            }
        }
    }
}
