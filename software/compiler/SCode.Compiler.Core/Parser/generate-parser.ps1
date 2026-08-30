# Generate the S-Code lexer and parser from the repository root:
# .\software\compiler\SCode.Compiler.Core\Parser\generate-parser.ps1

$ErrorActionPreference = "Stop"

$antlrJar = Get-ChildItem `
    "$env:USERPROFILE\.vscode\extensions\mike-lischke.vscode-antlr4-*\node_modules\antlr4ng-cli\antlr4-*-complete.jar" `
    | Sort-Object FullName -Descending `
    | Select-Object -First 1 -ExpandProperty FullName

if (-not $antlrJar) {
    throw "ANTLR VS Code extension or ANTLR JAR not found."
}

Push-Location $PSScriptRoot

try {
    java -jar $antlrJar `
        -Dlanguage=CSharp `
        -visitor `
        -no-listener `
        -o . `
        SCodeLexer.g4 `
        SCodeParser.g4
}
finally {
    Pop-Location
}