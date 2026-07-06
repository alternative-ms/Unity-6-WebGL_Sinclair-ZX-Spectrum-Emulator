// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
namespace Speculator.Core
{
    public interface IPortHandler
    {
        byte In(ushort portAddress);
        void Out(byte port, byte b);
    }
}