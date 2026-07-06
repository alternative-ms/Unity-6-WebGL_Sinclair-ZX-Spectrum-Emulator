// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System.Diagnostics;

namespace CSharp.Core.Extensions
{
    public static class ByteExtensions
    {
        public static bool IsBitSet(this byte b, byte i)
        {
            Debug.Assert(i <= 7, "Index out of range.");
            return (b & (1 << i)) != 0;
        }

        public static byte ResetBit(this byte b, byte i)
        {
            Debug.Assert(i <= 7, "Index out of range.");
            var mask = (byte)~(1 << i);
            return (byte)(b & mask);
        }

        public static byte SetBit(this byte b, byte i)
        {
            Debug.Assert(i <= 7, "Index out of range.");
            return (byte)(b | (1 << i));
        }
    }
}