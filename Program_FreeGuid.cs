using System;
using System.Windows.Forms;

namespace ConflictScanner;
internal static partial class Program
{
    private static void FreeGuid()
    {
        // find a GUID which is not in use (yes I know how rare it would be to have a duplicate GUID)
        Guid guid;
        do
        {
            guid = Guid.NewGuid();
        } while (_duplicateGuids.ContainsKey(guid));

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"Free GUID: \"{guid:N}\" (Copied).");
        Clipboard.SetText(guid.ToString("N"));
    }
}