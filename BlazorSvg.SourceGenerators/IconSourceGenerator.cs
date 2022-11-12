using Microsoft.CodeAnalysis;
using BlazorSvg.SourceGenerators.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections;
using System;

namespace BlazorSvg.SourceGenerators
{
    /// Based on https://github.com/sanchez/dansui/blob/master/Sanchez.DansUI.Icons/IconSourceGenerator.cs
    [Generator]
    public class IconSourceGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor AdditionalFilesNotFound = new DiagnosticDescriptor(
            id: "GSVG001",
            title: "AdditionalFiles not found",
            messageFormat: "You have an enum member named '{0}', but no corresponding file named '{1}' was referenced as an <AdditionalFiles> in your project file",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );
        private static readonly DiagnosticDescriptor TypePropertyNotFound = new DiagnosticDescriptor(
            id: "GSVG002",
            title: "Type property not found",
            messageFormat: "The specified type property '{0}' was not found in your class",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );
        private static readonly DiagnosticDescriptor AdditionalAttributesPropertyNotFound = new DiagnosticDescriptor(
            id: "GSVG003",
            title: "AdditionalAttributes property not found",
            messageFormat: "The specified additional attributes property '{0}' was not found in your class",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );
        private static readonly DiagnosticDescriptor TypePropertyIsNotAnEnum = new DiagnosticDescriptor(
            id: "GSVG004",
            title: "Type property is not an enum",
            messageFormat: "The specified type property '{0}' has type '{1}' whích is not an enum",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public class IconEntry
        {
            public string? Name { get; set; }
            public List<(string name, string value)>? Attrs { get; set; }
            public string? Body { get; set; }
        }

        private static IconEntry LoadIcon(AdditionalText x)
        {
            var doc = XDocument.Parse(x.GetText()?.ToString());
            foreach (var node in doc.Root.DescendantsAndSelf())
            {
                node.Name = node.Name.LocalName;
                node.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            }
            string codename = ToCamelCase(x.Path);
            return new IconEntry()
            {
                Name = codename,
                Attrs = doc.Root.Attributes().Select(a => (a.Name.LocalName, a.Value)).ToList(),
                Body = string.Join("", doc.Root.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)))
            };
        }

        private static string ToCamelCase(string path)
        {
            var filename = Path.GetFileNameWithoutExtension(path);
            var codename = string.Join("",
                filename.ToLowerInvariant().Split('-')
                .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1))
            );
            return codename;
        }

        private static string? FromCamelCase(string name)
        {
            var match = Regex.Match(name, @"^(^[a-z]+|[0-9]+|[A-Z]+(?![a-z])|[A-Z][a-z]*)*$");
            if (!match.Success) return null;
            return string.Join("-", match.Groups[1].Captures.Cast<Capture>().Select(c => c.Value.ToLowerInvariant()));
        }

        static string S(string text) => $@"@""{text.Replace("\"", "\"\"")}""";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
            {
                ctx.AddSource("GenerateSvgAttribute.g.cs", SourceText.From("""
                    using System;

                    namespace BlazorSvg
                    {
                        [AttributeUsage(AttributeTargets.Class)]
                        public class GenerateSvgAttribute : Attribute
                        {
                            public string TypeParameterName { get; }
                            public string AdditionalAttributesParameterName { get; }

                            public GenerateSvgAttribute(string typeParameterName, string additionalAttributesParameterName)
                            {
                                TypeParameterName = typeParameterName;
                                AdditionalAttributesParameterName = additionalAttributesParameterName;
                            }
                        }
                    }
                    """, Encoding.UTF8));
            });

            var svgFiles = context.AdditionalTextsProvider
                .Where(file => file.Path.EndsWith(".svg"))
                .Select((file, _) => (key: ToCamelCase(file.Path), file))
                .Collect();

            var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                "BlazorSvg.GenerateSvgAttribute",
                static (syn, _) => syn is ClassDeclarationSyntax { AttributeLists.Count: > 0, Modifiers: var mods } &&
                    mods.Any(SyntaxKind.PartialKeyword) &&
                    !mods.Any(SyntaxKind.StaticKeyword),
                static (ctx, ct) =>
                {
                    var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
                    foreach (var attr in ctx.Attributes)
                    {
                        if (attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::BlazorSvg.GenerateSvgAttribute" || attr.ConstructorArguments.Length != 2)
                        {
                            continue;
                        }

                        var typeParameterName = attr.ConstructorArguments[0].Value as string;
                        var additionalAttributesParameterName = attr.ConstructorArguments[1].Value as string;

                        var node = (ClassDeclarationSyntax)ctx.TargetNode;
                        var typeProperty = node.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(p => p.Identifier.Text == typeParameterName);
                        var additionalAttributesProperty = node.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(p => p.Identifier.Text == additionalAttributesParameterName);

                        if (typeProperty is null)
                        {
                            var syn = (AttributeSyntax?)attr.ApplicationSyntaxReference?.GetSyntax();
                            diagnostics.Add(Diagnostic.Create(
                                TypePropertyNotFound,
                                location: syn?.ArgumentList?.Arguments[0].GetLocation(),
                                typeParameterName
                            ));
                            continue;
                        }
                        if (additionalAttributesProperty is null)
                        {
                            var syn = (AttributeSyntax?)attr.ApplicationSyntaxReference?.GetSyntax();
                            diagnostics.Add(Diagnostic.Create(
                                AdditionalAttributesPropertyNotFound,
                                location: syn?.ArgumentList?.Arguments[1].GetLocation(),
                                typeParameterName
                            ));
                            continue;
                        }

                        var enumType = ctx.SemanticModel.GetSymbolInfo(typeProperty.Type, ct).Symbol as INamedTypeSymbol;

                        if (enumType?.TypeKind != TypeKind.Enum)
                        {
                            diagnostics.Add(Diagnostic.Create(
                                TypePropertyIsNotAnEnum,
                                location: typeProperty.Type.GetLocation(),
                                typeParameterName, enumType?.Name ?? typeProperty.Type.ToString()
                            ));
                            continue;
                        }

                        var enumMembers = enumType.GetMembers()
                            .Where(static m => m.Kind == SymbolKind.Field)
                            .ToImmutableArray();

                        var namespaceName = ctx.TargetSymbol.ContainingNamespace.ToDisplayString();
                        var className = ctx.TargetSymbol.Name;

                        return ((namespaceName, className, typeParameterName, additionalAttributesParameterName, enumType, enumMembers), diagnostics.ToImmutable());
                    }
                    return (default, diagnostics.ToImmutable());
                });

            context.RegisterSourceOutput(
                candidates.Combine(svgFiles),
                static (ctx, pair) =>
                {
                    var (namespaceName, className, typeParameterName, additionalAttributesParameterName, enumType, enumMembers) = pair.Left.Item1;
                    var diagnostics = pair.Left.Item2;
                    var svgFiles = pair.Right.ToDictionary(t => t.key, t => t.file);

                    if (diagnostics.Length > 0)
                    {
                        foreach (var d in diagnostics)
                        {
                            ctx.ReportDiagnostic(d);
                        }
                    }

                    if (className is null) return;

                    var source = new CodeWriter();

                    source.AppendLine("using Microsoft.AspNetCore.Components;");
                    source.AppendLine("using Microsoft.AspNetCore.Components.Rendering;");
                    source.AppendLine();
                    using (source.BeginScope($"namespace {namespaceName}"))
                    {
                        using (source.BeginScope($"public partial class {className} : ComponentBase"))
                        {
                            using (source.BeginScope("protected override void BuildRenderTree(RenderTreeBuilder builder)"))
                            {
                                source.AppendLine("object title = null;");
                                using (source.BeginScope($"switch ({typeParameterName})"))
                                {
                                    foreach (var enumMember in enumMembers)
                                    {
                                        if (!svgFiles.TryGetValue(enumMember.Name, out var f))
                                        {
                                            var expectedFilename = $"{FromCamelCase(enumMember.Name)}.svg";
                                            ctx.ReportDiagnostic(Diagnostic.Create(
                                                AdditionalFilesNotFound,
                                                location: enumMember.Locations.FirstOrDefault(),
                                                enumMember.Name, expectedFilename
                                            ));
                                            continue;
                                        }
                                        var icon = LoadIcon(f);

                                        source.AppendLine($"case {enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{enumMember.Name}:");
                                        source.IndentLevel++;
                                        source.AppendLine($@"builder.OpenElement(0, ""svg"");");
                                        if (icon.Attrs != null)
                                        {
                                            foreach (var (attrName, value) in icon.Attrs)
                                            {
                                                source.AppendLine($@"builder.AddAttribute(0, {S(attrName)}, {S(value)});");
                                            }
                                        }
                                        using (source.BeginScope($"if ({additionalAttributesParameterName} != null)"))
                                        {
                                            source.AppendLine($@"builder.AddMultipleAttributes(1, {additionalAttributesParameterName}.Where(static p => p.Key != ""title""));");
                                            using (source.BeginScope($@"if ({additionalAttributesParameterName}.TryGetValue(""title"", out title))"))
                                            {
                                                source.AppendLine($@"builder.OpenElement(2, ""title"");");
                                                source.AppendLine($@"builder.AddContent(2, title);");
                                                source.AppendLine($@"builder.CloseElement();");
                                            }
                                        }
                                        source.AppendLine($@"builder.AddMarkupContent(2, {S(icon.Body)});");
                                        source.AppendLine($@"builder.CloseElement();");
                                        source.AppendLine("break;");
                                        source.IndentLevel--;
                                    }
                                }
                            }
                        }
                    }

                    ctx.AddSource($"{className}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
                });

        }
    }
}
