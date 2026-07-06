// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using UnityEngine;
using UnityEngine.UI;
using Speculator.Core;
using System.IO;

public class SpectrumRunner : MonoBehaviour
{
    private ZxSpectrum m_spectrum;
    [SerializeField]
    private RawImage displayTarget;
    private UnityAudioBridge audioBridge;

    void Start()
    {
        var display = new ZxDisplay();
        m_spectrum = new ZxSpectrum(display);

        if (displayTarget != null) displayTarget.texture = display.Texture;

        // Loading ROM directly into memory
        string romPath = Path.Combine(Application.streamingAssetsPath, "48k_rom.bytes");
        if (File.Exists(romPath))
        {
            // Accessing the processor memory directly
            m_spectrum.TheCpu.MainMemory.LoadRom(new FileInfo(romPath));

            // Turn on the emulator
            m_spectrum.PowerOnAsync();
            Debug.Log("The emulator is running!");
        }
        else
        {
            Debug.LogError("Put 48k_rom.bytes file in Assets/StreamingAssets/ folder");
        }

        audioBridge = gameObject.GetComponent<UnityAudioBridge>();
        audioBridge.Init(m_spectrum.SoundHandler);

        m_spectrum.TheCpu.LoadRequested += (s, e) =>
        {
            // This will work when the ROM calls the LOAD routine.
            Debug.Log("Flash Load: Direct insertion of a block into memory...");

            // Trying to read the current tape block directly into CPU memory
            // (when TapeLoader has an instant download method)
        };

    }


    void Update()
    {
        if (m_spectrum == null) return;

        if (m_spectrum.TheDisplay.EmulationSpeed > 1.0)
        {
            m_spectrum.TheCpu.SetSpeed(ClockSync.Speed.Actual);
            Debug.Log("Speed ​​has been reduced to 100% to ensure proper tape loading.");
        }

        // Querying buttons/inputs
        m_spectrum.PortHandler.Update();

        // Forced transfer of data from emulator memory to Unity texture.
        // Doing this here because Update runs in the Main Thread.
        m_spectrum.TheDisplay.UpdateScreen();
    }


    private void OnDestroy()
    {
        m_spectrum?.Dispose();
    }

    public void LoadZ80Game(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        m_spectrum.TheCpu.ClockSync?.Reset(); // Resetting time synchronization
        m_spectrum.PortHandler.Update(); // Forcefully 'release' all buttons in the emulator

        var fileIo = new ZxFileIo(m_spectrum.TheCpu, m_spectrum.TheDisplay, m_spectrum.TheTapeLoader);
        fileIo.LoadFile(new FileInfo(path));

        Debug.Log("The game is loaded : " + fileName);
    }

    public void LoadTapGame(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(path))
        {
            // Resetting Spectrum (recommended for a clean boot)
            m_spectrum.TheCpu.TheRegisters.PC = 0x0000;

            // Loading the tape in TapeLoader
            m_spectrum.TheTapeLoader.Load(new FileInfo(path));

            Debug.Log($"Tape {fileName} loaded. Enter LOAD \"\" and press Enter!");
        }
    }

}
