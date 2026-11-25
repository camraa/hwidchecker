// Pages : test reseau, comparaison de deux PC, diagnostic, alertes, redemarrage dans le BIOS.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HwidChecker
{
    /// <summary>Erreurs rencontrees, sans valeur ni chemin : seulement l'endroit et le type.</summary>
    public static class Diag
    {
        static readonly List<string> errors = new List<string>();

        public static void Error(string where, Exception e)
        {
            lock (errors)
            {
                string t = where + " : " + e.GetType().Name + " 0x" + e.HResult.ToString("X8");
                if (errors.Count < 60 && !errors.Contains(t)) errors.Add(t);
            }
        }

        public static List<string> Errors { get { lock (errors) return errors.ToList(); } }
    }

    public partial class App
    {
        // ================= test reseau =================

        List<Probe> probes;
        bool probing;
        TextBlock probeLabel;

        Panel NetPage()
        {
            var p = PageRoot();
            var go = Capsule(probes == null ? "Lancer le test" : "Relancer le test", true, RunNet);
            go.IsEnabled = !probing;
            go.HorizontalAlignment = HorizontalAlignment.Center;
            go.Margin = new Thickness(0, 16, 0, 0);
            AutomationProperties.SetAutomationId(go, "nettest");
            var hero = Hero("ping", "#34C759", probing ? "Test en cours…" : "Test réseau",
                "Ping, pertes et gigue vers Internet et les serveurs de jeux, puis temps de réponse des serveurs DNS.", go);
            probeLabel = ((StackPanel)hero.Child).Children.OfType<TextBlock>().First();
            p.Children.Add(hero);
            if (probes == null || probing) return p;
            foreach (var g in probes.GroupBy(x => x.Group))
                AddGroup(p, g.Key, null, g.Select(x => (UIElement)Row(new Field
                {
                    Label = x.Name, State = x.State, Value = ProbeText(x),
                    Help = x.Dns ? "Temps moyen pour traduire un nom de site, mesuré directement auprès de ce serveur."
                        : x.Name == "Passerelle" ? "Votre box ou routeur. Un ping élevé ici vient du Wi-Fi ou du câble, pas d’Internet."
                        : x.Group == "Centres de données AWS" ? "Beaucoup de jeux hébergent leurs serveurs dans ces centres de données : le ping donne un ordre de grandeur."
                        : "Temps d’aller-retour moyen sur dix essais. La gigue est la variation entre deux essais. Certains serveurs ne répondent pas au ping."
                }, false)));
            return p;
        }

        static string ProbeText(Probe x)
        {
            if (x.Times.Count == 0) return "Sans réponse";
            string t = (x.Average < 1 ? "< 1" : x.Average.ToString("0")) + " ms";
            if (!x.Dns) t += " · " + L.T("gigue") + " " + x.Jitter.ToString("0") + " ms";
            return t + " · " + L.T("pertes") + " " + x.Loss + " %";
        }

        async void RunNet()
        {
            if (probing) return;
            probing = true;
            Render(false);
            var list = NetTest.Targets();
            try
            {
                await Task.Run(() => NetTest.Run(list, (i, n) => w.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (probeLabel != null && probing) probeLabel.Text = L.T("Test en cours…") + " " + i + " / " + n;
                }))));
                probes = list;
            }
            finally { probing = false; }
            if (page == "net") Render(true);
        }

        // ================= comparer deux PC =================

        Dictionary<string, string> other;
        string otherName;
        bool compareIds;

        /// <summary>Donnees comparables : section | rang du groupe | libelle.</summary>
        Dictionary<string, string[]> Comparable(bool ids)
        {
            var d = new Dictionary<string, string[]>();
            foreach (var s in report.Sections.Where(x => x.Key != "arp" && x.Key != "ip" && x.Key != "startup"))
                for (int i = 0; i < s.Groups.Count; i++)
                    foreach (var f in s.Groups[i].Fields)
                    {
                        if (f.Volatile || f.Url != null || (f.IsId && !ids)) continue;
                        string k = s.Key + "|" + i + "|" + f.Label;
                        if (!d.ContainsKey(k)) d[k] = new[] { f.Value, f.IsId ? "1" : "0" };
                    }
            return d;
        }

        void ComparePcCard(Panel p)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (other != null)
            {
                var close = Capsule("Fermer", false, () => { other = null; Render(false); });
                close.Margin = new Thickness(0, 0, 8, 0);
                row.Children.Add(close);
            }
            row.Children.Add(Capsule("Choisir un export…", other == null, OpenOther));
            var rows = new List<UIElement>
            {
                SettingRow(other == null ? "Comparer avec un autre PC" : "Comparé à : " + otherName, "Sur l’autre PC : bouton Exporter, format JSON. Choisissez ensuite ce fichier ici. Rien n’est envoyé.", row)
            };
            if (other != null)
            {
                var sw = new CheckBox { Style = (Style)w.Resources["Switch"], IsChecked = compareIds };
                sw.Click += (s, e) => { compareIds = sw.IsChecked == true; Render(false); };
                rows.Add(SettingRow("Inclure les identifiants", "Deux PC ont toujours des numéros de série différents : ils sont masqués par défaut pour ne garder que le matériel et les réglages.", sw));
            }
            AddGroup(p, "Comparer deux PC", null, rows);
            if (other == null) return;

            var mine = Comparable(compareIds);
            int shown = 0;
            foreach (var s in report.Sections)
            {
                var keys = mine.Keys.Union(other.Keys).Where(k => k.StartsWith(s.Key + "|")).Where(k =>
                {
                    if (!mine.ContainsKey(k) && !compareIds && IdLabel(k)) return false;
                    string b;
                    other.TryGetValue(k, out b);
                    return (mine.ContainsKey(k) ? mine[k][0] : null) != b;
                }).ToList();
                foreach (var grp in keys.GroupBy(k => int.Parse(k.Split('|')[1])).OrderBy(x => x.Key))
                {
                    var lines = new List<UIElement>();
                    foreach (var k in grp)
                    {
                        string b;
                        other.TryGetValue(k, out b);
                        string a = mine.ContainsKey(k) ? mine[k][0] : null;
                        bool id = (mine.ContainsKey(k) && mine[k][1] == "1") || IdLabel(k);
                        lines.Add(CompareRow(k.Split(new[] { '|' }, 3)[2], a, b, id));
                        shown++;
                    }
                    AddGroup(p, s.Title, s.Groups.Count > 1 || grp.Key > 0 ? "n° " + (grp.Key + 1) : null, lines);
                }
            }
            if (shown == 0) p.Children.Add(Note("Aucune différence."));
        }

        static bool IdLabel(string key)
        {
            string label = key.Split(new[] { '|' }, 3)[2];
            return label.StartsWith("Numéro de série") || label.StartsWith("Identifiant") || label.StartsWith("UUID") || label.StartsWith("Adresse") || label.StartsWith("Volume ")
                || label == "MachineGuid" || label.StartsWith("ID ") || label.StartsWith("SID") || label.StartsWith("Profil matériel") || label == "Nom de l’ordinateur" || label.StartsWith("Étiquette");
        }

        UIElement CompareRow(string label, string a, string b, bool id)
        {
            var g = new Grid { Margin = new Thickness(14, 8, 14, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(Text(label, 13, null, false));
            for (int i = 0; i < 2; i++)
            {
                string v = i == 0 ? a : b;
                var box = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
                box.Children.Add(Text(i == 0 ? "Ce PC" : "Autre PC", 11, "Text3", false));
                var t = new TextBlock { Text = v == null ? L.T("Absent") : id ? Shown(v, true) : L.T(v), TextWrapping = TextWrapping.Wrap, FontSize = 12.5 };
                t.SetResourceReference(TextBlock.ForegroundProperty, v == null ? "Text3" : "Text");
                box.Children.Add(t);
                Grid.SetColumn(box, i + 1);
                g.Children.Add(box);
            }
            return g;
        }

        void OpenOther()
        {
            var d = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (d.ShowDialog(w) == true) LoadOther(d.FileName);
        }

        void LoadOther(string file)
        {
            try
            {
                if (new FileInfo(file).Length > 5000000) throw new InvalidDataException();
                var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
                object c, n;
                int rank;
                if (!root.TryGetValue("comparaison", out c)) { ShowToast("Export trop ancien : refaites-le avec la version 1.2"); return; }
                other = ((Dictionary<string, object>)c)
                    .Where(x => x.Key.Split('|').Length >= 3 && int.TryParse(x.Key.Split('|')[1], out rank))
                    .ToDictionary(x => x.Key, x => Convert.ToString(x.Value));
                otherName = root.TryGetValue("modele", out n) ? Convert.ToString(n) : "?";
                Render(true);
            }
            catch { ShowToast("Fichier illisible"); }
        }

        // ================= diagnostic =================

        async void CopyDiagnostic()
        {
            ShowToast("Mesure en cours…");
            string t = await Task.Run(() => Diagnostic());
            Copy(t, "Diagnostic copié, sans identifiant");
        }

        string Diagnostic()
        {
            var sb = new StringBuilder();
            sb.AppendLine("HWID Checker " + VersionText + " / " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("Windows build " + Hardware.Build + ", " + (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits") + ", process " + (Environment.Is64BitProcess ? "64" : "32")
                + ", admin " + Hardware.IsAdmin + ", culture " + System.Globalization.CultureInfo.CurrentCulture.Name + ", CPU " + Environment.ProcessorCount + " threads");
            object rel = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", null);
            sb.AppendLine(".NET " + Environment.Version + " release " + rel);
            if (report != null)
            {
                foreach (var s in report.Sections)
                    sb.AppendLine("  " + s.Key + " : " + s.Groups.Count + " groupes, " + s.Groups.Sum(g => g.Fields.Count) + " champs, "
                        + s.Groups.Sum(g => g.Fields.Count(f => f.Value == "Non communiqué")) + " non communiqués, " + s.Groups.Sum(g => g.Fields.Count(f => f.Generic)) + " génériques");
                foreach (var f in report.Sections.Where(s => s.Key == "security").SelectMany(s => s.Groups).SelectMany(g => g.Fields).Where(f => f.State != null))
                    sb.AppendLine("  " + f.Label + " = " + f.Value);
                foreach (var f in report.Sections.SelectMany(s => s.Groups).SelectMany(g => g.Fields).Where(f => f.Label == "Santé"))
                    sb.AppendLine("  Santé = " + f.Value);
            }
            sb.AppendLine("CPU (" + string.Join(" | ", CpuLoad.Names) + ")");
            for (int i = 0; i < 5; i++)
            {
                var s = Live.Read();
                sb.AppendLine("  " + Num(s.Cpu) + "  <-  " + string.Join(" | ", CpuLoad.Raw.Select(Num)) + "   RAM " + Num(s.Ram) + "  GPU " + Num(s.Gpu) + "  T.GPU " + Num(s.GpuTemp) + "  T.ACPI " + Num(s.ZoneTemp));
                Thread.Sleep(1000);
            }
            if (drivers != null) sb.AppendLine("Pilotes : " + string.Join(", ", drivers.GroupBy(d => d.State).Select(g => g.Key + "=" + g.Count())));
            var err = Diag.Errors;
            sb.AppendLine("Erreurs : " + err.Count);
            foreach (var e in err) sb.AppendLine("  " + e);
            return sb.ToString();
        }

        static string Num(double v) { return double.IsNaN(v) ? "n/a" : v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }

        // ================= alertes =================

        readonly List<Field> alerts = new List<Field>();
        readonly Dictionary<string, DateTime> lastAlert = new Dictionary<string, DateTime>();
        System.Windows.Forms.NotifyIcon tray;
        DispatcherTimer watch;
        int hotGpu, hotDisk;

        /// <summary>Alertes deduites du dernier releve (affichees en haut de la vue d'ensemble).</summary>
        void BuildAlerts()
        {
            alerts.Clear();
            if (cfg.NoAlerts) return;
            foreach (var s in report.Sections.Where(x => x.Key == "storage" || x.Key == "battery"))
                foreach (var g in s.Groups)
                {
                    var f = g.Fields.FirstOrDefault(x => x.Label == "Santé" && x.State != null && x.State != "on");
                    if (f != null) alerts.Add(new Field { Label = string.IsNullOrEmpty(g.Sub) ? g.Title : g.Title + " · " + g.Sub, Value = f.Value, State = f.State, Help = f.Help });
                }
            foreach (var g in report.Sections.Where(x => x.Key == "mac").SelectMany(x => x.Groups).Where(g => g.Fields.Any(f => f.Alert != null)))
                alerts.Add(new Field { Label = g.Title, Value = "Adresse MAC modifiée", State = "warn", Help = Help.For("mac", "Adresse MAC d’usine") });
            var last = history.Entries.FirstOrDefault();
            // Peripheriques et ecrans exclus : les brancher ou les debrancher est normal.
            int n = last == null ? 0 : last.Changes.Count(c => !c[0].StartsWith("Périphériques externes") && !c[0].StartsWith("Écrans"));
            if (n > 0)
                alerts.Add(new Field { Label = "Identifiants", Value = n == 1 ? "1 changement" : n + " changements", State = "warn", Help = "Des identifiants ont changé depuis le lancement précédent. Le détail est dans Historique." });
            foreach (var a in alerts.Where(x => x.State == "off")) Notify(a.Label, a.Value);
        }

        void StartWatch()
        {
            if (watch != null) return;
            watch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            watch.Tick += async (s, e) =>
            {
                if (cfg.NoAlerts || reading) return;
                reading = true;
                double gpu = double.NaN, disk = double.NaN;
                try
                {
                    await Task.Run(() => { gpu = Live.GpuTemperature(); disk = Hardware.HottestNvme(); });
                }
                catch { }
                finally { reading = false; }
                // Deux releves de suite pour ignorer un pic.
                hotGpu = gpu >= 88 ? hotGpu + 1 : 0;
                hotDisk = disk >= 75 ? hotDisk + 1 : 0;
                if (hotGpu >= 2) Notify("Température du GPU", gpu.ToString("0") + " °C");
                if (hotDisk >= 2) Notify("Température du SSD", disk.ToString("0") + " °C");
            };
            watch.Start();
            w.Closed += (s, e) => { watch.Stop(); if (tray != null) tray.Dispose(); };
        }

        void Notify(string title, string text)
        {
            DateTime d;
            if (lastAlert.TryGetValue(title, out d) && (DateTime.Now - d).TotalMinutes < 10) return;
            lastAlert[title] = DateTime.Now;
            if (w.IsActive) { ShowToast(L.T(title) + " : " + L.T(text)); return; }
            try
            {
                if (tray == null)
                {
                    using (var ico = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                        tray = new System.Windows.Forms.NotifyIcon { Icon = new System.Drawing.Icon(ico, 16, 16), Text = "HWID Checker" };
                    tray.BalloonTipClosed += (s, e) => tray.Visible = false;
                    tray.BalloonTipClicked += (s, e) => { tray.Visible = false; w.WindowState = WindowState.Normal; w.Activate(); };
                }
                tray.Visible = true;
                tray.ShowBalloonTip(8000, "HWID Checker", L.T(title) + " : " + L.T(text), System.Windows.Forms.ToolTipIcon.Warning);
            }
            catch (Exception e) { Diag.Error("Notify", e); }
        }

        // ================= BIOS =================

        UIElement BiosCard()
        {
            var b = Capsule("Redémarrer dans le BIOS…", false, RebootToBios);
            AutomationProperties.SetAutomationId(b, "bios");
            bool uefi = report.Sections.Where(s => s.Key == "security").SelectMany(s => s.Groups).SelectMany(g => g.Fields).Any(f => f.Label == "Démarrage UEFI" && f.State == "on");
            b.IsEnabled = uefi && Hardware.IsAdmin;
            var c = Card(SettingRow("Réglages du BIOS", !uefi ? "Disponible seulement en mode UEFI." : !Hardware.IsAdmin ? "Demande les droits administrateur."
                : "Redémarre ce PC directement dans les réglages du BIOS, après confirmation.", b));
            c.Margin = new Thickness(0, 14, 0, 0);
            return c;
        }

        void RebootToBios()
        {
            var ok = MessageBox.Show(w, L.T("Le PC va redémarrer tout de suite et ouvrir les réglages du BIOS. Enregistrez votre travail avant de continuer.") + "\n\n" + L.T("Redémarrer maintenant ?"),
                "HWID Checker", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (ok != MessageBoxResult.Yes) return;
            try
            {
                var psi = new ProcessStartInfo(Environment.SystemDirectory + @"\shutdown.exe", "/r /fw /t 0") { UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                    if (!p.WaitForExit(8000) || p.ExitCode != 0) ShowToast("Ce BIOS refuse le redémarrage direct");
            }
            catch (Exception e) { Diag.Error("RebootToBios", e); ShowToast("Ce BIOS refuse le redémarrage direct"); }
        }

        // ================= demarrage =================

        UIElement StartupCard()
        {
            var b = Capsule("Ouvrir les réglages", false, () => Open("ms-settings:startupapps"));
            var c = Card(SettingRow("Activer ou désactiver un programme", Hardware.IsAdmin
                ? "Ouvre la page Démarrage des Paramètres Windows."
                : "Ouvre la page Démarrage des Paramètres Windows. L’impact n’est lisible qu’avec les droits administrateur.", b));
            c.Margin = new Thickness(0, 14, 0, 0);
            return c;
        }
    }
}
