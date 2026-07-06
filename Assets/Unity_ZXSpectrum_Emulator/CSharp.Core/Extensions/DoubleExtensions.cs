// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System;

namespace CSharp.Core.Extensions
{
    public static class DoubleExtensions
    {
        public static double Clamp(this double f, double min, double max) => Math.Max(min, Math.Min(max, f));
    }
}