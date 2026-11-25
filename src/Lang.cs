// Traduction francais -> anglais, appliquee a l'affichage. Le francais sert de cle.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HwidChecker
{
    public static class L
    {
        public static bool English;

        static readonly Dictionary<string, string> d = new Dictionary<string, string>();

        public static string T(string s)
        {
            string v;
            if (!English || string.IsNullOrEmpty(s)) return s;
            if (d.TryGetValue(s, out v)) return v;
            if (s.StartsWith("Appareil du réseau local"))
                return s.Replace("Appareil du réseau local : son adresse IP à gauche, son adresse MAC à droite.", "Device on the local network: IP address on the left, MAC address on the right.")
                        .Replace("Celui-ci est la box ou le routeur.", "This one is the router.")
                        .Replace("Son adresse MAC n’est pas celle d’usine : l’appareil la génère, comme le font les smartphones récents.", "Its MAC address is not the factory one: the device generates it, as recent smartphones do.");
            foreach (var sep in new[] { " › ", " · ", " / ", ", " })
                if (s.Contains(sep) && s.Length < 160)
                    return string.Join(sep, s.Split(new[] { sep }, System.StringSplitOptions.None).Select(T));
            foreach (var r in rules) s = Regex.Replace(s, r[0], r[1]);
            return s;
        }

        static readonly string[][] rules =
        {
            new[] { @"^Disque dur \(HDD\) (\d+)", "Hard drive $1" }, new[] { @"^Disque externe (\d+)", "External drive $1" },
            new[] { @"^Disque (\d+)", "Drive $1" }, new[] { @" \(système\)$", " (system)" },
            new[] { @"^(\d+) appareils$", "$1 devices" }, new[] { @"^1 appareil$", "1 device" },
            new[] { @"^IP locale : ", "Local IP: " }, new[] { @"^Barrette (\d+)", "Module $1" }, new[] { @"^Écran (\d+)", "Display $1" },
            new[] { @"^Processeur (\d+)", "Processor $1" }, new[] { @"^puce (.+)", "$1 chip" }, new[] { @"^(.+) seulement$", "$1 only" },
            new[] { @"(\d),(\d+) pouces$", "$1.$2 inches" }, new[] { @"(\d) pouces$", "$1 inches" },
            new[] { @"(\d),(\d) ([KMGT])o\b", "$1.$2 $3B" }, new[] { @"(\d) ([KMGT])o\b", "$1 $2B" },
            new[] { @"^1 changement$", "1 change" }, new[] { @"^(\d+) changements$", "$1 changes" },
            new[] { @"^Référence enregistrée le (.+)\.$", "Reference saved on $1." },
            new[] { @"^Aucun résultat pour « (.+) »\.$", "No results for “$1”." },
            new[] { @"^Analyse : (\d+) sur (\d+)$", "Scanning: $1 of $2" },
            new[] { @"^(\d+) à mettre à jour$", "$1 to update" }, new[] { @"^(\d+) à jour$", "$1 up to date" },
            new[] { @"^(\d+) sans source vérifiée$", "$1 without a verified source" }, new[] { @"^(\d+) non vérifiés$", "$1 not checked" },
            new[] { @"^Version (\S+) disponible \((.+)\)$", "Version $1 available ($2)" },
            new[] { @"^À jour \((.+)\)$", "Up to date ($1)" }, new[] { @"^(\d+) pilotes installés$", "$1 drivers installed" }
        };

        static L()
        {
            string[] p =
            {
                // Navigation
                "Rechercher", "Search", "Vue d’ensemble", "Overview", "Matériel", "Hardware", "Réseau", "Network", "Système", "System", "Outils", "Tools",
                "Carte mère (UUID)", "Motherboard (UUID)", "Carte graphique", "Graphics card", "Processeur et TPM", "Processor and TPM",
                "Stockage (SSD, RAID)", "Storage (SSD, RAID)", "Mémoire (RAM)", "Memory (RAM)", "Écrans", "Displays",
                "Périphériques externes", "External devices", "Adresses MAC", "MAC addresses", "Adresses IP", "IP addresses", "Table ARP", "ARP table",
                "Sécurité", "Security", "Pilotes", "Drivers", "Comparaison", "Compare", "Ressources", "Resources", "Apparence", "Appearance", "Résultats", "Results",
                // Barre d'outils
                "Fermer", "Close", "Réduire", "Minimize", "Agrandir", "Zoom", "Masquer les identifiants", "Hide identifiers",
                "Actualiser (Ctrl+R)", "Refresh (Ctrl+R)", "Tout copier (Ctrl+Maj+C)", "Copy all (Ctrl+Shift+C)", "Exporter (Ctrl+E)", "Export (Ctrl+E)",
                "Afficher ou masquer les identifiants (Ctrl+H)", "Show or hide identifiers (Ctrl+H)", "Langue", "Language",
                "Copier", "Copy", "Lecture du matériel…", "Reading hardware…",
                "Informations actualisées", "Information refreshed", "Presse-papiers occupé, réessayez", "Clipboard busy, try again",
                "Rapport complet copié", "Full report copied", "Rapport enregistré", "Report saved", "Empreinte copiée", "Fingerprint copied", "Copié", "Copied",
                "Échec de l’enregistrement", "Could not save", "Texte", "Text", "Aucun navigateur trouvé, lien copié", "No browser found, link copied",
                "Référence enregistrée", "Reference saved", "Impossible d’enregistrer la référence", "Could not save the reference",
                // Descriptions
                "UUID SMBIOS et numéros de série gravés dans la carte mère, le BIOS et le châssis.", "SMBIOS UUID and serial numbers stored in the motherboard, BIOS and chassis.",
                "UUID, numéro de série et identifiants PCI de chaque GPU.", "UUID, serial number and PCI identifiers of each GPU.",
                "Identifiant CPUID du processeur et clé unique du module de sécurité TPM.", "Processor CPUID and unique key of the TPM security module.",
                "Numéro de série de chaque disque, état RAID et numéros de volume.", "Serial number of each drive, RAID state and volume numbers.",
                "Marque, référence et numéro de série de chaque barrette.", "Brand, part number and serial number of each module.",
                "Informations EDID transmises par chaque écran connecté.", "EDID information reported by each connected display.",
                "Périphériques USB et Bluetooth, avec leur marque et leur numéro de série.", "USB and Bluetooth devices, with brand and serial number.",
                "Adresse actuelle de chaque carte, comparée à son adresse d’usine.", "Current address of each adapter, compared with its factory address.",
                "Adresses locales de chaque interface et adresse publique vue sur Internet.", "Local address of each interface and public address seen on the Internet.",
                "Appareils du réseau local récemment vus par ce PC : adresse IP et adresse MAC.", "Local network devices recently seen by this PC: IP and MAC address.",
                "État des protections matérielles et logicielles de ce PC.", "State of this PC’s hardware and software protections.",
                "Identifiants logiciels, régénérés lors d’une réinstallation de Windows.", "Software identifiers, regenerated when Windows is reinstalled.",
                "Aucun matériel détecté.", "No hardware detected.", "Aucun écran ne transmet d’informations EDID.", "No display reports EDID information.",
                "Aucun périphérique USB ou Bluetooth détecté.", "No USB or Bluetooth device detected.", "Aucune carte réseau détectée.", "No network adapter detected.",
                "La table ARP est vide.", "The ARP table is empty.",
                // Groupes
                "Carte mère", "Motherboard", "Ordinateur", "Computer", "Châssis", "Chassis", "Processeur", "Processor", "Volumes", "Volumes",
                "Numéros attribués au formatage", "Numbers assigned when formatting", "Grappe détectée", "Array detected", "Aucune grappe détectée", "No array detected",
                "Adaptateur virtuel", "Virtual adapter", "Non détecté", "Not detected", "Sans EDID", "No EDID", "Intégré", "Built-in",
                "IP publique", "Public IP", "IP locale", "Local IP", "Vue depuis Internet", "Seen from the Internet", "Identifiants", "Identifiers",
                "Propres à cette installation", "Specific to this installation", "Système d’exploitation", "Operating system",
                "Micrologiciel et matériel", "Firmware and hardware", "Réglages du BIOS / UEFI", "BIOS / UEFI settings", "BIOS / UEFI", "BIOS / UEFI",
                "Virtualisation", "Virtualization", "Isolation du noyau", "Core isolation", "Antivirus Microsoft", "Microsoft antivirus",
                "Identifiants principaux", "Main identifiers", "Windows et BIOS", "Windows and BIOS", "Composition de l’empreinte", "Fingerprint components",
                "Empreinte matérielle", "Hardware fingerprint", "UUID de la carte mère", "Motherboard UUID", "SN de la carte mère", "Motherboard SN", "SN du BIOS", "BIOS SN",
                "SN du disque système", "System drive SN", "UUID (carte mère)", "UUID (motherboard)", "Stockage", "Storage", "RAM", "RAM", "Écran", "Display",
                // Libelles
                "Numéro de série (SN)", "Serial number (SN)", "Marque", "Brand", "Modèle", "Model", "Révision", "Revision", "Référence (SKU)", "SKU",
                "Version du BIOS", "BIOS version", "Date du firmware actuel", "Current firmware date", "Dernière mise à jour", "Latest update",
                "Support du fabricant", "Manufacturer support", "Étiquette d’inventaire", "Asset tag", "UUID du GPU", "GPU UUID",
                "Identifiant PCI (PnP)", "PCI identifier (PnP)", "Mémoire vidéo", "Video memory", "Version du pilote", "Driver version", "Date du pilote", "Driver date", "Éditeur du pilote", "Driver publisher", "ID du processeur", "Processor ID",
                "ID unique du TPM", "TPM unique ID", "Cœurs / threads", "Cores / threads", "Support (socket)", "Socket", "Signature", "Signature",
                "Micrologiciel", "Firmware", "Identifiant EUI-64", "EUI-64 identifier", "Capacité", "Capacity", "État", "State", "Contrôleur", "Controller",
                "Niveau", "Level", "UUID de la grappe", "Array UUID", "Fréquence", "Speed", "Emplacement", "Slot", "Identifiant PnP", "PnP identifier",
                "Définition native", "Native resolution", "Diagonale", "Diagonal", "Année de fabrication", "Year of manufacture",
                "Identifiant USB (PnP)", "USB identifier (PnP)", "Connexion", "Connection", "Adresse Bluetooth", "Bluetooth address",
                "Adresse MAC", "MAC address", "Adresse MAC d’usine", "Factory MAC address", "Adresse IPv4 locale", "Local IPv4 address",
                "Adresse IPv6", "IPv6 address", "Adresse IPv6 de liaison", "Link-local IPv6 address", "Adresse IPv4 publique", "Public IPv4 address",
                "Adresse IPv6 publique", "Public IPv6 address", "Adresse publique", "Public address", "Masque de sous-réseau", "Subnet mask",
                "Passerelle", "Gateway", "Serveur DHCP", "DHCP server", "Serveurs DNS", "DNS servers", "ID de produit (SN Windows)", "Product ID (Windows SN)",
                "Profil matériel (GUID)", "Hardware profile (GUID)", "Identifiant SQM", "SQM identifier", "SID de l’utilisateur", "User SID",
                "Nom de l’ordinateur", "Computer name", "Version de Windows", "Windows version", "Type de licence", "License type", "Activation", "Activation",
                "Installé le", "Installed on", "Hyperviseur", "Hypervisor", "Intégrité de la mémoire", "Memory integrity",
                "Sécurité par virtualisation (VBS)", "Virtualization-based security (VBS)", "Virtualisation du processeur", "CPU virtualization",
                "Protection en temps réel", "Real-time protection", "Protection contre les falsifications", "Tamper protection",
                "Version des signatures", "Signature version", "Antivirus enregistré", "Registered antivirus", "Avant", "Before", "Maintenant", "Now",
                // Valeurs
                "Non communiqué", "Not reported", "Générique", "Generic", "Différente de l’adresse d’usine", "Differs from factory address", "Absent", "Missing",
                "Disque dur (HDD)", "Hard drive (HDD)", "Disque externe", "External drive", "Disque", "Drive", "Espaces de stockage", "Storage Spaces", "Carte SD", "SD card",
                "Disques en mode RAID matériel", "Drives in hardware RAID mode", "Espaces de stockage Windows (RAID logiciel)", "Windows Storage Spaces (software RAID)",
                "Aucun disque en RAID", "No drive in RAID", "USB interne", "Internal USB", "USB externe", "External USB", "Bluetooth (appairé)", "Bluetooth (paired)",
                "Carte physique", "Physical adapter", "Carte USB", "USB adapter", "Carte virtuelle", "Virtual adapter", "Connectée", "Connected", "Déconnectée", "Disconnected",
                "Indisponible (hors ligne)", "Unavailable (offline)", "MAC aléatoire", "Random MAC", "PC assemblé", "Custom PC",
                "OEM (clé numérique dans le BIOS)", "OEM (digital key in the BIOS)", "Retail (vente au détail)", "Retail", "Inconnu", "Unknown", "Inconnue", "Unknown",
                "Activé", "Enabled", "Activée", "Enabled", "Désactivé", "Disabled", "Désactivée", "Disabled", "Actif", "Active", "Active", "Active", "Inactif", "Inactive", "Inactive", "Inactive",
                "Période de grâce", "Grace period", "Mode notification (non activé)", "Notification mode (not activated)", "Non activé", "Not activated",
                "Présent et actif", "Present and active", "Absent ou désactivé", "Missing or disabled", "Non pris en charge", "Not supported",
                "Activé (AMD-Vi)", "Enabled (AMD-Vi)", "Activé (Intel VT-d)", "Enabled (Intel VT-d)", "Désactivé ou absent", "Disabled or missing",
                "En cours d’exécution", "Running", "Réglée sur activée, mais inactive", "Set to enabled, but inactive", "Configurée, inactive", "Configured, inactive",
                "Activée (AMD-V)", "Enabled (AMD-V)", "Activée (Intel VT-x)", "Enabled (Intel VT-x)", "Désactivée (AMD-V)", "Disabled (AMD-V)", "Désactivée (Intel VT-x)", "Disabled (Intel VT-x)",
                "Actif en mode passif", "Active in passive mode", "Service démarré, antivirus inactif", "Service running, antivirus inactive",
                "Service absent", "Service missing", "Désactivé (service arrêté)", "Disabled (service stopped)",
                "Vérification en cours…", "Checking…", "Vérification impossible", "Could not check",
                "Non disponible : aucune source officielle confirmée", "Not available: no confirmed official source",
                // Pilotes
                "Lancer l’analyse", "Start scan", "Relancer l’analyse", "Scan again",
                "Compare les pilotes installés à Windows Update, au catalogue Microsoft et à NVIDIA. Seuls les modèles de composants sont envoyés, jamais les numéros de série.",
                "Compares installed drivers with Windows Update, the Microsoft catalog and NVIDIA. Only component models are sent, never serial numbers.",
                "À mettre à jour", "To update", "À jour", "Up to date", "Sans source vérifiée", "No verified source", "Non vérifiés", "Not checked",
                "Mise à jour non disponible", "Update not available", "Mise à jour disponible", "Update available", "Catalogue Microsoft", "Microsoft catalog",
                "Installée", "Installed", "Publiée", "Published", "Lien non disponible", "Link not available", "Aucune mise à jour", "No updates",
                "Une version plus récente existe pour cet identifiant matériel exact. La ligne ouvre sa page officielle.", "A newer version exists for this exact hardware ID. The row opens its official page.",
                "Une version plus récente existe, mais sa page officielle n’a pas pu être confirmée : aucun lien n’est donné.", "A newer version exists, but its official page could not be confirmed, so no link is given.",
                "La source officielle ne propose rien de plus récent pour cet identifiant matériel. La ligne ouvre la dernière version publiée.", "The official source has nothing newer for this hardware ID. The row opens the latest published version.",
                "Aucune page officielle n’a pu être confirmée pour cet appareil : aucun lien n’est donné. Voyez le logiciel ou le site de son fabricant.", "No official page could be confirmed for this device, so no link is given. Check its manufacturer’s software or website.",
                "La source n’a pas répondu. Vérifiez la connexion, puis relancez l’analyse.", "The source did not respond. Check your connection, then scan again.",
                "Cliquez sur Lancer l’analyse pour chercher une version plus récente.", "Click Start scan to look for a newer version.",
                // Comparaison
                "Aucune référence", "No reference", "Enregistrez l’état actuel pour repérer plus tard tout identifiant modifié.", "Save the current state to spot any changed identifier later.",
                "Aucun changement", "No changes", "Enregistrer la référence", "Save reference", "Remplacer la référence", "Replace reference",
                "Cette valeur entre dans le calcul de l’empreinte.", "This value is part of the fingerprint.",
                "Valeur absente ou générique : elle est ignorée dans le calcul.", "Missing or generic value: ignored in the calculation.",
                // Apparence
                "Affichage", "Display settings", "Thème", "Theme", "Automatique", "Automatic", "Clair", "Light", "Sombre", "Dark", "Verre", "Glass", "Transparent", "Clear", "Teinté", "Tinted",
                "Confidentialité", "Privacy", "Automatique suit le réglage clair ou sombre de Windows.", "Automatic follows the Windows light or dark setting.",
                "Règle la transparence de la fenêtre, qui laisse apparaître les couleurs du fond d’écran.", "Adjusts the window transparency, which lets the wallpaper colors show through.",
                "Remplace les identifiants par des points à l’écran, pour une capture ou un partage. La copie et l’export gardent les vraies valeurs.", "Replaces identifiers with dots on screen, for screenshots or sharing. Copy and export keep the real values.",
                "Langue de l’interface et des explications.", "Language of the interface and explanations.",
                // Ressources
                "Réglages de Windows", "Windows settings", "Utilitaires", "Utilities", "WinUtil (code source)", "WinUtil (source code)",
                "Defender Control de Sordum : active ou désactive Windows Defender. Sans Defender ni autre antivirus, ce PC n’est plus protégé, et l’outil est parfois signalé par les antivirus.", "Sordum Defender Control: turns Windows Defender on or off. Without Defender or another antivirus this PC is unprotected, and antivirus software sometimes flags the tool.",
                "Active ou désactive les mises à jour automatiques de Windows. Bloquées, les corrections de sécurité ne sont plus installées.", "Turns Windows automatic updates on or off. While blocked, security fixes are no longer installed.",
                "Création et extraction d’archives RAR et ZIP. Page de téléchargement officielle.", "Creates and extracts RAR and ZIP archives. Official download page.",
                "Présentation officielle de WinUtil : installation de programmes, réglages et réparation de Windows.", "Official WinUtil page: program installation, Windows tweaks and repair.",
                "Dépôt GitHub officiel de l’outil de Chris Titus.", "Official GitHub repository of the Chris Titus tool.",
                // Explications
                "Identifiant universel gravé dans la carte mère par le fabricant. C’est l’élément le plus utilisé pour reconnaître un PC : il survit au formatage et au changement de disque.", "Universal identifier stored in the motherboard by the manufacturer. It is the most widely used way to recognize a PC: it survives formatting and drive changes.",
                "Numéro d’inventaire qu’une entreprise ou le fabricant peut inscrire dans le BIOS. Souvent vide sur un PC personnel.", "Inventory number a company or the manufacturer can store in the BIOS. Often empty on a personal PC.",
                "Version matérielle de la carte : deux cartes du même modèle peuvent avoir des révisions différentes.", "Hardware version of the board: two boards of the same model can have different revisions.",
                "Référence commerciale exacte de l’ordinateur, utile pour retrouver ses pièces ou ses pilotes.", "Exact product reference of the computer, useful to find parts or drivers.",
                "Version du micrologiciel actuellement installée sur la carte mère.", "Firmware version currently installed on the motherboard.",
                "Date de sortie de la version du BIOS installée.", "Release date of the installed BIOS version.",
                "Recherche le dernier BIOS publié pour ce PC dans le catalogue Microsoft. Le lien n’apparaît que si la fiche correspond exactement à ce micrologiciel ; sinon, passez par le support du fabricant.", "Looks up the latest BIOS published for this PC in the Microsoft catalog. The link only appears if the entry matches this exact firmware; otherwise use the manufacturer’s support page.",
                "Page officielle des pilotes et des BIOS du fabricant.", "The manufacturer’s official driver and BIOS page.",
                "Identifiant unique de la puce NVIDIA, fourni par le pilote. Il suit la carte si elle est montée dans un autre PC.", "Unique identifier of the NVIDIA chip, provided by the driver. It follows the card if moved to another PC.",
                "Adresse matérielle donnée par Windows : fabricant (VEN), puce (DEV), assembleur (SUBSYS), puis emplacement sur le bus PCI.", "Hardware address assigned by Windows: vendor (VEN), chip (DEV), board maker (SUBSYS), then location on the PCI bus.",
                "Quantité de VRAM dédiée à la carte graphique.", "Amount of VRAM dedicated to the graphics card.", "Version du micrologiciel de la carte graphique.", "Firmware version of the graphics card.",
                "Version du pilote installé pour cet appareil.", "Version of the driver installed for this device.",
                "Date de la version installée. Les pilotes fournis par Windows portent souvent la date fixe 2006-06-21, choisie par Microsoft.", "Date of the installed version. Drivers shipped with Windows often carry the fixed date 2006-06-21, chosen by Microsoft.",
                "Société qui a publié le pilote installé.", "Company that published the installed driver.",
                "Fournis par Windows", "Provided by Windows", "Via Windows Update", "Through Windows Update",
                "Pilote Microsoft, mis à jour avec Windows lui-même. Sa date est souvent fixée à 2006-06-21 par Microsoft.", "Microsoft driver, updated with Windows itself. Microsoft often sets its date to 2006-06-21.",
                "Code CPUID : famille, modèle et révision de la puce. Il est identique pour tous les processeurs du même modèle, il n’est donc pas unique à lui seul.", "CPUID code: family, model and revision of the chip. It is identical for every processor of the same model, so it is not unique on its own.",
                "Empreinte SHA-256 de la clé d’approbation (EK) du TPM. Cette clé est créée en usine et propre à chaque puce : c’est l’identifiant le plus fiable du TPM.", "SHA-256 hash of the TPM endorsement key (EK). This key is created at the factory and unique to each chip: it is the most reliable TPM identifier.",
                "Norme prise en charge par le module TPM. Windows 11 exige TPM 2.0.", "Standard supported by the TPM module. Windows 11 requires TPM 2.0.",
                "Nombre de cœurs physiques et de fils d’exécution simultanés.", "Number of physical cores and simultaneous threads.", "Type de connecteur entre le processeur et la carte mère.", "Connector type between processor and motherboard.",
                "Famille, modèle et stepping : la génération précise de la puce.", "Family, model and stepping: the exact chip generation.",
                "Identifiant mondial du contrôleur NVMe, attribué en usine. Windows l’affiche parfois à la place du numéro de série.", "Worldwide identifier of the NVMe controller, assigned at the factory. Windows sometimes shows it instead of the serial number.",
                "Bus utilisé par le disque : NVMe, SATA, USB ou RAID.", "Bus used by the drive: NVMe, SATA, USB or RAID.", "Taille annoncée par le composant.", "Size reported by the component.",
                "SSD (mémoire flash), disque dur (plateaux) ou disque externe.", "SSD (flash memory), hard drive (platters) or external drive.",
                "Indique si des disques sont regroupés en RAID, par le matériel ou par Windows.", "Shows whether drives are grouped in RAID, by hardware or by Windows.",
                "Puce ou pilote qui pilote les disques. Un contrôleur peut être compatible RAID sans qu’une grappe soit créée.", "Chip or driver controlling the drives. A controller can support RAID without any array being created.",
                "Groupe de disques géré par les Espaces de stockage Windows.", "Group of drives managed by Windows Storage Spaces.",
                "Nom et mode de résilience de la grappe : simple (RAID 0), miroir (RAID 1) ou parité (RAID 5).", "Array name and resiliency mode: simple (RAID 0), mirror (RAID 1) or parity (RAID 5).",
                "Identifiant unique de la grappe de disques.", "Unique identifier of the drive array.", "Vitesse à laquelle la barrette fonctionne actuellement.", "Speed the module is currently running at.",
                "Connecteur de la carte mère où se trouve la barrette.", "Motherboard slot holding the module.",
                "Adresse matérielle donnée par Windows à cet appareil. Elle dépend du modèle et du port utilisé.", "Hardware address assigned by Windows to this device. It depends on the model and the port used.",
                "Nombre de pixels réels de la dalle.", "Actual number of pixels on the panel.", "Taille de l’image, calculée à partir des dimensions annoncées par l’écran.", "Picture size, computed from the dimensions reported by the display.",
                "Année de production inscrite dans l’écran.", "Production year stored in the display.",
                "Adresse matérielle donnée par Windows : fabricant (VID), produit (PID), puis numéro de série ou port utilisé.", "Hardware address assigned by Windows: vendor (VID), product (PID), then serial number or port used.",
                "Codes officiels du fabricant (VID) et du produit (PID) sur le bus USB.", "Official vendor (VID) and product (PID) codes on the USB bus.", "Façon dont l’appareil est relié au PC.", "How the device is connected to the PC.",
                "Adresse matérielle unique de l’appareil Bluetooth, équivalente à une adresse MAC.", "Unique hardware address of the Bluetooth device, equivalent to a MAC address.",
                "Adresse matérielle que la carte utilise actuellement sur le réseau local. Elle peut être modifiée par logiciel.", "Hardware address the adapter currently uses on the local network. Software can change it.",
                "Adresse gravée dans la carte par le fabricant. Si elle diffère de l’adresse actuelle, la MAC a été modifiée.", "Address stored in the adapter by the manufacturer. If it differs from the current one, the MAC was changed.",
                "Carte physique, adaptateur USB, Bluetooth ou carte virtuelle (VPN, machine virtuelle).", "Physical adapter, USB adapter, Bluetooth or virtual adapter (VPN, virtual machine).",
                "Indique si la carte est reliée à un réseau en ce moment.", "Shows whether the adapter is currently connected to a network.",
                "Adresse de ce PC sur le réseau de la maison ou de l’entreprise. Elle n’est pas visible depuis Internet.", "Address of this PC on the home or company network. It is not visible from the Internet.",
                "Adresse IPv6 de ce PC. Elle peut être visible depuis Internet.", "IPv6 address of this PC. It may be visible from the Internet.", "Adresse IPv6 limitée au réseau local immédiat (fe80::).", "IPv6 address limited to the immediate local network (fe80::).",
                "Adresse attribuée par le fournisseur d’accès, vue par les sites visités. Obtenue auprès du service api.ipify.org.", "Address assigned by the Internet provider, seen by visited sites. Obtained from the api.ipify.org service.",
                "Adresse IPv6 vue par les sites visités. Obtenue auprès du service api6.ipify.org.", "IPv6 address seen by visited sites. Obtained from the api6.ipify.org service.",
                "Adresse vue par les sites visités. Elle n’a pas pu être obtenue.", "Address seen by visited sites. It could not be obtained.",
                "Définit quelles adresses appartiennent au même réseau local.", "Defines which addresses belong to the same local network.", "Adresse de la box ou du routeur qui relie ce PC à Internet.", "Address of the router connecting this PC to the Internet.",
                "Appareil qui a attribué l’adresse IP locale.", "Device that assigned the local IP address.", "Serveurs qui traduisent les noms de sites en adresses IP.", "Servers translating site names into IP addresses.",
                "Puce de sécurité qui protège les clés de chiffrement (BitLocker, Windows Hello). Exigée par Windows 11 et par certains anti-triche.", "Security chip protecting encryption keys (BitLocker, Windows Hello). Required by Windows 11 and some anti-cheat software.",
                "Démarrage sécurisé : l’UEFI refuse de lancer un système ou un pilote de démarrage non signé.", "UEFI refuses to start an unsigned system or boot driver.",
                "Contrôle les accès directs à la mémoire (DMA) des cartes PCIe et Thunderbolt. Détecté par la table ACPI DMAR (Intel) ou IVRS (AMD), publiée quand la fonction est active dans le BIOS.", "Controls direct memory access (DMA) from PCIe and Thunderbolt devices. Detected through the ACPI DMAR (Intel) or IVRS (AMD) table, published when the feature is enabled in the BIOS.",
                "Couche de virtualisation sous Windows (Hyper-V). Nécessaire à VBS et à HVCI. Actif aussi si ce PC est lui-même une machine virtuelle.", "Virtualization layer beneath Windows (Hyper-V). Required by VBS and HVCI. Also active if this PC is itself a virtual machine.",
                "Extensions VT-x ou AMD-V du processeur, activées dans le BIOS.", "VT-x or AMD-V processor extensions, enabled in the BIOS.", "Isole une partie du noyau Windows dans une zone protégée par l’hyperviseur.", "Isolates part of the Windows kernel in an area protected by the hypervisor.",
                "Intégrité du code protégée par l’hyperviseur : seuls des pilotes signés peuvent s’exécuter dans le noyau. Cet état est celui réellement en cours.", "Hypervisor-protected code integrity: only signed drivers can run in the kernel. This is the state actually in effect.",
                "Nom donné à HVCI dans l’application Sécurité Windows. Cette ligne tient compte du réglage enregistré, même s’il attend un redémarrage.", "Name given to HVCI in the Windows Security app. This row takes the saved setting into account, even if it is waiting for a restart.",
                "Antivirus intégré à Windows. Il se désactive seul quand un autre antivirus est installé.", "Antivirus built into Windows. It turns itself off when another antivirus is installed.",
                "Analyse des fichiers et programmes au moment où ils sont ouverts.", "Scans files and programs as they are opened.", "Empêche un programme de désactiver Windows Defender à votre insu.", "Prevents a program from turning off Windows Defender without your knowledge.",
                "Version de la base de menaces connues de Windows Defender.", "Version of the Windows Defender known-threat database.", "Antivirus déclaré auprès du Centre de sécurité Windows.", "Antivirus registered with Windows Security Center.",
                "Identifiant créé à l’installation de Windows. Beaucoup de logiciels l’utilisent pour leurs licences.", "Identifier created when Windows is installed. Many programs use it for licensing.",
                "Numéro de série de la licence Windows. Ce n’est pas la clé d’activation.", "Serial number of the Windows license. It is not the activation key.", "Identifiant du profil matériel actif, créé par Windows.", "Identifier of the active hardware profile, created by Windows.",
                "Identifiant utilisé par la télémétrie Microsoft.", "Identifier used by Microsoft telemetry.", "Identifiant de sécurité du compte Windows actuel.", "Security identifier of the current Windows account.",
                "Nom donné au PC, visible sur le réseau local.", "Name given to the PC, visible on the local network.", "Édition de Windows et version semestrielle installée.", "Windows edition and installed feature version.",
                "OEM : liée à ce PC. Retail : achetée séparément, transférable. Volume : licence d’entreprise (MAK ou KMS).", "OEM: tied to this PC. Retail: bought separately, transferable. Volume: business license (MAK or KMS).",
                "Indique si cette copie de Windows est activée.", "Shows whether this copy of Windows is activated.", "Numéro exact de la version de Windows.", "Exact Windows build number.",
                "Date de l’installation ou de la dernière mise à niveau majeure.", "Date of installation or last major upgrade.",
                "Numéro inscrit par le fabricant dans le BIOS. Il identifie cet exemplaire précis et sert pour la garantie.", "Number stored in the BIOS by the manufacturer. It identifies this exact unit and is used for warranty.",
                "Numéro gravé dans la carte. Les cartes grand public ne le communiquent généralement pas au pilote ; il figure sur l’étiquette.", "Number stored in the card. Consumer cards usually do not report it to the driver; it is printed on the label.",
                "Les processeurs et modules TPM actuels n’exposent plus de numéro de série, pour protéger la vie privée. Une valeur générique vient de la carte mère.", "Current processors and TPM modules no longer expose a serial number, for privacy. A generic value comes from the motherboard.",
                "Numéro gravé dans le disque en usine. Il ne change pas au formatage.", "Number stored in the drive at the factory. Formatting does not change it.", "Numéro inscrit dans la puce SPD de la barrette.", "Number stored in the module’s SPD chip.",
                "Numéro transmis par l’écran dans son bloc EDID. Les dalles de portables n’en ont souvent pas.", "Number reported by the display in its EDID block. Laptop panels often have none.",
                "Numéro annoncé par le périphérique. Beaucoup de souris, claviers et appareils Bluetooth n’en ont pas.", "Number reported by the device. Many mice, keyboards and Bluetooth devices have none.",
                "Windows ne donne pas accès au numéro de série des cartes réseau : l’adresse MAC d’usine joue ce rôle.", "Windows does not expose network adapter serial numbers: the factory MAC address plays that role.",
                "Fabricant du composant.", "Component manufacturer.", "Référence commerciale ou technique du composant.", "Commercial or technical reference of the component.", "Version installée.", "Installed version.",
                "Version du logiciel interne (firmware) du composant.", "Version of the component’s internal software (firmware).",
                "Résumé SHA-256 calculé à partir de cinq identifiants stables. Si un seul change, l’empreinte change entièrement.", "SHA-256 digest computed from five stable identifiers. If any of them changes, the whole fingerprint changes.",
                "Numéro attribué à la partition lors de son formatage. Il change à chaque formatage.", "Number assigned to the partition when formatted. It changes with every format."
            };
            for (int i = 0; i + 1 < p.Length; i += 2) d[p[i]] = p[i + 1];
        }
    }
}
