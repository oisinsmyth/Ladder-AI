using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;

namespace Converter;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("to-ir" or "to-xml"))
        {
            Console.Error.WriteLine("Usage: converter to-ir|to-xml <file> [<file> ...]");
            return 1;
        }

        var mode = args[0];
        var files = args[1..];

        foreach (var file in files)
        {
            try
            {
                if (mode == "to-ir")
                {
                    ConvertToIr(file);
                }
                else
                {
                    ConvertToXml(file);
                }
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException)
            {
                Console.Error.WriteLine($"{file}: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        return 0;
    }

    private static void ConvertToIr(string sourcePath)
    {
        var document = XDocument.Load(sourcePath);
        var block = BlockSourceParser.Parse(document);

        var networks = new List<IrNetwork>();
        var sidecars = new List<NetworkSidecar>();

        foreach (var compileUnit in block.CompileUnits)
        {
            var title = compileUnit.Comment ?? string.Empty;
            var reduced = GraphReducer.Reduce(compileUnit.Network, networks.Count + 1, title, compileUnit.UId);
            networks.Add(reduced.Network);
            sidecars.Add(reduced.Sidecar);
        }

        var irBlock = new IrBlock(block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, networks);
        var irText = IrSerializer.SerializeBlock(irBlock, sidecars);

        var outPath = Path.ChangeExtension(sourcePath, ".ir");
        File.WriteAllText(outPath, irText);
        Console.WriteLine($"{sourcePath} -> {outPath}");
    }

    private static void ConvertToXml(string sourcePath)
    {
        var irText = File.ReadAllText(sourcePath);
        var (block, sidecars) = IrParser.ParseBlock(irText);

        var flgNetworks = new List<FlgNetwork>();
        var compileUnitUIds = new List<string>();
        var networkComments = new List<string?>();

        for (var i = 0; i < block.Networks.Count; i++)
        {
            var sidecar = sidecars[i];
            flgNetworks.Add(FlgNetBuilder.Build(block.Networks[i], sidecar));
            compileUnitUIds.Add(sidecar.CompileUnitUId);
            networkComments.Add(string.IsNullOrEmpty(block.Networks[i].Title) ? null : block.Networks[i].Title);
        }

        var blockSource = new BlockSource(block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, Array.Empty<CompileUnitSource>());
        var xml = BlockSourceWriter.Write(blockSource, flgNetworks, compileUnitUIds, networkComments);

        var outPath = Path.ChangeExtension(sourcePath, ".xml");
        xml.Save(outPath);
        Console.WriteLine($"{sourcePath} -> {outPath}");
    }
}
