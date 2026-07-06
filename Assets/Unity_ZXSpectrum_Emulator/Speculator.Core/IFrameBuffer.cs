// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
namespace Speculator.Core
{
    public interface IFrameBuffer
    {
        void SetPixel(int x, int y, byte r, byte g, byte b);
    }
}
