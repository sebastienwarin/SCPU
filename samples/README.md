# S-CPU sample programs

This directory is the guided, runnable introduction to the S-CPU ecosystem:

- `asm/` contains assembly programs and reusable routines in `common/`;
- `scode/` contains S-Code programs and the shared library in `lib/`.

S-CPU is part of [BuildACPU.com](https://buildacpu.com/), **Build a CPU from
logic gates to code**. The site is the main home for tutorials and explanations;
the complete source code, documentation, and issue tracker live in the public
[sebastienwarin/SCPU repository](https://github.com/sebastienwarin/SCPU).

## Start here

Both software simulators open `.asm` and `.scode` files directly and assemble
or compile them in memory.

### Desktop simulator (recommended)

Open Hello World directly from the extracted desktop-simulator release:

```sh
./scpu-simulator samples/asm/HelloWorld.asm
```

From a source checkout, use:

```sh
dotnet run --project software/simulator/SCPU.Simulator.Desktop -- samples/asm/HelloWorld.asm
```

Press `F5` to run it and watch the terminal. Then use `Ctrl+O` to try:

- [`AutoTest.asm`](asm/AutoTest.asm): run it and check that the LED turns on;
- [`BlinkLED.asm`](asm/BlinkLED.asm): run or step through its timed MMIO loop;
- [`HelloWorld.scode`](scode/HelloWorld.scode): see S-Code compiled and executed.

The desktop simulator provides source/ROM views, breakpoints, watches, RAM,
stack, symbols, terminal, hexadecimal display, and LEDs.

See the [Desktop simulator guide](../software/simulator/SCPU.Simulator.Desktop/README.md)
for usage instructions, debugger features, MMIO device demos and keyboard shortcuts.

### CLI simulator

The toolchain release can load and run a finite sample in one pipeline:

```sh
./scpu load samples/asm/HelloWorld.asm -- run
./scpu load samples/asm/AutoTest.asm -- run -- assert led = 1
```

For a continuous program such as Blink LED, enter the interactive debugger:

```sh
./scpu load samples/asm/BlinkLED.asm -- debug
```

From a source checkout, the equivalent AutoTest command is:

```sh
dotnet run --project software/simulator/SCPU.Simulator.CLI -- load samples/asm/AutoTest.asm -- run -- assert led = 1
```

See the [CLI simulator guide](../software/simulator/SCPU.Simulator.CLI/README.md)
for interactive debugging, pipelines, assertions, and register aliases.

### Logisim, Digital, and physical S-CPU hardware

Use the assembler or compiler to produce the ROM format expected by the target,
then load that ROM in Logisim, Digital, or the physical programmer:

```text
./scpu-assembler -f Logisim16 -o blink-logisim.rom -d FREQ_HZ=50_000 samples/asm/BlinkLED.asm
./scpu-assembler -f Binary -o blink-asm.bin -d FREQ_HZ=2_000_000 samples/asm/BlinkLED.asm
./scode-compiler -f Binary -o blink-scode.bin -d FREQ_HZ=2_000_000 samples/scode/BlinkLED.scode
```

| Tool | Release | From a source checkout |
| --- | --- | --- |
| Assembler | `./scpu-assembler` | `dotnet run --project software/assembler/SCPU.Assembler.CLI --` |
| S-Code compiler | `./scode-compiler` | `dotnet run --project software/compiler/SCode.Compiler.CLI --` |

See the [software tool guide](../software/README.md#running-the-tools) for the
available ROM formats and complete source-build instructions.

## Simulator boundary

The software simulators implement the CPU, debugger, device 0 (hex display and
LEDs), and device 1 (TTY and keyboard). The Digital and Logisim implementations
also provide the device-1 TTY. None of them emulate the physical I2C bus, button
board, LCD2004, SSD1306 OLED, RTC, or sensors.

`drivers/TTY` is simulator-compatible. Samples using `io/TwoWire`,
`io/Buttons`, the device-3 input board, or any other driver require the physical
S-CPU TTL peripheral setup. Their headers state this explicitly.

## Recommended learning path

### 1. Assembly language

| Order | Sample | Focus | Observable result |
| ---: | --- | --- | --- |
| 1 | [`HelloWorld.asm`](asm/HelloWorld.asm) | ROM strings, pointers, TTY output | Universal greeting in the terminal |
| 2 | [`Minimal.asm`](asm/Minimal.asm) | Four native instructions | Hex display = 42 |
| 3 | [`LogicAndBranches.asm`](asm/LogicAndBranches.asm) | Masks and conditional jumps | Hex display = 3, LED on |
| 4 | [`AddressingModes.asm`](asm/AddressingModes.asm) | Immediate, RAM, address, indirect | Hex display = 42 |
| 5 | [`Stack.asm`](asm/Stack.asm) | PUSH/POP and LIFO | Two RAM values are swapped |
| 6 | [`Subroutines.asm`](asm/Subroutines.asm) | Nested CALL/RET | RAM variables = 10 and 15 |
| 7 | [`ArithmeticAndShifts.asm`](asm/ArithmeticAndShifts.asm) | Addition, loops, shifts | Hex display = 42 |
| 8 | [`LongAddition.asm`](asm/LongAddition.asm) | 32-bit addition and carry propagation | RAM = `0x0003_25AB` |

Continue with algorithms and I/O:

| Area | Samples |
| --- | --- |
| Algorithms | [`BubbleSort.asm`](asm/BubbleSort.asm), [`FibonacciIterative.asm`](asm/FibonacciIterative.asm), [`FibonacciRecursive.asm`](asm/FibonacciRecursive.asm) |
| Terminal | [`HelloWorld.asm`](asm/HelloWorld.asm), [`TerminalEcho.asm`](asm/TerminalEcho.asm), [`Console.asm`](asm/Console.asm) |
| LEDs and timing | [`BlinkLED.asm`](asm/BlinkLED.asm), [`LEDChaser.asm`](asm/LEDChaser.asm) (K2000 effect) |
| CPU validation | [`AutoTest.asm`](asm/AutoTest.asm) |
| TTL peripherals | [`Inputs.asm`](asm/Inputs.asm), [`LCD2004.asm`](asm/LCD2004.asm), [`I2C.asm`](asm/I2C.asm), [`TSL2561.asm`](asm/TSL2561.asm) |

### 2. S-Code language

| Order | Sample | Focus | Observable result |
| ---: | --- | --- | --- |
| 1 | [`HelloWorld.scode`](scode/HelloWorld.scode) | Includes, strings, output | Universal greeting in the terminal |
| 2 | [`Minimal.scode`](scode/Minimal.scode) | Smallest structured program | `answer` and accumulator = 42 |
| 3 | [`LanguageBasics.scode`](scode/LanguageBasics.scode) | Basic types, literals, operators, expressions | `result = 42` |
| 4 | [`ArraysAndPointers.scode`](scode/ArraysAndPointers.scode) | Arrays, pointers, address-of, read/write dereference, pass-by-pointer | `checksum = 42`, `pointer = 42`, `updated = 100`, `values[1] = 9`, `doubled = 12,18,24,30`, `swapped = 5,2` |
| 5 | [`StringManipulation.scode`](scode/StringManipulation.scode) | String indexing, char* traversal, strlen/strcmp | Manual and library string checks printed |
| 6 | [`ControlFlow.scode`](scode/ControlFlow.scode) | if, switch, for, while | Three successful terminal lines |
| 7 | [`Functions.scode`](scode/Functions.scode) | Parameters, returns, recursion | 42 and 120 |
| 8 | [`InlineAssembly.scode`](scode/InlineAssembly.scode) | Mixing S-Code and assembly | Hex display = 42 |

Continue with complete programs:

| Area | Samples |
| --- | --- |
| Algorithms | [`BubbleSort.scode`](scode/BubbleSort.scode), [`Fibonacci.scode`](scode/Fibonacci.scode), [`SieveOfEratosthenes.scode`](scode/SieveOfEratosthenes.scode) |
| LEDs and display | [`BlinkLED.scode`](scode/BlinkLED.scode), [`LEDChaser.scode`](scode/LEDChaser.scode) (K2000 effect), [`HexCounter.scode`](scode/HexCounter.scode) |
| Physical input | [`Inputs.scode`](scode/Inputs.scode), [`Buttons.scode`](scode/Buttons.scode) |
| LCD, clock, and sensors | [`LCDMenu.scode`](scode/LCDMenu.scode), [`RealTimeClock.scode`](scode/RealTimeClock.scode), [`RandomNumbers.scode`](scode/RandomNumbers.scode), [`TSL2561.scode`](scode/TSL2561.scode), [`BME280.scode`](scode/BME280.scode) |
| OLED | [`OLEDLogo.scode`](scode/OLEDLogo.scode), [`OLEDClock.scode`](scode/OLEDClock.scode) |
| Games | [`Pong.scode`](scode/Pong.scode), [`Snake.scode`](scode/Snake.scode) |
| Integrated showcases | [`WeatherStation.scode`](scode/WeatherStation.scode), [`HardwareShowcase.scode`](scode/HardwareShowcase.scode) |

## Shared S-Code library

| Namespace | Modules | Simulator support |
| --- | --- | --- |
| `core/` | Delay, Print, Random, String | Yes; visible output depends on the selected driver |
| `io/` | DigitalRead, DigitalWrite, Buttons, TwoWire | Device 0/1 only; Buttons and TwoWire are hardware-only |
| `drivers/` | TTY, HD44780, DS3231, TSL2561, BME280, SSD1306 | TTY only; all other drivers are hardware-only |

New samples should teach one main idea, produce an observable result, and reuse
`common/` or `lib/` rather than duplicate drivers.
