using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            Console.WriteLine("Enter absolute root folder path:");
            while (true)
            {
                try
                {
                    uri = new Uri(Console.ReadLine()!, UriKind.Absolute);
                    break;
                }
                catch (UriFormatException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid path.");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
        }
        UnturnedNexus nexus = new UnturnedNexus();
        nexus.initialize();
        AssetReader reader = new AssetReader();
        while (true)
        {
            List<FileAsset> assets = reader.Scan(Uri.UnescapeDataString(uri.AbsolutePath));
            Dictionary<EAssetType, Dictionary<ushort, List<FileAsset>>> duplicateIds = new Dictionary<EAssetType, Dictionary<ushort, List<FileAsset>>>();
            Dictionary<Guid, List<FileAsset>> duplicateGuids = new Dictionary<Guid, List<FileAsset>>();
            List<FileAsset> noGuid = new List<FileAsset>();
            for (int i = 0; i < assets.Count; ++i)
            {
                FileAsset file = assets[i];
                if (file.Category != EAssetType.NONE && file.Id > 0)
                {
                    if (duplicateIds.TryGetValue(file.Category, out Dictionary<ushort, List<FileAsset>> duplicateIdsInCategory))
                    {
                        if (duplicateIdsInCategory.TryGetValue(file.Id, out List<FileAsset> list))
                            list.Add(file);
                        else duplicateIdsInCategory.Add(file.Id, new List<FileAsset> { file });
                    }
                    else duplicateIds.Add(file.Category, new Dictionary<ushort, List<FileAsset>> { { file.Id, new List<FileAsset> { file } } });
                }

                if (file.Guid == Guid.Empty)
                {
                    noGuid.Add(file);
                }
                else if (duplicateGuids.TryGetValue(file.Guid, out List<FileAsset> files))
                    files.Add(file);
                else
                    duplicateGuids.Add(file.Guid, new List<FileAsset> { file });
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Operation? (free <start id> <category>, guid, conflicts, exit/quit)");
            string? input = Console.ReadLine();
            if (input == null)
                return;
            if (input.Equals("conflicts", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                if (noGuid.Count > 0)
                {
                    Console.WriteLine("No GUID:");
                    foreach (FileAsset file in noGuid)
                    {
                        Console.WriteLine(" " + file.AssetName);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("   " + Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(file.File.FullName, UriKind.Absolute)).ToString()));
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine();
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Duplicate GUIDs:");
                foreach (List<FileAsset> files in duplicateGuids.Values.Where(x => x.Count > 1))
                {
                    Console.WriteLine(" " + files[0].Guid);
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

                Console.WriteLine("Duplicate IDs:");
                foreach (List<FileAsset> files in duplicateIds.Values.SelectMany(x => x.Values).Where(x => x.Count > 1))
                {
                    Console.WriteLine(" " + files[0].Id + " @ " + files[0].Category);
                    foreach (FileAsset file in files)
                    {
                        Console.WriteLine("   " + file.AssetName);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("   " + Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(file.File.FullName, UriKind.Absolute)).ToString()));
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    Console.WriteLine();
                }

                Console.ForegroundColor = ConsoleColor.Gray;
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

                if (!Enum.TryParse(input.Substring(arg0 + 1, arg1 - arg0 - 1), true, out EAssetType type) || !ushort.TryParse(input.Substring(arg1 + 1), NumberStyles.Number, CultureInfo.InvariantCulture, out ushort id))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("free <category: EAssetType> <start id: UInt16>");
                    continue;
                }

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
                Guid guid;
                do
                {
                    guid = Guid.NewGuid();
                } while (duplicateGuids.ContainsKey(guid));

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"Free GUID: \"{guid:N}\" (Copied).");
                Clipboard.SetText(guid.ToString("N"));
            }
            else if (input.Equals("quit", StringComparison.InvariantCultureIgnoreCase) ||
                input.Equals("exit", StringComparison.InvariantCultureIgnoreCase)) break;

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Press [ENTER] to proceed.");

            input = Console.ReadLine();
            if (input == null)
                return;
            if (input.Equals("quit", StringComparison.InvariantCultureIgnoreCase) ||
                input.Equals("exit", StringComparison.InvariantCultureIgnoreCase)) break;
        }

        nexus.shutdown();
        Console.ReadLine();
    }
}