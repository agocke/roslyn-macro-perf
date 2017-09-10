using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Roslyn.Perf
{
    [MonoJob]
    public class ParsingBenchmark
    {
        private readonly string[] _files = new string[ReproConstants.FileNames.Length];
        private readonly SyntaxTree[] _parseTrees = new SyntaxTree[ReproConstants.FileNames.Length];

        public ParsingBenchmark()
        {
            var reproPath = Path.Combine(ReproConstants.GetSourceDirectory(), "CodeAnalysisRepro");
            for (int i = 0; i < ReproConstants.FileNames.Length; i++)
            {
                _files[i] = File.ReadAllText(Path.Combine(reproPath, ReproConstants.FileNames[i]));
            }
        }

        [Benchmark]
        public void ParseFiles()
        {
            Parallel.For(0, ReproConstants.FileNames.Length, i =>
            {
                _parseTrees[i] = SyntaxFactory.ParseSyntaxTree(
                    _files[i],
                    CSharpParseOptions.Default,
                    ReproConstants.FileNames[i]);
            });
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<ParsingBenchmark>();
        }
    }
}
