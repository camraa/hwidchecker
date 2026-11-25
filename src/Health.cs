// Sante des disques (journal NVMe, SMART) et base IEEE des fabricants de cartes reseau.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace HwidChecker
{
    public static partial class Hardware
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint create, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] input, int inLen, byte[] output, int outLen, out int returned, IntPtr overlapped);

        /// <summary>Journal SMART / Health (page 2) d'un disque NVMe. Lecture seule, sans droits particuliers.</summary>
        static byte[] NvmeLog(string index)
        {
            using (var h = CreateFile(@"\\.\PhysicalDrive" + index, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return null;
                foreach (int property in new[] { 49, 50 }) // specifique a l'adaptateur, puis au peripherique
                {
                    var b = new byte[8 + 40 + 512];
                    BitConverter.GetBytes(property).CopyTo(b, 0);
                    int[] v = { 3 /* NVMe */, 2 /* page de journal */, 2, 0, 40, 512, 0, 0, 0, 0 };
                    for (int i = 0; i < v.Length; i++) BitConverter.GetBytes(v[i]).CopyTo(b, 8 + i * 4);
                    var o = new byte[b.Length];
                    int n;
                    if (DeviceIoControl(h, 0x2D1400, b, b.Length, o, o.Length, out n, IntPtr.Zero) && n >= o.Length)
                        return o.Skip(48).ToArray();
                }
            }
            return null;
        }

        static string Hours(ulong h)
        {
            return h == 0 ? "" : h + " h (" + (h / 24) + " jours)";
        }

        /// <summary>Ajoute l'etat de sante d'un disque a son groupe.</summary>
        static void AddHealth(Group g, string index, string pnp, ulong windowsHealth, bool known)
        {
            var log = NvmeLog(index);
            if (log != null)
            {
                int used = log[5], spare = log[3], threshold = log[4];
                ulong errors = BitConverter.ToUInt64(log, 160);
                int health = Math.Max(0, 100 - used);
                bool bad = log[0] != 0 || spare < threshold || used >= 100, warn = used >= 90 || errors > 0;
                g.Status("Santé", bad ? "off" : warn ? "warn" : "on",
                    (bad ? "À remplacer" : warn ? "À surveiller" : "Bon état") + " (" + health + " %)");
                int kelvin = BitConverter.ToUInt16(log, 1);
                g.Info("Température", kelvin > 200 ? (kelvin - 273) + " °C" : "")
                 .Info("Usure", used + " %")
                 .Info("Réserve disponible", spare + " %")
                 .Info("Heures de fonctionnement", Hours(BitConverter.ToUInt64(log, 128)))
                 .Info("Démarrages", BitConverter.ToUInt64(log, 112).ToString())
                 .Info("Arrêts non sécurisés", BitConverter.ToUInt64(log, 144).ToString())
                 .Info("Données écrites", Size(BitConverter.ToUInt64(log, 48) * 512000UL, false))
                 .Info("Erreurs de support", errors.ToString());
                return;
            }

            // SATA : attributs SMART bruts, reserves aux administrateurs.
            var smart = SmartAttributes(pnp);
            var rel = Query("SELECT * FROM MSFT_StorageReliabilityCounter WHERE DeviceId='" + index + "'", @"root\Microsoft\Windows\Storage").FirstOrDefault();
            bool? predicted = SmartPredictsFailure(pnp);
            ulong realloc = At(smart, 5), pending = At(smart, 197), lost = At(smart, 198);
            if (predicted == true || (known && windowsHealth == 2)) g.Status("Santé", "off", "À remplacer");
            else if (realloc + pending + lost > 0 || (known && windowsHealth == 1)) g.Status("Santé", "warn", "À surveiller");
            else if (smart.Count > 0) g.Status("Santé", "on", "Bon état");
            else if (known && windowsHealth == 0) g.Status("Santé", "on", "Bon état selon Windows");
            else g.Info("Santé", "Non communiquée");

            ulong temp = smart.ContainsKey(194) ? smart[194] & 0xFF : At(smart, 190) & 0xFF;
            if (temp == 0) temp = Num(rel, "Temperature");
            ulong poh = At(smart, 9) & 0xFFFFFF;
            if (poh == 0) poh = Num(rel, "PowerOnHours");
            g.Info("Température", temp > 0 && temp < 120 ? temp + " °C" : "");
            if (Num(rel, "Wear") > 0) g.Info("Usure", Num(rel, "Wear") + " %");
            g.Info("Heures de fonctionnement", Hours(poh));
            if (smart.Count > 0)
                g.Info("Démarrages", At(smart, 12).ToString())
                 .Info("Secteurs réalloués", realloc.ToString())
                 .Info("Secteurs en attente", pending.ToString())
                 .Info("Secteurs illisibles", lost.ToString());
        }

        static ulong At(Dictionary<byte, ulong> d, byte id)
        {
            ulong v;
            return d.TryGetValue(id, out v) ? v : 0;
        }

        static bool Same(string instance, string pnp)
        {
            return pnp.Length > 0 && instance.StartsWith(pnp, StringComparison.OrdinalIgnoreCase);
        }

        static Dictionary<byte, ulong> SmartAttributes(string pnp)
        {
            var d = new Dictionary<byte, ulong>();
            var o = Query("SELECT InstanceName, VendorSpecific FROM MSStorageDriver_FailurePredictData", @"root\wmi")
                .FirstOrDefault(x => Same(Str(x, "InstanceName"), pnp));
            var raw = o == null ? null : o["VendorSpecific"] as byte[];
            if (raw == null) return d;
            for (int i = 2; i + 12 <= raw.Length && i < 362; i += 12)
                if (raw[i] != 0) d[raw[i]] = BitConverter.ToUInt32(raw, i + 5) | ((ulong)BitConverter.ToUInt16(raw, i + 9) << 32);
            // Seuls les 32 bits bas comptent pour les compteurs de secteurs.
            foreach (byte id in new byte[] { 5, 12, 197, 198 }) if (d.ContainsKey(id)) d[id] &= 0xFFFFFFFF;
            return d;
        }

        static bool? SmartPredictsFailure(string pnp)
        {
            var o = Query("SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus", @"root\wmi")
                .FirstOrDefault(x => Same(Str(x, "InstanceName"), pnp));
            return o == null ? (bool?)null : Str(o, "PredictFailure") == "True";
        }
    }

    /// <summary>Registre public IEEE : les trois premiers octets d'une adresse MAC designent son fabricant.</summary>
    public static class Oui
    {
        const string Source = "https://standards-oui.ieee.org/oui/oui.csv";
        static Dictionary<string, string> table;

        public static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HWID Checker", "oui.csv"); }
        }

        public static bool Available { get { return File.Exists(FilePath); } }

        public static Dictionary<string, string> Parse(string csv)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Registry,Assignment,Organization Name,Organization Address
            foreach (Match m in Regex.Matches(csv, "^MA-L,([0-9A-F]{6}),(?:\"((?:[^\"]|\"\")*)\"|([^,\\r\\n]*))", RegexOptions.Multiline))
                d[m.Groups[1].Value] = (m.Groups[2].Success ? m.Groups[2].Value.Replace("\"\"", "\"") : m.Groups[3].Value).Trim();
            return d;
        }

        public static string Lookup(string mac)
        {
            if (table == null)
            {
                try { table = Available ? Parse(File.ReadAllText(FilePath)) : new Dictionary<string, string>(); }
                catch { table = new Dictionary<string, string>(); }
            }
            string v, key = (mac ?? "").Replace(":", "");
            return key.Length >= 6 && table.TryGetValue(key.Substring(0, 6), out v) ? Short(v) : "";
        }

        // "SAGEMCOM BROADBAND SAS" -> "Sagemcom Broadband"
        static string Short(string v)
        {
            v = Regex.Replace(v, @"[,.]?\s*\b(Inc|Co|Ltd|LLC|Corp|Corporation|Corporate|GmbH|SAS|S\.?A\.?|AG|Limited|Company|Technologies|Technology|Electronics)\b\.?", "", RegexOptions.IgnoreCase);
            v = Regex.Replace(v, @"[,.\s]+$", "").Replace(" ,", ",").Trim(' ', ',');
            if (v.Length > 4 && v == v.ToUpperInvariant())
                v = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(v.ToLowerInvariant());
            // La virgule et le point median servent de separateurs a l'affichage.
            return v.Replace(",", "");
        }

        public static bool Download()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(Source);
                req.UserAgent = "Mozilla/5.0";
                req.Timeout = req.ReadWriteTimeout = 60000;
                string csv;
                using (var resp = req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                    csv = sr.ReadToEnd();
                var d = Parse(csv);
                if (d.Count < 10000) return false; // fichier incomplet ou page d'erreur
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, csv);
                table = d;
                return true;
            }
            catch { return false; }
        }
    }
}
