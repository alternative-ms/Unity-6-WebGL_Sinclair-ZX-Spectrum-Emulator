// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CSharp.Core.Extensions;
using CSharp.Core.ViewModels;
using Speculator.Core.Tape;

namespace Speculator.Core
{
    public class ZxPortHandler : ViewModelBase, IPortHandler, IDisposable
    {
        private readonly SoundHandler m_soundHandler;
        private readonly ZxDisplay m_theDisplay;
        private readonly TapeLoader m_tapeLoader;
        private readonly List<KeyCode> m_realKeysPressed = new List<KeyCode>();
        private readonly List<(KeyCode[] Pc, KeyCode[] Speccy)> m_pcToSpectrumKeyMap;
        private readonly List<(KeyCode[] Pc, KeyCode[] Speccy)> m_pcToSpectrumKeyMapWithJoystick;
        private bool m_emulateCursorJoystick;
        private bool m_handleKeyEvents = true;
        private bool? m_tapeSignal;

        public bool EmulateCursorJoystick
        {
            get => m_emulateCursorJoystick;
            set => SetField(ref m_emulateCursorJoystick, value);
        }

        public ZxPortHandler(SoundHandler soundHandler, ZxDisplay theDisplay, TapeLoader tapeLoader)
        {
            m_soundHandler = soundHandler;
            m_theDisplay = theDisplay;
            m_tapeLoader = tapeLoader;

            m_pcToSpectrumKeyMap = new List<(KeyCode[], KeyCode[])>();
            m_pcToSpectrumKeyMap.Add((K(KeyCode.Backspace), K(KeyCode.LeftShift, KeyCode.Alpha0)));
            m_pcToSpectrumKeyMap.Add((K(KeyCode.Comma), K(KeyCode.RightShift, KeyCode.N)));
            m_pcToSpectrumKeyMap.Add((K(KeyCode.Period), K(KeyCode.RightShift, KeyCode.M)));
            m_pcToSpectrumKeyMap.Add((K(KeyCode.Equals), K(KeyCode.RightShift, KeyCode.L)));
            m_pcToSpectrumKeyMap.Add((K(KeyCode.Minus), K(KeyCode.RightShift, KeyCode.J)));
            m_pcToSpectrumKeyMap.Add((K(KeyCode.Slash), K(KeyCode.RightShift, KeyCode.V)));
            m_pcToSpectrumKeyMap.Add((K(KeyCode.Quote), K(KeyCode.RightShift, KeyCode.P))); // KeyCode.Alpha7))); // change to allow type " via Right-Shift + P
            m_pcToSpectrumKeyMap.Add((K(KeyCode.Semicolon), K(KeyCode.RightShift, KeyCode.O)));
            m_pcToSpectrumKeyMap.Add((K(KeyCode.LeftAlt), K(KeyCode.LeftShift, KeyCode.RightShift)));

            m_pcToSpectrumKeyMapWithJoystick = m_pcToSpectrumKeyMap.ToList();
            m_pcToSpectrumKeyMapWithJoystick.Add((K(KeyCode.UpArrow), K(KeyCode.Alpha7)));
            m_pcToSpectrumKeyMapWithJoystick.Add((K(KeyCode.DownArrow), K(KeyCode.Alpha6)));
            m_pcToSpectrumKeyMapWithJoystick.Add((K(KeyCode.LeftArrow), K(KeyCode.Alpha5)));
            m_pcToSpectrumKeyMapWithJoystick.Add((K(KeyCode.RightArrow), K(KeyCode.Alpha8)));
            m_pcToSpectrumKeyMapWithJoystick.Add((K(KeyCode.BackQuote), K(KeyCode.Alpha0)));
            m_pcToSpectrumKeyMapWithJoystick.Add((K(KeyCode.Backslash), K(KeyCode.Alpha0)));

            // key mapping for H.A.T.E. (PC Arrow Keys -> Game Keys)
            m_pcToSpectrumKeyMap.Add((K(KeyCode.UpArrow), K(KeyCode.Q)));    // Up
            m_pcToSpectrumKeyMap.Add((K(KeyCode.DownArrow), K(KeyCode.A)));  // Down
            m_pcToSpectrumKeyMap.Add((K(KeyCode.LeftArrow), K(KeyCode.K)));  // Left
            m_pcToSpectrumKeyMap.Add((K(KeyCode.RightArrow), K(KeyCode.L))); // Right

            // We leave the 'Fire' button on the 'Spacebar' (it's already 'Spacebar' in the emulator)
        }

        private static KeyCode[] K(params KeyCode[] keyCodes) => keyCodes;

        // This method will call from the Update method of main-run script
        public void Update()
        {
            if (!m_handleKeyEvents) return;
            lock (m_realKeysPressed)
            {
                m_realKeysPressed.Clear();
                foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                {
                    if (k != KeyCode.None && k < (KeyCode)500 && Input.GetKey(k)) m_realKeysPressed.Add(k);
                }
            }
        }

        public byte In(ushort portAddress)
        {
            var result = (byte)0xFF;
            if ((portAddress & 0x00FF) == 0xFE)
            {
                result = ReadKeyboardPort(portAddress);
                m_tapeSignal = m_tapeLoader.GetTapeSignal();

                // for debug only
                //if (m_tapeSignal.HasValue) UnityEngine.Debug.Log($"Signal from the tape: {m_tapeSignal.Value}");

                result = m_tapeSignal == true ? result.SetBit(6) : result.ResetBit(6);
            }
            else if ((portAddress & 0x001F) == 0x1F)
            {
                result = ReadJoystickPort();
            }
            return result;
        }

        public void Out(byte port, byte b)
        {
            // We only care about writing to port 0xFE (254)
            if (port != 0xFE) return;

            // SOUND: Get speaker state (bits 3 and 4)
            // In the original it is (b & 0x18) >> 3, which gives values ​​0-3
            var speakerState = (byte)((b & 0x18) >> 3);

            if (m_tapeSignal.HasValue)
            {
                // If loading from tape, mix the tape recorder signal into the sound
                // this is what creates that legendary loading "squeal"
                speakerState = (byte)((speakerState & 0xfe) + (m_tapeSignal.Value ? 1 : 0));
            }

            // We pass the sound level (0-3) to our SoundHandler
            m_soundHandler?.SetSpeakerState(speakerState);

            // BORDER: The lower 3 bits specify the border color (0-7)
            if (m_theDisplay != null) m_theDisplay.BorderAttr = (byte)(b & 0x07);
        }



        private byte ReadJoystickPort()
        {
            lock (m_realKeysPressed)
            {
                byte b = 0x00;
                if (IsZxKeyPressed(KeyCode.BackQuote) || IsZxKeyPressed(KeyCode.Backslash)) b |= 0x10;
                if (IsZxKeyPressed(KeyCode.UpArrow)) b |= 0x8;
                if (IsZxKeyPressed(KeyCode.DownArrow)) b |= 0x4;
                if (IsZxKeyPressed(KeyCode.LeftArrow)) b |= 0x2;
                if (IsZxKeyPressed(KeyCode.RightArrow)) b |= 0x1;
                return b;
            }
        }

        private byte ReadKeyboardPort(ushort portAddress)
        {
            lock (m_realKeysPressed)
            {
                byte result = 0;
                var hi = (byte)(portAddress >> 8);

                // Row: CAPS SHIFT, Z, X, C, V
                if ((hi & 0x01) == 0)
                {
                    if (IsZxKeyPressed(KeyCode.LeftShift)) result |= 1 << 0;
                    if (IsZxKeyPressed(KeyCode.Z)) result |= 1 << 1;
                    if (IsZxKeyPressed(KeyCode.X)) result |= 1 << 2;
                    if (IsZxKeyPressed(KeyCode.C)) result |= 1 << 3;
                    if (IsZxKeyPressed(KeyCode.V)) result |= 1 << 4;
                }

                // Row: A, S, D, F, G
                if ((hi & 0x02) == 0)
                {
                    if (IsZxKeyPressed(KeyCode.A)) result |= 1 << 0;
                    if (IsZxKeyPressed(KeyCode.S)) result |= 1 << 1;
                    if (IsZxKeyPressed(KeyCode.D)) result |= 1 << 2;
                    if (IsZxKeyPressed(KeyCode.F)) result |= 1 << 3;
                    if (IsZxKeyPressed(KeyCode.G)) result |= 1 << 4;
                }

                // Row: Q, W, E, R, T
                if ((hi & 0x04) == 0)
                {
                    if (IsZxKeyPressed(KeyCode.Q)) result |= 1 << 0;
                    if (IsZxKeyPressed(KeyCode.W)) result |= 1 << 1;
                    if (IsZxKeyPressed(KeyCode.E)) result |= 1 << 2;
                    if (IsZxKeyPressed(KeyCode.R)) result |= 1 << 3;
                    if (IsZxKeyPressed(KeyCode.T)) result |= 1 << 4;
                }

                // Row: 1, 2, 3, 4, 5
                if ((hi & 0x08) == 0)
                {
                    if (IsZxKeyPressed(KeyCode.Alpha1)) result |= 1 << 0;
                    if (IsZxKeyPressed(KeyCode.Alpha2)) result |= 1 << 1;
                    if (IsZxKeyPressed(KeyCode.Alpha3)) result |= 1 << 2;
                    if (IsZxKeyPressed(KeyCode.Alpha4)) result |= 1 << 3;
                    if (IsZxKeyPressed(KeyCode.Alpha5)) result |= 1 << 4;
                }

                // Row: 0, 9, 8, 7, 6
                if ((hi & 0x10) == 0)
                {
                    if (IsZxKeyPressed(KeyCode.Alpha0)) result |= 1 << 0;
                    if (IsZxKeyPressed(KeyCode.Alpha9)) result |= 1 << 1;
                    if (IsZxKeyPressed(KeyCode.Alpha8)) result |= 1 << 2;
                    if (IsZxKeyPressed(KeyCode.Alpha7)) result |= 1 << 3;
                    if (IsZxKeyPressed(KeyCode.Alpha6)) result |= 1 << 4;
                }

                // Row: P, O, I, U, Y
                if ((hi & 0x20) == 0)
                {
                    if (IsZxKeyPressed(KeyCode.P)) result |= 1 << 0;
                    if (IsZxKeyPressed(KeyCode.O)) result |= 1 << 1;
                    if (IsZxKeyPressed(KeyCode.I)) result |= 1 << 2;
                    if (IsZxKeyPressed(KeyCode.U)) result |= 1 << 3;
                    if (IsZxKeyPressed(KeyCode.Y)) result |= 1 << 4;
                }

                // Row: ENTER, L, K, J, H
                if ((hi & 0x40) == 0)
                {
                    if (IsZxKeyPressed(KeyCode.Return)) result |= 1 << 0;
                    if (IsZxKeyPressed(KeyCode.L)) result |= 1 << 1;
                    if (IsZxKeyPressed(KeyCode.K)) result |= 1 << 2;
                    if (IsZxKeyPressed(KeyCode.J)) result |= 1 << 3;
                    if (IsZxKeyPressed(KeyCode.H)) result |= 1 << 4;
                }

                // Row: SPACE, SYMBOL SHIFT, M, N, B
                if ((hi & 0x80) == 0)
                {
                    if (IsZxKeyPressed(KeyCode.Space)) result |= 1 << 0;
                    if (IsZxKeyPressed(KeyCode.RightShift)) result |= 1 << 1;
                    if (IsZxKeyPressed(KeyCode.M)) result |= 1 << 2;
                    if (IsZxKeyPressed(KeyCode.N)) result |= 1 << 3;
                    if (IsZxKeyPressed(KeyCode.B)) result |= 1 << 4;
                }

                return (byte)~result;
            }
        }

        // legacy
        //private bool IsZxKeyPressed(KeyCode key)
        //{
        //    lock (m_realKeysPressed) return m_realKeysPressed.Contains(key);
        //}

        private bool IsZxKeyPressed(KeyCode speccyKey)
        {
            lock (m_realKeysPressed)
            {
                // 1. Check whether the key itself is pressed (for example 'K')
                if (m_realKeysPressed.Contains(speccyKey)) return true;

                // 2. Check the mapping (for example, whether the Left Arrow key, which replaces 'K', is pressed)
                foreach (var mapping in m_pcToSpectrumKeyMap)
                {
                    // If the mapping specifies the Spectrum button we need
                    if (mapping.Speccy.Contains(speccyKey))
                    {
                        // and if the physical PC key (Arrow) is currently pressed
                        if (mapping.Pc.All(pcKey => m_realKeysPressed.Contains(pcKey))) return true;
                    }
                }
                return false;
            }
        }

        public void Dispose() { }
    }
}