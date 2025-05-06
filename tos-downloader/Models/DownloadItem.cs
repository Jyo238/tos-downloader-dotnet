using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace tos_downloader.Models;

public class DownloadItem : INotifyPropertyChanged
{
    private long _downloaded;
    private string _status = "等待中";
    private string _displayName = string.Empty;
    private string _fileName = string.Empty;
    private bool _isSelected = true;

    // 用於 UI 顯示的名稱 (來自網頁)
    public string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); }
    }

    // 實際下載存檔用的檔案名稱 (來自 URL)
    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(); }
    }

    public string Url { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public long TotalBytes { get; set; }

    public long DownloadedBytes
    {
        get => _downloaded;
        set { if (_downloaded != value) { _downloaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(Progress)); } }
    }

    public double Progress => TotalBytes > 0 ? Math.Min(100.0, (double)DownloadedBytes / TotalBytes * 100.0) : 0;

    public string Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}