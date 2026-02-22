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
        public string SystemDirectory { get; set; } = ".";
        public string SaveDirectory { get; set; } = ".";

        public delegate void OnFrameDelegate(byte[] frame, uint width, uint height);
        public OnFrameDelegate OnFrame;

        public delegate void OnSampleDelegate(byte[] sample);
        public OnSampleDelegate OnSample;

        public delegate bool OnCheckInputDelegate(uint port, uint device, uint index, uint id);
        public OnCheckInputDelegate OnCheckInput;

        public delegate void OnLogDelegate(string level, string message);
        public OnLogDelegate OnLog;

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
            IntPtr systemPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(retro_system_info)));
            try
            {
                _core.get_system_info(ref *(retro_system_info*)systemPtr);
            }
            catch
            {
                Marshal.FreeHGlobal(systemPtr);
                throw;
            }
            bool needFullPath = ((retro_system_info*)systemPtr)->need_fullpath;
            Marshal.FreeHGlobal(systemPtr);

            IntPtr pathPtr = Marshal.StringToHGlobalAnsi(gamePath);
            IntPtr dataPtr = IntPtr.Zero;
            long fileSize = new FileInfo(gamePath).Length;

            if (!needFullPath)
            {
                dataPtr = Marshal.AllocHGlobal((int)fileSize);
                byte[] bytes = File.ReadAllBytes(gamePath);
                Marshal.Copy(bytes, 0, dataPtr, (int)fileSize);
            }

            IntPtr gamePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(retro_game_info)));
            retro_game_info* g = (retro_game_info*)gamePtr;
            g->path = (sbyte*)pathPtr;
            g->data = (void*)dataPtr;
            g->size = (UIntPtr)fileSize;
            g->meta = null;

            byte result = _core.load_game(ref *g);

            Marshal.FreeHGlobal(gamePtr);
            Marshal.FreeHGlobal(pathPtr);
            if (dataPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(dataPtr);

            IntPtr avPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(retro_system_av_info)));
            _core.get_system_av_info(ref *(retro_system_av_info*)avPtr);
            retro_system_av_info* av = (retro_system_av_info*)avPtr;
            Width = av->geometry.base_width;
            Height = av->geometry.base_height;
            FPS = av->timing.fps;
            SampleRate = av->timing.sample_rate;
            Marshal.FreeHGlobal(avPtr);

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
                        *cb = (char*)Marshal.StringToHGlobalAnsi(SystemDirectory ?? ".");
                        return true;
                    }
                case RetroBindings.RETRO_ENVIRONMENT_GET_SAVE_DIRECTORY:
                    {
                        char** cb = (char**)data;
                        *cb = (char*)Marshal.StringToHGlobalAnsi(SaveDirectory ?? SystemDirectory ?? ".");
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

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int _vsnprintf(sbyte* buffer, UIntPtr count, sbyte* format, IntPtr args);

        private void Log(retro_log_level level, sbyte* fmt)
        {
            if (OnLog == null) return;
            string levelStr = level == retro_log_level.RETRO_LOG_ERROR ? "ERROR" :
                              level == retro_log_level.RETRO_LOG_WARN ? "WARN" :
                              level == retro_log_level.RETRO_LOG_INFO ? "INFO" : "DEBUG";
            string raw = Marshal.PtrToStringAnsi((IntPtr)fmt) ?? "";
            string msg = raw.Contains("%") ? raw.Replace("%s", "?").Replace("%d", "?").Replace("%i", "?").Replace("%u", "?").Replace("%f", "?").Replace("%x", "?").TrimEnd('\n', '\r') : raw.TrimEnd('\n', '\r');
            OnLog(levelStr, msg);
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
