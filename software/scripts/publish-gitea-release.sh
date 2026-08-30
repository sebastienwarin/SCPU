#!/usr/bin/env bash
set -euo pipefail

: "${GITEA_TOKEN:?GITEA_TOKEN is required}"
: "${GITEA_SERVER_URL:?GITEA_SERVER_URL is required}"
: "${GITEA_REPOSITORY:?GITEA_REPOSITORY is required}"
: "${RELEASE_TAG:?RELEASE_TAG is required}"
: "${RELEASE_SHA:?RELEASE_SHA is required}"

version="${RELEASE_TAG#v}"
api_root="${GITEA_SERVER_URL%/}/api/v1/repos/$GITEA_REPOSITORY"
artifacts_dir="artifacts/release"
release_json="$artifacts_dir/release.json"

release_body=$(printf '%s\n' \
  '> **This release includes the complete S-CPU toolchain: desktop simulator, S-CPU assembler, S-Code compiler, CLI simulator and sample programs to start developing for S-CPU.**' \
  '' \
  "S-CPU $version is ready to use. No SDK installation, source checkout, or compilation is required." \
  '' \
  '## Which file should I download?' \
  '' \
  '| File | Platform | Contents | Requirement |' \
  '| --- | --- | --- | --- |' \
  "| \`scpu-simulator-$version-win-x64.zip\` | Windows x64 | Desktop simulator | None (self-contained) |" \
  "| \`scpu-simulator-$version-linux-x64.zip\` | Linux x64 | Desktop simulator | None (self-contained) |" \
  "| \`scpu-toolchain-$version-dotnet.zip\` | Windows, Linux and macOS | Assembler, S-Code compiler and CLI simulator | [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |" \
  "| \`scpu-documentation-$version.pdf\` | Any | Complete S-CPU documentation | PDF reader |" \
  '' \
  '> macOS users: there is no native desktop simulator package in this release. The .NET command-line toolchain remains cross-platform, and the desktop simulator can be run from source with `dotnet run --project software/simulator/SCPU.Simulator.Desktop`.' \
  '' \
  '## Quick start' \
  '' \
  'Extract the archive, then run:' \
  '' \
  '- Desktop simulator on Windows: `./scpu-simulator.exe`' \
  '- Desktop simulator on Linux: `./scpu-simulator`' \
  '- CLI tools on Windows, Linux and macOS: `./scpu-assembler`, `./scode-compiler` or `./scpu`' \
  '' \
  'Each archive also includes sample programs and a guided README. More tutorials, examples, and learning resources are available at [BuildACPU.com](https://buildacpu.com/).' \
  '' \
  '## Run from source' \
  '' \
  'The source archive attached to this release can be built and run on Windows, Linux and macOS. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), extract the sources, open a terminal at the repository root, then use one of these commands:' \
  '' \
  '- Assembler: `dotnet run --project software/assembler/SCPU.Assembler.CLI`' \
  '- SCode compiler: `dotnet run --project software/compiler/SCode.Compiler.CLI`' \
  '- CLI simulator: `dotnet run --project software/simulator/SCPU.Simulator.CLI`' \
  '- Desktop simulator: `dotnet run --project software/simulator/SCPU.Simulator.Desktop`' \
  '' \
  'To run the latest development code instead, clone the repository and use the same commands.' \
  '' \
  '## Verify your download' \
  '' \
  'Download `SHA256SUMS` alongside the archives and verify their integrity with `sha256sum -c SHA256SUMS` on Linux/macOS, or `Get-FileHash <file> -Algorithm SHA256` in PowerShell on Windows.')

json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//$'\r'/\\r}"
  value="${value//$'\n'/\\n}"
  printf '%s' "$value"
}

payload=$(printf '{"tag_name":"%s","target_commitish":"%s","name":"S-CPU Toolchain %s","body":"%s","draft":false,"prerelease":false}' \
  "$(json_escape "$RELEASE_TAG")" \
  "$(json_escape "$RELEASE_SHA")" \
  "$(json_escape "$version")" \
  "$(json_escape "$release_body")")

http_status=$(curl --silent --show-error \
  --output "$release_json" \
  --write-out '%{http_code}' \
  --header "Authorization: token $GITEA_TOKEN" \
  --header "Content-Type: application/json" \
  --request POST \
  --data "$payload" \
  "$api_root/releases")

if [[ "$http_status" != "201" ]]; then
  echo "Unable to create Gitea release (HTTP $http_status):" >&2
  cat "$release_json" >&2
  exit 1
fi

release_id=$(grep -o '"id":[0-9]*' "$release_json" | head -n 1 | cut -d: -f2)
if [[ -z "$release_id" ]]; then
  echo "Unable to read the release id from Gitea's response." >&2
  cat "$release_json" >&2
  exit 1
fi

for file in "$artifacts_dir"/*.zip "$artifacts_dir"/*.tar.gz "$artifacts_dir"/*.pdf "$artifacts_dir/SHA256SUMS"; do
  [[ -f "$file" ]] || continue
  name=$(basename "$file")
  echo "Uploading $name..."
  curl --fail --silent --show-error \
    --header "Authorization: token $GITEA_TOKEN" \
    --request POST \
    --form "attachment=@$file" \
    "$api_root/releases/$release_id/assets?name=$name" >/dev/null
done

echo "Published release $RELEASE_TAG with id $release_id."
