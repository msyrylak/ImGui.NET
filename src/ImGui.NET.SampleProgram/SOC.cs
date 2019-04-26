using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImGuiNET
{
    class SOC
    {
        // highlight byte used in the gui saying how many bytes to hihglight
        public ushort highlightbyte = 1;

        //
        public static Dictionary<ushort, ushort> changes = new Dictionary<ushort, ushort>();

        // constants
        const int k_numRegisters = 3;
        const int R0 = 0;
        const int R1 = 1;
        const int R2 = 2;


        // general purpose registers
        public byte[] registers = new byte[k_numRegisters];

        // status register
        public static byte SR;

        // flags
        const byte CARRY_SHIFT = 0;
        const byte ZERO_SHIFT = 1;
        const byte INTERRUPT_SHIFT = 2;
        const byte BREAK_SHIFT = 4;
        const byte OVERFLOW_SHIFT = 6;
        const byte NEGATIVE_SHIFT = 7;

        // performing bitwise shift to accommodate flags in their respective places on the SR
        const byte FLG_CARRY = 1 << CARRY_SHIFT;
        const byte FLG_ZERO = 1 << ZERO_SHIFT;
        const byte FLG_INTERRUPT = 1 << INTERRUPT_SHIFT;
        const byte FLG_BREAK = 1 << BREAK_SHIFT;
        const byte FLG_OVERFLOW = 1 << OVERFLOW_SHIFT;
        const byte FLG_NEGATIVE = 1 << NEGATIVE_SHIFT;


        // program counter
        public static ushort PC;

        // stack pointer 
        public static byte SP;

        // reset vectors
        public const ushort resetVectorH = 0xFFFD;
        public const ushort resetVectorL = 0xFFFC;

        // interrupt vectors
        public const ushort interruptVectorH = 0xFFFF;
        public const ushort interruptVectorL = 0xFFFE;

        public static byte[] memory = new byte[65536];

        public delegate void AssemblyOperation(byte[] registers, int index, ushort source);
        AssemblyOperation[] OpCodes = new AssemblyOperation[16];

        public delegate ushort AddressingMode();
        AddressingMode[] AddressingModes = new AddressingMode[3];

        byte[] InstructionSet = new byte[64];

        public SOC()
        {
            Reset();

            OpCodes[0x00] = Op_BRK;
            OpCodes[0x01] = Op_LD;
            OpCodes[0x02] = Op_ST;
            OpCodes[0x03] = Op_ADD;
            OpCodes[0x04] = Op_JMP;
            OpCodes[0x05] = Op_JPC;
            OpCodes[0x06] = Op_JPZ;
            OpCodes[0x07] = Op_JPN;
            OpCodes[0x08] = Op_PH;
            OpCodes[0x09] = Op_PL;
            OpCodes[0x0A] = Op_AND;
            OpCodes[0x0B] = Op_XOR;
            OpCodes[0x0C] = Op_CLC;

            AddressingModes[0x00] = Addr_ABS;
            AddressingModes[0x01] = Addr_IMM;
            AddressingModes[0x02] = Addr_IMP;


            // InstructionSet (opcode, addresing mode, register index)
            // instructions using r0
            InstructionSet[0x00] = InstructionManager(0x00, 0x00, R0); // brk
            InstructionSet[0x01] = InstructionManager(0x01, 0x01, R0); // ld
            InstructionSet[0x02] = InstructionManager(0x02, 0x00, R0); // st
            InstructionSet[0x03] = InstructionManager(0x03, 0x00, R0); // add
            InstructionSet[0x04] = InstructionManager(0x04, 0x00, R0); // jmp
            InstructionSet[0x05] = InstructionManager(0x05, 0x01, R0); // jpc
            InstructionSet[0x06] = InstructionManager(0x06, 0x00, R1); // jpz
            InstructionSet[0x07] = InstructionManager(0x07, 0x01, R0); // jpn
            InstructionSet[0x08] = InstructionManager(0x08, 0x02, R0); // ph
            InstructionSet[0x09] = InstructionManager(0x09, 0x02, R0); // pl
            InstructionSet[0x0A] = InstructionManager(0x0A, 0x02, R0); // and
            InstructionSet[0x0B] = InstructionManager(0x0B, 0x01, R0); // xor
            InstructionSet[0x0C] = InstructionManager(0x0C, 0x02, R0); // clc
            InstructionSet[0x15] = InstructionManager(0x03, 0x01, R0); // add with immediate addressing mode


            // instructions using r1
            InstructionSet[0x0D] = InstructionManager(0x01, 0x01, R1); // ld
            InstructionSet[0x0E] = InstructionManager(0x02, 0x00, R1); // st
            InstructionSet[0x0F] = InstructionManager(0x03, 0x01, R1); // add
            InstructionSet[0x11] = InstructionManager(0x0A, 0x01, R1); // and

            // instructions using r2
            InstructionSet[0x12] = InstructionManager(0x01, 0x01, R2); // ld
            InstructionSet[0x13] = InstructionManager(0x03, 0x01, R2); // add
            InstructionSet[0x14] = InstructionManager(0x02, 0x00, R2); // st
            InstructionSet[0x10] = InstructionManager(0x0B, 0x01, R2); // xor

        }


        public void Reset()
        {
            // set registers to 0
            registers[R0] = 0;
            registers[R1] = 0;
            registers[R2] = 0;


            SR = 0x00;
            SP = 0xFD; // set to 253

            // set pc to the values in reset vector
            PC = (ushort)((Read(resetVectorH) << 8) + (Read(resetVectorL)));

        }


        // method for creating instructions
        byte InstructionManager(byte opCode, byte address, int reg)
        {
            // assign bit representation of the instruction parameters 
            byte operationBits = opCode;
            byte addressBits = (byte)(address << 6);
            byte registerBits = (byte)(reg << 4);

            // combine them with logical OR
            byte instruction = (byte)(addressBits | registerBits | operationBits);

            return instruction;

        }


        // read and write functions
        public static byte Read(ushort address)
        {
            // return value from memory at given address
            return memory[address];
        }


        public static void Write(ushort address, byte value)
        {
            // check if the dictionary holding any changes
            // in the memory already contains the address that the instruction is trying to write to
            if (changes.ContainsKey(address))
            {
                // modify the value at the key (address)
                changes[address] = memory[address];
            }
            else
            {
                // add to the dictionary both key and value
                changes.Add(address, memory[address]);

            }

            // write to memory 
            memory[address] = value;
        }


        // flag sets
        private static void SetFlags(bool expression, byte flag)
        {
            // if expression is true set the flag as on
            if (expression)
            {
                SR |= flag;
            }
            else
            {
                // set the flag as off
                SR &= (byte)~flag;
            }
        }


        public static void SetCarry(bool expression)
        {
            SetFlags(expression, FLG_CARRY);
        }


        public static void SetZero(bool expression)
        {
            SetFlags(expression, FLG_ZERO);
        }


        public static void SetInterrupt(bool expression)
        {
            SetFlags(expression, FLG_INTERRUPT);
        }


        public static void SetBreak(bool expression)
        {
            SetFlags(expression, FLG_BREAK);
        }


        public static void SetOverflow(bool expression)
        {
            SetFlags(expression, FLG_OVERFLOW);
        }


        public static void SetNegative(bool expression)
        {
            SetFlags(expression, FLG_NEGATIVE);
        }


        // flag checks
        public static bool IfCarry()
        {
            bool flagCheck = (SR & FLG_CARRY) != 0 ? true : false;
            return flagCheck;
        }


        public static bool IfZero()
        {
            bool flagCheck = (SR & FLG_ZERO) != 0 ? true : false;
            return flagCheck;
        }


        public static bool IfInterrupt()
        {
            bool flagCheck = (SR & FLG_INTERRUPT) != 0 ? true : false;
            return flagCheck;
        }


        public static bool IfBreak()
        {
            bool flagCheck = (SR & FLG_BREAK) != 0 ? true : false;
            return flagCheck;
        }


        public static bool IfOverflow()
        {
            bool flagCheck = (SR & FLG_OVERFLOW) != 0 ? true : false;
            return flagCheck;
        }


        public static bool IfNegative()
        {
            bool flagCheck = (SR & FLG_NEGATIVE) != 0 ? true : false;
            return flagCheck;
        }


        // immediate
        public ushort Addr_IMM()
        {
            highlightbyte++;
            return PC++;
        }


        // implied
        public ushort Addr_IMP()
        {
            return 0;
        }


        // absolute
        public ushort Addr_ABS()
        {
            byte addressLowByte;
            byte addressHighByte;
            byte address;

            addressLowByte = Read(PC++);
            addressHighByte = Read(PC++);

            highlightbyte = 2;

            address = (byte)(addressLowByte + (addressHighByte << 8));

            return address;
        }


        // stack operations
        // push to the stack
        public static void StackPush(byte value)
        {
            // calculate the address so that the value can be written in the stack space in memory
            ushort address = (ushort)(0x0100 + SP);
            Write(address, value);
            SP--;
        }


        // read value from the stack
        public static byte StackPop()
        {
            SP++;
            ushort address = (ushort)(0x0100 + SP);
            return Read(address);
        }


        // main cpu function responsible for fetch-decode-execute cycle
        public void Run(int cycles)
        {
            byte opcode;
            byte instruction;

            for (int i = 0; i < cycles; i++)
            {
                // fetch
                opcode = Read(PC++);

                // decode
                instruction = InstructionSet[opcode];

                highlightbyte = 0; // reset the highlight
                //execute
                Execute(instruction);
            }
        }


        // method for decoding and executing the instruction
        void Execute(byte instruction)
        {
            // decode opcode, addressing mode and register to be used 
            byte opCode = (byte)(instruction & 0x0F);
            byte addressingMode = (byte)(instruction >> 6);
            byte registerBit = (byte)((instruction >> 4) & 3);

            // read the address by calling the delegate function from the array
            ushort src = AddressingModes[addressingMode]();

            // execute the assembly operation by calling the delegate function from the array
            OpCodes[opCode](registers, registerBit, src);
        }


        // assembly instructions
        // break
        public void Op_BRK(byte[] registers, int index, ushort source)
        {
            PC++;
            byte PCHighByte = (byte)((PC >> 8) & 0xFF);
            byte PCLowByte = (byte)(PC & 0xFF);
            StackPush(PCHighByte);
            StackPush(PCLowByte);
            StackPush(SR);
            SetInterrupt(true);
            ushort newAddress = (ushort)(Read(interruptVectorH) << 8);
            PC = (ushort)(newAddress + Read(interruptVectorL));
        }


        // load
        public void Op_LD(byte[] registers, int index, ushort source)
        {
            byte memory = Read(source); // read value from memory address
            bool negativeCheck = (memory & 0x80) < 0 ? true : false; // check if negative
            SetNegative(negativeCheck);
            bool zeroCheck = memory == 0 ? true : false; // check if zero
            SetZero(zeroCheck);
            registers[index] = memory; // load the value into the register
        }


        // store
        public void Op_ST(byte[] registers, int index, ushort source)
        {
            Write(source, registers[index]); // write value from register into memory
        }


        // add
        public void Op_ADD(byte[] registers, int index, ushort source)
        {
            ushort value = Read(source); // read value from memory
            uint temp = (uint)(value + registers[index] + (IfCarry() ? 1 : 0)); // add it to the value in register
            bool carryCheck = temp > 0xFF ? true : false;
            SetCarry(carryCheck); // set carry if value cannot be stored in a byte 
            bool zeroCheck = (~(temp & 0xFF)) == 0 ? true : false; // check if zero
            SetZero(zeroCheck);
            registers[index] = (byte)(temp & 0xFF); // write the result back to the register
        }


        // jump
        public void Op_JMP(byte[] registers, int index, ushort source)
        {
            PC = source; // set the pc to an arbitrary address
        }


        // jump if carry
        public void Op_JPC(byte[] registers, int index, ushort source)
        {
            if (IfCarry())
            {
                PC += source;
            }
        }


        // jump if zero
        public void Op_JPZ(byte[] registers, int index, ushort source)
        {
            if (IfZero())
            {
                PC += source;
            }
        }


        // jump if negative
        public void Op_JPN(byte[] registers, int index, ushort source)
        {
            if (IfNegative())
            {
                PC += source;
            }
        }


        // stack push
        public void Op_PH(byte[] registers, int index, ushort source)
        {
            StackPush(registers[index]);
        }


        // stack pull
        public void Op_PL(byte[] registers, int index, ushort source)
        {
            registers[index] = StackPop();
            bool negativeCheck = (registers[index] & 0x80) < 0 ? true : false;
            SetNegative(negativeCheck);
            bool zeroCheck = (~registers[index]) == 0 ? true : false;
            SetZero(zeroCheck);
        }


        // logical and
        public void Op_AND(byte[] registers, int index, ushort source)
        {
            byte memory = Read(source); // read value 
            byte result = (byte)(registers[index] & memory); // perform anding
            bool negativeCheck = (result & 0x80) < 0 ? true : false; // check if negative
            SetNegative(negativeCheck);
            bool zeroCheck = (result) == 0 ? true : false; // check if zero
            SetZero(zeroCheck);
            registers[index] = result; // write result into a regsiter
        }


        // logical xor
        public void Op_XOR(byte[] registers, int index, ushort source)
        {
            byte memory = Read(source); // read value
            byte result = (byte)(registers[index] ^ memory); // perform xor
            bool negativeCheck = (result & 0x80) < 0 ? true : false; // check if negative
            SetNegative(negativeCheck);
            bool zeroCheck = (~result) == 0 ? true : false; // check if zero
            SetZero(zeroCheck);
            registers[index] = result;  // write result into a register
        }


        // clear carry flag
        public void Op_CLC(byte[] registers, int index, ushort source)
        {
            SetCarry(false);
        }


        // save program into a binary file
        public void SaveProgram(byte[] memory, int size)
        {
            BinaryWriter binaryWriter;

            try
            {
                binaryWriter = new BinaryWriter(new FileStream("addition.dat", FileMode.Create));
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message + "\n Cannot create the file");
                throw;
            }

            try
            {
                for (int i = 0; i < size; i++)
                {
                    binaryWriter.Write(memory[i]);
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message + "\n Cannot write to the file");
                throw;
            }
        }


        // load program into memory
        public void LoadProgram(byte[] memory, int size, string programName)
        {
            BinaryReader binaryReader;

            try
            {
                binaryReader = new BinaryReader(new FileStream(programName + ".dat", FileMode.Open));
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message + "\n Cannot open file");
                throw;
            }

            try
            {
                for (int i = 0; i < size; i++)
                {
                    memory[i] = binaryReader.ReadByte();
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message + "\n Cannot read from the file");
                throw;
            }
        }
    }
}
