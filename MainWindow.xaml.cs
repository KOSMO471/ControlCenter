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
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ControlCenter
{
    public partial class MainWindow : Window
    {
        private UserData _userData = new UserData();
        private readonly string _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userdata.json");
        private bool _isSearching = false;

        private PerformanceCounter? _cpuCounter, _ramCounter, _netDownCounter, _netUpCounter;
        private Dictionary<string, PerformanceCounter> _processCpuCounters = new Dictionary<string, PerformanceCounter>();
        private MMDeviceEnumerator? _deviceEnumerator;
        private MMDevice? _defaultPlaybackDevice;
        private Process? _activeConsoleProcess;
        private StreamWriter? _consoleInputWriter;
        private DispatcherTimer? _pomodoroTimer, _systemTimer, _pingTimer, _clipboardTimer, _processTimer, _audioTimer;
        private int _pomodoroSeconds = 1500;
        private bool _pomodoroRunning = false;
        private string _currentEditingFilePath = "";
        private ObservableCollection<string> _clipboardItems = new ObservableCollection<string>();
        private string _lastClipboardText = "";
        private DateTime _lastToastTime = DateTime.MinValue;
        private ObservableCollection<AudioSessionInfo> _audioSessions = new ObservableCollection<AudioSessionInfo>();

        private Computer _computer = new Computer();
        private List<IHardware> _hardwareList = new List<IHardware>();
        private IHardware? _cpuHardware;
        private IHardware? _gpuHardware;

        private TaskbarIcon? _trayIcon;
        private OverlayWindow? _overlayWindow;
        private int _fps = 0, _frameCount = 0;
        private DateTime _lastFpsUpdate = DateTime.Now;
        private long _pingMs = 0;
        private bool _isOverlayVisible = true, _showFpsPing = true;
        private bool _isAdmin = false;

        private DispatcherTimer? _uptimeTimer;
        private DispatcherTimer? _muteWatchTimer;
        private DispatcherTimer? _ghostTimer;
        private DispatcherTimer? _activityTimer;
        private DateTime _muteStartTime = DateTime.MinValue;
        private DateTime _lastActivityTime = DateTime.Now;
        private DateTime _trayHideTime = DateTime.MinValue;
        private bool _isInTray = false;
        private long _weekendSecondsToday = 0;
        private DateTime _lastWeekendReset = DateTime.Today;

        private DispatcherTimer? _bluetoothUsageTimer;
        private GlobalSystemMediaTransportControlsSessionManager? _mediaSessionManager;
        private DispatcherTimer? _bluetoothMediaTimer;
        private Border? _btMediaTitleHost;
        private TextBlock? _btMediaTitleBlock;
        private TextBlock? _btMediaArtistBlock;
        private Image? _btMediaArtImage;

        private readonly List<Achievement> _achievements = new()
        {
            new Achievement { Id = "nightowl",     Title = "Gece Kuşu",              Description = "Gece 03:00–05:00 arası bilgisayarı açık tut" },
            new Achievement { Id = "earlybird",    Title = "Erken Kalkan Yol Alır",  Description = "Sabah 06:00'dan önce bilgisayarı aç" },
            new Achievement { Id = "marathon",     Title = "Maratoncu",              Description = "Uygulamayı aralıksız 8 saat açık tut" },
            new Achievement { Id = "timetraveler", Title = "Zaman Yolcusu",          Description = "Yılbaşı, Cadılar Bayramı veya Sevgililer Günü'nde uygulamaya gir" },
            new Achievement { Id = "mute",         Title = "Sessizlik Mantrası",     Description = "Bilgisayar sesini tamamen kapatıp en az 1 saat kullan" },
            new Achievement { Id = "collector",    Title = "Dijital Koleksiyoncu",   Description = "Toplamda 10'dan fazla uygulama veya web sitesi kısayolu ekle" },
            new Achievement { Id = "devpreview",   Title = "Kod / Metin Editörü",    Description = "Dosya gezgininde .cs, .py veya .json dosyasını açıp kaydet" },
            new Achievement { Id = "ghost",        Title = "Hayalet",                Description = "Sistem tepsisinde 5 saat boyunca hiç dokunmadan çalışmasına izin ver" },
            new Achievement { Id = "weekend",      Title = "Hafta Sonu Savaşçısı",   Description = "Cumartesi ve Pazar toplamda 10 saatten fazla aktif zaman geçir" },
            new Achievement { Id = "workaholic",   Title = "İşe Mola Ver!",          Description = "Klavye/mouse durmaksızın 4 saat boyunca mola vermeden çalış" },
            new Achievement { Id = "customizer",   Title = "Arayüz Mimarı",          Description = "Panellerin yerini değiştir, kendi özel düzenini oluştur ve kaydet" },
            new Achievement { Id = "explorer",     Title = "Meraklı Köfte",          Description = "Uygulamadaki tüm sekmeleri en az bir kez tıklayıp incele" },

            // ==================== YENİ BAŞARIMLAR ====================
            new Achievement { Id = "commandlinerookie", Title = "Terminal Çaylağı",      Description = "Konsola ilk komutunu gönder" },
            new Achievement { Id = "shellmaster",      Title = "Kabuk Ustası",          Description = "Konsoldan toplam 100 komut gönder" },
            new Achievement { Id = "adminmode",        Title = "Yönetici Koltuğu",      Description = "Konsolu Yönetici modunda 10 kez başlat" },
            new Achievement { Id = "powershellfan",    Title = "PS Fanatiği",           Description = "Sadece PowerShell kullanarak 50 komut gönder" },
            new Achievement { Id = "wificracker",      Title = "Kablosuz Casus",        Description = "\"Wi-Fi Şifrem\" hızlı işlemini 5 kez kullan" },
            new Achievement { Id = "cleanfreak",       Title = "Temizlik Takıntısı",    Description = "\"Çöpleri Temizle\" işlemini 10 kez çalıştır" },
            new Achievement { Id = "updatemaniac",     Title = "Güncel Kal!",           Description = "\"Tam Güncelleme\" işlemini 5 kez çalıştır" },
            new Achievement { Id = "ghostmode",        Title = "İz Bırakmayan",         Description = "\"Ayak İzlerimi Sil\" işlemini 3 kez çalıştır" },
            new Achievement { Id = "softwaredealer",   Title = "Yazılım Taciri",        Description = "Hızlı İşlemler'den 5 kez uygulama indir" },
            new Achievement { Id = "consolemarathon",  Title = "Konsol Maratonu",       Description = "Tek bir oturumda 200 komut gönder" },
            new Achievement { Id = "filejourney",      Title = "Dosya Yolculuğu",       Description = "100 farklı dosyaya önizleme yap" },
            new Achievement { Id = "imagedetective",   Title = "Görsel Dedektifi",      Description = "50 farklı görsel dosyası aç" },
            new Achievement { Id = "cinephile",        Title = "Sinemasever",           Description = "Uygulama içinde 2 saat video izle" },
            new Achievement { Id = "pdfreader",        Title = "PDF Kurdu",             Description = "20 farklı PDF dosyası aç" },
            new Achievement { Id = "docxmaster",       Title = "Kelime Ustası",         Description = "10 farklı DOCX dosyasını oku" },
            new Achievement { Id = "codewhisperer",    Title = "Kod Fısıldayan",        Description = "5 farklı dilde (cs, py, js, json, xaml) dosya düzenleyip kaydet" },
            new Achievement { Id = "editor",           Title = "Editör",                Description = "Toplam 50 kez metin dosyası kaydet" },
            new Achievement { Id = "longtext",         Title = "Uzun Soluklu",          Description = "10.000+ karakterlik bir dosya kaydet" },
            new Achievement { Id = "archiveexplorer",  Title = "Arşiv Avcısı",          Description = "15 farklı sıkıştırılmış dosya türünü incele" },
            new Achievement { Id = "pomodorofan",      Title = "Pomodoro Fanatiği",     Description = "20 kez zamanlayıcı tamamla" },
            new Achievement { Id = "pomodoropro",      Title = "Odak Ustası",           Description = "Aralıksız 5 pomodoro tamamla" },
            new Achievement { Id = "mathgenius",       Title = "Matematik Dehası",      Description = "Hesap makinesiyle 100 işlem yap" },
            new Achievement { Id = "calcbreaker",      Title = "Hesap Kırıcı",          Description = "Hesap makinesinde hata mesajı al (geçersiz ifade gir)" },
            new Achievement { Id = "noteaddict",       Title = "Not Bağımlısı",         Description = "Notları 50 kez kaydet" },
            new Achievement { Id = "novelist",         Title = "Roman Yazarı",          Description = "Tek bir notta 5.000+ karakter yaz" },
            new Achievement { Id = "clipboardking",    Title = "Pano Kralı",            Description = "Pano geçmişinde 50 farklı öğe topla" },
            new Achievement { Id = "clipboardsweeper", Title = "Pano Süpürücüsü",       Description = "Pano geçmişini 10 kez temizle" },
            new Achievement { Id = "timerreset",       Title = "Sabır Taşı",            Description = "Zamanlayıcıyı 30 kez sıfırla" },
            new Achievement { Id = "executioner",      Title = "Cellat",                Description = "25 işlem sonlandır" },
            new Achievement { Id = "heavykiller",      Title = "Ağır Sıklet",           Description = "1 GB+ RAM kullanan bir işlemi sonlandır" },
            new Achievement { Id = "processwatcher",   Title = "Gözcü",                 Description = "İşlem listesini 30 kez yenile" },
            new Achievement { Id = "applauncher",      Title = "Başlatıcı",             Description = "Uygulama listesinden 20 kez uygulama başlat" },
            new Achievement { Id = "uninstaller",      Title = "Kaldırıcı",             Description = "10 uygulamayı geri dönüşüme gönder" },
            new Achievement { Id = "careful",          Title = "Dikkatli Kullanıcı",    Description = "Uygulama silerken 5 kez \"Hayır\" seç" },
            new Achievement { Id = "systemscan",       Title = "Sistem Tarayıcı",       Description = "Uygulama listesini 15 kez yenile" },
            new Achievement { Id = "memoryhunter",     Title = "Bellek Avcısı",         Description = "En yüksek RAM kullanan 10 farklı işlemi sonlandır" },
            new Achievement { Id = "volumedj",         Title = "Ses DJ'i",              Description = "Ses seviyesini 100 kez değiştir" },
            new Achievement { Id = "silentnight",      Title = "Sessiz Gece",           Description = "Sesi %0'a indirip 30 dakika öyle bırak" },
            new Achievement { Id = "maxvolume",        Title = "Full Ses",              Description = "Sesi %100'e çıkar" },
            new Achievement { Id = "precisedj",        Title = "Hassas Ayar",           Description = "Ses seviyesini 3 kez tam %50'ye ayarla" },
            new Achievement { Id = "muteunmute",       Title = "Sessizlik Oyunu",       Description = "Sesi 50 kez kapat/aç" },
            new Achievement { Id = "searchaddict",     Title = "Arama Bağımlısı",       Description = "Toplam 100 internet araması yap" },
            new Achievement { Id = "shortcutking",     Title = "Kısayol Kralı",         Description = "25 uygulama kısayolu ekle" },
            new Achievement { Id = "webmaster",        Title = "Web Ustası",            Description = "25 web sitesi kısayolu ekle" },
            new Achievement { Id = "dragger",          Title = "Sürükle Bırakçı",       Description = "Sürükle-bırak ile 10 kısayol ekle" },
            new Achievement { Id = "shortcutlauncher", Title = "Hızlı Başlatıcı",       Description = "Kısayollardan toplam 200 kez uygulama aç" },
            new Achievement { Id = "collectorpro",     Title = "Süper Koleksiyoncu",    Description = "50+ toplam kısayola sahip ol" },
            new Achievement { Id = "cleaner",          Title = "Düzenli",               Description = "25 kısayol sil" },
            new Achievement { Id = "btscanner",        Title = "Bluetooth Avcısı",      Description = "Bluetooth panelini 20 kez aç" },
            new Achievement { Id = "multidevice",      Title = "Çoklu Cihaz",           Description = "5 farklı Bluetooth cihazı eşleştir" },
            new Achievement { Id = "priorityuser",     Title = "Öncelikli Kullanıcı",   Description = "En az 3 cihazı öncelikli olarak işaretle" },
            new Achievement { Id = "btmarathon",       Title = "Bluetooth Maratonu",    Description = "Bir Bluetooth cihazını günlük 8 saat kullan" },
            new Achievement { Id = "mediajockey",      Title = "Medya Şefi",            Description = "Medya kontrollerini (prev/play/next) 50 kez kullan" },
            new Achievement { Id = "batterywatcher",   Title = "Pil Gözcüsü",           Description = "Bir Bluetooth cihazının pili %10'un altına düşsün" },
            new Achievement { Id = "btdevices",        Title = "Cihaz Koleksiyoncusu",  Description = "10 farklı Bluetooth cihazıyla en az 1'er kez bağlantı kur" },
            new Achievement { Id = "athlete",          Title = "Atlet",                 Description = "Toplam 100 saat uygulamayı açık tut" },
            new Achievement { Id = "loyaluser",        Title = "Sadık Kullanıcı",       Description = "Uygulamayı 30 farklı günde kullan" },
            new Achievement { Id = "returner",         Title = "Geri Dönen",            Description = "7 gün boyunca her gün uygulamayı aç" },
            new Achievement { Id = "morningperson",    Title = "Sabah İnsanı",          Description = "7 kez sabah 07:00'den önce uygulamayı aç" },
            new Achievement { Id = "nightwatch",       Title = "Gece Nöbeti",           Description = "Gece 00:00–04:00 arasında 5 kez uygulamayı kullan" },
            new Achievement { Id = "lunchbreak",       Title = "Öğle Molası",           Description = "Her gün 12:00–13:00 arası 5 kez uygulamayı kullan" },
            new Achievement { Id = "firstofmonth",     Title = "Ayın İlki",             Description = "Ayın 1'inde uygulamayı aç" },
            new Achievement { Id = "leapday",          Title = "Artık Gün",             Description = "29 Şubat'ta uygulamayı aç" },
            new Achievement { Id = "newyear",          Title = "Yeni Yıl Kuşağı",       Description = "3 farklı yılbaşında uygulamayı aç" },
            new Achievement { Id = "hibernator",       Title = "Kış Uykusu",            Description = "Uygulamayı 7 gün boyunca hiç açma, sonra tekrar aç" },
            new Achievement { Id = "startupuser",      Title = "Başlangıç Kullanıcısı", Description = "Windows ile Başlat'ı aktif et ve 3 kez başlangıçta çalıştır" },
            new Achievement { Id = "systemguard",      Title = "Sistem Bekçisi",        Description = "Sıcaklık/RAM uyarısı bildirimini 5 kez gör" },
            new Achievement { Id = "notifymaster",     Title = "Bildirim Ustası",       Description = "Toplam 25 bildirim al (başarım veya uyarı)" },
            new Achievement { Id = "allergyfree",      Title = "Alerjisiz",             Description = "Uygulamayı kullanırken 30 dakika boyunca hiç uyarı almadan çalış" },
            new Achievement { Id = "drawerlover",      Title = "Çekmece Dostu",         Description = "Sol paneli (drawer) 100 kez aç/kapat" },
            new Achievement { Id = "trayresident",     Title = "Tepside Yaşayan",       Description = "Toplam 50 saat sistem tepsisinde kal" },
            new Achievement { Id = "masterofall",      Title = "Her Şeyin Ustası",      Description = "Tüm sekme ve widgetları aynı gün içinde aç" },
            new Achievement { Id = "perfectionist",    Title = "Mükemmeliyetçi",        Description = "Tüm başarımların %75'ini aç" },
            new Achievement { Id = "completionist",    Title = "Tamamlayıcı",           Description = "%100 başarım tamamla" },
            new Achievement { Id = "firstachievement", Title = "İlk Adım",              Description = "İlk başarımı aç" },
            new Achievement { Id = "frequentuser",     Title = "Sık Kullanıcı",         Description = "7 gün boyunca her gün en az 1 saat kullan" },
            new Achievement { Id = "antivirus",        Title = "Sağlam Kullanıcı",      Description = "Bir hafta boyunca uygulama hiç çökmesin" },
            new Achievement { Id = "developer",        Title = "Geliştirici Modu",      Description = "50 kez metin editöründe kod dosyası kaydet" },
            new Achievement { Id = "themewanderer",    Title = "Tema Gezgini",          Description = "Tüm görünümleri aynı gün içinde en az 5'er kez aç" },
            new Achievement { Id = "overlaycorner",    Title = "Köşe Kapmaca",          Description = "Overlay'i 4 farklı köşeye sürükle" },
            new Achievement { Id = "overlaymode",      Title = "Mod Ustası",            Description = "Overlay'i Compact ↔ Detailed modları arasında 20 kez değiştir" },
            new Achievement { Id = "fpspingfan",       Title = "FPS/Ping Fanatiği",     Description = "FPS/Ping göstergesini 50 kez aç/kapat" },
            new Achievement { Id = "overlayresident",  Title = "Overlay Sakini",        Description = "Overlay'i 24 saat boyunca kapatmadan kullan" },
        };

        public MainWindow()
        {
            InitializeComponent();
            LoadUserData();
            InitHardwareMonitor();
            InitTrayIcon();
            InitSystemTimers();
            InitNetworkCounters();
            RefreshShortcuts();
            LoadNotes();
            CheckStartupModule();
            InitializeAudioDevice();
            InitClipboardManager();
            InitAudioMixer();
            InitProcessManager();
            InitAchievements();
            InitBluetoothTracking();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitOverlay();
            InitFpsCounter();
            InitPingTimer();
            TrackDailyUsage();

            if (_userData.StartWithWindows)
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
                _isInTray = true;
                _trayHideTime = DateTime.Now;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _userData.LastAppCloseTime = DateTime.Now;
            SaveUserData();

            if (_userData.TrayOnClose)
            {
                e.Cancel = true;
                this.Hide();
                _isInTray = true;
                _trayHideTime = DateTime.Now;
                _trayIcon?.ShowBalloonTip("Control Center", "Uygulama sistem tepsisinde çalışmaya devam ediyor.", BalloonIcon.Info);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_userData.TrayOnClose)
            {
                e.Cancel = true;
                this.Hide();
                _isInTray = true;
                _trayHideTime = DateTime.Now;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (this.IsVisible && this.WindowState != WindowState.Minimized)
                {
                    this.Hide();
                    _isInTray = true;
                    _trayHideTime = DateTime.Now;
                }
                else
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                    _isInTray = false;
                    _trayHideTime = DateTime.MinValue;
                }
            }
        }

        private void LoadUserData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    string json = File.ReadAllText(_dataFilePath);
                    _userData = JsonConvert.DeserializeObject<UserData>(json) ?? new UserData();
                }
            }
            catch { _userData = new UserData(); }

            ChkStartWithWindows.IsChecked = _userData.StartWithWindows;
        }

        private void SaveUserData()
        {
            try
            {
                _userData.StartWithWindows = ChkStartWithWindows.IsChecked ?? false;
                string json = JsonConvert.SerializeObject(_userData, Formatting.Indented);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veriler kaydedilemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadNotes() => TxtNotes.Text = _userData.Notes;
        private void SaveNotes() { _userData.Notes = TxtNotes.Text; SaveUserData(); }

        private async void RefreshShortcuts()
        {
            var appShortcuts = _userData.AppShortcuts.Where(s => !s.IsWeb).ToList();
            var webShortcuts = _userData.AppShortcuts.Where(s => s.IsWeb).ToList();

            ItemsAppShortcuts.ItemsSource = appShortcuts;
            ItemsWebShortcuts.ItemsSource = webShortcuts;

            foreach (var shortcut in webShortcuts)
            {
                if (shortcut.IconImage == null)
                    await LoadFaviconAsync(shortcut);
            }

            if (_userData.AppShortcuts.Count > 10)
            {
                _userData.ShortcutCollectorUnlocked = true;
                UnlockAchievement("collector");
            }
            if (_userData.AppShortcuts.Count >= 50)
                UnlockAchievement("collectorpro");
        }

        private async Task LoadFaviconAsync(AppShortcut shortcut)
        {
            try
            {
                if (!shortcut.IsWeb || string.IsNullOrEmpty(shortcut.Path)) return;
                var domain = new Uri(shortcut.Path).Host;
                var faviconUrl = $"https://www.google.com/s2/favicons?domain={domain}&sz=64";

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync(faviconUrl);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsByteArrayAsync();
                    var ms = new MemoryStream(data);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    shortcut.IconImage = bitmap;
                }
            }
            catch { }
        }

        private void BtnAddCustomApp_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Uygulamalar (*.exe)|*.exe" };
            if (ofd.ShowDialog() == true)
            {
                string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                var dialog = new InputDialog("Kısayol Adı", "Ad:", name);
                if (dialog.ShowDialog() == true)
                {
                    _userData.AppShortcuts.Add(new AppShortcut { Name = dialog.Answer1, Path = ofd.FileName, Icon = "🚀", IsWeb = false });
                    _userData.AppShortcutAddCount++;
                    if (_userData.AppShortcutAddCount >= 25) UnlockAchievement("shortcutking");
                    SaveUserData();
                    RefreshShortcuts();
                }
            }
        }

        private void BtnAddWebShortcut_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Web Sitesi", "Ad:", "", "URL:");
            if (dialog.ShowDialog() == true)
            {
                string url = dialog.Answer2;
                if (!url.StartsWith("http")) url = "https://" + url;
                _userData.AppShortcuts.Add(new AppShortcut { Name = dialog.Answer1, Path = url, Icon = "🌐", IsWeb = true });
                _userData.WebShortcutAddCount++;
                if (_userData.WebShortcutAddCount >= 25) UnlockAchievement("webmaster");
                SaveUserData();
                RefreshShortcuts();
            }
        }

        private void BtnDeleteShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AppShortcut sc)
            {
                _userData.AppShortcuts.Remove(sc);
                _userData.ShortcutDeleteCount++;
                if (_userData.ShortcutDeleteCount >= 25) UnlockAchievement("cleaner");
                SaveUserData();
                RefreshShortcuts();
            }
        }

        private void WheelApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    _userData.ShortcutLaunchCount++;
                    if (_userData.ShortcutLaunchCount >= 200) UnlockAchievement("shortcutlauncher");
                    SaveUserData();
                }
                catch (Exception ex) { MessageBox.Show($"Başlatılamadı: {ex.Message}"); }
            }
        }

        private void BtnSaveNotes_Click(object sender, RoutedEventArgs e)
        {
            SaveNotes();
            _userData.NotesSaveCount++;
            if (_userData.NotesSaveCount >= 50) UnlockAchievement("noteaddict");
            if (TxtNotes.Text.Length >= 5000) UnlockAchievement("novelist");
            SaveUserData();
            MessageBox.Show("Notlar kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void InitTrayIcon()
        {
            _trayIcon = new TaskbarIcon();
            _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _trayIcon.TrayMouseDoubleClick += (s, e) =>
            {
                this.Show(); this.WindowState = WindowState.Normal; this.Activate();
                _isInTray = false; _trayHideTime = DateTime.MinValue;
            };

            var menu = new ContextMenu();
            var itemShow = new MenuItem { Header = "Göster" };
            itemShow.Click += (s, e) =>
            {
                this.Show(); this.WindowState = WindowState.Normal; this.Activate();
                _isInTray = false; _trayHideTime = DateTime.MinValue;
            };
            menu.Items.Add(itemShow);

            var itemOverlay = new MenuItem { Header = "Overlay Göster" };
            itemOverlay.Click += (s, e) => ToggleOverlayVisibility();
            menu.Items.Add(itemOverlay);

            var itemGameMode = new MenuItem { Header = "🎮 Oyun Modu" };
            itemGameMode.Click += (s, e) => ActivateGameMode();
            menu.Items.Add(itemGameMode);

            var itemOptimize = new MenuItem { Header = "⚡ Optimize Et" };
            itemOptimize.Click += (s, e) => OptimizeSystem();
            menu.Items.Add(itemOptimize);

            var itemFpsPing = new MenuItem { Header = "📊 FPS/Ping Göster" };
            itemFpsPing.IsCheckable = true;
            itemFpsPing.IsChecked = _showFpsPing;
            itemFpsPing.Click += (s, e) =>
            {
                _showFpsPing = !_showFpsPing;
                _userData.FpsPingToggleCount++;
                if (_userData.FpsPingToggleCount >= 50) UnlockAchievement("fpspingfan");
                SaveUserData();
                itemFpsPing.IsChecked = _showFpsPing;
                if (_overlayWindow != null) _overlayWindow.SetFpsPingVisibility(_showFpsPing);
            };
            menu.Items.Add(itemFpsPing);

            var itemExit = new MenuItem { Header = "Çıkış" };
            itemExit.Click += (s, e) =>
            {
                _trayIcon.Dispose();
                _overlayWindow?.Close();
                Environment.Exit(0);
            };
            menu.Items.Add(itemExit);

            _trayIcon.ContextMenu = menu;
        }

        private void ToggleOverlayVisibility()
        {
            _isOverlayVisible = !_isOverlayVisible;
            if (_overlayWindow != null)
                _overlayWindow.Visibility = _isOverlayVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InitOverlay()
        {
            _overlayWindow = new OverlayWindow();
            _overlayWindow.Owner = this;
            _overlayWindow.Show();
            _userData.OverlayOpenTime = DateTime.Now;

            _overlayWindow.OnShowMainWindow += () =>
            {
                this.Show(); this.WindowState = WindowState.Normal; this.Activate();
                _isInTray = false; _trayHideTime = DateTime.MinValue;
            };
            _overlayWindow.OnGameMode += ActivateGameMode;
            _overlayWindow.OnOptimize += OptimizeSystem;
            _overlayWindow.OnToggleFpsPing += () =>
            {
                _showFpsPing = !_showFpsPing;
                _userData.FpsPingToggleCount++;
                if (_userData.FpsPingToggleCount >= 50) UnlockAchievement("fpspingfan");
                SaveUserData();
                _overlayWindow.SetFpsPingVisibility(_showFpsPing);
            };
            _overlayWindow.OnExit += () =>
            {
                _trayIcon?.Dispose();
                _overlayWindow?.Close();
                Environment.Exit(0);
            };
            _overlayWindow.OnReopenApp += () =>
            {
                this.Show(); this.WindowState = WindowState.Normal; this.Activate();
                _isInTray = false; _trayHideTime = DateTime.MinValue;
            };

            _overlayWindow.OnMascotClicked += HandleMascotClick;
            _overlayWindow.OnNyanTriggered += () => { _userData.NyanTriggered = true; UnlockAchievement("nyan"); };
            _overlayWindow.OnScreenshotTaken += () => { _userData.ScreenshotTaken = true; UnlockAchievement("snapshot"); };

            _overlayWindow.Visibility = _isOverlayVisible ? Visibility.Visible : Visibility.Collapsed;
            _overlayWindow.SetFpsPingVisibility(_showFpsPing);
        }

        private void InitFpsCounter()
        {
            CompositionTarget.Rendering += (s, e) =>
            {
                _frameCount++;
                var now = DateTime.Now;
                if ((now - _lastFpsUpdate).TotalSeconds >= 1)
                {
                    _fps = _frameCount;
                    _frameCount = 0;
                    _lastFpsUpdate = now;
                    if (_overlayWindow != null && _showFpsPing)
                        _overlayWindow.UpdateFPS(_fps);
                }
            };
        }

        private void InitPingTimer()
        {
            _pingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _pingTimer.Tick += async (s, e) =>
            {
                try
                {
                    using (var ping = new Ping())
                    {
                        var reply = await ping.SendPingAsync("8.8.8.8", 1000);
                        if (reply.Status == IPStatus.Success)
                            _pingMs = reply.RoundtripTime;
                        else
                            _pingMs = -1;
                    }
                }
                catch { _pingMs = -1; }
                if (_overlayWindow != null && _showFpsPing)
                    _overlayWindow.UpdatePing(_pingMs);
            };
            _pingTimer.Start();
        }

        private void ActivateGameMode()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", "-setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")
                { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);

                var killList = new[] { "chrome", "spotify", "msedge", "discord", "steam", "EpicGamesLauncher" };
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        if (killList.Any(k => proc.ProcessName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                            proc.Kill();
                    }
                    catch { }
                }
                MessageBox.Show("Oyun Modu aktif! Gereksiz uygulamalar kapatıldı, güç planı Yüksek Performans olarak ayarlandı.", "Oyun Modu", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Oyun Modu hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OptimizeSystem()
        {
            try
            {
                string tempPath = Path.GetTempPath();
                DeleteDirectoryContents(tempPath);

                string winTemp = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Temp");
                if (Directory.Exists(winTemp)) DeleteDirectoryContents(winTemp);

                var psi = new ProcessStartInfo("cmd", "/c echo y | %windir%\\System32\\rundll32.exe advapi32.dll,ProcessIdleTasks")
                { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);

                MessageBox.Show("Sistem optimize edildi: geçici dosyalar temizlendi ve bellek boşaltma işlemi başlatıldı.", "Optimize Et", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Optimizasyon hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteDirectoryContents(string path)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", System.IO.SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (var dir in Directory.GetDirectories(path))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
            catch { }
        }

        private void InitHardwareMonitor()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
                    IsNetworkEnabled = true,
                    IsStorageEnabled = true
                };
                _computer.Open();
                _hardwareList = _computer.Hardware.ToList();
                foreach (var hw in _hardwareList) hw.Update();

                _cpuHardware = _hardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                _gpuHardware = _hardwareList.FirstOrDefault(h =>
                    h.HardwareType == HardwareType.GpuNvidia ||
                    h.HardwareType == HardwareType.GpuAmd ||
                    h.HardwareType == HardwareType.GpuIntel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Donanım monitörü başlatılamadı:\n{ex.Message}\n\nSistem takibi devre dışı bırakılacak.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                _computer = null;
                _hardwareList = new List<IHardware>();
                _cpuHardware = null;
                _gpuHardware = null;
            }
        }

        private (float? temp, float? load, float? fan) GetCpuMetrics()
        {
            float? temp = null, load = null, fan = null;
            try
            {
                if (_cpuHardware != null)
                {
                    _cpuHardware.Update();
                    foreach (var sensor in _cpuHardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                            temp = sensor.Value.Value;
                        else if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                            load = sensor.Value.Value;
                        else if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                            fan = sensor.Value.Value;
                    }
                }
            }
            catch { }
            return (temp, load, fan);
        }

        private (float? temp, float? load, float? fan, float? vramUsed, float? vramTotal) GetGpuMetrics()
        {
            float? temp = null, load = null, fan = null, vramUsed = null, vramTotal = null;
            try
            {
                if (_gpuHardware != null)
                {
                    _gpuHardware.Update();
                    foreach (var sensor in _gpuHardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                            temp = sensor.Value.Value;
                        else if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                            load = sensor.Value.Value;
                        else if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                            fan = sensor.Value.Value;
                        else if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Used") && sensor.Value.HasValue)
                            vramUsed = sensor.Value.Value;
                        else if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("Memory Total") && sensor.Value.HasValue)
                            vramTotal = sensor.Value.Value;
                    }
                }
            }
            catch { }
            return (temp, load, fan, vramUsed, vramTotal);
        }

        private void ShowToastNotification(string title, string message)
        {
            try
            {
                _trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Warning);
                _userData.NotificationShownCount++;
                if (_userData.NotificationShownCount >= 25) UnlockAchievement("notifymaster");
                _userData.SystemAlertShownCount++;
                if (_userData.SystemAlertShownCount >= 5) UnlockAchievement("systemguard");
                SaveUserData();
            }
            catch { }
        }

        private void InitNetworkCounters()
        {
            try
            {
                var category = new PerformanceCounterCategory("Network Interface");
                var instance = category.GetInstanceNames().FirstOrDefault();
                if (instance != null)
                {
                    _netDownCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instance);
                    _netUpCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ağ sayaçları başlatılamadı:\n{ex.Message}", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                _netDownCounter = null;
                _netUpCounter = null;
            }
        }

        private void InitSystemTimers()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PerformanceCounter oluşturulamadı:\n{ex.Message}\n\nCPU ve RAM takibi devre dışı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                _cpuCounter = null;
                _ramCounter = null;
            }

            _systemTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _systemTimer.Tick += (s, e) =>
            {
                TxtClock.Text = DateTime.Now.ToString("HH:mm");
                TxtDate.Text = DateTime.Now.ToString("dd MMMM yyyy dddd");

                try
                {
                    double cpu = 0, ram = 0;
                    if (_cpuCounter != null)
                        cpu = Math.Round(_cpuCounter.NextValue(), 1);
                    PbCpuUsage.Value = cpu;
                    TxtCpuPercent.Text = $"{cpu:F0}%";

                    var cpuMetrics = GetCpuMetrics();
                    if (cpuMetrics.temp.HasValue)
                        TxtCpuTemp.Text = $"{cpuMetrics.temp.Value:F0}°C";

                    if (_ramCounter != null)
                        ram = Math.Round(_ramCounter.NextValue(), 1);
                    PbRamUsage.Value = ram;
                    TxtRamPercent.Text = $"{ram:F0}%";

                    var driveC = new DriveInfo("C");
                    var driveD = new DriveInfo("D");
                    double diskC = Math.Round((double)(driveC.TotalSize - driveC.AvailableFreeSpace) / driveC.TotalSize * 100, 1);
                    double diskD = driveD.IsReady ? Math.Round((double)(driveD.TotalSize - driveD.AvailableFreeSpace) / driveD.TotalSize * 100, 1) : 0;
                    PbDiskC.Value = diskC;
                    PbDiskD.Value = diskD;
                    TxtDiskCPercent.Text = $"{diskC:F0}%";
                    TxtDiskDPercent.Text = $"{diskD:F0}%";

                    if (_netDownCounter != null && _netUpCounter != null)
                    {
                        double down = _netDownCounter.NextValue() / 1024;
                        double up = _netUpCounter.NextValue() / 1024;
                        TxtNetSpeed.Text = $"⬇️ {down:F1} KB/s | ⬆️ {up:F1} KB/s";
                    }

                    if (_overlayWindow != null)
                    {
                        var gpuMetrics = GetGpuMetrics();
                        double cpuTemp = cpuMetrics.temp ?? 0;
                        double gpuTemp = gpuMetrics.temp ?? 0;
                        double down = _netDownCounter?.NextValue() / 1024 ?? 0;
                        double up = _netUpCounter?.NextValue() / 1024 ?? 0;
                        _overlayWindow.UpdateHardwareInfo(cpu, ram, cpuTemp, gpuTemp, down, up);
                        _overlayWindow.UpdateGpuInfo(gpuMetrics.load ?? 0, gpuMetrics.vramUsed ?? 0, gpuMetrics.vramTotal ?? 0, gpuMetrics.fan ?? 0);
                    }

                    if ((DateTime.Now - _lastToastTime).TotalMinutes >= 10)
                    {
                        bool alert = false;
                        string alertMsg = "";
                        if (cpuMetrics.temp.HasValue && cpuMetrics.temp.Value > 85)
                        {
                            alert = true;
                            alertMsg += $"🔥 CPU sıcaklığı {cpuMetrics.temp.Value:F0}°C (85°C üzeri!)\n";
                        }
                        if (ram > 90)
                        {
                            alert = true;
                            alertMsg += $"💾 RAM kullanımı %{ram:F0} (%90 üzeri!)\n";
                        }
                        if (alert)
                        {
                            _lastToastTime = DateTime.Now;
                            ShowToastNotification("⚠️ Sistem Uyarısı", alertMsg);
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.AppendAllText("error.log", $"{DateTime.Now}: Timer Error: {ex}\n");
                    }
                    catch { }
                }
            };
            _systemTimer.Start();
        }

        private void InitializeAudioDevice()
        {
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();
                _defaultPlaybackDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (_defaultPlaybackDevice != null)
                {
                    float currentVol = _defaultPlaybackDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
                    SliderMasterVolume.Value = currentVol;
                    TxtVolumeVal.Text = $"%{(int)currentVol}";
                }
            }
            catch { TxtVolumeVal.Text = "N/A"; }
        }

        private void SliderMasterVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_defaultPlaybackDevice != null)
            {
                try
                {
                    float newVolume = (float)Math.Round(SliderMasterVolume.Value / 100.0, 2);
                    _defaultPlaybackDevice.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;
                    int displayValue = (int)Math.Round(newVolume * 100);
                    TxtVolumeVal.Text = $"%{displayValue}";

                    _userData.VolumeChangeCount++;
                    if (_userData.VolumeChangeCount >= 100) UnlockAchievement("volumedj");

                    if (displayValue == 100) { _userData.MaxVolumeReached = true; UnlockAchievement("maxvolume"); }
                    if (displayValue == 50)
                    {
                        _userData.Precise50Count++;
                        if (_userData.Precise50Count >= 3) UnlockAchievement("precisedj");
                    }

                    bool muted = _defaultPlaybackDevice.AudioEndpointVolume.Mute || displayValue <= 0;
                    if (muted)
                    {
                        if (_userData.SilentNightStart == DateTime.MinValue)
                            _userData.SilentNightStart = DateTime.Now;
                    }
                    else
                    {
                        _userData.SilentNightStart = DateTime.MinValue;
                    }

                    SaveUserData();
                }
                catch { }
            }
        }

        private void InitAudioMixer()
        {
            _audioTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _audioTimer.Tick += (s, e) => UpdateAudioSessions();
            _audioTimer.Start();
        }

        private void UpdateAudioSessions()
        {
            try
            {
                if (_defaultPlaybackDevice == null) return;
                var sessionManager = _defaultPlaybackDevice.AudioSessionManager;
                var sessions = sessionManager.Sessions;

                var existingIds = _audioSessions.Select(a => a.SessionIdentifier).ToHashSet();

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.IsSystemSoundsSession) continue;

                    var sessionId = session.GetSessionIdentifier;
                    var existing = _audioSessions.FirstOrDefault(a => a.SessionIdentifier == sessionId);
                    if (existing != null)
                    {
                        existing.Volume = session.SimpleAudioVolume.Volume * 100;
                        existing.IsMuted = session.SimpleAudioVolume.Mute;
                        existing.AudioVolume = session.SimpleAudioVolume;
                        existingIds.Remove(sessionId);
                    }
                    else
                    {
                        var info = new AudioSessionInfo
                        {
                            DisplayName = GetSessionDisplayName(session),
                            Volume = session.SimpleAudioVolume.Volume * 100,
                            IsMuted = session.SimpleAudioVolume.Mute,
                            AudioVolume = session.SimpleAudioVolume,
                            SessionIdentifier = sessionId
                        };
                        Dispatcher.Invoke(() => _audioSessions.Add(info));
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    foreach (var id in existingIds)
                    {
                        var toRemove = _audioSessions.FirstOrDefault(a => a.SessionIdentifier == id);
                        if (toRemove != null) _audioSessions.Remove(toRemove);
                    }
                });
            }
            catch { }
        }

        private string GetSessionDisplayName(AudioSessionControl session)
        {
            try
            {
                var displayName = session.DisplayName;
                if (!string.IsNullOrEmpty(displayName)) return displayName;

                var sessionIdentifier = session.GetSessionIdentifier;
                if (!string.IsNullOrEmpty(sessionIdentifier) && sessionIdentifier.Contains("\\"))
                    return Path.GetFileName(sessionIdentifier);

                return "Bilinmeyen Uygulama";
            }
            catch { return "Bilinmeyen Uygulama"; }
        }

        private void BtnSwitchAudioDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var devices = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                var currentDevice = devices.FirstOrDefault(d => d.ID == _defaultPlaybackDevice?.ID);
                if (currentDevice == null) return;

                var currentIndex = devices.ToList().IndexOf(currentDevice);
                var nextIndex = (currentIndex + 1) % devices.Count;
                var nextDevice = devices[nextIndex];

                if (nextDevice != null)
                {
                    _defaultPlaybackDevice = nextDevice;
                    InitializeAudioDevice();
                    MessageBox.Show($"Ses cihazı değiştirildi: {nextDevice.FriendlyName}", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateAudioSessions();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ses cihazı değiştirilemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitProcessManager()
        {
            _processTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _processTimer.Tick += (s, e) => UpdateProcesses();
            _processTimer.Start();
            UpdateProcesses();
        }

        private void UpdateProcesses()
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                    .OrderByDescending(p => p.WorkingSet64)
                    .Take(20)
                    .ToList();

                var list = new List<ProcessInfo>();
                foreach (var p in processes)
                {
                    try
                    {
                        string counterKey = p.ProcessName + p.Id;
                        if (!_processCpuCounters.ContainsKey(counterKey))
                        {
                            try
                            {
                                var counter = new PerformanceCounter("Process", "% Processor Time", p.ProcessName, true);
                                counter.NextValue();
                                _processCpuCounters[counterKey] = counter;
                            }
                            catch { }
                        }

                        double cpu = 0;
                        if (_processCpuCounters.TryGetValue(counterKey, out var cpuCounter))
                        {
                            try { cpu = cpuCounter.NextValue() / Environment.ProcessorCount; } catch { }
                        }

                        list.Add(new ProcessInfo
                        {
                            ProcessName = p.ProcessName.Length > 20 ? p.ProcessName.Substring(0, 20) : p.ProcessName,
                            Id = p.Id,
                            CpuUsage = $"{cpu:F1}%",
                            Memory = $"{(p.WorkingSet64 / 1024 / 1024):F0} MB",
                            Process = p
                        });
                    }
                    catch { }
                }

                var activeKeys = processes.Select(p => p.ProcessName + p.Id).ToHashSet();
                var keysToRemove = _processCpuCounters.Keys.Where(k => !activeKeys.Contains(k)).ToList();
                foreach (var key in keysToRemove)
                {
                    _processCpuCounters[key].Dispose();
                    _processCpuCounters.Remove(key);
                }

                Dispatcher.Invoke(() => LvProcesses.ItemsSource = list);
            }
            catch { }
        }

        private void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            UpdateProcesses();
            _userData.ProcessRefreshCount++;
            if (_userData.ProcessRefreshCount >= 30) UnlockAchievement("processwatcher");
            SaveUserData();
        }

        private void BtnKillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProcessInfo info)
            {
                try
                {
                    if (info.Process != null && !info.Process.HasExited)
                    {
                        bool heavy = info.Process.WorkingSet64 >= 1073741824L;
                        long ramMB = info.Process.WorkingSet64 / 1024 / 1024;
                        info.Process.Kill();
                        info.Process.WaitForExit(1000);

                        _userData.ProcessKillCount++;
                        if (_userData.ProcessKillCount >= 25) UnlockAchievement("executioner");

                        if (heavy)
                        {
                            _userData.HeavyProcessKilled = true;
                            UnlockAchievement("heavykiller");
                        }

                        _userData.TopMemoryKilledProcesses.Add(info.ProcessName + info.Id);
                        if (_userData.TopMemoryKilledProcesses.Count >= 10) UnlockAchievement("memoryhunter");

                        SaveUserData();

                        MessageBox.Show($"İşlem sonlandırıldı: {info.ProcessName} (ID: {info.Id})", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateProcesses();
                    }
                    else
                    {
                        MessageBox.Show("İşlem zaten sonlandırılmış.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"İşlem sonlandırılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnTaskManager_Click(object sender, RoutedEventArgs e) => SwitchView(ViewTaskManager);

        private void SetupFileManager()
        {
            try
            {
                TreeFolders.Items.Clear();
                var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var rootNode = CreateTreeItem(userFolder);
                TreeFolders.Items.Add(rootNode);
                rootNode.IsExpanded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SetupFileManager hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TreeViewItem CreateTreeItem(string path)
        {
            try
            {
                var item = new TreeViewItem { Header = Path.GetFileName(path), Tag = path };
                item.Items.Add(null);
                item.Expanded += TreeItem_Expanded;
                return item;
            }
            catch { return new TreeViewItem { Header = "Hata", Tag = path }; }
        }

        private void TreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = (TreeViewItem)sender;
                if (item.Items.Count == 1 && item.Items[0] == null)
                {
                    item.Items.Clear();
                    string path = (string)item.Tag;
                    try
                    {
                        foreach (var dir in Directory.GetDirectories(path))
                            item.Items.Add(CreateTreeItem(dir));
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"TreeItem_Expanded hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TreeFolders_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                if (TreeFolders.SelectedItem is TreeViewItem item)
                {
                    string path = (string)item.Tag;
                    LoadFolderFiles(path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Klasör seçimi hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFolderFiles(string path)
        {
            try
            {
                LstFiles.Items.Clear();
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.GetFiles())
                {
                    long size = file.Length;
                    LstFiles.Items.Add(new FileItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        Extension = file.Extension.ToLower(),
                        Size = FormatBytes(size),
                        RawSize = size
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosyalar yüklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1073741824) return $"{bytes / 1073741824:F2} GB";
            if (bytes >= 1048576) return $"{bytes / 1048576:F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024:F2} KB";
            return $"{bytes} B";
        }

        private void LstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstFiles.SelectedItem is FileItem file)
                OpenInAppViewer(file);
        }

        private void OpenInAppViewer(FileItem file)
        {
            ImgViewer.Visibility = Visibility.Collapsed;
            PdfViewer.Visibility = Visibility.Collapsed;
            TxtViewer.Visibility = Visibility.Collapsed;
            BtnSaveTextFile.Visibility = Visibility.Collapsed;
            TxtReaderPlaceholder.Visibility = Visibility.Collapsed;
            VideoViewer.Visibility = Visibility.Collapsed;
            VideoViewer.Source = null;

            string ext = file.Extension;
            _currentEditingFilePath = file.FullPath;

            try
            {
                _userData.PreviewedFiles.Add(file.FullPath);
                if (_userData.PreviewedFiles.Count >= 100) UnlockAchievement("filejourney");

                if (new[] { ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".flv", ".webm" }.Contains(ext))
                {
                    VideoViewer.Source = new Uri(file.FullPath);
                    VideoViewer.Visibility = Visibility.Visible;
                    VideoViewer.Play();
                    SaveUserData();
                    return;
                }

                if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".tiff", ".webp" }.Contains(ext))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(file.FullPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ImgViewer.Source = bitmap;
                    ImgViewer.Visibility = Visibility.Visible;

                    _userData.OpenedImages.Add(file.FullPath);
                    if (_userData.OpenedImages.Count >= 50) UnlockAchievement("imagedetective");
                    SaveUserData();
                    return;
                }

                if (ext == ".pdf")
                {
                    PdfViewer.Navigate(new Uri(file.FullPath));
                    PdfViewer.Visibility = Visibility.Visible;

                    _userData.OpenedPdfs.Add(file.FullPath);
                    if (_userData.OpenedPdfs.Count >= 20) UnlockAchievement("pdfreader");
                    SaveUserData();
                    return;
                }

                if (ext == ".docx")
                {
                    try
                    {
                        using (var wordDoc = WordprocessingDocument.Open(file.FullPath, false))
                        {
                            var body = wordDoc.MainDocumentPart?.Document.Body;
                            if (body != null)
                            {
                                var text = new StringBuilder();
                                foreach (var para in body.Elements<Paragraph>())
                                {
                                    foreach (var run in para.Elements<Run>())
                                    {
                                        foreach (var textElement in run.Elements<Text>())
                                        {
                                            text.Append(textElement.Text);
                                        }
                                    }
                                    text.AppendLine();
                                }
                                TxtViewer.Text = text.ToString();
                                TxtViewer.Visibility = Visibility.Visible;
                                BtnSaveTextFile.Visibility = Visibility.Collapsed;

                                _userData.OpenedDocx.Add(file.FullPath);
                                if (_userData.OpenedDocx.Count >= 10) UnlockAchievement("docxmaster");
                                SaveUserData();
                            }
                            else
                            {
                                TxtReaderPlaceholder.Text = "📄 DOCX dosyası okunamadı (boş veya hasarlı).";
                                TxtReaderPlaceholder.Visibility = Visibility.Visible;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        TxtReaderPlaceholder.Text = $"⚠️ DOCX okuma hatası: {ex.Message}";
                        TxtReaderPlaceholder.Visibility = Visibility.Visible;
                    }
                    return;
                }

                if (new[] { ".txt", ".json", ".cs", ".xml", ".log", ".html", ".css", ".js", ".xaml", ".py", ".md", ".yaml", ".yml", ".jodn" }.Contains(ext))
                {
                    TxtViewer.Text = File.ReadAllText(file.FullPath);
                    TxtViewer.Visibility = Visibility.Visible;
                    BtnSaveTextFile.Visibility = Visibility.Visible;
                    SaveUserData();
                    return;
                }

                if (new[] { ".rar", ".zip", ".7z", ".tar", ".gz" }.Contains(ext))
                {
                    TxtReaderPlaceholder.Text = $"📦 {ext.ToUpper()} arşivi (Önizleme desteklenmiyor)";
                    TxtReaderPlaceholder.Visibility = Visibility.Visible;

                    _userData.ExploredArchives.Add(ext);
                    if (_userData.ExploredArchives.Count >= 15) UnlockAchievement("archiveexplorer");
                    SaveUserData();
                    return;
                }

                if (new[] { ".doc", ".xlsx", ".xls", ".pptx", ".ppt" }.Contains(ext))
                {
                    TxtReaderPlaceholder.Text = $"📄 {ext.ToUpper()} dosyası (Önizleme için Office gerekli)";
                    TxtReaderPlaceholder.Visibility = Visibility.Visible;
                    return;
                }

                TxtReaderPlaceholder.Text = $"❓ {ext.ToUpper()} için önizleme yok";
                TxtReaderPlaceholder.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                TxtReaderPlaceholder.Text = $"⚠️ Önizleme hatası: {ex.Message}";
                TxtReaderPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void BtnSaveTextFile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentEditingFilePath) && File.Exists(_currentEditingFilePath))
            {
                try
                {
                    File.WriteAllText(_currentEditingFilePath, TxtViewer.Text);

                    _userData.TextFileSaveCount++;
                    if (_userData.TextFileSaveCount >= 50) UnlockAchievement("editor");
                    if (TxtViewer.Text.Length >= 10000) UnlockAchievement("longtext");

                    string ext = Path.GetExtension(_currentEditingFilePath).ToLower().TrimStart('.');
                    if (ext == "cs" || ext == "py" || ext == "js" || ext == "json" || ext == "xaml")
                    {
                        _userData.SavedCodeExtensions.Add(ext);
                        if (_userData.SavedCodeExtensions.Count >= 5) UnlockAchievement("codewhisperer");
                    }
                    if (ext == "cs" || ext == "py" || ext == "json")
                    {
                        _userData.DevPreviewUnlocked = true;
                        UnlockAchievement("devpreview");
                    }

                    SaveUserData();
                    MessageBox.Show("Dosya başarıyla kaydedildi!", "Başarılı");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dosya kaydedilemedi: {ex.Message}");
                }
            }
        }

        private void BtnConsoleView_Click(object sender, RoutedEventArgs e)
        {
            SwitchView(ViewConsole);
            StartConsoleProcess("cmd.exe");
        }

        private void StartConsoleProcess(string exe)
        {
            KillConsoleProcess();
            TxtConsoleOutput.Clear();
            TxtConsoleOutput.AppendText($"--- {exe.ToUpper()} OTURUMU BAŞLATILDI ---\n\n");
            _userData.ConsoleSessionCommandCount = 0;

            if (_isAdmin)
            {
                _userData.ConsoleAdminStartCount++;
                if (_userData.ConsoleAdminStartCount >= 10) UnlockAchievement("adminmode");
                SaveUserData();
            }

            _activeConsoleProcess = new Process();
            _activeConsoleProcess.StartInfo.FileName = exe;
            _activeConsoleProcess.StartInfo.UseShellExecute = false;
            _activeConsoleProcess.StartInfo.RedirectStandardInput = true;
            _activeConsoleProcess.StartInfo.RedirectStandardOutput = true;
            _activeConsoleProcess.StartInfo.RedirectStandardError = true;
            _activeConsoleProcess.StartInfo.CreateNoWindow = true;

            if (_isAdmin)
            {
                _activeConsoleProcess.StartInfo.Verb = "runas";
                _activeConsoleProcess.StartInfo.UseShellExecute = true;
                _activeConsoleProcess.StartInfo.RedirectStandardInput = false;
                _activeConsoleProcess.StartInfo.RedirectStandardOutput = false;
                _activeConsoleProcess.StartInfo.RedirectStandardError = false;
            }

            _activeConsoleProcess.OutputDataReceived += (s, ev) =>
            {
                if (ev.Data != null)
                    Dispatcher.Invoke(() => TxtConsoleOutput.AppendText(ev.Data + "\n"));
            };
            _activeConsoleProcess.ErrorDataReceived += (s, ev) =>
            {
                if (ev.Data != null)
                    Dispatcher.Invoke(() => TxtConsoleOutput.AppendText("[HATA] " + ev.Data + "\n"));
            };

            try
            {
                _activeConsoleProcess.Start();
                if (!_isAdmin)
                {
                    _consoleInputWriter = _activeConsoleProcess.StandardInput;
                    _activeConsoleProcess.BeginOutputReadLine();
                    _activeConsoleProcess.BeginErrorReadLine();
                }
            }
            catch (Exception ex)
            {
                TxtConsoleOutput.AppendText($"[HATA] Konsol başlatılamadı: {ex.Message}\n");
            }
        }

        private void KillConsoleProcess()
        {
            if (_activeConsoleProcess != null)
            {
                try
                {
                    if (!_activeConsoleProcess.HasExited)
                        _activeConsoleProcess.Kill();
                    _activeConsoleProcess.Dispose();
                }
                catch { }
                finally
                {
                    _activeConsoleProcess = null;
                    _consoleInputWriter = null;
                }
            }
        }

        private void BtnSendConsole_Click(object sender, RoutedEventArgs e)
        {
            if (_consoleInputWriter != null && !string.IsNullOrWhiteSpace(TxtConsoleInput.Text))
            {
                string cmd = TxtConsoleInput.Text;
                TxtConsoleOutput.AppendText($"> {cmd}\n");
                _consoleInputWriter.WriteLine(cmd);
                _consoleInputWriter.Flush();
                TxtConsoleInput.Clear();

                _userData.ConsoleCommandCount++;
                if (_userData.ConsoleCommandCount == 1) UnlockAchievement("commandlinerookie");
                if (_userData.ConsoleCommandCount >= 100) UnlockAchievement("shellmaster");

                if (RadPowerShell.IsChecked == true)
                {
                    _userData.PowerShellCommandCount++;
                    if (_userData.PowerShellCommandCount >= 50) UnlockAchievement("powershellfan");
                }

                _userData.ConsoleSessionCommandCount++;
                if (_userData.ConsoleSessionCommandCount >= 200) UnlockAchievement("consolemarathon");

                SaveUserData();
            }
        }

        private void TxtConsoleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnSendConsole_Click(null, null!);
        }

        private void ConsoleType_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewConsole.Visibility == Visibility.Visible)
            {
                string exe = (RadPowerShell.IsChecked == true) ? "powershell.exe" : "cmd.exe";
                StartConsoleProcess(exe);
            }
        }

        private void ChkAdminMode_Changed(object sender, RoutedEventArgs e)
        {
            _isAdmin = ChkAdminMode.IsChecked == true;
            if (_isAdmin)
            {
                TxtConsoleOutput.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x0A, 0x0A));
                TxtConsoleOutput.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00));
            }
            else
            {
                TxtConsoleOutput.Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0C));
                TxtConsoleOutput.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x66));
            }
        }

        private void BtnQuickActions_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var wifiItem = new MenuItem { Header = "📶 Wi-Fi Şifrem" };
            wifiItem.Click += async (s, ev) => await ExecuteQuickAction("wifi");
            menu.Items.Add(wifiItem);

            var cleanItem = new MenuItem { Header = "🧹 Çöpleri Temizle" };
            cleanItem.Click += async (s, ev) => await ExecuteQuickAction("clean");
            menu.Items.Add(cleanItem);

            var updateItem = new MenuItem { Header = "🔄 Tam Güncelleme" };
            updateItem.Click += async (s, ev) => await ExecuteQuickAction("update");
            menu.Items.Add(updateItem);

            var traceItem = new MenuItem { Header = "🕵️ Ayak İzlerimi Sil" };
            traceItem.Click += async (s, ev) => await ExecuteQuickAction("trace");
            menu.Items.Add(traceItem);

            var installItem = new MenuItem { Header = "📥 Uygulama İndir" };
            installItem.Click += async (s, ev) => await ExecuteQuickAction("install");
            menu.Items.Add(installItem);

            menu.IsOpen = true;
        }

        private async Task ExecuteQuickAction(string action)
        {
            try
            {
                switch (action)
                {
                    case "wifi":
                        _userData.WifiActionCount++;
                        if (_userData.WifiActionCount >= 5) UnlockAchievement("wificracker");
                        SaveUserData();
                        var wifiOutput = await RunPowerShellCommand("(netsh wlan show profiles | Select-String ':') -replace '.*:\\s*','' | ForEach-Object { $p=$_.Trim(); $k=(netsh wlan show profile name=$p key=clear | Select-String 'Key Content').ToString().Split(':')[1].Trim(); Write-Output \"$p : $k\" }");
                        TxtConsoleOutput.AppendText($"\n[Wi-Fi Şifreleri]\n{wifiOutput}\n");
                        break;

                    case "clean":
                        _userData.CleanActionCount++;
                        if (_userData.CleanActionCount >= 10) UnlockAchievement("cleanfreak");
                        SaveUserData();
                        string tempPath = Path.GetTempPath();
                        DeleteDirectoryContents(tempPath);
                        string winTemp = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Temp");
                        if (Directory.Exists(winTemp)) DeleteDirectoryContents(winTemp);
                        TxtConsoleOutput.AppendText("\n[Çöpleri Temizle] Gereksiz dosyalar çöpe atıldı, bilgisayarın ferahladı!\n");
                        break;

                    case "update":
                        _userData.UpdateActionCount++;
                        if (_userData.UpdateActionCount >= 5) UnlockAchievement("updatemaniac");
                        SaveUserData();
                        TxtConsoleOutput.AppendText("\n[Tam Güncelleme] Tüm uygulamaların tek tıkla en son sürüme güncelleniyor...\n");
                        var updateOutput = await RunCommandAsync("winget", "upgrade --all --include-unknown");
                        TxtConsoleOutput.AppendText(updateOutput + "\n");
                        break;

                    case "trace":
                        _userData.TraceActionCount++;
                        if (_userData.TraceActionCount >= 3) UnlockAchievement("ghostmode");
                        SaveUserData();
                        var traceOutput = await RunPowerShellCommand(@"
Remove-Item -Path ""$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Cache\*"" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ""$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache\*"" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ""$env:APPDATA\Mozilla\Firefox\Profiles\*\cache2\*"" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ""$env:APPDATA\Microsoft\Windows\Recent\*"" -Recurse -Force -ErrorAction SilentlyContinue
Set-Clipboard -Value $null
Write-Output 'Tarayıcı önbellekleri, son açılan dosyalar ve pano temizlendi.'
");
                        TxtConsoleOutput.AppendText($"\n[Ayak İzlerimi Sil] {traceOutput}\n");
                        break;

                    case "install":
                        var dialog = new InputDialog("Uygulama İndir", "Uygulama adı:", "");
                        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Answer1))
                        {
                            _userData.InstallActionCount++;
                            if (_userData.InstallActionCount >= 5) UnlockAchievement("softwaredealer");
                            SaveUserData();
                            TxtConsoleOutput.AppendText($"\n[Uygulama İndir] {dialog.Answer1} indiriliyor...\n");
                            var installOutput = await RunCommandAsync("winget", $"install {dialog.Answer1} --accept-package-agreements --accept-source-agreements");
                            TxtConsoleOutput.AppendText(installOutput + "\n");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                TxtConsoleOutput.AppendText($"[HATA] {ex.Message}\n");
            }
        }

        private async Task<string> RunPowerShellCommand(string command)
        {
            var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{command}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            string output = await proc.StandardOutput.ReadToEndAsync();
            string error = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return string.IsNullOrEmpty(error) ? output : output + "\n[HATA] " + error;
        }

        private async Task<string> RunCommandAsync(string cmd, string args)
        {
            var psi = new ProcessStartInfo(cmd, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            string output = await proc.StandardOutput.ReadToEndAsync();
            string error = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return string.IsNullOrEmpty(error) ? output : output + "\n[HATA] " + error;
        }

        private void TxtConsoleOutput_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void TxtConsoleOutput_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string path = files[0];
                    if (_consoleInputWriter != null)
                    {
                        TxtConsoleOutput.AppendText($"> {path}\n");
                        _consoleInputWriter.WriteLine(path);
                        _consoleInputWriter.Flush();
                    }
                }
            }
        }

        private void TxtConsoleInput_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void TxtConsoleInput_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    TxtConsoleInput.Text = files[0];
                }
            }
        }

        private async void BtnAppManager_Click(object sender, RoutedEventArgs e)
        {
            SwitchView(ViewAppManager);
            await LoadAppsAsync();
        }

        private async Task LoadAppsAsync()
        {
            await Task.Run(() =>
            {
                var appList = new List<AppInfo>();
                var folders = new List<string>
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };
                foreach (var folder in folders)
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (var subDir in Directory.GetDirectories(folder))
                    {
                        try
                        {
                            var exeFiles = Directory.GetFiles(subDir, "*.exe", System.IO.SearchOption.TopDirectoryOnly);
                            if (exeFiles.Length > 0)
                            {
                                string name = new DirectoryInfo(subDir).Name;
                                appList.Add(new AppInfo { Name = name, Path = exeFiles[0] });
                            }
                        }
                        catch { }
                    }
                }
                var uniqueApps = appList.GroupBy(a => a.Name).Select(g => g.First()).ToList();
                Dispatcher.Invoke(() => LvApps.ItemsSource = uniqueApps);
            });
        }

        private void BtnRefreshApps_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadAppsAsync();
            _userData.AppListRefreshCount++;
            if (_userData.AppListRefreshCount >= 15) UnlockAchievement("systemscan");
            SaveUserData();
        }

        private void BtnLaunchApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppInfo app)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(app.Path) { UseShellExecute = true });
                    _userData.AppLaunchCount++;
                    if (_userData.AppLaunchCount >= 20) UnlockAchievement("applauncher");
                    SaveUserData();
                }
                catch (Exception ex) { MessageBox.Show($"Uygulama başlatılamadı: {ex.Message}"); }
            }
        }

        private void BtnUninstallApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppInfo app)
            {
                var result = MessageBox.Show($"'{app.Name}' silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        FileSystem.DeleteFile(app.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        _userData.AppUninstallCount++;
                        if (_userData.AppUninstallCount >= 10) UnlockAchievement("uninstaller");
                        SaveUserData();
                        MessageBox.Show("Uygulama başarıyla geri dönüşüme gönderildi.");
                        _ = LoadAppsAsync();
                    }
                    catch (Exception ex) { MessageBox.Show($"Silinirken hata: {ex.Message}\nYetki yok veya dosya kullanımda.", "Hata"); }
                }
                else
                {
                    _userData.AppUninstallCancelCount++;
                    if (_userData.AppUninstallCancelCount >= 5) UnlockAchievement("careful");
                    SaveUserData();
                }
            }
        }

        private void BtnSetTimer_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtTimerInput.Text, out int minutes) && minutes > 0)
            {
                _pomodoroSeconds = minutes * 60;
                TimeSpan t = TimeSpan.FromSeconds(_pomodoroSeconds);
                TxtTimer.Text = t.ToString(@"mm\:ss");
            }
        }

        private void BtnStartTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_pomodoroTimer == null)
            {
                _pomodoroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _pomodoroTimer.Tick += (s, ev) =>
                {
                    if (_pomodoroSeconds > 0)
                    {
                        _pomodoroSeconds--;
                        TimeSpan t = TimeSpan.FromSeconds(_pomodoroSeconds);
                        TxtTimer.Text = t.ToString(@"mm\:ss");

                        if (_pomodoroSeconds == 0)
                        {
                            _pomodoroTimer?.Stop();
                            _pomodoroRunning = false;
                            _userData.PomodoroCompletedCount++;
                            _userData.PomodoroConsecutiveCount++;
                            if (_userData.PomodoroCompletedCount >= 20) UnlockAchievement("pomodorofan");
                            if (_userData.PomodoroConsecutiveCount >= 5) UnlockAchievement("pomodoropro");
                            SaveUserData();
                        }
                    }
                };
            }

            if (_pomodoroRunning)
            {
                _pomodoroTimer.Stop();
                _pomodoroRunning = false;
            }
            else
            {
                _pomodoroTimer.Start();
                _pomodoroRunning = true;
            }
        }

        private void BtnResetTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_pomodoroTimer != null) _pomodoroTimer.Stop();
            _pomodoroRunning = false;
            _userData.TimerResetCount++;
            _userData.PomodoroConsecutiveCount = 0;
            if (_userData.TimerResetCount >= 30) UnlockAchievement("timerreset");
            SaveUserData();
            BtnSetTimer_Click(sender, e);
        }

        private void Calc_Btn(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                TxtCalcDisplay.Text += btn.Content.ToString();
        }

        private void Calc_Clear(object sender, RoutedEventArgs e) => TxtCalcDisplay.Clear();

        private void Calc_Backspace(object sender, RoutedEventArgs e)
        {
            if (TxtCalcDisplay.Text.Length > 0)
                TxtCalcDisplay.Text = TxtCalcDisplay.Text.Substring(0, TxtCalcDisplay.Text.Length - 1);
        }

        private void Calc_Equal(object sender, RoutedEventArgs e)
        {
            try
            {
                string expression = TxtCalcDisplay.Text;
                var result = new DataTable().Compute(expression, null);
                TxtCalcDisplay.Text = result.ToString();

                _userData.CalculatorOpCount++;
                if (_userData.CalculatorOpCount >= 100) UnlockAchievement("mathgenius");
                SaveUserData();
            }
            catch
            {
                TxtCalcDisplay.Text = "Hata";
                _userData.CalculatorErrorTriggered = true;
                UnlockAchievement("calcbreaker");
                SaveUserData();
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e) { if (!_isSearching) { _isSearching = true; ExecuteSearch(); } }
        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { e.Handled = true; if (!_isSearching) { _isSearching = true; ExecuteSearch(); } }
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtSearch.Text == "İnternette veya Sistemde Ara...") TxtSearch.Clear();
        }

        private void ExecuteSearch()
        {
            string url = _userData.SearchEngine switch
            {
                "Bing" => $"https://www.bing.com/search?q={Uri.EscapeDataString(TxtSearch.Text)}",
                "DuckDuckGo" => $"https://duckduckgo.com/?q={Uri.EscapeDataString(TxtSearch.Text)}",
                "YouTube" => $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(TxtSearch.Text)}",
                _ => $"https://www.google.com/search?q={Uri.EscapeDataString(TxtSearch.Text)}"
            };
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            _isSearching = false;

            _userData.SearchCount++;
            if (_userData.SearchCount >= 100) UnlockAchievement("searchaddict");
            SaveUserData();
        }

        private void SwitchView(UIElement target)
        {
            foreach (UIElement child in DynamicViewContainer.Children)
                child.Visibility = Visibility.Collapsed;
            if (target != ViewConsole) KillConsoleProcess();
            target.Visibility = Visibility.Visible;

            string viewName = (target as FrameworkElement)?.Name ?? "unknown";
            if (!string.IsNullOrEmpty(viewName) && viewName != "unknown")
            {
                if (!_userData.VisitedViews.Contains(viewName))
                {
                    _userData.VisitedViews.Add(viewName);
                }

                string[] requiredViews = { "ViewFileManager", "ViewWidgets", "ViewConsole", "ViewAppManager", "ViewTaskManager" };
                if (requiredViews.All(v => _userData.VisitedViews.Contains(v)))
                {
                    _userData.ExplorerUnlocked = true;
                    UnlockAchievement("explorer");
                }

                // themewanderer / masterofall - aynı gün içinde her görünümü en az 5 kez
                if (_userData.SameDayViewDate.Date != DateTime.Today)
                {
                    _userData.SameDayViewCounts.Clear();
                    _userData.SameDayViewDate = DateTime.Today;
                }
                if (!_userData.SameDayViewCounts.ContainsKey(viewName))
                    _userData.SameDayViewCounts[viewName] = 0;
                _userData.SameDayViewCounts[viewName]++;

                string[] allViews = { "ViewFileManager", "ViewWidgets", "ViewConsole", "ViewAppManager", "ViewTaskManager" };
                if (allViews.All(v => _userData.SameDayViewCounts.TryGetValue(v, out var c) && c >= 5))
                    UnlockAchievement("themewanderer");
                if (allViews.All(v => _userData.SameDayViewCounts.ContainsKey(v)))
                    UnlockAchievement("masterofall");

                SaveUserData();
            }
        }

        private void BtnDosyalar_Click(object sender, RoutedEventArgs e)
        {
            SwitchView(ViewFileManager);
            SetupFileManager();
        }

        private void BtnWidgets_Click(object sender, RoutedEventArgs e) => SwitchView(ViewWidgets);

        private void BtnToggleDrawer_Click(object sender, RoutedEventArgs e)
        {
            DrawerPanel.Visibility = DrawerPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

            _userData.CustomizerUnlocked = true;
            UnlockAchievement("customizer");

            _userData.DrawerToggleCount++;
            if (_userData.DrawerToggleCount >= 100) UnlockAchievement("drawerlover");
            SaveUserData();
        }

        private void BtnCreator_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/KOSMO471") { UseShellExecute = true });

        private void CheckStartupModule() => SetupFileManager();

        private void InitAchievements()
        {
            foreach (var a in _achievements)
            {
                if (_userData.UnlockedAchievements.Contains(a.Id))
                    a.Unlocked = true;
            }

            _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _uptimeTimer.Tick += (s, e) => CheckUptimeAchievements();
            _uptimeTimer.Start();

            _muteWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _muteWatchTimer.Tick += (s, e) => CheckMuteAchievement();
            _muteWatchTimer.Start();

            _ghostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _ghostTimer.Tick += (s, e) => CheckGhostAchievement();
            _ghostTimer.Start();

            _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _activityTimer.Tick += (s, e) => CheckActivityAchievements();
            _activityTimer.Start();

            CheckStartupAchievements();
        }

        private void TrackDailyUsage()
        {
            try
            {
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                if (!_userData.UsedDays.Contains(today))
                    _userData.UsedDays.Add(today);

                if (_userData.LastUsedDate.Date == DateTime.Today.AddDays(-1))
                    _userData.ConsecutiveDaysUsed++;
                else if (_userData.LastUsedDate.Date != DateTime.Today)
                    _userData.ConsecutiveDaysUsed = 1;
                _userData.LastUsedDate = DateTime.Today;

                if (_userData.UsedDays.Count >= 30) UnlockAchievement("loyaluser");
                if (_userData.ConsecutiveDaysUsed >= 7) UnlockAchievement("returner");

                var now = DateTime.Now;
                if (now.Hour < 7) { _userData.MorningLaunchCount++; if (_userData.MorningLaunchCount >= 7) UnlockAchievement("morningperson"); }
                if (now.Hour >= 0 && now.Hour < 4) { _userData.NightUsageCount++; if (_userData.NightUsageCount >= 5) UnlockAchievement("nightwatch"); }
                if (now.Hour >= 12 && now.Hour < 13) { _userData.LunchUsageCount++; if (_userData.LunchUsageCount >= 5) UnlockAchievement("lunchbreak"); }

                if (now.Day == 1) UnlockAchievement("firstofmonth");
                if (now.Month == 2 && now.Day == 29) UnlockAchievement("leapday");
                if (now.Month == 1 && now.Day == 1)
                {
                    _userData.NewYearYears.Add(now.Year);
                    if (_userData.NewYearYears.Count >= 3) UnlockAchievement("newyear");
                }

                if (_userData.LastAppCloseTime != DateTime.MinValue &&
                    (DateTime.Now - _userData.LastAppCloseTime).TotalDays >= 7)
                {
                    UnlockAchievement("hibernator");
                }

                if (_userData.StartWithWindows)
                {
                    _userData.StartupRunCount++;
                    if (_userData.StartupRunCount >= 3) UnlockAchievement("startupuser");
                }

                SaveUserData();
            }
            catch { }
        }

        private void CheckStartupAchievements()
        {
            var now = DateTime.Now;
            int hour = now.Hour;

            if (hour >= 3 && hour < 5)
            {
                _userData.NightOwlUnlocked = true;
                UnlockAchievement("nightowl");
            }

            if (hour < 6)
            {
                _userData.EarlyBirdUnlocked = true;
                UnlockAchievement("earlybird");
            }

            string today = now.ToString("MM-dd");
            if (today == "12-31" || today == "10-31" || today == "02-14")
            {
                if (!_userData.SeenHolidays.Contains(today))
                {
                    _userData.SeenHolidays.Add(today);
                }
                UnlockAchievement("timetraveler");
            }

            int month = now.Month;
            if (month == 3 || month == 4 || month == 5 || month == 10 || month == 11)
            {
                _userData.RainDanceUnlocked = true;
                UnlockAchievement("raindance");
            }

            SaveUserData();
        }

        private void CheckUptimeAchievements()
        {
            try
            {
                _userData.TotalUptimeSeconds += 60;
                SaveUserData();

                if (_userData.TotalUptimeSeconds >= 8 * 3600)
                    UnlockAchievement("marathon");
                if (_userData.TotalUptimeSeconds >= 100 * 3600)
                    UnlockAchievement("athlete");

                // Antivirus - 1 hafta çökmeden çalıştıysa
                if ((DateTime.Now - _userData.FirstLaunchTime).TotalDays >= 7)
                    UnlockAchievement("antivirus");

                // Silent Night - 30 dakika boyunca ses %0
                if (_userData.SilentNightStart != DateTime.MinValue &&
                    (DateTime.Now - _userData.SilentNightStart).TotalMinutes >= 30 &&
                    !_userData.SilentNightUnlocked)
                {
                    _userData.SilentNightUnlocked = true;
                    UnlockAchievement("silentnight");
                }

                // Perfectionist / Completionist
                int total = _achievements.Count;
                int unlocked = _achievements.Count(x => x.Unlocked);
                double percent = total > 0 ? (unlocked * 100.0 / total) : 0;
                if (percent >= 75) UnlockAchievement("perfectionist");
                if (unlocked >= total && total > 0) UnlockAchievement("completionist");

                // Tray resident - tepsinde toplam süre
                if (_isInTray && _trayHideTime != DateTime.MinValue)
                {
                    _userData.TrayResidentSeconds += 60;
                    if (_userData.TrayResidentSeconds >= 50 * 3600) UnlockAchievement("trayresident");
                    SaveUserData();
                }

                // Overlay resident - 24 saat
                if (_userData.OverlayOpenTime != DateTime.MinValue &&
                    (DateTime.Now - _userData.OverlayOpenTime).TotalHours >= 24 &&
                    _overlayWindow != null && _overlayWindow.IsVisible)
                {
                    UnlockAchievement("overlayresident");
                }
            }
            catch { }
        }

        private void CheckMuteAchievement()
        {
            try
            {
                if (_defaultPlaybackDevice == null) return;

                bool isMuted = _defaultPlaybackDevice.AudioEndpointVolume.Mute ||
                               _defaultPlaybackDevice.AudioEndpointVolume.MasterVolumeLevelScalar <= 0.01f;

                if (isMuted)
                {
                    if (_muteStartTime == DateTime.MinValue)
                        _muteStartTime = DateTime.Now;
                    else
                    {
                        var elapsed = (DateTime.Now - _muteStartTime).TotalSeconds;
                        if (elapsed >= 3600 && !_userData.MuteHourUnlocked)
                        {
                            _userData.MuteHourUnlocked = true;
                            UnlockAchievement("mute");
                        }
                    }
                }
                else
                {
                    _muteStartTime = DateTime.MinValue;
                }
            }
            catch { }
        }

        private void CheckGhostAchievement()
        {
            try
            {
                if (_isInTray && _trayHideTime != DateTime.MinValue)
                {
                    var elapsed = (DateTime.Now - _trayHideTime).TotalSeconds;
                    _userData.TrayIdleSeconds = (long)elapsed;
                    if (elapsed >= 5 * 3600)
                    {
                        _userData.GhostUserUnlocked = true;
                        UnlockAchievement("ghost");
                    }
                }
            }
            catch { }
        }

        private void CheckActivityAchievements()
        {
            try
            {
                var now = DateTime.Now;

                if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
                {
                    if (_lastWeekendReset.Date != now.Date)
                    {
                        _weekendSecondsToday = 0;
                        _lastWeekendReset = now.Date;
                    }

                    var idle = GetSystemIdleSeconds();
                    if (idle < 300)
                    {
                        _weekendSecondsToday += 30;
                        _userData.WeekendActiveSeconds = _weekendSecondsToday;
                        if (_weekendSecondsToday >= 10 * 3600)
                        {
                            _userData.WeekendWarriorUnlocked = true;
                            UnlockAchievement("weekend");
                        }
                    }
                }

                var idleSec = GetSystemIdleSeconds();
                if (idleSec < 60)
                {
                    _userData.ContinuousActiveSeconds += 30;
                    if (_userData.ContinuousActiveSeconds >= 4 * 3600)
                    {
                        _userData.WorkaholicUnlocked = true;
                        UnlockAchievement("workaholic");
                    }
                }
                else if (idleSec > 300)
                {
                    _userData.ContinuousActiveSeconds = 0;
                }

                SaveUserData();
            }
            catch { }
        }

        private double GetSystemIdleSeconds()
        {
            try
            {
                var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
                if (GetLastInputInfo(ref lastInput))
                    return ((uint)Environment.TickCount - lastInput.dwTime) / 1000.0;
            }
            catch { }
            return 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private void HandleMascotClick(int rapidClickCount)
        {
            _userData.MascotTotalClicks++;
            SaveUserData();

            if (_userData.MascotTotalClicks >= 100)
                UnlockAchievement("idler");

            if (rapidClickCount >= 10)
            {
                _userData.MascotRageClicks++;
                SaveUserData();
                UnlockAchievement("rage");
            }
        }

        public void UnlockAchievement(string id)
        {
            var a = _achievements.FirstOrDefault(x => x.Id == id);
            if (a == null || a.Unlocked) return;

            a.Unlocked = true;
            a.UnlockTime = DateTime.Now;

            if (!_userData.UnlockedAchievements.Contains(id))
                _userData.UnlockedAchievements.Add(id);

            // İlk başarım kilidi
            if (!_userData.UnlockedAchievements.Contains("firstachievement") && id != "firstachievement")
            {
                _userData.UnlockedAchievements.Add("firstachievement");
                var first = _achievements.FirstOrDefault(x => x.Id == "firstachievement");
                if (first != null && !first.Unlocked)
                {
                    first.Unlocked = true;
                    first.UnlockTime = DateTime.Now;
                }
            }

            SaveUserData();

            _trayIcon?.ShowBalloonTip("🏆 Başarım Kazanıldı!", $"{a.Icon} {a.Title}\n{a.Description}", BalloonIcon.Info);

            _overlayWindow?.Mascot?.Celebrate();
            _overlayWindow?.ShowAchievementNotification($"{a.Icon} {a.Title}");

            _userData.NotificationShownCount++;
            SaveUserData();
        }

        private void BtnAchievements_Click(object sender, RoutedEventArgs e)
        {
            var popup = new Window
            {
                Title = "🏆 Başarımlar",
                Width = 520,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (Brush)Application.Current.Resources["PanelBackground"],
                Foreground = (Brush)Application.Current.Resources["TextForeground"],
                ResizeMode = ResizeMode.NoResize
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "🏆 Başarımlar",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = (Brush)Application.Current.Resources["AccentColor"]
            });

            int total = _achievements.Count;
            int unlocked = _achievements.Count(x => x.Unlocked);
            double percent = total > 0 ? (unlocked * 100.0 / total) : 0;

            stack.Children.Add(new TextBlock
            {
                Text = $"İlerleme: {unlocked} / {total}  ({percent:F0}%)",
                FontSize = 14,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 20)
            });

            foreach (var a in _achievements)
            {
                var border = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackground"],
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 5, 0, 5),
                    Opacity = a.Unlocked ? 1.0 : 0.55,
                    BorderThickness = new Thickness(a.Unlocked ? 2 : 1, 0, 0, 0),
                    BorderBrush = a.Unlocked
                        ? (Brush)Application.Current.Resources["SuccessColor"]
                        : (Brush)Application.Current.Resources["CardBackground"]
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = new TextBlock
                {
                    Text = a.Unlocked ? "🏆" : "🔒",
                    FontSize = 34,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 14, 0)
                };
                Grid.SetColumn(icon, 0);
                grid.Children.Add(icon);

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textStack.Children.Add(new TextBlock
                {
                    Text = a.Title,
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)Application.Current.Resources["TextForeground"]
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = a.Description,
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                if (a.Unlocked && a.UnlockTime.HasValue)
                {
                    textStack.Children.Add(new TextBlock
                    {
                        Text = $"✅ {a.UnlockTime.Value:dd.MM.yyyy HH:mm}",
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["SuccessColor"],
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                }

                Grid.SetColumn(textStack, 1);
                grid.Children.Add(textStack);

                var status = new TextBlock
                {
                    Text = a.Unlocked ? "✅" : "🔒",
                    FontSize = 24,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(status, 2);
                grid.Children.Add(status);

                border.Child = grid;
                stack.Children.Add(border);
            }

            scroll.Content = stack;
            popup.Content = scroll;
            popup.ShowDialog();
        }

        private void ChkStartWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ChkStartWithWindows.IsChecked == true)
                    AddToStartup();
                else
                    RemoveFromStartup();
                SaveUserData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Başlangıç ayarı güncellenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddToStartup()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue("ControlCenter", exePath);
                }
            }
            catch { }
        }

        private void RemoveFromStartup()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue("ControlCenter", false);
            }
            catch { }
        }

        private void DrawerPanel_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void DrawerPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void DrawerPanel_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;

                foreach (var file in files)
                {
                    if (Directory.Exists(file))
                        continue;

                    if (File.Exists(file))
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (ext == ".exe")
                        {
                            string name = Path.GetFileNameWithoutExtension(file);
                            _userData.AppShortcuts.Add(new AppShortcut { Name = name, Path = file, Icon = "🚀", IsWeb = false });
                            _userData.AppShortcutAddCount++;
                        }
                        else if (ext == ".url" || ext == ".lnk")
                        {
                            try
                            {
                                var lines = File.ReadAllLines(file);
                                foreach (var line in lines)
                                {
                                    if (line.StartsWith("URL="))
                                    {
                                        var url = line.Substring(4);
                                        _userData.AppShortcuts.Add(new AppShortcut { Name = Path.GetFileNameWithoutExtension(file), Path = url, Icon = "🌐", IsWeb = true });
                                        _userData.WebShortcutAddCount++;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            MessageBox.Show($"Desteklenmeyen dosya türü: {ext}", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }

                _userData.DragDropShortcutCount++;
                if (_userData.DragDropShortcutCount >= 10) UnlockAchievement("dragger");
                if (_userData.AppShortcutAddCount >= 25) UnlockAchievement("shortcutking");
                if (_userData.WebShortcutAddCount >= 25) UnlockAchievement("webmaster");

                SaveUserData();
                RefreshShortcuts();
                MessageBox.Show($"Kısayollar eklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sürükle-bırak hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitClipboardManager()
        {
            LvClipboard.ItemsSource = _clipboardItems;
            _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _clipboardTimer.Tick += ClipboardTimer_Tick;
            _clipboardTimer.Start();
        }

        private void ClipboardTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text) && text != _lastClipboardText)
                    {
                        _lastClipboardText = text;
                        AddClipboardItem(text);
                        _userData.BluetoothSharedClipboard = text;

                        _userData.ClipboardUniqueCount++;
                        if (_userData.ClipboardUniqueCount >= 50) UnlockAchievement("clipboardking");
                        SaveUserData();
                    }
                }
            }
            catch { }
        }

        private void AddClipboardItem(string text)
        {
            if (text.Length > 1000) text = text.Substring(0, 1000) + "...";

            if (_clipboardItems.Contains(text))
                _clipboardItems.Remove(text);

            _clipboardItems.Insert(0, text);

            while (_clipboardItems.Count > 10)
                _clipboardItems.RemoveAt(_clipboardItems.Count - 1);
        }

        private void BtnClearClipboard_Click(object sender, RoutedEventArgs e)
        {
            _clipboardItems.Clear();
            _lastClipboardText = "";
            try { Clipboard.Clear(); } catch { }

            _userData.ClipboardClearCount++;
            if (_userData.ClipboardClearCount >= 10) UnlockAchievement("clipboardsweeper");
            SaveUserData();
        }

        private class InputDialog : Window
        {
            public string Answer1 { get; private set; } = "";
            public string Answer2 { get; private set; } = "";

            public InputDialog(string title, string label1, string default1 = "", string label2 = "")
            {
                Title = title;
                Width = 400;
                Height = 250;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Owner = Application.Current?.MainWindow ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault();

                var sp = new StackPanel { Margin = new Thickness(20) };
                sp.Children.Add(new TextBlock { Text = label1 });
                var t1 = new TextBox { Text = default1, Margin = new Thickness(0, 5, 0, 10) };
                sp.Children.Add(t1);

                TextBox? t2 = null;
                if (!string.IsNullOrEmpty(label2))
                {
                    sp.Children.Add(new TextBlock { Text = label2 });
                    t2 = new TextBox { Margin = new Thickness(0, 5, 0, 10) };
                    sp.Children.Add(t2);
                }

                var bp = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                var ok = new Button { Content = "Tamam", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
                var cancel = new Button { Content = "İptal", Width = 80 };

                ok.Click += (s, e) =>
                {
                    Answer1 = t1.Text;
                    if (t2 != null) Answer2 = t2.Text;
                    DialogResult = true;
                    Close();
                };
                cancel.Click += (s, e) => { DialogResult = false; Close(); };

                bp.Children.Add(ok);
                bp.Children.Add(cancel);
                sp.Children.Add(bp);
                Content = sp;
            }
        }

        private void InitBluetoothTracking()
        {
            _bluetoothUsageTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _bluetoothUsageTimer.Tick += async (s, e) => await TrackBluetoothUsageAsync();
            _bluetoothUsageTimer.Start();
        }

        private async Task TrackBluetoothUsageAsync()
        {
            try
            {
                if (_userData.BluetoothUsageResetDate.Date != DateTime.Today)
                {
                    _userData.BluetoothUsageSeconds.Clear();
                    _userData.BluetoothUsageResetDate = DateTime.Today;
                }

                var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                var deviceInfos = await DeviceInformation.FindAllAsync(selector);
                foreach (var di in deviceInfos)
                {
                    try
                    {
                        var bt = await BluetoothDevice.FromIdAsync(di.Id);
                        if (bt == null) continue;
                        if (bt.ConnectionStatus != BluetoothConnectionStatus.Connected) continue;

                        var id = bt.DeviceId;
                        if (!_userData.BluetoothUsageSeconds.ContainsKey(id))
                            _userData.BluetoothUsageSeconds[id] = 0;
                        _userData.BluetoothUsageSeconds[id] += 60;

                        _userData.UsedBluetoothDevices.Add(id);
                        _userData.PairedBluetoothDevices.Add(id);

                        if (_userData.UsedBluetoothDevices.Count >= 10) UnlockAchievement("btdevices");
                        if (_userData.PairedBluetoothDevices.Count >= 5) UnlockAchievement("multidevice");

                        if (_userData.BluetoothUsageSeconds[id] >= 8 * 3600) UnlockAchievement("btmarathon");
                    }
                    catch { }
                }
                SaveUserData();
            }
            catch { }
        }

        private async void BtnBluetooth_Click(object sender, RoutedEventArgs e)
        {
            _userData.BluetoothPanelOpenCount++;
            if (_userData.BluetoothPanelOpenCount >= 20) UnlockAchievement("btscanner");
            SaveUserData();
            await ShowBluetoothPanelAsync();
        }

        private async Task ShowBluetoothPanelAsync()
        {
            var popup = new Window
            {
                Title = "Bluetooth Yönetimi",
                Width = 560,
                Height = 760,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (Brush)Application.Current.Resources["PanelBackground"],
                Foreground = (Brush)Application.Current.Resources["TextForeground"],
                ResizeMode = ResizeMode.CanResize
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(18) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Bluetooth Yönetimi",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["AccentColor"],
                Margin = new Thickness(0, 0, 0, 12)
            });

            var mediaHost = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackground"],
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var mediaStack = new StackPanel();
            mediaStack.Children.Add(new TextBlock
            {
                Text = "Medya Kontrolü",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var mediaGrid = new Grid();
            mediaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mediaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btMediaArtImage = new Image
            {
                Width = 64,
                Height = 64,
                Stretch = Stretch.UniformToFill,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(_btMediaArtImage, 0);
            mediaGrid.Children.Add(_btMediaArtImage);

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _btMediaTitleBlock = new TextBlock
            {
                Text = "Çalan içerik yok",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _btMediaArtistBlock = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoStack.Children.Add(_btMediaTitleBlock);
            infoStack.Children.Add(_btMediaArtistBlock);
            Grid.SetColumn(infoStack, 1);
            mediaGrid.Children.Add(infoStack);

            mediaStack.Children.Add(mediaGrid);

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var prevBtn = new Button { Content = "◀◀", Width = 70, Height = 34, Margin = new Thickness(2) };
            var playBtn = new Button { Content = "▶ / ❚❚", Width = 90, Height = 34, Margin = new Thickness(2) };
            var nextBtn = new Button { Content = "▶▶", Width = 70, Height = 34, Margin = new Thickness(2) };

            prevBtn.Click += async (s, ev) => { var sess = await GetMediaSessionAsync(); if (sess != null) await sess.TrySkipPreviousAsync(); IncrementMediaControl(); };
            playBtn.Click += async (s, ev) => { var sess = await GetMediaSessionAsync(); if (sess != null) await sess.TryTogglePlayPauseAsync(); IncrementMediaControl(); };
            nextBtn.Click += async (s, ev) => { var sess = await GetMediaSessionAsync(); if (sess != null) await sess.TrySkipNextAsync(); IncrementMediaControl(); };

            controls.Children.Add(prevBtn);
            controls.Children.Add(playBtn);
            controls.Children.Add(nextBtn);
            mediaStack.Children.Add(controls);

            mediaHost.Child = mediaStack;
            stack.Children.Add(mediaHost);

            stack.Children.Add(new TextBlock
            {
                Text = "Eşleşmiş Cihazlar",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 6, 0, 8)
            });

            var deviceListStack = new StackPanel();
            stack.Children.Add(deviceListStack);

            var btnsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };
            var refreshBtn = new Button { Content = "Yenile", Width = 100, Height = 32, Margin = new Thickness(0, 0, 6, 0) };
            var settingsBtn = new Button { Content = "Windows Bluetooth Ayarları", Height = 32 };
            refreshBtn.Click += async (s, ev) => await ReloadBtDevicesAsync(deviceListStack);
            settingsBtn.Click += (s, ev) => { try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); } catch { } };
            btnsRow.Children.Add(refreshBtn);
            btnsRow.Children.Add(settingsBtn);
            stack.Children.Add(btnsRow);

            stack.Children.Add(new TextBlock
            {
                Text = "Günlük Kullanım",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 16, 0, 8)
            });
            var usageStack = new StackPanel();
            stack.Children.Add(usageStack);

            stack.Children.Add(new TextBlock
            {
                Text = "Seçenekler",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 16, 0, 8)
            });

            var optStack = new StackPanel();
            var chkAutoConnect = new CheckBox { Content = "Otomatik bağlanma ve cihaz tanıma", IsChecked = _userData.BluetoothAutoConnectEnabled, Margin = new Thickness(0, 4, 0, 4), Foreground = (Brush)Application.Current.Resources["TextForeground"] };
            var chkSilent = new CheckBox { Content = "Tam ekranda aramaları sessize al", IsChecked = _userData.BluetoothSilentModeOnFullscreen, Margin = new Thickness(0, 4, 0, 4), Foreground = (Brush)Application.Current.Resources["TextForeground"] };
            var chkDual = new CheckBox { Content = "Çift ses çıkışı paylaşımı", IsChecked = _userData.BluetoothDualAudioEnabled, Margin = new Thickness(0, 4, 0, 4), Foreground = (Brush)Application.Current.Resources["TextForeground"] };
            chkAutoConnect.Checked += (s, ev) => { _userData.BluetoothAutoConnectEnabled = true; SaveUserData(); };
            chkAutoConnect.Unchecked += (s, ev) => { _userData.BluetoothAutoConnectEnabled = false; SaveUserData(); };
            chkSilent.Checked += (s, ev) => { _userData.BluetoothSilentModeOnFullscreen = true; SaveUserData(); };
            chkSilent.Unchecked += (s, ev) => { _userData.BluetoothSilentModeOnFullscreen = false; SaveUserData(); };
            chkDual.Checked += (s, ev) => { _userData.BluetoothDualAudioEnabled = true; SaveUserData(); };
            chkDual.Unchecked += (s, ev) => { _userData.BluetoothDualAudioEnabled = false; SaveUserData(); };
            optStack.Children.Add(chkAutoConnect);
            optStack.Children.Add(chkSilent);
            optStack.Children.Add(chkDual);
            stack.Children.Add(optStack);

            scroll.Content = stack;
            popup.Content = scroll;

            _bluetoothMediaTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _bluetoothMediaTimer.Tick += async (s, ev) => await UpdateBtMediaInfoAsync();
            _bluetoothMediaTimer.Start();
            popup.Closed += (s, ev) => { _bluetoothMediaTimer?.Stop(); _bluetoothMediaTimer = null; };

            await UpdateBtMediaInfoAsync();
            await ReloadBtDevicesAsync(deviceListStack);
            ReloadBtUsageStats(usageStack);

            popup.ShowDialog();
        }

        private void IncrementMediaControl()
        {
            _userData.BluetoothMediaControlCount++;
            if (_userData.BluetoothMediaControlCount >= 50) UnlockAchievement("mediajockey");
            SaveUserData();
        }

        private async Task ReloadBtDevicesAsync(StackPanel host)
        {
            host.Children.Clear();
            host.Children.Add(new TextBlock { Text = "Taranıyor...", Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 4) });

            var devices = new List<BluetoothDevice>();
            try
            {
                var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                var deviceInfos = await DeviceInformation.FindAllAsync(selector);
                foreach (var di in deviceInfos)
                {
                    try
                    {
                        var bt = await BluetoothDevice.FromIdAsync(di.Id);
                        if (bt != null)
                        {
                            devices.Add(bt);
                            _userData.PairedBluetoothDevices.Add(bt.DeviceId);
                        }
                    }
                    catch { }
                }
                if (_userData.PairedBluetoothDevices.Count >= 5) UnlockAchievement("multidevice");
                SaveUserData();
            }
            catch { }

            host.Children.Clear();

            if (devices.Count == 0)
            {
                host.Children.Add(new TextBlock
                {
                    Text = "Eşleşmiş cihaz bulunamadı. Windows Bluetooth ayarlarından cihaz eşleştirebilirsiniz.",
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 4)
                });
                return;
            }

            foreach (var bt in devices)
            {
                host.Children.Add(await CreateBtDeviceCardAsync(bt, host));
            }
        }

        private async Task<Border> CreateBtDeviceCardAsync(BluetoothDevice bt, StackPanel host)
        {
            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackground"],
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 4)
            };

            var outer = new Grid();
            outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            string displayName = string.IsNullOrEmpty(bt.Name) ? "Bilinmeyen Cihaz" : bt.Name;
            infoStack.Children.Add(new TextBlock
            {
                Text = displayName,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            bool connected = bt.ConnectionStatus == BluetoothConnectionStatus.Connected;
            string statusText = connected ? "Bağlı" : "Bağlı değil";
            var statusBrush = connected ? (Brush)Application.Current.Resources["SuccessColor"] : Brushes.Gray;

            infoStack.Children.Add(new TextBlock
            {
                Text = statusText,
                FontSize = 12,
                Foreground = statusBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });

            string address = bt.BluetoothAddress.ToString("X12");
            var formattedAddress = string.Join(":", Enumerable.Range(0, 6).Select(i => address.Substring(i * 2, 2)));
            infoStack.Children.Add(new TextBlock
            {
                Text = formattedAddress,
                FontSize = 11,
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 2, 0, 0)
            });

            int battery = connected ? await TryGetBatteryLevelAsync(bt) : -1;
            if (battery >= 0)
            {
                var batteryPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                batteryPanel.Children.Add(new TextBlock { Text = "Pil:", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 6, 0) });
                var batteryBar = new ProgressBar { Width = 100, Height = 8, Minimum = 0, Maximum = 100, Value = battery, Foreground = battery < 20 ? Brushes.Red : (Brush)Application.Current.Resources["SuccessColor"] };
                batteryPanel.Children.Add(batteryBar);
                batteryPanel.Children.Add(new TextBlock { Text = $" %{battery}", FontSize = 11, Margin = new Thickness(6, 0, 0, 0) });

                infoStack.Children.Add(batteryPanel);

                if (battery < 10)
                {
                    _userData.BluetoothLowBatterySeen = true;
                    UnlockAchievement("batterywatcher");
                    SaveUserData();
                }
            }

            if (_userData.BluetoothUsageSeconds.TryGetValue(bt.DeviceId, out var usageSeconds) && usageSeconds > 0)
            {
                var ts = TimeSpan.FromSeconds(usageSeconds);
                infoStack.Children.Add(new TextBlock
                {
                    Text = $"Bugün: {(int)ts.TotalHours} sa {ts.Minutes} dk",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            Grid.SetColumn(infoStack, 0);
            outer.Children.Add(infoStack);

            var actionStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            bool isPriority = _userData.PriorityBluetoothDevices.Contains(bt.DeviceId);
            var chkPriority = new CheckBox
            {
                Content = "Öncelikli",
                IsChecked = isPriority,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = (Brush)Application.Current.Resources["TextForeground"]
            };
            chkPriority.Checked += (s, ev) =>
            {
                if (!_userData.PriorityBluetoothDevices.Contains(bt.DeviceId))
                    _userData.PriorityBluetoothDevices.Add(bt.DeviceId);
                if (_userData.PriorityBluetoothDevices.Count >= 3) UnlockAchievement("priorityuser");
                SaveUserData();
            };
            chkPriority.Unchecked += (s, ev) =>
            {
                _userData.PriorityBluetoothDevices.Remove(bt.DeviceId);
                SaveUserData();
            };
            actionStack.Children.Add(chkPriority);

            var manageBtn = new Button
            {
                Content = "Yönet",
                Width = 80,
                Height = 28,
                FontSize = 12
            };
            manageBtn.Click += (s, ev) =>
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); }
                catch { }
            };
            actionStack.Children.Add(manageBtn);

            Grid.SetColumn(actionStack, 1);
            outer.Children.Add(actionStack);

            card.Child = outer;
            return card;
        }

        private async Task<int> TryGetBatteryLevelAsync(BluetoothDevice bt)
        {
            try
            {
                using var leDevice = await BluetoothLEDevice.FromIdAsync(bt.DeviceId);
                if (leDevice == null) return -1;

                var svcResult = await leDevice.GetGattServicesForUuidAsync(GattServiceUuids.Battery);
                if (svcResult.Status != GattCommunicationStatus.Success || svcResult.Services.Count == 0)
                    return -1;

                var svc = svcResult.Services[0];
                var charResult = await svc.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel);
                if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
                    return -1;

                var readResult = await charResult.Characteristics[0].ReadValueAsync();
                if (readResult.Status != GattCommunicationStatus.Success)
                    return -1;

                using var reader = DataReader.FromBuffer(readResult.Value);
                return reader.ReadByte();
            }
            catch { return -1; }
        }

        private void ReloadBtUsageStats(StackPanel host)
        {
            host.Children.Clear();
            if (_userData.BluetoothUsageSeconds.Count == 0)
            {
                host.Children.Add(new TextBlock { Text = "Bugün için kayıtlı kullanım yok.", Foreground = Brushes.Gray, FontSize = 12 });
                return;
            }

            foreach (var kvp in _userData.BluetoothUsageSeconds)
            {
                var ts = TimeSpan.FromSeconds(kvp.Value);
                host.Children.Add(new TextBlock
                {
                    Text = $"{kvp.Key}: {(int)ts.TotalHours} sa {ts.Minutes} dk",
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["TextForeground"],
                    Margin = new Thickness(0, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        private async Task<GlobalSystemMediaTransportControlsSession?> GetMediaSessionAsync()
        {
            try
            {
                if (_mediaSessionManager == null)
                    _mediaSessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                return _mediaSessionManager.GetCurrentSession();
            }
            catch { return null; }
        }

        private async Task UpdateBtMediaInfoAsync()
        {
            if (_btMediaTitleBlock == null || _btMediaArtistBlock == null) return;
            var session = await GetMediaSessionAsync();
            if (session == null)
            {
                _btMediaTitleBlock.Text = "Çalan içerik yok";
                _btMediaArtistBlock.Text = "";
                if (_btMediaArtImage != null) _btMediaArtImage.Source = null;
                return;
            }
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                _btMediaTitleBlock.Text = string.IsNullOrEmpty(props.Title) ? "Bilinmeyen" : props.Title;
                _btMediaArtistBlock.Text = string.IsNullOrEmpty(props.Artist) ? "" : props.Artist;

                if (_btMediaArtImage != null)
                {
                    try
                    {
                        var thumb = props.Thumbnail;
                        if (thumb != null)
                        {
                            using var stream = await thumb.OpenReadAsync();
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = stream.AsStreamForRead();
                            bmp.EndInit();
                            bmp.Freeze();
                            _btMediaArtImage.Source = bmp;
                        }
                        else
                        {
                            _btMediaArtImage.Source = null;
                        }
                    }
                    catch { _btMediaArtImage.Source = null; }
                }
            }
            catch { }
        }

        private void BtnHowToUse_Click(object sender, RoutedEventArgs e)
        {
            var popup = new Window
            {
                Title = "Nasıl Kullanılır",
                Width = 620,
                Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (Brush)Application.Current.Resources["PanelBackground"],
                Foreground = (Brush)Application.Current.Resources["TextForeground"],
                ResizeMode = ResizeMode.CanResize
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(22) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Nasıl Kullanılır",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["AccentColor"],
                Margin = new Thickness(0, 0, 0, 14)
            });

            stack.Children.Add(CreateHowToSection("Genel Kullanım",
                "Sol taraftaki panelden Dosya Gezgini, CMD/PowerShell, Uygulamalar ve İşlem Yöneticisi sekmeleri arasında geçiş yapabilirsiniz.\n" +
                "Sağ panelde sistem kaynakları, ağ hızı, ana ses seviyesi ve hızlı erişim butonları bulunur."));

            stack.Children.Add(CreateHowToSection("Kısayollar",
                "Sol paneldeki alanlara .exe dosyalarını sürükleyip bırakarak uygulama kısayolu ekleyebilirsiniz.\n" +
                "Web sitesi kısayolları için URL girmeniz yeterlidir; favicon otomatik olarak yüklenir.\n" +
                "Kısayolları silmek için kısayolun solundaki çöp kutusu butonuna basın."));

            stack.Children.Add(CreateHowToSection("Arama",
                "Üstteki arama çubuğundan internet araması yapabilir veya doğrudan URL yazarak site açabilirsiniz.\n" +
                "Enter tuşu ile aramayı başlatabilirsiniz."));

            stack.Children.Add(CreateHowToSection("Dosya Gezgini",
                "Soldaki ağaç görünümünden klasör seçin, sağdaki listede dosyalar görüntülenir.\n" +
                "Metin, kod, resim, PDF, DOCX ve video dosyaları uygulama içinde açılır.\n" +
                "Açılan metin dosyalarını düzenleyip Kaydet butonu ile kaydedebilirsiniz."));

            stack.Children.Add(CreateHowToSection("Konsol",
                "CMD veya PowerShell seçimi yapabilirsiniz. Yönetici olarak çalıştırmak için Yönetici kutusunu işaretleyin.\n" +
                "Hızlı İşlemler menüsünden Wi-Fi şifrelerini, güncellemeleri, temizlik işlemlerini tek tıkla yapabilirsiniz."));

            stack.Children.Add(CreateHowToSection("Bluetooth",
                "Bluetooth butonuna basarak bağlı ve eşleşmiş cihazları görüntüleyebilirsiniz.\n" +
                "Cihaz kartlarından pil durumu, bağlantı durumu ve günlük kullanım süresi görüntülenir.\n" +
                "Öncelikli cihaz işaretleyerek otomatik bağlanma önceliği tanımlayabilirsiniz.\n" +
                "Medya kontrol bölümünden çalan müziği yönetebilirsiniz."));

            stack.Children.Add(CreateHowToSection("Başarımlar",
                "Başarımlar butonu ile kilitli ve açık başarımları görüntüleyebilirsiniz.\n" +
                "Maskota tıklayarak, Konami kodunu girerek ve çeşitli sistem etkinlikleri ile başarım açabilirsiniz."));

            stack.Children.Add(CreateHowToSection("Widgetlar",
                "Not defteri, zamanlayıcı, hesap makinesi ve pano geçmişi widgetları tek sekmede toplanmıştır."));

            stack.Children.Add(CreateHowToSection("Sistem Tepsisi",
                "Uygulama kapatıldığında sistem tepsisinde çalışmaya devam eder.\n" +
                "CTRL+SHIFT+C kısayolu ile pencereyi gizleyip gösterebilirsiniz.\n" +
                "Tepsi simgesine çift tıklayarak ana pencereyi açabilirsiniz."));

            stack.Children.Add(CreateHowToSection("Overlay",
                "Küçük overlay penceresi ekranın köşesinde FPS, ping, sıcaklık ve sistem bilgilerini gösterir.\n" +
                "Ayarlar butonundan overlay için hızlı işlemler yapabilirsiniz."));

            stack.Children.Add(CreateHowToSection("Kısayol Tuşları",
                "CTRL+SHIFT+C: Ana pencereyi göster/gizle\n" +
                "Konami Kodu (↑↑↓↓←→←→): Nyan modu"));

            scroll.Content = stack;
            popup.Content = scroll;
            popup.ShowDialog();
        }

        private Border CreateHowToSection(string title, string body)
        {
            var border = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackground"],
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 5, 0, 5)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["AccentColor"],
                Margin = new Thickness(0, 0, 0, 6)
            });
            stack.Children.Add(new TextBlock
            {
                Text = body,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextForeground"],
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });

            border.Child = stack;
            return border;
        }
    }
}