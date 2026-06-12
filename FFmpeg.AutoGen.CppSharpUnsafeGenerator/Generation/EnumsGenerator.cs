using System.Collections.Generic;
using System.Linq;
using FFmpeg.AutoGen.CppSharpUnsafeGenerator.Definitions;

namespace FFmpeg.AutoGen.CppSharpUnsafeGenerator.Generation;

internal sealed class EnumsGenerator : GeneratorBase<EnumerationDefinition>
{
    public EnumsGenerator(string path, GenerationContext context) : base(path, context)
    {
    }

    public override IEnumerable<string> Usings()
    {
        yield return "System";
    }

    public static void Generate(string path, GenerationContext context)
    {
        using var g = new EnumsGenerator(path, context);
        g.Generate();
    }

    protected override void GenerateDefinition(EnumerationDefinition @enum)
    {
        this.WriteSummary(@enum);
        this.WriteObsoletion(@enum);
        
        // if every item in the enum is in the form 1 << n, then we will treat it
        // as a flags enum
        if (@enum.Items.All(test => test.Value.Contains("<<")))
            WriteLine("[Flags]");
        
        WriteLine($"public enum {@enum.Name} : {@enum.TypeName}");

        using (BeginBlock())
            foreach (var item in @enum.Items)
            {
                this.WriteSummary(item);
                WriteLine($"@{item.Name} = {item.Value},");
            }

        WriteLine();
    }
}
