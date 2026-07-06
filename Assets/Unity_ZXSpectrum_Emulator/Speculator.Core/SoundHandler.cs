// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System;
using System.Collections.Generic; // need just for Queue
using CSharp.Core.ViewModels;

namespace Speculator.Core
{
    public class SoundHandler : ViewModelBase, IDisposable
    {
        private int m_logCounter = 0;

        private byte m_soundLevel;
        private const int SampleHz = 44100; //11025;
        private const double TicksPerSample = TicksPerSecond / SampleHz; // CPU.TStatesPerSecond / SampleHz;
        private double m_ticksUntilSample = TicksPerSample;
        private readonly int[] m_soundLevels = new int[4];
        private const double TicksPerSecond = 3500000.0;
        private float m_dcFilter = 0f;

        // Queue for transfer sound to Unity
        private readonly Queue<float> m_audioQueue = new Queue<float>();

        public void SetSpeakerState(byte soundLevel) => m_soundLevel = soundLevel;

        public void SampleSpeakerState(long tStateCount)
        {
            m_soundLevels[m_soundLevel % 4]++;
            m_ticksUntilSample -= tStateCount;

            if (m_ticksUntilSample > 0) return;

            while (m_ticksUntilSample <= 0) // Cycle for precise frequency matching
            {
                m_ticksUntilSample += TicksPerSample;

                double sampleValue = 0;
                double sampleCount = 0;
                for (int i = 0; i < m_soundLevels.Length; i++)
                {
                    sampleValue += i * m_soundLevels[i];
                    sampleCount += m_soundLevels[i];
                    // We do NOT reset it here, we will reset it after the cycle
                }

                float raw = sampleCount > 0 ? (float)(sampleValue / sampleCount) : 0f;
                float current = (raw * 0.5f) - 0.25f;

                // DC FILTER (Removes hum by 100%)
                m_dcFilter = current - (current - m_dcFilter) * 0.995f;
                float finalSample = current - m_dcFilter;

                lock (m_audioQueue)
                {
                    m_audioQueue.Enqueue(finalSample * 2.0f); // Gain
                    if (m_audioQueue.Count > 8000) m_audioQueue.Dequeue();
                }
            }

            // We reset the level counters for the next batch of clocks
            Array.Clear(m_soundLevels, 0, m_soundLevels.Length);
        }

        // Method for Unity to capture sound
        public float GetNextSample()
        {
            lock (m_audioQueue)
            {
                if (m_audioQueue.Count > 0)
                {
                    float s = m_audioQueue.Dequeue();

                    // We log only if the sound "squeaks" and only every 500th sample
                    if (s != 0 && ++m_logCounter > 500)
                    {
                        //UnityEngine.Debug.Log($"Sound sample: {s}");
                        m_logCounter = 0;
                    }
                    return s;
                }
                return 0f;
            }
        }

        public void Start() { }
        public void SetEnabled(bool value) { }
        public void Dispose() { }
    }
}