// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System.Linq;
using System.Collections.Generic;

namespace Speculator.Core.Tape
{
    /// <summary>
    /// Stores an array of hi/lo pulses representing the sound of a single  .tap tape block.
    /// </summary>
    public class SoundBuffer
    {
        public List<(bool level, long tStateLength, long tStateStart)> Levels { get; } = new();

        public void Add(bool level, long tStateLength)
        {
            long tStateStart;
            if (Levels.Any())
            {
                var prev = Levels.Last();
                tStateStart = prev.tStateStart + prev.tStateLength;
            }
            else
                tStateStart = 0;

            Levels.Add((level, tStateLength, tStateStart));
        }

        /// <summary>
        /// Comparer for binary-searching. 
        /// </summary>
        public class Comparer : IComparer<(bool level, long tStateLength, long tStateStart)>
        {
            public int Compare((bool level, long tStateLength, long tStateStart) x, (bool level, long tStateLength, long tStateStart) y) =>
                x.tStateStart.CompareTo(y.tStateStart);
        }

        /// <summary>
        /// Reserve memory to store 'count' more bytes.
        /// </summary>
        public void ReserveExtra(int count)
        {
            int requiredCapacity = Levels.Count + count;
            if (Levels.Capacity < requiredCapacity)
            {
                Levels.Capacity = requiredCapacity;
            }
        }
    }
}