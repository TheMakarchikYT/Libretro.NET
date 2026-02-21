using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Libretro.NET.Bindings;

namespace Libretro.NET
{
    public unsafe class RetroCore : IDisposable
    {
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private IntPtr _lib;
        private List<Delegate> _delegates = new List<Delegate>();

        public void Load(string path)
        {
            _lib = LoadLibrary(path);
            if (_lib == IntPtr.Zero)
                throw new Exception("Failed to load core: " + path);
        }

        private TDelegate GetFunc<TDelegate>(string name) where TDelegate : class
        {
            IntPtr ptr = GetProcAddress(_lib, name);
            if (ptr == IntPtr.Zero)
                throw new Exception("Could not find function: " + name);
            Delegate d = Marshal.GetDelegateForFunctionPointer(ptr, typeof(TDelegate));
            _delegates.Add(d);
            return d as TDelegate;
        }

        public IntPtr Register(Delegate del)
        {
            _delegates.Add(del);
            return Marshal.GetFunctionPointerForDelegate(del);
        }

        public void set_environment(retro_environment_t cb)
        {
            _delegates.Add(cb);
            GetFunc<SetEnvironmentFunc>("retro_set_environment")(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void set_video_refresh(retro_video_refresh_t cb)
        {
            _delegates.Add(cb);
            GetFunc<SetVideoRefreshFunc>("retro_set_video_refresh")(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void set_input_poll(retro_input_poll_t cb)
        {
            _delegates.Add(cb);
            GetFunc<SetInputPollFunc>("retro_set_input_poll")(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void set_input_state(retro_input_state_t cb)
        {
            _delegates.Add(cb);
            GetFunc<SetInputStateFunc>("retro_set_input_state")(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void set_audio_sample(retro_audio_sample_t cb)
        {
            _delegates.Add(cb);
            GetFunc<SetAudioSampleFunc>("retro_set_audio_sample")(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void set_audio_sample_batch(retro_audio_sample_batch_t cb)
        {
            _delegates.Add(cb);
            GetFunc<SetAudioSampleBatchFunc>("retro_set_audio_sample_batch")(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void init()
        {
            GetFunc<InitFunc>("retro_init")();
        }

        public void get_system_info(ref retro_system_info info)
        {
            fixed (retro_system_info* p = &info)
                GetFunc<GetSystemInfoFunc>("retro_get_system_info")(p);
        }

        public byte load_game(ref retro_game_info game)
        {
            fixed (retro_game_info* p = &game)
                return GetFunc<LoadGameFunc>("retro_load_game")(p);
        }

        public void get_system_av_info(ref retro_system_av_info av)
        {
            fixed (retro_system_av_info* p = &av)
                GetFunc<GetSystemAvInfoFunc>("retro_get_system_av_info")(p);
        }

        public void run()
        {
            GetFunc<RunFunc>("retro_run")();
        }

        public void deinit()
        {
            GetFunc<DeinitFunc>("retro_deinit")();
        }

        public void Dispose()
        {
            if (_lib != IntPtr.Zero)
            {
                FreeLibrary(_lib);
                _lib = IntPtr.Zero;
            }
            _delegates.Clear();
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetEnvironmentFunc(IntPtr cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetVideoRefreshFunc(IntPtr cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetInputPollFunc(IntPtr cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetInputStateFunc(IntPtr cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetAudioSampleFunc(IntPtr cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetAudioSampleBatchFunc(IntPtr cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void InitFunc();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DeinitFunc();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void GetSystemInfoFunc(retro_system_info* info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate byte LoadGameFunc(retro_game_info* game);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void GetSystemAvInfoFunc(retro_system_av_info* av);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RunFunc();
    }
}
