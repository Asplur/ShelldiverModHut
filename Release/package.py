import os, sys, zipfile

root = sys.argv[1]
out = sys.argv[2]
version = '1.0.0'
readme = os.path.join(root, 'Release', 'README.txt')

packages = {
    f'ShelldiverOfficeMods_{version}.zip': {
        'mods/managed/FrameBridge.dll': os.path.join(out, 'FrameBridge', 'FrameBridge.dll'),
        'mods/bubble/OfficeMod.dll': os.path.join(out, 'OfficeMod', 'OfficeMod.dll'),
        'mods/bubble/LandExtensionMod.dll': os.path.join(out, 'LandExtensionMod', 'LandExtensionMod.dll'),
        'mods/bubble/DevConsoleMod.dll': os.path.join(out, 'DevConsoleMod', 'DevConsoleMod.dll'),
        'mods/bubble/NewGamePlusMod.dll': os.path.join(out, 'NewGamePlusMod', 'NewGamePlusMod.dll'),
        'mods/bubble/Office/Office.png': os.path.join(root, 'OfficeMod', 'Office', 'Office.png'),
        'ShelldiverOfficeMods_README.txt': readme,
    },
    f'ShelldiverTrainer_{version}.zip': {
        'mods/bubble/TrainerMod.dll': os.path.join(out, 'TrainerMod', 'TrainerMod.dll'),
    },
}

for name, files in packages.items():
    path = os.path.join(root, 'Release', name)
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as archive:
        for arcname, source in files.items():
            archive.write(source, arcname)
    with zipfile.ZipFile(path) as archive:
        print(name, os.path.getsize(path))
        for info in archive.infolist():
            print(' ', info.filename, info.file_size)
