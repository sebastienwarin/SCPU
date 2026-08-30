#!/usr/bin/env bash
set -euo pipefail

tag="${1:-}"

if [[ ! "$tag" =~ ^v([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  echo "Release tag must use the stable SemVer form vMAJOR.MINOR.PATCH (received: '$tag')." >&2
  exit 1
fi

version="${tag#v}"
configuration="Release"
artifacts_dir="$(pwd)/artifacts/release"
publish_root="$(pwd)/artifacts/publish"
source_revision="${GITHUB_SHA:-unknown}"
source_revision="${source_revision:0:10}"

if [[ ! -f "software/SCPU.sln" || ! -f "software/Directory.Build.props" ]]; then
  echo "Run this script from the SCPU repository root." >&2
  exit 1
fi

rm -rf "$artifacts_dir" "$publish_root"
mkdir -p "$artifacts_dir" "$publish_root"

projects=(
  "software/assembler/SCPU.Assembler.CLI/SCPU.Assembler.CLI.csproj"
  "software/compiler/SCode.Compiler.CLI/SCode.Compiler.CLI.csproj"
  "software/simulator/SCPU.Simulator.CLI/SCPU.Simulator.CLI.csproj"
  "software/simulator/SCPU.Simulator.Desktop/SCPU.Simulator.Desktop.csproj"
)

rids=(
  "win-x64"
  "linux-x64"
)

# Optional override: export SCPU_RELEASE_RIDS="win-x64 linux-x64 linux-arm64"
if [[ -n "${SCPU_RELEASE_RIDS:-}" ]]; then
  # shellcheck disable=SC2206
  rids=(${SCPU_RELEASE_RIDS})
fi

publish_project() {
  local project="$1"
  local output="$2"

  dotnet publish "$project" \
    --configuration "$configuration" \
    --self-contained false \
    --output "$output" \
    -p:Platform="Any CPU" \
    -p:Version="$version" \
    -p:VersionSuffix= \
    -p:AssemblyVersion="$version.0" \
    -p:FileVersion="$version.0" \
    -p:InformationalVersion="$version+$source_revision" \
    -p:IncludeSourceRevisionInInformationalVersion=false \
    -p:UseAppHost=false \
    -p:DebugType=embedded \
    -p:RestoreIgnoreFailedSources=false
}

publish_desktop_project() {
  local project="$1"
  local rid="$2"
  local output="$3"

  dotnet publish "$project" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --self-contained true \
    --output "$output" \
    -p:Platform="Any CPU" \
    -p:Version="$version" \
    -p:VersionSuffix= \
    -p:AssemblyVersion="$version.0" \
    -p:FileVersion="$version.0" \
    -p:InformationalVersion="$version+$source_revision" \
    -p:IncludeSourceRevisionInInformationalVersion=false \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=embedded \
    -p:RestoreIgnoreFailedSources=false
}

copy_dotnet_entry() {
  local source_dir="$1"
  local source_base="$2"
  local target_dir="$3"
  local target_base="$4"

  mkdir -p "$target_dir"

  while IFS= read -r file; do
    local name
    name="$(basename "$file")"
    case "$name" in
      "$source_base.dll"|"$source_base.deps.json"|"$source_base.runtimeconfig.json")
        continue
        ;;
    esac
    if [[ -d "$file" ]]; then
      mkdir -p "$target_dir/$name"
      cp -R "$file/." "$target_dir/$name"
    else
      cp -R "$file" "$target_dir/$name"
    fi
  done < <(find "$source_dir" -mindepth 1 -maxdepth 1)

  cp "$source_dir/$source_base.dll" "$target_dir/$target_base.dll"
  cp "$source_dir/$source_base.deps.json" "$target_dir/$target_base.deps.json"
  cp "$source_dir/$source_base.runtimeconfig.json" "$target_dir/$target_base.runtimeconfig.json"
}

create_dotnet_launcher() {
  local output_path="$1"
  local body="$2"

  printf '%s\n' "$body" > "$output_path"
  chmod +x "$output_path"
}

copy_samples() {
  local target_dir="$1"
  local public_repository="https://github.com/sebastienwarin/SCPU"
  local readme

  cp -R "samples" "$target_dir/samples"
  readme="$target_dir/samples/README.md"

  # README links to repository documentation are relative in the source tree.
  # Release archives are standalone, so pin those links to this release tag.
  sed -i "s#](../#]($public_repository/blob/$tag/#g" "$readme"

  if grep -qF '](../' "$readme"; then
    echo "Unable to rewrite every repository-relative link in $readme." >&2
    exit 1
  fi
}

toolchain_root="$publish_root/dotnet"
toolchain_sources="$toolchain_root/src"
toolchain_name="scpu-toolchain-$version-dotnet"
toolchain_dir="$toolchain_root/$toolchain_name"
mkdir -p "$toolchain_sources" "$toolchain_dir"

publish_project "${projects[0]}" "$toolchain_sources/assembler"
publish_project "${projects[1]}" "$toolchain_sources/compiler"
publish_project "${projects[2]}" "$toolchain_sources/simulator-cli"

copy_dotnet_entry "$toolchain_sources/assembler" "SCPU.Assembler.CLI" "$toolchain_dir" "scpu-assembler"
copy_dotnet_entry "$toolchain_sources/compiler" "SCode.Compiler.CLI" "$toolchain_dir" "scode-compiler"
copy_dotnet_entry "$toolchain_sources/simulator-cli" "SCPU.Simulator.CLI" "$toolchain_dir" "scpu"

create_dotnet_launcher "$toolchain_dir/scpu-assembler" '#!/usr/bin/env sh
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec dotnet "$SCRIPT_DIR/scpu-assembler.dll" "$@"'
create_dotnet_launcher "$toolchain_dir/scode-compiler" '#!/usr/bin/env sh
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec dotnet "$SCRIPT_DIR/scode-compiler.dll" "$@"'
create_dotnet_launcher "$toolchain_dir/scpu" '#!/usr/bin/env sh
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec dotnet "$SCRIPT_DIR/scpu.dll" "$@"'
printf '%s\r\n' '@echo off' 'dotnet "%~dp0scpu-assembler.dll" %*' > "$toolchain_dir/scpu-assembler.cmd"
printf '%s\r\n' '@echo off' 'dotnet "%~dp0scode-compiler.dll" %*' > "$toolchain_dir/scode-compiler.cmd"
printf '%s\r\n' '@echo off' 'dotnet "%~dp0scpu.dll" %*' > "$toolchain_dir/scpu.cmd"
copy_samples "$toolchain_dir"

(cd "$toolchain_root" && zip -9 -r "$artifacts_dir/$toolchain_name.zip" "$toolchain_name")

for rid in "${rids[@]}"; do
  rid_root="$publish_root/$rid"

  extension=""
  if [[ "$rid" == win-* ]]; then
    extension=".exe"
  fi

  publish_desktop_project "${projects[3]}" "$rid" "$rid_root/simulator-desktop"

  desktop_name="scpu-simulator-$version-$rid"
  desktop_dir="$rid_root/$desktop_name"
  mkdir -p "$desktop_dir"
  cp "$rid_root/simulator-desktop/SCPU.Simulator.Desktop$extension" "$desktop_dir/scpu-simulator$extension"
  copy_samples "$desktop_dir"

  if [[ "$rid" != win-* ]]; then
    chmod +x "$desktop_dir"/*
  fi

  (cd "$rid_root" && zip -9 -r "$artifacts_dir/$desktop_name.zip" "$desktop_name")
done

(cd "$artifacts_dir" && sha256sum ./*.zip > SHA256SUMS)

echo "Release artifacts:"
ls -lh "$artifacts_dir"
