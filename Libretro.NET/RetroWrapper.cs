using System;
using System.IO;
using System.Runtime.InteropServices;
using Libretro.NET.Bindings;

namespace Libretro.NET
{
    public unsafe class RetroWrapper : IDisposable
    {
        private RetroCore _core;

        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public double FPS { get; private set; }
        public double SampleRate { get; private set; }
        public retro_pixel_format PixelFormat { get; private set; }

        public delegate void OnFrameDelegate(byte[] frame, uint width, uint height);
        public OnFrameDelegate OnFrame;

        public delegate void OnSampleDelegate(byte[] sample);
        public OnSampleDelegate OnSample;

        public delegate bool OnCheckInputDelegate(uint port, uint device, uint index, uint id);
        public OnCheckInputDelegate OnCheckInput;

        public void LoadCore(string corePath)
        {
            _core = new RetroCore();
            _core.Load(corePath);

            _core.set_environment(Environment);
            _core.set_video_refresh(VideoRefresh);
            _core.set_input_poll(InputPoll);
            _core.set_input_state(InputState);
            _core.set_audio_sample(AudioSample);
            _core.set_audio_sample_batch(AudioSampleBatch);
            _core.init();
        }

        public bool LoadGame(string gamePath)
        {
            var game = new retro_game_info
            {
                path = (sbyte*)Marshal.StringToHGlobalAnsi(gamePath),
                size = (UIntPtr)new FileInfo(gamePath).Length
            };

            var system = new retro_system_info();
            _core.get_system_info(ref system);

            if (!system.need_fullpath)
            {
                game.data = (void*)Marshal.AllocHGlobal((int)game.size);
                byte[] bytes = File.ReadAllBytes(gamePath);
                Marshal.Copy(bytes, 0, (IntPtr)game.data, (int)game.size);
            }

            var result = _core.load_game(ref game);

            var av = new retro_system_av_info();
            _core.get_system_av_info(ref av);

            Width = av.geometry.base_width;
            Height = av.geometry.base_height;
            FPS = av.timing.fps;
            SampleRate = av.timing.sample_rate;

            return result == 1;
        }

        public void Run()
        {
            _core.run();
        }

        private bool Environment(uint cmd, void* data)
        {
            switch (cmd)
            {
                case RetroBindings.RETRO_ENVIRONMENT_GET_SYSTEM_DIRECTORY:
                {
                    char** cb = (char**)data;
                    *cb = (char*)Marshal.StringToHGlobalAnsi(".");
                    return true;
                }
                case RetroBindings.RETRO_ENVIRONMENT_SET_PIXEL_FORMAT:
                {
                    PixelFormat = (retro_pixel_format)(*(byte*)data);
                    return true;
                }
                case RetroBindings.RETRO_ENVIRONMENT_GET_LOG_INTERFACE:
                {
                    retro_log_callback* cb = (retro_log_callback*)data;
                    retro_log_printf_t logDel = Log;
                    cb->log = _core.Register(logDel);
                    return true;
                }
                case RetroBindings.RETRO_ENVIRONMENT_GET_CAN_DUPE:
                {
                    *(bool*)data = true;
                    return true;
                }
                case RetroBindings.RETRO_ENVIRONMENT_SET_FRAME_TIME_CALLBACK:
                {
                    retro_frame_time_callback* cb = (retro_frame_time_callback*)data;
                    retro_frame_time_callback_t timeDel = Time;
                    cb->callback = _core.Register(timeDel);
                    return true;
                }
                default:
                    return false;
            }
        }

        private void VideoRefresh(void* data, uint width, uint height, UIntPtr pitch)
        {
            byte[] raw = new byte[(uint)pitch * height];
            Marshal.Copy((IntPtr)data, raw, 0, (int)pitch * (int)height);

            byte[] result = new byte[width * 2 * height];
            int destinationIndex = 0;
            for (int sourceIndex = 0; sourceIndex < (uint)pitch * height; sourceIndex += (int)pitch)
            {
                Array.Copy(raw, sourceIndex, result, destinationIndex, width * 2);
                destinationIndex += (int)width * 2;
            }

            if (OnFrame != null)
                OnFrame(result, width, height);
        }

        private void InputPoll()
        {
        }

        private short InputState(uint port, uint device, uint index, uint id)
        {
            if (OnCheckInput != null && OnCheckInput(port, device, index, id))
                return 1;
            return 0;
        }

        private void AudioSample(short left, short right)
        {
            int count = 2;
            byte[] audio = new byte[count * 2];
            IntPtr data = Marshal.AllocHGlobal(count * 2);

            Marshal.Copy(new short[] { left, right }, 0, data, 0);
            Marshal.Copy(data, audio, 0, count * 2);
            Marshal.FreeHGlobal(data);

            if (OnSample != null)
                OnSample(audio);
        }

        private UIntPtr AudioSampleBatch(short* data, UIntPtr frames)
        {
            int count = (int)frames * 2;
            byte[] audio = new byte[count * 2];

            Marshal.Copy((IntPtr)data, audio, 0, count * 2);

            if (OnSample != null)
                OnSample(audio);

            return frames;
        }

        private void Log(retro_log_level level, sbyte* fmt)
        {
        }

        private void Time(long usec)
        {
        }

        public void Dispose()
        {
            if (_core != null)
            {
                _core.deinit();
                _core.Dispose();
                _core = null;
            }
        }
    }
}
