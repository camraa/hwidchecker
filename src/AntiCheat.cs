// Conditions des anti-triche, relevees sur leurs pages officielles.
using System.Collections.Generic;
using System.Linq;

namespace HwidChecker
{
    public class Need
    {
        public string Label;
        public bool Must;   // exige ; sinon "peut etre exige"
        public bool Met;
    }

    public class AntiCheat
    {
        public string Name, Games, Note, Url;
        public List<Need> Needs = new List<Need>();

        AntiCheat Must(params string[] l) { Needs.AddRange(l.Select(x => new Need { Label = x, Must = true })); return this; }
        AntiCheat May(params string[] l) { Needs.AddRange(l.Select(x => new Need { Label = x })); return this; }

        /// <summary>"on" : pret, "warn" : refus possible, "off" : condition exigee absente.</summary>
        public string State
        {
            get { return Needs.Any(n => n.Must && !n.Met) ? "off" : Needs.Any(n => !n.Met) ? "warn" : "on"; }
        }

        public string Verdict
        {
            get
            {
                int must = Needs.Count(n => n.Must && !n.Met);
                return must > 1 ? must + " conditions manquantes" : must == 1 ? "1 condition manquante"
                    : Needs.Any(n => !n.Met) ? "Refus possible" : Needs.Count == 0 ? "Aucune exigence publiée" : "Prêt";
            }
        }

        const string Win10 = "Windows 10 22H2 ou plus récent", Win11 = "Windows 11 25H2 ou plus récent";

        public static List<AntiCheat> Evaluate(Report r)
        {
            var list = new List<AntiCheat>
            {
                new AntiCheat
                {
                    Name = "Riot Vanguard", Games = "VALORANT, League of Legends",
                    Url = "https://support.riotgames.com/en-us/riot/performance/vanguard-security-requirements",
                    Note = "Riot indique que Vanguard peut exiger ces protections selon la configuration du PC."
                }.May("TPM 2.0", "Secure Boot", "Intégrité de la mémoire", "IOMMU / VT-d"),

                new AntiCheat
                {
                    Name = "Vanguard à la demande", Games = "Mode facultatif de Riot (Pre-Check)",
                    Url = "https://support.riotgames.com/fr-FR/riot/performance/vanguard-pre-check",
                    Note = "Mode facultatif : Vanguard ne démarre plus avec Windows. Ces conditions ne concernent que ce mode."
                }.Must(Win11, "TPM 2.0", "Secure Boot", "IOMMU / VT-d", "Sécurité par virtualisation (VBS)", "HVCI"),

                new AntiCheat
                {
                    Name = "FACEIT", Games = "Counter-Strike 2",
                    Url = "https://support.faceit.com/hc/en-us/articles/23117181142556",
                    Note = "FACEIT exige TPM 2.0 et Secure Boot. IOMMU et VBS sont déployés par vagues de joueurs."
                }.Must("TPM 2.0", "Secure Boot").May("IOMMU / VT-d", "Sécurité par virtualisation (VBS)"),

                new AntiCheat
                {
                    Name = "Ricochet", Games = "Call of Duty : Black Ops 7, Modern Warfare 4, Warzone",
                    Url = "https://support.activision.com/articles/trusted-platform-module-and-secure-boot",
                    Note = "Activision exige TPM 2.0 et Secure Boot. Sans eux, l’accès en ligne est limité ou bloqué selon le jeu."
                }.Must(Win10, "TPM 2.0", "Secure Boot", "Démarrage UEFI", "Disque système GPT"),

                new AntiCheat
                {
                    Name = "EA Javelin", Games = "Battlefield 6",
                    Url = "https://help.ea.com/en/articles/technical-issues/secure-boot/",
                    Note = "EA demande Secure Boot, qui nécessite TPM 2.0, un BIOS en mode UEFI et un disque GPT."
                }.Must("Secure Boot", "TPM 2.0", "Démarrage UEFI", "Disque système GPT"),

                new AntiCheat
                {
                    Name = "Easy Anti-Cheat", Games = "Fortnite, Apex Legends, Rust…",
                    Url = "https://easy.ac/support/articles/additional-security-requirements",
                    Note = "Chaque développeur choisit ses exigences : elles varient d’un jeu à l’autre."
                }.May("TPM 2.0", "Secure Boot"),

                new AntiCheat
                {
                    Name = "BattlEye", Games = "PUBG, Rainbow Six Siege, DayZ…",
                    Url = "https://www.battleye.com/support/faq/",
                    Note = "BattlEye ne publie pas d’exigence sur TPM ou Secure Boot. Le mode test des pilotes n’est pas pris en charge."
                }.Must("Signature des pilotes")
            };

            var states = r.Sections.Where(s => s.Key == "security").SelectMany(s => s.Groups).SelectMany(g => g.Fields)
                .Where(f => f.State != null).GroupBy(f => f.Label).ToDictionary(g => g.Key, g => g.First().State == "on");
            foreach (var a in list)
                foreach (var n in a.Needs)
                {
                    bool v;
                    n.Met = n.Label == Win10 ? Hardware.Build >= 19045 : n.Label == Win11 ? Hardware.Build >= 26200
                          : states.TryGetValue(n.Label, out v) && v;
                }
            return list;
        }

        /// <summary>Ou activer une condition manquante.</summary>
        public static string Fix(string label)
        {
            switch (label)
            {
                case "TPM 2.0": return "Dans le BIOS, menu Sécurité : activer TPM, Intel PTT ou AMD fTPM.";
                case "Secure Boot": return "Dans le BIOS, menu Démarrage ou Sécurité : activer Secure Boot. Le PC doit d’abord être en mode UEFI avec un disque GPT.";
                case "IOMMU / VT-d": return "Dans le BIOS : activer Intel VT-d ou AMD IOMMU.";
                case "Sécurité par virtualisation (VBS)": return "S’active avec l’intégrité de la mémoire, si la virtualisation est activée dans le BIOS.";
                case "HVCI":
                case "Intégrité de la mémoire": return "Sécurité Windows, Sécurité de l’appareil, Isolation du noyau : activer Intégrité de la mémoire, puis redémarrer.";
                case "Démarrage UEFI": return "Passer le BIOS du mode hérité (CSM) au mode UEFI. Le disque système doit d’abord être en GPT.";
                case "Disque système GPT": return "Convertir le disque système avec l’outil Microsoft MBR2GPT, après une sauvegarde.";
                case "Signature des pilotes": return "Désactiver le mode test de Windows (bcdedit /set testsigning off), puis redémarrer.";
                default: return "Installer la version de Windows demandée avec Windows Update.";
            }
        }
    }
}
