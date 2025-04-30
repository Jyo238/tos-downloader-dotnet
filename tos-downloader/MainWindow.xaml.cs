using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WinForms = System.Windows.Forms;

using tos_downloader.Models;
using tos_downloader.Services;
using tos_downloader.Utils;

namespace tos_downloader;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DownloadItem> downloadItems = new();
    private CancellationTokenSource? cts;

    public MainWindow()
    {
        InitializeComponent();
        FileList.ItemsSource = downloadItems;
        Loaded += async (_, _) => await LoadListAsync();   // 啟動即載入
    }

    /* ---------- UI 事件 ---------- */

    private void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new WinForms.FolderBrowserDialog();
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            PathBox.Text = dlg.SelectedPath;
    }

    private async void LoadList_Click(object? sender, RoutedEventArgs e) => await LoadListAsync();

    private async void StartDownload_Click(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(PathBox.Text))
        {
            MessageBox.Show("請先選擇下載資料夾"); return;
        }

        ToggleButtons(isDownloading: true);

        cts = new CancellationTokenSource();
        var logger = new Progress<string>(s => Dispatcher.Invoke(() => LogBox.AppendText(s + "\n")));
        int maxPar = ParallelBox.IsChecked == true ? 4 : 1;
        var targets = downloadItems.Where(i => i.IsSelected).ToList();

        var tasks = targets.Chunk(maxPar)
                           .Select(batch => Task.WhenAll(
                               batch.Select(it => Downloader.DownloadAsync(it, PathBox.Text, logger, cts.Token))));

        await Task.WhenAll(tasks);

        ToggleButtons(isDownloading: false);
        MessageBox.Show("🎉 所有下載完成");
        Process.Start("explorer", PathBox.Text);
    }

    private void Pause_Click(object? sender, RoutedEventArgs e)
    {
        Downloader.PauseEvent.Reset();
        PauseBtn.IsEnabled = false;
        ResumeBtn.IsEnabled = true;
    }

    private void Resume_Click(object? sender, RoutedEventArgs e)
    {
        Downloader.PauseEvent.Set();
        PauseBtn.IsEnabled = true;
        ResumeBtn.IsEnabled = false;
    }

    /* ---------- 私用方法 ---------- */

    private async Task LoadListAsync()
    {
        LogBox.Clear();
        LogBox.AppendText("🔍 解析中...\n");

        downloadItems.Clear();
        foreach (var it in await HtmlParser.ParseDownloadItemsAsync())
            downloadItems.Add(it);

        LogBox.AppendText($"✅ 共找到 {downloadItems.Count} 個檔案\n");
    }

    private void ToggleButtons(bool isDownloading)
    {
        StartBtn.IsEnabled = !isDownloading;
        PauseBtn.IsEnabled = isDownloading;
        ResumeBtn.IsEnabled = false;
    }
}
