using SCPU.Architecture;

namespace SCPU.Simulator.Core
{
    /// <summary>
    /// Simulates the S-CPU execution core.
    /// Implements a 2-phase pipeline (S0 fetch / S1 execute), instruction decoding,
    /// addressing mode handling, data bus multiplexing, and ROM/RAM/MMIO interactions.
    /// </summary>
    /// <remarks>
    /// This class models the functional behavior of the S-CPU at instruction-cycle level,
    /// including ALU operations, control flow, indirect addressing, and memory-mapped I/O.
    /// </remarks>
    public class Processor
    {
        #region Counters

        /// <summary>
        /// Program counter (PC), 16-bit, addressing ROM space (0x0000-0xFFFF).
        /// </summary>
        public ushort ProgramCounter { get; private set; }

        /// <summary>
        /// Pipeline micro-step (S0 = fetch, S1 = execute).
        /// </summary>
        public Step StepCounter { get; private set; }

        #endregion

        #region 16-bit registers

        /// <summary>
        /// Instruction register (IR), 16-bit: [15:14]=opcode, [13:0]=addressing/operand.
        /// </summary>
        public ushort InstructionRegister { get; private set; }

        /// <summary>
        /// Accumulator register (A), 16-bit.
        /// </summary>
        public ushort AccumulatorRegister { get; private set; }

        #endregion

        #region Flags

        /// <summary>
        /// Carry flag (C). Set by ADD and certain STA-to-internal-register operations; cleared on JCC (at S0).
        /// </summary>
        public bool CarryFlag { get; private set; }

        /// <summary>
        /// Indicates an in-flight indirect operand resolution across S0->S1 (IR rewrite path).
        /// </summary>
        public bool IndirectedFlag { get; private set; }

        #endregion

        #region ROM & RAM

        /// <summary>
        /// 64K Program ROM words (16-bit).
        /// </summary>
        public ushort[] ROM { get; private set; } = new ushort[MemoryMap.Rom.Length];

        /// <summary>
        /// 2K Data RAM words (16-bit).
        /// </summary>
        public ushort[] RAM { get; private set; } = new ushort[MemoryMap.Ram.Length];

        #endregion

        #region I/O Devices

        /// <summary>
        /// Registered memory-mapped I/O devices by device id (0..7).
        /// </summary>
        public Dictionary<DeviceId, IODevice> Devices { get; private set; } = [];

        #endregion

        #region Helpers properties

        /// <summary>
        /// Decoded opcode from IR bits [15:14].
        /// </summary>
        public Instruction CurrentInstruction => (Instruction)(InstructionRegister >> 14);

        /// <summary>
        /// Raw 11-bit operand from IR bits [10:0].
        /// </summary>
        public ushort CurrentInstructionOperand => (ushort)(InstructionRegister & 0x7FF);

        /// <summary>
        /// Decoded addressing mode from IR.
        /// </summary>
        public AddressingMode CurrentAddressingMode => GetAddressingMode();

        /// <summary>
        /// Target MMIO device id (IR bits [10:8]) when addressing mode is MMIO.
        /// </summary>
        public DeviceId TargetDevice => (DeviceId)((InstructionRegister >> 8) & 7);

        /// <summary>
        /// True when IR must be (re)fetched at S0 (and not currently resolving an indirect operand).
        /// </summary>
        public bool ShouldFetchIR => StepCounter == Step.S0 && !IndirectedFlag;

        /// <summary>
        /// True when the ROM chip is enabled for this micro-step (S0 fetch, or explicit ROM read).
        /// </summary>
        public bool IsROMEnable => CurrentAddressingMode == AddressingMode.ROM || ShouldFetchIR;

        /// <summary>
        /// True when the RAM chip is enabled (RAM/Indirect on S1).
        /// </summary>
        public bool IsRAMEnable => (CurrentAddressingMode == AddressingMode.RAM || CurrentAddressingMode == AddressingMode.Indirect) && !ShouldFetchIR;

        /// <summary>
        /// True when MMIO is enabled on S1 for device register access.
        /// </summary>
        public bool IsIOEnable => StepCounter != Step.S0 && CurrentAddressingMode == AddressingMode.MMIO;

        /// <summary>
        /// True when the selected MMIO device is the internal S-CPU device (Device7).
        /// </summary>
        public bool IsInternalIODevice => IsIOEnable && TargetDevice == DeviceId.Device7;

        /// <summary>
        /// True when the current MMIO address maps to the carry-flag control register (internal device).
        /// </summary>
        public bool IsCarryFlagAddress => IsInternalIODevice && (InstructionRegister & 0xF) == 0xF;

        /// <summary>
        /// ROM address used at this micro-step: S0 uses PC; otherwise low 12 bits of IR.
        /// </summary>
        public ushort ROMAddress => StepCounter == Step.S0 ? ProgramCounter : (ushort)(InstructionRegister & 0xFFF);

        /// <summary>
        /// Effective ALU operand: immediate (IR 11-bit) or value coming from the data bus.
        /// </summary>
        public ushort ALUOperand => CurrentAddressingMode == AddressingMode.Immediate ? CurrentInstructionOperand : DataBus;

        /// <summary>
        /// Read-only data bus multiplexer: ROM, RAM, or MMIO (if present), else 0.
        /// </summary>
        public ushort DataBus => IsROMEnable
            ? ROM[ROMAddress]
            : IsRAMEnable
                ? RAM[CurrentInstructionOperand]
                : IsIOEnable && Devices.ContainsKey(TargetDevice)
                    ? Devices[TargetDevice][(byte)(CurrentInstructionOperand & 0xF)]
                    : (ushort)0;

        /// <summary>
        /// Observes the current bus value without triggering side effects on MMIO devices.
        /// Intended for debugger snapshots and diagnostics only.
        /// </summary>
        public ushort PeekDataBus => IsROMEnable
            ? ROM[ROMAddress]
            : IsRAMEnable
                ? RAM[CurrentInstructionOperand]
                : IsIOEnable && Devices.TryGetValue(TargetDevice, out var device)
                    ? device.Peek((byte)(CurrentInstructionOperand & 0xF))
                    : (ushort)0;

        #endregion

        #region Public methods

        /// <summary>
        /// Advances the simulation by one micro-step (S0->S1 or S1->S0), performing fetch/execute,
        /// indirect resolution, memory/MMIO access, and ALU operations.
        /// </summary>
        public void Tick()
        {
            if (StepCounter == Step.S0)
            {
                if (CurrentInstruction == Instruction.JCC)
                {
                    // Reset the carry flag after JCC instruction
                    this.CarryFlag = false;
                }
                if (!IndirectedFlag)
                {
                    // Fetch the next instruction
                    InstructionRegister = ROM[ProgramCounter++];
                }
            }
            else if (StepCounter == Step.S1)
            {
                if (this.IndirectedFlag)
                {
                    // Reset the Indirected flag on S1
                    this.IndirectedFlag = false;
                }

                if (CurrentAddressingMode == AddressingMode.Indirect)
                {
                    // Resolve the indirect address
                    InstructionRegister = (ushort)(((ushort)CurrentInstruction << 14) + ((ushort)DataBus & 0x3FFF));
                    this.IndirectedFlag = true;
                }
                else
                {
                    // Execute instructions
                    switch (CurrentInstruction)
                    {
                        case Instruction.NOR:
                            AccumulatorRegister = (ushort)~(AccumulatorRegister | ALUOperand);
                            break;

                        case Instruction.ADD:
                            AccumulatorRegister = Add(AccumulatorRegister, ALUOperand, out bool carry);
                            this.CarryFlag = carry;
                            break;

                        case Instruction.STA:
                            if (IsCarryFlagAddress && (CurrentInstructionOperand & 1) == 1)
                            {
                                this.CarryFlag = true;
                            }
                            else if (IsROMEnable)
                            {
                                ROM[ROMAddress] = AccumulatorRegister;
                            }
                            else if (IsRAMEnable)
                            {
                                RAM[CurrentInstructionOperand] = AccumulatorRegister;
                            }
                            else if (IsIOEnable && Devices.TryGetValue(TargetDevice, out var dev))
                            {
                                dev[(byte)(CurrentInstructionOperand & 0xF)] = AccumulatorRegister;
                            }
                            break;

                        case Instruction.JCC:
                            if (!CarryFlag)
                            {
                                ProgramCounter = ALUOperand;
                            }
                            break;
                    }
                }
            }

            // Next step
            StepCounter = (Step)((((int)StepCounter) + 1) % 2);
        }

        /// <summary>
        /// Loads a big-endian byte stream of 16-bit words into ROM (pads remaining words with 0).
        /// </summary>
        /// <param name="data">Byte array; pairs of bytes form one 16-bit word: (hi, lo).</param>
        public void Load(byte[] data)
        {
            int x = 0;
            int len = data.Length & ~1;
            for (int i = 0; i < len && x < ROM.Length; i += 2)
            {
                ROM[x++] = (ushort)((data[i] << 8) | data[i + 1]);
            }
            // If odd length, pad the last low byte as 0
            if ((data.Length & 1) == 1 && x < ROM.Length)
            {
                ROM[x++] = (ushort)(data[^1] << 8);
            }

            // Pad remainder with zeros
            Array.Clear(ROM, x, ROM.Length - x);
        }

        /// <summary>
        /// Loads a ROM image from a file on disk.
        /// </summary>
        /// <param name="filename">Path to the binary ROM image.</param>
        public void LoadFromFile(string filename)
        {
            if (File.Exists(filename))
            {
                Load(File.ReadAllBytes(filename));
            }
            else
            { 
                throw new FileNotFoundException(filename);
            }
        }

        /// <summary>
        /// Resets the CPU state (PC, IR, A, flags), clears RAM, and resets all registered I/O devices.
        /// </summary>
        public void Reset()
        {
            StepCounter = Step.S0;
            ProgramCounter = 0;
            InstructionRegister = 0;
            AccumulatorRegister = 0;
            CarryFlag = false;
            IndirectedFlag = false;
            this.ClearRAM();
            foreach (var device in this.Devices)
            {
                device.Value.Reset();
            }
        }

        /// <summary>
        /// Fills RAM with zeros.
        /// </summary>
        public void ClearRAM() => Array.Clear(RAM, 0, RAM.Length);

        /// <summary>
        /// Reads a 16-bit value from a virtual address (ROM/RAM/MMIO).
        /// </summary>
        /// <param name="vaddress">Virtual address (e.g., 0x12000 for RAM base).</param>
        /// <returns>Word value at the translated location; 0 if address is invalid or device missing.</returns>
        public ushort LookupValue(uint vaddress)
        {
            if (!Addressing.TryTranslateVirtualAddress(vaddress, Addressing.AddressView.PhysicalOffset, out var offset, out var region))
                return 0;
            return region switch
            {
                Addressing.MemoryRegion.Rom => ROM[offset],
                Addressing.MemoryRegion.Ram => RAM[offset],
                Addressing.MemoryRegion.Mmio => ReadMmio(offset),
                _ => (ushort)0
            };
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Reads a 16-bit value from the MMIO space (device-decoded).
        /// </summary>
        /// <param name="address">Physical MMIO offset (0x000-0x7FF): [10:8]=device, [7:0]=register.</param>
        private ushort ReadMmio(ushort address)
        {
            var deviceId = (DeviceId)((address >> 8) & 7);
            var addr = (byte)(address & 0xFF);
            return Devices.TryGetValue(deviceId, out var dev) ? dev[addr] : (ushort)0;
        }

        /// <summary>
        /// Decodes the addressing mode from the IR (ROM or 3-bit mode from bits [13:11]).
        /// </summary>
        private AddressingMode GetAddressingMode()
        {
            return InstructionUtils.GetAddressingMode(InstructionRegister);
        }

        /// <summary>
        /// Adds two 16-bit values, returning the sum and an unsigned carry-out flag.
        /// </summary>
        private static ushort Add(ushort a, ushort b, out bool overflowFlag)
        {
            unchecked
            {
                ushort c = (ushort)(a + b);
                overflowFlag = (a + b) > ushort.MaxValue;
                return c;
            }
        }

        #endregion
    }
}
