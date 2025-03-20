using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnturnedAssets;

namespace ConflictScanner;
internal static partial class Program
{
    private static void Conflicts()
    {
        if (_noGuid.Count > 0)
        {
            // files without GUIDs
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No GUID:");
            foreach (UnturnedAssetFile file in _noGuid)
            {
                Console.WriteLine(" " + file.AssetName);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("   " + FindRelativePath(file.File.FullName));
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine();
            }
            Console.WriteLine();
            Console.WriteLine();
        }

        // files with overlapping GUIDs
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Duplicate GUIDs:");
        foreach (List<UnturnedAssetFile> files in _duplicateGuids.Values.Where(x => x.Count > 1))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" " + files[0].Guid);
            Console.ForegroundColor = ConsoleColor.Red;
            foreach (UnturnedAssetFile file in files)
            {
                Console.WriteLine("   " + file.AssetName);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("   " + FindRelativePath(file.File.FullName));
                Console.ForegroundColor = ConsoleColor.Red;
            }
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine();

        // files with overlapping IDs and Categories
        Console.WriteLine("Duplicate IDs:");
        foreach (List<UnturnedAssetFile> files in _duplicateIds.Values.SelectMany(x => x.Values).Where(x => x.Count > 1))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" " + files[0].Category switch
            {
                EAssetType.ANIMAL => "Animals",
                EAssetType.EFFECT => "Effects",
                EAssetType.ITEM => "Items",
                EAssetType.MYTHIC => "Mythics",
                EAssetType.NPC => "NPCs",
                EAssetType.OBJECT => "Level Objects",
                EAssetType.RESOURCE => "Resources",
                EAssetType.SKIN => "Skins",
                EAssetType.SPAWN => "Spawns",
                EAssetType.VEHICLE => "Vehicles",
                var v => v.ToString()
            } + " / " + files[0].Id.ToString(CultureInfo.InvariantCulture));
            Console.ForegroundColor = ConsoleColor.Red;

            foreach (UnturnedAssetFile file in files)
            {
                Console.WriteLine("   " + file.AssetName);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("   " + FindRelativePath(file.File.FullName));
                Console.ForegroundColor = ConsoleColor.Red;
            }
            Console.WriteLine();
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Done");
    }
}