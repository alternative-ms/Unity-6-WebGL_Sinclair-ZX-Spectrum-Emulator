// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine; // for Unity's Texture2D works
using CSharp.Core.Extensions;
using CSharp.Core.ViewModels;

namespace Speculator.Core
{
    public class ZxDisplay : ViewModelBase
    {
        public const int ScreenBase = 0x4000;

        private const int ColorMapBase = 0x5800;
        private const int LeftMargin = 32;
        private const int RightMargin = 32;
        private const int TopMargin = 24;
        private const int BottomMargin = 24;
        private const int WriteableWidth = 256;
        private const int WritableHeight = 192;
        private const int FramesPerFlash = 16;

        private bool m_isCrt = true;
        private Vector3 m_scanlineMultiplier = Vector3.one;
        private float m_phosphorShrink = 1.0f;
        private float m_brightness = 1.0f;
        private int m_flashFrameCount;
        private bool m_isFlashing;
        private DateTime m_lastFlashTime = DateTime.Now;
        private double m_emulationSpeed;
        private bool m_isPaused;
        private readonly System.Random m_random = new System.Random(0);

        // Pixel grid (color indices)
        private readonly byte[][] m_screenBuffer;

        // Unity's texture instead of WriteableBitmap
        public Texture2D Texture { get; private set; }
        private Color32[] m_nativePixels;
        private readonly Color32[] m_palette;

        private bool m_didPixelsChange;

        private static readonly Vector3[] Colors =
        {
            new Vector3(0, 0, 0),       // Black
            new Vector3(0, 0, 205),     // Blue
            new Vector3(205, 0, 0),     // Red
            new Vector3(205, 0, 205),   // Magenta
            new Vector3(0, 205, 0),     // Green
            new Vector3(0, 205, 205),   // Cyan
            new Vector3(205, 205, 0),   // Yellow
            new Vector3(205, 205, 205), // White
            new Vector3(0, 0, 0),       // Bright Black
            new Vector3(0, 0, 255),     // Bright Blue
            new Vector3(255, 0, 0),     // Bright Red
            new Vector3(255, 0, 255),   // Bright Magenta
            new Vector3(0, 255, 0),     // Bright Green
            new Vector3(0, 255, 255),   // Bright Cyan
            new Vector3(255, 255, 0),   // Bright Yellow
            new Vector3(255, 255, 255)  // Bright White
        };

        public byte BorderAttr { get; set; }

        public bool IsPaused
        {
            get => m_isPaused;
            set
            {
                if (m_isPaused == value) return;
                m_isPaused = value;
                if (m_isPaused) EmulationSpeed = 0.0;
                m_didPixelsChange = true;
            }
        }

        public double EmulationSpeed
        {
            get => m_emulationSpeed;
            private set => SetField(ref m_emulationSpeed, value);
        }

        public event EventHandler Refreshed;

        public ZxDisplay()
        {
            int width = LeftMargin + WriteableWidth + RightMargin; // 320
            int height = TopMargin + WritableHeight + BottomMargin; // 240

            // create a indices buffer
            m_screenBuffer = new byte[height][];
            for (int i = 0; i < height; i++)
                m_screenBuffer[i] = new byte[width];

            // initialize Unity's texture
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Texture.filterMode = FilterMode.Point;
            Texture.wrapMode = TextureWrapMode.Clamp;
            m_nativePixels = new Color32[width * height];

            // preparing a palette
            m_palette = Colors.Select(v => new Color32((byte)v.x, (byte)v.y, (byte)v.z, 255)).ToArray();
        }

        private static (byte, byte) GetColorIndices(byte attr, bool invert = false)
        {
            var paperIndex = (byte)(attr >> 3 & 0x07);
            var penIndex = (byte)(attr & 0x07);
            if ((attr & 0x40) != 0)
            {
                paperIndex += 8;
                penIndex += 8;
            }
            return invert ? (paperIndex, penIndex) : (penIndex, paperIndex);
        }

        public void UpdateScreen()
        {
            int width = Texture.width;
            int height = Texture.height;

            for (int y = 0; y < height; y++)
            {
                byte[] row = m_screenBuffer[y];
                int unityY = (height - 1 - y) * width; // invert Y for Unity
                for (int x = 0; x < width; x++)
                {
                    m_nativePixels[unityY + x] = m_palette[row[x]];
                }
            }

            Texture.SetPixels32(m_nativePixels);
            Texture.Apply();
            Refreshed?.Invoke(this, EventArgs.Empty);
        }

        public void OnRenderScanline(object sender, (Memory memory, int scanline) args)
        {
            bool didReachBottom = RenderScanlineIntoBuffer(args.memory, args.scanline, m_screenBuffer, BorderAttr, m_isFlashing, ref m_didPixelsChange);

            if (!didReachBottom) return;

            if (m_flashFrameCount++ == FramesPerFlash)
            {
                m_isFlashing = !m_isFlashing;
                m_flashFrameCount = 0;
                m_lastFlashTime = DateTime.Now;
            }

            //if (m_didPixelsChange) UpdateScreen();

            m_didPixelsChange = false;
        }

        private static bool RenderScanlineIntoBuffer(Memory memory, int scanlineIndex, byte[][] screenBuffer, byte borderAttr, bool isFlashing, ref bool didPixelsChange)
        {
            int y = scanlineIndex - (48 - TopMargin);
            if (y < 0 || y >= screenBuffer.Length) return false;

            byte border = GetColorIndices(borderAttr).Item1;
            if (screenBuffer[y][0] != border)
            {
                didPixelsChange = true;
                for (int i = 0; i < screenBuffer[y].Length; i++) screenBuffer[y][i] = border;
            }

            y -= TopMargin;
            if (y < 0 || y >= WritableHeight) return false;

            // drawing a pixels
            for (int b = 0; b < 32; b++)
            {
                var scrPtr = GetAddress(y, b);
                var attrPtr = (ushort)(ColorMapBase + (y / 8) * 32 + b);
                var attr = memory.Peek(attrPtr);
                var (pen, paper) = GetColorIndices(attr, isFlashing && (attr & 0x80) != 0);

                var pixels = memory.Peek(scrPtr);
                for (int bit = 0; bit < 8; bit++)
                {
                    bool isSet = (pixels & (0x80 >> bit)) != 0;
                    screenBuffer[y + TopMargin][LeftMargin + b * 8 + bit] = isSet ? pen : paper;
                }
            }

            return scanlineIndex == 240 + 48; // Approximate end of frame cutoff
        }

        private static ushort GetAddress(int y, int b)
        {
            var row = (ushort)(y & 0x07);
            var sector = (ushort)((y & 0xC0) >> 6);
            var line = (ushort)((y & 0x38) >> 3);
            return (ushort)(ScreenBase | (sector << 11) | (row << 8) | (line << 5) | b);
        }

        public void Dispose()
        {
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
        }
    }
}