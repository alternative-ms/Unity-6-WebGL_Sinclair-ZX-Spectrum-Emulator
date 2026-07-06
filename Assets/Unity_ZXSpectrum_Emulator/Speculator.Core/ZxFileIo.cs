// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Speculator.Core.Tape;

namespace Speculator.Core
{
    public class ZxFileIo
    {
        private readonly CPU m_cpu;
        private readonly ZxDisplay m_zxDisplay;
        private readonly TapeLoader m_tapeLoader;

        public enum RomType { System, Game }

        public ZxFileIo(CPU cpu, ZxDisplay zxDisplay, TapeLoader tapeLoader)
        {
            m_cpu = cpu;
            m_zxDisplay = zxDisplay;
            m_tapeLoader = tapeLoader;
        }

        public void LoadFile(FileInfo fileInfo)
        {
            // Use ClockSync if it exists, otherwise just load it
            using (m_cpu.ClockSync?.CreatePauser())
            {
                if (!fileInfo.Exists) return;

                // now block the CPU while writing to memory
                lock (m_cpu.CpuStepLock)
                {
                    LoadFileInternal(fileInfo);
                }
            }
        }

        private void LoadFileInternal(FileInfo fileInfo)
        {
            switch (fileInfo.Extension.ToLower())
            {
                case ".z80":
                    LoadZ80(fileInfo);
                    break;
                case ".tap":
                    m_tapeLoader.Load(fileInfo);
                    break;
            }
        }

        private void LoadZ80(FileInfo file)
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
            m_cpu.TheRegisters.Clear();
            m_cpu.TheRegisters.Main.A = (byte)stream.ReadByte();
            m_cpu.TheRegisters.Main.F = (byte)stream.ReadByte();
            m_cpu.TheRegisters.Main.BC = ReadZxWord(stream);
            m_cpu.TheRegisters.Main.HL = ReadZxWord(stream);
            m_cpu.TheRegisters.PC = ReadZxWord(stream);
            m_cpu.TheRegisters.SP = ReadZxWord(stream);
            m_cpu.TheRegisters.I = (byte)stream.ReadByte();
            m_cpu.TheRegisters.R = (byte)(stream.ReadByte() & 0x7F);

            var byte12 = (byte)stream.ReadByte();
            if (byte12 == 0xFF) byte12 = 0x01;
            if ((byte12 & 0x01) != 0) m_cpu.TheRegisters.R |= 0x80;

            if (m_zxDisplay != null) m_zxDisplay.BorderAttr = (byte)((byte12 & 0x0e) >> 1);

            var isDataCompressed = (byte12 & 0x20) != 0;

            m_cpu.TheRegisters.Main.DE = ReadZxWord(stream);
            m_cpu.TheRegisters.Alt.BC = ReadZxWord(stream);
            m_cpu.TheRegisters.Alt.DE = ReadZxWord(stream);
            m_cpu.TheRegisters.Alt.HL = ReadZxWord(stream);
            m_cpu.TheRegisters.Alt.A = (byte)stream.ReadByte();
            m_cpu.TheRegisters.Alt.F = (byte)stream.ReadByte();
            m_cpu.TheRegisters.IY = ReadZxWord(stream);
            m_cpu.TheRegisters.IX = ReadZxWord(stream);
            m_cpu.TheRegisters.IFF1 = stream.ReadByte() != 0;
            m_cpu.TheRegisters.IFF2 = stream.ReadByte() != 0;
            m_cpu.TheRegisters.IM = (byte)(stream.ReadByte() & 0x03);

            var isVersion1 = m_cpu.TheRegisters.PC != 0x0000;
            if (isVersion1)
            {
                var bytesToRead = (int)(stream.Length - stream.Position);
                var data = ReadBytes(stream, bytesToRead);
                if (isDataCompressed)
                {
                    Decompress(data);
                    if (data.Count > 4) data.RemoveRange(data.Count - 4, 4);
                }
                m_cpu.MainMemory.LoadData(data, 0x4000);

                return;
            }

            int extendedHeaderLength = ReadZxWord(stream);
            var extendedHeader = new byte[extendedHeaderLength];
            stream.Read(extendedHeader, 0, extendedHeaderLength);
            m_cpu.TheRegisters.PC = (ushort)((extendedHeader[1] << 8) + extendedHeader[0]); // IndexOutOfRangeException: Index was outside the bounds of the array.

            while (stream.Position < stream.Length)
            {
                var blockSize = ReadZxWord(stream);
                var pageNumber = stream.ReadByte();
                var data = ReadBytes(stream, blockSize == 0xFFFF ? 16384 : blockSize);
                if (blockSize != 0xFFFF) Decompress(data);

                switch (pageNumber)
                {
                    case 4: m_cpu.MainMemory.LoadData(data, 0x8000); break;
                    case 5: m_cpu.MainMemory.LoadData(data, 0xC000); break;
                    case 8: m_cpu.MainMemory.LoadData(data, 0x4000); break;
                }
            }
        }

        private static ushort ReadZxWord(Stream s) => (ushort)(s.ReadByte() | (s.ReadByte() << 8));

        private static List<byte> ReadBytes(Stream stream, int byteCount)
        {
            var data = new List<byte>(byteCount);
            for (var i = 0; i < byteCount; i++) data.Add((byte)stream.ReadByte());

            return data;
        }

        private static void Decompress(List<byte> data)
        {
            var offset = 0;
            while (offset < data.Count - 3)
            {
                if (data[offset] == 0xED && data[offset + 1] == 0xED)
                {
                    var count = data[offset + 2];
                    var b = data[offset + 3];
                    data.RemoveRange(offset, 4);
                    for (var i = 0; i < count; i++) data.Insert(offset, b);
                    offset += count;
                }
                else offset++;
            }
        }

        private static bool ReportHardwareMode(int hardwareMode)
        {
            var modeDescription = hardwareMode switch
            {
                0 => "48K Spectrum",
                1 => "48K Spectrum + Interface 1",
                2 => "SamRam",
                3 => "128K Spectrum",
                4 => "128K Spectrum + Interface 1",
                _ => $"Unknown hardware mode: {hardwareMode}"
            };

            var isSupported = hardwareMode <= 1;
            if (isSupported) return true;

            UnityEngine.Debug.LogWarning($"Unsupported model: {modeDescription}");
            return false;
        }

    }
}