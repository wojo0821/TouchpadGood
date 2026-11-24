# TouchpadGood

[English](README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | [中文 (简体)](README.zh.md) | [Español](README.es.md) | [Deutsch](README.de.md) | **Français**

**Pilote Precision Touchpad ultra-faible latence pour le gaming et la productivité sous Windows 11.**
Service Win32 pur en arrière-plan (`TouchpadGood.exe`) + Paramètres Fluent Design (`TouchpadGoodSettings.exe`) + Installateur tout-en-un (`TouchpadGoodSetup.exe`).

---

## Fonctionnalités clés

- **Architecture Win32 pure sans latence (`TouchpadGood.exe`)**
  - Démon léger de `~55 Ko` — sans WinForms/GDI+, sans icône de barre des tâches, 0 hook `WH_MOUSE_LL`, **0 % CPU au repos**.
  - `GetRawInputData` Zero-Copy en un seul appel système + analyse `byte*` directe via `hid.dll` + `SendInput` sur la pile.
  - Priorité `High`, planification **MMCSS `"Games"`**, EcoQoS désactivé et GC `SustainedLowLatency`.
- **Intégration native avec Windows 11**
  - Déplacement/tap à 1 doigt, **défilement natif sur le bord droit (`SingleFingerPanMode = 2`)** et gestes à 4 doigts restent 100 % natifs Windows.
  - Suivi linéaire 1:1 (désactive l'accélération du pointeur au niveau du noyau).
  - Bouton en un clic **« Auto-configurer Windows »** pour appliquer accélération OFF, délai de frappe 0, défilement bord droit et tap à 1 doigt.
- **Gaming FPS et gestes multi-doigts personnalisables**
  - **Entrée simultanée FPS (Viser + Tirer)** :
    - Démarrez un glisser droit à 3 doigts → relevez 1 doigt (2 doigts maintiennent la visée) → touchez ou maintenez un 3e doigt pour tirer (clic gauche) tout en visant.
    - Démarrez un glisser gauche à 2 doigts → touchez ou maintenez un 3e doigt pour un clic/glisser droit simultané.
  - **Verrouillage sur le doigt principal** :
    - L'ajout de doigts en mouvement (`1 → 1+1` / `1 → 1+2`) conserve le doigt initial pour déplacer le pointeur.
    - Un glisser direct à 2/3 doigts (`0 → 2` / `0 → 3`) se verrouille sur l'index (doigt le plus en bas à gauche).
  - Raccourci global **`Ctrl + Alt + T`** pour activer/mettre en pause à tout moment.
- **Interface Fluent Design (7 langues & mise à jour automatique)**
  - Thème sombre/clair automatique, barre de défilement et menus déroulants Fluent personnalisés.
  - Changement de langue instantané (7 langues) et **mise à jour automatique depuis GitHub** en un clic.

---

## Gestes par défaut

| Geste | Action par défaut |
| :--- | :--- |
| **1 doigt déplacer / tap** | Natif Windows (1:1 linéaire, accélération OFF) |
| **Défilement bord droit (1 doigt)** | Natif Windows 11 (`SingleFingerPanMode = 2`) |
| **Défilement bord inférieur (1 doigt)** | Défilement horizontal fluide (`WM_MOUSEHWHEEL`, 0 hook au repos) |
| **Glisser à 2 doigts** | Glisser gauche |
| **Tap à 2 doigts (`0 → 2`)** | Clic droit |
| **Mouvement 1 doigt + 1 tap (`1 → 2`)** | Clic gauche (maintenir = glisser gauche) |
| **Glisser à 3 doigts** | Glisser droit |
| **Tap à 3 doigts (`0 → 3`)** | Clic milieu |
| **Mouvement 1 doigt + 2 taps (`1 → 3`)** | Clic droit (maintenir = glisser droit) |
| **3D Droit → 2D maintien + 3e doigt** | Glisser droit + clic gauche simultanés (FPS) |
| **2D Gauche + 3e doigt** | Glisser gauche + clic droit simultanés |
| **4 doigts** | Natif Windows |

---

## Installation

### Option 1 : Installateur tout-en-un (Recommandé)
1. Téléchargez **[TouchpadGoodSetup.exe](https://github.com/wojo0821/TouchpadGood/releases/latest)** ([téléchargement direct](https://github.com/wojo0821/TouchpadGood/raw/main/TouchpadGoodSetup.exe)).
2. Lancez `TouchpadGoodSetup.exe` puis cliquez sur **Installer**.
   - S'installe dans `%LOCALAPPDATA%\Programs\TouchpadGood` et restaure automatiquement les paramètres Windows d'origine lors de la désinstallation.

### Option 2 : Compiler depuis les sources (Sans Visual Studio)
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

---

## Licence

Licence MIT — voir [LICENSE](LICENSE).
