// SystemWidget - compact GPU / VRAM / CPU / RAM gauges for Windows.
// Built locally by Installer.ps1 with the .NET Framework compiler.
// The uiAccess=true manifest puts the window in the band reserved for
// accessibility tools, so the taskbar never draws over it.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SystemWidgetApp
{
    [DataContract]
    public class Config
    {
        [DataMember] public double X = -99999;
        [DataMember] public double Y = -99999;
        [DataMember] public double Opacity = 1.0;
        [DataMember] public string Lang = "en";
        // Nullable on purpose: DataContractJsonSerializer skips field
        // initializers, so a plain bool absent from an older config file would
        // read back as false - the opposite of the intended default.
        [DataMember] public bool? HideFullScreen;
    }

    // ---------- localization ----------
    // To add a language: copy one of the blocks in I18n, translate the values
    // and append it to Catalog. Nothing else to touch - the language menu and
    // the config file are both driven by Catalog.
    public class Strings
    {
        public string Code;     // ISO 639-1, stored in config.json
        public string Native;   // language name written in that language

        public string MenuRefresh, MenuMoveBottomLeft, MenuOpacity,
                      MenuStartWithWindows, MenuHideFullScreen, MenuLanguage,
                      MenuRestart, MenuQuit;

        public string TipGpuPower;      // {0} watts, {1} limit, {2} percent, {3} engine load
        public string TipVideoMemory;   // {0} = amount
        public string TipNoGpu;
        public string TipCpu;
        public string TipSystemMemory;  // {0} = amount

        public string UnitGigabyte, UnitMegabyte;
    }

    public static class I18n
    {
        public static readonly Strings[] Catalog = { English(), French(), Spanish(), German() };
        public static Strings T = Catalog[0];   // English is the default

        // Unknown or missing code falls back to the first entry.
        public static void Use(string code)
        {
            foreach (Strings entry in Catalog)
                if (entry.Code == code) { T = entry; return; }
            T = Catalog[0];
        }

        static Strings English()
        {
            return new Strings
            {
                Code = "en", Native = "English",
                MenuRefresh = "Refresh",
                MenuMoveBottomLeft = "Move to bottom left",
                MenuOpacity = "Opacity",
                MenuStartWithWindows = "Start with Windows",
                MenuHideFullScreen = "Hide in full-screen apps",
                MenuLanguage = "Language",
                MenuRestart = "Restart widget",
                MenuQuit = "Quit",
                TipGpuPower = "GPU power: {0:0.0} W / {1:0.0} W ({2:0}%)\nGPU engine load: {3:0}%",
                TipVideoMemory = "Video memory: {0}",
                TipNoGpu = "nvidia-smi not found, or no NVIDIA card detected.",
                TipCpu = "Total processor usage",
                TipSystemMemory = "System memory: {0}",
                UnitGigabyte = "GB", UnitMegabyte = "MB"
            };
        }

        static Strings French()
        {
            return new Strings
            {
                Code = "fr", Native = "Français",
                MenuRefresh = "Actualiser",
                MenuMoveBottomLeft = "Replacer en bas à gauche",
                MenuOpacity = "Opacité",
                MenuStartWithWindows = "Lancer au démarrage de Windows",
                MenuHideFullScreen = "Masquer en plein écran",
                MenuLanguage = "Langue",
                MenuRestart = "Redémarrer le widget",
                MenuQuit = "Quitter",
                TipGpuPower = "Puissance GPU : {0:0.0} W / {1:0.0} W ({2:0} %)\nCharge du moteur GPU : {3:0} %",
                TipVideoMemory = "Mémoire vidéo : {0}",
                TipNoGpu = "nvidia-smi introuvable, ou aucune carte NVIDIA détectée.",
                TipCpu = "Utilisation totale du processeur",
                TipSystemMemory = "Mémoire vive : {0}",
                UnitGigabyte = "Go", UnitMegabyte = "Mo"
            };
        }

        static Strings Spanish()
        {
            return new Strings
            {
                Code = "es", Native = "Español",
                MenuRefresh = "Actualizar",
                MenuMoveBottomLeft = "Mover abajo a la izquierda",
                MenuOpacity = "Opacidad",
                MenuStartWithWindows = "Iniciar con Windows",
                MenuHideFullScreen = "Ocultar en pantalla completa",
                MenuLanguage = "Idioma",
                MenuRestart = "Reiniciar el widget",
                MenuQuit = "Salir",
                TipGpuPower = "Potencia de la GPU: {0:0.0} W / {1:0.0} W ({2:0} %)\nCarga del motor gráfico: {3:0} %",
                TipVideoMemory = "Memoria de vídeo: {0}",
                TipNoGpu = "No se encuentra nvidia-smi, o no se ha detectado ninguna tarjeta NVIDIA.",
                TipCpu = "Uso total del procesador",
                TipSystemMemory = "Memoria del sistema: {0}",
                UnitGigabyte = "GB", UnitMegabyte = "MB"
            };
        }

        static Strings German()
        {
            return new Strings
            {
                Code = "de", Native = "Deutsch",
                MenuRefresh = "Aktualisieren",
                MenuMoveBottomLeft = "Unten links platzieren",
                MenuOpacity = "Deckkraft",
                MenuStartWithWindows = "Mit Windows starten",
                MenuHideFullScreen = "Bei Vollbild ausblenden",
                MenuLanguage = "Sprache",
                MenuRestart = "Widget neu starten",
                MenuQuit = "Beenden",
                TipGpuPower = "GPU-Leistung: {0:0.0} W / {1:0.0} W ({2:0} %)\nGPU-Auslastung: {3:0} %",
                TipVideoMemory = "Grafikspeicher: {0}",
                TipNoGpu = "nvidia-smi nicht gefunden oder keine NVIDIA-Karte erkannt.",
                TipCpu = "Gesamte Prozessorauslastung",
                TipSystemMemory = "Arbeitsspeicher: {0}",
                UnitGigabyte = "GB", UnitMegabyte = "MB"
            };
        }
    }

    public static class Json
    {
        public static T Read<T>(string text) where T : class
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                    return (T)serializer.ReadObject(stream);
            }
            catch { return null; }
        }

        public static string Write<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    public class Snapshot
    {
        public double GpuPowerPct;
        public double GpuWatts;
        public double GpuPowerLimit;
        public double GpuLoadPct;
        public double VramPct;
        public double VramUsedMb;
        public double VramTotalMb;
        public double CpuPct;
        public double RamPct;
        public ulong RamUsedMb;
        public ulong RamTotalMb;
        public bool GpuAvailable;
    }

    public static class Metrics
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FileTime
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MemoryStatus
        {
            public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus status);

        static readonly object CpuLock = new object();
        static ulong _lastIdle;
        static ulong _lastKernel;
        static ulong _lastUser;
        static bool _cpuReady;

        static ulong ToUInt64(FileTime time)
        {
            return ((ulong)time.High << 32) | time.Low;
        }

        static double CpuUsage()
        {
            lock (CpuLock)
            {
                FileTime idleFt, kernelFt, userFt;
                if (!GetSystemTimes(out idleFt, out kernelFt, out userFt)) return 0;

                ulong idle = ToUInt64(idleFt);
                ulong kernel = ToUInt64(kernelFt);
                ulong user = ToUInt64(userFt);
                if (!_cpuReady)
                {
                    _lastIdle = idle;
                    _lastKernel = kernel;
                    _lastUser = user;
                    _cpuReady = true;
                    return 0;
                }

                ulong idleDelta = idle - _lastIdle;
                ulong kernelDelta = kernel - _lastKernel;
                ulong userDelta = user - _lastUser;
                ulong total = kernelDelta + userDelta;
                _lastIdle = idle;
                _lastKernel = kernel;
                _lastUser = user;
                if (total == 0) return 0;
                return Clamp(100.0 * (total - idleDelta) / total);
            }
        }

        static void ReadMemory(Snapshot snapshot)
        {
            var status = new MemoryStatus();
            if (!GlobalMemoryStatusEx(status)) return;
            snapshot.RamTotalMb = status.TotalPhys / 1024 / 1024;
            snapshot.RamUsedMb = (status.TotalPhys - status.AvailPhys) / 1024 / 1024;
            snapshot.RamPct = status.TotalPhys == 0
                ? 0
                : Clamp(100.0 * (status.TotalPhys - status.AvailPhys) / status.TotalPhys);
        }

        static string NvidiaSmiPath()
        {
            string systemPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "nvidia-smi.exe");
            if (File.Exists(systemPath)) return systemPath;

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string nvPath = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
            if (File.Exists(nvPath)) return nvPath;
            return "nvidia-smi.exe";
        }

        static void ReadGpu(Snapshot snapshot)
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = NvidiaSmiPath(),
                    Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total,power.draw,power.limit --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var process = Process.Start(start))
                {
                    string line = process.StandardOutput.ReadLine();
                    if (!process.WaitForExit(3000))
                    {
                        try { process.Kill(); } catch { }
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(line)) return;
                    string[] values = line.Split(',');
                    if (values.Length < 5) return;

                    double load, used, total, watts, limit;
                    if (!TryNumber(values[0], out load) ||
                        !TryNumber(values[1], out used) ||
                        !TryNumber(values[2], out total) ||
                        !TryNumber(values[3], out watts) ||
                        !TryNumber(values[4], out limit)) return;

                    snapshot.GpuAvailable = true;
                    snapshot.GpuLoadPct = Clamp(load);
                    snapshot.VramUsedMb = used;
                    snapshot.VramTotalMb = total;
                    snapshot.VramPct = total <= 0 ? 0 : Clamp(100.0 * used / total);
                    snapshot.GpuWatts = watts;
                    snapshot.GpuPowerLimit = limit;
                    snapshot.GpuPowerPct = limit <= 0 ? 0 : Clamp(100.0 * watts / limit);
                }
            }
            catch { }
        }

        static bool TryNumber(string value, out double result)
        {
            value = value.Trim();
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        static double Clamp(double value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        public static void PrimeCpu()
        {
            CpuUsage();
        }

        public static Snapshot Read()
        {
            var snapshot = new Snapshot();
            snapshot.CpuPct = CpuUsage();
            ReadMemory(snapshot);
            ReadGpu(snapshot);
            return snapshot;
        }
    }

    public class GaugeRow
    {
        public Grid Root;
        public TextBlock Percent;
        public Border Fill;
        public Border Track;
        public string Name;

        static Brush BrushFrom(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        public GaugeRow(string name)
        {
            Name = name;
            Root = new Grid { Height = 18, Width = 142 };
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });

            var label = new TextBlock
            {
                Text = name,
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom("#8B91A7"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            Root.Children.Add(label);

            Percent = new TextBlock
            {
                Text = "--",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom("#DA7756"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(Percent, 1);
            Root.Children.Add(Percent);

            Track = new Border
            {
                Width = 62,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = BrushFrom("#303442"),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Fill = new Border
            {
                Height = 4,
                Width = 0,
                CornerRadius = new CornerRadius(2),
                Background = BrushFrom("#DA7756"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Track.Child = Fill;
            Grid.SetColumn(Track, 2);
            Root.Children.Add(Track);
        }

        static string Blend(
            int r1, int g1, int b1,
            int r2, int g2, int b2,
            double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            int r = (int)Math.Round(r1 + (r2 - r1) * amount);
            int g = (int)Math.Round(g1 + (g2 - g1) * amount);
            int b = (int)Math.Round(b1 + (b2 - b1) * amount);
            return string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
        }

        static string Color(double value)
        {
            value = Math.Max(0, Math.Min(100, value));

            // Continuous gradient: green at 0%, amber in the middle,
            // then red at 100%.
            if (value <= 50)
                return Blend(52, 199, 123, 240, 164, 60, value / 50.0);
            return Blend(240, 164, 60, 226, 77, 91, (value - 50) / 50.0);
        }

        public void Set(double value)
        {
            value = Math.Max(0, Math.Min(100, value));
            string color = Color(value);
            Percent.Text = (int)Math.Round(value) + "%";
            Percent.Foreground = BrushFrom(color);
            Fill.Background = BrushFrom(color);
            Fill.Width = Track.Width * value / 100.0;
        }

        public void Unavailable()
        {
            Percent.Text = "--";
            Percent.Foreground = BrushFrom("#6C7086");
            Fill.Width = 0;
        }
    }

    public class MainWindow : Window
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        static readonly IntPtr TopMost = new IntPtr(-1);
        static readonly IntPtr NotTopMost = new IntPtr(-2);
        const uint PositionFlags = 0x1 | 0x2 | 0x10;

        [StructLayout(LayoutKind.Sequential)]
        struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle, out Rect rect);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr handle, StringBuilder buffer, int count);
        const uint MonitorDefaultToNearest = 2;

        readonly Border _root;
        readonly GaugeRow _gpuPower;
        readonly GaugeRow _vram;
        readonly GaugeRow _cpu;
        readonly GaugeRow _ram;
        readonly Config _config;
        MenuItem[] _opacityItems;
        IntPtr _handle = IntPtr.Zero;
        int _reading;
        bool _hiddenForFullScreen;
        Snapshot _lastSnapshot;

        static Strings L { get { return I18n.T; } }

        static string ConfigDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SystemWidget");
            }
        }

        static string ConfigPath
        {
            get { return Path.Combine(ConfigDirectory, "config.json"); }
        }

        static Brush BrushFrom(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        public MainWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowActivated = false;
            Title = "System Widget";

            Config loaded = null;
            try
            {
                if (File.Exists(ConfigPath))
                    loaded = Json.Read<Config>(File.ReadAllText(ConfigPath));
            }
            catch { }
            _config = loaded ?? new Config();
            // DataContractJsonSerializer bypasses field initializers, so a
            // config file written by an older build leaves Lang null. Use()
            // maps null to English, and we write the resolved code back.
            I18n.Use(_config.Lang);
            _config.Lang = L.Code;
            Opacity = (_config.Opacity >= 0.2 && _config.Opacity <= 1.0) ? _config.Opacity : 1.0;

            _root = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = BrushFrom("#F21E2029"),
                BorderBrush = BrushFrom("#22FFFFFF"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 3, 8, 3)
            };

            var rows = new Grid();
            rows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            rows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(142) });
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11) });
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(142) });
            _gpuPower = new GaugeRow("GPU W");
            _vram = new GaugeRow("VRAM");
            _cpu = new GaugeRow("CPU");
            _ram = new GaugeRow("RAM");

            Grid.SetRow(_gpuPower.Root, 0);
            Grid.SetColumn(_gpuPower.Root, 0);
            Grid.SetRow(_vram.Root, 1);
            Grid.SetColumn(_vram.Root, 0);
            Grid.SetRow(_cpu.Root, 0);
            Grid.SetColumn(_cpu.Root, 2);
            Grid.SetRow(_ram.Root, 1);
            Grid.SetColumn(_ram.Root, 2);

            var separator = new Border
            {
                Width = 1,
                Background = BrushFrom("#20FFFFFF"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            Grid.SetRow(separator, 0);
            Grid.SetRowSpan(separator, 2);
            Grid.SetColumn(separator, 1);

            rows.Children.Add(_gpuPower.Root);
            rows.Children.Add(_vram.Root);
            rows.Children.Add(_cpu.Root);
            rows.Children.Add(_ram.Root);
            rows.Children.Add(separator);
            _root.Child = rows;
            Content = _root;

            BuildMenu();
            Metrics.PrimeCpu();

            MouseLeftButtonDown += delegate
            {
                try
                {
                    DragMove();
                    SavePosition();
                }
                catch { }
            };

            Loaded += delegate
            {
                _handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                ApplyPosition();
                RefreshMetrics();

                var refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                refreshTimer.Tick += delegate { RefreshMetrics(); };
                refreshTimer.Start();

                var topTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                topTimer.Tick += delegate { AssertTopMost(); };
                topTimer.Start();
            };
        }

        // A game running borderless-fullscreen is just a window covering the
        // whole monitor, so SHQueryUserNotificationState misses it - which is
        // exactly the common case. We compare the foreground window to its
        // monitor instead. Monitor bounds, not work area: a merely maximized
        // window stops at the taskbar and must NOT count as full screen.
        static bool ForegroundIsFullScreen(IntPtr self)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == self) return false;

            // The desktop and the shell permanently span the screen.
            var className = new StringBuilder(64);
            GetClassName(foreground, className, className.Capacity);
            string name = className.ToString();
            if (name == "Progman" || name == "WorkerW" || name == "Shell_TrayWnd" ||
                name == "Windows.UI.Core.CoreWindow") return false;

            Rect bounds;
            if (!GetWindowRect(foreground, out bounds)) return false;
            IntPtr monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return false;
            var info = new MonitorInfo();
            info.Size = Marshal.SizeOf(typeof(MonitorInfo));
            if (!GetMonitorInfo(monitor, ref info)) return false;

            return bounds.Left <= info.Monitor.Left && bounds.Top <= info.Monitor.Top
                && bounds.Right >= info.Monitor.Right && bounds.Bottom >= info.Monitor.Bottom;
        }

        void AssertTopMost()
        {
            if (_handle == IntPtr.Zero) return;

            bool hide = (_config.HideFullScreen ?? true) && ForegroundIsFullScreen(_handle);
            if (hide != _hiddenForFullScreen)
            {
                _hiddenForFullScreen = hide;
                Visibility = hide ? Visibility.Hidden : Visibility.Visible;
            }
            // Re-asserting topmost over a full-screen game can kick it out of
            // its display mode or stutter it, so we stop entirely while hidden.
            if (hide) return;

            SetWindowPos(_handle, NotTopMost, 0, 0, 0, 0, PositionFlags);
            SetWindowPos(_handle, TopMost, 0, 0, 0, 0, PositionFlags);
        }

        void ApplyPosition()
        {
            var workArea = SystemParameters.WorkArea;
            if (_config.X > -9999 && _config.Y > -9999 &&
                _config.X < SystemParameters.VirtualScreenWidth &&
                _config.Y < SystemParameters.VirtualScreenHeight)
            {
                Left = _config.X;
                Top = _config.Y;
            }
            else
            {
                UpdateLayout();
                Left = 8;
                Top = workArea.Bottom - ActualHeight - 4;
                SavePosition();
            }
        }

        void SavePosition()
        {
            _config.X = Left;
            _config.Y = Top;
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigPath, Json.Write(_config));
            }
            catch { }
        }

        static string MemoryText(double usedMb, double totalMb)
        {
            if (totalMb <= 0) return "";
            if (totalMb >= 1024)
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0}/{1:0.#} {2}",
                    usedMb / 1024.0,
                    totalMb / 1024.0,
                    L.UnitGigabyte);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0}/{1:0} {2}", usedMb, totalMb, L.UnitMegabyte);
        }

        void RefreshMetrics()
        {
            if (Interlocked.Exchange(ref _reading, 1) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Snapshot snapshot = null;
                try { snapshot = Metrics.Read(); }
                catch { }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        if (snapshot != null) Render(snapshot);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _reading, 0);
                    }
                }));
            });
        }

        void Render(Snapshot snapshot)
        {
            // Kept so a language change can repaint the tooltips immediately
            // instead of waiting for the next reading.
            _lastSnapshot = snapshot;

            if (snapshot.GpuAvailable)
            {
                _gpuPower.Set(snapshot.GpuPowerPct);
                _vram.Set(snapshot.VramPct);
                _gpuPower.Root.ToolTip = string.Format(
                    CultureInfo.InvariantCulture,
                    L.TipGpuPower,
                    snapshot.GpuWatts,
                    snapshot.GpuPowerLimit,
                    snapshot.GpuPowerPct,
                    snapshot.GpuLoadPct);
                _vram.Root.ToolTip = string.Format(
                    L.TipVideoMemory, MemoryText(snapshot.VramUsedMb, snapshot.VramTotalMb));
            }
            else
            {
                _gpuPower.Unavailable();
                _vram.Unavailable();
                _gpuPower.Root.ToolTip = L.TipNoGpu;
                _vram.Root.ToolTip = L.TipNoGpu;
            }

            _cpu.Set(snapshot.CpuPct);
            _ram.Set(snapshot.RamPct);
            _cpu.Root.ToolTip = L.TipCpu;
            _ram.Root.ToolTip = string.Format(
                L.TipSystemMemory, MemoryText(snapshot.RamUsedMb, snapshot.RamTotalMb));
        }

        void BuildMenu()
        {
            var menu = new ContextMenu();

            var refresh = new MenuItem { Header = L.MenuRefresh };
            refresh.Click += delegate { RefreshMetrics(); };
            menu.Items.Add(refresh);

            var resetPosition = new MenuItem { Header = L.MenuMoveBottomLeft };
            resetPosition.Click += delegate
            {
                UpdateLayout();
                var workArea = SystemParameters.WorkArea;
                Left = 8;
                Top = workArea.Bottom - ActualHeight - 4;
                SavePosition();
            };
            menu.Items.Add(resetPosition);

            var opacityMenu = new MenuItem { Header = L.MenuOpacity };
            double[] values = { 1.0, 0.85, 0.7, 0.55, 0.4 };
            _opacityItems = new MenuItem[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                double value = values[i];
                var item = new MenuItem
                {
                    Header = (int)(value * 100) + "%",
                    IsCheckable = true,
                    IsChecked = Math.Abs(Opacity - value) < 0.01
                };
                item.Click += delegate
                {
                    Opacity = value;
                    _config.Opacity = value;
                    SavePosition();
                    foreach (var opacityItem in _opacityItems)
                        opacityItem.IsChecked = opacityItem == item;
                };
                _opacityItems[i] = item;
                opacityMenu.Items.Add(item);
            }
            menu.Items.Add(opacityMenu);

            var languageMenu = new MenuItem { Header = L.MenuLanguage };
            foreach (Strings entry in I18n.Catalog)
            {
                Strings language = entry;
                var item = new MenuItem
                {
                    Header = language.Native,
                    IsCheckable = true,
                    IsChecked = language.Code == L.Code
                };
                item.Click += delegate
                {
                    I18n.Use(language.Code);
                    _config.Lang = language.Code;
                    SavePosition();
                    BuildMenu();    // the menu itself has to be rebuilt translated
                    if (_lastSnapshot != null) Render(_lastSnapshot);
                };
                languageMenu.Items.Add(item);
            }
            menu.Items.Add(languageMenu);

            var autoStart = new MenuItem
            {
                Header = L.MenuStartWithWindows,
                IsCheckable = true
            };
            string shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "System Widget.lnk");
            autoStart.IsChecked = File.Exists(shortcutPath);
            autoStart.Click += delegate
            {
                try
                {
                    if (autoStart.IsChecked)
                    {
                        var type = Type.GetTypeFromProgID("WScript.Shell");
                        dynamic shell = Activator.CreateInstance(type);
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        shortcut.Save();
                    }
                    else if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                    }
                }
                catch { }
            };
            menu.Items.Add(autoStart);

            var hideFullScreen = new MenuItem
            {
                Header = L.MenuHideFullScreen,
                IsCheckable = true,
                IsChecked = _config.HideFullScreen ?? true
            };
            hideFullScreen.Click += delegate
            {
                _config.HideFullScreen = hideFullScreen.IsChecked;
                SavePosition();
                // Unticking it while hidden must bring the widget straight back.
                if (!hideFullScreen.IsChecked && _hiddenForFullScreen)
                {
                    _hiddenForFullScreen = false;
                    Visibility = Visibility.Visible;
                }
            };
            menu.Items.Add(hideFullScreen);

            var restart = new MenuItem { Header = L.MenuRestart };
            restart.Click += delegate
            {
                try
                {
                    string executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    Process.Start(executable);
                }
                catch { }
            };
            menu.Items.Add(restart);

            menu.Items.Add(new Separator());
            var quit = new MenuItem { Header = L.MenuQuit };
            quit.Click += delegate { Close(); };
            menu.Items.Add(quit);
            _root.ContextMenu = menu;
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // single instance: the new one replaces the old
            var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id == current.Id) continue;
                try
                {
                    process.Kill();
                    process.WaitForExit(2000);
                }
                catch { }
            }

            var app = new Application();
            app.Run(new MainWindow());
        }
    }
}
