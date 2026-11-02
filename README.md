# Shelldiver Office Mods

Unofficial fan mods for [Shelldiver](https://store.steampowered.com/app/3862670/Shelldiver/): a new office with an in-game settings menu, new land, a dev console, New Game+ and an optional trainer.

Tested on Windows with the Steam version of Shelldiver v1.0.2.300.020.

## Downloads

| Download | Needed? |
|---|---|
| [.NET 9 Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0) | Required: choose the x64 installer under ".NET Runtime" |
| [AurieInstaller.exe](https://github.com/AurieFramework/Aurie/releases/download/v2.0.0b/AurieInstaller.exe) | Required |
| [BubbleLib v0.0.2](https://github.com/TP-repo/BubbleLib/releases/download/latest/BubbleLib_v0_0_2.zip) | Required |
| [Shelldiver Office Mods 1.0.0](https://github.com/Asplur/ShelldiverModHut/raw/main/Release/ShelldiverOfficeMods_1.0.0.zip) | Required: the main mod |
| [Shelldiver Trainer 1.0.0](https://github.com/Asplur/ShelldiverModHut/raw/main/Release/ShelldiverTrainer_1.0.0.zip) | Optional |

## Install

**Back up your save first:** copy `%APPDATA%\Shelldiver\save` somewhere safe.

1. Install the .NET 9 Runtime.
2. Run `AurieInstaller.exe`, tick **Allow C# mods**, click **Find my game!** and select `Shelldiver.exe`.
3. Extract the BubbleLib zip into your Shelldiver folder.
4. Extract the Shelldiver Office Mods zip into your Shelldiver folder, merging the `mods` folders.
5. Optional: extract the Trainer zip the same way.

Your Shelldiver folder is usually `C:\Program Files (x86)\Steam\steamapps\common\Shelldiver`.

Start the game. A **Mods** option on the main menu means it worked.

## Features

| Mod | How to use it |
|---|---|
| **Office** | Walk left from the hammock and press interact to open the menu |
| **Land** | New land on both sides of the village |
| **Dev Console** | Press the `` ` `` key. Commands: `help`, `clear`, `npc [on\|off]` |
| **New Game+** | On the main menu. Resets progress, keeps some bonuses and closes the game |
| **Trainer** | Optional. Open **Office > Mods > Trainer** |

## Office menu controls

| Action | Input |
|---|---|
| Move | W/S, arrow keys or mouse |
| Select | Enter, Space or left-click a label |
| Change a value | Mouse wheel, A/D, Left/Right, or left-click to lower and right-click to raise |
| Back or close | Esc |

## Known issues

- A game update or **Verify integrity of game files** can undo the install. Repeat steps 2 to 5.
- If Steam Cloud reports a save conflict after New Game+, choose the local files.
- The Trainer changes values the game may save, and Steam achievements may still trigger.
- The Office prompt always shows E, even if your interact key is different.
- Some antivirus tools may flag the mods, because they load code into the game at runtime.

## Uninstall

Verify the game files in Steam, then delete the `mods` folder from your Shelldiver folder. Saves are kept.

## Building from source

| Folder | Mod |
|---|---|
| `FrameBridge` | Drives BubbleLib's frame updates on YYToolkit 5.0.0c |
| `OfficeMod` | The office and its settings menu |
| `LandExtensionMod` | New land on both sides of the village |
| `DevConsoleMod` | Dev console and commands |
| `Mod` | New Game+ |
| `TrainerMod` | Optional trainer |

Install the requirements first, because the projects reference `BubbleLib.dll` and `auriesharpinterop.dll` from the game's `mods` folder. If your game isn't at the default path in the project files, change `GameDir` in each `.csproj`.

```
dotnet build TrainerMod/TrainerMod.csproj -c Release
```

## Credits

BubbleLib by TP-repo. Aurie, YYToolkit and AurieSharp by the AurieFramework project. Shelldiver by Gagonfe. This project is not affiliated with Gagonfe.
