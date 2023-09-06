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
            dictionary.TryParseGuid("GUID", out guid);
            dictionary.TryParseUInt16("ID", out id);
        }
        if (guid == Guid.Empty)
        {
            ReportError(file, "GUID was empty.");
            return null;
        }
        if (assetType == null)
        {
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
        asset.CacheLocal(_parser);
        return asset;
    }
    private void ReportError(FileInfo file, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Asset \"{Path.GetFileNameWithoutExtension(file.FullName)}\" load error: \"{message}\".{Environment.NewLine}\tFile: {file.FullName}");
        Console.ResetColor();
    }

    private DatDictionary ReadFile(string path)
    {
        using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using StreamReader inputReader = new StreamReader(fileStream);
        // ReSharper disable once InconsistentlySynchronizedField
        return _parser.Parse(inputReader);
    }
}
