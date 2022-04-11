using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mug.MemoryAccessing
{
    class MemoryAccesser
    {
        private static readonly int PROCESS_ACCESS_ALL = 0x1F0FFF;
        private static readonly uint ALLOCATION_TYPE = 0x1000;
        private static readonly uint FREE_TYPE = 0x8000;
        private static readonly uint MEMORY_PROTECTION = 0x40;

        const int MAX_INSTRUCTION_SIZE = 15;
        const int JUMP_INSTRUCTION_SIZE = 5;
        const byte JUMP_INSTRUCTION_OPERATOR = 0xE9;
        const byte NOP_INSTRUCTION = 0x90;

        //TODO make a layer to avoid using those functions carelessly
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        public Process process = null;
        private IntPtr processHandle;

        private List<AllocatedMemory> memoryAllocations = new List<AllocatedMemory>();
        private List<ReplacedCode> originalCodeAlterations = new List<ReplacedCode>();

        public bool AttachTo(string processName)
        {
            bool success = false;
            var processesFound = Process.GetProcessesByName(processName);
            if (processesFound.Length != 0) {
                process = processesFound[0];
                processHandle = OpenProcess(PROCESS_ACCESS_ALL, false, process.Id);
                if (processHandle != null)
                {
                    success = true;
                }
            }
            return success;
        }

        public IntPtr GetMainModuleAdress()
        {
            return process.MainModule.BaseAddress;
        }

        public IntPtr AllocateMemory(int size)
        {
            var addr = VirtualAllocEx(processHandle, IntPtr.Zero, (uint)size, ALLOCATION_TYPE, MEMORY_PROTECTION);
            memoryAllocations.Add(new AllocatedMemory(this, addr, size));
            var defaultValue = new byte[size];
            for (int i = 0; i < size; i++)
            {
                defaultValue[i] = 0x0;
            }
            WriteBytes(addr, defaultValue);
            return addr;
        }

        public void FreeMemory(IntPtr addr, int size)
        {
            VirtualFreeEx(processHandle, addr, (uint)size, FREE_TYPE);
        }

        public byte[] ReadBytes(IntPtr address, int size, ref int bytes)
        {
            byte[] buffer = new byte[size];
            ReadProcessMemory((int)processHandle, (int)address, buffer, size, ref bytes);
            return buffer;
        }
        public void WriteBytes(IntPtr allocated_memory_address, byte[] bytes)
        {
            WriteProcessMemory(processHandle, allocated_memory_address, bytes, (uint)bytes.Length, out var bytesWritten);
        }

        public void CopyPointer(IntPtr src, IntPtr dest, int size)
        {
            int bytesRead = 0;
            var value = ReadBytes(src, size, ref bytesRead);
            WriteBytes(dest, value);
        }

        private void CodeAlterationWriteBytes(IntPtr addr, byte[] bytes)
        {
            int bytesRead = 0;
            var replacedCode = ReadBytes(addr, bytes.Length, ref bytesRead);
            var injectedCode = new ReplacedCode(this, addr, replacedCode);
            originalCodeAlterations.Add(injectedCode);
            WriteBytes(addr, bytes);
        }

        public byte[] ReadFromOffsets(ProcessModule m, int[] offsets, int size, ref int bytes)
        {
            int pointerBytesRead = 0;
            var memoryAddress = m.BaseAddress + offsets[0];
            for (int i = 1; i < offsets.Length; i++)
            {
                var pointerBytes = ReadBytes(memoryAddress, sizeof(int), ref pointerBytesRead);
                memoryAddress = (IntPtr)BitConverter.ToInt32(pointerBytes, 0);
                memoryAddress += offsets[i];
            }
            return ReadBytes(memoryAddress, size, ref bytes);
        }

        public byte[] ReadMemoryValue(MemoryValue val, ref int bytes, ProcessModule m = null)
        {
            return ReadFromOffsets(m == null ? process.MainModule : m, val.offsets, val.size, ref bytes);
        }

        public IntPtr InjectCode(byte[] code)
        {
            var allocated_memory_address = AllocateMemory(code.Length);
            CodeAlterationWriteBytes(allocated_memory_address, code);
            return allocated_memory_address;
        }

        //TODO FIX will totally fuck up relative addresses, return instructions ...
        public IntPtr InsertCode(byte[] codeToInsert, IntPtr insertAddress)
        {
            //Original code at this address
            var originalInstructions = OriginalInstructionsAt(insertAddress);

            //Init
            byte[] fullCode = new byte[codeToInsert.Length + originalInstructions.Length + JUMP_INSTRUCTION_SIZE];
            var newFunctionAddress = AllocateMemory(fullCode.Length);
            var originalAddressJumpFrom = (int)insertAddress + JUMP_INSTRUCTION_SIZE;
            var originalAddressJumpTo = (int)insertAddress + originalInstructions.Length;

            //Instruction to jump back to original code
            var newCodeRelativeAddress = originalAddressJumpTo - ((int)newFunctionAddress + fullCode.Length);//target - current
            byte[] returnJumpInstruction = CreateJumpInstruction(newCodeRelativeAddress);

            //Fill new code array with bytes
            Array.Copy(codeToInsert, 0, fullCode, 0, codeToInsert.Length);
            Array.Copy(originalInstructions, 0, fullCode, codeToInsert.Length, originalInstructions.Length);
            Array.Copy(returnJumpInstruction, 0, fullCode, codeToInsert.Length + originalInstructions.Length, returnJumpInstruction.Length);

            //Write inserted code
            WriteBytes(newFunctionAddress, fullCode);

            //Instruction to jump to added code
            byte[] replacementCode = new byte[originalInstructions.Length];
            var originalCodeRelativeAddress = (int)newFunctionAddress - (originalAddressJumpFrom);//target - current
            byte[] jumpToNewCode = CreateJumpInstruction(originalCodeRelativeAddress);
            Array.Copy(jumpToNewCode, replacementCode, jumpToNewCode.Length);
            for (var i = jumpToNewCode.Length; i < replacementCode.Length; i++)
            {
                replacementCode[i] = NOP_INSTRUCTION;
            }

            CodeAlterationWriteBytes(insertAddress, replacementCode);
            return newFunctionAddress;
        }

        private byte[] CreateJumpInstruction(int relativeAddressInt)
        {

            var relativeAddressByte = addressToBytes(relativeAddressInt);
            byte[] jumpInstruction = new byte[JUMP_INSTRUCTION_SIZE];
            jumpInstruction[0] = JUMP_INSTRUCTION_OPERATOR;
            Array.Copy(relativeAddressByte, 0, jumpInstruction, 1, JUMP_INSTRUCTION_SIZE - 1);
            return jumpInstruction;
        }

        private byte[] addressToBytes(int addr)
        {
            byte[] intBytes = BitConverter.GetBytes(addr);
            return intBytes;
        }

        private byte[] OriginalInstructionsAt(IntPtr address)
        {
            SharpDisasm.ArchitectureMode mode = SharpDisasm.ArchitectureMode.x86_32;

            var found = false;
            var bytesRead = 0;
            var i = JUMP_INSTRUCTION_SIZE;
            byte[] originalBytes = new byte[] { };

            while (i < MAX_INSTRUCTION_SIZE && !found)
            {
                byte[] byteArray = ReadBytes(address, i, ref bytesRead);
                var instructions = (new SharpDisasm.Disassembler(byteArray, mode)).Disassemble();
                var invalidInstructionsCount = instructions.Where((SharpDisasm.Instruction inst) => inst.Error).ToList().Count;
                if (invalidInstructionsCount == 0)
                {
                    originalBytes = byteArray;
                    found = true;
                }
                i++;
            }
            return originalBytes;
        }

        public void Execute(IntPtr allocated_memory_address)
        {
            CreateRemoteThread(processHandle, IntPtr.Zero, 0, allocated_memory_address, IntPtr.Zero, 0, IntPtr.Zero);
        }

        public void ResetTargetProcessMemory()
        {

            if (process == null || process.HasExited) return;

            foreach(var allocatedMemory in memoryAllocations)
            {
                allocatedMemory.Free();
            }

            foreach(var replacedCode in originalCodeAlterations)
            {
                replacedCode.Revert();
            }

            memoryAllocations.Clear();
            originalCodeAlterations.Clear();
        }
    }

    class AllocatedMemory{
        public MemoryAccesser Accesser { get; private set; }
        public IntPtr Addr { get; private set; }
        public int Size { get; private set; }

        public AllocatedMemory(MemoryAccesser accesser, IntPtr addr, int size)
        {
            Accesser = accesser;
            Addr = addr;
            Size = size;
        }

        public void Free()
        {
            Accesser.FreeMemory(Addr, Size);
        }
    }

    class ReplacedCode
    {
        public MemoryAccesser Accesser { get; private set; }
        public IntPtr Addr { get; private set; }
        public byte[] OriginalCode { get; private set; }

        public ReplacedCode(MemoryAccesser accesser, IntPtr addr, byte[] code)
        {
            Accesser = accesser;
            Addr = addr;
            OriginalCode = code;
        }

        public void Revert()
        {
            Accesser.WriteBytes(Addr, OriginalCode);
        }
    }
}
