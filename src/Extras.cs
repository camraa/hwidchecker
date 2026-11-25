// Batterie, programmes de demarrage, temperatures NVMe.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;

namespace HwidChecker
{
    public static partial class Hardware
    {
        public static bool IsAdmin
        {
            get
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        // ---------- batterie ----------

        static Section Battery()
        {
            if (Query("SELECT BatteryStatus FROM Win32_Battery").Count == 0) return null;
            var s = new Section { Key = "battery", Category = "Matériel", Title = "Batterie", Icon = "battery", Color = "#30D158",
                Description = "Capacité réelle, usure et cycles de la batterie." };

            XElement root = null;
            string file = Path.Combine(Path.GetTempPath(), "hwid-battery-" + Process.GetCurrentProcess().Id + ".xml");
            try
            {
                Run(Environment.SystemDirectory + @"\powercfg.exe", "/batteryreport /xml /output \"" + file + "\"");
                if (File.Exists(file)) root = XDocument.Load(file).Root;
            }
            catch { /* rapport indisponible */ }
            finally { try { File.Delete(file); } catch { } }

            var live = Query("SELECT * FROM BatteryStatus", @"root\wmi");
            int n = 0;
            var list = root == null ? new List<XElement>() : root.Descendants().Where(e => e.Name.LocalName == "Battery" && e.Elements().Any(x => x.Name.LocalName == "DesignCapacity")).ToList();
            foreach (var b in list)
            {
                Func<string, string> X = name => { var e = b.Elements().FirstOrDefault(x => x.Name.LocalName == name); return e == null ? "" : e.Value.Trim(); };
                double design, full;
                double.TryParse(X("DesignCapacity"), out design);
                double.TryParse(X("FullChargeCapacity"), out full);
                string chem = X("Chemistry").ToUpperInvariant();
                chem = chem.StartsWith("LI-I") || chem == "LION" ? "Lithium-ion" : chem.StartsWith("LIP") ? "Lithium-polymère" : chem;
                var g = new Group(list.Count > 1 ? "Batterie " + (n + 1) : "Batterie", Clean(X("Manufacturer")));
                if (design > 0 && full > 0)
                {
                    int health = (int)Math.Min(100, Math.Round(100 * full / design));
                    g.Status("Santé", health < 50 ? "off" : health < 75 ? "warn" : "on",
                        (health < 50 ? "À remplacer" : health < 75 ? "À surveiller" : "Bon état") + " (" + health + " %)");
                }
                string id = X("Id");
                g.Id("Numéro de série (SN)", X("SerialNumber"))
                 .Info("Marque", Clean(X("Manufacturer")))
                 .Info("Modèle", Clean(id))
                 .Info("Technologie", chem)
                 .Info("Capacité d’origine", design > 0 ? (design / 1000).ToString("0.0") + " Wh" : "")
                 .Info("Capacité actuelle", full > 0 ? (full / 1000).ToString("0.0") + " Wh" : "")
                 .Info("Cycles de charge", X("CycleCount") == "0" || X("CycleCount").Length == 0 ? "Non communiqué" : X("CycleCount"));
                var l = n < live.Count ? live[n] : null;
                if (l != null)
                {
                    double rest = Num(l, "RemainingCapacity"), volt = Num(l, "Voltage");
                    if (full > 0 && rest > 0) g.Info("Charge", (int)Math.Min(100, Math.Round(100 * rest / full)) + " %");
                    g.Info("Alimentation", Str(l, "PowerOnline") == "True" ? "Sur secteur" : "Sur batterie")
                     .Info("Tension", volt > 0 ? (volt / 1000).ToString("0.00") + " V" : "");
                    double rate = Num(l, "DischargeRate");
                    if (rate > 0 && rate < 300000) g.Info("Consommation", (rate / 1000).ToString("0.0") + " W");
                }
                s.Groups.Add(g);
                n++;
            }
            s.Empty = "La batterie ne communique aucune information.";
            return s;
        }

        // ---------- demarrage ----------

        class StartItem { public string Name, Path, Approved; public bool Machine; }

        static string ExePath(string command)
        {
            string c = Environment.ExpandEnvironmentVariables(command ?? "").Trim();
            var m = Regex.Match(c, "^\"([^\"]+)\"");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(c, @"^(.+?\.(exe|bat|cmd|lnk|vbs|ps1))(\s|$)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : c;
        }

        static Section Startup()
        {
            var s = new Section { Key = "startup", Category = "Système", Title = "Démarrage", Icon = "startup", Color = "#FF9F0A",
                Description = "Programmes lancés à l’ouverture de session, avec l’impact mesuré par Windows.", Empty = "Aucun programme au démarrage." };
            var items = new List<StartItem>();
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (var sub in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run" })
                    try
                    {
                        using (var b = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                        using (var k = b.OpenSubKey(sub))
                            if (k != null)
                                foreach (var v in k.GetValueNames().Where(x => x.Length > 0))
                                    items.Add(new StartItem { Name = v, Path = ExePath(Convert.ToString(k.GetValue(v))), Machine = hive == RegistryHive.LocalMachine, Approved = sub.Contains("WOW6432") ? "Run32" : "Run" });
                    }
                    catch { }
            foreach (var f in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
                try
                {
                    foreach (var p in Directory.GetFiles(Environment.GetFolderPath(f)).Where(p => !p.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                        items.Add(new StartItem { Name = Path.GetFileName(p), Path = p, Machine = f == Environment.SpecialFolder.CommonStartup, Approved = "StartupFolder" });
                }
                catch { }

            var impact = StartupImpact();
            Group on = new Group("Activés"), off = new Group("Désactivés");
            foreach (var i in items.GroupBy(x => x.Name + "|" + x.Path).Select(x => x.First()).OrderBy(x => x.Name))
            {
                bool enabled = true;
                try
                {
                    using (var b = RegistryKey.OpenBaseKey(i.Machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64))
                    using (var k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\" + i.Approved))
                    {
                        var raw = k == null ? null : k.GetValue(i.Name) as byte[];
                        if (raw != null && raw.Length > 0) enabled = (raw[0] & 1) == 0; // impair : desactive
                    }
                }
                catch { }
                string maker = "";
                try { if (File.Exists(i.Path)) maker = (FileVersionInfo.GetVersionInfo(i.Path).CompanyName ?? "").Trim(); } catch { }
                string name = Regex.Replace(i.Name, @"(_[0-9A-F]{16,}|\.lnk$)", "", RegexOptions.IgnoreCase);
                if (!enabled) { off.Info(name, maker.Length == 0 ? "Désactivé" : maker); continue; }
                int level;
                string exe = "";
                try { exe = Path.GetFileName(i.Path).ToLowerInvariant(); } catch { }
                string suffix = maker.Length == 0 ? "" : " · " + maker;
                if (impact.TryGetValue(exe, out level))
                    on.Status(name, level == 2 ? "off" : level == 1 ? "warn" : "on", (level == 2 ? "Impact élevé" : level == 1 ? "Impact moyen" : "Impact faible") + suffix);
                else on.Info(name, "Impact non mesuré" + suffix);
            }
            on.Sub = on.Fields.Count.ToString();
            off.Sub = off.Fields.Count.ToString();
            if (on.Fields.Count > 0) s.Groups.Add(on);
            if (off.Fields.Count > 0) s.Groups.Add(off);
            return s;
        }

        /// <summary>
        /// Mesures de Windows (dossier reserve aux administrateurs). Seuils du Gestionnaire des taches :
        /// eleve au-dela d'une seconde de processeur ou de 3 Mo lus, moyen au-dela de 0,3 s ou 300 Ko.
        /// </summary>
        static Dictionary<string, int> StartupImpact()
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string sid;
                using (var id = WindowsIdentity.GetCurrent()) sid = id.User.Value;
                var dir = new DirectoryInfo(Environment.SystemDirectory + @"\wdi\LogFiles\StartupInfo");
                var last = dir.GetFiles(sid + "_StartupInfo*.xml").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
                if (last == null) return d;
                foreach (var p in XDocument.Load(last.FullName).Descendants().Where(e => e.Name.LocalName == "Process"))
                {
                    string exe = Path.GetFileName((string)p.Attribute("Name") ?? "").ToLowerInvariant();
                    Func<string, double> X = n =>
                    {
                        var e = p.Elements().FirstOrDefault(x => x.Name.LocalName == n);
                        double v;
                        return e != null && double.TryParse(e.Value, out v) ? v : 0;
                    };
                    double cpu = X("CpuUsage") / 1e6, disk = X("DiskUsage"); // microsecondes, octets
                    int level = cpu > 1 || disk > 3 * 1048576 ? 2 : cpu > 0.3 || disk > 300 * 1024 ? 1 : 0, old;
                    if (!d.TryGetValue(exe, out old) || level > old) d[exe] = level;
                }
            }
            catch { /* sans droits administrateur : impact inconnu */ }
            return d;
        }

        /// <summary>Temperature NVMe la plus elevee, ou NaN.</summary>
        public static double HottestNvme()
        {
            double best = double.NaN;
            for (int i = 0; i < 6; i++)
            {
                byte[] log = null;
                try { log = NvmeLog(i.ToString()); } catch { }
                if (log == null) continue;
                int k = BitConverter.ToUInt16(log, 1);
                if (k > 200 && k < 420 && (double.IsNaN(best) || k - 273 > best)) best = k - 273;
            }
            return best;
        }
    }
}
