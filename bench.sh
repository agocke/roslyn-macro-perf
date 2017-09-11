#!/bin/bash

ROSLYN_PKG_DIR=obj/roslyn

if [[ ! -e $ROSLYN_PKG_DIR ]]; then
    mkdir -p $ROSLYN_PKG_DIR
    pushd $ROSLYN_PKG_DIR
    curl -L "https://dotnet.myget.org/F/roslyn/api/v2/package/Microsoft.Net.Compilers/2.6.0-beta1-62110-01" -o Microsoft.Net.Compilers.zip
    unzip Microsoft.Net.Compilers.zip
    popd
fi

msbuild -p:Configuration=Release -v:m roslyn-macro-perf.csproj

# Do a dry-run of the perf test where we gather an AOT profile
mono --profile=aot:output=obj/aotprofile bin/Release/net461/roslyn-macro-perf.exe --dry-run

# AOT the Roslyn assemblies
mono --aot=bind-to-runtime-version,write-symbols,profile=obj/aotprofile bin/Release/net461/{roslyn-macro-perf.exe,Microsoft.CodeAnalysis.CSharp.dll,Microsoft.CodeAnalysis.dll,System.Reflection.Metadata.dll,System.Collections.Immutable.dll}