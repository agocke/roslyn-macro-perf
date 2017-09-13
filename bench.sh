#!/bin/bash

set -e

ROSLYN_PKG_DIR=$(pwd)/obj/roslyn
COMPILER_TOOLS_DIR=$ROSLYN_PKG_DIR/microsoft.net.compilers/tools
MONO_PATH=${MONO_PATH:-`which mono`}
REF_ASSEMBLIES_PATH=${REF_ASSEMBLIES_PATH:-`dirname $MONO_PATH`/../lib/mono/4.5-api}
ROSLYN_VERSION=2.6.0-beta1-62110-01

echo "Fetching Roslyn version $ROSLYN_VERSION from MyGet"

if [[ ! -e $COMPILER_TOOLS_DIR ]]; then
    rm -rf $ROSLYN_PKG_DIR
    mkdir -p $ROSLYN_PKG_DIR
    pushd $ROSLYN_PKG_DIR > /dev/null
    curl -L "https://dotnet.myget.org/F/roslyn/api/v2/package/Microsoft.Net.Compilers/2.6.0-beta1-62110-01" -o Microsoft.Net.Compilers.zip
    unzip Microsoft.Net.Compilers.zip -d microsoft.net.compilers
    popd
fi

REFERENCES=(`realpath $REF_ASSEMBLIES_PATH/**/*.dll`)
CSC_ARGS="-noconfig -nologo -nowarn:1574 ${REFERENCES[@]/#/-r:} @repro.rsp"

cd CodeAnalysisRepro

# Create output directory, in case it doesn't already exist
mkdir -p Binaries

echo "Using Mono from: $MONO_PATH"

# Do a dry-run of the perf test where we gather an AOT profile
echo "Gathering AOT profile"
$MONO_PATH --profile=aot:output=../obj/aotprofile $COMPILER_TOOLS_DIR/csc.exe $CSC_ARGS

# AOT the Roslyn assemblies
echo ""
echo "Running Mono AOT"
$MONO_PATH --aot=bind-to-runtime-version,write-symbols,profile=../obj/aotprofile $COMPILER_TOOLS_DIR/{Microsoft.CodeAnalysis.CSharp.dll,Microsoft.CodeAnalysis.dll,System.Reflection.Metadata.dll,System.Collections.Immutable.dll}

# Run the benchmark
echo ""
echo "Running benchmark"
if [ `which perf` ]; then
    perf stat -r 10 $MONO_PATH $COMPILER_TOOLS_DIR/csc.exe $CSC_ARGS
else
    time $MONO_PATH $COMPILER_TOOLS_DIR/csc.exe $CSC_ARGS
fi