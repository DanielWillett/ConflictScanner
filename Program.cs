using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnturnedAssets;

namespace ConflictScanner;

internal static partial class Program
{
    private static Dictionary<Guid, List<UnturnedAssetFile>> _duplicateGuids = null!;
    private static Dictionary<EAssetType, Dictionary<ushort, List<UnturnedAssetFile>>> _duplicateIds = null!;
    private static List<UnturnedAssetFile> _noGuid = null!;
    private static List<UnturnedAssetFile> _assets = null!;
    private static string[] _paths = null!;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            GetPaths(out _paths);
        }
        else
        {
            _paths = args;
            foreach (string arg in args)
            {
                if (Directory.Exists(arg))
                    continue;

                Console.WriteLine($"Directory not found: \"{arg}\".");
                return 3;
            }
        }

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());

        UnturnedAssetFinder reader = new UnturnedAssetFinder(loggerFactory.CreateLogger<UnturnedAssetFinder>());

        refresh:
        _assets = new List<UnturnedAssetFile>(1024);
        (int X, int Y) cursorPos = default;
        bool isFirst = true;
        foreach (string srcPath in _paths)
        {
            if (!isFirst)
            {
                Console.SetCursorPosition(0, cursorPos.Y);
            }
            else isFirst = false;
                          // Found 000000 in \"...\"
            string write = $"Scanning...     \"{srcPath}\"";

            Console.ForegroundColor = ConsoleColor.DarkGray;

            Console.WriteLine(write);

            Console.ForegroundColor = ConsoleColor.Gray;

            cursorPos = Console.GetCursorPosition();

            List<UnturnedAssetFile> lclAssets;
            try
            {
                lclAssets = reader.ScanAsync(srcPath).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error scanning files in \"{srcPath}\".");
                Console.WriteLine(ex);
                continue;
            }

            (int X, int Y) consolePos2 = Console.GetCursorPosition();

            Console.ForegroundColor = ConsoleColor.DarkGray;

            if (!cursorPos.Equals(consolePos2))
            {
                write = $"Found {_assets.Count + lclAssets.Count,5} assets | +{lclAssets.Count}      ";
                Console.WriteLine(write);
            }
            else
            {
                write = $"Found {lclAssets.Count}";
                int spaces = 12 - write.Length;
                if (spaces > 0)
                    write += new string(' ', spaces);
                write += " in ";

                Console.SetCursorPosition(0, cursorPos.Y - 1);
                Console.Write(write);
                Console.SetCursorPosition(consolePos2.X, consolePos2.Y);
            }

            Console.ForegroundColor = ConsoleColor.Gray;


            _assets.AddRange(lclAssets);
        }

        if (!isFirst)
            Console.SetCursorPosition(0, cursorPos.Y);

        Console.WriteLine($"Found {_assets.Count,5} assets            ");

        if (_paths.Length == 0)
        {
            Console.WriteLine("No results found.");
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

        // stores lists of file assets separated by ID and type, ik this isn't a great way to do this but it works well and is pretty fast.
        _duplicateIds = new Dictionary<EAssetType, Dictionary<ushort, List<UnturnedAssetFile>>>();

        // stores lists of files per GUID.
        _duplicateGuids = new Dictionary<Guid, List<UnturnedAssetFile>>();

        // files without a GUID.
        _noGuid = new List<UnturnedAssetFile>();

        for (int i = 0; i < _assets.Count; ++i)
        {
            UnturnedAssetFile file = _assets[i];
            if (file.Category != EAssetType.NONE && file.Id > 0)
            {
                if (_duplicateIds.TryGetValue(file.Category, out Dictionary<ushort, List<UnturnedAssetFile>>? duplicateIdsInCategory))
                {
                    if (duplicateIdsInCategory.TryGetValue(file.Id, out List<UnturnedAssetFile>? list))
                    {
                        // duplicate ID
                        list.Add(file);
                    }
                    else
                    {
                        // duplicate category, unique ID
                        duplicateIdsInCategory.Add(file.Id, new List<UnturnedAssetFile> { file });
                    }
                }
                else _duplicateIds.Add(file.Category, new Dictionary<ushort, List<UnturnedAssetFile>> { { file.Id, new List<UnturnedAssetFile> { file } } });
            }

            if (file.Guid == Guid.Empty)
            {
                // no GUID
                _noGuid.Add(file);
            }
            else if (_duplicateGuids.TryGetValue(file.Guid, out List<UnturnedAssetFile>? files))
            {
                // duplicate GUID
                files.Add(file);
            }
            else
                _duplicateGuids.Add(file.Guid, new List<UnturnedAssetFile> { file });
        }


        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Operation? (free <category> <start id>, guid, conflicts, refresh, path, find [type] [search], query <x => x.Dictionary[\"???\"] == ???>, exit/quit)");
            Console.ForegroundColor = ConsoleColor.Gray;
            string? input = Console.ReadLine();
            if (input == null)
                return 0;
            if (input.StartsWith("conflicts", StringComparison.InvariantCultureIgnoreCase))
            {
                Conflicts();
            }
            else if (input.StartsWith("free", StringComparison.InvariantCultureIgnoreCase))
            {
                FreeId(input);
            }
            else if (input.StartsWith("guid", StringComparison.InvariantCultureIgnoreCase))
            {
                FreeGuid();
            }
            else if (input.StartsWith("find", StringComparison.InvariantCultureIgnoreCase))
            {
                Find(input.Substring(4));
            }
            else if (input.StartsWith("query", StringComparison.InvariantCultureIgnoreCase))
            {
                Query(input.Substring(5));
            }
            else if (input.Equals("quit", StringComparison.InvariantCultureIgnoreCase) || input.Equals("exit", StringComparison.InvariantCultureIgnoreCase))
            {
                break;
            }
            else if (input.Equals("clear", StringComparison.InvariantCultureIgnoreCase) || input.Equals("cls", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.Clear();
                Console.WriteLine();
                continue;
            }
            else if (input.StartsWith("refresh", StringComparison.InvariantCultureIgnoreCase))
            {
                goto refresh;
            }
            else if (input.StartsWith("path", StringComparison.InvariantCultureIgnoreCase))
            {
                GetPaths(out _paths);
                goto refresh;
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static void GetPaths(out string[] uris)
    {
        // get a path from console and validate it.
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Enter a result set: (or an installed workshop ID, 'all' (a) for all vanlla + workshop, 'enabled' (e) for all vanlla + **enabled** workshop, 'vanilla' (v) for just vanilla assets):");
        Console.WriteLine("  'all'    /'a': All installed vanilla and workshop content.");
        Console.WriteLine("  'enabled'/'e': All installed vanilla and workshop content enabled in the 'Workshop -> Manage' menu.");
        Console.WriteLine("  'vanilla'/'v': All vanilla content including the 'Maps' folder.");
        Console.WriteLine("  <workshop id>: One or more workshop item ID separated by commas.");
        Console.WriteLine("  <folder>:      One or more absolute folder path separated by commas.");
        while (true)
        {
            try
            {
                string? txt = Console.ReadLine();
                if (txt == null)
                {
                    uris = Array.Empty<string>();
                    break;
                }

                if (Directory.Exists(txt))
                {
                    uris = [ txt ];
                    return;
                }

                bool isAll = txt.Equals("all", StringComparison.InvariantCultureIgnoreCase) || txt.Equals("a", StringComparison.InvariantCultureIgnoreCase);
                bool isVanilla = txt.Equals("vanilla", StringComparison.InvariantCultureIgnoreCase) || txt.Equals("v", StringComparison.InvariantCultureIgnoreCase);
                bool isAllEnabled = txt.Equals("enabled", StringComparison.InvariantCultureIgnoreCase) || txt.Equals("e", StringComparison.InvariantCultureIgnoreCase);
                ulong[]? modIds = null;
                bool isModId = false;
                if (!isAll && !isVanilla && !isAllEnabled)
                {
                    string[] modIdTxts = txt.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    modIds = new ulong[modIdTxts.Length];
                    isModId = modIds.Length > 0;
                    for (int i = 0; i < modIdTxts.Length; i++)
                    {
                        string modIdTxt = modIdTxts[i];
                        if (!ulong.TryParse(modIdTxt, NumberStyles.Number, CultureInfo.InvariantCulture, out ulong modId) || modId == 0)
                        {
                            isModId = false;
                            break;
                        }

                        modIds[i] = modId;
                    }
                }

                List<string> dirs = new List<string>();

                if (isAll || isVanilla || isAllEnabled || isModId && modIds != null)
                {
                    string? steamDir =
                        (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null)
                         ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null))?.ToString();

                    string libraryFilePath;
                    if (steamDir == null || !Directory.Exists(steamDir) || !File.Exists(libraryFilePath = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf")))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Steam installation not found.");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        continue;
                    }

                    Regex findPathRegex = new Regex(@"\""path\""\s*\""([^\n\r]*)\""(?=.*\""304930\"")", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant);

                    MatchCollection match = findPathRegex.Matches(File.ReadAllText(libraryFilePath));
                    string libraryDir;
                    string unturnedInstallDir;
                    if (match.Count == 0 || match[^1].Groups.Count <= 1 || !Directory.Exists(unturnedInstallDir = Path.Combine(libraryDir = match[^1].Groups[1].Value, "steamapps", "common", "Unturned")))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Unturned installation not found.");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        continue;
                    }

                    string workshopInstallDir = Path.Combine(libraryDir, "steamapps", "workshop", "content", "304930");

                    string tempDir;
                    if (isVanilla || isAll || isAllEnabled)
                    {
                        tempDir = Path.Combine(unturnedInstallDir, "Sandbox");
                        if (Directory.Exists(tempDir))
                            dirs.Add(tempDir);

                        tempDir = Path.Combine(unturnedInstallDir, "Bundles");
                        if (Directory.Exists(tempDir))
                            dirs.Add(tempDir);

                        tempDir = Path.Combine(unturnedInstallDir, "Maps");
                        if (Directory.Exists(tempDir))
                        {
                            foreach (string mapDir in Directory.EnumerateDirectories(tempDir, "*", SearchOption.TopDirectoryOnly))
                            {
                                tempDir = Path.Combine(mapDir, "Bundles");
                                if (Directory.Exists(tempDir))
                                    dirs.Add(tempDir);
                            }
                        }
                    }

                    if ((isAll || isModId || isAllEnabled) && Directory.Exists(workshopInstallDir))
                    {
                        List<ulong>? disabledMods = null;
                        foreach (string workshopDir in Directory.EnumerateDirectories(workshopInstallDir, "*", SearchOption.TopDirectoryOnly))
                        {
                            if (!TryGetUgcType(workshopDir, out ESteamUGCType type) || type == ESteamUGCType.LOCALIZATION)
                                continue;

                            ulong.TryParse(Path.GetFileName(workshopDir), NumberStyles.Number, CultureInfo.InvariantCulture, out ulong thisModId);

                            if (!isAll && !isAllEnabled && modIds != null && Array.IndexOf(modIds, thisModId) == -1)
                            {
                                continue;
                            }

                            if (isAllEnabled && !IsEnabled(thisModId, unturnedInstallDir, ref disabledMods))
                            {
                                continue;
                            }

                            tempDir = type == ESteamUGCType.MAP
                                ? Path.Combine(workshopDir, "Bundles")
                                : workshopDir;

                            if (Directory.Exists(tempDir))
                                dirs.Add(tempDir);
                        }
                    }

                    uris = dirs.ToArray();
                    break;
                }

                string[] splitDirs = txt.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                bool isError = false;
                foreach (string str in splitDirs)
                {
                    if (Directory.Exists(str))
                    {
                        dirs.Add(txt);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Directory does not exist: \"{str}\".");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        isError = true;
                        break;
                    }
                }

                if (isError)
                    continue;
   
                uris = dirs.ToArray();
                break;
            }
            catch (UriFormatException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid directory path.");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unable to access directory path.");
                Console.WriteLine(ex);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
    }

    private static bool IsEnabled(ulong modId, string installDir, ref List<ulong>? disabledMods)
    {
        if (disabledMods != null)
        {
            return !disabledMods.Contains(modId);
        }

        string convenientSavedata = Path.Combine(installDir, "Cloud", "ConvenientSavedata.json");
        disabledMods = new List<ulong>(0);
        if (!File.Exists(convenientSavedata))
        {
            return false;
        }

        JsonDocument document;
        using (FileStream fs = new FileStream(convenientSavedata, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            document = JsonDocument.Parse(fs, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }

        using (document)
        {
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("Booleans", out JsonElement element)
                && element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.False
                        && property.Name.StartsWith("Enabled_Workshop_Item_", StringComparison.Ordinal)
                        && property.Name.Length > 22
                        && ulong.TryParse(property.Name.AsSpan(22), NumberStyles.Number, CultureInfo.InvariantCulture, out ulong fileId))
                    {
                        if (property.Value.ValueKind == JsonValueKind.False)
                            disabledMods.Add(fileId);
                    }
                }
            }
        }

        return !disabledMods.Contains(modId);
    }

    public static bool TryGetUgcType(string directory, out ESteamUGCType type)
    {
        if (File.Exists(Path.Combine(directory, "Localization.meta")))
        {
            type = ESteamUGCType.LOCALIZATION;
            return true;
        }

        if (File.Exists(Path.Combine(directory, "Object.meta")))
        {
            type = ESteamUGCType.OBJECT;
            return true;
        }

        if (File.Exists(Path.Combine(directory, "Item.meta")))
        {
            type = ESteamUGCType.ITEM;
            return true;
        }

        if (File.Exists(Path.Combine(directory, "Vehicle.meta")))
        {
            type = ESteamUGCType.VEHICLE;
            return true;
        }

        if (File.Exists(Path.Combine(directory, "Skin.meta")))
        {
            type = ESteamUGCType.SKIN;
            return true;
        }

        if (File.Exists(Path.Combine(directory, "Map.meta")))
        {
            type = ESteamUGCType.MAP;
            return true;
        }

        type = 0;
        return false;
    }

    private struct AssetConsoleMeasurements
    {
        public readonly int MaxAssetNameLength;
        public readonly int MaxFriendlyNameLength;
        public readonly int MaxTypeNameLength;
        public AssetConsoleMeasurements(IEnumerable<UnturnedAssetFile> files)
        {
            int maxAssetName = 8;
            int maxFriendlyName = 8;
            int maxTypeName = 0;
            foreach (UnturnedAssetFile file in files)
            {
                if (file.AssetName.Length > maxAssetName)
                    maxAssetName = file.AssetName.Length;
                if (file.FriendlyName != null)
                {
                    string fn = TerminalColorHelper.ConvertRichTextToVirtualTerminalSequences(file.FriendlyName, StackColorFormatType.None);

                    if (fn.Length > maxFriendlyName)
                        maxFriendlyName = fn.Length;
                }
                if (file.AssetType.Name.Length > maxTypeName)
                    maxTypeName = file.AssetType.Name.Length;
            }

            MaxAssetNameLength = maxAssetName;
            MaxFriendlyNameLength = maxFriendlyName;
            MaxTypeNameLength = maxTypeName;
        }
    }

    private static string GetAssetSource(UnturnedAssetFile file)
    {
        string fileName = file.File.Name;
        if (fileName.Equals("Asset.dat", StringComparison.Ordinal))
            fileName = Path.GetFileName(Path.GetDirectoryName(file.File.FullName)!);

        string maps = Path.DirectorySeparatorChar + "Maps" + Path.DirectorySeparatorChar;
        foreach (string path in _paths)
        {
            if (ContainsDir(path, file.File.FullName))
            {
                ReadOnlySpan<char> span = path.AsSpan();
                if (span.Contains(maps, StringComparison.Ordinal))
                {
                    return Path.GetFileName(Path.GetDirectoryName(path)!) + "/" + Path.GetFileName(path) + "/.../" + fileName;
                }
                    

                return Path.GetFileName(path) + "/.../" + fileName;
            }
        }

        return "???/" + fileName;
    }

    private static string FindRelativePath(string file)
    {
        foreach (string path in _paths)
        {
            if (ContainsDir(path, file))
                return Path.Combine(Path.GetFileName(path), Path.GetRelativePath(path, file));
        }

        return file;
    }

    private static bool ContainsDir(string small, string big)
    {
        DirectoryInfo smallDir = new DirectoryInfo(small);
        DirectoryInfo bigDir = new DirectoryInfo(big);

        for (DirectoryInfo? temp; (temp = bigDir.Parent) != null; bigDir = temp)
        {
            if (temp.FullName == smallDir.FullName)
                return true;
        }

        return false;
    }

    private static void WriteAssetToConsoleHeader(in AssetConsoleMeasurements measurements)
    {
        ConsoleColor oldColor = Console.ForegroundColor;
        ConsoleColor oldBkgr = Console.BackgroundColor;

        Console.BackgroundColor = ConsoleColor.DarkGray;
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("## \\/ ");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("GUID                             ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("ID    ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"{Pad("Asset Name", measurements.MaxAssetNameLength)} ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{Pad("Friendly Name", measurements.MaxFriendlyNameLength)} ");

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"{Pad("Asset Type", measurements.MaxTypeNameLength)}");

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.BackgroundColor = oldBkgr;
        Console.Write(" Source");

        Console.ForegroundColor = oldColor;

        Console.WriteLine();
    }
    private static void WriteAssetToConsole(int index, UnturnedAssetFile file, in AssetConsoleMeasurements measurements)
    {
        ConsoleColor oldColor = Console.ForegroundColor;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"{index + 1, -5} ");

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write($"{file.Guid:N} ");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($"{file.Id, -5} ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{Pad(file.AssetName, measurements.MaxAssetNameLength)} ");
        if (!string.IsNullOrWhiteSpace(file.FriendlyName))
        {
            string fn = TerminalColorHelper.ConvertRichTextToVirtualTerminalSequences(file.FriendlyName, StackColorFormatType.ExtendedANSIColor, argbForeground: (int)ConsoleColor.DarkCyan);
            if (ReferenceEquals(fn, file.FriendlyName))
                Console.Write($"{Pad(fn, measurements.MaxFriendlyNameLength)} ");
            else
            {
                int charLen = TerminalColorHelper.ConvertRichTextToVirtualTerminalSequences(file.FriendlyName, StackColorFormatType.None).Length;
                Console.Write($"{Pad(fn, charLen, measurements.MaxFriendlyNameLength)} ");
            }
        }
        else
        {
            for (int i = 0; i <= measurements.MaxFriendlyNameLength; ++i)
                Console.Write(' ');
        }

        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.Write($"{Pad(file.AssetType.Name, measurements.MaxTypeNameLength)} ");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(GetAssetSource(file));

        Console.ForegroundColor = oldColor;

        Console.WriteLine();
    }
    private static string Pad(string str, int amt)
    {
        if (str.Length == amt)
            return str;

        return str.Length >= amt
            ? str.Substring(0, amt)
            : string.Create(amt, str, (span, state) =>
            {
                state.AsSpan().CopyTo(span);
                span.Slice(state.Length).Fill(' ');
            });
    }
    private static string Pad(string str, int len, int amt)
    {
        if (len == amt)
            return str;

        return len >= amt
            ? str.Substring(0, amt)
            : string.Create(str.Length + (amt - len), str, (span, state) =>
            {
                state.AsSpan().CopyTo(span);
                span.Slice(state.Length).Fill(' ');
            });
    }
}