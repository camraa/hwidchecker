// Pages : anti-triche, direct, historique, partage, mises a jour.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HwidChecker
{
    public class HistoryEntry
    {
        public string Date { get; set; }
        public List<string[]> Changes { get; set; } // chemin, avant, maintenant
    }

    /// <summary>Identifiants du dernier lancement et liste des changements, gardes sur ce PC uniquement.</summary>
    public class History
    {
        public Dictionary<string, string> Last { get; set; }
        public List<HistoryEntry> Entries { get; set; }

        static string FileName
        {
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HWID Checker", "history.json"); }
        }

        public static History Load()
        {
            try
            {
                var h = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<History>(File.ReadAllText(FileName));
                if (h != null && h.Entries != null) return h;
            }
            catch { /* premier lancement */ }
            return new History { Entries = new List<HistoryEntry>() };
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FileName));
                File.WriteAllText(FileName, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(this));
            }
            catch { /* dossier inaccessible : l'historique reste en memoire */ }
        }

        public static List<string[]> Diff(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            var l = new List<string[]>();
            foreach (var k in a.Keys.Union(b.Keys))
            {
                string x, y;
                a.TryGetValue(k, out x);
                b.TryGetValue(k, out y);
                if (x != y) l.Add(new[] { k, x, y });
            }
            return l;
        }

        public void Record(Dictionary<string, string> now, DateTime date)
        {
            Entries.Insert(0, new HistoryEntry
            {
                Date = date.ToString("yyyy-MM-dd HH:mm"), Changes = Last == null ? new List<string[]>() : Diff(Last, now)
            });
            if (Entries.Count > 200) Entries.RemoveRange(200, Entries.Count - 200);
            Last = now;
            Save();
        }
    }

    public partial class App
    {
        public const string VersionText = "1.1.1";
        const string Repo = "https://github.com/camraa/hwidchecker";

        // ================= anti-triche =================

        Border Hero(string icon, string color, string title, string desc, UIElement extra)
        {
            var h = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            var tile = Tile(icon, color, 56);
            tile.HorizontalAlignment = HorizontalAlignment.Center;
            h.Children.Add(tile);
            var t = Text(title, 19, null, true);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 10, 0, 3);
            h.Children.Add(t);
            var d = Text(desc, 13, "Text2", false);
            d.TextAlignment = TextAlignment.Center;
            d.MaxWidth = 600;
            h.Children.Add(d);
            if (extra != null) h.Children.Add(extra);
            return Card(h);
        }

        Panel AntiCheatPage()
        {
            var p = PageRoot();
            p.Children.Add(Hero("game", "#BF5AF2", "Anti-triche",
                "Compare ce PC aux conditions publiées par chaque anti-triche. Elles évoluent : la page officielle fait foi.", null));
            foreach (var a in AntiCheat.Evaluate(report))
            {
                var rows = new List<UIElement>
                {
                    Row(new Field { Label = "Résultat", Value = a.Verdict, State = a.State, Help = a.Note }, false)
                };
                rows.AddRange(a.Needs.Select(n => (UIElement)Row(new Field
                {
                    Label = n.Label, State = n.Met ? "on" : n.Must ? "off" : "warn",
                    Value = n.Met ? "Rempli" : n.Must ? "Exigé, absent" : "Peut être exigé, absent",
                    Help = n.Met ? Help.For("security", n.Label) ?? a.Note : AntiCheat.Fix(n.Label)
                }, false)));
                rows.Add(Row(new Field { Label = "Page officielle", Value = new Uri(a.Url).Host.Replace("www.", ""), Url = a.Url,
                    Help = "Source des conditions, relevées le 25 novembre 2026." }, false));
                AddGroup(p, a.Name, a.Games, rows);
            }
            return p;
        }

        // ================= direct =================

        readonly Dictionary<string, List<double>> series = new Dictionary<string, List<double>>();
        readonly Dictionary<string, TextBlock> liveValue = new Dictionary<string, TextBlock>(), liveSub = new Dictionary<string, TextBlock>();
        readonly List<Chart> charts = new List<Chart>();
        DispatcherTimer liveTimer;
        bool reading;
        Sample sample;

        Panel LivePage()
        {
            liveValue.Clear();
            liveSub.Clear();
            charts.Clear();
            var p = PageRoot();
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(-7, 0, -7, 0) };
            grid.Children.Add(Gauge("cpu", "Processeur", "%", "Part du temps où le processeur travaille, tous cœurs confondus."));
            grid.Children.Add(Gauge("ram", "Mémoire (RAM)", "%", "Part de la mémoire vive utilisée."));
            grid.Children.Add(Gauge("gpu", "Carte graphique", "%", "Charge du processeur graphique, donnée par le pilote NVIDIA ou par Windows."));
            grid.Children.Add(Gauge("vram", "Mémoire vidéo", "%", "Part de la VRAM utilisée. Disponible avec les cartes NVIDIA."));
            grid.Children.Add(Gauge("gputemp", "Température du GPU", "°C", "Sonde de la puce graphique. Disponible avec les cartes NVIDIA."));
            grid.Children.Add(Gauge("zone", "Température du système", "°C", "Sonde ACPI la plus chaude de la carte mère, souvent proche du processeur. Windows ne donne pas accès aux sondes internes des cœurs."));
            p.Children.Add(grid);
            if (liveTimer == null)
            {
                liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                liveTimer.Tick += (s, e) => LiveTick();
            }
            liveTimer.Start();
            LiveTick();
            return p;
        }

        UIElement Gauge(string key, string title, string unit, string help)
        {
            if (!series.ContainsKey(key)) series[key] = new List<double>();
            var box = new StackPanel { Margin = new Thickness(16, 14, 16, 12) };
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Text(title, 13, "Text2", false));
            head.Children.Add(InfoDot(title, help));
            box.Children.Add(head);
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
            var v = Text("–", 24, null, true);
            var sub = Text("", 12, "Text2", false);
            sub.Margin = new Thickness(10, 7, 0, 0);
            line.Children.Add(v);
            line.Children.Add(sub);
            box.Children.Add(line);
            var c = new Chart(series[key], unit);
            AutomationProperties.SetName(c, L.T(title));
            charts.Add(c);
            box.Children.Add(c);
            liveValue[key] = v;
            liveSub[key] = sub;
            var card = Card(box);
            card.Margin = new Thickness(7, 0, 7, 14);
            return card;
        }

        async void LiveTick()
        {
            if (page != "live" || search.Text.Length > 0) { liveTimer.Stop(); return; }
            if (reading || w.WindowState == WindowState.Minimized) return;
            reading = true;
            try { sample = await Task.Run(() => Live.Read()); }
            catch { return; }
            finally { reading = false; }
            Push("cpu", sample.Cpu, "%", "");
            Push("ram", sample.Ram, "%", sample.RamText);
            Push("gpu", sample.Gpu, "%", sample.GpuName);
            Push("vram", sample.Vram, "%", sample.VramText);
            Push("gputemp", sample.GpuTemp, "°C", "");
            Push("zone", sample.ZoneTemp, "°C", "");
            foreach (var c in charts) c.InvalidateVisual();
        }

        void Push(string key, double v, string unit, string sub)
        {
            var l = series[key];
            l.Add(v);
            if (l.Count > Chart.Capacity) l.RemoveAt(0);
            if (!liveValue.ContainsKey(key)) return;
            bool none = l.All(double.IsNaN);
            liveValue[key].Text = none ? L.T("Non disponible") : double.IsNaN(v) ? "–" : v.ToString("0") + " " + unit;
            liveValue[key].FontSize = none ? 15 : 24;
            if (sub.Length == 0 && !none)
            {
                var ok = l.Where(x => !double.IsNaN(x)).ToList();
                sub = "min " + ok.Min().ToString("0") + " · max " + ok.Max().ToString("0");
            }
            liveSub[key].Text = L.T(sub);
        }

        // ================= historique =================

        readonly History history = History.Load();

        void HistoryList(Panel p)
        {
            if (history.Entries.Count == 0) return;
            foreach (var e in history.Entries.Where(x => x.Changes.Count > 0).Take(30))
            {
                var rows = new List<UIElement>();
                foreach (var c in e.Changes.Take(40))
                {
                    var box = new StackPanel { Margin = new Thickness(14, 8, 14, 8) };
                    box.Children.Add(Text(c[0], 13, null, false));
                    string a = c[1] == null ? L.T("Absent") : Shown(c[1], true), b = c[2] == null ? L.T("Absent") : Shown(c[2], true);
                    var t = new TextBlock { Text = a + "  →  " + b, FontFamily = Mono, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
                    t.SetResourceReference(TextBlock.ForegroundProperty, "Text2");
                    box.Children.Add(t);
                    rows.Add(box);
                }
                AddGroup(p, e.Date, e.Changes.Count == 1 ? "1 changement" : e.Changes.Count + " changements", rows);
            }
            AddGroup(p, "Derniers lancements", null, history.Entries.Take(12).Select(e => (UIElement)Row(new Field
            {
                Label = e.Date, State = e.Changes.Count == 0 ? "on" : "warn",
                Value = e.Changes.Count == 0 ? "Aucun changement" : e.Changes.Count == 1 ? "1 changement" : e.Changes.Count + " changements"
            }, false)));
            var clear = Capsule("Effacer l’historique", false, () =>
            {
                history.Entries.Clear();
                history.Save();
                ShowToast("Historique effacé");
                Render(false);
            });
            clear.HorizontalAlignment = HorizontalAlignment.Center;
            clear.Margin = new Thickness(0, 18, 0, 0);
            p.Children.Add(clear);
        }

        // ================= partage =================

        bool shareMask = true;

        Panel SharePage()
        {
            var p = PageRoot();
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
            var copy = Capsule("Copier l’image", true, () =>
            {
                try { Clipboard.SetImage(ShareImage()); ShowToast("Image copiée"); }
                catch { ShowToast("Presse-papiers occupé, réessayez"); }
            });
            var save = Capsule("Enregistrer l’image", false, SaveShare);
            save.Margin = new Thickness(10, 0, 0, 0);
            buttons.Children.Add(copy);
            buttons.Children.Add(save);
            p.Children.Add(Hero("export", "#30B0C7", "Partager", "Une image de ce PC, à coller dans Discord avec Ctrl+V.", buttons));

            var sw = new CheckBox { Style = (Style)w.Resources["Switch"], IsChecked = shareMask };
            var img = new Image { Stretch = Stretch.Uniform, Source = ShareImage(), Margin = new Thickness(0, 14, 0, 0) };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            sw.Click += (s, e) => { shareMask = sw.IsChecked == true; img.Source = ShareImage(); };
            var opt = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            opt.Children.Add(Card(SettingRow("Masquer les identifiants sur l’image", "Conseillé : un identifiant publié permet de reconnaître ce PC.", sw)));
            p.Children.Add(opt);
            p.Children.Add(img);
            return p;
        }

        void SaveShare()
        {
            var d = new SaveFileDialog { FileName = "HWID " + DateTime.Now.ToString("yyyy-MM-dd"), Filter = "PNG (*.png)|*.png" };
            if (d.ShowDialog(w) != true) return;
            try
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(ShareImage()));
                using (var f = File.Create(d.FileName)) enc.Save(f);
                ShowToast("Image enregistrée");
            }
            catch { ShowToast("Échec de l’enregistrement"); }
        }

        static TextBlock Ink(string t, double size, byte alpha, bool bold)
        {
            return new TextBlock
            {
                Text = t, FontSize = size, Foreground = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255)),
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
            };
        }

        BitmapSource ShareImage()
        {
            Func<string, string> M = v => shareMask ? new string(v.Select(c => char.IsLetterOrDigit(c) ? '•' : c).ToArray()) : v;
            var body = new StackPanel { Margin = new Thickness(34, 30, 34, 26) };

            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(Tile(report.IsLaptop ? "laptop" : "desktop", "#5E5CE6", 58));
            var names = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(Ink(report.Model, 22, 255, true));
            var win = report.Sections.Where(s => s.Key == "windows").SelectMany(s => s.Groups).SelectMany(g => g.Fields).FirstOrDefault(f => f.Label == "Version de Windows");
            names.Children.Add(Ink(win == null ? "" : win.Value, 13.5, 170, false));
            top.Children.Add(names);
            body.Children.Add(top);

            var list = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
            foreach (var s in report.Summary.Where(x => x.Section != "ip")) // les adresses IP ne sont jamais sur l'image
            {
                var sec = report.Sections.First(x => x.Key == s.Section);
                var g = new Grid { Height = 40 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                var tile = Tile(sec.Icon, sec.Color, 26);
                tile.HorizontalAlignment = HorizontalAlignment.Left;
                g.Children.Add(tile);
                var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                mid.Children.Add(Ink(L.T(s.Label), 12.5, 255, true));
                mid.Children.Add(Ink(L.T(s.Sub), 11, 150, false));
                Grid.SetColumn(mid, 1);
                g.Children.Add(mid);
                var val = Ink(s.IsId ? M(s.Value) : L.T(s.Value), 12, 225, false);
                val.FontFamily = Mono;
                val.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(val, 2);
                g.Children.Add(val);
                list.Children.Add(g);
            }
            body.Children.Add(list);

            var states = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            string[] keep = { "TPM 2.0", "Secure Boot", "IOMMU / VT-d", "Hyperviseur", "HVCI", "Intégrité de la mémoire", "Windows Defender" };
            foreach (var f in report.Sections.Where(s => s.Key == "security").SelectMany(s => s.Groups).SelectMany(g => g.Fields).Where(f => keep.Contains(f.Label) && f.State != null))
            {
                var chip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(11, 5, 12, 6) };
                chip.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = StateBrush(f.State), Margin = new Thickness(0, 1, 7, 0), VerticalAlignment = VerticalAlignment.Center });
                chip.Children.Add(Ink(L.T(f.Label) + "  ", 11.5, 255, true));
                chip.Children.Add(Ink(L.T(f.State == "on" ? "Activé" : f.State == "warn" ? "Partiel" : "Désactivé"), 11.5, 160, false));
                states.Children.Add(new Border { CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)), Margin = new Thickness(0, 0, 8, 8), Child = chip });
            }
            body.Children.Add(states);

            var foot = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            var fp = Ink(L.T("Empreinte matérielle") + "  " + M(report.Fingerprint.Substring(0, 16)) + "…", 11.5, 150, false);
            foot.Children.Add(fp);
            var by = Ink("HWID Checker · Made by 1skf", 11.5, 150, false);
            by.HorizontalAlignment = HorizontalAlignment.Right;
            foot.Children.Add(by);
            body.Children.Add(foot);

            var card = new Border
            {
                Width = 860, CornerRadius = new CornerRadius(24), Child = body,
                Background = new LinearGradientBrush(Hex("#26262B"), Hex("#17171A"), 60),
                BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)), BorderThickness = new Thickness(1)
            };
            TextOptions.SetTextFormattingMode(card, TextFormattingMode.Ideal);
            card.SetValue(TextBlock.FontFamilyProperty, w.FontFamily);
            card.Measure(new Size(860, double.PositiveInfinity));
            card.Arrange(new Rect(card.DesiredSize));
            card.UpdateLayout();
            var r = new RenderTargetBitmap((int)card.ActualWidth * 2, (int)Math.Ceiling(card.ActualHeight) * 2, 192, 192, PixelFormats.Pbgra32);
            r.Render(card);
            r.Freeze();
            return r;
        }

        // ================= base IEEE =================

        UIElement OuiCard()
        {
            var g = new Grid { Margin = new Thickness(14, 10, 14, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = Text(Oui.Available ? "Base IEEE des fabricants installée." : "Pour afficher la marque de chaque appareil, téléchargez la base publique de l’IEEE (3,9 Mo).", 13, "Text2", false);
            t.Margin = new Thickness(0, 0, 16, 0);
            g.Children.Add(t);
            Button b = null;
            b = Capsule(Oui.Available ? "Mettre à jour" : "Télécharger", !Oui.Available, async () =>
            {
                b.IsEnabled = false;
                b.Content = L.T("Téléchargement…");
                bool ok = await Task.Run(() => Oui.Download());
                ShowToast(ok ? "Base installée" : "Téléchargement impossible");
                if (ok) Refresh(); else Render(false);
            });
            AutomationProperties.SetAutomationId(b, "oui");
            Grid.SetColumn(b, 1);
            g.Children.Add(b);
            var card = Card(g);
            card.Margin = new Thickness(0, 14, 0, 0);
            return card;
        }

        // ================= mises a jour =================

        string newVersion, updateState = "Non vérifiée";

        async void CheckUpdate(bool manual)
        {
            updateState = "Vérification en cours…";
            string found = await Task.Run(() => LatestVersion());
            Version a, b;
            if (found == null) updateState = "Vérification impossible";
            else if (Version.TryParse(found, out a) && Version.TryParse(VersionText, out b) && a > b)
            {
                newVersion = found;
                updateState = "Version " + found + " disponible";
                if (page == "overview") Render(false);
            }
            else updateState = "À jour";
            if (manual) ShowToast(updateState);
            if (page == "appearance") Render(false);
        }

        static string LatestVersion()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/camraa/hwidchecker/tags");
                req.UserAgent = "HWID-Checker/" + VersionText;
                req.Timeout = req.ReadWriteTimeout = 10000;
                using (var resp = req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                    return Regex.Matches(sr.ReadToEnd(), "\"name\"\\s*:\\s*\"v?(\\d+\\.\\d+\\.\\d+)\"").Cast<Match>()
                        .Select(m => new Version(m.Groups[1].Value)).OrderByDescending(v => v).Select(v => v.ToString()).FirstOrDefault() ?? "0.0.0";
            }
            catch { return null; }
        }

        UIElement UpdateBanner()
        {
            var g = new Grid { Margin = new Thickness(16, 10, 12, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(Text("HWID Checker " + newVersion + " est disponible", 13, null, true));
            var b = Capsule("Voir", true, () => Open(Repo + "/releases/tag/v" + newVersion));
            Grid.SetColumn(b, 1);
            g.Children.Add(b);
            var card = Card(g);
            card.Margin = new Thickness(0, 0, 0, 14);
            return card;
        }
    }
}
