// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Speculator.Core
{
    public class Memory
    {
        private int m_romSize;

        /// <summary>
        /// Raised when a large chunk of data is loaded from an external source (I.e. Disk).
        /// </summary>
        public event EventHandler DataLoaded;

        public byte[] MemoryData { get; } = new byte[0x10000];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Poke(ushort addr, byte value)
        {
            if (IsRomArea(addr))
                return MemoryData[addr]; // Can't write to ROM.
            MemoryData[addr] = value;
            return value;
        }

        public void Poke(ushort addr, ushort v)
        {
            Poke(addr, (byte)(v & 0x00ff));
            Poke((ushort)(addr + 1), (byte)(v >> 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Peek(ushort addr) => MemoryData[addr];

        public ushort PeekWord(ushort addr) =>
            (ushort)(MemoryData[(ushort)(addr + 1)] << 8 | MemoryData[addr]);

        public string ReadAsHexString(ushort addr, ushort byteCount, bool wantSpaces = false)
        {
            var result = new StringBuilder();
            for (var i = 0; i < byteCount && addr + i <= 0xffff; i++)
            {
                result.Append($"{Peek((ushort)(addr + i)):X2}");
                if (wantSpaces) result.Append(' ');
            }

            return result.ToString().Trim();
        }

        public void LoadRom(FileInfo systemRom)
        {
            UnityEngine.Debug.Log($"Loading ROM '{systemRom.FullName}'.");

            // Here we use a regular File.ReadAllBytes to send a full file path
            var romBytes = System.IO.File.ReadAllBytes(systemRom.FullName);

            UnityEngine.Debug.Log($"ROM size: {romBytes.Length} bytes.");
            if (romBytes.Length > 0xffff)
            {
                UnityEngine.Debug.LogError("ROM is too large to fit in memory.");
                return;
            }

            // Clear entire array (array, start index, lenght)
            Array.Clear(MemoryData, 0, MemoryData.Length);

            m_romSize = romBytes.Length;
            LoadData(romBytes, 0x0000);
        }


        public bool IsRomArea(ushort addr) => addr < m_romSize;

        /// <summary>
        /// Bulk load data into memory (such as from disk).
        /// </summary>
        public void LoadData(IList<byte> data, ushort addr)
        {
            data.CopyTo(MemoryData, addr);
            DataLoaded?.Invoke(this, EventArgs.Empty);
        }
    }
}