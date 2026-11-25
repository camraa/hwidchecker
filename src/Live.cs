// Mesures en direct et graphique.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace HwidChecker
{
    public class Sample
    {
        public double Cpu = double.NaN, Ram = double.NaN, Gpu = double.NaN, Vram = double.NaN, GpuTemp = double.NaN, ZoneTemp = double.NaN;
        public string RamText = "", VramText = "", GpuName = "";
    }

    /// <summary>Lecture des compteurs. Appele depuis un seul fil d'arriere-plan a la fois.</summary>
    public static class Live
    {
        [DllImport("kernel32.dll")]
        static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        [StructLayout(LayoutKind.Sequential)]
        class MemStatus
        {
            public uint Length = (uint)Marshal.SizeOf(typeof(MemStatus)), Load;
            public ulong Total, Avail, TotalPage, AvailPage, TotalVirtual, AvailVirtual, AvailExt;
        }
        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx([In, Out] MemStatus m);

        // NVML : bibliotheque du pilote NVIDIA
        [DllImport("nvml.dll")] static extern int nvmlInit_v2();
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetHandleByIndex_v2(uint i, out IntPtr dev);
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetTemperature(IntPtr dev, int sensor, out uint t);
        [StructLayout(LayoutKind.Sequential)] struct Util { public uint Gpu, Memory; }
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetUtilizationRates(IntPtr dev, out Util u);
        [StructLayout(LayoutKind.Sequential)] struct Mem { public ulong Total, Free, Used; }
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetMemoryInfo(IntPtr dev, out Mem m);
        [DllImport("nvml.dll", CharSet = CharSet.Ansi)] static extern int nvmlDeviceGetName(IntPtr dev, StringBuilder name, uint len);

        static long lastIdle, lastTotal;
        static IntPtr nv;
        static bool nvTried, zoneOff, engineOff;
        static double lastZone = double.NaN;
        static int tick;

        public static Sample Read()
        {
            var s = new Sample();
            long idle, kernel, user;
            if (GetSystemTimes(out idle, out kernel, out user))
            {
                long total = kernel + user; // le temps noyau inclut l'inactivite
                if (lastTotal > 0 && total > lastTotal)
                    s.Cpu = Math.Max(0, Math.Min(100, 100.0 * (1 - (double)(idle - lastIdle) / (total - lastTotal))));
                lastIdle = idle;
                lastTotal = total;
            }
            var m = new MemStatus();
            if (GlobalMemoryStatusEx(m))
            {
                s.Ram = 100.0 * (m.Total - m.Avail) / m.Total;
                s.RamText = Gb(m.Total - m.Avail) + " / " + Gb(m.Total);
            }
            if (!nvTried)
            {
                nvTried = true;
                try { if (nvmlInit_v2() != 0 || nvmlDeviceGetHandleByIndex_v2(0, out nv) != 0) nv = IntPtr.Zero; }
                catch { nv = IntPtr.Zero; } // pas de pilote NVIDIA
            }
            if (nv != IntPtr.Zero)
            {
                Util u; Mem v; uint t;
                if (nvmlDeviceGetUtilizationRates(nv, out u) == 0) s.Gpu = u.Gpu;
                if (nvmlDeviceGetTemperature(nv, 0, out t) == 0) s.GpuTemp = t;
                if (nvmlDeviceGetMemoryInfo(nv, out v) == 0 && v.Total > 0)
                {
                    s.Vram = 100.0 * v.Used / v.Total;
                    s.VramText = Gb(v.Used) + " / " + Gb(v.Total);
                }
                var name = new StringBuilder(96);
                if (nvmlDeviceGetName(nv, name, 96) == 0) s.GpuName = name.ToString();
            }
            else if (!engineOff) s.Gpu = EngineLoad();

            if (!zoneOff && tick++ % 2 == 0) lastZone = Zone();
            s.ZoneTemp = lastZone;
            return s;
        }

        static string Gb(ulong b) { return (b / 1073741824.0).ToString("0.0", CultureInfo.GetCultureInfo("fr-FR")) + " Go"; }

        static double Zone()
        {
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT HighPrecisionTemperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation"))
                {
                    double best = double.NaN;
                    foreach (ManagementObject o in q.Get())
                    {
                        double c = Convert.ToDouble(o["HighPrecisionTemperature"]) / 10 - 273.15;
                        if (c > 5 && c < 120 && (double.IsNaN(best) || c > best)) best = c;
                    }
                    if (double.IsNaN(best)) zoneOff = true;
                    return best;
                }
            }
            catch { zoneOff = true; return double.NaN; }
        }

        // Cartes AMD et Intel : charge 3D vue par Windows.
        static double EngineLoad()
        {
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE '%engtype_3D'"))
                    return Math.Min(100, q.Get().Cast<ManagementObject>().Sum(o => Convert.ToDouble(o["UtilizationPercentage"])));
            }
            catch { engineOff = true; return double.NaN; }
        }
    }

    /// <summary>Courbe d'une seule serie sur 0-100, avec reticule au survol.</summary>
    public class Chart : FrameworkElement
    {
        public const int Capacity = 60;
        readonly List<double> data;
        readonly string unit;
        Point? hover;

        public Chart(List<double> values, string unitText)
        {
            data = values;
            unit = unitText;
            Height = 112;
            MouseMove += (s, e) => { hover = e.GetPosition(this); InvalidateVisual(); };
            MouseLeave += (s, e) => { hover = null; InvalidateVisual(); };
        }

        Brush Res(string key) { return TryFindResource(key) as Brush ?? Brushes.Gray; }

        FormattedText Label(string t, double size, string brush, bool bold)
        {
            return new FormattedText(t, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, Res(brush), 1.0);
        }

        protected override void OnRender(DrawingContext d)
        {
            double w = ActualWidth, h = ActualHeight, left = 30, right = 8, top = 8, bottom = 8;
            if (w < 80) return;
            d.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // zone de survol entiere
            double pw = w - left - right, ph = h - top - bottom;
            var grid = new Pen(Res("Sep"), 1);
            foreach (int v in new[] { 0, 50, 100 })
            {
                double y = Math.Round(top + ph * (1 - v / 100.0)) + .5;
                d.DrawLine(grid, new Point(left, y), new Point(w - right, y));
                var t = Label(v.ToString(), 10, "Text3", false);
                d.DrawText(t, new Point(left - 7 - t.Width, y - t.Height / 2));
            }

            Func<int, double> X = i => left + pw * (Capacity - data.Count + i) / (Capacity - 1.0);
            Func<double, double> Y = v => top + ph * (1 - Math.Max(0, Math.Min(100, v)) / 100);
            var pts = new List<int>();
            for (int i = 0; i < data.Count; i++) if (!double.IsNaN(data[i])) pts.Add(i);
            if (pts.Count == 0) return;

            var accent = Res("Accent") as SolidColorBrush;
            if (accent == null) return;
            if (pts.Count > 1)
            {
                var line = new StreamGeometry();
                using (var c = line.Open())
                {
                    c.BeginFigure(new Point(X(pts[0]), Y(data[pts[0]])), false, false);
                    c.PolyLineTo(pts.Skip(1).Select(i => new Point(X(i), Y(data[i]))).ToList(), true, true);
                }
                var area = new StreamGeometry();
                using (var c = area.Open())
                {
                    c.BeginFigure(new Point(X(pts[0]), top + ph), true, true);
                    c.PolyLineTo(pts.Select(i => new Point(X(i), Y(data[i]))).Concat(new[] { new Point(X(pts[pts.Count - 1]), top + ph) }).ToList(), false, false);
                }
                d.DrawGeometry(new SolidColorBrush(accent.Color) { Opacity = .1 }, null, area);
                d.DrawGeometry(null, new Pen(accent, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line);
            }

            int mark = pts[pts.Count - 1];
            if (hover.HasValue)
            {
                mark = pts.OrderBy(i => Math.Abs(X(i) - hover.Value.X)).First();
                d.DrawLine(new Pen(Res("Text3"), 1), new Point(Math.Round(X(mark)) + .5, top), new Point(Math.Round(X(mark)) + .5, top + ph));
            }
            var p = new Point(X(mark), Y(data[mark]));
            d.DrawEllipse(Res("Surface"), null, p, 6, 6); // anneau de la couleur du fond
            d.DrawEllipse(accent, null, p, 4, 4);

            if (hover.HasValue)
            {
                int ago = data.Count - 1 - mark;
                var t = Label(data[mark].ToString("0") + " " + unit + (ago == 0 ? "" : "  ·  −" + ago + " s"), 11.5, "Text", true);
                double bw = t.Width + 18, bh = t.Height + 8;
                double bx = p.X + 12 + bw > w ? p.X - 12 - bw : p.X + 12, by = Math.Max(0, Math.Min(h - bh, p.Y - bh / 2));
                d.DrawRoundedRectangle(Res("Tip"), new Pen(Res("CardBorder"), 1), new Rect(bx, by, bw, bh), 8, 8);
                d.DrawText(t, new Point(bx + 9, by + 4));
            }
        }
    }
}
