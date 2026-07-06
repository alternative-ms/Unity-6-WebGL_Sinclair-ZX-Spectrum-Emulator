// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System;
using CSharp.Core.ViewModels;
using Speculator.Core.Tape;

namespace Speculator.Core
{
    /// <summary>
    /// The main emulation entry point object.
    /// </summary>
    public class ZxSpectrum : ViewModelBase, IDisposable
    {
        private SoundHandler m_soundHandler;
        private readonly ZxFileIo m_zxFileIo;
        private ClockSync.Speed m_emulationSpeed;

        public ZxDisplay TheDisplay { get; }
        public CPU TheCpu { get; }
        public ZxPortHandler PortHandler { get; }
        public SoundHandler SoundHandler => m_soundHandler ??= new SoundHandler();
        public TapeLoader TheTapeLoader { get; } = new TapeLoader();
        //public Debugger.Debugger TheDebugger { get; }
        //public CpuHistory CpuHistory { get; }

        public ClockSync.Speed EmulationSpeed
        {
            get => m_emulationSpeed;
            set
            {
                if (!SetField(ref m_emulationSpeed, value)) return;
                TheCpu.SetSpeed(value);
                TheDisplay.IsPaused = value == ClockSync.Speed.Pause;
            }
        }

        public ZxSpectrum(ZxDisplay display)
        {
            TheDisplay = display;
            PortHandler = new ZxPortHandler(SoundHandler, TheDisplay, TheTapeLoader);
            TheCpu = new CPU(new Memory(), PortHandler, SoundHandler);
            TheTapeLoader.SetCpu(TheCpu);
            //TheDebugger = new Debugger.Debugger(TheCpu);

            TheCpu.RenderScanline += TheDisplay.OnRenderScanline;

            //TheDebugger.IsSteppingChanged += (_, _) =>
            //{
            //    if (TheDebugger.IsStepping) SoundHandler.SetEnabled(false);
            //};

            m_zxFileIo = new ZxFileIo(TheCpu, TheDisplay, TheTapeLoader);
            //CpuHistory = new CpuHistory(TheCpu, m_zxFileIo);
        }

        public void PowerOnAsync() => TheCpu.PowerOnAsync();

        //public void LoadSystemRom(FileInfo systemRom)
        //{
        //    EmulationSpeed = ClockSync.Speed.Actual;
        //    m_zxFileIo.LoadSystemRom(systemRom);
        //}

        //public void LoadRom(FileInfo romFile)
        //{
        //    EmulationSpeed = ClockSync.Speed.Actual;
        //    m_zxFileIo.LoadFile(romFile);
        //}

        //public void SaveRom(FileInfo romFile) => m_zxFileIo.SaveFile(romFile);

        public void ResetAsync()
        {
            EmulationSpeed = ClockSync.Speed.Actual;
            TheCpu.ResetAsync();
        }

        public void Dispose()
        {
            m_soundHandler?.Dispose();
            PortHandler?.Dispose();
            TheCpu?.PowerOffAsync();
        }
    }
}