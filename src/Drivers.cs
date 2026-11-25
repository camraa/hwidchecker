// Recherche de mises a jour de pilotes et de BIOS.
// Regle : un lien n'est affiche que si la page a ete ouverte et confirmee pour cet appareil precis
// (identifiant materiel exact, architecture et version de Windows). Sinon, aucun lien.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HwidChecker
{
    public class Driver
    {
        public string Name, Provider, Version, Class, DeviceId;
        public string[] HardwareIds = new string[0];
        public DateTime Date;
        public string State = "unknown"; // update, ok, none (sans source verifiee), error (verification impossible)
        public string NewVersion, Source, Url;
        public DateTime NewDate;
    }

    public static partial class Hardware
    {
        const string Catalog = "https://www.catalog.update.microsoft.com/";

        class Entry
        {
            public string Id, Version, Page, Provider;
            public DateTime Date;
            public HashSet<string> HardwareIds;
        }

        // ---------- pilotes installes ----------

        public static List<Driver> InstalledDrivers()
        {
            var ids = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in Query("SELECT PNPDeviceID, HardwareID, CompatibleID FROM Win32_PnPEntity"))
            {
                var h = (o["HardwareID"] as string[] ?? new string[0]).Concat(o["CompatibleID"] as string[] ?? new string[0]);
                ids[Str(o, "PNPDeviceID")] = h.ToArray();
            }
            var list = new List<Driver>();
            foreach (var o in Query("SELECT * FROM Win32_PnPSignedDriver"))
            {
                string provider = Str(o, "DriverProviderName"), name = Str(o, "DeviceName"), cls = Str(o, "DeviceClass");
                if (name.Length == 0 || (provider.Length == 0 && cls != "FIRMWARE")) continue;
                var d = DriverDay(o);
                var x = new Driver { Name = name, Provider = provider, Version = Str(o, "DriverVersion"), Class = cls, DeviceId = Str(o, "DeviceID"), Date = d };
                string[] hw;
                x.HardwareIds = ids.TryGetValue(x.DeviceId, out hw) && hw.Length > 0 ? hw : new[] { Str(o, "HardWareID") };
                // Les pilotes fournis par Microsoft sont mis a jour avec Windows.
                if (provider.StartsWith("Microsoft")) x.State = "windows";
                if (provider.StartsWith("NVIDIA") && cls == "DISPLAY") x.Version = NvidiaVersion(x.Version);
                if (!list.Any(y => y.Name == x.Name && y.Version == x.Version)) list.Add(x);
            }
            return list.OrderBy(x => x.Name).ToList();
        }

        // ---------- analyse ----------

        public static List<Driver> CheckDrivers(Action<int, int> progress)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 8;
            searches.Clear();
            var list = InstalledDrivers();
            int done = 0;
            var wu = Task.Run(() => WindowsUpdateDrivers());
            var todo = list.Where(x => x.State == "unknown").ToList();
            Parallel.ForEach(todo, new ParallelOptions { MaxDegreeOfParallelism = 3 }, d =>
            {
                try
                {
                    if (d.Class == "DISPLAY" && d.Provider.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) CheckNvidia(d);
                    if (d.State == "unknown") CheckCatalog(d);
                }
                catch { d.State = "error"; }
                progress(Interlocked.Increment(ref done), todo.Count);
            });

            // Windows Update a le dernier mot : il cible exactement ce PC.
            foreach (var u in wu.Result)
            {
                var d = list.FirstOrDefault(x => x.HardwareIds.Contains(u.HardwareIds[0], StringComparer.OrdinalIgnoreCase));
                if (d == null) { list.Add(u); continue; }
                if (u.NewDate <= d.Date) continue;
                if (d.State == "update" && d.NewDate >= u.NewDate) continue;
                d.State = "update"; d.NewVersion = u.NewVersion; d.NewDate = u.NewDate; d.Source = u.Source; d.Url = u.Url;
            }
            return list;
        }

        /// <summary>BIOS : le micrologiciel UEFI est publie comme un pilote dans le catalogue Microsoft.</summary>
        public static Driver CheckBios()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var d = InstalledDrivers().FirstOrDefault(x => x.Class == "FIRMWARE" &&
                        (x.Name.IndexOf("System", StringComparison.OrdinalIgnoreCase) >= 0 || x.Name.IndexOf("BIOS", StringComparison.OrdinalIgnoreCase) >= 0));
            if (d == null) return null;
            try { CheckCatalog(d); } catch { d.State = "error"; }
            return d;
        }

        // ---------- catalogue Microsoft Update ----------

        static readonly ConcurrentDictionary<string, Entry> entries = new ConcurrentDictionary<string, Entry>();
        static readonly ConcurrentDictionary<string, Lazy<string>> searches = new ConcurrentDictionary<string, Lazy<string>>();
        static readonly object pace = new object();
        static DateTime nextCall = DateTime.MinValue;

        /// <summary>
        /// Page du catalogue. Trop sollicite, le site repond 200 avec une page vide de resultats :
        /// sans l'un des reperes attendus, on patiente et on reessaie, puis on echoue plutot que de conclure a tort.
        /// </summary>
        static string CatalogPage(string path, params string[] markers)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                TimeSpan wait;
                lock (pace) // au plus quatre requetes par seconde, tous fils confondus
                {
                    var now = DateTime.UtcNow;
                    if (nextCall < now) nextCall = now;
                    wait = nextCall - now;
                    nextCall = nextCall.AddMilliseconds(250);
                }
                if (wait > TimeSpan.Zero) Thread.Sleep(wait);
                try
                {
                    string html = GetPage(Catalog + path);
                    if (markers.Any(m => html.Contains(m))) return html;
                }
                catch (WebException) { if (attempt == 4) throw; }
                Thread.Sleep(800 * (attempt + 1));
            }
            throw new WebException("Catalogue indisponible");
        }

        static void CheckCatalog(Driver d)
        {
            d.State = "none";
            string hw = d.HardwareIds.FirstOrDefault() ?? "";
            // Du plus precis au plus general ; la fiche doit de toute facon citer un identifiant exact de l'appareil.
            string[] patterns =
            {
                @"^PCI\\VEN_\w{4}&DEV_\w{4}&SUBSYS_\w{8}", @"^PCI\\VEN_\w{4}&DEV_\w{4}", @"^HDAUDIO\\FUNC_\w{2}&VEN_\w{4}&DEV_\w{4}&SUBSYS_\w{8}",
                @"^USB\\VID_\w{4}&PID_\w{4}", @"^ACPI\\VEN_\w+&DEV_\w+", @"^SWC\\[\w&]+", @"(?<=^UEFI\\RES_\{)[0-9a-f-]{36}"
            };
            foreach (var pattern in patterns)
            {
                var m = Regex.Match(hw, pattern, RegexOptions.IgnoreCase);
                if (m.Success && SearchCatalog(d, m.Value)) return;
            }
            string acpi = d.HardwareIds.FirstOrDefault(x => Regex.IsMatch(x, @"^ACPI\\[A-Z]{3,4}\w{4}$", RegexOptions.IgnoreCase));
            if (acpi != null) SearchCatalog(d, acpi);
        }

        static bool SearchCatalog(Driver d, string q)
        {
            string html = searches.GetOrAdd(q, x => new Lazy<string>(() =>
                CatalogPage("Search.aspx?q=" + Uri.EscapeDataString(x), "_R0\"", "noResultText"))).Value;
            var rows = new List<KeyValuePair<string, DateTime>>();
            foreach (Match r in Regex.Matches(html, "<tr id=\"([0-9a-f-]{36})_R\\d+\".*?</tr>", RegexOptions.Singleline))
            {
                var td = Regex.Matches(r.Value, "<td.*?</td>", RegexOptions.Singleline).Cast<Match>()
                    .Select(x => WebUtility.HtmlDecode(Regex.Replace(x.Value, "<[^>]+>", "")).Trim()).ToArray();
                DateTime date;
                if (td.Length < 6 || !ForThisWindows(td[2])) continue;
                if (!DateTime.TryParseExact(td[4], "M/d/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) continue;
                rows.Add(new KeyValuePair<string, DateTime>(r.Groups[1].Value, date));
            }

            // Les plus recentes d'abord ; la premiere fiche confirmee est la bonne.
            foreach (var row in rows.OrderByDescending(x => x.Value).Take(6))
            {
                var e = Detail(row.Key);
                if (e == null || !d.HardwareIds.Any(h => Specific(h) && e.HardwareIds.Contains(h)) || !SameMaker(e.Provider, d.Provider)) continue;
                d.NewVersion = e.Version;
                d.NewDate = e.Date;
                d.Url = e.Page;
                d.Source = "Catalogue Microsoft";
                d.State = e.Date > d.Date && Newer(e.Version, d.Version) ? "update" : "ok";
                return true;
            }
            return false;
        }

        // Identifiants generiques (PNP0C0A, classe PCI...) : partages par des appareils sans rapport.
        static bool Specific(string id)
        {
            return !Regex.IsMatch(id, @"^ACPI\\(VEN_)?PNP|&CC_|^\*", RegexOptions.IgnoreCase)
                && (!id.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase) || id.IndexOf("&DEV_", StringComparison.OrdinalIgnoreCase) > 0)
                && (!id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) || id.IndexOf("&PID_", StringComparison.OrdinalIgnoreCase) > 0);
        }

        // Un autre editeur numerote ses versions autrement : la comparaison n'aurait pas de sens.
        static bool SameMaker(string a, string b)
        {
            string x = Regex.Match(a, @"\w+").Value, y = Regex.Match(b, @"\w+").Value;
            return x.Length == 0 || y.Length == 0 || x.Equals(y, StringComparison.OrdinalIgnoreCase);
        }

        static Entry Detail(string id)
        {
            Entry e;
            if (entries.TryGetValue(id, out e)) return e;
            string path = "ScopedViewInline.aspx?updateid=" + id;
            string html = CatalogPage(path, "ScopedViewHandler_titleText");
            {
                string version = Regex.Match(html, "ScopedViewHandler_version\">([^<]+)<").Groups[1].Value.Trim();
                DateTime date;
                bool dated = DateTime.TryParseExact(Regex.Match(html, "ScopedViewHandler_versionDate\">([^<]+)<").Groups[1].Value.Trim(),
                    "M/d/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
                string arch = Regex.Match(html, "labelArchitecture_Separator\"[^>]*>[^<]*</span>\\s*([^<]+)<").Groups[1].Value.Trim();
                int i = html.IndexOf("id=\"driverhwIDs\"");
                string mine = Environment.Is64BitOperatingSystem ? "AMD64" : "X86";
                // Les extensions completent un pilote et ont leur propre numerotation.
                string cls = Regex.Match(html, "ScopedViewHandler_driverClass\">([^<]+)<").Groups[1].Value;
                if (cls.IndexOf("Extension", StringComparison.OrdinalIgnoreCase) >= 0) version = "";
                if (version.Length > 0 && dated && i > 0 && (arch.Length == 0 || arch.IndexOf(mine, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    string block = WebUtility.HtmlDecode(html.Substring(i, Math.Min(400000, html.Length - i)));
                    e = new Entry
                    {
                        Id = id, Version = version, Date = date, Page = Catalog + path,
                        Provider = WebUtility.HtmlDecode(Regex.Match(html, "ScopedViewHandler_driverProvider\">([^<]*)<").Groups[1].Value),
                        HardwareIds = new HashSet<string>(Regex.Matches(block, @"[A-Za-z]+\\[A-Za-z0-9_&{}\-\.\*]+").Cast<Match>().Select(x => x.Value), StringComparer.OrdinalIgnoreCase)
                    };
                }
            }
            entries[id] = e;
            return e;
        }

        static bool ForThisWindows(string products)
        {
            const string cv = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            int build;
            int.TryParse(Reg(cv, "CurrentBuildNumber"), out build);
            string mine = Reg(cv, "DisplayVersion");
            foreach (var part in Regex.Split(products, @",\s*(?=Windows)"))
            {
                string p = part.ToLowerInvariant();
                if (p.Contains("server")) continue;
                bool eleven = p.Contains("windows 11") || p.Contains("windows - client");
                if (eleven != (build >= 22000)) continue;
                if (!eleven && !p.Contains("windows 10")) continue;
                var v = Regex.Match(p, @"version (\d{2}h\d)");
                if (v.Success && string.CompareOrdinal(v.Groups[1].Value, mine.ToLowerInvariant()) > 0) continue;
                return true;
            }
            return false;
        }

        static bool Newer(string a, string b)
        {
            Version x, y;
            return Version.TryParse(a, out x) && Version.TryParse(b, out y) && x > y;
        }

        // ---------- Windows Update ----------

        static List<Driver> WindowsUpdateDrivers()
        {
            var list = new List<Driver>();
            try
            {
                dynamic session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session"));
                dynamic result = session.CreateUpdateSearcher().Search("IsInstalled=0 and Type='Driver'");
                foreach (dynamic u in result.Updates)
                {
                    string id = u.Identity.UpdateID;
                    Entry e = null;
                    try { e = Detail(id); } catch (WebException) { /* pas de fiche : pas de lien */ }
                    string title = u.Title;
                    list.Add(new Driver
                    {
                        Name = (string)u.DriverModel, Provider = (string)u.DriverManufacturer, Class = (string)u.DriverClass, Version = "",
                        HardwareIds = new[] { (string)u.DriverHardwareID }, State = "update", Source = "Windows Update",
                        NewDate = (DateTime)u.DriverVerDate,
                        NewVersion = e != null ? e.Version : Regex.Match(title, @"\d+(\.\d+){1,3}").Value,
                        Url = e != null ? e.Page : null
                    });
                }
            }
            catch { /* service desactive ou hors ligne : les autres sources restent utilisees */ }
            return list;
        }

        // ---------- pilote d'un appareil precis ----------

        static Lazy<Dictionary<string, ManagementObject>> signed;

        static void ResetDriverCache()
        {
            signed = new Lazy<Dictionary<string, ManagementObject>>(() =>
            {
                var d = new Dictionary<string, ManagementObject>(StringComparer.OrdinalIgnoreCase);
                foreach (var o in Query("SELECT DeviceID, DeviceClass, DriverVersion, DriverDate, DriverProviderName FROM Win32_PnPSignedDriver"))
                    d[Str(o, "DeviceID")] = o;
                return d;
            });
        }

        /// <summary>
        /// Date du pilote. Win32_PnPSignedDriver inverse le jour et le mois hors des Etats-Unis :
        /// la valeur exacte (FILETIME) est lue dans le registre.
        /// </summary>
        static DateTime DriverDay(ManagementObject o)
        {
            string key = Reg(@"SYSTEM\CurrentControlSet\Enum\" + Str(o, "DeviceID"), "Driver");
            var raw = key.Length == 0 ? null : RegRaw(@"SYSTEM\CurrentControlSet\Control\Class\" + key, "DriverDateData") as byte[];
            if (raw != null && raw.Length == 8)
            {
                long t = BitConverter.ToInt64(raw, 0);
                if (t > 0) return DateTime.FromFileTimeUtc(t).Date;
            }
            return WmiDay(Str(o, "DriverDate"));
        }

        /// <summary>Ajoute version, date et fournisseur du pilote de l'appareil.</summary>
        static Group WithDriver(this Group g, string pnp)
        {
            ManagementObject o;
            if (pnp.Length == 0 || !signed.Value.TryGetValue(pnp, out o)) return g;
            string v = Str(o, "DriverVersion"), p = Str(o, "DriverProviderName");
            if (p.StartsWith("NVIDIA") && Str(o, "DeviceClass") == "DISPLAY") v = NvidiaVersion(v);
            return g.Info("Version du pilote", v).Info("Date du pilote", DriverDay(o) == DateTime.MinValue ? "" : DriverDay(o).ToString("yyyy-MM-dd")).Info("Éditeur du pilote", p);
        }

        // 32.0.15.8278 -> 582.78, le numero affiche par NVIDIA
        static string NvidiaVersion(string v)
        {
            string digits = v.Replace(".", "");
            return digits.Length < 5 ? v : digits.Substring(digits.Length - 5).Insert(3, ".");
        }

        // ---------- NVIDIA ----------

        static void CheckNvidia(Driver d)
        {
            string name = Regex.Replace(d.Name, "^NVIDIA ", "", RegexOptions.IgnoreCase).Trim();
            bool laptop = Query("SELECT BatteryStatus FROM Win32_Battery").Count > 0;
            const string api = "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=";
            var notebooks = new HashSet<string>(Regex.Matches(GetPage(api + "2"), @"<Name>([^<]*Notebooks[^<]*)</Name>\s*<Value>(\d+)</Value>").Cast<Match>().Select(m => m.Groups[2].Value));
            string series = null, product = null;
            foreach (Match m in Regex.Matches(GetPage(api + "3"), "ParentID=\"(\\d+)\"[^>]*>\\s*<Name>([^<]+)</Name>\\s*<Value>(\\d+)</Value>"))
            {
                if (!m.Groups[2].Value.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                if (notebooks.Contains(m.Groups[1].Value) != laptop) continue;
                series = m.Groups[1].Value; product = m.Groups[3].Value;
                break;
            }
            if (product == null) return;
            int build;
            int.TryParse(Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber"), out build);
            string json = GetPage("https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=" +
                series + "&pfid=" + product + "&osID=" + (build >= 22000 ? "135" : "57") + "&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1");
            string version = Regex.Match(json, "\"Version\"\\s*:\\s*\"([\\d.]+)\"").Groups[1].Value;
            string url = Regex.Match(json, "\"DetailsURL\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value.Replace("\\/", "/");
            string mine = d.Version;
            if (version.Length == 0) return;
            DateTime date;
            if (DateTime.TryParseExact(Regex.Match(json, "\"ReleaseDateTime\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value, "ddd MMM dd, yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) d.NewDate = date;
            d.NewVersion = version;
            d.Source = "NVIDIA";
            d.State = Newer(version, mine) ? "update" : "ok";
            try
            {
                // La page de details vient de l'API NVIDIA ; on verifie que le fichier de cette version est bien publie.
                string file = Regex.Match(json, "\"DownloadURL\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value.Replace("\\/", "/");
                var req = (HttpWebRequest)WebRequest.Create(file);
                req.Method = "HEAD";
                req.Timeout = 15000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                    if (resp.StatusCode == HttpStatusCode.OK && file.Contains(version) && url.StartsWith("https://www.nvidia.com/")) d.Url = url;
            }
            catch { }
        }

        static string GetPage(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0";
            req.KeepAlive = false; // le catalogue renvoie des pages vides quand une connexion est reutilisee
            req.Timeout = req.ReadWriteTimeout = 25000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
                return sr.ReadToEnd();
        }
    }
}
