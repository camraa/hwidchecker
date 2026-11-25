// Texte du point d'information place a cote de chaque donnee.
using System.Collections.Generic;
using System.Linq;

namespace HwidChecker
{
    public static class Help
    {
        // Cle : "section|libelle" (prioritaire) ou "libelle".
        static readonly Dictionary<string, string> texts = new Dictionary<string, string>
        {
            // Carte mere
            { "board|UUID", "Identifiant universel gravé dans la carte mère par le fabricant. C’est l’élément le plus utilisé pour reconnaître un PC : il survit au formatage et au changement de disque." },
            { "Étiquette d’inventaire", "Numéro d’inventaire qu’une entreprise ou le fabricant peut inscrire dans le BIOS. Souvent vide sur un PC personnel." },
            { "Révision", "Version matérielle de la carte : deux cartes du même modèle peuvent avoir des révisions différentes." },
            { "Référence (SKU)", "Référence commerciale exacte de l’ordinateur, utile pour retrouver ses pièces ou ses pilotes." },
            { "Version du BIOS", "Version du micrologiciel actuellement installée sur la carte mère." },
            { "Date du firmware actuel", "Date de sortie de la version du BIOS installée." },
            { "Dernière mise à jour", "Recherche le dernier BIOS publié pour ce PC dans le catalogue Microsoft. Le lien n’apparaît que si la fiche correspond exactement à ce micrologiciel ; sinon, passez par le support du fabricant." },
            { "Support du fabricant", "Page officielle des pilotes et des BIOS du fabricant." },

            // GPU
            { "UUID du GPU", "Identifiant unique de la puce NVIDIA, fourni par le pilote. Il suit la carte si elle est montée dans un autre PC." },
            { "Identifiant PCI (PnP)", "Adresse matérielle donnée par Windows : fabricant (VEN), puce (DEV), assembleur (SUBSYS), puis emplacement sur le bus PCI." },
            { "Mémoire vidéo", "Quantité de VRAM dédiée à la carte graphique." },
            { "VBIOS", "Version du micrologiciel de la carte graphique." },
            { "Version du pilote", "Version du pilote installé pour cet appareil." },
            { "Date du pilote", "Date de la version installée. Les pilotes fournis par Windows portent souvent la date fixe 2006-06-21, choisie par Microsoft." },
            { "Éditeur du pilote", "Société qui a publié le pilote installé." },

            // CPU + TPM
            { "ID du processeur", "Code CPUID : famille, modèle et révision de la puce. Il est identique pour tous les processeurs du même modèle, il n’est donc pas unique à lui seul." },
            { "ID unique du TPM", "Empreinte SHA-256 de la clé d’approbation (EK) du TPM. Cette clé est créée en usine et propre à chaque puce : c’est l’identifiant le plus fiable du TPM." },
            { "cpu|Version", "Norme prise en charge par le module TPM. Windows 11 exige TPM 2.0." },
            { "Cœurs / threads", "Nombre de cœurs physiques et de fils d’exécution simultanés." },
            { "Support (socket)", "Type de connecteur entre le processeur et la carte mère." },
            { "Signature", "Famille, modèle et stepping : la génération précise de la puce." },

            // Stockage
            { "Identifiant EUI-64", "Identifiant mondial du contrôleur NVMe, attribué en usine. Windows l’affiche parfois à la place du numéro de série." },
            { "Interface", "Bus utilisé par le disque : NVMe, SATA, USB ou RAID." },
            { "Capacité", "Taille annoncée par le composant." },
            { "storage|Type", "SSD (mémoire flash), disque dur (plateaux) ou disque externe." },
            { "storage|État", "Indique si des disques sont regroupés en RAID, par le matériel ou par Windows." },
            { "Contrôleur", "Puce ou pilote qui pilote les disques. Un contrôleur peut être compatible RAID sans qu’une grappe soit créée." },
            { "Pool", "Groupe de disques géré par les Espaces de stockage Windows." },
            { "Niveau", "Nom et mode de résilience de la grappe : simple (RAID 0), miroir (RAID 1) ou parité (RAID 5)." },
            { "UUID de la grappe", "Identifiant unique de la grappe de disques." },

            // Sante des disques
            { "Santé", "État du disque d’après ses propres compteurs (SMART). Le pourcentage d’un SSD est sa durée de vie restante estimée par le fabricant." },
            { "Température", "Température actuelle mesurée par le disque." },
            { "Usure", "Part de la durée de vie prévue déjà consommée." },
            { "Réserve disponible", "Blocs de secours encore libres pour remplacer des cellules usées." },
            { "Heures de fonctionnement", "Temps total pendant lequel le disque a été alimenté." },
            { "Démarrages", "Nombre de mises sous tension du disque." },
            { "Arrêts non sécurisés", "Coupures d’alimentation sans arrêt propre." },
            { "Données écrites", "Volume total écrit depuis la mise en service." },
            { "Erreurs de support", "Erreurs de données que le disque n’a pas pu corriger. La valeur normale est 0." },
            { "Secteurs réalloués", "Secteurs défectueux remplacés par des secteurs de réserve. Un nombre qui augmente annonce une panne." },
            { "Secteurs en attente", "Secteurs instables que le disque doit encore vérifier." },
            { "Secteurs illisibles", "Secteurs que le disque n’a pas pu relire." },

            // Batterie
            { "battery|Santé", "Capacité actuelle comparée à la capacité d’origine. En dessous de 75 %, l’autonomie baisse nettement." },
            { "battery|Numéro de série (SN)", "Numéro transmis par la batterie. Beaucoup de portables ne le communiquent pas." },
            { "Technologie", "Type de cellules de la batterie." },
            { "Capacité d’origine", "Énergie que la batterie pouvait stocker neuve." },
            { "Capacité actuelle", "Énergie que la batterie peut stocker aujourd’hui, pleine." },
            { "Cycles de charge", "Nombre de charges complètes équivalentes. Certains portables ne le communiquent pas." },
            { "Charge", "Niveau de charge au moment du relevé." },
            { "Alimentation", "Source d’énergie au moment du relevé." },
            { "Tension", "Tension mesurée aux bornes de la batterie." },
            { "Consommation", "Puissance tirée de la batterie au moment du relevé." },

            // RAM
            { "Fréquence", "Vitesse à laquelle la barrette fonctionne actuellement." },
            { "Emplacement", "Connecteur de la carte mère où se trouve la barrette." },

            // Ecrans
            { "Identifiant PnP", "Adresse matérielle donnée par Windows à cet appareil. Elle dépend du modèle et du port utilisé." },
            { "Définition native", "Nombre de pixels réels de la dalle." },
            { "Diagonale", "Taille de l’image, calculée à partir des dimensions annoncées par l’écran." },
            { "Année de fabrication", "Année de production inscrite dans l’écran." },

            // Peripheriques
            { "Identifiant USB (PnP)", "Adresse matérielle donnée par Windows : fabricant (VID), produit (PID), puis numéro de série ou port utilisé." },
            { "VID / PID", "Codes officiels du fabricant (VID) et du produit (PID) sur le bus USB." },
            { "Connexion", "Façon dont l’appareil est relié au PC." },
            { "Adresse Bluetooth", "Adresse matérielle unique de l’appareil Bluetooth, équivalente à une adresse MAC." },

            // Reseau
            { "Adresse MAC", "Adresse matérielle que la carte utilise actuellement sur le réseau local. Elle peut être modifiée par logiciel." },
            { "Adresse MAC d’usine", "Adresse gravée dans la carte par le fabricant. Si elle diffère de l’adresse actuelle, la MAC a été modifiée." },
            { "mac|Type", "Carte physique, adaptateur USB, Bluetooth ou carte virtuelle (VPN, machine virtuelle)." },
            { "mac|État", "Indique si la carte est reliée à un réseau en ce moment." },
            { "Adresse IPv4 locale", "Adresse de ce PC sur le réseau de la maison ou de l’entreprise. Elle n’est pas visible depuis Internet." },
            { "Adresse IPv6", "Adresse IPv6 de ce PC. Elle peut être visible depuis Internet." },
            { "Adresse IPv6 de liaison", "Adresse IPv6 limitée au réseau local immédiat (fe80::)." },
            { "Adresse IPv4 publique", "Adresse attribuée par le fournisseur d’accès, vue par les sites visités. Obtenue auprès du service api.ipify.org." },
            { "Adresse IPv6 publique", "Adresse IPv6 vue par les sites visités. Obtenue auprès du service api6.ipify.org." },
            { "Adresse publique", "Adresse vue par les sites visités. Elle n’a pas pu être obtenue." },
            { "Masque de sous-réseau", "Définit quelles adresses appartiennent au même réseau local." },
            { "Passerelle", "Adresse de la box ou du routeur qui relie ce PC à Internet." },
            { "Serveur DHCP", "Appareil qui a attribué l’adresse IP locale." },
            { "Serveurs DNS", "Serveurs qui traduisent les noms de sites en adresses IP." },

            // Securite
            { "TPM 2.0", "Puce de sécurité qui protège les clés de chiffrement (BitLocker, Windows Hello). Exigée par Windows 11 et par certains anti-triche." },
            { "Secure Boot", "Démarrage sécurisé : l’UEFI refuse de lancer un système ou un pilote de démarrage non signé." },
            { "IOMMU / VT-d", "Contrôle les accès directs à la mémoire (DMA) des cartes PCIe et Thunderbolt. Détecté par la table ACPI DMAR (Intel) ou IVRS (AMD), publiée quand la fonction est active dans le BIOS." },
            { "Démarrage UEFI", "Mode de démarrage du PC. Secure Boot n’existe qu’en mode UEFI." },
            { "Disque système GPT", "Type de table de partitions du disque Windows. Le mode UEFI demande une table GPT." },
            { "Signature des pilotes", "Windows n’accepte que des pilotes signés. Le mode test lève cette règle et bloque la plupart des anti-triche." },
            { "Hyperviseur", "Couche de virtualisation sous Windows (Hyper-V). Nécessaire à VBS et à HVCI. Actif aussi si ce PC est lui-même une machine virtuelle." },
            { "Virtualisation du processeur", "Extensions VT-x ou AMD-V du processeur, activées dans le BIOS." },
            { "Sécurité par virtualisation (VBS)", "Isole une partie du noyau Windows dans une zone protégée par l’hyperviseur." },
            { "HVCI", "Intégrité du code protégée par l’hyperviseur : seuls des pilotes signés peuvent s’exécuter dans le noyau. Cet état est celui réellement en cours." },
            { "Intégrité de la mémoire", "Nom donné à HVCI dans l’application Sécurité Windows. Cette ligne tient compte du réglage enregistré, même s’il attend un redémarrage." },
            { "Windows Defender", "Antivirus intégré à Windows. Il se désactive seul quand un autre antivirus est installé." },
            { "Protection en temps réel", "Analyse des fichiers et programmes au moment où ils sont ouverts." },
            { "Protection contre les falsifications", "Empêche un programme de désactiver Windows Defender à votre insu." },
            { "Version des signatures", "Version de la base de menaces connues de Windows Defender." },
            { "Antivirus enregistré", "Antivirus déclaré auprès du Centre de sécurité Windows." },

            // Windows
            { "MachineGuid", "Identifiant créé à l’installation de Windows. Beaucoup de logiciels l’utilisent pour leurs licences." },
            { "ID de produit (SN Windows)", "Numéro de série de la licence Windows. Ce n’est pas la clé d’activation." },
            { "Profil matériel (GUID)", "Identifiant du profil matériel actif, créé par Windows." },
            { "Identifiant SQM", "Identifiant utilisé par la télémétrie Microsoft." },
            { "SID de l’utilisateur", "Identifiant de sécurité du compte Windows actuel." },
            { "Nom de l’ordinateur", "Nom donné au PC, visible sur le réseau local." },
            { "Version de Windows", "Édition de Windows et version semestrielle installée." },
            { "Type de licence", "OEM : liée à ce PC. Retail : achetée séparément, transférable. Volume : licence d’entreprise (MAK ou KMS)." },
            { "Activation", "Indique si cette copie de Windows est activée." },
            { "Build", "Numéro exact de la version de Windows." },
            { "Installé le", "Date de l’installation ou de la dernière mise à niveau majeure." },

            // SN par type de composant
            { "board|Numéro de série (SN)", "Numéro inscrit par le fabricant dans le BIOS. Il identifie cet exemplaire précis et sert pour la garantie." },
            { "gpu|Numéro de série (SN)", "Numéro gravé dans la carte. Les cartes grand public ne le communiquent généralement pas au pilote ; il figure sur l’étiquette." },
            { "cpu|Numéro de série (SN)", "Les processeurs et modules TPM actuels n’exposent plus de numéro de série, pour protéger la vie privée. Une valeur générique vient de la carte mère." },
            { "storage|Numéro de série (SN)", "Numéro gravé dans le disque en usine. Il ne change pas au formatage." },
            { "memory|Numéro de série (SN)", "Numéro inscrit dans la puce SPD de la barrette." },
            { "display|Numéro de série (SN)", "Numéro transmis par l’écran dans son bloc EDID. Les dalles de portables n’en ont souvent pas." },
            { "usb|Numéro de série (SN)", "Numéro annoncé par le périphérique. Beaucoup de souris, claviers et appareils Bluetooth n’en ont pas." },
            { "mac|Numéro de série (SN)", "Windows ne donne pas accès au numéro de série des cartes réseau : l’adresse MAC d’usine joue ce rôle." },

            // Commun
            { "Marque", "Fabricant du composant." },
            { "Modèle", "Référence commerciale ou technique du composant." },
            { "Version", "Version installée." },
            { "Micrologiciel", "Version du logiciel interne (firmware) du composant." },
            { "Empreinte", "Résumé SHA-256 calculé à partir de cinq identifiants stables. Si un seul change, l’empreinte change entièrement." }
        };

        public static string For(string section, string label)
        {
            string t;
            if (texts.TryGetValue(section + "|" + label, out t) || texts.TryGetValue(label, out t)) return t;
            if (section == "arp") return "Appareil du réseau local : son adresse IP à gauche, son adresse MAC à droite." +
                (label.Contains("Passerelle") ? " Celui-ci est la box ou le routeur." : "") +
                (label.Contains("aléatoire") ? " Son adresse MAC n’est pas celle d’usine : l’appareil la génère, comme le font les smartphones récents." : "");
            if (section == "startup") return "Programme lancé à l’ouverture de session. Impact élevé : plus d’une seconde de processeur ou 3 Mo lus au démarrage, d’après Windows.";
            if (label.StartsWith("Volume ")) return "Numéro attribué à la partition lors de son formatage. Il change à chaque formatage.";
            return null;
        }
    }
}
