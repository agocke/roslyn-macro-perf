using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Xunit.Performance;
using Microsoft.Xunit.Performance.Api;

namespace Roslyn.Perf
{
    public class CompilationBenchmark
    {
        private readonly string[] _files = new string[ReproConstants.FileNames.Length];
        private readonly SyntaxTree[] _parseTrees = new SyntaxTree[ReproConstants.FileNames.Length];

        private readonly List<MetadataReference> _references = new List<MetadataReference>();

        private readonly CSharpCompilationOptions _options = 
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithOverflowChecks(true)
            .WithAllowUnsafe(true);

        public CompilationBenchmark()
        {
            // Load all source texts into memory
            var reproPath = Path.Combine(ReproConstants.GetSourceDirectory(), "CodeAnalysisRepro");
            for (int i = 0; i < ReproConstants.FileNames.Length; i++)
            {
                _files[i] = File.ReadAllText(Path.Combine(reproPath, ReproConstants.FileNames[i]));
            }

            // Create references
            var assemblyPath = Environment.GetEnvironmentVariable("MONO_REF_PATH");
            _references.AddRange(Directory.GetFiles(assemblyPath, "*.dll", SearchOption.AllDirectories)
                .Select(f => MetadataReference.CreateFromFile(f)));
            _references.Add(MetadataReference.CreateFromFile(Path.Combine(reproPath, "System.Collections.Immutable.dll")));
            _references.Add(MetadataReference.CreateFromFile(Path.Combine(reproPath, "System.Reflection.Metadata.dll")));
        }

        public void Compile()
        {
            ParseFiles();
            var comp = CSharpCompilation.Create("CodeAnalysis",
                syntaxTrees: _parseTrees,
                references: _references,
                options: _options);

            foreach (var diag in comp.GetDiagnostics())
            {
                if (diag.Severity >= DiagnosticSeverity.Warning)
                {
                    Console.WriteLine(diag);
                }
            }

            var result = comp.Emit(
                peStream: new MemoryStream(),
                pdbStream: new MemoryStream(),
                xmlDocumentationStream: new MemoryStream(),
                win32Resources: null,
                manifestResources: Enumerable.Empty<ResourceDescription>(),
                options: new EmitOptions()
                    .WithDebugInformationFormat(DebugInformationFormat.PortablePdb),
                cancellationToken: default(CancellationToken));

            foreach (var diag in result.Diagnostics)
            {
                if (diag.Severity >= DiagnosticSeverity.Warning)
                {
                    Console.WriteLine(diag);
                }
            }
        }

        private void ParseFiles()
        {
            Parallel.For(0, ReproConstants.FileNames.Length, i =>
            {
                _parseTrees[i] = SyntaxFactory.ParseSyntaxTree(
                    _files[i],
                    ReproConstants.ParseOptions,
                    ReproConstants.FileNames[i],
                    encoding: Encoding.UTF8);
            });
        }
    }

    public class Program
    {
        public static int Main(string[] args)
        {
            using (var harness = new XunitPerformanceHarness(args))
            {
                var entryAssemblyPath = Assembly.GetEntryAssembly().Location;
                harness.RunBenchmarks(entryAssemblyPath);
            }
            return 0;
        }

        private static void ShowHelp()
        {
            Console.Error.WriteLine("usage: roslyn-macro-perf [options] <path-to-ref-assemblies>");
        }
    }
}
