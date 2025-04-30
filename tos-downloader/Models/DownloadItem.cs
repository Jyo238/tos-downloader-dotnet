using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace tos_downloader.Models;

public class DownloadItem : INotifyPropertyChanged
{
    private long _downloaded;
    private string _status = "等待中";

    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
    public long TotalBytes { get; set; }

    public long DownloadedBytes
    {
        get => _downloaded;
        set { _downloaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(Progress)); }
    }

    public double Progress => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
