// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using UnityEngine;
using Speculator.Core;

public class UnityAudioBridge : MonoBehaviour
{
    private SoundHandler m_handler;
    private bool m_isReady;

    public void Init(SoundHandler handler)
    {
        m_handler = handler;
        var source = GetComponent<AudioSource>();

        // Create an empty "endless" clip to make the AudioSource work
        source.clip = AudioClip.Create("Silent", 44100, 1, 44100, false);
        source.loop = true;
        source.Play();

        m_isReady = true;
        Debug.Log("The sound bridge has been launched.");
    }


    // This method is called by Unity itself to fill the speaker's audio buffer.
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!m_isReady || m_handler == null) return;

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = m_handler.GetNextSample();
            // Recording the same sound in all channels (L/R)
            for (int c = 0; c < channels; c++)
            {
                data[i + c] = sample;
            }
        }
    }
}
