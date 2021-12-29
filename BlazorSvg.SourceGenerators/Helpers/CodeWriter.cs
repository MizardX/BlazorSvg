using System;
using System.Text;

namespace BlazorSvg.SourceGenerators.Helpers
{
    /// Based on https://github.com/Grauenwolf/Tortuga-TestMonkey/blob/main/Tortuga.TestMonkey/CodeWriter.cs
    internal class CodeWriter
    {
        public CodeWriter()
        {
            _scopeTracker = new ScopeTracker(this);
        }

        StringBuilder Content { get; } = new StringBuilder();
        public int IndentLevel { get; set; }
        ScopeTracker _scopeTracker { get; }
        public void Append(string line) => Content.Append(line);

        public void AppendLine(string line) => Content.Append(new string(' ', 4 * IndentLevel)).AppendLine(line);
        public void AppendLine() => Content.AppendLine();

        public IDisposable BeginScope(string line)
        {
            AppendLine(line);
            return BeginScope();
        }

        public IDisposable BeginScope()
        {
            Content.Append(new string(' ', 4 * IndentLevel)).AppendLine("{");
            IndentLevel += 1;
            return _scopeTracker;
        }

        public void EndLine() => Content.AppendLine();

        public void EndScope()
        {
            IndentLevel -= 1;
            Content.Append(new string(' ', 4 * IndentLevel)).AppendLine("}");
        }

        public void StartLine() => Content.Append(new string(' ', 4 * IndentLevel));
        public override string ToString() => Content.ToString();

        class ScopeTracker : IDisposable
        {
            public ScopeTracker(CodeWriter parent)
            {
                Parent = parent;
            }
            public CodeWriter Parent { get; }

            public void Dispose()
            {
                Parent.EndScope();
            }
        }
    }
}
