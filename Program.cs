using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using UnturnedWorkshopAnalyst.Assets;
using System.Windows.Forms;

namespace ConflictScanner;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string? path = args.Length > 0 ? args[0] : null;
        if (path == null || !Uri.TryCreate(path, UriKind.Absolute, out Uri uri))
        {
            GetPath(out uri);
        }

        // nexus is needed to get the type dictionary that unturned uses for v1 assets.
        UnturnedNexus nexus = new UnturnedNexus();
        nexus.initialize();

        AssetReader reader = new AssetReader();

        refresh:
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Scanning...  ");
        Console.ForegroundColor = ConsoleColor.Gray;
        List<FileAsset> assets = reader.Scan(Uri.UnescapeDataString(uri.AbsolutePath));

        // stores lists of file assets separated by ID and type, ik this isn't a great way to do this but it works well and is pretty fast.
        Dictionary<EAssetType, Dictionary<ushort, List<FileAsset>>> duplicateIds = new Dictionary<EAssetType, Dictionary<ushort, List<FileAsset>>>();

        // stores lists of files per GUID.
        Dictionary<Guid, List<FileAsset>> duplicateGuids = new Dictionary<Guid, List<FileAsset>>();

        // files without a GUID.
        List<FileAsset> noGuid = new List<FileAsset>();

        for (int i = 0; i < assets.Count; ++i)
        {
            FileAsset file = assets[i];
            if (file.Category != EAssetType.NONE && file.Id > 0)
            {
                if (duplicateIds.TryGetValue(file.Category, out Dictionary<ushort, List<FileAsset>> duplicateIdsInCategory))
                {
                    if (duplicateIdsInCategory.TryGetValue(file.Id, out List<FileAsset> list))
                    {
                        // duplicate ID
                        list.Add(file);
                    }
                    else
                    {
                        // duplicate category, unique ID
                        duplicateIdsInCategory.Add(file.Id, new List<FileAsset> { file });
                    }
                }
                else duplicateIds.Add(file.Category, new Dictionary<ushort, List<FileAsset>> { { file.Id, new List<FileAsset> { file } } });
            }

            if (file.Guid == Guid.Empty)
            {
                // no GUID
                noGuid.Add(file);
            }
            else if (duplicateGuids.TryGetValue(file.Guid, out List<FileAsset> files))
            {
                // duplicate GUID
                files.Add(file);
            }
            else
                duplicateGuids.Add(file.Guid, new List<FileAsset> { file });
        }


        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Operation? (free <category> <start id>, guid, conflicts, refresh, path, exit/quit)");
            Console.ForegroundColor = ConsoleColor.Gray;
            string? input = Console.ReadLine();
            if (input == null)
                return;
            if (input.Equals("conflicts", StringComparison.InvariantCultureIgnoreCase))
            {
                if (noGuid.Count > 0)
                {
                    // files without GUIDs
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("No GUID:");
                    foreach (FileAsset file in noGuid)
                    {
                        Console.WriteLine(" " + file.AssetName);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("   " + Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(file.File.FullName, UriKind.Absolute)).ToString()));
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine();
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                }

                // files with overlapping GUIDs
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Duplicate GUIDs:");
                foreach (List<FileAsset> files in duplicateGuids.Values.Where(x => x.Count > 1))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(" " + files[0].Guid);
                    Console.ForegroundColor = ConsoleColor.Red;
                    foreach (FileAsset file in files)
                    {
                        Console.WriteLine("   " + file.AssetName);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("   " + Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(file.File.FullName, UriKind.Absolute)).ToString()));
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();
                Console.WriteLine();

                // files with overlapping IDs and Categories
                Console.WriteLine("Duplicate IDs:");
                foreach (List<FileAsset> files in duplicateIds.Values.SelectMany(x => x.Values).Where(x => x.Count > 1))
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

                    foreach (FileAsset file in files)
                    {
                        Console.WriteLine("   " + file.AssetName);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("   " + Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(file.File.FullName, UriKind.Absolute)).ToString()));
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    Console.WriteLine();
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done");
            }
            else if (input.StartsWith("free", StringComparison.InvariantCultureIgnoreCase))
            {
                int arg0 = input.IndexOf(' ');
                if (arg0 == -1 || arg0 == input.Length - 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("free <category> <start id>");
                    continue;
                }

                int arg1 = input.IndexOf(' ', arg0 + 1);
                if (arg1 == -1 || arg1 == input.Length - 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("free <category> <start id>");
                    continue;
                }

                if (!Enum.TryParse(input.Substring(arg0 + 1, arg1 - arg0 - 1), true, out EAssetType type) || !ushort.TryParse(input.Substring(arg1 + 1), NumberStyles.Number, CultureInfo.InvariantCulture, out ushort id) || type == EAssetType.NONE)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("free <category: EAssetType> <start id: UInt16>");
                    continue;
                }

                if (id == 0)
                    id = 1;

                if (!duplicateIds.TryGetValue(type, out Dictionary<ushort, List<FileAsset>> dict))
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"Free {type.ToString().ToLowerInvariant()} id: {id} (Copied).");
                    Clipboard.SetText(id.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                while (id < ushort.MaxValue)
                {
                    if (!dict.ContainsKey(id))
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine($"Free {type.ToString().ToLowerInvariant()} id: {id} (Copied).");
                        Clipboard.SetText(id.ToString(CultureInfo.InvariantCulture));
                        break;
                    }

                    ++id;
                }

                if (id == ushort.MaxValue)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"No free {type} ids.");
                }
            }
            else if (input.Equals("guid", StringComparison.InvariantCultureIgnoreCase))
            {
                // find a GUID which is not in use (yes I know how rare it would be to have a duplicate GUID)
                Guid guid;
                do
                {
                    guid = Guid.NewGuid();
                } while (duplicateGuids.ContainsKey(guid));

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"Free GUID: \"{guid:N}\" (Copied).");
                Clipboard.SetText(guid.ToString("N"));
            }
            else if (input.Equals("quit", StringComparison.InvariantCultureIgnoreCase) || input.Equals("exit", StringComparison.InvariantCultureIgnoreCase))
            {
                break;
            }
            else if (input.Equals("refresh", StringComparison.InvariantCultureIgnoreCase))
            {
                goto refresh;
            }
            else if (input.Equals("path", StringComparison.InvariantCultureIgnoreCase))
            {
                GetPath(out uri);
                goto refresh;
            }

            Console.WriteLine();
        }

        nexus.shutdown();
    }

    private static void GetPath(out Uri uri)
    {
        // get a path from console and validate it.
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Enter absolute root folder path:");
        while (true)
        {
            try
            {
                uri = new Uri(Console.ReadLine()!, UriKind.Absolute);
                string p = Uri.UnescapeDataString(uri.AbsolutePath);
                if (Directory.Exists(p))
                {
                    new FileIOPermission(FileIOPermissionAccess.Read, p).Demand();
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Directory does not exist.");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            catch (UriFormatException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid directory path.");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unable to access directory path.");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
    }
}