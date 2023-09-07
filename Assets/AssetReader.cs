using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;

namespace UnturnedWorkshopAnalyst.Assets;
internal class AssetReader
{
    private readonly DatParser _parser = new DatParser();

    public List<FileAsset> Scan(string folder)
    {
        lock (_parser)
        {
            List<FileAsset> rtn = new List<FileAsset>(32);
            DirectoryInfo dir = new DirectoryInfo(folder);
            foreach (FileInfo info in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                // looks for a lone asset file
                if (info.Extension.Equals(".asset", StringComparison.Ordinal))
                {
                    string? dirName = Path.GetFileName(Path.GetDirectoryName(info.FullName));
                    if (dirName != null && !dirName.Equals(Path.GetFileNameWithoutExtension(info.FullName), StringComparison.Ordinal))
                    {
                        if (File.Exists(Path.Combine(dirName, dirName + ".asset")) ||
                            File.Exists(Path.Combine(dirName, dirName + ".dat")) ||
                            File.Exists(Path.Combine(dirName, "Asset.dat")))
                            continue;
                        
                    }
                }
                // looks for a dat file in its folder
                else if (info.Extension.Equals(".dat", StringComparison.Ordinal))
                {
                    string? dirName = Path.GetFileName(Path.GetDirectoryName(info.FullName));
                    if (dirName != null && !dirName.Equals(Path.GetFileNameWithoutExtension(info.FullName), StringComparison.Ordinal))
                        continue;
                }
                else continue;
                DatDictionary dict = ReadFile(info.FullName);
                FileAsset? asset = TryRead(dict, info);
                if (asset != null)
                {
                    asset.CacheLocal(_parser);
                    rtn.Add(asset);
                }
            }
            Console.WriteLine($"Assets discovered: {rtn.Count}.");

            return rtn;
        }
    }
    public FileAsset? TryRead(DatDictionary dictionary, FileInfo file)
    {
        Guid guid;
        ushort id = 0;
        Type? assetType = null;
        // v2 metadata
        if (dictionary.TryGetDictionary("Metadata", out DatDictionary metadata))
        {
            if (!metadata.TryParseGuid("Guid", out guid))
            {
                ReportError(file, "Missing Metadata.Guid property.");
                return null;
            }
            assetType = metadata.ParseType("Type");
            if (assetType == null)
            {
                ReportError(file, "Missing Metadata.Type property.");
                return null;
            }
        }
        else
        {
            // v1 metadata
            dictionary.TryParseGuid("GUID", out guid);
            dictionary.TryParseUInt16("ID", out id);
        }
        if (assetType == null)
        {
            // v1 metadata
            if (!dictionary.TryGetString("Type", out string typeStr))
            {
                ReportError(file, "Missing Type property.");
                return null;
            }

            assetType = SDG.Unturned.Assets.assetTypes.getType(typeStr);

            if (assetType == null)
            {
                assetType = dictionary.ParseType("Type");

                if (assetType == null)
                {
                    ReportError(file, $"Unknown value: \"{typeStr}\" for Type property.");
                    return null;
                }
            }
        }

        if (!typeof(Asset).IsAssignableFrom(assetType))
        {
            ReportError(file, $"Type {assetType.FullName} is not assignable from Asset.");
            return null;
        }

        FileAsset asset = new FileAsset(assetType, file, guid, id, dictionary);
        return asset;
    }
    private static void ReportError(FileInfo file, string message)
    {
        ConsoleColor clr = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Asset \"{Path.GetFileNameWithoutExtension(file.FullName)}\" load error: \"{message}\".{Environment.NewLine}\tFile: {file.FullName}");
        Console.ForegroundColor = clr;
    }

    private DatDictionary ReadFile(string path)
    {
        using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using StreamReader inputReader = new StreamReader(fileStream);
        // ReSharper disable once InconsistentlySynchronizedField
        return _parser.Parse(inputReader);
    }
}
