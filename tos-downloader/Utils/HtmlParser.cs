using HtmlAgilityPack;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using tos_downloader.Models;

namespace tos_downloader.Utils;

public static class HtmlParser
{
    private const string BasePage = "https://tos.omg.com.tw/Download/Client";
    private static readonly Regex fileRegex = new(@"TreeOfSaviorTW(-\d+)?\.bin|TreeOfSaviorTW\.exe", RegexOptions.IgnoreCase);

    public static async Task<List<DownloadItem>> ParseDownloadItemsAsync()
    {
        var web = new HtmlWeb();
        var doc = await web.LoadFromWebAsync(BasePage);

        var list = new List<DownloadItem>();
        var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'box-1')]//a[contains(text(),'DOWNLOAD')]");

        if (nodes == null) return list;

        foreach (var aNode in nodes)
        {
            var href = aNode.GetAttributeValue("href", null);
            if (string.IsNullOrWhiteSpace(href)) continue;

            href = href.Replace("\\", "/");
            if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
            if (!fileRegex.IsMatch(href)) continue;

            // --- 從網頁表格提取顯示名稱 ---
            string displayName = "Unknown";
            var trNode = aNode.SelectSingleNode("ancestor::tr[1]");
            if (trNode != null)
            {
                // 抓取該行 (tr) 的第二個儲存格 (td[2]) 作為顯示名稱
                var tdNode = trNode.SelectSingleNode("td[2]");
                if (tdNode != null)
                {
                    displayName = HtmlEntity.DeEntitize(tdNode.InnerText)?.Trim() ?? string.Empty;
                }
            }
            // --- 提取結束 ---

            var actualFileName = Path.GetFileName(href);
            if (string.IsNullOrEmpty(actualFileName)) continue;

            list.Add(new DownloadItem
            {
                DisplayName = displayName,      // UI 顯示用
                FileName = actualFileName, // 實際檔案名
                Url = href,
                IsSelected = true
            });
        }
        return list;
    }
}