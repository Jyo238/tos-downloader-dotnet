using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks; // 確保 using System.Threading.Tasks;
using tos_downloader.Models;

namespace tos_downloader.Services;

public static class Downloader
{
    private static readonly HttpClient client = new();
    // ManualResetEventSlim 仍然是控制暫停/恢復的好工具
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
                    // 確保獲取文件大小的操作不會因文件被佔用而出錯
                    item.DownloadedBytes = new FileInfo(dest).Length;
                    req.Headers.Range = new RangeHeaderValue(item.DownloadedBytes, null);
                    log.Report($"偵測到已存在檔案: {item.FileName}, 從 {item.DownloadedBytes} bytes 繼續。");
                }
                catch (IOException ex)
                {
                    // 如果無法獲取文件信息（可能被鎖定），則從頭開始下載
                    log.Report($"警告：無法讀取現有文件 {item.FileName} ({ex.Message})，將重新下載。");
                    item.DownloadedBytes = 0;
                    // 清除可能已設定的 Range header
                    req.Headers.Range = null;
                    // 嘗試刪除可能已損壞或無法訪問的文件
                    try { File.Delete(dest); } catch { /* 忽略刪除失敗 */ }
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
                    // 檢查本地文件大小是否與預期一致（如果知道總大小的話）
                    // 這裡假設如果收到416，且本地有檔案，就認為完成了
                    if (item.DownloadedBytes > 0)
                    {
                        item.Status = "✅ 已完成 (驗證)";
                        // 這裡可以選擇不更新TotalBytes，或者嘗試重新獲取完整大小
                        if (item.TotalBytes == 0 || item.TotalBytes == item.DownloadedBytes)
                        {
                            // 如果總大小未知或已匹配，標記完成
                            item.TotalBytes = item.DownloadedBytes; //確保進度為100%
                            log.Report($"✅ 完成 (驗證): {item.FileName}");
                            return; // 結束此任務
                        }
                        else
                        {
                            // 大小不匹配，可能伺服器有問題或文件損壞，重新下載
                            log.Report($"警告: {item.FileName} 大小不匹配，重新下載。");
                            item.DownloadedBytes = 0;
                            req.Headers.Range = null;
                            // 重新發送請求 (需要重構或放棄) - 簡單起見，先報錯
                            throw new HttpRequestException($"Range不可滿足且大小不匹配 ({item.FileName})");
                        }
                    }
                }
                res.EnsureSuccessStatusCode(); // 如果不是200或206，且不是預期的416，則拋出異常
            }


            // 根據響應更新 TotalBytes
            if (res.StatusCode == System.Net.HttpStatusCode.PartialContent)
            {
                // ContentRange header 格式通常是 "bytes start-end/total"
                var contentRange = res.Content.Headers.ContentRange;
                if (contentRange?.HasLength == true)
                {
                    item.TotalBytes = contentRange.Length!.Value;
                }
                else
                {
                    // 如果部分內容響應沒有總長度，我們無法計算總進度
                    // 嘗試使用已下載的加上響應內容長度（但這不準確）
                    item.TotalBytes = item.DownloadedBytes + res.Content.Headers.ContentLength.GetValueOrDefault();
                    log.Report($"警告: 無法從 ContentRange 取得總大小 ({item.FileName})");
                }
            }
            else // OK (200)
            {
                item.TotalBytes = res.Content.Headers.ContentLength.GetValueOrDefault();
                // 如果是從頭下載(200 OK)，重置 DownloadedBytes
                if (item.DownloadedBytes > 0)
                {
                    log.Report($"伺服器不支持斷點續傳 ({item.FileName})，從頭開始。");
                    item.DownloadedBytes = 0;
                    // 清理可能存在的舊文件
                    try { File.Delete(dest); } catch { /* 忽略刪除失敗 */ }
                }
            }


            // 確保目標資料夾存在
            Directory.CreateDirectory(folder);

            // 使用 FileMode.Create 會覆蓋檔案，適用於從頭下載(200 OK)或續傳前清除文件
            // 使用 FileMode.Append 適用於斷點續傳 (206 PartialContent)
            FileMode fileMode = (res.StatusCode == System.Net.HttpStatusCode.PartialContent && item.DownloadedBytes > 0) ? FileMode.Append : FileMode.Create;

            using var fs = new FileStream(dest, fileMode, FileAccess.Write, FileShare.None);
            // 如果是 Append 模式，確保文件指標在末尾 (雖然 FileStream 構造函數應已處理)
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
                // --- 修改點：將阻塞式 Wait 改為非阻塞式異步等待 ---
                while (!PauseEvent.IsSet) // 當 PauseEvent 被 Reset (即請求暫停)
                {
                    token.ThrowIfCancellationRequested(); // 在等待暫停時也能響應取消請求
                    item.Status = "⏸️ 已暫停";
                    await Task.Delay(200, token); // 非阻塞地等待 200ms，然後再次檢查 PauseEvent 狀態
                }
                // ----------------------------------------------------

                // 如果從暫停恢復，狀態可能需要更新回下載中
                if (item.Status == "⏸️ 已暫停")
                {
                    item.Status = "下載中..."; // 或者使用下面的速度/進度更新狀態
                }

                token.ThrowIfCancellationRequested(); // 在寫入前也檢查一次取消

                await fs.WriteAsync(buffer.AsMemory(0, read), token); // 使用異步寫入
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

            // 下載完成後，確保最後的狀態顯示正確
            await fs.FlushAsync(token); // 確保所有緩衝數據寫入文件
            if (item.TotalBytes == 0)
            { // 如果一開始沒拿到總大小，用實際下載的 bytes 作為總大小
                item.TotalBytes = item.DownloadedBytes;
            }
            item.Status = "✅ 已完成";
            log.Report($"✅ 完成: {item.FileName}");

        }
        catch (OperationCanceledException) // 捕捉由 token 引起的取消異常
        {
            item.Status = "⏹️ 已取消";
            log.Report($"⏹️ 取消: {item.FileName}");
        }
        catch (HttpRequestException httpEx)
        {
            // 更具體地處理網絡相關錯誤
            item.Status = $"❌ HTTP錯誤: {httpEx.StatusCode?.ToString() ?? httpEx.Message}";
            log.Report($"❌ HTTP錯誤 ({item.FileName}): {httpEx.Message}");
        }
        catch (IOException ioEx)
        {
            // 處理文件讀寫錯誤
            item.Status = $"❌ 文件錯誤: {ioEx.Message}";
            log.Report($"❌ 文件錯誤 ({item.FileName}): {ioEx.Message}");
        }
        catch (Exception ex) // 捕捉其他所有異常
        {
            item.Status = $"❌ 錯誤: {ex.Message}";
            log.Report($"❌ 錯誤 ({item.FileName}): {ex.Message}");
        }
        finally
        {
            // 確保即使出錯，下載速度計時器也停止
            // (這裡不需要特別處理，因為 sw 是局部變數)
        }
    }
}