// Collecte des identifiants materiels (lecture seule, aucun droit administrateur requis).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace HwidChecker
{
    public class Field
    {
        public string Label, Value;
        public string Help;      // explication affichee par le point d'information
        public bool IsId;        // donnee sensible : masquable
        public bool Volatile;    // change naturellement (IP, ARP...) : exclue de la comparaison
        public bool Generic;     // valeur bidon laissee par le fabricant
        public string Alert;     // anomalie a signaler
        public string Url;       // lien hypertexte a ouvrir au clic
        public string State;     // "on", "off" ou "warn" : pastille d'etat (section securite)
    }

    public class Group
    {
        public string Title, Sub;
        public List<Field> Fields = new List<Field>();

        public Group(string title) { Title = title; }
        public Group(string title, string sub) { Title = title; Sub = sub; }

        public Group Info(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                Fields.Add(new Field { Label = label, Value = value.Trim() });
            return this;
        }

        public Group Id(string label, string value) { return Id(label, value, null); }

        /// <summary>Identifiant : toujours affiche, meme quand le materiel ne le communique pas.</summary>
        public Group Id(string label, string value, string alert)
        {
            string v = value == null ? "" : value.Trim();
            Fields.Add(new Field
            {
                Label = label,
                Value = v.Length == 0 ? "Non communiqué" : v,
                IsId = v.Length > 0,
                Generic = v.Length > 0 && Hardware.IsPlaceholder(v),
                Alert = alert
            });
            return this;
        }

        public Group Status(string label, string state, string text)
        {
            Fields.Add(new Field { Label = label, Value = text, State = state });
            return this;
        }

        public Group Link(string label, string text, string url)
        {
            Fields.Add(new Field { Label = label, Value = text, Url = url });
            return this;
        }

        public Group Net(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                Fields.Add(new Field { Label = label, Value = value.Trim(), IsId = true, Volatile = true });
            return this;
        }
    }

    public class Section
    {
        public string Key, Title, Description, Icon, Color, Category;
        public string Empty = "Aucun matériel détecté.";
        public List<Group> Groups = new List<Group>();
    }

    public class Component
    {
        public string Name, Value;
        public bool Used;
    }

    public class Summary
    {
        public string Section, Label, Sub, Value;
        public string Help;
        public bool IsId;
    }

    public class Report
    {
        public List<Section> Sections = new List<Section>();
        public List<Component> Components = new List<Component>();
        public List<Summary> Summary = new List<Summary>();
        public string Fingerprint, PcName, Model;
        public bool IsLaptop;
        public DateTime Date;

        /// <summary>Identifiants stables, indexes par un chemin (pour la comparaison).</summary>
        public Dictionary<string, string> Flatten()
        {
            var d = new Dictionary<string, string>();
            foreach (var s in Sections)
                foreach (var g in s.Groups)
                    foreach (var f in g.Fields)
                    {
                        if (!f.IsId || f.Volatile) continue;
                        string path = s.Title + " › " + g.Title + " › " + f.Label, k = path;
                        for (int i = 2; d.ContainsKey(k); i++) k = path + " (" + i + ")";
                        d[k] = f.Value;
                    }
            return d;
        }
    }

    public static partial class Hardware
    {
        static readonly string[] Placeholders =
        {
            "to be filled by o.e.m.", "to be filled by o.e.m", "default string", "system serial number",
            "system product name", "base board serial number", "chassis serial number", "not specified",
            "not applicable", "none", "n/a", "na", "[n/a]", "unknown", "o.e.m.", "oem", "invalid", "empty",
            "123456789", "0123456789", "1234567890", "03000200-0400-0500-0006-000700080009"
        };

        public static bool IsPlaceholder(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return true;
            string s = v.Trim().ToLowerInvariant();
            if (Placeholders.Contains(s)) return true;
            // 0000..., FFFF..., y compris sous forme d'UUID
            string core = Regex.Replace(s, "[-: _.]", "");
            return core.Length == 0 || (core.Length > 1 && core.Distinct().Count() == 1 && "0f".IndexOf(core[0]) >= 0);
        }

        // ---------- aides ----------

        static List<ManagementObject> Query(string wql, string scope)
        {
            var list = new List<ManagementObject>();
            try
            {
                using (var s = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql)))
                {
                    s.Options.Timeout = TimeSpan.FromSeconds(15);
                    foreach (ManagementObject o in s.Get()) list.Add(o);
                }
            }
            catch { /* classe absente ou acces refuse : la section reste vide */ }
            return list;
        }

        static List<ManagementObject> Query(string wql) { return Query(wql, @"root\cimv2"); }
        static ManagementObject One(string wql) { return Query(wql).FirstOrDefault(); }

        static string Str(ManagementBaseObject o, string prop)
        {
            if (o == null) return "";
            try
            {
                object v = o[prop];
                return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture).Trim();
            }
            catch { return ""; }
        }

        static ulong Num(ManagementBaseObject o, string prop)
        {
            ulong n;
            return ulong.TryParse(Str(o, prop), out n) ? n : 0;
        }

        static object RegRaw(string path, string name)
        {
            try
            {
                using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var k = b.OpenSubKey(path))
                    return k == null ? null : k.GetValue(name);
            }
            catch { return null; }
        }

        static string Reg(string path, string name)
        {
            object v = RegRaw(path, name);
            return v == null ? "" : v.ToString();
        }

        static string Size(ulong bytes, bool binary)
        {
            if (bytes == 0) return "";
            double b = binary ? 1024.0 : 1000.0, v = bytes;
            string[] u = { "o", "Ko", "Mo", "Go", "To" };
            int i = 0;
            while (v >= b && i < u.Length - 1) { v /= b; i++; }
            return v.ToString(v >= 100 || Math.Abs(v - Math.Round(v)) < 0.05 ? "0" : "0.#", CultureInfo.GetCultureInfo("fr-FR")) + " " + u[i];
        }

        static string Mac(string raw)
        {
            string h = new string((raw ?? "").Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (h.Length != 12) return (raw ?? "").Trim();
            return string.Join(":", Enumerable.Range(0, 6).Select(i => h.Substring(i * 2, 2)));
        }

        static string Run(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
                };
                using (var p = Process.Start(psi))
                {
                    var read = p.StandardOutput.ReadToEndAsync();
                    if (!p.WaitForExit(6000)) { p.Kill(); return ""; }
                    return read.Result;
                }
            }
            catch { return ""; }
        }

        static string Clean(string v) { return IsPlaceholder(v) ? "" : v.Trim(); }

        static string Join(string sep, params string[] parts)
        {
            return string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        static string Sha(byte[] data)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("X2")));
        }

        // ---------- collecte ----------

        public static Report Collect()
        {
            var r = new Report { Date = DateTime.Now, PcName = Environment.MachineName };

            ResetDriverCache();
            var tasks = new Func<Section>[] { Board, Gpu, Cpu, Disks, Memory, Displays, Peripherals, MacAddresses, Ip, Arp, Security, WindowsInfo }
                .Select(f => Task.Run(() => Safe(f))).ToArray();
            Task.WaitAll(tasks);
            r.Sections.AddRange(tasks.Select(t => t.Result).Where(s => s != null));
            foreach (var s in r.Sections)
                foreach (var g in s.Groups)
                    foreach (var f in g.Fields)
                        f.Help = Help.For(s.Key, f.Label);

            var cs = One("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            string maker = Clean(Str(cs, "Manufacturer")), model = Clean(Str(cs, "Model"));
            r.Model = model.Length == 0 ? "PC assemblé"
                : maker.Length == 0 || model.IndexOf(maker, StringComparison.OrdinalIgnoreCase) >= 0 ? model : maker + " " + model;
            r.IsLaptop = Query("SELECT BatteryStatus FROM Win32_Battery").Count > 0;

            Fingerprint(r);
            Summarize(r);
            return r;
        }

        static Section Safe(Func<Section> f)
        {
            try { return f(); } catch { return null; }
        }

        static void Fingerprint(Report r)
        {
            string[,] parts =
            {
                { "UUID de la carte mère", Str(One("SELECT UUID FROM Win32_ComputerSystemProduct"), "UUID") },
                { "SN de la carte mère", Str(One("SELECT SerialNumber FROM Win32_BaseBoard"), "SerialNumber") },
                { "SN du BIOS", Str(One("SELECT SerialNumber FROM Win32_BIOS"), "SerialNumber") },
                { "ID du processeur", Str(One("SELECT ProcessorId FROM Win32_Processor"), "ProcessorId") },
                { "SN du disque système", Str(One("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index=" + SystemDiskIndex()), "SerialNumber") }
            };
            var sb = new StringBuilder();
            for (int i = 0; i < parts.GetLength(0); i++)
            {
                string v = parts[i, 1];
                bool ok = !IsPlaceholder(v);
                r.Components.Add(new Component { Name = parts[i, 0], Value = v.Length == 0 ? "Non communiqué" : v, Used = ok });
                sb.Append(ok ? v.ToUpperInvariant() : "-").Append('|');
            }
            r.Fingerprint = Sha(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        // Une ligne par identifiant principal pour la vue d'ensemble.
        static void Summarize(Report r)
        {
            string[,] wanted =
            {
                { "board", "UUID", "UUID (carte mère)" }, { "gpu", "UUID du GPU", "Carte graphique" },
                { "cpu", "ID du processeur", "Processeur" }, { "cpu", "ID unique du TPM", "TPM" },
                { "storage", "Numéro de série (SN)", "Stockage" }, { "memory", "Numéro de série (SN)", "RAM" },
                { "display", "Numéro de série (SN)", "Écran" }, { "mac", "Adresse MAC", "Adresse MAC" },
                { "ip", "Adresse IPv4 locale", "IP locale" }, { "ip", "Adresse IPv4 publique", "IP publique" }
            };
            for (int i = 0; i < wanted.GetLength(0); i++)
            {
                var s = r.Sections.FirstOrDefault(x => x.Key == wanted[i, 0]);
                if (s == null) continue;
                string label = wanted[i, 1];
                var hits = (from g in s.Groups
                            from f in g.Fields
                            where f.Label == label || (label == "UUID du GPU" && f.Label == "Identifiant PCI (PnP)")
                            select new { g, f }).ToList();
                var hit = hits.FirstOrDefault(h => h.f.Label == label && h.f.IsId && !h.f.Generic) ?? hits.FirstOrDefault();
                if (hit == null) continue;
                r.Summary.Add(new Summary
                {
                    Section = s.Key, Label = wanted[i, 2], Value = hit.f.Value, IsId = hit.f.IsId, Help = hit.f.Help,
                    Sub = hit.g.Sub == null ? hit.g.Title : hit.g.Title + " · " + hit.g.Sub
                });
            }
        }

        static string SystemDiskIndex()
        {
            string drive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").TrimEnd('\\');
            string i = Str(One("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + drive + "'} WHERE AssocClass=Win32_LogicalDiskToPartition"), "DiskIndex");
            return i.Length == 0 ? "0" : i;
        }

        /// <summary>Jour inscrit dans une date WMI (aaaammjj...), sans conversion de fuseau.</summary>
        static DateTime WmiDay(string raw)
        {
            DateTime d;
            return raw.Length >= 8 && DateTime.TryParseExact(raw.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                ? d : DateTime.MinValue;
        }

        static string WmiDate(string raw)
        {
            var d = WmiDay(raw);
            return d == DateTime.MinValue ? "" : d.ToString("yyyy-MM-dd");
        }

        // ---------- carte mere ----------

        static Section Board()
        {
            var s = new Section { Key = "board", Category = "Matériel", Title = "Carte mère (UUID)", Icon = "board", Color = "#34C759",
                Description = "UUID SMBIOS et numéros de série gravés dans la carte mère, le BIOS et le châssis." };
            var p = One("SELECT * FROM Win32_ComputerSystemProduct");
            var b = One("SELECT * FROM Win32_BaseBoard");
            var i = One("SELECT * FROM Win32_BIOS");
            var e = One("SELECT * FROM Win32_SystemEnclosure");
            var c = One("SELECT * FROM Win32_ComputerSystem");
            s.Groups.Add(new Group("Carte mère", Join(" ", Clean(Str(b, "Manufacturer")), Clean(Str(b, "Product"))))
                .Id("UUID", Str(p, "UUID"))
                .Id("Numéro de série (SN)", Str(b, "SerialNumber"))
                .Info("Marque", Str(b, "Manufacturer"))
                .Info("Modèle", Str(b, "Product"))
                .Info("Révision", Str(b, "Version")));
            s.Groups.Add(new Group("Ordinateur", Clean(Str(c, "Model")))
                .Id("Numéro de série (SN)", Str(p, "IdentifyingNumber"))
                .Info("Marque", Str(c, "Manufacturer"))
                .Info("Modèle", Str(c, "Model"))
                .Info("Référence (SKU)", Str(c, "SystemSKUNumber")));
            s.Groups.Add(new Group("BIOS / UEFI", Clean(Str(i, "Manufacturer")))
                .Id("Numéro de série (SN)", Str(i, "SerialNumber"))
                .Info("Marque", Str(i, "Manufacturer"))
                .Info("Version du BIOS", Str(i, "SMBIOSBIOSVersion"))
                .Info("Date du firmware actuel", WmiDate(Str(i, "ReleaseDate")))
                .Info("Dernière mise à jour", "Vérification en cours…")
                .Link("Support du fabricant", Vendors.SupportName(Maker(c, b)), Vendors.SupportUrl(Maker(c, b))));
            s.Groups[2].Fields.RemoveAll(f => f.Label == "Support du fabricant" && f.Url == null);
            s.Groups.Add(new Group("Châssis", Clean(Str(e, "Manufacturer")))
                .Id("Numéro de série (SN)", Str(e, "SerialNumber"))
                .Id("Étiquette d’inventaire", Str(e, "SMBIOSAssetTag"))
                .Info("Marque", Str(e, "Manufacturer")));
            return s;
        }

        // PC de marque : fabricant de l'ordinateur. PC assemble : fabricant de la carte mere.
        static string Maker(ManagementObject system, ManagementObject board)
        {
            string m = Clean(Str(system, "Manufacturer"));
            return Vendors.SupportUrl(m) != null ? m : Clean(Str(board, "Manufacturer"));
        }

        // ---------- GPU ----------

        static Section Gpu()
        {
            var s = new Section { Key = "gpu", Category = "Matériel", Title = "Carte graphique", Icon = "gpu", Color = "#FF2D55",
                Description = "UUID, numéro de série et identifiants PCI de chaque GPU." };

            // Une ligne par carte NVIDIA : nom, UUID, SN, VBIOS
            var nv = new List<string[]>();
            foreach (var line in Run(Environment.SystemDirectory + @"\nvidia-smi.exe", "--query-gpu=name,uuid,serial,vbios_version --format=csv,noheader").Split('\n'))
            {
                var p = line.Split(',').Select(x => x.Trim()).ToArray();
                if (p.Length == 4 && p[1].StartsWith("GPU-")) nv.Add(p);
            }

            foreach (var o in Query("SELECT * FROM Win32_VideoController"))
            {
                string name = Str(o, "Name"), pnp = Str(o, "PNPDeviceID");
                bool physical = pnp.StartsWith("PCI", StringComparison.OrdinalIgnoreCase);
                var sub = Regex.Match(pnp, "SUBSYS_[0-9A-F]{4}([0-9A-F]{4})", RegexOptions.IgnoreCase);
                var ven = Regex.Match(pnp, "VEN_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                string brand = sub.Success ? Vendors.Pci(sub.Groups[1].Value) : "";
                string chip = ven.Success ? Vendors.Pci(ven.Groups[1].Value) : Str(o, "AdapterCompatibility");
                var match = nv.FirstOrDefault(x => name.IndexOf(x[0], StringComparison.OrdinalIgnoreCase) >= 0
                                                || x[0].IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                var g = new Group(name.Length == 0 ? "Carte graphique" : name, physical ? brand : "Adaptateur virtuel");
                if (match != null) g.Id("UUID du GPU", match[1]);
                if (physical) g.Id("Numéro de série (SN)", match == null || IsPlaceholder(match[2]) ? "" : match[2]);
                g.Id("Identifiant PCI (PnP)", pnp)
                 .Info("Marque", Join(" / puce ", brand, chip == brand ? "" : chip))
                 .Info("Modèle", name)
                 .Info("Mémoire vidéo", physical ? Size(Vram(name, Num(o, "AdapterRAM")), true) : "")
                 .Info("VBIOS", match == null ? "" : match[3])
                 .WithDriver(pnp);
                if (match != null) nv.Remove(match);
                if (physical) s.Groups.Insert(0, g); else s.Groups.Add(g);
            }
            return s;
        }

        // AdapterRAM plafonne a 4 Go : le registre contient la vraie valeur.
        static ulong Vram(string name, ulong fallback)
        {
            const string root = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            for (int i = 0; i < 16; i++)
            {
                string k = root + @"\" + i.ToString("0000");
                ulong n;
                if (Reg(k, "DriverDesc") == name && ulong.TryParse(Reg(k, "HardwareInformation.qwMemorySize"), out n) && n > 0) return n;
            }
            return fallback;
        }

        // ---------- CPU + TPM ----------

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        static extern int NCryptOpenStorageProvider(out IntPtr provider, string name, int flags);
        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        static extern int NCryptGetProperty(IntPtr obj, string prop, byte[] output, int size, out int result, int flags);
        [DllImport("ncrypt.dll")]
        static extern int NCryptFreeObject(IntPtr obj);

        static byte[] TpmProp(IntPtr provider, string prop)
        {
            int n;
            if (NCryptGetProperty(provider, prop, null, 0, out n, 0) != 0 || n <= 0) return null;
            var buf = new byte[n];
            return NCryptGetProperty(provider, prop, buf, n, out n, 0) == 0 ? buf : null;
        }

        static Section Cpu()
        {
            var s = new Section { Key = "cpu", Category = "Matériel", Title = "Processeur et TPM", Icon = "cpu", Color = "#007AFF",
                Description = "Identifiant CPUID du processeur et clé unique du module de sécurité TPM." };
            int n = 0;
            foreach (var o in Query("SELECT * FROM Win32_Processor"))
            {
                string maker = Str(o, "Manufacturer");
                maker = maker == "GenuineIntel" ? "Intel" : maker == "AuthenticAMD" ? "AMD" : maker;
                s.Groups.Add(new Group(n++ == 0 ? "Processeur" : "Processeur " + n, Str(o, "Name"))
                    .Id("ID du processeur", Str(o, "ProcessorId"))
                    .Id("Numéro de série (SN)", Str(o, "SerialNumber"))
                    .Info("Marque", maker)
                    .Info("Modèle", Str(o, "Name"))
                    .Info("Cœurs / threads", Str(o, "NumberOfCores") + " / " + Str(o, "NumberOfLogicalProcessors"))
                    .Info("Support (socket)", Str(o, "SocketDesignation"))
                    .Info("Signature", Str(o, "Caption")));
            }

            string version = TpmVersion(), ek = "", tpmMaker = "", firmware = "";
            try
            {
                IntPtr h;
                if (NCryptOpenStorageProvider(out h, "Microsoft Platform Crypto Provider", 0) == 0)
                {
                    var pub = TpmProp(h, "PCP_EKPUB");
                    if (pub != null) ek = Sha(pub);
                    var type = TpmProp(h, "PCP_PLATFORM_TYPE");
                    if (type != null)
                    {
                        string t = Encoding.Unicode.GetString(type);
                        tpmMaker = Vendors.Tpm(Regex.Match(t, @"VendorID:'?([A-Za-z]+)").Groups[1].Value);
                        firmware = Regex.Match(t, @"Firmware:\s*([0-9A-Fa-f.]+)").Groups[1].Value;
                    }
                    NCryptFreeObject(h);
                }
            }
            catch { }
            var tpm = new Group("TPM", version.Length == 0 ? "Non détecté" : Join(" ", tpmMaker, version));
            tpm.Id("ID unique du TPM", ek).Id("Numéro de série (SN)", "");
            if (version.Length == 0) tpm.Fields[0].Value = "Non détecté";
            s.Groups.Add(tpm
                .Info("Marque", tpmMaker)
                .Info("Version", version.Length == 0 ? "Non détecté" : version)
                .Info("Micrologiciel", firmware));
            return s;
        }

        // ---------- stockage ----------

        static Section Disks()
        {
            var s = new Section { Key = "storage", Category = "Matériel", Title = "Stockage (SSD, RAID)", Icon = "storage", Color = "#FF9500",
                Description = "Numéro de série et santé de chaque disque, état RAID et numéros de volume." };
            const string ns = @"root\Microsoft\Windows\Storage";
            var phys = Query("SELECT * FROM MSFT_PhysicalDisk", ns);
            string sys = SystemDiskIndex();
            bool raidBus = false;

            foreach (var o in Query("SELECT * FROM Win32_DiskDrive").OrderBy(x => Num(x, "Index")))
            {
                string index = Str(o, "Index"), model = Str(o, "Model");
                var ph = phys.FirstOrDefault(x => Str(x, "DeviceId") == index);
                ulong bus = Num(ph, "BusType"), media = Num(ph, "MediaType");
                raidBus |= bus == 8;
                string iface = bus == 17 ? "NVMe" : bus == 11 ? "SATA" : bus == 7 ? "USB" : bus == 8 ? "RAID"
                             : bus == 16 ? "Espaces de stockage" : bus == 10 ? "SAS" : bus == 12 ? "Carte SD" : Str(o, "InterfaceType");
                string type = bus == 7 ? "Disque externe" : media == 4 ? "SSD" : media == 3 ? "Disque dur (HDD)" : "Disque";
                string serial = Str(o, "SerialNumber"), real = Regex.Replace(Clean(Str(ph, "AdapterSerialNumber")), @"\s+_\d+$", ""); // suffixe = espace de noms NVMe
                var g = new Group(type + " " + index + (index == sys ? " (système)" : ""), model)
                    .Id("Numéro de série (SN)", real.Length > 0 ? real : serial);
                if (real.Length > 0 && real != serial) g.Id("Identifiant EUI-64", serial.TrimEnd('.'));
                else if (bus == 17 && Regex.IsMatch(serial, @"^[0-9A-F_]+\.$")) g.Fields[g.Fields.Count - 1].Value = serial.TrimEnd('.');
                s.Groups.Add(g
                    .Info("Marque", Vendors.Disk(model))
                    .Info("Modèle", model)
                    .Info("Type", type)
                    .Info("Interface", iface)
                    .Info("Capacité", Size(Num(o, "Size"), false))
                    .Info("Micrologiciel", Str(o, "FirmwareRevision")));
                AddHealth(g, index, Str(o, "PNPDeviceID"), Num(ph, "HealthStatus"), ph != null);
                g.WithDriver(Str(o, "PNPDeviceID"));
            }

            var pools = Query("SELECT * FROM MSFT_StoragePool WHERE IsPrimordial=FALSE", ns);
            var virt = Query("SELECT * FROM MSFT_VirtualDisk", ns);
            var raid = new Group("RAID", raidBus || virt.Count > 0 ? "Grappe détectée" : "Aucune grappe détectée")
                .Info("État", raidBus ? "Disques en mode RAID matériel" : virt.Count > 0 ? "Espaces de stockage Windows (RAID logiciel)" : "Aucun disque en RAID");
            foreach (var o in Query("SELECT * FROM Win32_SCSIController"))
                raid.Info("Contrôleur", Join(", ", Str(o, "Name"), Clean(Str(o, "Manufacturer")))).WithDriver(Str(o, "PNPDeviceID"));
            foreach (var o in pools) raid.Info("Pool", Str(o, "FriendlyName"));
            foreach (var o in virt)
                raid.Info("Niveau", Join(" ", Str(o, "FriendlyName"), Str(o, "ResiliencySettingName")))
                    .Id("UUID de la grappe", Str(o, "UniqueId"));
            s.Groups.Add(raid);

            var v = new Group("Volumes", "Numéros attribués au formatage");
            foreach (var o in Query("SELECT DeviceID, VolumeSerialNumber FROM Win32_LogicalDisk WHERE DriveType=3"))
            {
                string n = Str(o, "VolumeSerialNumber");
                if (n.Length == 8) n = n.Substring(0, 4) + "-" + n.Substring(4);
                v.Id("Volume " + Str(o, "DeviceID"), n);
            }
            if (v.Fields.Count > 0) s.Groups.Add(v);
            return s;
        }

        // ---------- RAM ----------

        static Section Memory()
        {
            var s = new Section { Key = "memory", Category = "Matériel", Title = "Mémoire (RAM)", Icon = "memory", Color = "#AF52DE",
                Description = "Marque, référence et numéro de série de chaque barrette." };
            foreach (var o in Query("SELECT * FROM Win32_PhysicalMemory"))
            {
                ulong t = Num(o, "SMBIOSMemoryType");
                string type = t == 20 ? "DDR" : t == 21 ? "DDR2" : t == 24 ? "DDR3" : t == 26 ? "DDR4"
                            : t == 30 ? "LPDDR4" : t == 34 ? "DDR5" : t == 35 ? "LPDDR5" : "";
                string speed = Str(o, "ConfiguredClockSpeed"), slot = Str(o, "DeviceLocator");
                string brand = Vendors.Ram(Str(o, "Manufacturer")), part = Str(o, "PartNumber");
                string size = Join(" ", Size(Num(o, "Capacity"), true), type);
                s.Groups.Add(new Group(slot.Length == 0 ? "Barrette " + (s.Groups.Count + 1) : slot, Join(" ", brand, size))
                    .Id("Numéro de série (SN)", Str(o, "SerialNumber"))
                    .Info("Marque", brand)
                    .Info("Modèle", part)
                    .Info("Capacité", size)
                    .Info("Fréquence", speed.Length == 0 || speed == "0" ? "" : speed + " MT/s")
                    .Info("Emplacement", Join(", ", slot, Str(o, "BankLabel"))));
            }
            return s;
        }

        // ---------- ecrans (EDID lu directement) ----------

        static Section Displays()
        {
            var s = new Section { Key = "display", Category = "Matériel", Title = "Écrans", Icon = "display", Color = "#5856D6",
                Description = "Informations EDID transmises par chaque écran connecté.",
                Empty = "Aucun écran ne transmet d’informations EDID." };
            foreach (var o in Query("SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass='Monitor'"))
            {
                string pnp = Str(o, "PNPDeviceID");
                var e = RegRaw(@"SYSTEM\CurrentControlSet\Enum\" + pnp + @"\Device Parameters", "EDID") as byte[];
                if (e == null || e.Length < 128)
                {
                    s.Groups.Add(new Group(Str(o, "Name"), "Sans EDID")
                        .Id("Numéro de série (SN)", "").Id("Identifiant PnP", pnp));
                    continue;
                }
                int m = (e[8] << 8) | e[9];
                string code = new string(new[] { (char)(64 + ((m >> 10) & 31)), (char)(64 + ((m >> 5) & 31)), (char)(64 + (m & 31)) });
                string brand = Vendors.Monitor(code), name = "", sn = "", text = "";
                for (int d = 54; d <= 108; d += 18)
                {
                    if (e[d] != 0 || e[d + 1] != 0) continue;
                    string v = Encoding.ASCII.GetString(e, d + 5, 13).Split('\n')[0].Trim('\0', ' ', '\r');
                    if (e[d + 3] == 0xFC) name = v;
                    else if (e[d + 3] == 0xFF) sn = v;
                    else if (e[d + 3] == 0xFE && text.Length == 0) text = v;
                }
                if (name.Length == 0) name = text;
                uint num = BitConverter.ToUInt32(e, 12);
                string res = e[54] == 0 && e[55] == 0 ? "" : (e[56] | ((e[58] & 0xF0) << 4)) + " × " + (e[59] | ((e[61] & 0xF0) << 4));
                string diag = e[21] == 0 || e[22] == 0 ? "" :
                    (Math.Sqrt(e[21] * e[21] + e[22] * e[22]) / 2.54).ToString("0.#", CultureInfo.GetCultureInfo("fr-FR")) + " pouces";
                s.Groups.Add(new Group(name.Length == 0 ? "Écran " + (s.Groups.Count + 1) : name, brand)
                    .Id("Numéro de série (SN)", sn.Length > 0 ? sn : num != 0 ? num.ToString() : "")
                    .Id("Identifiant PnP", pnp)
                    .Info("Marque", brand)
                    .Info("Modèle", Join(" ", name, "(" + code + BitConverter.ToUInt16(e, 10).ToString("X4") + ")"))
                    .Info("Définition native", res)
                    .Info("Diagonale", diag)
                    .Info("Année de fabrication", e[17] == 0 ? "" : (1990 + e[17]).ToString())
                    .WithDriver(pnp));
            }
            return s;
        }

        // ---------- peripheriques externes ----------

        static Section Peripherals()
        {
            var s = new Section { Key = "usb", Category = "Matériel", Title = "Périphériques externes", Icon = "usb", Color = "#FF6482",
                Description = "Périphériques USB et Bluetooth, avec leur marque et leur numéro de série.",
                Empty = "Aucun périphérique USB ou Bluetooth détecté." };
            const string fixedContainer = "{00000000-0000-0000-ffff-ffffffffffff}";
            var inner = new List<Group>();
            var bt = new List<Group>();

            foreach (var o in Query("SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB\\\\VID%'"))
            {
                string pnp = Str(o, "PNPDeviceID"), service = Str(o, "Service");
                if (pnp.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) >= 0 || service.StartsWith("usbhub", StringComparison.OrdinalIgnoreCase)) continue;
                var id = Regex.Match(pnp, @"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})[^\\]*\\(.+)$", RegexOptions.IgnoreCase);
                if (!id.Success) continue;
                string brand = Vendors.Usb(id.Groups[1].Value), tail = id.Groups[3].Value;
                string reported = BusName(o), name = reported.Length > 0 ? reported : Str(o, "Name");
                bool isInner = Reg(@"SYSTEM\CurrentControlSet\Enum\" + pnp, "ContainerID").Equals(fixedContainer, StringComparison.OrdinalIgnoreCase);
                if (brand.Length == 0) brand = GenericMaker(Str(o, "Manufacturer"));
                var g = new Group(name, Join(" · ", brand, isInner ? "Intégré" : "USB"))
                    .Id("Numéro de série (SN)", tail.IndexOf('&') >= 0 ? "" : tail)
                    .Id("Identifiant USB (PnP)", pnp)
                    .Info("Marque", brand)
                    .Info("Modèle", name)
                    .Info("VID / PID", id.Groups[1].Value.ToUpperInvariant() + " / " + id.Groups[2].Value.ToUpperInvariant())
                    .Info("Connexion", isInner ? "USB interne" : "USB externe")
                    .WithDriver(pnp);
                if (isInner) inner.Add(g); else s.Groups.Add(g);
            }

            foreach (var o in Query("SELECT Name, PNPDeviceID, Manufacturer FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'BTHENUM\\\\DEV%' OR PNPDeviceID LIKE 'BTHLE\\\\DEV%'"))
            {
                var m = Regex.Match(Str(o, "PNPDeviceID"), @"DEV_([0-9A-F]{12})", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                string mac = Mac(m.Groups[1].Value);
                if (bt.Any(x => x.Fields.Any(f => f.Value == mac))) continue;
                bt.Add(new Group(Str(o, "Name"), "Bluetooth")
                    .Id("Adresse Bluetooth", mac)
                    .Id("Numéro de série (SN)", "")
                    .Info("Modèle", Str(o, "Name"))
                    .Info("Connexion", "Bluetooth (appairé)"));
            }
            s.Groups.AddRange(bt);
            s.Groups.AddRange(inner);
            return s;
        }

        static string GenericMaker(string m)
        {
            return m.StartsWith("(") || IsPlaceholder(m) ? "" : m;
        }

        // Nom annonce par le peripherique lui-meme, plus precis que le nom du pilote.
        static string BusName(ManagementObject o)
        {
            try
            {
                var args = new object[] { new[] { "{540b947e-8b40-45bc-a8a2-6a0b894cbda2} 4" }, null };
                o.InvokeMethod("GetDeviceProperties", args);
                var p = ((ManagementBaseObject[])args[1])[0];
                return Str(p, "Data");
            }
            catch { return ""; }
        }

        // ---------- reseau ----------

        static Section MacAddresses()
        {
            var s = new Section { Key = "mac", Category = "Réseau", Title = "Adresses MAC", Icon = "network", Color = "#32ADE6",
                Description = "Adresse actuelle de chaque carte, comparée à son adresse d’usine.",
                Empty = "Aucune carte réseau détectée." };

            var perm = new Dictionary<string, ManagementObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in Query("SELECT * FROM MSFT_NetAdapter", @"root\StandardCimv2"))
                perm[Str(o, "InstanceID")] = o;

            foreach (var o in Query("SELECT * FROM Win32_NetworkAdapter WHERE MACAddress IS NOT NULL")
                              .OrderByDescending(x => Str(x, "PhysicalAdapter") == "True" && Str(x, "PNPDeviceID").StartsWith("PCI")))
            {
                if (Str(o, "NetConnectionID").Length == 0) continue; // pseudo-cartes WAN Miniport
                ManagementObject na;
                perm.TryGetValue(Str(o, "GUID"), out na);
                string mac = Mac(Str(o, "MACAddress")), factory = Mac(Str(na, "PermanentAddress"));
                bool known = !IsPlaceholder(factory);
                bool changed = known && factory != mac;
                string pnp = Str(o, "PNPDeviceID");
                var g = new Group(Str(o, "NetConnectionID"), Str(o, "Name"))
                    .Id("Adresse MAC", mac, changed ? "Différente de l’adresse d’usine" : null);
                if (known) g.Id("Adresse MAC d’usine", factory);
                s.Groups.Add(g
                    .Id("Numéro de série (SN)", "")
                    .Info("Marque", GenericMaker(Str(o, "Manufacturer")))
                    .Info("Modèle", Str(o, "Name"))
                    .Info("Type", pnp.StartsWith("PCI") ? "Carte physique" : pnp.StartsWith("USB") ? "Carte USB"
                                : pnp.StartsWith("BTH") ? "Bluetooth" : "Carte virtuelle")
                    .Info("État", Str(o, "NetConnectionStatus") == "2" ? "Connectée" : "Déconnectée")
                    .WithDriver(pnp));
            }
            return s;
        }

        static IEnumerable<NetworkInterface> UpInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !a.Address.Equals(IPAddress.Any)));
        }

        static string Download(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = req.ReadWriteTimeout = 5000;
                using (var resp = req.GetResponse())
                using (var sr = new System.IO.StreamReader(resp.GetResponseStream()))
                {
                    IPAddress ip;
                    string t = sr.ReadToEnd().Trim();
                    return IPAddress.TryParse(t, out ip) ? t : "";
                }
            }
            catch { return ""; }
        }

        static Section Ip()
        {
            var s = new Section { Key = "ip", Category = "Réseau", Title = "Adresses IP", Icon = "globe", Color = "#0A84FF",
                Description = "Adresses locales de chaque interface et adresse publique vue sur Internet." };

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var t4 = Task.Run(() => Download("https://api.ipify.org"));
            var t6 = Task.Run(() => Download("https://api6.ipify.org"));
            Task.WaitAll(t4, t6);
            var pub = new Group("IP publique", "Vue depuis Internet")
                .Net("Adresse IPv4 publique", t4.Result).Net("Adresse IPv6 publique", t6.Result);
            if (pub.Fields.Count == 0) pub.Info("Adresse publique", "Indisponible (hors ligne)");
            s.Groups.Add(pub);

            foreach (var n in UpInterfaces())
            {
                var p = n.GetIPProperties();
                var g = new Group("IP locale : " + n.Name, n.Description);
                foreach (var a in p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
                    g.Net("Adresse IPv4 locale", a.Address.ToString()).Info("Masque de sous-réseau", a.IPv4Mask.ToString());
                foreach (var a in p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6))
                    g.Net(a.Address.IsIPv6LinkLocal ? "Adresse IPv6 de liaison" : "Adresse IPv6", a.Address.ToString().Split('%')[0]);
                foreach (var a in p.GatewayAddresses) g.Net("Passerelle", a.Address.ToString().Split('%')[0]);
                g.Net("Serveur DHCP", string.Join(", ", p.DhcpServerAddresses));
                g.Net("Serveurs DNS", string.Join(", ", p.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork)));
                if (g.Fields.Count > 0) s.Groups.Add(g);
            }
            return s;
        }

        static Section Arp()
        {
            var s = new Section { Key = "arp", Category = "Réseau", Title = "Table ARP", Icon = "arp", Color = "#30B0C7",
                Description = "Appareils du réseau local récemment vus par ce PC : adresse IP et adresse MAC.",
                Empty = "La table ARP est vide." };
            var gateways = new HashSet<string>(UpInterfaces().SelectMany(n => n.GetIPProperties().GatewayAddresses).Select(a => a.Address.ToString()));
            var rows = Query("SELECT * FROM MSFT_NetNeighbor WHERE AddressFamily=2", @"root\StandardCimv2")
                .Select(o => new { If = Str(o, "InterfaceAlias"), Ip = Str(o, "IPAddress"), Mac = Mac(Str(o, "LinkLayerAddress")) })
                .Where(x =>
                {
                    IPAddress a;
                    if (!IPAddress.TryParse(x.Ip, out a) || IsPlaceholder(x.Mac) || x.Mac.Length != 17) return false;
                    byte first = a.GetAddressBytes()[0];
                    return first < 224 && first != 127; // ni multicast, ni diffusion, ni boucle locale
                })
                .GroupBy(x => x.If);
            foreach (var it in rows)
            {
                var list = it.OrderBy(x => BitConverter.ToUInt32(IPAddress.Parse(x.Ip).GetAddressBytes().Reverse().ToArray(), 0)).ToList();
                var g = new Group(it.Key.Length == 0 ? "Interface" : it.Key, list.Count + (list.Count > 1 ? " appareils" : " appareil"));
                foreach (var x in list)
                {
                    g.Net(x.Ip, x.Mac);
                    string note = Vendors.Mac(x.Mac);
                    if (gateways.Contains(x.Ip)) note = Join(", ", "Passerelle", note);
                    g.Fields[g.Fields.Count - 1].Label = note.Length == 0 ? x.Ip : x.Ip + " · " + note;
                }
                s.Groups.Add(g);
            }
            return s;
        }

        // ---------- Windows ----------

        static Section WindowsInfo()
        {
            var s = new Section { Key = "windows", Category = "Système", Title = "Windows", Icon = "windows", Color = "#8E8E93",
                Description = "Identifiants logiciels, régénérés lors d’une réinstallation de Windows." };
            var o = One("SELECT * FROM Win32_OperatingSystem");
            const string cv = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string ubr = Reg(cv, "UBR"), sid = "";
            try { using (var w = WindowsIdentity.GetCurrent()) sid = w.User.Value; } catch { }
            var lic = One("SELECT Description, LicenseStatus, ProductKeyChannel FROM SoftwareLicensingProduct " +
                          "WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL");
            string ch = Str(lic, "ProductKeyChannel"), st = Str(lic, "LicenseStatus");
            string type = ch.StartsWith("OEM:DM") ? "OEM (clé numérique dans le BIOS)" : ch.StartsWith("OEM") ? "OEM (" + ch + ")"
                        : ch.StartsWith("Retail") ? "Retail (vente au détail)" : ch.StartsWith("Volume:MAK") ? "Volume (MAK)"
                        : ch.StartsWith("Volume:GVLK") ? "Volume (KMS)" : ch.Length > 0 ? ch : "Inconnu";
            string active = st == "1" ? "Activé" : st == "2" || st == "3" || st == "6" ? "Période de grâce"
                          : st == "5" ? "Mode notification (non activé)" : "Non activé";
            s.Groups.Add(new Group("Système d’exploitation", Str(o, "Caption"))
                .Info("Version de Windows", Join(" ", Str(o, "Caption").Replace("Microsoft ", ""), Reg(cv, "DisplayVersion")))
                .Info("Build", Str(o, "BuildNumber") + (ubr.Length > 0 ? "." + ubr : ""))
                .Info("Type de licence", type)
                .Status("Activation", st == "1" ? "on" : "warn", active)
                .Info("Installé le", WmiDate(Str(o, "InstallDate"))));
            s.Groups.Add(new Group("Identifiants", "Propres à cette installation")
                .Id("MachineGuid", Reg(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid"))
                .Id("ID de produit (SN Windows)", Str(o, "SerialNumber"))
                .Id("Profil matériel (GUID)", Reg(@"SYSTEM\CurrentControlSet\Control\IDConfigDB\Hardware Profiles\0001", "HwProfileGuid"))
                .Id("Identifiant SQM", Reg(@"SOFTWARE\Microsoft\SQMClient", "MachineId"))
                .Id("SID de l’utilisateur", sid)
                .Id("Nom de l’ordinateur", Environment.MachineName));
            return s;
        }
    }
}
