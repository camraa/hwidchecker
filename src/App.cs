// Interface : fenetre, pages, theme et effets de verre.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

[assembly: AssemblyTitle("HWID Checker")]
[assembly: AssemblyProduct("HWID Checker")]
[assembly: AssemblyCompany("1skf")]
[assembly: AssemblyCopyright("Made by 1skf (Discord)")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace HwidChecker
{
    public class Settings
    {
        public string Theme { get; set; }   // auto, light, dark
        public double Glass { get; set; }   // 0 = transparent, 1 = teinte
        public bool Mask { get; set; }
        public string Lang { get; set; }    // fr ou en ; vide = langue de Windows
        public bool SkipUpdateCheck { get; set; }
        public string ReferenceDate { get; set; }
        public Dictionary<string, string> Reference { get; set; }

        public Settings() { Theme = "auto"; Glass = 0.5; }

        static string Folder
        {
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HWID Checker"); }
        }

        public static Settings Load()
        {
            try
            {
                var s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(System.IO.Path.Combine(Folder, "settings.json")));
                if (s != null)
                {
                    s.Glass = Math.Max(0, Math.Min(1, s.Glass));
                    return s;
                }
            }
            catch { /* premier lancement ou fichier illisible */ }
            return new Settings();
        }

        public bool Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(System.IO.Path.Combine(Folder, "settings.json"), new JavaScriptSerializer().Serialize(this));
                return true;
            }
            catch { return false; }
        }
    }

    public partial class App
    {
        static readonly FontFamily Mono = new FontFamily("SF Mono, Cascadia Mono, Consolas");

        static readonly Dictionary<string, string> Icons = new Dictionary<string, string>
        {
            { "overview", "M12 11 V14 C12 16.5 11.5 18.7 10.6 20.6 M8.2 19.2 C8.9 17.5 9.2 15.7 9.2 14 V11 A2.8 2.8 0 0 1 14.8 11 V13.5 C14.8 16.1 14.4 18.5 13.6 20.7 M5.4 16.6 C5.8 15.6 6 14.5 6 13.4 V11 A6 6 0 0 1 18 11 V13 C18 14.9 17.8 16.7 17.4 18.5 M3.4 8.2 A9.2 9.2 0 0 1 17.2 3.4 M20.4 7.4 C20.9 8.5 21.2 9.7 21.2 11 V13.5" },
            { "desktop", "M4.5 4.5 H19.5 A1.5 1.5 0 0 1 21 6 V14.5 A1.5 1.5 0 0 1 19.5 16 H4.5 A1.5 1.5 0 0 1 3 14.5 V6 A1.5 1.5 0 0 1 4.5 4.5 Z M8.5 20 H15.5 M12 16 V20" },
            { "display", "M4.5 4.5 H19.5 A1.5 1.5 0 0 1 21 6 V14.5 A1.5 1.5 0 0 1 19.5 16 H4.5 A1.5 1.5 0 0 1 3 14.5 V6 A1.5 1.5 0 0 1 4.5 4.5 Z M8.5 20 H15.5 M12 16 V20" },
            { "laptop", "M6 5 H18 A1.5 1.5 0 0 1 19.5 6.5 V15.5 H4.5 V6.5 A1.5 1.5 0 0 1 6 5 Z M2.5 19 H21.5" },
            { "board", "M5.5 3.5 H18.5 A2 2 0 0 1 20.5 5.5 V18.5 A2 2 0 0 1 18.5 20.5 H5.5 A2 2 0 0 1 3.5 18.5 V5.5 A2 2 0 0 1 5.5 3.5 Z M7.5 7.5 H12.5 V12.5 H7.5 Z M16.3 7.5 V12.5 M7.5 16.3 H10.5 M13.5 16.3 H16.5" },
            { "cpu", "M8 5.5 H16 A2.5 2.5 0 0 1 18.5 8 V16 A2.5 2.5 0 0 1 16 18.5 H8 A2.5 2.5 0 0 1 5.5 16 V8 A2.5 2.5 0 0 1 8 5.5 Z M10 10 H14 V14 H10 Z M9.3 2.5 V5.5 M14.7 2.5 V5.5 M9.3 18.5 V21.5 M14.7 18.5 V21.5 M2.5 9.3 H5.5 M2.5 14.7 H5.5 M18.5 9.3 H21.5 M18.5 14.7 H21.5" },
            { "memory", "M3.5 7 H20.5 A1 1 0 0 1 21.5 8 V16.5 H2.5 V8 A1 1 0 0 1 3.5 7 Z M7 10 V13 M12 10 V13 M17 10 V13 M6 16.5 V19 M10 16.5 V19 M14 16.5 V19 M18 16.5 V19" },
            { "storage", "M6.5 5 H17.5 L21 13 V17.5 A2 2 0 0 1 19 19.5 H5 A2 2 0 0 1 3 17.5 V13 Z M3 13 H21 M16.8 16.3 H17.2 M7 16.3 H12" },
            { "gpu", "M3 6 H20 A1.5 1.5 0 0 1 21.5 7.5 V15 A1.5 1.5 0 0 1 20 16.5 H3 M3 4 V20 M14.5 8.2 A3 3 0 1 1 14.5 14.2 A3 3 0 1 1 14.5 8.2 Z M6.5 9.5 H8.5 M6.5 13 H8.5 M7 16.5 V19 M12 16.5 V19" },
            { "network", "M2.8 9.3 A13.2 13.2 0 0 1 21.2 9.3 M5.9 12.7 A8.8 8.8 0 0 1 18.1 12.7 M9 16 A4.4 4.4 0 0 1 15 16 M12 19.4 V19.5" },
            { "globe", "M12 3 A9 9 0 1 1 12 21 A9 9 0 1 1 12 3 Z M3 12 H21 M12 3 C17 8 17 16 12 21 M12 3 C7 8 7 16 12 21" },
            { "arp", "M9 3.5 H15 V8.5 H9 Z M3 15.5 H9 V20.5 H3 Z M15 15.5 H21 V20.5 H15 Z M12 8.5 V12 M6 15.5 V12 H18 V15.5" },
            { "usb", "M12 20.5 V4 M9.5 6.5 L12 3.5 L14.5 6.5 M12 17 L7 14 V10.5 M12 14 L17 11.5 V9 M5.7 8 H8.3 V10.5 H5.7 Z M17 6.2 A1.3 1.3 0 1 1 17 8.8 A1.3 1.3 0 1 1 17 6.2 Z" },
            { "security", "M12 3 L19.5 5.8 V11.5 C19.5 16 16.5 19.3 12 21 C7.5 19.3 4.5 16 4.5 11.5 V5.8 Z M8.8 12 L11.2 14.4 L15.4 9.8" },
            { "windows", "M4.5 4.5 H10.5 V10.5 H4.5 Z M13.5 4.5 H19.5 V10.5 H13.5 Z M4.5 13.5 H10.5 V19.5 H4.5 Z M13.5 13.5 H19.5 V19.5 H13.5 Z" },
            { "compare", "M4 8.5 H19 M15.5 5 L19 8.5 L15.5 12 M20 15.5 H5 M8.5 12 L5 15.5 L8.5 19" },
            { "appearance", "M4 7 H11 M17 7 H20 M14 4.3 A2.7 2.7 0 1 1 14 9.7 A2.7 2.7 0 1 1 14 4.3 Z M4 17 H7 M13 17 H20 M10 14.3 A2.7 2.7 0 1 1 10 19.7 A2.7 2.7 0 1 1 10 14.3 Z" },
            { "link", "M10 14 L20 4 M14 4 H20 V10 M17.5 14 V18.5 A1.5 1.5 0 0 1 16 20 H5.5 A1.5 1.5 0 0 1 4 18.5 V8 A1.5 1.5 0 0 1 5.5 6.5 H10" },
            { "game", "M8 8 H16 A5 5 0 0 1 21 13 V14.5 A3 3 0 0 1 15.6 16.3 L15 15.5 H9 L8.4 16.3 A3 3 0 0 1 3 14.5 V13 A5 5 0 0 1 8 8 Z M8.5 10.5 V13.5 M7 12 H10 M15.5 11 V11.1 M17.3 13 V13.1" },
            { "live", "M3 12.5 H7 L9.5 6 L14 18 L16.5 12.5 H21" },
            { "search", "M10.5 4 A6.5 6.5 0 1 1 10.5 17 A6.5 6.5 0 1 1 10.5 4 Z M15.5 15.5 L20.5 20.5" },
            { "eye", "M2.5 12 C5 7.5 8.2 5.5 12 5.5 C15.8 5.5 19 7.5 21.5 12 C19 16.5 15.8 18.5 12 18.5 C8.2 18.5 5 16.5 2.5 12 Z M12 9 A3 3 0 1 1 12 15 A3 3 0 1 1 12 9 Z" },
            { "eyeoff", "M2.5 12 C5 7.5 8.2 5.5 12 5.5 C15.8 5.5 19 7.5 21.5 12 C19 16.5 15.8 18.5 12 18.5 C8.2 18.5 5 16.5 2.5 12 Z M12 9 A3 3 0 1 1 12 15 A3 3 0 1 1 12 9 Z M4.5 20 L19.5 4" },
            { "refresh", "M20 12 A8 8 0 1 1 17.5 6.2 M17.8 2.8 V6.5 H14" },
            { "copy", "M10 8.5 H18 A2 2 0 0 1 20 10.5 V18.5 A2 2 0 0 1 18 20.5 H10 A2 2 0 0 1 8 18.5 V10.5 A2 2 0 0 1 10 8.5 Z M5 15.5 A1.5 1.5 0 0 1 4 14 V5.5 A2 2 0 0 1 6 3.5 H14 A1.5 1.5 0 0 1 15.5 4.6" },
            { "export", "M12 15 V3.5 M7.8 7.5 L12 3.3 L16.2 7.5 M7 11 H6 A1.5 1.5 0 0 0 4.5 12.5 V19 A1.5 1.5 0 0 0 6 20.5 H18 A1.5 1.5 0 0 0 19.5 19 V12.5 A1.5 1.5 0 0 0 18 11 H17" }
        };

        Window w;
        Settings cfg;
        Report report;
        string page = "overview";
        bool loading, dark, scanning, scanned;
        List<Driver> drivers;
        Driver bios;
        string scanText;
        TextBlock scanLabel;
        Grid outer, shell, host, toast;
        Border shadow, rim;
        StackPanel side;
        ScrollViewer scroller;
        TextBox search;
        Rectangle wall;
        ImageBrush wallBrush;
        readonly List<VisualBrush> lenses = new List<VisualBrush>();
        readonly List<Grid> lensHosts = new List<Grid>();
        readonly Dictionary<string, RadioButton> items = new Dictionary<string, RadioButton>();

        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show(e.Exception.Message, "HWID Checker", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            app.Run(new App().Build());
        }

        T Get<T>(string name) where T : class { return w.FindName(name) as T; }

        Window Build()
        {
            cfg = Settings.Load();
            if (string.IsNullOrEmpty(cfg.Lang))
                cfg.Lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";
            L.English = cfg.Lang == "en";
            var asm = Assembly.GetExecutingAssembly();
            using (var xaml = asm.GetManifestResourceStream("MainWindow.xaml"))
                w = (Window)XamlReader.Load(xaml);
            using (var ico = asm.GetManifestResourceStream("app.ico"))
                w.Icon = BitmapFrame.Create(ico, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            outer = Get<Grid>("Outer"); shell = Get<Grid>("Shell"); host = Get<Grid>("PageHost"); toast = Get<Grid>("Toast");
            shadow = Get<Border>("Shadow"); rim = Get<Border>("Rim"); side = Get<StackPanel>("Side");
            scroller = Get<ScrollViewer>("Scroller"); search = Get<TextBox>("SearchBox"); wall = Get<Rectangle>("Wall");

            ApplyTheme();
            SetupWindow();
            SetupLens("Lens1");
            SetupLens("Lens2");

            SetIcon("BtnMask", cfg.Mask ? "eyeoff" : "eye");
            SetIcon("BtnRefresh", "refresh"); SetIcon("BtnCopy", "copy"); SetIcon("BtnExport", "export");
            Get<Button>("BtnCredit").Click += (s, e) => Copy("1skf", "Pseudo Discord copié");
            Get<Button>("BtnLang").Click += (s, e) => SetLang(L.English ? "fr" : "en");
            Get<Button>("BtnMask").Click += (s, e) => ToggleMask();
            Get<Button>("BtnRefresh").Click += (s, e) => Refresh();
            Get<Button>("BtnCopy").Click += (s, e) => CopyAll();
            Get<Button>("BtnExport").Click += (s, e) => Export();
            search.TextChanged += (s, e) => Render(false);
            w.PreviewKeyDown += Keys;

            LocalizeChrome();
            BuildSidebar();
            Render(false);
            // Le contexte de synchronisation n'existe qu'une fois la boucle de messages lancee.
            w.Loaded += (s, e) => Refresh();
            return w;
        }

        void Keys(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && search.Text.Length > 0) { search.Clear(); e.Handled = true; return; }
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C) { CopyAll(); e.Handled = true; return; }
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key == Key.F) { search.Focus(); search.SelectAll(); }
            else if (e.Key == Key.R) Refresh();
            else if (e.Key == Key.E) Export();
            else if (e.Key == Key.H) ToggleMask();
            else if (e.Key == Key.W || e.Key == Key.Q) w.Close();
            else return;
            e.Handled = true;
        }

        void SetIcon(string button, string icon) { Get<Button>(button).Tag = Geometry.Parse(Icons[icon]); }

        void SetLang(string lang)
        {
            cfg.Lang = lang;
            cfg.Save();
            L.English = lang == "en";
            LocalizeChrome();
            BuildSidebar();
            Render(false);
        }

        void LocalizeChrome()
        {
            search.Tag = L.T("Rechercher");
            AutomationProperties.SetName(search, L.T("Rechercher"));
            Get<Button>("BtnLang").Content = L.English ? "EN" : "FR";
            string[,] tips =
            {
                { "BtnLang", "Langue" }, { "BtnMask", "Afficher ou masquer les identifiants (Ctrl+H)" }, { "BtnRefresh", "Actualiser (Ctrl+R)" },
                { "BtnCopy", "Tout copier (Ctrl+Maj+C)" }, { "BtnExport", "Exporter (Ctrl+E)" }
            };
            for (int i = 0; i < tips.GetLength(0); i++)
            {
                var b = Get<Button>(tips[i, 0]);
                string t = tips[i, 0] == "BtnLang" ? (L.English ? "Français" : "English") : L.T(tips[i, 1]);
                b.ToolTip = t;
                AutomationProperties.SetName(b, t);
            }
            var credit = Get<Button>("BtnCredit");
            credit.ToolTip = L.T("Copier le pseudo Discord");
            AutomationProperties.SetName(credit, "Made by 1skf (Discord)");
            AutomationProperties.SetName(Get<Button>("BtnClose"), L.T("Fermer"));
            AutomationProperties.SetName(Get<Button>("BtnMin"), L.T("Réduire"));
            AutomationProperties.SetName(Get<Button>("BtnMax"), L.T("Agrandir"));
        }

        // ================= donnees =================

        async void Refresh()
        {
            if (loading) return;
            loading = true;
            var btn = Get<Button>("BtnRefresh");
            btn.IsEnabled = Get<Button>("BtnCopy").IsEnabled = Get<Button>("BtnExport").IsEnabled = false;
            bool first = report == null;
            if (first) Render(false);
            try { report = await Task.Run(() => Hardware.Collect()); }
            finally
            {
                loading = false;
                btn.IsEnabled = Get<Button>("BtnCopy").IsEnabled = Get<Button>("BtnExport").IsEnabled = true;
            }
            if (first) history.Record(report.Flatten(), report.Date);
            BuildSidebar();
            Render(first);
            CheckBios();
            if (first && !cfg.SkipUpdateCheck) CheckUpdate(false);
            if (!first) ShowToast("Informations actualisées");
        }

        Field BiosField()
        {
            return report.Sections.Where(x => x.Key == "board").SelectMany(x => x.Groups).SelectMany(g => g.Fields).FirstOrDefault(f => f.Label == "Dernière mise à jour");
        }

        async void CheckBios()
        {
            var r = report;
            var d = bios ?? await Task.Run(() => { try { return Hardware.CheckBios(); } catch { return null; } });
            if (r != report) return; // actualise entre-temps
            var f = BiosField();
            if (f == null) return;
            if (d != null && d.State == "error") f.Value = "Vérification impossible";
            else if (d == null || d.Url == null) f.Value = "Non disponible : aucune source officielle confirmée";
            else
            {
                bios = d;
                f.Value = d.State == "update" ? "Version " + d.NewVersion + " disponible (" + d.NewDate.ToString("yyyy-MM-dd") + ")" : "À jour (" + d.NewVersion + ")";
                f.Url = d.Url;
            }
            if (page == "overview" || page == "board") Render(false);
        }

        async void ScanDrivers()
        {
            if (scanning) return;
            scanning = true;
            scanText = "Analyse : 0 sur 0";
            Render(false);
            try
            {
                drivers = await Task.Run(() => Hardware.CheckDrivers((i, n) => w.Dispatcher.BeginInvoke(new Action(() =>
                {
                    scanText = "Analyse : " + i + " sur " + n;
                    if (scanLabel != null) scanLabel.Text = L.T(scanText);
                }))));
                scanned = true;
            }
            finally { scanning = false; }
            if (page == "drivers") Render(true);
        }

        void ToggleMask()
        {
            cfg.Mask = !cfg.Mask;
            cfg.Save();
            SetIcon("BtnMask", cfg.Mask ? "eyeoff" : "eye");
            Render(false);
        }

        string Shown(string value, bool id)
        {
            return id && cfg.Mask ? new string(value.Select(c => char.IsLetterOrDigit(c) ? '•' : c).ToArray()) : value;
        }

        void Copy(string text, string message)
        {
            try { Clipboard.SetDataObject(text, true); ShowToast(message); }
            catch { ShowToast("Presse-papiers occupé, réessayez"); }
        }

        void Open(string url)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + url + "\""); } // l'explorateur ouvre le navigateur sans droits administrateur
            catch { Copy(url, "Aucun navigateur trouvé, lien copié"); }
        }

        string AsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("HWID Checker, " + report.Date.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine(report.PcName + ", " + report.Model);
            sb.AppendLine(L.T("Empreinte matérielle") + ": " + report.Fingerprint);
            foreach (var s in report.Sections)
            {
                sb.AppendLine().AppendLine("== " + L.T(s.Title) + " ==");
                foreach (var g in s.Groups)
                {
                    sb.AppendLine("[" + L.T(g.Title) + (string.IsNullOrEmpty(g.Sub) ? "" : " / " + L.T(g.Sub)) + "]");
                    foreach (var f in g.Fields) sb.AppendLine("  " + L.T(f.Label) + ": " + (f.IsId ? f.Value : L.T(f.Value)) + (f.Url == null ? "" : " <" + f.Url + ">"));
                }
            }
            return sb.ToString();
        }

        string AsJson()
        {
            return new JavaScriptSerializer().Serialize(new
            {
                date = report.Date.ToString("s"), ordinateur = report.PcName, modele = report.Model, empreinte = report.Fingerprint,
                sections = report.Sections.Select(s => new
                {
                    titre = L.T(s.Title),
                    groupes = s.Groups.Select(g => new { titre = L.T(g.Title), detail = L.T(g.Sub), donnees = g.Fields.Select(f => new { nom = L.T(f.Label), valeur = f.IsId ? f.Value : L.T(f.Value) }) })
                })
            });
        }

        void CopyAll()
        {
            if (report != null) Copy(AsText(), "Rapport complet copié");
        }

        void Export()
        {
            if (report == null) return;
            var d = new SaveFileDialog
            {
                FileName = "HWID " + report.Date.ToString("yyyy-MM-dd"), Filter = L.T("Texte") + " (*.txt)|*.txt|JSON (*.json)|*.json"
            };
            if (d.ShowDialog(w) != true) return;
            try
            {
                File.WriteAllText(d.FileName, d.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? AsJson() : AsText(), new UTF8Encoding(true));
                ShowToast("Rapport enregistré");
            }
            catch { ShowToast("Échec de l’enregistrement"); }
        }

        // ================= elements visuels =================

        static FrameworkElement Glyph(string key, double size, string brushKey, Brush brush)
        {
            var p = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Icons[key]), Width = 24, Height = 24, StrokeThickness = 1.9,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            if (brush != null) p.Stroke = brush; else p.SetResourceReference(Shape.StrokeProperty, brushKey);
            return new Viewbox { Width = size, Height = size, Child = p };
        }

        static Color Hex(string c) { return (Color)ColorConverter.ConvertFromString(c); }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(255, (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        /// <summary>Icone coloree facon macOS : degrade, reflet et glyphe blanc.</summary>
        static FrameworkElement Tile(string icon, string color, double size)
        {
            Color c = Hex(color);
            var g = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(size * .27),
                Background = new LinearGradientBrush(Mix(c, Colors.White, .22), Mix(c, Colors.Black, .06), 90)
            });
            g.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(size * .27), BorderThickness = new Thickness(size > 40 ? 1.2 : .8),
                BorderBrush = new LinearGradientBrush(Color.FromArgb(150, 255, 255, 255), Color.FromArgb(25, 255, 255, 255), 80)
            });
            var gl = Glyph(icon, size * .66, null, Brushes.White);
            gl.HorizontalAlignment = HorizontalAlignment.Center;
            gl.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(gl);
            return g;
        }

        static TextBlock Text(string t, double size, string brush, bool bold)
        {
            var b = new TextBlock { Text = L.T(t), FontSize = size, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            if (bold) b.FontWeight = FontWeights.SemiBold;
            if (brush != null) b.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return b;
        }

        static Border Card(UIElement child)
        {
            var b = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Child = child };
            b.SetResourceReference(Border.BackgroundProperty, "Card");
            b.SetResourceReference(Border.BorderBrushProperty, "CardBorder");
            return b;
        }

        static Border Separator(double left)
        {
            var b = new Border { Height = 1, Margin = new Thickness(left, 0, 14, 0) };
            b.SetResourceReference(Border.BackgroundProperty, "Sep");
            return b;
        }

        Button Capsule(string text, bool accent, Action click)
        {
            var b = new Button { Content = L.T(text), Style = (Style)w.Resources[accent ? "CapsuleAccent" : "Capsule"] };
            b.Click += (s, e) => click();
            return b;
        }

        /// <summary>Point d'information : survol ou clic pour lire l'explication.</summary>
        FrameworkElement InfoDot(string title, string help)
        {
            var i = new TextBlock { Text = "i", FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -1, 0, 0) };
            i.SetResourceReference(TextBlock.ForegroundProperty, "Text2");
            var dot = new Border { Width = 15, Height = 15, CornerRadius = new CornerRadius(7.5), Margin = new Thickness(6, 1, 0, 0), Child = i, Cursor = Cursors.Help, VerticalAlignment = VerticalAlignment.Center };
            dot.SetResourceReference(Border.BackgroundProperty, "Dot");

            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = L.T(title), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) });
            body.Children.Add(new TextBlock { Text = L.T(help), TextWrapping = TextWrapping.Wrap, Opacity = .82, LineHeight = 17 });
            var tip = new ToolTip { Content = body, Placement = System.Windows.Controls.Primitives.PlacementMode.Right, HorizontalOffset = 6, PlacementTarget = dot };
            foreach (var k in new[] { "Tip", "CardBorder", "Text" }) tip.SetResourceReference(TagKeys(k), k);
            tip.Resources = w.Resources;
            dot.ToolTip = tip;
            ToolTipService.SetInitialShowDelay(dot, 60);
            ToolTipService.SetShowDuration(dot, 60000);
            AutomationProperties.SetHelpText(dot, L.T(help));

            dot.MouseEnter += (s, e) =>
            {
                dot.SetResourceReference(Border.BackgroundProperty, "Accent");
                i.Foreground = Brushes.White;
            };
            dot.MouseLeave += (s, e) =>
            {
                dot.SetResourceReference(Border.BackgroundProperty, "Dot");
                i.SetResourceReference(TextBlock.ForegroundProperty, "Text2");
            };
            // Le clic ne doit pas copier la ligne.
            dot.PreviewMouseLeftButtonDown += (s, e) => { tip.IsOpen = true; e.Handled = true; };
            return dot;
        }

        static DependencyProperty TagKeys(string k)
        {
            return k == "Tip" ? Control.BackgroundProperty : k == "Text" ? Control.ForegroundProperty : Control.BorderBrushProperty;
        }

        static Brush StateBrush(string state)
        {
            return new SolidColorBrush(Hex(state == "on" ? "#30D158" : state == "warn" ? "#FF9F0A" : "#FF453A"));
        }

        FrameworkElement Pill(string text, Color c)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(38, c.R, c.G, c.B)),
                Padding = new Thickness(8, 1, 8, 2), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = L.T(text), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(dark ? Mix(c, Colors.White, .25) : Mix(c, Colors.Black, .3)) }
            };
        }

        Button Row(Field f, bool maskLabel)
        {
            string label = maskLabel && cfg.Mask ? System.Text.RegularExpressions.Regex.Replace(f.Label, @"\d", "•") : f.Label;
            var g = new Grid { Margin = new Thickness(0, 9, 0, 9) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 22, 0) };
            left.Children.Add(Text(label, 13, null, false));
            if (f.Help != null) left.Children.Add(InfoDot(f.Label, f.Help));
            if (f.Generic) left.Children.Add(Pill("Générique", Hex("#8E8E93")));
            if (f.Alert != null) left.Children.Add(Pill(f.Alert, Hex("#FF9500")));
            g.Children.Add(left);

            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            FrameworkElement value;
            if (f.State != null)
            {
                right.Children.Add(Text(f.Value, 13, "Text2", false));
                right.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = StateBrush(f.State), Margin = new Thickness(9, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                value = right;
            }
            else if (f.Url != null)
            {
                var link = Text(f.Value, 13, "Accent", false);
                link.TextDecorations = TextDecorations.Underline;
                var arrow = Glyph("link", 13, "Accent", null);
                arrow.Margin = new Thickness(7, 1, 0, 0);
                right.Children.Add(link);
                right.Children.Add(arrow);
                value = right;
            }
            else
            {
                var t = Text(Shown(f.Value, f.IsId), f.IsId ? 12.5 : 13, f.IsId && !f.Generic ? "Text" : f.Value == "Non communiqué" ? "Text3" : "Text2", false);
                if (f.IsId) t.FontFamily = Mono;
                t.TextAlignment = TextAlignment.Right;
                value = t;
            }
            Grid.SetColumn(value, 1);
            g.Children.Add(value);

            var copy = Glyph("copy", 14, "Text2", null);
            copy.Margin = new Thickness(8, 0, 0, 0);
            copy.Opacity = 0;
            left.Children.Add(copy);

            var b = new Button { Content = g, Style = (Style)w.Resources["Row"] };
            AutomationProperties.SetName(b, L.T(label) + " : " + Shown(f.IsId ? f.Value : L.T(f.Value), f.IsId));
            if (f.Help != null) AutomationProperties.SetHelpText(b, L.T(f.Help));
            if (f.Url != null)
            {
                copy.Visibility = Visibility.Collapsed;
                b.Cursor = Cursors.Hand;
                b.ToolTip = f.Url;
                b.Click += (s, e) => Open(f.Url);
                return b;
            }
            b.MouseEnter += (s, e) => copy.Opacity = 1;
            b.MouseLeave += (s, e) => copy.Opacity = 0;
            b.Click += (s, e) => Copy(f.IsId ? f.Value : L.T(f.Value), "Copié");
            return b;
        }

        void AddGroup(Panel p, string title, string sub, IEnumerable<UIElement> rows)
        {
            if (title != null)
            {
                var h = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 22, 14, 8) };
                h.Children.Add(Text(title, 14, null, true));
                if (!string.IsNullOrEmpty(sub))
                {
                    var t = Text(sub, 12.5, "Text2", false);
                    t.Margin = new Thickness(9, 1, 0, 0);
                    h.Children.Add(t);
                }
                p.Children.Add(h);
            }
            var list = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            foreach (var r in rows)
            {
                if (list.Children.Count > 0) list.Children.Add(Separator(14));
                list.Children.Add(r);
            }
            p.Children.Add(Card(list));
        }

        static StackPanel PageRoot()
        {
            return new StackPanel { Margin = new Thickness(26, 68, 26, 28), MaxWidth = 860 };
        }

        // ================= barre laterale =================

        void BuildSidebar()
        {
            side.Children.Clear();
            items.Clear();
            AddItem("overview", "Vue d’ensemble", "overview", "#5E5CE6");
            string cat = null;
            if (report != null)
                foreach (var s in report.Sections)
                {
                    if (s.Category != cat) AddHeader(cat = s.Category);
                    AddItem(s.Key, s.Title, s.Icon, s.Color);
                }
            if (report != null)
            {
                AddItem("anticheat", "Anti-triche", "game", "#BF5AF2");
                AddItem("live", "En direct", "live", "#FF9500");
            }
            AddHeader("Outils");
            AddItem("drivers", "Pilotes", "refresh", "#FF375F");
            AddItem("compare", "Historique", "compare", "#FF9F0A");
            if (report != null) AddItem("share", "Partager", "export", "#30B0C7");
            AddItem("resources", "Ressources", "link", "#0A84FF");
            AddItem("appearance", "Apparence", "appearance", "#636366");
        }

        void AddHeader(string t)
        {
            var b = Text(t, 11.5, "Text2", true);
            b.Margin = new Thickness(8, 11, 0, 4);
            side.Children.Add(b);
        }

        void AddItem(string key, string title, string icon, string color)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal };
            p.Children.Add(Tile(icon, color, 22));
            p.Children.Add(new TextBlock { Text = L.T(title), Margin = new Thickness(9, 0, 0, 1), VerticalAlignment = VerticalAlignment.Center });
            var r = new RadioButton { Content = p, Style = (Style)w.Resources["SideItem"] };
            AutomationProperties.SetName(r, L.T(title));
            AutomationProperties.SetAutomationId(r, "nav-" + key);
            r.IsChecked = key == page && search.Text.Length == 0;
            r.Checked += (s, e) => Go(key);
            items[key] = r;
            side.Children.Add(r);
        }

        void Go(string key)
        {
            page = key;
            if (search.Text.Length > 0) search.Clear(); // declenche Render
            scroller.ScrollToTop();
            Render(true);
        }

        // ================= pages =================

        void Render(bool animate)
        {
            string q = search.Text.Trim();
            Panel p;
            string title;
            if (q.Length > 0)
            {
                foreach (var i in items.Values) i.IsChecked = false;
                p = SearchPage(q);
                title = "Résultats";
            }
            else
            {
                RadioButton item;
                if (items.TryGetValue(page, out item) && item.IsChecked != true) item.IsChecked = true;
                if (page == "appearance") { p = AppearancePage(); title = "Apparence"; }
                else if (page == "drivers") { p = DriversPage(); title = "Pilotes"; }
                else if (page == "resources") { p = ResourcesPage(); title = "Ressources"; }
                else if (report == null) { p = LoadingPage(); title = "Vue d’ensemble"; }
                else if (page == "compare") { p = ComparePage(); title = "Historique"; }
                else if (page == "anticheat") { p = AntiCheatPage(); title = "Anti-triche"; }
                else if (page == "live") { p = LivePage(); title = "En direct"; }
                else if (page == "share") { p = SharePage(); title = "Partager"; }
                else
                {
                    var s = report.Sections.FirstOrDefault(x => x.Key == page);
                    p = s == null ? OverviewPage() : SectionPage(s);
                    title = s == null ? "Vue d’ensemble" : s.Title;
                }
            }
            Get<TextBlock>("PageTitle").Text = L.T(title);
            host.Children.Clear();
            host.Children.Add(p);
            if (animate && SystemParameters.ClientAreaAnimation)
            {
                var t = new TranslateTransform();
                p.RenderTransform = t;
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                p.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
                t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            }
        }

        Panel LoadingPage()
        {
            var p = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 250, 0, 0) };
            var spin = new Grid { Width = 30, Height = 30, HorizontalAlignment = HorizontalAlignment.Center };
            for (int i = 0; i < 8; i++)
            {
                var r = new Rectangle { Width = 3, Height = 8, RadiusX = 1.5, RadiusY = 1.5, Opacity = .15 + i * .11, VerticalAlignment = VerticalAlignment.Top, RenderTransformOrigin = new Point(.5, 15.0 / 8) };
                r.SetResourceReference(Shape.FillProperty, "Text2");
                r.RenderTransform = new RotateTransform(i * 45);
                spin.Children.Add(r);
            }
            var rot = new RotateTransform();
            spin.RenderTransformOrigin = new Point(.5, .5);
            spin.RenderTransform = rot;
            var a = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromMilliseconds(800) };
            for (int i = 0; i < 8; i++) a.KeyFrames.Add(new DiscreteDoubleKeyFrame(i * 45, TimeSpan.FromMilliseconds(i * 100)));
            rot.BeginAnimation(RotateTransform.AngleProperty, a);
            p.Children.Add(spin);
            var t = Text("Lecture du matériel…", 13, "Text2", false);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 14, 0, 0);
            p.Children.Add(t);
            return p;
        }

        Panel OverviewPage()
        {
            var p = PageRoot();

            if (newVersion != null) p.Children.Add(UpdateBanner());

            // En-tete : machine + empreinte
            var hero = new StackPanel { Margin = new Thickness(20) };
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(Tile(report.IsLaptop ? "laptop" : "desktop", "#5E5CE6", 60));
            var names = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(Text(Shown(report.PcName, true), 20, null, true));
            names.Children.Add(Text(report.Model, 13, "Text2", false));
            top.Children.Add(names);
            hero.Children.Add(top);

            var line = Separator(0);
            line.Margin = new Thickness(0, 18, 0, 16);
            hero.Children.Add(line);

            var fp = new Grid();
            fp.ColumnDefinitions.Add(new ColumnDefinition());
            fp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var fl = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            fl.Children.Add(Text("Empreinte matérielle", 12.5, "Text2", false));
            fl.Children.Add(InfoDot("Empreinte matérielle", Help.For("", "Empreinte")));
            ft.Children.Add(fl);
            var groups = Enumerable.Range(0, 8).Select(i => report.Fingerprint.Substring(i * 8, 8)).ToArray();
            for (int i = 0; i < 2; i++)
            {
                var t = Text(Shown(string.Join("  ", groups.Skip(i * 4).Take(4)), true), 15.5, null, false);
                t.FontFamily = Mono;
                t.TextWrapping = TextWrapping.NoWrap;
                t.Margin = new Thickness(0, 1, 0, 1);
                ft.Children.Add(t);
            }
            fp.Children.Add(ft);
            var copy = Capsule("Copier", true, () => Copy(report.Fingerprint, "Empreinte copiée"));
            copy.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(copy, 1);
            fp.Children.Add(copy);
            hero.Children.Add(fp);
            p.Children.Add(Card(hero));

            AddGroup(p, "Identifiants principaux", null, report.Summary.Select(s => SummaryRow(s)));

            string[] sys = { "Version de Windows", "Build", "Type de licence", "Activation", "Version du BIOS", "Date du firmware actuel", "Dernière mise à jour" };
            var all = report.Sections.Where(s => s.Key == "windows" || s.Key == "board").SelectMany(s => s.Groups).SelectMany(g => g.Fields).ToList();
            AddGroup(p, "Windows et BIOS", null, sys.Select(k => all.FirstOrDefault(f => f.Label == k)).Where(f => f != null).Select(f => (UIElement)Row(f, false)));

            var disks = report.Sections.Where(s => s.Key == "storage").SelectMany(s => s.Groups)
                .Select(g => new { g, f = g.Fields.FirstOrDefault(x => x.Label == "Santé") }).Where(x => x.f != null).ToList();
            if (disks.Count > 0)
                AddGroup(p, "Santé des disques", null, disks.Select(x => (UIElement)Row(new Field
                {
                    Label = x.g.Title + " · " + x.g.Sub, Value = x.f.Value, State = x.f.State, Help = x.f.Help
                }, false)));

            var sec = report.Sections.FirstOrDefault(s => s.Key == "security");
            if (sec != null)
            {
                string[] keep = { "TPM 2.0", "Secure Boot", "HVCI", "Intégrité de la mémoire", "IOMMU / VT-d", "Hyperviseur", "Windows Defender" };
                var states = sec.Groups.SelectMany(g => g.Fields).Where(f => f.State != null).ToList();
                AddGroup(p, "Sécurité", null, keep.Select(k => states.FirstOrDefault(f => f.Label == k)).Where(f => f != null).Select(f => (UIElement)Row(f, false)));
            }

            AddGroup(p, "Composition de l’empreinte", null, report.Components.Select(c => (UIElement)Row(new Field
            {
                Label = c.Name, Value = c.Value, IsId = c.Used, Generic = !c.Used && c.Value != "Non communiqué",
                Help = c.Used ? "Cette valeur entre dans le calcul de l’empreinte." : "Valeur absente ou générique : elle est ignorée dans le calcul."
            }, false)));
            return p;
        }

        UIElement SummaryRow(Summary s)
        {
            var sec = report.Sections.First(x => x.Key == s.Section);
            var g = new Grid { Margin = new Thickness(0, 9, 0, 9) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(Tile(sec.Icon, sec.Color, 30));

            var mid = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Text(s.Label, 13, null, true));
            if (s.Help != null) head.Children.Add(InfoDot(s.Label, s.Help));
            mid.Children.Add(head);
            var sub = Text(s.Sub, 11.5, "Text2", false);
            sub.TextWrapping = TextWrapping.NoWrap;
            sub.TextTrimming = TextTrimming.CharacterEllipsis;
            mid.Children.Add(sub);
            Grid.SetColumn(mid, 1);
            g.Children.Add(mid);

            var v = Text(Shown(s.Value, s.IsId), 12.5, s.IsId ? "Text" : "Text3", false);
            if (s.IsId) v.FontFamily = Mono;
            v.TextAlignment = TextAlignment.Right;
            v.TextWrapping = TextWrapping.NoWrap;
            v.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(v, 2);
            g.Children.Add(v);

            var b = new Button { Content = g, Style = (Style)w.Resources["Row"] };
            AutomationProperties.SetName(b, L.T(s.Label) + " : " + Shown(s.Value, s.IsId));
            b.Click += (o, e) => Copy(s.Value, "Copié");
            return b;
        }

        Panel SectionPage(Section s)
        {
            var p = PageRoot();
            var h = new StackPanel { Margin = new Thickness(20, 20, 20, 18) };
            var tile = Tile(s.Icon, s.Color, 56);
            tile.HorizontalAlignment = HorizontalAlignment.Center;
            h.Children.Add(tile);
            var t = Text(s.Title, 19, null, true);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 10, 0, 3);
            h.Children.Add(t);
            var d = Text(s.Description, 13, "Text2", false);
            d.TextAlignment = TextAlignment.Center;
            h.Children.Add(d);
            p.Children.Add(Card(h));

            foreach (var g in s.Groups)
                AddGroup(p, g.Title, g.Sub, g.Fields.Select(f => (UIElement)Row(f, s.Key == "arp")));
            if (s.Groups.Count == 0) p.Children.Add(Note(s.Empty));
            if (s.Key == "arp") p.Children.Insert(1, OuiCard());
            return p;
        }

        static TextBlock Note(string text)
        {
            var t = Text(text, 13, "Text2", false);
            t.TextAlignment = TextAlignment.Center;
            t.Margin = new Thickness(0, 40, 0, 0);
            return t;
        }

        Panel SearchPage(string q)
        {
            var p = PageRoot();
            if (report == null) return p;
            int n = 0;
            foreach (var s in report.Sections)
                foreach (var g in s.Groups)
                {
                    // Valeurs masquees exclues : la recherche ne doit pas les reveler.
                    bool whole = Has(L.T(g.Title), q) || Has(g.Sub, q) || Has(L.T(s.Title), q);
                    var hits = g.Fields.Where(f => whole || Has(L.T(f.Label), q) || (!(cfg.Mask && f.IsId) && Has(f.IsId ? f.Value : L.T(f.Value), q))).ToList();
                    if (hits.Count == 0) continue;
                    n += hits.Count;
                    AddGroup(p, s.Title, g.Title, hits.Select(f => (UIElement)Row(f, s.Key == "arp")));
                }
            if (n == 0) p.Children.Add(Note("Aucun résultat pour « " + q + " »."));
            return p;
        }

        static bool Has(string text, string q)
        {
            return text != null && System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(text, q,
                System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        Panel ComparePage()
        {
            var p = PageRoot();
            var h = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };
            var tile = Tile("compare", "#FF9F0A", 56);
            tile.HorizontalAlignment = HorizontalAlignment.Center;
            h.Children.Add(tile);

            string title, desc;
            var diffs = new List<string[]>(); // chemin, avant, maintenant
            if (cfg.Reference == null)
            {
                title = "Aucune référence";
                desc = "Enregistrez l’état actuel pour repérer plus tard tout identifiant modifié.";
            }
            else
            {
                var now = report.Flatten();
                foreach (var k in cfg.Reference.Keys.Union(now.Keys))
                {
                    string a, b;
                    cfg.Reference.TryGetValue(k, out a);
                    now.TryGetValue(k, out b);
                    if (a != b) diffs.Add(new[] { k, a, b });
                }
                title = diffs.Count == 0 ? "Aucun changement" : diffs.Count == 1 ? "1 changement" : diffs.Count + " changements";
                desc = "Référence enregistrée le " + cfg.ReferenceDate + ".";
            }
            var t = Text(title, 19, null, true);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 10, 0, 3);
            h.Children.Add(t);
            var d = Text(desc, 13, "Text2", false);
            d.TextAlignment = TextAlignment.Center;
            h.Children.Add(d);
            var save = Capsule(cfg.Reference == null ? "Enregistrer la référence" : "Remplacer la référence", true, () =>
            {
                cfg.Reference = report.Flatten();
                cfg.ReferenceDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                ShowToast(cfg.Save() ? "Référence enregistrée" : "Impossible d’enregistrer la référence");
                Render(false);
            });
            save.HorizontalAlignment = HorizontalAlignment.Center;
            save.Margin = new Thickness(0, 16, 0, 0);
            h.Children.Add(save);
            p.Children.Add(Card(h));

            foreach (var x in diffs)
                AddGroup(p, x[0], null, new UIElement[]
                {
                    Row(new Field { Label = "Avant", Value = x[1] ?? "Absent", IsId = x[1] != null }, false),
                    Row(new Field { Label = "Maintenant", Value = x[2] ?? "Absent", IsId = x[2] != null }, false)
                });
            HistoryList(p);
            return p;
        }

        Panel DriversPage()
        {
            var p = PageRoot();
            var h = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            var tile = Tile("refresh", "#FF375F", 56);
            tile.HorizontalAlignment = HorizontalAlignment.Center;
            h.Children.Add(tile);
            var list = drivers ?? new List<Driver>();
            string title = scanning ? scanText : !scanned ? "Pilotes"
                : list.Any(d => d.State == "error") && !list.Any(d => d.State == "update") ? "Vérification impossible"
                : list.Any(d => d.State == "update") ? list.Count(d => d.State == "update") + " à mettre à jour" : "Aucune mise à jour";
            var t = Text(title, 19, null, true);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 10, 0, 3);
            if (scanning) scanLabel = t;
            h.Children.Add(t);
            var desc = Text("Compare les pilotes installés à Windows Update, au catalogue Microsoft et à NVIDIA. Seuls les modèles de composants sont envoyés, jamais les numéros de série.", 13, "Text2", false);
            desc.TextAlignment = TextAlignment.Center;
            desc.MaxWidth = 560;
            h.Children.Add(desc);
            var go = Capsule(scanned ? "Relancer l’analyse" : "Lancer l’analyse", true, ScanDrivers);
            go.IsEnabled = !scanning;
            go.HorizontalAlignment = HorizontalAlignment.Center;
            go.Margin = new Thickness(0, 16, 0, 0);
            AutomationProperties.SetAutomationId(go, "scan");
            h.Children.Add(go);
            p.Children.Add(Card(h));

            if (scanning) return p;
            if (drivers == null && !scanned) LoadDrivers();
            DriverGroup(p, "À mettre à jour", list.Where(d => d.State == "update"));
            DriverGroup(p, "À jour", list.Where(d => d.State == "ok"));
            DriverGroup(p, "Sans source vérifiée", list.Where(d => d.State == "none"));
            DriverGroup(p, scanned ? "Vérification impossible" : "Non vérifiés", list.Where(d => d.State == "unknown" || d.State == "error"));
            DriverGroup(p, "Fournis par Windows", list.Where(d => d.State == "windows"));
            return p;
        }

        async void LoadDrivers()
        {
            var l = await Task.Run(() => Hardware.InstalledDrivers());
            if (drivers != null) return;
            drivers = l;
            if (page == "drivers" && !scanning && search.Text.Length == 0) Render(false);
        }

        void DriverGroup(Panel p, string title, IEnumerable<Driver> set)
        {
            var l = set.ToList();
            if (l.Count > 0) AddGroup(p, title, l.Count.ToString(), l.Select(d => DriverRow(d)));
        }

        UIElement DriverRow(Driver d)
        {
            var g = new Grid { Margin = new Thickness(0, 9, 0, 9) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string help = d.State == "update" ? (d.Url != null ? "Une version plus récente existe pour cet identifiant matériel exact. La ligne ouvre sa page officielle."
                                                              : "Une version plus récente existe, mais sa page officielle n’a pas pu être confirmée : aucun lien n’est donné.")
                : d.State == "ok" ? "La source officielle ne propose rien de plus récent pour cet identifiant matériel. La ligne ouvre la dernière version publiée."
                : d.State == "none" ? "Aucune page officielle n’a pu être confirmée pour cet appareil : aucun lien n’est donné. Voyez le logiciel ou le site de son fabricant."
                : d.State == "windows" ? "Pilote Microsoft, mis à jour avec Windows lui-même. Sa date est souvent fixée à 2006-06-21 par Microsoft."
                : d.State == "error" ? "La source n’a pas répondu. Vérifiez la connexion, puis relancez l’analyse."
                : "Cliquez sur Lancer l’analyse pour chercher une version plus récente.";
            var left = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            var name = new DockPanel { HorizontalAlignment = HorizontalAlignment.Left };
            var dot = InfoDot(d.Name, help);
            DockPanel.SetDock(dot, Dock.Right);
            name.Children.Add(dot);
            var n = new TextBlock { Text = d.Name, TextTrimming = TextTrimming.CharacterEllipsis };
            name.Children.Add(n);
            left.Children.Add(name);
            string date = d.Date == DateTime.MinValue ? "" : " (" + d.Date.ToString("yyyy-MM-dd") + ")";
            string sub = d.Provider + (d.Version.Length == 0 ? "" : " · " + L.T("Installée") + (L.English ? ": " : " : ") + d.Version + date);
            if (d.NewVersion != null)
                sub += " · " + L.T("Publiée") + (L.English ? ": " : " : ") + d.NewVersion + (d.NewDate == DateTime.MinValue ? "" : " (" + d.NewDate.ToString("yyyy-MM-dd") + ")");
            var st = new TextBlock { Text = sub, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 0) };
            st.SetResourceReference(TextBlock.ForegroundProperty, "Text2");
            left.Children.Add(st);
            g.Children.Add(left);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(right, 1);
            if (d.Url != null)
            {
                var link = Text(d.Source, 13, "Accent", false);
                link.TextDecorations = TextDecorations.Underline;
                var arrow = Glyph("link", 13, "Accent", null);
                arrow.Margin = new Thickness(6, 1, 0, 0);
                right.Children.Add(link);
                right.Children.Add(arrow);
            }
            else if (d.State == "none" || d.State == "update")
                right.Children.Add(Text(d.State == "none" ? "Mise à jour non disponible" : "Lien non disponible", 12.5, "Text3", false));
            else if (d.State == "windows") right.Children.Add(Text("Via Windows Update", 12.5, "Text3", false));
            right.Children.Add(new Ellipse
            {
                Width = 10, Height = 10, Margin = new Thickness(10, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Fill = d.State == "ok" ? StateBrush("on") : d.State == "update" ? StateBrush("warn") : new SolidColorBrush(Hex("#8E8E93"))
            });
            g.Children.Add(right);

            var b = new Button { Content = g, Style = (Style)w.Resources["Row"] };
            AutomationProperties.SetName(b, d.Name);
            AutomationProperties.SetHelpText(b, L.T(help));
            if (d.Url != null)
            {
                b.Cursor = Cursors.Hand;
                b.ToolTip = d.Url;
                b.Click += (s, e) => Open(d.Url);
            }
            else b.Click += (s, e) => Copy(d.Name + " " + d.Version, "Copié");
            return b;
        }

        Panel ResourcesPage()
        {
            var p = PageRoot();
            AddGroup(p, "Sordum", "Réglages de Windows", new UIElement[]
            {
                Row(new Field { Label = "Windows Defender Blocker", Value = "Defender Control", Url = "https://www.sordum.org/9480/defender-control-v2-1/",
                    Help = "Defender Control de Sordum : active ou désactive Windows Defender. Sans Defender ni autre antivirus, ce PC n’est plus protégé, et l’outil est parfois signalé par les antivirus." }, false),
                Row(new Field { Label = "Windows Update Blocker", Value = "Windows Update Blocker", Url = "https://www.sordum.org/9470/windows-update-blocker-v1-8/",
                    Help = "Active ou désactive les mises à jour automatiques de Windows. Bloquées, les corrections de sécurité ne sont plus installées." }, false)
            });
            AddGroup(p, "Utilitaires", null, new UIElement[]
            {
                Row(new Field { Label = "WinRAR", Value = "win-rar.com", Url = "https://www.win-rar.com/download.html",
                    Help = "Création et extraction d’archives RAR et ZIP. Page de téléchargement officielle." }, false),
                Row(new Field { Label = "Chris Titus Tech", Value = "christitus.com", Url = "https://christitus.com/windows-tool/",
                    Help = "Présentation officielle de WinUtil : installation de programmes, réglages et réparation de Windows." }, false),
                Row(new Field { Label = "WinUtil (code source)", Value = "GitHub", Url = "https://github.com/ChrisTitusTech/winutil",
                    Help = "Dépôt GitHub officiel de l’outil de Chris Titus." }, false)
            });
            return p;
        }

        UIElement SettingRow(string label, string help, FrameworkElement control)
        {
            var g = new Grid { Margin = new Thickness(14, 0, 14, 0), MinHeight = 48 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var l = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            l.Children.Add(Text(label, 13, null, false));
            l.Children.Add(InfoDot(label, help));
            g.Children.Add(l);
            control.HorizontalAlignment = HorizontalAlignment.Right;
            control.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(control, L.T(label));
            Grid.SetColumn(control, 1);
            g.Children.Add(control);
            return g;
        }

        Panel AppearancePage()
        {
            var p = PageRoot();

            var seg = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
            foreach (var o in new[] { new[] { "auto", "Automatique" }, new[] { "light", "Clair" }, new[] { "dark", "Sombre" } })
            {
                string key = o[0];
                var r = new RadioButton { Content = L.T(o[1]), GroupName = "theme", Style = (Style)w.Resources["Segment"], IsChecked = cfg.Theme == key };
                r.Checked += (s, e) => { cfg.Theme = key; cfg.Save(); ApplyTheme(); };
                seg.Children.Add(r);
            }
            var segBox = new Border { CornerRadius = new CornerRadius(15), Child = seg };
            segBox.SetResourceReference(Border.BackgroundProperty, "Control");

            var slide = new StackPanel { Orientation = Orientation.Horizontal };
            var a = Text("Transparent", 12, "Text2", false);
            var b = Text("Teinté", 12, "Text2", false);
            var sl = new Slider { Minimum = 0, Maximum = 1, Value = cfg.Glass, Width = 190, Margin = new Thickness(10, 0, 10, 0), SmallChange = .05, LargeChange = .1 };
            AutomationProperties.SetName(sl, "Verre");
            sl.ValueChanged += (s, e) => { cfg.Glass = sl.Value; ApplyTheme(); };
            sl.LostMouseCapture += (s, e) => cfg.Save();
            sl.LostKeyboardFocus += (s, e) => cfg.Save();
            slide.Children.Add(a); slide.Children.Add(sl); slide.Children.Add(b);

            var sw = new CheckBox { Style = (Style)w.Resources["Switch"], IsChecked = cfg.Mask };
            sw.Click += (s, e) =>
            {
                cfg.Mask = sw.IsChecked == true;
                cfg.Save();
                SetIcon("BtnMask", cfg.Mask ? "eyeoff" : "eye");
            };

            var lang = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
            foreach (var o in new[] { new[] { "fr", "Français" }, new[] { "en", "English" } })
            {
                string key = o[0];
                var r = new RadioButton { Content = o[1], GroupName = "lang", Style = (Style)w.Resources["Segment"], IsChecked = cfg.Lang == key };
                r.Checked += (s, e) => SetLang(key);
                lang.Children.Add(r);
            }
            var langBox = new Border { CornerRadius = new CornerRadius(15), Child = lang };
            langBox.SetResourceReference(Border.BackgroundProperty, "Control");

            AddGroup(p, "Affichage", null, new[]
            {
                SettingRow("Langue", "Langue de l’interface et des explications.", langBox),
                SettingRow("Thème", "Automatique suit le réglage clair ou sombre de Windows.", segBox),
                SettingRow("Verre", "Règle la transparence de la fenêtre, qui laisse apparaître les couleurs du fond d’écran.", slide)
            });
            var up = new StackPanel { Orientation = Orientation.Horizontal };
            var st = Text(VersionText + " · " + L.T(updateState), 12.5, "Text2", false);
            st.Margin = new Thickness(0, 0, 12, 0);
            up.Children.Add(st);
            up.Children.Add(Capsule("Vérifier", false, () => { CheckUpdate(true); Render(false); }));
            var auto = new CheckBox { Style = (Style)w.Resources["Switch"], IsChecked = !cfg.SkipUpdateCheck };
            auto.Click += (s, e) => { cfg.SkipUpdateCheck = auto.IsChecked != true; cfg.Save(); };
            AddGroup(p, "Mises à jour", null, new[]
            {
                SettingRow("Version de HWID Checker", "Compare cette version à celles publiées sur GitHub. Rien n’est téléchargé ni installé sans vous.", up),
                SettingRow("Vérifier au démarrage", "Interroge GitHub une fois à chaque lancement.", auto)
            });
            AddGroup(p, "Confidentialité", null, new[]
            {
                SettingRow("Masquer les identifiants", "Remplace les identifiants par des points à l’écran, pour une capture ou un partage. La copie et l’export gardent les vraies valeurs.", sw)
            });
            return p;
        }

        // ================= notification =================

        void ShowToast(string text)
        {
            Get<TextBlock>("ToastText").Text = L.T(text);
            var a = new DoubleAnimationUsingKeyFrames();
            a.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(150)));
            a.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(1700)));
            a.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(2100)));
            toast.BeginAnimation(UIElement.OpacityProperty, a);
        }

        // ================= theme =================

        static bool SystemDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return k != null && Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) == 0;
            }
            catch { return false; }
        }

        void Set(string key, string color) { Set(key, Hex(color)); }

        void Set(string key, Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            w.Resources[key] = b;
        }

        static Color Alpha(string c, double a)
        {
            Color x = Hex(c);
            return Color.FromArgb((byte)(255 * a), x.R, x.G, x.B);
        }

        void ApplyTheme()
        {
            bool was = dark;
            dark = cfg.Theme == "dark" || (cfg.Theme == "auto" && SystemDark());
            double g = cfg.Glass;
            if (dark)
            {
                Set("Text", "#F2FFFFFF"); Set("Text2", "#A3FFFFFF"); Set("Text3", "#6EFFFFFF");
                Set("Accent", "#0A84FF"); Set("Card", "#0FFFFFFF"); Set("CardBorder", "#12FFFFFF"); Set("Sep", "#17FFFFFF");
                Set("Hover", "#14FFFFFF"); Set("Control", "#1FFFFFFF"); Set("Field", "#14FFFFFF"); Set("Dot", "#24FFFFFF");
                Set("SwitchOff", "#33FFFFFF"); Set("Scroll", "#59FFFFFF"); Set("Tip", "#FF3A3A3D"); Set("LightOff", "#4DFFFFFF"); Set("Surface", "#FF29292B");
                Set("Glass", "#1AFFFFFF"); Set("GlassBase", "#8C2B2B2E"); Set("WindowRim", "#38FFFFFF");
                Set("SidebarTint", Alpha("#2A2A2D", .45 + .53 * g)); Set("ContentTint", Alpha("#1C1C1E", .72 + .28 * g));
                Rim("#66FFFFFF", "#14FFFFFF", "#33FFFFFF");
            }
            else
            {
                Set("Text", "#E6000000"); Set("Text2", "#A8000000"); Set("Text3", "#80000000");
                Set("Accent", "#0A7AFF"); Set("Card", "#99FFFFFF"); Set("CardBorder", "#0F000000"); Set("Sep", "#14000000");
                Set("Hover", "#0D000000"); Set("Control", "#14000000"); Set("Field", "#80FFFFFF"); Set("Dot", "#17000000");
                Set("SwitchOff", "#26000000"); Set("Scroll", "#4D000000"); Set("Tip", "#FFFFFFFF"); Set("LightOff", "#2E000000"); Set("Surface", "#FFFAFAFB");
                Set("Glass", "#66FFFFFF"); Set("GlassBase", "#99FFFFFF"); Set("WindowRim", "#33000000");
                Set("SidebarTint", Alpha("#F0F0F4", .68 + .31 * g)); Set("ContentTint", Alpha("#F7F7F9", .84 + .16 * g));
                Rim("#FFFFFFFF", "#26000000", "#B3FFFFFF");
            }
            ApplySelection();
            // Les pastilles calculent leur couleur a la construction.
            if (was != dark && host != null && host.Children.Count > 0 && page != "appearance") Render(false);
        }

        void Rim(string a, string b, string c)
        {
            var l = new LinearGradientBrush { StartPoint = new Point(.2, 0), EndPoint = new Point(.8, 1) };
            l.GradientStops.Add(new GradientStop(Hex(a), 0));
            l.GradientStops.Add(new GradientStop(Hex(b), .5));
            l.GradientStops.Add(new GradientStop(Hex(c), 1));
            l.Freeze();
            w.Resources["GlassRim"] = l;
        }

        void ApplySelection()
        {
            bool on = w.IsActive;
            w.Resources["Selection"] = w.Resources[on ? "Accent" : "Control"];
            w.Resources["SelectionText"] = on ? Brushes.White : w.Resources["Text"];
        }

        // ================= verre =================

        /// <summary>Pastille qui floute le contenu passant dessous.</summary>
        void SetupLens(string name)
        {
            var g = Get<Grid>(name);
            var brush = new VisualBrush(host) { ViewboxUnits = BrushMappingMode.Absolute, ViewportUnits = BrushMappingMode.RelativeToBoundingBox, Stretch = Stretch.Fill };
            g.Children.Add(new Rectangle { Fill = brush, Opacity = .55, Effect = new BlurEffect { Radius = 9, KernelType = KernelType.Gaussian } });
            lenses.Add(brush);
            lensHosts.Add(g);
            g.SizeChanged += (s, e) => { g.Clip = new RectangleGeometry(new Rect(g.RenderSize), 18, 18); UpdateLenses(); };
            scroller.ScrollChanged += (s, e) => UpdateLenses();
        }

        void UpdateLenses()
        {
            for (int i = 0; i < lenses.Count; i++)
            {
                var g = lensHosts[i];
                if (g.ActualWidth < 1 || !g.IsVisible) continue;
                Point o = g.TranslatePoint(new Point(0, 0), host);
                lenses[i].Viewbox = new Rect(o, g.RenderSize);
            }
        }

        /// <summary>Fond d'ecran fortement floute, aligne sur la position de la fenetre.</summary>
        void LoadWallpaper()
        {
            try
            {
                var sb = new StringBuilder(520);
                SystemParametersInfo(0x73, sb.Capacity, sb, 0); // SPI_GETDESKWALLPAPER
                string path = sb.ToString();
                if (!File.Exists(path))
                    path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
                BitmapSource img = null;
                if (File.Exists(path))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = 192;
                    bi.UriSource = new Uri(path);
                    bi.EndInit();
                    img = bi;
                }
                wallBrush = new ImageBrush(img == null ? Fallback() : Blur(img)) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };
            }
            catch { wallBrush = new ImageBrush(Fallback()) { Stretch = Stretch.Fill }; }
            RenderOptions.SetBitmapScalingMode(wallBrush, BitmapScalingMode.HighQuality);
            wall.Fill = wallBrush;
            UpdateWallpaper();
        }

        static BitmapSource Fallback()
        {
            var v = new DrawingVisual();
            using (var d = v.RenderOpen())
            {
                d.DrawRectangle(new LinearGradientBrush(Hex("#3B6FD8"), Hex("#E0729A"), 35), null, new Rect(0, 0, 192, 108));
                d.DrawEllipse(new RadialGradientBrush(Hex("#CCFFB26B"), Hex("#00FFB26B")), null, new Point(150, 20), 90, 70);
            }
            var r = new RenderTargetBitmap(192, 108, 96, 96, PixelFormats.Pbgra32);
            r.Render(v);
            return r;
        }

        static BitmapSource Blur(BitmapSource src)
        {
            // Recadrage comme le mode "Remplir" de Windows
            double ratio = SystemParameters.PrimaryScreenWidth / SystemParameters.PrimaryScreenHeight;
            int cw = Math.Min(src.PixelWidth, (int)(src.PixelHeight * ratio)), ch = Math.Min(src.PixelHeight, (int)(src.PixelWidth / ratio));
            var crop = new CroppedBitmap(src, new Int32Rect((src.PixelWidth - cw) / 2, (src.PixelHeight - ch) / 2, cw, ch));
            var conv = new FormatConvertedBitmap(crop, PixelFormats.Bgr32, null, 0);
            int W = conv.PixelWidth, H = conv.PixelHeight, R = 9;
            var px = new byte[W * H * 4];
            conv.CopyPixels(px, W * 4, 0);
            var tmp = new byte[px.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                Box(px, tmp, W, H, R, true);
                Box(tmp, px, W, H, R, false);
            }
            for (int i = 0; i < px.Length; i += 4) // couleurs un peu plus vives, comme le verre macOS
            {
                double l = px[i] * .114 + px[i + 1] * .587 + px[i + 2] * .299;
                for (int c = 0; c < 3; c++) px[i + c] = (byte)Math.Max(0, Math.Min(255, l + (px[i + c] - l) * 1.5));
            }
            var b = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgr32, null, px, W * 4);
            b.Freeze();
            return b;
        }

        static void Box(byte[] a, byte[] o, int W, int H, int R, bool horizontal)
        {
            int n = horizontal ? W : H, m = horizontal ? H : W;
            int step = horizontal ? 4 : W * 4, row = horizontal ? W * 4 : 4;
            for (int j = 0; j < m; j++)
                for (int c = 0; c < 3; c++)
                {
                    int sum = 0, s = j * row + c;
                    for (int k = -R; k <= R; k++) sum += a[s + Math.Max(0, Math.Min(n - 1, k)) * step];
                    for (int i = 0; i < n; i++)
                    {
                        o[s + i * step] = (byte)(sum / (2 * R + 1));
                        sum += a[s + Math.Min(n - 1, i + R + 1) * step] - a[s + Math.Max(0, i - R) * step];
                    }
                }
        }

        void UpdateWallpaper()
        {
            if (wallBrush == null || w.WindowState == WindowState.Minimized || PresentationSource.FromVisual(shell) == null || shell.ActualWidth < 1) return;
            var a = shell.PointToScreen(new Point(0, 0));
            var b = shell.PointToScreen(new Point(shell.ActualWidth, shell.ActualHeight));
            var scr = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(w).Handle).Bounds;
            double vw = Math.Min(1, (b.X - a.X) / scr.Width), vh = Math.Min(1, (b.Y - a.Y) / scr.Height);
            double x = Math.Max(0, Math.Min(1 - vw, (a.X - scr.X) / scr.Width)), y = Math.Max(0, Math.Min(1 - vh, (a.Y - scr.Y) / scr.Height));
            wallBrush.Viewbox = new Rect(x, y, vw, vh);
        }

        // ================= fenetre =================

        void SetupWindow()
        {
            // Ecrans plus petits que la taille par defaut
            w.Width = Math.Min(w.Width, SystemParameters.WorkArea.Width);
            w.Height = Math.Min(w.Height, SystemParameters.WorkArea.Height);

            Get<Button>("BtnClose").Click += (s, e) => w.Close();
            Get<Button>("BtnMin").Click += (s, e) => w.WindowState = WindowState.Minimized;
            Get<Button>("BtnMax").Click += (s, e) => ToggleMax();
            MouseButtonEventHandler drag = (s, e) =>
            {
                if (e.ClickCount == 2) ToggleMax();
                else if (e.ButtonState == MouseButtonState.Pressed) w.DragMove();
            };
            Get<Grid>("SideDrag").MouseLeftButtonDown += drag;
            Get<Grid>("Toolbar").MouseLeftButtonDown += drag;

            w.SourceInitialized += (s, e) => HwndSource.FromHwnd(new WindowInteropHelper(w).Handle).AddHook(Hook);
            w.Loaded += (s, e) =>
            {
                LoadWallpaper();
                if (!SystemParameters.ClientAreaAnimation) return;
                var t = new ScaleTransform(1, 1);
                outer.RenderTransform = t;
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var z = new DoubleAnimation(.96, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
                t.BeginAnimation(ScaleTransform.ScaleXProperty, z);
                t.BeginAnimation(ScaleTransform.ScaleYProperty, z);
                outer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            };
            w.LocationChanged += (s, e) => UpdateWallpaper();
            w.StateChanged += (s, e) => Shape_();
            shell.SizeChanged += (s, e) => { Shape_(); UpdateWallpaper(); };
            w.Activated += (s, e) => { shadow.Opacity = 1; ApplySelection(); };
            w.Deactivated += (s, e) => ApplySelection();
            SystemEvents.UserPreferenceChanged += OnPreference;
            w.Closed += (s, e) => SystemEvents.UserPreferenceChanged -= OnPreference;
        }

        void OnPreference(object s, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Desktop) return;
            w.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyTheme();
                if (e.Category == UserPreferenceCategory.Desktop) LoadWallpaper();
            }));
        }

        void ToggleMax() { w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }

        void Shape_()
        {
            bool max = w.WindowState == WindowState.Maximized;
            double r = max ? 0 : 20;
            shell.Margin = shadow.Margin = new Thickness(max ? 0 : 28);
            shadow.Visibility = rim.Visibility = max ? Visibility.Collapsed : Visibility.Visible;
            shell.Clip = new RectangleGeometry(new Rect(shell.RenderSize), r, r);
        }

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO : ne pas recouvrir la barre des taches
            {
                var scr = System.Windows.Forms.Screen.FromHandle(hwnd);
                var m = (MinMax)Marshal.PtrToStructure(lp, typeof(MinMax));
                m.maxPos.x = scr.WorkingArea.X - scr.Bounds.X;
                m.maxPos.y = scr.WorkingArea.Y - scr.Bounds.Y;
                m.maxSize.x = scr.WorkingArea.Width;
                m.maxSize.y = scr.WorkingArea.Height;
                var src = PresentationSource.FromVisual(w);
                double k = src == null ? 1 : src.CompositionTarget.TransformToDevice.M11;
                m.minTrack.x = (int)(w.MinWidth * k);
                m.minTrack.y = (int)(w.MinHeight * k);
                Marshal.StructureToPtr(m, lp, true);
                handled = true;
            }
            else if (msg == 0x0084 && w.WindowState == WindowState.Normal) // WM_NCHITTEST : bords de redimensionnement
            {
                long v = lp.ToInt64();
                var p = shell.PointFromScreen(new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF)));
                double W = shell.ActualWidth, H = shell.ActualHeight;
                if (p.X < -6 || p.Y < -6 || p.X > W + 6 || p.Y > H + 6) return IntPtr.Zero;
                bool near = p.X < 4 || p.Y < 4 || p.X > W - 4 || p.Y > H - 4;
                if (!near) return IntPtr.Zero;
                bool l = p.X < 16, r = p.X > W - 16, t = p.Y < 16, b = p.Y > H - 16;
                int hit = t && l ? 13 : t && r ? 14 : b && l ? 16 : b && r ? 17 : p.X < 4 ? 10 : p.X > W - 4 ? 11 : p.Y < 4 ? 12 : 15;
                handled = true;
                return new IntPtr(hit);
            }
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Pt { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        struct MinMax { public Pt reserved, maxSize, maxPos, minTrack, maxTrack; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SystemParametersInfo(int action, int size, StringBuilder buf, int flags);
    }
}
