using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Mono.Options;

namespace Roslyn.Perf
{
    [MonoJob]
    public class CompilationBenchmark
    {
        internal static string s_refAssemblyPath;

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
            _references.AddRange(Directory.GetFiles(s_refAssemblyPath, "*.dll", SearchOption.AllDirectories)
                .Select(f => MetadataReference.CreateFromFile(f)));
            _references.Add(MetadataReference.CreateFromFile(Path.Combine(reproPath, "System.Collections.Immutable.dll")));
            _references.Add(MetadataReference.CreateFromFile(Path.Combine(reproPath, "System.Reflection.Metadata.dll")));
        }

        [Benchmark]
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
            bool dryRun = false;

            var options = new OptionSet()
            {
                {"dry-run", "Run the benchmark without monitoring", _ => dryRun = true}
            };

            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }

            if (extra.Count != 1)
            {
                ShowHelp();
                return 1;
            }

            CompilationBenchmark.s_refAssemblyPath = Path.GetFullPath(extra[0]);

            if (dryRun)
            {
                DryRun();
                return 0;
            }

            var summary = BenchmarkRunner.Run<CompilationBenchmark>();
            return 0;
        }

        private static void DryRun()
        {
            Console.WriteLine("Running dry run");
            (new CompilationBenchmark()).Compile();
        }
        
        private static void ShowHelp()
        {
            Console.Error.WriteLine("usage: roslyn-macro-perf [options] <path-to-ref-assemblies>");
        }
    }
}
