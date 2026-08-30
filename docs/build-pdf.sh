#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="$ROOT_DIR/docs/pdf-manifest.txt"
CSS_FILE="$ROOT_DIR/docs/pdf.css"
LUA_FILTER="$ROOT_DIR/docs/pdf.lua"
BUILD_DIR="$ROOT_DIR/artifacts/documentation"

if [[ $# -eq 0 ]]; then
    OUTPUT_FILE="$BUILD_DIR/S-CPU-Documentation.pdf"
elif [[ "$1" = /* ]]; then
    OUTPUT_FILE="$1"
else
    OUTPUT_FILE="$ROOT_DIR/$1"
fi

DOCUMENT_VERSION="${2:-${SCPU_DOCUMENTATION_VERSION:-development}}"

if [[ ! "$DOCUMENT_VERSION" =~ ^[0-9A-Za-z._+-]+$ ]]; then
    echo "Invalid documentation version: $DOCUMENT_VERSION" >&2
    exit 1
fi

mkdir -p "$(dirname "$OUTPUT_FILE")"

TEMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TEMP_DIR"' EXIT
FOOTER_FILE="$TEMP_DIR/footer.html"
COMBINED_FILE="$TEMP_DIR/document.md"

if [[ "$DOCUMENT_VERSION" == "development" ]]; then
    VERSION_LABEL="$DOCUMENT_VERSION"
else
    VERSION_LABEL="v$DOCUMENT_VERSION"
fi

printf '%s\n' \
    "<div class=\"pdf-footer\">S-CPU Documentation · $VERSION_LABEL · © <a href=\"https://buildacpu.com/\">BuildACPU.com</a></div>" \
    > "$FOOTER_FILE"

if [[ ! -f "$MANIFEST" ]]; then
    echo "Manifest not found: $MANIFEST" >&2
    exit 1
fi

MARKDOWN_FILES=()

while IFS= read -r markdown_file || [[ -n "$markdown_file" ]]; do
    # Remove Windows CRLF, an optional UTF-8 BOM, and surrounding whitespace.
    markdown_file="${markdown_file//$'\r'/}"
    markdown_file="${markdown_file#$'\xEF\xBB\xBF'}"
    markdown_file="$(printf '%s' "$markdown_file" | xargs)"

    # Ignore empty lines and comments.
    if [[ -z "$markdown_file" || "$markdown_file" == \#* ]]; then
        continue
    fi

    source_file="$ROOT_DIR/$markdown_file"

    if [[ ! -f "$source_file" ]]; then
        echo "Markdown file not found: $markdown_file" >&2
        exit 1
    fi

    MARKDOWN_FILES+=("$markdown_file")

    printf '\nSCPU_PDF_SOURCE_BOUNDARY::%s\n\n' \
        "$markdown_file" >> "$COMBINED_FILE"
    command cat "$source_file" >> "$COMBINED_FILE"
    printf '\n' >> "$COMBINED_FILE"
done < "$MANIFEST"

if [[ ${#MARKDOWN_FILES[@]} -eq 0 ]]; then
    echo "The manifest does not contain any Markdown files." >&2
    exit 1
fi

echo "Rendering ${#MARKDOWN_FILES[@]} Markdown files as one document..."

(
    cd "$ROOT_DIR"

    pandoc "$COMBINED_FILE" \
        --from=gfm \
        --to=html5 \
        --standalone \
        --pdf-engine=weasyprint \
        --css="$CSS_FILE" \
        --lua-filter="$LUA_FILTER" \
        --resource-path="$ROOT_DIR" \
        --metadata="pagetitle:S-CPU Documentation" \
        --include-before-body="$FOOTER_FILE" \
        --output="$OUTPUT_FILE"
)

echo
echo "Documentation generated:"
echo "$OUTPUT_FILE"
