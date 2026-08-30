# S-CPU Software Toolchain

This directory contains the **software components** of the S-CPU project: the
[assembler](assembler/), [compiler](compiler/), [simulators](simulator/) and
[shared architecture library](common/).

Together, they provide the tools required to write, compile, assemble, debug and run
programs for the S-CPU.

## Running the tools

There are two supported ways to use the software:

1. **Recommended for users:** download and extract a package from the project
   Releases. The desktop simulator packages are self-contained. The portable
   toolchain contains `scpu-assembler`, `scode-compiler`, and `scpu` and only
   requires the .NET 10 Runtime.
2. **For contributors:** clone the repository, install the .NET 10 SDK, and run
   the projects from source.

The launchers have the same names on every platform. From the extracted
directory, use the `./` prefix in PowerShell and Unix shells. The examples
below can omit it only when a tool is installed on `PATH`.

The packaged desktop simulator is available for Windows x64 and Linux x64.
macOS users can use the cross-platform CLI toolchain or run the desktop project
from source.

| Tool | Release command | Run from a source checkout |
| --- | --- | --- |
| Assembler | `./scpu-assembler` | `dotnet run --project software/assembler/SCPU.Assembler.CLI --` |
| S-Code compiler | `./scode-compiler` | `dotnet run --project software/compiler/SCode.Compiler.CLI --` |
| CLI simulator | `./scpu` | `dotnet run --project software/simulator/SCPU.Simulator.CLI --` |
| Desktop simulator | `./scpu-simulator` | `dotnet run --project software/simulator/SCPU.Simulator.Desktop --` |

Everything after the command name is identical in both modes. For example,
`./scpu-assembler samples/asm/AutoTest.asm -p` becomes:

```sh
dotnet run --project software/assembler/SCPU.Assembler.CLI -- samples/asm/AutoTest.asm -p
```

## Project structure

```text
software/
├── assembler/   → S-CPU assembly to binary and ROM formats
├── compiler/    → S-Code to S-CPU assembly
├── simulator/   → Desktop/CLI simulators and shared libraries
└── common/      → Shared architecture, memory map and instruction definitions
```

## Components

### S-CPU Assembler

* Converts **S-CPU Assembly (`.asm`)** into executable machine code.
* Supports macros, constants, includes, labels and conditional blocks.
* Exports Binary, Intel HEX, Logisim, Annotated, Verilog, Gowin and Symbol formats.
* Provides a **.NET library** and a **command-line application**.

[📘 Read the Assembler documentation](./assembler/README.md)

### S-Code Compiler

* Compiles the C/C#-inspired **S-Code language (`.scode`)** to S-CPU assembly.
* Supports variables, functions, expressions, arrays and control structures.
* Uses **ANTLR**, an AST and the S-CPU assembler pipeline.
* Provides a **.NET library**, a **CLI** and an xUnit test suite.

[📘 Read the Compiler documentation](./compiler/README.md)

### S-CPU Simulator Desktop

The main graphical simulator and debugger for the S-CPU.

![S-CPU Simulator Desktop](../docs/assets/simulator-desktop/SimulatorDesktop.png)

* Opens binary ROM, assembly and S-Code files directly.
* Provides CPU/datapath, ROM, source, RAM, stack, watches and breakpoints views.
* Simulates terminal, LED and seven-segment MMIO devices.
* Supports stepping, PC tracking, diagnostics and ROM exports.

Open `samples/scode/BlinkLED.scode` from the application, or start with it directly:

```sh
# Release
./scpu-simulator samples/scode/BlinkLED.scode

# Source checkout
dotnet run --project software/simulator/SCPU.Simulator.Desktop -- samples/scode/BlinkLED.scode
```

[📘 Read the Desktop Simulator documentation](./simulator/SCPU.Simulator.Desktop/README.md)

### S-CPU Simulator CLI

The terminal debugger for interactive sessions, scripts and CI.

![S-CPU Simulator CLI](../docs/assets/simulator-cli/Debug.png)

* Loads binary, assembly and S-Code programs.
* Provides run/step commands, registers, memory, symbols, watches and breakpoints.
* Supports an interactive shell and bounded automated execution.

```sh
# Release
./scpu

# Source checkout
dotnet run --project software/simulator/SCPU.Simulator.CLI
```

[📘 Read the CLI Simulator documentation](./simulator/SCPU.Simulator.CLI/README.md)

### Simulator libraries

* **`SCPU.Simulator.Core`** — deterministic processor, ROM, RAM and MMIO model.  
  [Core documentation](./simulator/SCPU.Simulator.Core/README.md)
* **`SCPU.Simulator.Debugger`** — loading, debug sessions, symbols, snapshots and execution runner.  
  [Debugger documentation](./simulator/SCPU.Simulator.Debugger/README.md)
* **`SCPU.Simulator.Devices`** — reusable terminal and LED/display MMIO devices shared by both frontends.  
  [Devices documentation](./simulator/SCPU.Simulator.Devices/README.md)

### Shared library (`SCPU.Architecture`)

* Shared definitions used by the assembler, compiler and simulators.
* Contains the **instruction set**, **addressing modes**, **memory map** and address helpers.
* Keeps binary and memory formats consistent across the complete toolchain.

## Typical workflow

1. Write a program in **S-Code** or **S-CPU Assembly**.
2. Open it directly in the Desktop or CLI simulator to test and debug it.
3. Export or build a ROM image with the assembler/compiler.
4. Deploy the image to an [S-CPU hardware target](../hardware/).

## References

* [S-CPU project documentation](../readme.md)
* [S-CPU Architecture guide](../docs/architecture.md)
* [S-CPU hardware projects](../hardware/)
* [Assembler documentation](./assembler/README.md)
* [Compiler documentation](./compiler/README.md)
