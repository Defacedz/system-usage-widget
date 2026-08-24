# System Widget

Une petite jauge toujours visible, pour Windows, qui affiche d'un coup d'œil la
consommation du GPU, la VRAM, le CPU et la RAM.

*Read this in [English](README.md).*

<img src="docs/screenshot.png" alt="Le widget affichant les jauges GPU, VRAM, CPU et RAM" width="546">

Elle se place au-dessus de la barre des tâches et ne passe jamais derrière,
car l'exécutable est compilé avec le privilège `uiAccess` — le même que la
Loupe ou le clavier visuel. Elle ne se replace au-dessus que si la barre des
tâches l'a réellement recouverte, au lieu de deux fois par seconde — fini le
clignotement.

**Températures** : le thermomètre GPU affiche la température du cœur NVIDIA
(via `nvidia-smi`). Le thermomètre CPU lit le capteur du processeur lui-même
(« CPU Package ») grâce à la bibliothèque
[LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
embarquée (MIT) — le moteur de la plupart des outils de capteurs. Cette
lecture exige les droits administrateur : l'installateur lance le widget
élevé, et l'option *Lancer au démarrage de Windows* crée une tâche planifiée
avec privilèges maximaux — aucune fenêtre UAC à l'ouverture de session. Sans
ces droits, le thermomètre reste vide plutôt que d'inventer un chiffre. C'est
le seul binaire livré du projet (`lib/LibreHardwareMonitorLib.dll`, avec sa
dépendance `HidSharp.dll`, toutes deux MIT) — tout le reste se compile depuis
les sources.

## Installation

Collez ceci dans **PowerShell** et acceptez la demande d'élévation :

```powershell
irm https://raw.githubusercontent.com/Defacedz/system-usage-widget/main/web-install.ps1 | iex
```

Ou depuis **cmd.exe** :

```bat
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/Defacedz/system-usage-widget/main/web-install.ps1 | iex"
```

Cette commande télécharge le dépôt dans un dossier temporaire et lance
`Installer.ps1`. Si vous préférez lire avant d'exécuter — le bon réflexe face à
n'importe quelle commande `| iex` — clonez le dépôt et double-cliquez sur
`Installer.bat`.

### Ce que fait l'installateur

- Compile `SystemWidget.cs` **sur votre machine**, avec le compilateur C# déjà
  inclus dans Windows. Rien n'est téléchargé en dehors des sources de ce dépôt,
  aucune chaîne de compilation à installer.
- Crée un certificat auto-signé `CN=SystemWidget Local` et l'ajoute au magasin
  racine de confiance de la machine. Windows n'accorde `uiAccess` qu'à un
  exécutable signé et installé sous `Program Files` : les deux étapes sont donc
  indispensables pour que le widget reste au-dessus de la barre des tâches.
  **Ajouter un certificat racine n'est pas un geste anodin** — voir
  [Désinstallation](#désinstallation) pour le retirer.
- Copie le binaire signé dans `C:\Program Files\SystemWidget\` et le lance.

## Fonctions

- **GPU W** — consommation électrique, en pourcentage de la limite de la carte
- **VRAM** — mémoire vidéo utilisée
- **CPU** — utilisation totale du processeur
- **RAM** — mémoire physique utilisée
- Dégradé de couleur continu : vert au repos, orange, puis rouge près de la limite
- **Ne gêne pas les jeux** : la jauge se masque dès qu'une application plein
  écran est au premier plan, y compris en plein écran sans bordure, et cesse
  de se remettre au premier plan — elle ne peut donc plus faire sortir un jeu
  de son mode d'affichage
- Survol d'une jauge pour les chiffres exacts (watts, Go, charge du moteur)
- Glisser pour déplacer, position mémorisée ; opacité réglable ; lancement au
  démarrage de Windows en option
- **English, Français, Español, Deutsch** — clic droit → Langue
- **Deux thèmes** — clic droit → Thème : le *Sombre* d'origine, ou *Ivoire*,
  bâti sur la palette d'Anthropic, pour que le panneau se pose sur une barre
  des tâches claire au lieu d'y faire un trou noir
- **Mises à jour intégrées** — le widget compare sa version à ce dépôt toutes
  les 6 heures, et à chaque clic sur *Actualiser* ; quand une nouvelle version
  est publiée, le contour passe à l'orange Claude et une entrée *Mise à jour
  disponible* apparaît en tête du clic droit

## Prérequis

- Windows 10 ou 11
- .NET Framework 4.x (présent sur tout Windows encore supporté — rien à installer)
- **Carte NVIDIA** pour les jauges GPU et VRAM : elles lisent `nvidia-smi`,
  fourni avec le pilote NVIDIA. Sur une autre carte, ces deux jauges affichent
  `--` ; le CPU et la RAM continuent de fonctionner.

## D'où viennent les mesures

| Jauge | Source |
|---|---|
| CPU | `GetSystemTimes`, échantillonné une fois par seconde |
| RAM | `GlobalMemoryStatusEx` |
| GPU W, VRAM | `nvidia-smi --query-gpu=...`, un appel masqué par seconde |

Aucun pilote, aucun module noyau, aucun service privilégié. Le widget tourne
sous votre propre compte et ne lit rien d'autre ; il n'accède pas au réseau et
n'envoie aucune télémétrie.

## Ajouter une langue

Tout se trouve dans la classe `I18n` de `SystemWidget.cs`. Copiez un des blocs
`English()` / `French()`, traduisez les valeurs, puis ajoutez-le à `Catalog` :

```csharp
public static readonly Strings[] Catalog = { English(), French(), Spanish(), German(), Italian() };
```

Le menu des langues et le fichier de configuration sont tous deux pilotés par
`Catalog` : il n'y a rien d'autre à brancher. Enregistrez le fichier en
**UTF-8 avec BOM**. Les contributions sont bienvenues.

## En cas de problème

**GPU et VRAM affichent `--`.** `nvidia-smi` est introuvable ou n'a rien
renvoyé. Le widget le cherche dans `System32` puis dans
`Program Files\NVIDIA Corporation\NVSMI`, avant de se rabattre sur le `PATH`.
Sur une carte non NVIDIA, c'est le comportement attendu. Survolez la jauge
GPU : l'infobulle donne la raison exacte, y compris ce que `nvidia-smi` a
répondu.

**La jauge GPU affiche un pourcentage mais l'infobulle ne donne aucun
wattage.** Cette carte ne rapporte pas `power.draw` — c'est courant sur les
cartes de portable. La jauge se rabat alors sur la charge du moteur
graphique, et l'infobulle le précise. Chaque champ est lu indépendamment :
un champ manquant ne vide plus les autres.

**Le widget a disparu.** Une application plein écran est au premier plan ; il
revient tout seul. Décochez *Masquer en plein écran* pour le garder visible
par-dessus une vidéo en plein écran — au prix de son retour par-dessus les jeux.

## Désinstallation

1. Clic droit sur le widget → *Quitter*
2. Supprimez `C:\Program Files\SystemWidget`
3. Supprimez `%APPDATA%\SystemWidget`
4. Retirez le certificat : `certlm.msc` → *Autorités de certification racines
   de confiance* → *Certificats* → supprimez **SystemWidget Local**, puis
   faites de même dans *Éditeurs approuvés* et *Personnel*

## Licence

MIT — voir [LICENSE](LICENSE).
