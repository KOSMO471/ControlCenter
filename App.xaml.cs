global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using Clipboard = System.Windows.Clipboard;
global using Color = System.Windows.Media.Color;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using Button = System.Windows.Controls.Button;
global using TextBox = System.Windows.Controls.TextBox;
global using Orientation = System.Windows.Controls.Orientation;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using SearchOption = System.IO.SearchOption;
global using Image = System.Windows.Controls.Image;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using DragEventArgs = System.Windows.DragEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
global using DataFormats = System.Windows.DataFormats;
global using DragDropEffects = System.Windows.DragDropEffects;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Border = System.Windows.Controls.Border;
global using CheckBox = System.Windows.Controls.CheckBox;
global using FontFamily = System.Windows.Media.FontFamily;
global using ProgressBar = System.Windows.Controls.ProgressBar;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Hardcodet.Wpf.TaskbarNotification;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;


namespace ControlCenter
{
    public class TodoItem : INotifyPropertyChanged
    {
        private bool _isDone;
        public string Text { get; set; } = "";
        public bool IsDone { get => _isDone; set { _isDone = value; OnPropertyChanged(nameof(IsDone)); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AppShortcut : INotifyPropertyChanged
    {
        private BitmapImage? _iconImage;
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Icon { get; set; } = "⚙️";
        public bool IsWeb { get; set; } = false;

        public BitmapImage? IconImage
        {
            get => _iconImage;
            set { _iconImage = value; OnPropertyChanged(nameof(IconImage)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class UserData
    {
        public List<AppShortcut> AppShortcuts { get; set; } = new List<AppShortcut>();
        public List<TodoItem> TodoItems { get; set; } = new List<TodoItem>();
        public string Notes { get; set; } = "";
        public string SearchEngine { get; set; } = "Google";
        public bool StartWithWindows { get; set; } = false;
        public string? AppPassword { get; set; } = null;
        public bool OverlayStartMinimized { get; set; } = false;
        public bool TrayOnClose { get; set; } = true;

        public int MascotTotalClicks { get; set; } = 0;
        public int MascotRageClicks { get; set; } = 0;
        public bool NyanTriggered { get; set; } = false;
        public bool ScreenshotTaken { get; set; } = false;
        public DateTime FirstLaunchTime { get; set; } = DateTime.Now;
        public long TotalUptimeSeconds { get; set; } = 0;
        public List<string> UnlockedAchievements { get; set; } = new List<string>();
        public List<string> SeenHolidays { get; set; } = new List<string>();
        public bool EarlyBirdUnlocked { get; set; } = false;
        public bool NightOwlUnlocked { get; set; } = false;
        public bool MuteHourUnlocked { get; set; } = false;
        public bool RainDanceUnlocked { get; set; } = false;

        public bool ShortcutCollectorUnlocked { get; set; } = false;
        public bool DevPreviewUnlocked { get; set; } = false;
        public bool GhostUserUnlocked { get; set; } = false;
        public long TrayIdleSeconds { get; set; } = 0;
        public long WeekendActiveSeconds { get; set; } = 0;
        public bool WeekendWarriorUnlocked { get; set; } = false;
        public long ContinuousActiveSeconds { get; set; } = 0;
        public bool WorkaholicUnlocked { get; set; } = false;
        public bool CustomizerUnlocked { get; set; } = false;
        public List<string> VisitedViews { get; set; } = new List<string>();
        public bool ExplorerUnlocked { get; set; } = false;

        public List<string> PriorityBluetoothDevices { get; set; } = new List<string>();
        public Dictionary<string, long> BluetoothUsageSeconds { get; set; } = new Dictionary<string, long>();
        public DateTime BluetoothUsageResetDate { get; set; } = DateTime.Today;
        public bool BluetoothDualAudioEnabled { get; set; } = false;
        public bool BluetoothAutoConnectEnabled { get; set; } = true;
        public bool BluetoothSilentModeOnFullscreen { get; set; } = true;
        public string BluetoothSharedClipboard { get; set; } = "";

        // ============ YENİ BAŞARIM TAKİP ALANLARI ============
        public int ConsoleCommandCount { get; set; } = 0;
        public int ConsoleAdminStartCount { get; set; } = 0;
        public int PowerShellCommandCount { get; set; } = 0;
        public int ConsoleSessionCommandCount { get; set; } = 0;
        public int WifiActionCount { get; set; } = 0;
        public int CleanActionCount { get; set; } = 0;
        public int UpdateActionCount { get; set; } = 0;
        public int TraceActionCount { get; set; } = 0;
        public int InstallActionCount { get; set; } = 0;

        public HashSet<string> PreviewedFiles { get; set; } = new HashSet<string>();
        public HashSet<string> OpenedImages { get; set; } = new HashSet<string>();
        public HashSet<string> OpenedPdfs { get; set; } = new HashSet<string>();
        public HashSet<string> OpenedDocx { get; set; } = new HashSet<string>();
        public HashSet<string> ExploredArchives { get; set; } = new HashSet<string>();
        public HashSet<string> SavedCodeExtensions { get; set; } = new HashSet<string>();
        public int TextFileSaveCount { get; set; } = 0;

        public int PomodoroCompletedCount { get; set; } = 0;
        public int PomodoroConsecutiveCount { get; set; } = 0;
        public int CalculatorOpCount { get; set; } = 0;
        public bool CalculatorErrorTriggered { get; set; } = false;
        public int NotesSaveCount { get; set; } = 0;
        public int ClipboardUniqueCount { get; set; } = 0;
        public int ClipboardClearCount { get; set; } = 0;
        public int TimerResetCount { get; set; } = 0;

        public int ProcessKillCount { get; set; } = 0;
        public bool HeavyProcessKilled { get; set; } = false;
        public int ProcessRefreshCount { get; set; } = 0;
        public HashSet<string> TopMemoryKilledProcesses { get; set; } = new HashSet<string>();
        public int AppLaunchCount { get; set; } = 0;
        public int AppUninstallCount { get; set; } = 0;
        public int AppUninstallCancelCount { get; set; } = 0;
        public int AppListRefreshCount { get; set; } = 0;

        public int VolumeChangeCount { get; set; } = 0;
        public bool MaxVolumeReached { get; set; } = false;
        public int Precise50Count { get; set; } = 0;
        public int MuteToggleCount { get; set; } = 0;
        public DateTime SilentNightStart { get; set; } = DateTime.MinValue;
        public bool SilentNightUnlocked { get; set; } = false;

        public int SearchCount { get; set; } = 0;
        public int AppShortcutAddCount { get; set; } = 0;
        public int WebShortcutAddCount { get; set; } = 0;
        public int DragDropShortcutCount { get; set; } = 0;
        public int ShortcutLaunchCount { get; set; } = 0;
        public int ShortcutDeleteCount { get; set; } = 0;

        public int BluetoothPanelOpenCount { get; set; } = 0;
        public HashSet<string> PairedBluetoothDevices { get; set; } = new HashSet<string>();
        public int BluetoothMediaControlCount { get; set; } = 0;
        public bool BluetoothLowBatterySeen { get; set; } = false;
        public HashSet<string> UsedBluetoothDevices { get; set; } = new HashSet<string>();

        public HashSet<string> UsedDays { get; set; } = new HashSet<string>();
        public int ConsecutiveDaysUsed { get; set; } = 0;
        public DateTime LastUsedDate { get; set; } = DateTime.MinValue;
        public int MorningLaunchCount { get; set; } = 0;
        public int NightUsageCount { get; set; } = 0;
        public int LunchUsageCount { get; set; } = 0;
        public HashSet<int> NewYearYears { get; set; } = new HashSet<int>();
        public DateTime LastAppCloseTime { get; set; } = DateTime.MinValue;
        public int StartupRunCount { get; set; } = 0;
        public int SystemAlertShownCount { get; set; } = 0;
        public int NotificationShownCount { get; set; } = 0;

        public int DrawerToggleCount { get; set; } = 0;
        public long TrayResidentSeconds { get; set; } = 0;
        public Dictionary<string, int> SameDayViewCounts { get; set; } = new Dictionary<string, int>();
        public DateTime SameDayViewDate { get; set; } = DateTime.MinValue;
        public HashSet<string> OverlayCorners { get; set; } = new HashSet<string>();
        public int OverlayModeToggleCount { get; set; } = 0;
        public int FpsPingToggleCount { get; set; } = 0;
        public DateTime OverlayOpenTime { get; set; } = DateTime.MinValue;
    }

    public class Achievement
    {
        public string Id { get; set; } = "";
        public string Icon { get; set; } = "🏆";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Unlocked { get; set; } = false;
        public DateTime? UnlockTime { get; set; }
    }

    public class FileItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Extension { get; set; } = "";
        public string Size { get; set; } = "";
        public long RawSize { get; set; } = 0;
    }

    public class GameInfo { public string Name { get; set; } = ""; public string Path { get; set; } = ""; public BitmapImage? Icon { get; set; } }
    public class AppInfo { public string Name { get; set; } = ""; public string Path { get; set; } = ""; public string? IconPath { get; set; } }

    public class ProcessInfo
    {
        public string ProcessName { get; set; } = "";
        public int Id { get; set; } = 0;
        public string CpuUsage { get; set; } = "0%";
        public string Memory { get; set; } = "0 MB";
        public Process? Process { get; set; }
    }

    public class AudioSessionInfo
    {
        public string DisplayName { get; set; } = "";
        public float Volume { get; set; } = 100;
        public bool IsMuted { get; set; } = false;
        public SimpleAudioVolume? AudioVolume { get; set; }
        public string? SessionIdentifier { get; set; }
    }
}