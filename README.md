# HWID Checker

Lecteur d'identifiants matériels pour Windows 10 et 11, avec une interface inspirée de macOS 27 (Liquid Glass).
**[Télécharger HWID-Checker.exe](https://github.com/camraa/hwidchecker/releases/latest/download/HWID-Checker.exe)** (un seul fichier). Rien à installer, lecture seule. Les droits administrateur sont demandés au lancement, pour lire les compteurs SMART des disques.

## Contenu

- **Matériel** : UUID de la carte mère, GPU, processeur et TPM, stockage (SSD, RAID), RAM, écrans, périphériques USB et Bluetooth, avec marque, modèle et numéro de série quand le matériel les communique
- **Réseau** : adresses MAC (actuelle et d'usine), IP locales et publique, table ARP
- **Sécurité** : TPM 2.0, Secure Boot, IOMMU / VT-d, hyperviseur, HVCI, intégrité de la mémoire, Windows Defender
- **Windows et BIOS** : version, type de licence, activation, version et date du BIOS, lien vers le dernier BIOS publié
- **Pilotes** : version et date de chaque pilote, comparées à Windows Update, au catalogue Microsoft Update et à NVIDIA
- **Santé des disques** : usure, température, heures de fonctionnement, erreurs (NVMe et SMART)
- **Anti-triche** : conditions des tournois Fortnite, de Vanguard, FACEIT, Ricochet, EA Javelin, Easy Anti-Cheat et BattlEye, avec la page officielle
- **En direct** : charge et températures du processeur, de la RAM et du GPU
- **Historique** : référence enregistrée et changements détectés à chaque lancement
- **Partager** : image du PC à coller dans Discord, identifiants masqués par défaut
- **Ressources** : liens vers Sordum, WinRAR et WinUtil

Un point d'information à côté de chaque donnée explique à quoi elle correspond.
Interface en français et en anglais, thème clair ou sombre, export TXT et JSON, masquage des identifiants.

## Liens de mise à jour

Un lien n'est affiché que si la fiche officielle cite un identifiant matériel exact de l'appareil, pour la bonne
version de Windows et la bonne architecture. Sinon, l'application indique que la mise à jour n'est pas disponible.

## Confidentialité

Les identifiants restent sur le PC. L'application contacte :

- `api.ipify.org` et `api6.ipify.org`, pour l'adresse IP publique
- `catalog.update.microsoft.com`, pour le BIOS au lancement (identifiant de modèle du micrologiciel) et, pendant l'analyse, pour les pilotes (identifiants de modèle)
- `api.github.com`, pour signaler une nouvelle version (désactivable dans Apparence)
- `standards-oui.ieee.org`, seulement si vous téléchargez la base des fabricants
- Windows Update et NVIDIA, pendant l'analyse des pilotes

Aucun numéro de série n'est envoyé.

## Compiler

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Utilise le compilateur C# livré avec Windows (.NET Framework 4.8).

---

Made by **1skf** sur Discord.
