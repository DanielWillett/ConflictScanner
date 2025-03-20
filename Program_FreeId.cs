using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using UnturnedAssets;

namespace ConflictScanner;
internal static partial class Program
{
    private static void FreeId(string input)
    {
        int arg0 = input.IndexOf(' ');
        if (arg0 == -1 || arg0 == input.Length - 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("free <category> <start id>");
            return;
        }

        int arg1 = input.IndexOf(' ', arg0 + 1);
        if (arg1 == -1 || arg1 == input.Length - 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("free <category> <start id>");
            return;
        }

        if (!Enum.TryParse(input.Substring(arg0 + 1, arg1 - arg0 - 1), true, out EAssetType type) || !ushort.TryParse(input.Substring(arg1 + 1), NumberStyles.Number, CultureInfo.InvariantCulture, out ushort id) || type == EAssetType.NONE)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("free <category: EAssetType> <start id: UInt16>");
            return;
        }

        if (id == 0)
            id = 1;

        if (!_duplicateIds.TryGetValue(type, out Dictionary<ushort, List<UnturnedAssetFile>>? dict))
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"Free {type.ToString().ToLowerInvariant()} id: {id} (Copied).");
            Clipboard.SetText(id.ToString(CultureInfo.InvariantCulture));
            return;
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
}