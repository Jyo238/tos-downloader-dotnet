# 救世者之樹 自動下載器 (TOS Downloader)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
<!-- 你可以未來加入其他徽章，例如 Build Status -->

一個使用 C#、.NET 8 和 WPF 開發的圖形介面工具，旨在自動化下載 MMORPG《救世者之樹》(Tree of Savior) 的主程式檔案。

---

## 🤔 為何選擇此工具？

*   **自動化：** 自動從官網解析最新的主程式分段檔案連結 (`.exe`, `.bin`)，省去手動尋找和複製的麻煩。
*   **效率：** 支援多檔案同時下載及斷點續傳，最大化利用你的網路頻寬，並在網路中斷後能從上次進度繼續。
*   **使用者友善：** 提供直觀的圖形介面，輕鬆選擇下載路徑、勾選檔案並監控下載進度。
*   **更少的誤報：** 相較於使用 PyInstaller 等工具打包的 Python 腳本，原生編譯的 .NET 應用程式結構更標準，能顯著降低被 Windows Defender 或其他防毒軟體誤判為惡意軟體的機率。

---

## ✨ 主要功能

*   [x] 直觀的圖形化使用者介面 (WPF)
*   [x] 自動解析官網最新下載連結
*   [x] 自由勾選需要下載的檔案
*   [x] 支援多檔並行下載 (最多 4 個，可配置)
*   [x] 支援斷點續傳 (基於 HTTP Range)
*   [x] 即時顯示總進度、個別檔案狀態、下載速度和預估剩餘時間
*   [x] 所有選定檔案下載完成後彈出提示，並自動開啟目標資料夾

---

## 🚀 如何使用 (給一般使用者)

1.  **前往 [GitHub Releases](https://github.com/JYO238/tos-downloader-dotnet/releases) 頁面。** (請將 `YOUR_USERNAME` 替換成你的 GitHub 使用者名稱)
2.  **下載最新版本的 `tos-downloader-dotnet-vX.X.X.zip` 壓縮檔。**
3.  **將下載的 `.zip` 檔案解壓縮到你喜歡的位置。**
4.  **執行 `tos-downloader-dotnet.exe`。** (檔名可能會因編譯設定而異，請確認解壓縮後的 `.exe` 檔)

**處理 Windows SmartScreen 提示：**

> 由於此應用程式沒有購買昂貴的程式碼簽章憑證，Windows SmartScreen 可能會顯示警告。這是正常的。
>
> *   點擊「**其他資訊**」(More info)。
> *   然後點擊「**仍要執行**」(Run anyway)。

---

## 🛠️ 如何編譯 (給開發者)

### 編譯需求

*   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本。
*   Visual Studio 2022 (建議，包含 WPF 開發工具) 或其他支援 .NET 的 IDE/編輯器。

### 步驟

1.  **克隆 (Clone) 儲存庫：**
    ```bash
    git clone https://github.com/YOUR_USERNAME/tos-downloader.git
    cd tos-downloader
    ```
2.  **使用 Visual Studio：**
    *   直接開啟 `tos-downloader.sln` 方案檔。
    *   選擇 `Release` 組態。
    *   點擊「建置 (Build)」->「建置方案 (Build Solution)」(或按 F6)。
3.  **使用 .NET CLI (命令列)：**
    ```bash
    # 編譯 Release 版本
    dotnet build -c Release
    ```
4.  **執行檔位置：**
    編譯後的執行檔通常位於： `bin/Release/net8.0-windows/tos-downloader.exe`

---

## 📦 打包與分發建議

*   **Framework-Dependent (推薦):** 預設的編譯方式。產生的 `.exe` 較小，但需要使用者系統上已安裝 .NET 8 Desktop Runtime。
*   **Self-Contained:** 可以將 .NET Runtime 打包進應用程式，使用者無需預先安裝。檔案較大。可在 `.csproj` 或 `dotnet publish` 指令中設定。
*   **ZIP 壓縮包:** 將 `Release` 資料夾 (或 `publish` 資料夾) 的內容壓縮成 `.zip` 檔案，附上簡易說明，是簡單的分發方式。
*   **安裝程式 (如 Inno Setup):** 如果需要更正式的安裝體驗 (例如建立桌面捷徑、反安裝程式)，可以使用 [Inno Setup](https://jrsoftware.org/isinfo.php) 等工具製作安裝包。
*   **程式碼簽章:** 若要完全避免 SmartScreen 警告並提高信任度，可以考慮購買程式碼簽章憑證並對你的 `.exe` 進行簽章。

---

## 🛡️ 關於防毒軟體提示

*   **原始碼透明：** 本專案原始碼完全公開，不含任何惡意程式碼。其功能僅限於解析網頁內容和執行標準的 HTTP 下載操作。
*   **為何 .NET 可能更優：** 如前所述，.NET 應用程式的標準結構較不易觸發防毒軟體的啟發式掃描警報，尤其是相比於將解譯器和腳本打包在一起的方式。
*   **自行驗證：** 如果你對執行檔的安全性有疑慮，可以將下載的 `.exe` 上傳到 [VirusTotal](https://www.virustotal.com/) 進行線上掃描，該網站會使用數十種不同的防毒引擎進行檢測。

---

## 📄 授權 (License)

本專案採用 [MIT License](LICENSE) 授權。