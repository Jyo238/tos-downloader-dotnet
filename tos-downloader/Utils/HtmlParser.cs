using HtmlAgilityPack;
using System.IO;
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
        var doc = await Task.Run(() => web.Load(BasePage));

        var list = new List<DownloadItem>();
        var nodes = doc.DocumentNode.SelectNodes("//a[contains(text(),'DOWNLOAD')]")!;

        foreach (var a in nodes)
        {
            var href = a.GetAttributeValue("href", "").Replace("\\", "/");
            if (!href.StartsWith("http")) continue;
            if (!fileRegex.IsMatch(href)) continue;
            list.Add(new DownloadItem
            {
                FileName = Path.GetFileName(href),
                Url = href
            });
        }
        return list;
    }
}
