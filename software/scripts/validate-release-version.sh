#!/usr/bin/env bash
set -euo pipefail

tag="${1:-}"

if [[ ! "$tag" =~ ^v([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  echo "Release tag must use the stable SemVer form vMAJOR.MINOR.PATCH (received: '$tag')." >&2
  exit 1
fi

version="${tag#v}"
project_version=$(dotnet msbuild software/assembler/SCPU.Assembler.CLI/SCPU.Assembler.CLI.csproj \
  -nologo \
  -getProperty:VersionPrefix)

if [[ "$project_version" != "$version" ]]; then
  echo "Tag $tag does not match VersionPrefix $project_version in software/Directory.Build.props." >&2
  exit 1
fi

echo "Validated release version $version."

