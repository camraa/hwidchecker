// Correspondances code constructeur -> marque (liste courte des marques courantes).
using System;
using System.Collections.Generic;

namespace HwidChecker
{
    public static class Vendors
    {
        static Dictionary<string, string> Map(string s)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in s.Split(';'))
            {
                int i = e.IndexOf('=');
                d[e.Substring(0, i).Trim()] = e.Substring(i + 1).Trim();
            }
            return d;
        }

        static string Get(Dictionary<string, string> d, string k)
        {
            string v;
            return k != null && d.TryGetValue(k, out v) ? v : "";
        }

        static readonly Dictionary<string, string> pci = Map(
            "1043=ASUS;1462=MSI;1458=Gigabyte;3842=EVGA;19DA=Zotac;10DE=NVIDIA;1002=AMD;1DA2=Sapphire;174B=Sapphire;148C=PowerColor;" +
            "1682=XFX;1849=ASRock;196E=PNY;1569=Palit;10B0=Gainward;103C=HP;1028=Dell;17AA=Lenovo;8086=Intel;1025=Acer;7377=Colorful;" +
            "1B4C=KFA2;144D=Samsung;106B=Apple;1414=Microsoft;15AD=VMware;1D05=Tongfang;1558=Clevo;1A58=Razer");

        static readonly Dictionary<string, string> usb = Map(
            "046D=Logitech;1532=Razer;045E=Microsoft;1B1C=Corsair;1038=SteelSeries;0951=Kingston / HyperX;03F0=HP;05AC=Apple;04E8=Samsung;" +
            "054C=Sony;8087=Intel;0BDA=Realtek;0781=SanDisk;0B05=ASUS;1462=MSI;2109=VIA Labs;05E3=Genesys Logic;04F2=Chicony;18D1=Google;" +
            "413C=Dell;17EF=Lenovo;04D9=Holtek;258A=SinoWealth;3434=Keychron;0D8C=C-Media;1058=Western Digital;0BC2=Seagate;090C=Silicon Motion;" +
            "13FE=Phison;28DE=Valve;057E=Nintendo;0C45=Sonix;04CA=Lite-On;0A12=CSR (Bluetooth);1235=Focusrite;0FD9=Elgato;1E71=NZXT;3842=EVGA;" +
            "2DC8=8BitDo;046A=Cherry;056A=Wacom;04B4=Cypress;1004=LG;043E=LG;0489=Foxconn;138A=Validity;06CB=Synaptics;27C6=Goodix;" +
            "8564=Transcend;0930=Toshiba;0480=Toshiba;125F=ADATA;12D1=Huawei;2717=Xiaomi;2833=Meta;152D=JMicron;174C=ASMedia;0CF3=Qualcomm Atheros;" +
            "13D3=IMC Networks;2516=Cooler Master;1E7D=ROCCAT;044F=Thrustmaster;0E6F=PDP;20D6=PowerA;1A86=WCH;10C4=Silicon Labs;0403=FTDI;" +
            "0A5C=Broadcom;1770=MSI;0B0E=Jabra;0951=Kingston;17A0=Samson;0582=Roland;0499=Yamaha;04A9=Canon;04B8=Epson;04F9=Brother;" +
            "05C8=Cheng Uei (Foxlink);064E=Suyin;5986=Bison;1BCF=Sunplus;0424=Microchip;2357=TP-Link;0846=Netgear;148F=Ralink;0E8D=MediaTek");

        static readonly Dictionary<string, string> mon = Map(
            "SAM=Samsung;GSM=LG;DEL=Dell;AUS=ASUS;ACI=ASUS;ACR=Acer;BNQ=BenQ;AOC=AOC;MSI=MSI;HPN=HP;HWP=HP;LEN=Lenovo;PHL=Philips;IVM=Iiyama;" +
            "VSC=ViewSonic;GBT=Gigabyte;AUO=AU Optronics;BOE=BOE;LGD=LG Display;LPL=LG Philips;SDC=Samsung Display;SEC=Samsung;CMN=Innolux;" +
            "CMO=Chi Mei;SHP=Sharp;SNY=Sony;APP=Apple;XMI=Xiaomi;HKC=HKC;INL=Innolux;EIZ=EIZO;ENC=EIZO;NEC=NEC;PSC=Parsec (virtuel);TCL=TCL;PAN=Panasonic");

        static readonly Dictionary<string, string> ram = Map(
            "80CE=Samsung;CE00=Samsung;80AD=SK hynix;AD00=SK hynix;802C=Micron;2C00=Micron;0198=Kingston;9801=Kingston;859B=Crucial;029E=Corsair;04CD=G.Skill;CD04=G.Skill");

        static readonly Dictionary<string, string> tpm = Map(
            "INTC=Intel (PTT);AMD=AMD (fTPM);IFX=Infineon;STM=STMicroelectronics;NTC=Nuvoton;NTZ=Nationz;MSFT=Microsoft (virtuel);QCOM=Qualcomm;VMW=VMware;ATML=Atmel");

        static readonly Dictionary<string, string> mac = Map(
            "00:50:56=VMware;00:0C:29=VMware;08:00:27=VirtualBox;00:15:5D=Hyper-V;B8:27:EB=Raspberry Pi;DC:A6:32=Raspberry Pi;52:54:00=QEMU");

        // marque (debut du nom SMBIOS) | nom affiche | page de support
        static readonly string[][] support =
        {
            new[] { "HP", "HP", "https://support.hp.com/fr-fr/drivers" },
            new[] { "Hewlett", "HP", "https://support.hp.com/fr-fr/drivers" },
            new[] { "ASUS", "ASUS", "https://www.asus.com/fr/support/" },
            new[] { "Micro-Star", "MSI", "https://www.msi.com/support" },
            new[] { "MSI", "MSI", "https://www.msi.com/support" },
            new[] { "Gigabyte", "Gigabyte", "https://www.gigabyte.com/fr/Support" },
            new[] { "ASRock", "ASRock", "https://www.asrock.com/support/index.fr.asp" },
            new[] { "Dell", "Dell", "https://www.dell.com/support/home/fr-fr" },
            new[] { "Alienware", "Dell", "https://www.dell.com/support/home/fr-fr" },
            new[] { "Lenovo", "Lenovo", "https://pcsupport.lenovo.com/fr/fr" },
            new[] { "Acer", "Acer", "https://www.acer.com/fr-fr/support" },
            new[] { "Microsoft", "Microsoft Surface", "https://support.microsoft.com/surface" }
        };

        static string[] Support(string maker)
        {
            foreach (var r in support)
                if ((maker ?? "").StartsWith(r[0], StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        public static string SupportName(string maker) { var r = Support(maker); return r == null ? "" : r[1]; }
        public static string SupportUrl(string maker) { var r = Support(maker); return r == null ? null : r[2]; }

        public static string Pci(string id) { return Get(pci, id); }
        public static string Usb(string vid) { return Get(usb, vid); }

        public static string Monitor(string code)
        {
            string v = Get(mon, code);
            return v.Length > 0 ? v : code;
        }

        public static string Tpm(string id)
        {
            string v = Get(tpm, id);
            return v.Length > 0 ? v : (id ?? "");
        }

        public static string Ram(string maker)
        {
            string m = (maker ?? "").Trim();
            string v = m.Length >= 4 ? Get(ram, m.Substring(0, 4)) : "";
            return v.Length > 0 ? v : Hardware.IsPlaceholder(m) ? "" : m;
        }

        public static string Mac(string address)
        {
            string v = Get(mac, address.Substring(0, 8));
            if (v.Length > 0) return v;
            // 2e bit du premier octet : adresse administree localement
            return (Convert.ToByte(address.Substring(0, 2), 16) & 2) != 0 ? "MAC aléatoire" : "";
        }

        public static string Disk(string model)
        {
            string m = (model ?? "").ToUpperInvariant();
            string[,] t =
            {
                { "SAMSUNG", "Samsung" }, { "WDC", "Western Digital" }, { "WD_", "Western Digital" }, { "WD ", "Western Digital" },
                { "SANDISK", "SanDisk" }, { "CRUCIAL", "Crucial" }, { "KINGSTON", "Kingston" }, { "SEAGATE", "Seagate" },
                { "TOSHIBA", "Toshiba" }, { "KIOXIA", "Kioxia" }, { "HGST", "HGST" }, { "HITACHI", "Hitachi" }, { "INTEL", "Intel" },
                { "SK HYNIX", "SK hynix" }, { "SKHYNIX", "SK hynix" }, { "HFS", "SK hynix" }, { "MICRON", "Micron" }, { "ADATA", "ADATA" },
                { "CORSAIR", "Corsair" }, { "SABRENT", "Sabrent" }, { "LEXAR", "Lexar" }, { "PNY", "PNY" }, { "PATRIOT", "Patriot" },
                { "TEAM", "TeamGroup" }, { "NETAC", "Netac" }, { "FIKWOT", "Fikwot" }, { "GIGABYTE", "Gigabyte" }, { "MSI", "MSI" },
                { "SOLIDIGM", "Solidigm" }, { "TRANSCEND", "Transcend" }, { "VMWARE", "VMware" }, { "VBOX", "VirtualBox" }
            };
            for (int i = 0; i < t.GetLength(0); i++)
                if (m.Contains(t[i, 0])) return t[i, 1];
            if (m.StartsWith("CT") && m.Contains("SSD")) return "Crucial";
            if (m.StartsWith("ST") && m.Length > 3 && char.IsDigit(m[2])) return "Seagate";
            return "";
        }
    }
}
