// Etat des protections : TPM, Secure Boot, IOMMU, HVCI, hyperviseur, Windows Defender.
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HwidChecker
{
    public static partial class Hardware
    {
        [StructLayout(LayoutKind.Sequential)]
        struct TpmInfo { public uint structVersion, tpmVersion, tpmInterfaceType, tpmImpRevision; }

        [DllImport("tbs.dll")]
        static extern uint Tbsi_GetDeviceInfo(uint size, out TpmInfo info);
        [DllImport("ntdll.dll")]
        static extern int NtQuerySystemInformation(int cls, byte[] buf, int len, out int ret);
        [DllImport("kernel32.dll")]
        static extern uint EnumSystemFirmwareTables(uint provider, byte[] buf, uint size);

        [DllImport("kernel32.dll")]
        static extern bool GetFirmwareType(out int type);

        public static int Build
        {
            get
            {
                int b;
                int.TryParse(Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber"), out b);
                return b;
            }
        }

        static string TpmVersion()
        {
            try
            {
                TpmInfo i;
                if (Tbsi_GetDeviceInfo((uint)Marshal.SizeOf(typeof(TpmInfo)), out i) == 0)
                    return i.tpmVersion == 2 ? "TPM 2.0" : "TPM 1.2";
            }
            catch { }
            return "";
        }

        static string AcpiTables()
        {
            const uint acpi = 0x41435049; // 'ACPI'
            uint n = EnumSystemFirmwareTables(acpi, null, 0);
            var b = new byte[n];
            return n > 0 && EnumSystemFirmwareTables(acpi, b, n) > 0 ? Encoding.ASCII.GetString(b) : "";
        }

        static Section Security()
        {
            var s = new Section { Key = "security", Category = "Système", Title = "Sécurité", Icon = "security", Color = "#30D158",
                Description = "État des protections matérielles et logicielles de ce PC." };

            // --- micrologiciel ---
            string tpm = TpmVersion();
            string sb = Reg(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled");
            string tables = AcpiTables();
            bool iommu = tables.Contains("DMAR") || tables.Contains("IVRS");
            var cpu = One("SELECT Manufacturer, VirtualizationFirmwareEnabled FROM Win32_Processor");
            bool amd = Str(cpu, "Manufacturer") == "AuthenticAMD";

            int firmware;
            bool uefi = GetFirmwareType(out firmware) && firmware == 2;
            bool gpt = Query("SELECT Type FROM Win32_DiskPartition WHERE DiskIndex=" + SystemDiskIndex()).Any(x => Str(x, "Type").StartsWith("GPT"));

            s.Groups.Add(new Group("Micrologiciel et matériel", "Réglages du BIOS / UEFI")
                .Status("TPM 2.0", tpm == "TPM 2.0" ? "on" : tpm.Length > 0 ? "warn" : "off",
                        tpm == "TPM 2.0" ? "Présent et actif" : tpm.Length > 0 ? tpm + " seulement" : "Absent ou désactivé")
                .Status("Secure Boot", sb == "1" ? "on" : "off",
                        sb == "1" ? "Activé" : sb == "0" ? "Désactivé" : "Non pris en charge")
                .Status("IOMMU / VT-d", iommu ? "on" : "off",
                        iommu ? (amd ? "Activé (AMD-Vi)" : "Activé (Intel VT-d)") : "Désactivé ou absent")
                .Status("Démarrage UEFI", uefi ? "on" : "off", uefi ? "Mode UEFI" : "Mode hérité (BIOS)")
                .Status("Disque système GPT", gpt ? "on" : "off", gpt ? "Table GPT" : "Table MBR"));

            // --- virtualisation ---
            int ret;
            var ci = new byte[8];
            BitConverter.GetBytes(8).CopyTo(ci, 0);
            uint options = NtQuerySystemInformation(103, ci, 8, out ret) == 0 ? BitConverter.ToUInt32(ci, 4) : 0;
            bool hvci = (options & 0x400) != 0; // CODEINTEGRITY_OPTION_HVCI_KMCI_ENABLED
            string setting = Reg(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
            string vbs = Str(Query("SELECT * FROM Win32_DeviceGuard", @"root\Microsoft\Windows\DeviceGuard").FirstOrDefault(),
                             "VirtualizationBasedSecurityStatus");
            bool hyper = Str(One("SELECT HypervisorPresent FROM Win32_ComputerSystem"), "HypervisorPresent") == "True";
            bool vtx = hyper || Str(cpu, "VirtualizationFirmwareEnabled") == "True";

            s.Groups.Add(new Group("Virtualisation", "Isolation du noyau")
                .Status("Hyperviseur", hyper ? "on" : "off", hyper ? "Actif" : "Inactif")
                .Status("HVCI", hvci ? "on" : "off", hvci ? "En cours d’exécution" : "Inactif")
                .Status("Intégrité de la mémoire", hvci ? "on" : setting == "1" ? "warn" : "off",
                        hvci ? "Activée" : setting == "1" ? "Réglée sur activée, mais inactive" : "Désactivée")
                .Status("Sécurité par virtualisation (VBS)", vbs == "2" ? "on" : vbs == "1" ? "warn" : "off",
                        vbs == "2" ? "Active" : vbs == "1" ? "Configurée, inactive" : "Inactive")
                .Status("Virtualisation du processeur", vtx ? "on" : "off",
                        (vtx ? "Activée" : "Désactivée") + (amd ? " (AMD-V)" : " (Intel VT-x)"))
                .Status("Signature des pilotes", (options & 2) == 0 ? "on" : "off", (options & 2) == 0 ? "Exigée" : "Mode test activé"));

            // --- antivirus : la classe Defender ne repond pas quand le service est arrete ---
            var mp = Query("SELECT * FROM MSFT_MpComputerStatus", @"root\Microsoft\Windows\Defender").FirstOrDefault();
            string service = Str(One("SELECT State FROM Win32_Service WHERE Name='WinDefend'"), "State");
            bool av = Str(mp, "AntivirusEnabled") == "True", rt = Str(mp, "RealTimeProtectionEnabled") == "True";
            var wd = new Group("Windows Defender", "Antivirus Microsoft")
                .Status("Windows Defender", av ? "on" : service == "Running" ? "warn" : "off",
                        av ? (Str(mp, "AMRunningMode").StartsWith("Passive") ? "Actif en mode passif" : "Activé")
                           : service == "Running" ? "Service démarré, antivirus inactif"
                           : service.Length == 0 ? "Service absent" : "Désactivé (service arrêté)")
                .Status("Protection en temps réel", rt ? "on" : "off", rt ? "Activée" : "Désactivée");
            if (mp != null)
            {
                bool tamper = Str(mp, "IsTamperProtected") == "True";
                wd.Status("Protection contre les falsifications", tamper ? "on" : "off", tamper ? "Activée" : "Désactivée")
                  .Info("Version des signatures", Str(mp, "AntivirusSignatureVersion"));
            }
            foreach (var o in Query("SELECT displayName FROM AntiVirusProduct", @"root\SecurityCenter2"))
                wd.Info("Antivirus enregistré", Str(o, "displayName"));
            s.Groups.Add(wd);
            return s;
        }
    }
}
