using System;
using System.Linq;
using System.Text.RegularExpressions;
using CppSharp.AST;
using CppSharp.AST.Extensions;
using FFmpeg.AutoGen.CppSharpUnsafeGenerator.Definitions;

namespace FFmpeg.AutoGen.CppSharpUnsafeGenerator.Processing;

internal partial class EnumerationProcessor
{
    private readonly ProcessingContext _context;

    public EnumerationProcessor(ProcessingContext context) => _context = context;

    public void Process(TranslationUnit translationUnit)
    {
        foreach (var enumeration in translationUnit.Enums)
        {
            if (!enumeration.Type.IsPrimitiveType()) throw new NotSupportedException();

            var enumerationName = enumeration.Name;
            if (string.IsNullOrEmpty(enumerationName))
            {
                enumerationName = DeriveNameFromMembers(enumeration);
                if (enumerationName == null) continue;
            }

            MakeDefinition(enumeration, enumerationName);
        }
    }

    /// <summary>
    /// Derives a synthetic enum name from member names by finding the longest common prefix.
    /// E.g. AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX, AV_CODEC_HW_CONFIG_METHOD_INTERNAL
    /// → common prefix "AV_CODEC_HW_CONFIG_METHOD_" → PascalCase "AvCodecHwConfigMethod"
    /// </summary>
    private static string DeriveNameFromMembers(Enumeration enumeration)
    {
        var items = enumeration.Items;
        if (items.Count == 0) return null;

        // Find longest common prefix up to last underscore
        var prefix = items[0].Name;
        foreach (var item in items.Skip(1))
        {
            var len = 0;
            while (len < prefix.Length && len < item.Name.Length && prefix[len] == item.Name[len])
                len++;
            prefix = prefix[..len];
        }

        // Trim to last underscore boundary
        var lastUnderscore = prefix.LastIndexOf('_');
        if (lastUnderscore <= 0) return null;
        prefix = prefix[..(lastUnderscore + 1)];

        // Need at least 2 segments (e.g. "AV_SOMETHING_")
        if (prefix.Count(c => c == '_') < 2) return null;

        // Convert to PascalCase: "AV_CODEC_HW_CONFIG_METHOD_" → "AvCodecHwConfigMethod"
        var parts = prefix.TrimEnd('_').Split('_');
        var name = string.Concat(parts.Select(p =>
            p.Length <= 1 ? p.ToUpperInvariant() : char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));

        return name;
    }

    public void MakeDefinition(Enumeration enumeration, string name)
    {
        name = string.IsNullOrEmpty(enumeration.Name) ? name : enumeration.Name;
        if (_context.Definitions.Any(d => d.Name == name)) return;

        var definition = new EnumerationDefinition
        {
            Name = name,
            TypeName = TypeHelper.GetTypeName(enumeration.Type),
            Content = enumeration.Comment?.BriefText,
            Obsoletion = ObsoletionHelper.CreateObsoletion(enumeration),
            Items = enumeration.Items
                .Select(x =>
                    new EnumerationItem
                    {
                        Name = x.Name,
                        Value = ConvertValue(x.Expression, x.Value, enumeration.BuiltinType.Type).ToString(),
                        Content = x.Comment?.BriefText
                    })
                .ToArray()
        };

        _context.AddDefinition(definition);
    }

    // A regular expression that looks if the expression ends in "= 1 << n" (with any
    // number of spaces between the elements.
    [GeneratedRegex(@"=\s*1\s*<<\s*(\d+)\s*$")]
    private static partial Regex CheckForBitExpression();

    private static string ConvertValue(string expression, ulong value, PrimitiveType primitiveType)
    {
        // Check if the expression is of the form 1 << n. If it is, preserve the original definition
        // instead of collapsing the value.
        if (CheckForBitExpression().Match(expression) is { Success: true } match)
        {
            // Keep it as a 1 << n in the output. We will always
            // return it in the form "1 << n" with that exact spacing.
            var suffix = primitiveType switch
            {
                PrimitiveType.Int => "",
                PrimitiveType.UInt => "u",
                PrimitiveType.Long => "l",
                PrimitiveType.ULong => "ul",
                _ => throw new NotSupportedException()
            };
            
            return $"1{suffix} << {match.Groups[1].Value}";
        }

        // Otherwise, fallback on using the compiler's value
        object compilerValue = primitiveType switch
        {
            PrimitiveType.Int => value > int.MaxValue ? (int)value : value,
            PrimitiveType.UInt => value,
            PrimitiveType.Long => value > long.MaxValue ? (long)value : value,
            PrimitiveType.ULong => value,
            _ => throw new NotSupportedException()
        };
        return compilerValue.ToString();
    }
}
