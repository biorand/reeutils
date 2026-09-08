using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RszViewer
{
    public class RszTabViewModel : INotifyPropertyChanged
    {
        public string Header { get; set; } = "";
        public string FullPath { get; set; } = "";
        public ObservableCollection<RszNodeViewModel> Nodes { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TextureTabViewModel : RszTabViewModel 
    {
        // Inherits Header, FullPath. Nodes unused.
    }
}
