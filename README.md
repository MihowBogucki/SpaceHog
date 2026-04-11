# 🐷 SpaceHog

**Find out what's hogging your disk space.**

SpaceHog is a free, open-source disk space analyzer for Windows. It scans your drives and shows you exactly where your storage is going — with a fast, dark-themed UI.

![.NET](https://img.shields.io/badge/.NET%209-512BD4?logo=dotnet&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green)

---

## ✨ Features

- **Drive picker** — see all drives with used/total space at a glance
- **Fast recursive scan** — scans thousands of folders per second with live progress
- **Tree + Details view** — folder tree on the left, sortable detail list on the right
- **Visual size bars** — instantly see which folders are the biggest
- **Percentage columns** — % of parent folder and % of total disk
- **Filter/search** — quickly find folders by name
- **Right-click menu** — open any folder in Explorer or copy its path
- **Double-click navigation** — drill into subfolders in the details panel
- **Dark theme** — easy on the eyes
- **Single .exe** — no installer needed, fully self-contained

---

## 📸 Screenshot

> _Run the app and scan a drive to see the UI in action!_

---

## 🚀 Quick Start

### Option 1: Download the release (easiest)

1. Go to the [Releases](../../releases) page
2. Download `SpaceHog.exe`
3. Run it — no install required

### Option 2: Build from source

**Prerequisites:** [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or later)

```bash
git clone https://github.com/MihowBogucki/SpaceHog.git
cd SpaceHog
dotnet publish -c Release -o ./publish
```

The compiled `SpaceHog.exe` will be in the `./publish` folder.

---

## 🛠️ Usage

1. **Launch** `SpaceHog.exe`
2. **Pick a drive** from the dropdown (or click **Browse...** to choose any folder)
3. Click **Scan**
4. **Explore the results:**
   - Click folders in the tree (left panel) to see their contents in the detail view (right panel)
   - Double-click a folder in the detail view to navigate into it
   - Right-click any folder to open it in Explorer or copy the path
   - Use the filter box to search for a specific folder name
5. Click **Stop** to cancel a scan in progress

### Tips

- **Run as Administrator** to scan folders that require elevated permissions (e.g., `C:\Windows`, `C:\ProgramData`)
- The scan depth is 6 levels by default — this covers most use cases without taking too long
- Folders below 0 bytes are hidden from the results to keep things clean

---

## 📁 Project Structure

```
SpaceHog/
├── SpaceHog.csproj       # Project file (.NET 9 WinForms)
├── Program.cs             # Entry point
├── MainForm.cs            # Main UI — tree view, details list, toolbar, dark theme
├── DirScanner.cs          # Recursive directory scanner (async-friendly)
├── DirEntry.cs            # Data model for a scanned directory
├── SizeBarRenderer.cs     # Custom bar rendering for the details list
└── README.md              # You are here
```

---

## 🤝 Contributing

Contributions are welcome! Feel free to:

- Report bugs or request features via [Issues](../../issues)
- Submit a pull request

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).

---

## 🙏 Acknowledgments

Built as an open-source alternative for analyzing disk usage on Windows.
