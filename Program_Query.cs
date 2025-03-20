using System;
using System.Collections.Generic;
using System.Linq;
using UnturnedAssets;
using Z.Expressions;

namespace ConflictScanner;
internal static partial class Program
{
    private static void Query(string input)
    {
        EvalContext context = new EvalContext();
        context.IsCaseSensitive = false;
        context.UseCache = false;
        context.VariableFactory = argument =>
        {
            Console.WriteLine($"{argument.Name} - {argument.Value}");
        };

        try
        {
            List<UnturnedAssetFile> assets = _assets.WhereDynamic(input.Trim()).ToList();

            AssetConsoleMeasurements m = new AssetConsoleMeasurements(assets);

            WriteAssetToConsoleHeader(in m);
            for (int i = assets.Count - 1; i >= 0; i--)
            {
                WriteAssetToConsole(i, assets[i], in m);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }
}