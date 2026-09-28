using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace ControlCenter
{
    public class MascotState
    {
        public string File { get; set; } = "";
        public int Frames { get; set; } = 1;
        public int Fps { get; set; } = 6;
        public bool Loop { get; set; } = true;
        public string? NextState { get; set; }
    }

    public class MascotConfig
    {
        public string Name { get; set; } = "Kosmo";
        public string Version { get; set; } = "1.0";
        public string DefaultState { get; set; } = "idle";
        public int FrameWidth { get; set; } = 64;
        public int FrameHeight { get; set; } = 64;
        public Dictionary<string, MascotState> States { get; set; } = new();
        public Dictionary<string, string> Holidays { get; set; } = new();
    }

    public class MascotController
    {
        private readonly Image _target;
        private readonly string _mascotFolder;
        private MascotConfig _config = new();
        private readonly Dictionary<string, BitmapSource[]> _framesCache = new();
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _nightWatchTimer;
        private string _currentState = "idle";
        private int _currentFrame = 0;
        private DateTime _lastClickTime = DateTime.MinValue;
        private int _clickCount = 0;
        private string _baseState = "idle";
        private bool _tempStateActive = false;

        public event Action<string>? OnStateChanged;
        public event Action<int>? OnClicked;
        public event Action? OnRageTriggered;
        public event Action? OnScreenshotTriggered;

        public MascotController(Image target)
        {
            _target = target;
            _mascotFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mascot");
            LoadConfig();
            _timer = new DispatcherTimer();
            _timer.Tick += (s, e) => NextFrame();

            // Her 5 saniyede bir maskot tepkileri kontrol edilir (gece uykusu zorlaması)
            _nightWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _nightWatchTimer.Tick += (s, e) => EnforceNightTime();
            _nightWatchTimer.Start();
        }

        private void LoadConfig()
        {
            try
            {
                var jsonPath = Path.Combine(_mascotFolder, "mascot.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    _config = JsonConvert.DeserializeObject<MascotConfig>(json) ?? new MascotConfig();
                }
            }
            catch { _config = new MascotConfig(); }

            _currentState = _config.DefaultState;
            _baseState = _config.DefaultState;
        }

        private BitmapSource[]? LoadFrames(string stateName)
        {
            if (_framesCache.TryGetValue(stateName, out var cached))
                return cached;
            if (!_config.States.TryGetValue(stateName, out var st))
                return null;

            var filePath = Path.Combine(_mascotFolder, st.File);
            if (!File.Exists(filePath))
            {
                if (stateName != _config.DefaultState)
                    return LoadFrames(_config.DefaultState);
                return null;
            }

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(filePath);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                int fw = _config.FrameWidth, fh = _config.FrameHeight;
                int totalCols = Math.Max(1, bmp.PixelWidth / fw);
                int totalFrames = Math.Min(st.Frames, totalCols);

                var frames = new BitmapSource[totalFrames];
                for (int i = 0; i < totalFrames; i++)
                {
                    var rect = new Int32Rect(i * fw, 0, fw, fh);
                    var cropped = new CroppedBitmap(bmp, rect);
                    cropped.Freeze();
                    frames[i] = cropped;
                }
                _framesCache[stateName] = frames;
                return frames;
            }
            catch { return null; }
        }

        public void Start()
        {
            SetState(DetermineInitialState());
        }

        private static bool IsNightTime()
        {
            int hour = DateTime.Now.Hour;
            return hour >= 20 || hour < 6;
        }

        private string DetermineInitialState()
        {
            // Gece 20:00 - 06:00 arası uyku; uygulama açılsa dahi uyanmaz
            if (IsNightTime())
                return "sleep";

            var holiday = GetHolidayState();
            if (holiday != null)
                return holiday;

            return "wakeup";
        }

        private void EnforceNightTime()
        {
            try
            {
                if (IsNightTime())
                {
                    // Gece boyunca hangi durumda olursa olsun zorla uyku
                    _tempStateActive = false;
                    if (_currentState != "sleep")
                        SetState("sleep");
                }
                else
                {
                    if (_currentState == "sleep" && !_tempStateActive)
                        SetState(_baseState == "sleep" ? "idle" : _baseState);
                }
            }
            catch { }
        }

        private string? GetHolidayState()
        {
            string today = DateTime.Now.ToString("MM-dd");
            if (_config.Holidays.TryGetValue(today, out var state))
                return state;
            return null;
        }

        public void SetState(string stateName)
        {
            // Gece ise her durum isteği sleep'e çevrilir
            if (IsNightTime())
                stateName = "sleep";

            if (!_config.States.ContainsKey(stateName))
                stateName = _config.DefaultState;

            _currentState = stateName;
            _currentFrame = 0;
            _tempStateActive = false;

            var frames = LoadFrames(stateName);
            if (frames == null || frames.Length == 0)
            {
                _timer.Stop();
                return;
            }

            _target.Source = frames[0];
            var st = _config.States[stateName];
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, st.Fps));
            _timer.Start();

            OnStateChanged?.Invoke(stateName);
        }

        public void SetTemporaryState(string stateName, int durationMs = 0)
        {
            // Gece geçici animasyonlar oynatılmaz
            if (IsNightTime()) return;
            if (!_config.States.ContainsKey(stateName)) return;
            _tempStateActive = true;
            SetStateInternal(stateName);

            if (durationMs > 0)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                t.Tick += (s, e) =>
                {
                    t.Stop();
                    _tempStateActive = false;
                    SetState(_baseState);
                };
                t.Start();
            }
        }

        private void SetStateInternal(string stateName)
        {
            _currentState = stateName;
            _currentFrame = 0;
            var frames = LoadFrames(stateName);
            if (frames == null || frames.Length == 0) return;
            _target.Source = frames[0];
            var st = _config.States[stateName];
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, st.Fps));
            _timer.Start();
            OnStateChanged?.Invoke(stateName);
        }

        public void SetBaseState(string stateName)
        {
            // Gece uyanmayı engelle: base state değişmez, ekran uyku kalır
            if (IsNightTime())
            {
                SetState("sleep");
                return;
            }

            _baseState = stateName;
            if (!_tempStateActive)
                SetState(stateName);
        }

        private void NextFrame()
        {
            var frames = LoadFrames(_currentState);
            if (frames == null || frames.Length == 0) { _timer.Stop(); return; }

            _currentFrame++;
            if (_currentFrame >= frames.Length)
            {
                var st = _config.States[_currentState];
                if (st.Loop)
                {
                    _currentFrame = 0;
                }
                else
                {
                    _timer.Stop();
                    var next = st.NextState ?? _baseState;
                    _tempStateActive = false;
                    SetState(next);
                    return;
                }
            }
            _target.Source = frames[_currentFrame];
        }

        // ---- Etkileşimler ----

        public void HandleClick()
        {
            // Gece tıklama maskotu uyandırmaz
            if (IsNightTime()) return;

            var now = DateTime.Now;
            if ((now - _lastClickTime).TotalSeconds < 1.5)
                _clickCount++;
            else
                _clickCount = 1;
            _lastClickTime = now;

            OnClicked?.Invoke(_clickCount);

            if (_clickCount >= 10)
            {
                _clickCount = 0;
                OnRageTriggered?.Invoke();
                SetTemporaryState("confused", 2000);
            }
            else
            {
                SetTemporaryState("jump", 1200);
            }
        }

        public void OnScreenshot()
        {
            if (IsNightTime()) return;
            OnScreenshotTriggered?.Invoke();
            SetTemporaryState("screenshot", 1500);
        }

        public void OnMuteChanged(bool muted)
        {
            if (IsNightTime()) return;
            if (muted)
                SetBaseState("muted");
            else if (_baseState == "muted")
                SetBaseState("idle");
        }

        public void OnLowBattery(bool low)
        {
            if (IsNightTime()) return;
            if (low)
                SetBaseState("lowbattery");
            else if (_baseState == "lowbattery")
                SetBaseState("idle");
        }

        public void OnWeatherChanged(string weather)
        {
            if (IsNightTime()) return;
            switch (weather)
            {
                case "rain": SetBaseState("rain"); break;
                case "snow": SetBaseState("snow"); break;
                case "clear_sun": SetBaseState("idle_sunbath"); break;
                default: SetBaseState("idle"); break;
            }
        }

        public void ActivateNyanMode()
        {
            if (IsNightTime()) return;
            SetBaseState("nyan");
        }

        public void ActivateDarknessMode()
        {
            if (IsNightTime()) return;
            SetTemporaryState("darkness", 3000);
        }

        public void CheckHoliday()
        {
            if (IsNightTime()) return;
            var h = GetHolidayState();
            if (h != null) SetBaseState(h);
        }

        // ---- Başarım kutlaması ----
        public void Celebrate()
        {
            // Gece kutlama oynatılmaz (bildirim yine de gösterilir)
            if (IsNightTime()) return;

            string stateName = "celebrate";
            if (!_config.States.ContainsKey(stateName))
                stateName = _config.States.ContainsKey("celebration") ? "celebration" : "jump";
            SetTemporaryState(stateName, 2500);
        }

        public void Stop()
        {
            _timer.Stop();
            _nightWatchTimer.Stop();
        }
        public string CurrentState => _currentState;
    }
}