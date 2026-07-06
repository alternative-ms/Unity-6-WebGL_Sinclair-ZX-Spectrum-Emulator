// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System.IO;
using System.Collections.Generic;

namespace Speculator.Core.Tape
{
    public class TapeLoader
    {
        private int m_currentTapeBlockIndex;
        private List<TapeBlock> m_tapeBlocks;
        private FileInfo m_tapeFile;
        private CPU m_theCpu;

        public bool IsLoading => m_tapeFile?.Exists == true;

        public void SetCpu(CPU cpu)
        {
            m_theCpu = cpu;
            m_theCpu.PoweredOff += (_, _) => Stop();
        }

        public void Load(FileInfo tapeFile)
        {
            m_tapeBlocks = null;
            m_tapeFile = tapeFile;
        }

        public bool? GetTapeSignal()
        {
            if (m_tapeFile?.Exists != true)
            {
                // No tape.
                Stop();
                return null;
            }

            // Load tape data.
            if (m_tapeBlocks == null)
            {
                var tapeBytes = File.ReadAllBytes(m_tapeFile.FullName);
                m_tapeBlocks = new List<TapeBlock>();

                var i = 0;
                while (i < tapeBytes.Length)
                {
                    var blockSize = tapeBytes[i++] + (tapeBytes[i++] << 8);
                    m_tapeBlocks.Add(new TapeBlock(tapeBytes, i, blockSize));

                    i += blockSize;
                }

                m_currentTapeBlockIndex = 0;
            }

            // Get current tape block and sure its 'tones' are populated.
            bool? signal = null;
            while (!signal.HasValue && m_tapeFile != null)
            {
                signal = m_tapeBlocks[m_currentTapeBlockIndex]
                    .PopulateTones(m_theCpu.TStatesSinceCpuStart)
                    .GetSignal(m_theCpu.TStatesSinceCpuStart);

                if (signal != null)
                    continue; // We have a signal.

                // End of block reached - Move to the next.
                m_currentTapeBlockIndex++;
                if (m_currentTapeBlockIndex < m_tapeBlocks.Count)
                    continue;

                // No more tape!
                Stop();
                return false;
            }

            return signal;
        }

        private void Stop()
        {
            m_tapeBlocks = null;
            m_tapeFile = null;
        }
    }
}