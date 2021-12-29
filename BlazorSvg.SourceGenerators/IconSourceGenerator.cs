using Microsoft.CodeAnalysis;
using BlazorSvg.SourceGenerators.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BlazorSvg.SourceGenerators
{
    /// Based on https://github.com/sanchez/dansui/blob/master/Sanchez.DansUI.Icons/IconSourceGenerator.cs
    [Generator]
    public class IconSourceGenerator : ISourceGenerator
    {
        public class IconEntry
        {
            public string Name { get; set; }
            public List<(string name, string value)> Attrs { get; set; }
            public string Body { get; set; }
        }

        private IconEntry LoadIcon(AdditionalText x)
        {
            var doc = XDocument.Parse(x.GetText().ToString());
            foreach (var node in doc.Root.DescendantsAndSelf())
            {
                node.Name = node.Name.LocalName;
                node.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            }
            var filename = Path.GetFileNameWithoutExtension(x.Path);
            var codename = string.Join("", filename.ToLowerInvariant().Split('-').Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
            return new IconEntry()
            {
                Name = codename,
                Attrs = doc.Root.Attributes().Select(a => (a.Name.LocalName, a.Value)).ToList(),
                Body = string.Join("", doc.Root.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)))
            };
        }

        private void AppendEnum(CodeWriter source, List<IconEntry> icons)
        {
            using (source.BeginScope("public enum IconType"))
            {
                bool first = true;
                foreach (var icon in icons)
                {
                    if (!first)
                    {
                        source.Append($",");
                        source.EndLine();
                    }
                    source.StartLine();
                    source.Append($"{icon.Name}");
                    if (first) source.Append(" = 1");
                    first = false;
                }
                source.EndLine();
            }
        }

        static string S(string text) => $@"@""{text.Replace("\"", "\"\"")}""";

        private void AppendConverter(CodeWriter source, List<IconEntry> icons)
        {
            using (source.BeginScope("public static class IconTypeHelper"))
            {
                using (source.BeginScope("public static RenderFragment ToRenderFragment(this IconType type, IDictionary<string, object> attrs)"))
                {
                    using (source.BeginScope("switch (type)"))
                    {
                        foreach (var icon in icons)
                        {
                            source.AppendLine($@"case IconType.{icon.Name}: return (RenderTreeBuilder builder) => {{");
                            source.IndentLevel++;
                            source.AppendLine($@"builder.OpenElement(0, ""svg"");");
                            foreach (var (name,value) in icon.Attrs)
                            {
                                source.AppendLine($@"builder.AddAttribute(0, {S(name)}, {S(value)});");
                            }
                            source.AppendLine($@"builder.AddMultipleAttributes(1, attrs);");
                            source.AppendLine($@"builder.AddMarkupContent(2, {S(icon.Body)});");
                            source.AppendLine($@"builder.CloseElement();");
                            source.IndentLevel--;
                            source.AppendLine($@"}};");
                        }
                        source.AppendLine("default: throw new System.ArgumentException(\"Unsupported icon type\", nameof(type));");
                    }
                }
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var iconItems =
                context.AdditionalFiles
                .Where(x => Path.GetExtension(x.Path) == ".svg")
                .Select(x => LoadIcon(x))
                .OrderBy(x => x.Name)
                .ToList();

            var sourceBuilder = new CodeWriter();

            sourceBuilder.AppendLine("using Microsoft.AspNetCore.Components;");
            sourceBuilder.AppendLine("using Microsoft.AspNetCore.Components.Rendering;");
            sourceBuilder.AppendLine("using System.Collections.Generic;");

            using (sourceBuilder.BeginScope("namespace BlazorSvg.Client"))
            {
                AppendEnum(sourceBuilder, iconItems);
                AppendConverter(sourceBuilder, iconItems);
            }

            context.AddSource("IconPack.cs", sourceBuilder.ToString());
        }

        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            //if (!System.Diagnostics.Debugger.IsAttached)
            //{
            //    System.Diagnostics.Debugger.Launch();
            //}
#endif
        }
    }
}
