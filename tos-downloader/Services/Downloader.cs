using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using tos_downloader.Models;

namespace tos_downloader.Services;

public static class Downloader
{
    private static readonly HttpClient client = new();
    public static readonly ManualResetEventSlim PauseEvent = new(true); // true = 初始狀態為 Set (非暫停)

    public static async Task DownloadAsync(DownloadItem item, string folder, IProgress<string> log, CancellationToken token)
    {
        try
        {
            string dest = Path.Combine(folder, item.FileName);

            item.Status = "連線中";
            log.Report($"連線中: {item.FileName}");

            var req = new HttpRequestMessage(HttpMethod.Get, item.Url);
            if (File.Exists(dest))
            {
                try
                {
                    item.DownloadedBytes = new FileInfo(dest).Length;
                    req.Headers.Range = new RangeHeaderValue(item.DownloadedBytes, null);
                    log.Report($"偵測到已存在檔案: {item.FileName}, 從 {item.DownloadedBytes} bytes 繼續。");
                }
                catch (IOException ex)
                {
                    log.Report($"警告：無法讀取現有文件 {item.FileName} ({ex.Message})，將重新下載。");
                    item.DownloadedBytes = 0;
                    req.Headers.Range = null;

                    try { File.Delete(dest); }
                    catch
                    {
                    }
                }
            }
            else
            {
                item.DownloadedBytes = 0;
            }


            using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);

            // 處理部分內容(206)或完整內容(200)
            if (res.StatusCode != System.Net.HttpStatusCode.PartialContent && res.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // 如果伺服器回覆 416 (Range Not Satisfiable)，表示請求的範圍無效 (可能檔案已下載完成)
                if (res.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    log.Report($"檔案可能已完成或伺服器不支持斷點續傳: {item.FileName}");
                    if (item.DownloadedBytes > 0)
                    {
                        item.Status = "✅ 已完成 (驗證)";
                        if (item.TotalBytes == 0 || item.TotalBytes == item.DownloadedBytes)
                        {
                            item.TotalBytes = item.DownloadedBytes; //確保進度為100%
                            log.Report($"✅ 完成 (驗證): {item.FileName}");
                            return; 
                        }
                        else
                        {
                            log.Report($"警告: {item.FileName} 大小不匹配，重新下載。");
                            item.DownloadedBytes = 0;
                            req.Headers.Range = null;
                            throw new HttpRequestException($"Range不可滿足且大小不匹配 ({item.FileName})");
                        }
                    }
                }
                res.EnsureSuccessStatusCode(); // 如果不是200或206，且不是預期的416，則拋出異常
            }


            // 根據響應更新 TotalBytes
            if (res.StatusCode == System.Net.HttpStatusCode.PartialContent)
            {
                var contentRange = res.Content.Headers.ContentRange;
                if (contentRange?.HasLength == true)
                {
                    item.TotalBytes = contentRange.Length!.Value;
                }
                else
                {
                    item.TotalBytes = item.DownloadedBytes + res.Content.Headers.ContentLength.GetValueOrDefault();
                    log.Report($"警告: 無法從 ContentRange 取得總大小 ({item.FileName})");
                }
            }
            else // OK (200)
            {
                item.TotalBytes = res.Content.Headers.ContentLength.GetValueOrDefault();

                if (item.DownloadedBytes > 0)
                {
                    log.Report($"伺服器不支持斷點續傳 ({item.FileName})，從頭開始。");
                    item.DownloadedBytes = 0;
                    try { File.Delete(dest); } catch {  }
                }
            }

            Directory.CreateDirectory(folder);
            FileMode fileMode = (res.StatusCode == System.Net.HttpStatusCode.PartialContent && item.DownloadedBytes > 0) ? FileMode.Append : FileMode.Create;

            using var fs = new FileStream(dest, fileMode, FileAccess.Write, FileShare.None);
            if (fileMode == FileMode.Append)
            {
                fs.Seek(0, SeekOrigin.End);
            }

            using var stream = await res.Content.ReadAsStreamAsync(token);

            var buffer = new byte[8192]; // 8KB buffer
            var sw = Stopwatch.StartNew();
            long bytesReadSinceLastUpdate = 0;

            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
            {
                while (!PauseEvent.IsSet) 
                {
                    token.ThrowIfCancellationRequested();
                    item.Status = "⏸️ 已暫停";
                    await Task.Delay(200, token); 
                }

                if (item.Status == "⏸️ 已暫停")
                {
                    item.Status = "下載中...";
                }

                token.ThrowIfCancellationRequested();

                await fs.WriteAsync(buffer.AsMemory(0, read), token);
                item.DownloadedBytes += read;
                bytesReadSinceLastUpdate += read;

                // 更新狀態和進度 (例如每秒更新一次)
                if (sw.ElapsedMilliseconds >= 1000)
                {
                    double speed = bytesReadSinceLastUpdate / sw.Elapsed.TotalSeconds; // Bytes per second
                    double speedKB = speed / 1024d;
                    double speedMB = speedKB / 1024d;
                    string speedStr;
                    if (speedMB >= 1)
                        speedStr = $"{speedMB:F1} MB/s";
                    else
                        speedStr = $"{speedKB:F1} KB/s";

                    string remainStr = "";
                    if (speed > 0 && item.TotalBytes > 0)
                    {
                        double remainingSeconds = (item.TotalBytes - item.DownloadedBytes) / speed;
                        if (remainingSeconds > 0 && !double.IsInfinity(remainingSeconds))
                        {
                            remainStr = $" | 剩 {TimeSpan.FromSeconds(remainingSeconds):hh\\:mm\\:ss}";
                        }
                    }

                    item.Status = $"{item.Progress:F1}% | {speedStr}{remainStr}";

                    sw.Restart();
                    bytesReadSinceLastUpdate = 0;
                }
            }

            await fs.FlushAsync(token);
            if (item.TotalBytes == 0)
            {
                item.TotalBytes = item.DownloadedBytes;
            }
            item.Status = "✅ 已完成";
            log.Report($"✅ 完成: {item.FileName}");

        }
        catch (OperationCanceledException)
        {
            item.Status = "⏹️ 已取消";
            log.Report($"⏹️ 取消: {item.FileName}");
        }
        catch (HttpRequestException httpEx)
        {
            item.Status = $"❌ HTTP錯誤: {httpEx.StatusCode?.ToString() ?? httpEx.Message}";
            log.Report($"❌ HTTP錯誤 ({item.FileName}): {httpEx.Message}");
        }
        catch (IOException ioEx)
        {
            item.Status = $"❌ 文件錯誤: {ioEx.Message}";
            log.Report($"❌ 文件錯誤 ({item.FileName}): {ioEx.Message}");
        }
        catch (Exception ex)
        {
            item.Status = $"❌ 錯誤: {ex.Message}";
            log.Report($"❌ 錯誤 ({item.FileName}): {ex.Message}");
        }
    }
}