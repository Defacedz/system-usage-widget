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
using System.Windows.Markup;
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
        // "dark" (the original) or "ivory". A string rather than an enum for
        // the same reason: a config file written by an older build has no
        // value at all, and Theme.Use() maps anything unknown back to dark.
        [DataMember] public string Theme;
    }

    // ---------- themes ----------
    // Two skins for the same widget. Dark is the original; Ivory is built on
    // Anthropic's palette - Ivory Medium #F0EEE6 is the claude.ai background -
    // so the panel sits on a light Windows taskbar instead of punching a black
    // hole in it. The gauge gradient (green to amber to red) is identical in
    // both skins: it carries meaning, not decoration.
    public class Theme
    {
        public string Name;

        public string Panel;        // widget and menu background
        public string Border;       // widget border at rest
        public string MenuBorder;
        public string Divider;      // the vertical rule between the two halves
        public string Ink;          // menu and tooltip text
        public string Label;        // "GPU" / "VRAM" row labels
        public string Dim;          // thermometer tag, unavailable readings
        public string Mid;          // submenu arrow
        public string Bright;       // temperature figure
        public string Track;        // empty part of a gauge or a thermometer
        public string Sep;          // menu separator
        public string Highlight;    // hovered menu row

        public static readonly Theme Dark = new Theme
        {
            Name = "dark",
            Panel = "#F21E2029", Border = "#22FFFFFF", MenuBorder = "#33FFFFFF",
            Divider = "#20FFFFFF", Ink = "#E8EAF2", Label = "#8B91A7",
            Dim = "#6C7086", Mid = "#9BA0B5", Bright = "#B8BCCB",
            Track = "#303442", Sep = "#26FFFFFF", Highlight = "#2EDA7756"
        };

        public static readonly Theme Ivory = new Theme
        {
            Name = "ivory",
            Panel = "#F2F0EEE6", Border = "#33191919", MenuBorder = "#33191919",
            Divider = "#1F191919", Ink = "#191919", Label = "#6B6A64",
            Dim = "#91918D", Mid = "#6B6A64", Bright = "#40403E",
            Track = "#E3DACC", Sep = "#26191919", Highlight = "#30DA7756"
        };

        public static readonly Theme[] All = { Dark, Ivory };
        public static Theme Current = Dark;

        // Unknown or missing name falls back to the original dark skin.
        public static void Use(string name)
        {
            foreach (Theme t in All)
                if (t.Name == name) { Current = t; return; }
            Current = Dark;
        }
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

        public string MenuTheme, ThemeDark, ThemeIvory;   // appearance submenu

        public string TipGpuPower;      // {0} watts, {1} limit, {2} percent, {3} engine load
        public string TipVideoMemory;   // {0} = amount
        public string TipNoGpu;
        public string TipGpuLoadOnly;   // {0} engine load, when power is not reported
        public string TipNoVram;
        public string TipCpu;
        public string TipSystemMemory;  // {0} = amount
        public string TipTemperature;   // {0} = degrees Celsius

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
                MenuTheme = "Theme", ThemeDark = "Dark", ThemeIvory = "Ivory",
                MenuRestart = "Restart widget",
                MenuQuit = "Quit",
                TipGpuPower = "GPU power: {0:0.0} W / {1:0.0} W ({2:0}%)\nGPU engine load: {3:0}%",
                TipVideoMemory = "Video memory: {0}",
                TipNoGpu = "nvidia-smi not found, or no NVIDIA card detected.",
                TipGpuLoadOnly = "GPU engine load: {0:0}%\nThis GPU does not report its power draw.",
                TipNoVram = "This GPU does not report its video memory.",
                TipCpu = "Total processor usage",
                TipSystemMemory = "System memory: {0}",
                TipTemperature = "Temperature: {0:0} °C",
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
                MenuTheme = "Thème", ThemeDark = "Sombre", ThemeIvory = "Ivoire",
                MenuRestart = "Redémarrer le widget",
                MenuQuit = "Quitter",
                TipGpuPower = "Puissance GPU : {0:0.0} W / {1:0.0} W ({2:0} %)\nCharge du moteur GPU : {3:0} %",
                TipVideoMemory = "Mémoire vidéo : {0}",
                TipNoGpu = "nvidia-smi introuvable, ou aucune carte NVIDIA détectée.",
                TipGpuLoadOnly = "Charge du moteur GPU : {0:0} %\nCette carte ne rapporte pas sa consommation électrique.",
                TipNoVram = "Cette carte ne rapporte pas sa mémoire vidéo.",
                TipCpu = "Utilisation totale du processeur",
                TipSystemMemory = "Mémoire vive : {0}",
                TipTemperature = "Température : {0:0} °C",
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
                MenuTheme = "Tema", ThemeDark = "Oscuro", ThemeIvory = "Marfil",
                MenuRestart = "Reiniciar el widget",
                MenuQuit = "Salir",
                TipGpuPower = "Potencia de la GPU: {0:0.0} W / {1:0.0} W ({2:0} %)\nCarga del motor gráfico: {3:0} %",
                TipVideoMemory = "Memoria de vídeo: {0}",
                TipNoGpu = "No se encuentra nvidia-smi, o no se ha detectado ninguna tarjeta NVIDIA.",
                TipGpuLoadOnly = "Carga del motor gráfico: {0:0} %\nEsta tarjeta no informa de su consumo eléctrico.",
                TipNoVram = "Esta tarjeta no informa de su memoria de vídeo.",
                TipCpu = "Uso total del procesador",
                TipSystemMemory = "Memoria del sistema: {0}",
                TipTemperature = "Temperatura: {0:0} °C",
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
                MenuTheme = "Design", ThemeDark = "Dunkel", ThemeIvory = "Elfenbein",
                MenuRestart = "Widget neu starten",
                MenuQuit = "Beenden",
                TipGpuPower = "GPU-Leistung: {0:0.0} W / {1:0.0} W ({2:0} %)\nGPU-Auslastung: {3:0} %",
                TipVideoMemory = "Grafikspeicher: {0}",
                TipNoGpu = "nvidia-smi nicht gefunden oder keine NVIDIA-Karte erkannt.",
                TipGpuLoadOnly = "GPU-Auslastung: {0:0} %\nDiese Karte meldet ihre Leistungsaufnahme nicht.",
                TipNoVram = "Diese Karte meldet ihren Videospeicher nicht.",
                TipCpu = "Gesamte Prozessorauslastung",
                TipSystemMemory = "Arbeitsspeicher: {0}",
                TipTemperature = "Temperatur: {0:0} °C",
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
        // A GPU can answer for some fields and not others - laptop cards very
        // often report power.draw as [N/A] - so each block says whether it is
        // worth reading. GpuDiag carries the reason when nothing came back.
        public bool GpuPowerKnown;
        public bool VramKnown;
        public string GpuDiag;
        public double GpuTempC = -1;    // -1 = not available
        public double CpuTempC = -1;
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
            string raw = null;
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = NvidiaSmiPath(),
                    Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total,power.draw,power.limit,temperature.gpu --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var process = Process.Start(start))
                {
                    raw = process.StandardOutput.ReadLine();
                    if (!process.WaitForExit(3000))
                    {
                        try { process.Kill(); } catch { }
                        snapshot.GpuDiag = "nvidia-smi did not answer within 3 s";
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                snapshot.GpuDiag = "nvidia-smi: " + e.Message;
                return;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                snapshot.GpuDiag = "nvidia-smi answered nothing";
                return;
            }
            string[] values = raw.Split(',');

            // Every field is read on its own. Laptop cards in particular report
            // power.draw and power.limit as [N/A], and refusing the whole line
            // over them used to leave the panel empty on machines where load,
            // memory and temperature were all perfectly readable.
            double load = 0, used = 0, total = 0, watts = 0, limit = 0, temp = -1;
            bool haveLoad = values.Length >= 1 && TryNumber(values[0], out load);
            bool haveUsed = values.Length >= 2 && TryNumber(values[1], out used);
            bool haveTotal = values.Length >= 3 && TryNumber(values[2], out total);
            bool haveWatts = values.Length >= 4 && TryNumber(values[3], out watts);
            bool haveLimit = values.Length >= 5 && TryNumber(values[4], out limit);
            bool haveTemp = values.Length >= 6 && TryNumber(values[5], out temp);

            if (!haveLoad && !(haveUsed && haveTotal))
            {
                snapshot.GpuDiag = "nothing readable in: " + raw.Trim();
                return;
            }

            snapshot.GpuAvailable = true;
            snapshot.GpuLoadPct = haveLoad ? Clamp(load) : 0;

            snapshot.VramKnown = haveUsed && haveTotal && total > 0;
            if (snapshot.VramKnown)
            {
                snapshot.VramUsedMb = used;
                snapshot.VramTotalMb = total;
                snapshot.VramPct = Clamp(100.0 * used / total);
            }

            snapshot.GpuPowerKnown = haveWatts && haveLimit && limit > 0;
            if (snapshot.GpuPowerKnown)
            {
                snapshot.GpuWatts = watts;
                snapshot.GpuPowerLimit = limit;
                snapshot.GpuPowerPct = Clamp(100.0 * watts / limit);
            }

            if (haveTemp) snapshot.GpuTempC = temp;
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

        // CPU temperature through the embedded LibreHardwareMonitor library
        // (MIT), the engine behind most sensor tools. It reads the CPU's own
        // sensor via its ring-0 helper, which requires administrator rights:
        // without them the sensor list stays empty and the thermometer stays
        // blank - never a wrong number. The ACPI "thermal zone" was tried
        // first and abandoned: it is a chipset probe stuck near 28 degrees C
        // while the CPU sits at 55.
        static LibreHardwareMonitor.Hardware.Computer _computer;
        static bool _lhmBroken;

        static void ReadCpuTemperature(Snapshot snapshot)
        {
            if (_lhmBroken) return;
            try
            {
                if (_computer == null)
                {
                    _computer = new LibreHardwareMonitor.Hardware.Computer { IsCpuEnabled = true };
                    _computer.Open();
                }
                foreach (LibreHardwareMonitor.Hardware.IHardware hw in _computer.Hardware)
                {
                    if (hw.HardwareType != LibreHardwareMonitor.Hardware.HardwareType.Cpu) continue;
                    hw.Update();
                    double package = -1, coreMax = -1, any = -1;
                    foreach (LibreHardwareMonitor.Hardware.ISensor s in hw.Sensors)
                    {
                        if (s.SensorType != LibreHardwareMonitor.Hardware.SensorType.Temperature
                            || !s.Value.HasValue) continue;
                        string name = s.Name ?? "";
                        if (name.IndexOf("TjMax", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        double v = s.Value.Value;
                        if (v <= 0 || v >= 120) continue;
                        if (name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Tctl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Tdie", StringComparison.OrdinalIgnoreCase) >= 0)
                            package = Math.Max(package, v);
                        else if (name.IndexOf("Core Max", StringComparison.OrdinalIgnoreCase) >= 0)
                            coreMax = Math.Max(coreMax, v);
                        else
                            any = Math.Max(any, v);
                    }
                    double best = package >= 0 ? package : (coreMax >= 0 ? coreMax : any);
                    if (best >= 0) snapshot.CpuTempC = best;
                }
            }
            catch { _lhmBroken = true; }
        }

        public static Snapshot Read()
        {
            var snapshot = new Snapshot();
            snapshot.CpuPct = CpuUsage();
            ReadMemory(snapshot);
            ReadGpu(snapshot);
            ReadCpuTemperature(snapshot);
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
        TextBlock _label;

        static Brush BrushFrom(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        public GaugeRow(string name)
        {
            Name = name;
            Root = new Grid { Height = 18, Width = 110 };
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

            // Centred so GPU/VRAM (and CPU/RAM) share the same axis.
            _label = new TextBlock
            {
                Text = name,
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom(Theme.Current.Label),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_label, 0);
            Root.Children.Add(_label);

            Percent = new TextBlock
            {
                Text = "--",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom("#DA7756"),
                VerticalAlignment = VerticalAlignment.Center,
                // nudge up so its baseline meets the smaller label's baseline
                Margin = new Thickness(0, 0, 0, 2)
            };
            Grid.SetColumn(Percent, 1);
            Root.Children.Add(Percent);

            Track = new Border
            {
                Width = 48,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = BrushFrom(Theme.Current.Track),
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

        public static string Color(double value)
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
            Percent.Foreground = BrushFrom(Theme.Current.Dim);
            Fill.Width = 0;
        }

        // Repaints the parts that carry the skin. The gauge gradient is not
        // one of them: green, amber and red mean the same in both themes.
        public void Retheme()
        {
            _label.Foreground = BrushFrom(Theme.Current.Label);
            Track.Background = BrushFrom(Theme.Current.Track);
            if (Percent.Text == "--") Percent.Foreground = BrushFrom(Theme.Current.Dim);
        }
    }

    // Vertical thermometer spanning both gauge rows: micro-label on top, tube
    // above a small bulb, value underneath - all on one centred axis. The
    // colour follows the same green-amber-red gradient as the gauges,
    // mapping 20-90 degrees Celsius onto the tube.
    public class ThermoGauge
    {
        public Canvas Root;
        TextBlock _tag, _value;
        Border _tubeFill, _tubeTrack;
        System.Windows.Shapes.Ellipse _bulbBack;
        System.Windows.Shapes.Ellipse _bulbFill;

        static Brush BrushFrom(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        const double TubeTop = 10, TubeH = 13;

        public ThermoGauge(string tag)
        {
            Root = new Canvas { Width = 20, Height = 36, HorizontalAlignment = HorizontalAlignment.Center };

            _tag = new TextBlock
            {
                Text = tag, FontSize = 6.5, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom(Theme.Current.Dim), Width = 20, TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(_tag, 0); Canvas.SetTop(_tag, 0);
            Root.Children.Add(_tag);

            _tubeTrack = new Border
            {
                Width = 4, Height = TubeH, CornerRadius = new CornerRadius(2),
                Background = BrushFrom(Theme.Current.Track)
            };
            Canvas.SetLeft(_tubeTrack, 8); Canvas.SetTop(_tubeTrack, TubeTop);
            Root.Children.Add(_tubeTrack);

            // The bulb overlaps the tube and its fill is the same size as its
            // track: no ring, no seam - one continuous thermometer.
            _bulbBack = new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6, Fill = BrushFrom(Theme.Current.Track)
            };
            Canvas.SetLeft(_bulbBack, 7); Canvas.SetTop(_bulbBack, TubeTop + TubeH - 2);
            Root.Children.Add(_bulbBack);

            _tubeFill = new Border
            {
                Width = 4, Height = 0, CornerRadius = new CornerRadius(2),
                Background = Brushes.Transparent
            };
            Canvas.SetLeft(_tubeFill, 8); Canvas.SetTop(_tubeFill, TubeTop + TubeH);
            Root.Children.Add(_tubeFill);

            _bulbFill = new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6, Fill = Brushes.Transparent
            };
            Canvas.SetLeft(_bulbFill, 7); Canvas.SetTop(_bulbFill, TubeTop + TubeH - 2);
            Root.Children.Add(_bulbFill);

            _value = new TextBlock
            {
                Text = "", FontSize = 8.5, Foreground = BrushFrom(Theme.Current.Bright),
                Width = 20, TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(_value, 0); Canvas.SetTop(_value, 26.5);
            Root.Children.Add(_value);
        }

        // degrees < 0 means "unknown": everything goes blank instead of lying
        public void Set(double degrees)
        {
            if (degrees < 0)
            {
                _value.Text = "";
                _tubeFill.Height = 0;
                _bulbFill.Fill = Brushes.Transparent;
                return;
            }
            double fraction = Math.Max(0, Math.Min(1, (degrees - 20) / 70.0));
            string color = GaugeRow.Color(fraction * 100);
            double height = Math.Max(3, TubeH * fraction);
            // +2 dips the fill into the bulb so tube and bulb read as one
            _tubeFill.Height = height + 2;
            Canvas.SetTop(_tubeFill, TubeTop + (TubeH - height));
            _tubeFill.Background = BrushFrom(color);
            _bulbFill.Fill = BrushFrom(color);
            _value.Text = (int)Math.Round(degrees) + "°";
        }

        public void Retheme()
        {
            _tag.Foreground = BrushFrom(Theme.Current.Dim);
            _tubeTrack.Background = BrushFrom(Theme.Current.Track);
            _bulbBack.Fill = BrushFrom(Theme.Current.Track);
            _value.Foreground = BrushFrom(Theme.Current.Bright);
        }

        // Feeds the red-border alert: how hot on the 0-100 gauge scale.
        public static double AlertPct(double degrees)
        {
            return degrees < 0 ? 0 : Math.Max(0, Math.Min(100, (degrees - 20) / 70.0 * 100));
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
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr handle, uint command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string className, string title);
        const uint MonitorDefaultToNearest = 2;
        const uint GetWindowPrevious = 3;

        readonly Border _root;
        Border _divider;
        readonly GaugeRow _gpuPower;
        readonly GaugeRow _vram;
        readonly GaugeRow _cpu;
        readonly GaugeRow _ram;
        readonly ThermoGauge _gpuThermo;
        readonly ThermoGauge _cpuThermo;

        // The thermometer spans the two gauge rows. Its axis sits on the
        // OPTICAL midpoint: between the end of the bars and the separator
        // line for the left half, and the window border for the right half -
        // a plain centring in the column landed a few pixels left of both.
        static Grid MakeHalf(GaugeRow top, GaugeRow bottom, ThermoGauge thermo, bool rightHalf)
        {
            var half = new Grid { Width = 148 };
            half.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            half.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            half.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            half.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            thermo.Root.HorizontalAlignment = HorizontalAlignment.Left;
            thermo.Root.Margin = new Thickness(rightHalf ? 13.5 : 11.75, 0, 0, 0);

            Grid.SetRow(top.Root, 0); Grid.SetColumn(top.Root, 0);
            Grid.SetRow(bottom.Root, 1); Grid.SetColumn(bottom.Root, 0);
            Grid.SetRow(thermo.Root, 0); Grid.SetRowSpan(thermo.Root, 2); Grid.SetColumn(thermo.Root, 1);
            half.Children.Add(top.Root);
            half.Children.Add(bottom.Root);
            half.Children.Add(thermo.Root);
            return half;
        }
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
            Theme.Use(_config.Theme);
            _config.Theme = Theme.Current.Name;
            Opacity = (_config.Opacity >= 0.2 && _config.Opacity <= 1.0) ? _config.Opacity : 1.0;

            _root = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = BrushFrom(Theme.Current.Panel),
                BorderBrush = BrushFrom(Theme.Current.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 3, 8, 3)
            };

            var rows = new Grid();
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11) });
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            _gpuPower = new GaugeRow("GPU");
            _vram = new GaugeRow("VRAM");
            _cpu = new GaugeRow("CPU");
            _ram = new GaugeRow("RAM");
            _gpuThermo = new ThermoGauge("GPU");
            _cpuThermo = new ThermoGauge("CPU");

            var left = MakeHalf(_gpuPower, _vram, _gpuThermo, false);
            Grid.SetColumn(left, 0);
            rows.Children.Add(left);

            var right = MakeHalf(_cpu, _ram, _cpuThermo, true);
            Grid.SetColumn(right, 2);
            rows.Children.Add(right);

            _divider = new Border
            {
                Width = 1,
                Background = BrushFrom(Theme.Current.Divider),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            Grid.SetColumn(_divider, 1);
            rows.Children.Add(_divider);
            _root.Child = rows;
            Content = _root;

            // Window-level skin: the dark rounded tooltips apply everywhere.
            try { Resources.MergedDictionaries.Add(MenuSkin()); }
            catch { }

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

        // The widget is above the taskbar exactly when it comes before it in
        // the z-order: walking upwards from the taskbar must reach our handle.
        static bool IsAboveTaskbar(IntPtr self)
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero) return false;
            IntPtr handle = taskbar;
            for (int i = 0; i < 512; i++)
            {
                handle = GetWindow(handle, GetWindowPrevious);
                if (handle == IntPtr.Zero) break;
                if (handle == self) return true;
            }
            return false;
        }

        bool _menuOpen;

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

            // The blind NotTopMost/TopMost dance every 500 ms caused a visible
            // flicker (and fought the context menu). Re-assert only when the
            // taskbar has actually climbed above us.
            if (_menuOpen || IsAboveTaskbar(_handle)) return;

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
                // A card that does not report its power draw still reports how
                // busy it is, so the gauge shows the engine load rather than an
                // empty bar - and the tooltip says which of the two it is.
                _gpuPower.Set(snapshot.GpuPowerKnown ? snapshot.GpuPowerPct : snapshot.GpuLoadPct);
                if (snapshot.VramKnown) _vram.Set(snapshot.VramPct); else _vram.Unavailable();
                _gpuThermo.Set(snapshot.GpuTempC);
                string gpuTip = snapshot.GpuPowerKnown
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        L.TipGpuPower,
                        snapshot.GpuWatts,
                        snapshot.GpuPowerLimit,
                        snapshot.GpuPowerPct,
                        snapshot.GpuLoadPct)
                    : string.Format(CultureInfo.InvariantCulture, L.TipGpuLoadOnly, snapshot.GpuLoadPct);
                if (snapshot.GpuTempC >= 0)
                {
                    string tempTip = string.Format(CultureInfo.InvariantCulture, L.TipTemperature, snapshot.GpuTempC);
                    gpuTip += "\n" + tempTip;
                    _gpuThermo.Root.ToolTip = tempTip;
                }
                _gpuPower.Root.ToolTip = gpuTip;
                _vram.Root.ToolTip = snapshot.VramKnown
                    ? string.Format(L.TipVideoMemory, MemoryText(snapshot.VramUsedMb, snapshot.VramTotalMb))
                    : L.TipNoVram;
            }
            else
            {
                _gpuPower.Unavailable();
                _vram.Unavailable();
                _gpuThermo.Set(-1);
                // The reason goes in the tooltip: with no log file, that is
                // where someone can see what nvidia-smi actually said.
                string noGpu = L.TipNoGpu;
                if (!string.IsNullOrEmpty(snapshot.GpuDiag)) noGpu += "\n" + snapshot.GpuDiag;
                _gpuPower.Root.ToolTip = noGpu;
                _vram.Root.ToolTip = noGpu;
            }

            _cpu.Set(snapshot.CpuPct);
            _ram.Set(snapshot.RamPct);
            _cpuThermo.Set(snapshot.CpuTempC);
            string cpuTip = L.TipCpu;
            if (snapshot.CpuTempC >= 0)
            {
                string tempTip = string.Format(CultureInfo.InvariantCulture, L.TipTemperature, snapshot.CpuTempC);
                cpuTip += "\n" + tempTip;
                _cpuThermo.Root.ToolTip = tempTip;
            }
            _cpu.Root.ToolTip = cpuTip;
            _ram.Root.ToolTip = string.Format(
                L.TipSystemMemory, MemoryText(snapshot.RamUsedMb, snapshot.RamTotalMb));

            // Red frame when anything enters the red zone (>= 90 on the
            // gauge scale; for temperatures that is about 83 degrees).
            double gpuGauge = !snapshot.GpuAvailable ? 0
                : (snapshot.GpuPowerKnown ? snapshot.GpuPowerPct : snapshot.GpuLoadPct);
            double worst = Math.Max(
                Math.Max(gpuGauge, snapshot.CpuPct),
                Math.Max(snapshot.VramKnown ? snapshot.VramPct : 0, snapshot.RamPct));
            worst = Math.Max(worst, ThermoGauge.AlertPct(snapshot.GpuTempC));
            worst = Math.Max(worst, ThermoGauge.AlertPct(snapshot.CpuTempC));
            _root.BorderBrush = BrushFrom(worst >= 90 ? "#CCE05252" : Theme.Current.Border);
        }

        static int SchTasks(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var process = Process.Start(start))
                {
                    if (!process.WaitForExit(5000)) { try { process.Kill(); } catch { } return -1; }
                    return process.ExitCode;
                }
            }
            catch { return -1; }
        }

        // Claude-styled skin for the context menu: dark rounded panel, orange
        // highlight, same palette as the widget. Replaces the gray system look.
        const string MenuSkinXaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style TargetType='ContextMenu'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ContextMenu'>
          <Border Background='@PANEL@' BorderBrush='@MENUBORDER@' BorderThickness='1'
                  CornerRadius='7' Padding='4' MinWidth='170'>
            <ItemsPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='Separator'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Separator'>
          <Border Height='1' Background='@SEP@' Margin='6,3'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='ToolTip'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Foreground' Value='@INK@'/>
    <Setter Property='FontSize' Value='11'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ToolTip'>
          <Border Background='@PANEL@' BorderBrush='@MENUBORDER@' BorderThickness='1'
                  CornerRadius='6' Padding='9,6'>
            <ContentPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='MenuItem'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Foreground' Value='@INK@'/>
    <Setter Property='FontSize' Value='11.5'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='MenuItem'>
          <Border x:Name='Bd' Background='Transparent' CornerRadius='4' Padding='8,5'>
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='15'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='12'/>
              </Grid.ColumnDefinitions>
              <TextBlock x:Name='Check' Text='&#x2713;' FontSize='10' Foreground='#DA7756'
                         Visibility='Hidden' VerticalAlignment='Center'/>
              <ContentPresenter Grid.Column='1' ContentSource='Header' VerticalAlignment='Center'/>
              <TextBlock x:Name='Arrow' Grid.Column='2' Text='&#x203A;' FontSize='12'
                         Foreground='@MID@' Visibility='Hidden'
                         VerticalAlignment='Center' HorizontalAlignment='Right'/>
              <Popup x:Name='PART_Popup' Placement='Right' HorizontalOffset='2' VerticalOffset='-6'
                     IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}'
                     AllowsTransparency='True' Focusable='False'>
                <Border Background='@PANEL@' BorderBrush='@MENUBORDER@' BorderThickness='1'
                        CornerRadius='7' Padding='4' MinWidth='110'>
                  <ItemsPresenter/>
                </Border>
              </Popup>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='@HIGHLIGHT@'/>
            </Trigger>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='Check' Property='Visibility' Value='Visible'/>
            </Trigger>
            <Trigger Property='HasItems' Value='True'>
              <Setter TargetName='Arrow' Property='Visibility' Value='Visible'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Foreground' Value='@DIM@'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";

        static ResourceDictionary _menuSkin;
        static string _menuSkinFor;     // the theme the cached skin was built for

        static ResourceDictionary MenuSkin()
        {
            Theme t = Theme.Current;
            if (_menuSkin != null && _menuSkinFor == t.Name) return _menuSkin;
            string xaml = MenuSkinXaml
                .Replace("@PANEL@", t.Panel)
                .Replace("@MENUBORDER@", t.MenuBorder)
                .Replace("@SEP@", t.Sep)
                .Replace("@INK@", t.Ink)
                .Replace("@MID@", t.Mid)
                .Replace("@DIM@", t.Dim)
                .Replace("@HIGHLIGHT@", t.Highlight);
            _menuSkin = (ResourceDictionary)XamlReader.Parse(xaml);
            _menuSkinFor = t.Name;
            return _menuSkin;
        }

        // Everything the theme touches, repainted in one place. The window-level
        // dictionary carries the tooltip style, so it is swapped as well.
        void ApplyTheme()
        {
            _root.Background = BrushFrom(Theme.Current.Panel);
            _divider.Background = BrushFrom(Theme.Current.Divider);
            _gpuPower.Retheme(); _vram.Retheme(); _cpu.Retheme(); _ram.Retheme();
            _gpuThermo.Retheme(); _cpuThermo.Retheme();
            Resources.MergedDictionaries.Clear();
            try { Resources.MergedDictionaries.Add(MenuSkin()); } catch { }
            BuildMenu();
            if (_lastSnapshot != null) Render(_lastSnapshot);
            else _root.BorderBrush = BrushFrom(Theme.Current.Border);
        }

        void BuildMenu()
        {
            var menu = new ContextMenu();
            try { menu.Resources.MergedDictionaries.Add(MenuSkin()); }
            catch { }
            // AssertTopMost must not fight the open menu's popup for the z-order.
            menu.Opened += delegate { _menuOpen = true; };
            menu.Closed += delegate { _menuOpen = false; };

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

            var themeMenu = new MenuItem { Header = L.MenuTheme };
            foreach (Theme entry in Theme.All)
            {
                Theme theme = entry;
                var item = new MenuItem
                {
                    Header = theme == Theme.Dark ? L.ThemeDark : L.ThemeIvory,
                    IsCheckable = true,
                    IsChecked = theme == Theme.Current
                };
                item.Click += delegate
                {
                    Theme.Use(theme.Name);
                    _config.Theme = theme.Name;
                    SavePosition();
                    ApplyTheme();
                };
                themeMenu.Items.Add(item);
            }
            menu.Items.Add(themeMenu);

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

            // Autostart is a scheduled task with highest privileges: the
            // embedded sensor library needs administrator rights, and the
            // task grants them at logon without a UAC prompt. The old
            // Startup-folder shortcut (which started the widget unelevated)
            // is removed when the option is toggled.
            var autoStart = new MenuItem
            {
                Header = L.MenuStartWithWindows,
                IsCheckable = true
            };
            string shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "System Widget.lnk");
            autoStart.IsChecked = SchTasks("/Query /TN \"SystemWidget\"") == 0 || File.Exists(shortcutPath);
            autoStart.Click += delegate
            {
                try
                {
                    if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
                    if (autoStart.IsChecked)
                    {
                        string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        SchTasks("/Create /F /SC ONLOGON /RL HIGHEST /TN \"SystemWidget\" /TR \"\\\"" + exe + "\\\"\"");
                    }
                    else
                    {
                        SchTasks("/Delete /F /TN \"SystemWidget\"");
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
