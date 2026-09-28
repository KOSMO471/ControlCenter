using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ControlCenter
{
    public enum OverlayMode
    {
        Compact,
        Detailed
    }

    public partial class OverlayWindow : Window
    {
        public event Action? OnShowMainWindow;
        public event Action? OnGameMode;
        public event Action? OnOptimize;
        public event Action? OnToggleFpsPing;
        public event Action? OnExit;
        public event Action? OnReopenApp;

        public event Action<int>? OnMascotClicked;
        public event Action? OnNyanTriggered;
        public event Action? OnScreenshotTaken;

        private DispatcherTimer? _stopwatchTimer;
        private DateTime _startTime;

        private DispatcherTimer? _rtssTimer;
        private bool _useRTSS = false;
        private int _lastRTSSFPS = 0;

        private bool _contentVisible = true;
        private OverlayMode _currentMode = OverlayMode.Compact;

        public MascotController? Mascot { get; private set; }
        private DispatcherTimer? _mascotIdleTimer;
        private DispatcherTimer? _batteryTimer;
        private DispatcherTimer? _weatherTimer;
        private DispatcherTimer? _screenshotWatcher;
        private DateTime _lastScreenshotCheck = DateTime.Now;
        private readonly List<Key> _konamiBuffer = new();
        private readonly Key[] _konami = { Key.Up, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Left, Key.Right };
        private bool _mascotNyanActive = false;

        public OverlayWindow()
        {
            InitializeComponent();

            _startTime = DateTime.Now;
            _stopwatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _stopwatchTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _startTime;
                TxtTimer.Text = elapsed.ToString(@"hh\:mm\:ss");
            };
            _stopwatchTimer.Start();

            _rtssTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _rtssTimer.Tick += (s, e) => PollRTSSFPS();
            _rtssTimer.Start();

            this.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };

            SetMode(OverlayMode.Compact);

            Mascot = new MascotController(ImgMascot);
            Mascot.Start();

            Mascot.OnClicked += (count) => OnMascotClicked?.Invoke(count);
            Mascot.OnScreenshotTriggered += () => OnScreenshotTaken?.Invoke();

            this.KeyDown += OverlayWindow_KeyDown;

            // Maskot tepkileri her 5 saniyede bir kontrol edilir
            _mascotIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _mascotIdleTimer.Tick += (s, e) => CheckIdleState();
            _mascotIdleTimer.Start();

            _batteryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _batteryTimer.Tick += (s, e) => CheckBattery();
            _batteryTimer.Start();

            _weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            _weatherTimer.Tick += (s, e) => CheckWeather();
            _weatherTimer.Start();

            _screenshotWatcher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _screenshotWatcher.Tick += (s, e) => CheckScreenshot();
            _screenshotWatcher.Start();

            this.Closed += (s, e) =>
            {
                Mascot?.Stop();
                _stopwatchTimer?.Stop();
                _rtssTimer?.Stop();
                _mascotIdleTimer?.Stop();
                _batteryTimer?.Stop();
                _weatherTimer?.Stop();
                _screenshotWatcher?.Stop();
            };
        }

        private void PollRTSSFPS()
        {
            try
            {
                using (var mmf = MemoryMappedFile.OpenExisting("Global\\RTSSSharedMemoryV2"))
                using (var accessor = mmf.CreateViewAccessor(0, 4096, MemoryMappedFileAccess.Read))
                {
                    uint signature = accessor.ReadUInt32(0);
                    if (signature != 0x54525352)
                    {
                        _useRTSS = false;
                        return;
                    }

                    int fps = accessor.ReadInt32(8);
                    if (fps > 0 && fps < 1000)
                    {
                        _useRTSS = true;
                        _lastRTSSFPS = fps;
                        UpdateFPS(fps);
                    }
                    else
                    {
                        _useRTSS = false;
                    }
                }
            }
            catch
            {
                _useRTSS = false;
            }
        }

        public void UpdateFPS(int fps)
        {
            Dispatcher.Invoke(() =>
            {
                TxtFPS.Text = $"FPS: {fps}";
                TxtFPS.Foreground = fps < 60 ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.White);
            });
        }

        public void UpdatePing(long pingMs)
        {
            Dispatcher.Invoke(() =>
            {
                TxtPing.Text = pingMs < 0 ? "Ping: -" : $"Ping: {pingMs}ms";
            });
        }

        public void SetFpsPingVisibility(bool visible)
        {
            Dispatcher.Invoke(() =>
            {
                TxtFPS.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                TxtPing.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        public void UpdateHardwareInfo(double cpuUsage, double ramUsage, double cpuTemp, double gpuTemp, double netDown, double netUp)
        {
            Dispatcher.Invoke(() =>
            {
                TxtCPU.Text = $"CPU: {cpuUsage:F0}%";
                TxtRAM.Text = $"RAM: {ramUsage:F0}%";
                TxtCPUTemp.Text = $"CPU: {cpuTemp:F0} C";
                TxtGPUTemp.Text = $"GPU: {gpuTemp:F0} C";

                string downUnit = netDown >= 1024 ? $"{netDown / 1024:F1} MB/s" : $"{netDown:F1} KB/s";
                string upUnit = netUp >= 1024 ? $"{netUp / 1024:F1} MB/s" : $"{netUp:F1} KB/s";
                TxtNet.Text = $"D:{downUnit} U:{upUnit}";
            });
        }

        public void UpdateGpuInfo(double gpuLoad, double vramUsed, double vramTotal, double fanSpeed)
        {
            Dispatcher.Invoke(() =>
            {
                TxtGPUUsage.Text = $"GPU: {gpuLoad:F0}%";
                TxtVRAM.Text = $"VRAM: {vramUsed:F0}/{vramTotal:F0} MB";
                TxtFan.Text = $"Fan: {fanSpeed:F0} RPM";
            });
        }

        public void SetMode(OverlayMode mode)
        {
            _currentMode = mode;
            Dispatcher.Invoke(() =>
            {
                bool detailed = (mode == OverlayMode.Detailed);
                TxtCPU.Visibility = detailed ? Visibility.Visible : Visibility.Collapsed;
                TxtRAM.Visibility = detailed ? Visibility.Visible : Visibility.Collapsed;
                TxtCPUTemp.Visibility = detailed ? Visibility.Visible : Visibility.Collapsed;
                TxtGPUTemp.Visibility = detailed ? Visibility.Visible : Visibility.Collapsed;
                TxtGPUUsage.Visibility = detailed ? Visibility.Visible : Visibility.Collapsed;
                TxtVRAM.Visibility = detailed ? Visibility.Visible : Visibility.Collapsed;
                TxtFan.Visibility = detailed ? Visibility.Visible : Visibility.Collapsed;
                TxtNet.Visibility = detailed ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            _contentVisible = !_contentVisible;
            ContentPanel.Visibility = _contentVisible ? Visibility.Visible : Visibility.Collapsed;
            BtnToggle.Content = _contentVisible ? "\u25C4" : "\u25BA";
            this.InvalidateMeasure();
        }

        private void BtnReopen_Click(object sender, RoutedEventArgs e)
        {
            // Ayarlar (dişli) tuşu SADECE sol tıkla bağlam menüsünü açar.
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void BtnReopen_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Sağ tık devre dışı - menü yalnızca sol tık ile açılır
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (this.WindowState == WindowState.Normal)
            {
                var workArea = SystemParameters.WorkArea;
                double screenLeft = workArea.Left, screenTop = workArea.Top;
                double screenRight = workArea.Right, screenBottom = workArea.Bottom;

                double left = this.Left, top = this.Top;
                double width = this.ActualWidth, height = this.ActualHeight;

                if (left < (screenLeft + screenRight) / 2)
                    left = screenLeft;
                else
                    left = screenRight - width;

                if (top < (screenTop + screenBottom) / 2)
                    top = screenTop;
                else
                    top = screenBottom - height;

                this.Left = left;
                this.Top = top;
            }
        }

        private void MainBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x00, 0x00, 0x00));
        }

        private void MainBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x00, 0x00));
        }

        private void MenuItem_Show_Click(object sender, RoutedEventArgs e) => OnShowMainWindow?.Invoke();
        private void MenuItem_GameMode_Click(object sender, RoutedEventArgs e) => OnGameMode?.Invoke();
        private void MenuItem_Optimize_Click(object sender, RoutedEventArgs e) => OnOptimize?.Invoke();

        private void MenuItem_ToggleFpsPing_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                mi.IsChecked = !mi.IsChecked;
                OnToggleFpsPing?.Invoke();
            }
        }

        private void MenuItem_ToggleMode_Click(object sender, RoutedEventArgs e)
        {
            var newMode = (_currentMode == OverlayMode.Compact) ? OverlayMode.Detailed : OverlayMode.Compact;
            SetMode(newMode);
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e) => OnExit?.Invoke();

        private void ImgMascot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Mascot?.HandleClick();
        }

        private void OverlayWindow_KeyDown(object sender, KeyEventArgs e)
        {
            _konamiBuffer.Add(e.Key);
            if (_konamiBuffer.Count > _konami.Length)
                _konamiBuffer.RemoveAt(0);

            bool match = _konamiBuffer.Count == _konami.Length;
            if (match)
            {
                for (int i = 0; i < _konami.Length; i++)
                    if (_konamiBuffer[i] != _konami[i]) { match = false; break; }
            }
            if (match)
            {
                _mascotNyanActive = !_mascotNyanActive;
                if (_mascotNyanActive)
                {
                    Mascot?.ActivateNyanMode();
                    OnNyanTriggered?.Invoke();
                }
                else Mascot?.SetBaseState("idle");
                _konamiBuffer.Clear();
            }
        }

        private void CheckIdleState()
        {
            try
            {
                var idle = GetSystemIdle();
                if (idle > 90 && Mascot != null && Mascot.CurrentState == "idle")
                    Mascot.SetBaseState("idle_sunbath");
                else if (idle < 70 && Mascot != null && Mascot.CurrentState == "idle_sunbath")
                    Mascot.SetBaseState("idle");
            }
            catch { }
        }

        private double GetSystemIdle()
        {
            try
            {
                var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
                if (GetLastInputInfo(ref lastInput))
                {
                    var idle = (uint)Environment.TickCount - lastInput.dwTime;
                    return Math.Min(100, idle / 600.0);
                }
            }
            catch { }
            return 0;
        }

        private void CheckBattery()
        {
            try
            {
                var status = System.Windows.Forms.SystemInformation.PowerStatus;
                float percent = status.BatteryLifePercent;
                bool low = percent >= 0 && percent < 0.15f;
                Mascot?.OnLowBattery(low);
            }
            catch { }
        }

        private void CheckWeather()
        {
            try
            {
                int month = DateTime.Now.Month;
                if (month == 12 || month == 1 || month == 2)
                    Mascot?.OnWeatherChanged("snow");
                else if (month == 3 || month == 4 || month == 5 || month == 10 || month == 11)
                    Mascot?.OnWeatherChanged("rain");
                else
                    Mascot?.OnWeatherChanged("clear_sun");
            }
            catch { }
        }

        private void CheckScreenshot()
        {
            try
            {
                if (System.Windows.Forms.Clipboard.ContainsImage())
                {
                    var img = System.Windows.Forms.Clipboard.GetImage();
                    if (img != null && (DateTime.Now - _lastScreenshotCheck).TotalSeconds > 1)
                    {
                        _lastScreenshotCheck = DateTime.Now;
                        Mascot?.OnScreenshot();
                    }
                }
            }
            catch { }
        }

        public void TriggerDarknessEasterEgg()
        {
            Mascot?.ActivateDarknessMode();
        }

        public void OnMuteChanged(bool muted)
        {
            Mascot?.OnMuteChanged(muted);
        }

        // ---- Başarım bildirimi ----
        // Overlay sağa doğru genişler, başarım ismi 2 saniye görünür ve kaybolur.
        public void ShowAchievementNotification(string achievementTitle)
        {
            Dispatcher.Invoke(() =>
            {
                TxtAchievementTitle.Text = achievementTitle;
                AchievementPanel.Visibility = Visibility.Visible;
                this.UpdateLayout();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    AchievementPanel.Visibility = Visibility.Collapsed;
                    this.UpdateLayout();
                };
                timer.Start();
            });
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    }
}