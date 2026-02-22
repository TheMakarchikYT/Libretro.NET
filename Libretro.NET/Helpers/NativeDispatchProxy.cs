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

        private SetEnvironmentFunc _setEnvironment;
        private SetVideoRefreshFunc _setVideoRefresh;
        private SetInputPollFunc _setInputPoll;
        private SetInputStateFunc _setInputState;
        private SetAudioSampleFunc _setAudioSample;
        private SetAudioSampleBatchFunc _setAudioSampleBatch;
        private InitFunc _init;
        private DeinitFunc _deinit;
        private GetSystemInfoFunc _getSystemInfo;
        private LoadGameFunc _loadGame;
        private GetSystemAvInfoFunc _getSystemAvInfo;
        private RunFunc _run;

        public void Load(string path)
        {
            _lib = LoadLibrary(path);
            if (_lib == IntPtr.Zero)
                throw new Exception("Failed to load core: " + path);

            _setEnvironment = GetFunc<SetEnvironmentFunc>("retro_set_environment");
            _setVideoRefresh = GetFunc<SetVideoRefreshFunc>("retro_set_video_refresh");
            _setInputPoll = GetFunc<SetInputPollFunc>("retro_set_input_poll");
            _setInputState = GetFunc<SetInputStateFunc>("retro_set_input_state");
            _setAudioSample = GetFunc<SetAudioSampleFunc>("retro_set_audio_sample");
            _setAudioSampleBatch = GetFunc<SetAudioSampleBatchFunc>("retro_set_audio_sample_batch");
            _init = GetFunc<InitFunc>("retro_init");
            _deinit = GetFunc<DeinitFunc>("retro_deinit");
            _getSystemInfo = GetFunc<GetSystemInfoFunc>("retro_get_system_info");
            _loadGame = GetFunc<LoadGameFunc>("retro_load_game");
            _getSystemAvInfo = GetFunc<GetSystemAvInfoFunc>("retro_get_system_av_info");
            _run = GetFunc<RunFunc>("retro_run");
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
            EnvironmentBridge bridge = (uint cmd, IntPtr data) => cb(cmd, (void*)data) ? 1 : 0;
            _delegates.Add(bridge);
            _setEnvironment(Marshal.GetFunctionPointerForDelegate(bridge));
        }

        public void set_video_refresh(retro_video_refresh_t cb)
        {
            VideoRefreshBridge bridge = (IntPtr data, uint width, uint height, UIntPtr pitch) => cb((void*)data, width, height, pitch);
            _delegates.Add(bridge);
            _setVideoRefresh(Marshal.GetFunctionPointerForDelegate(bridge));
        }

        public void set_input_poll(retro_input_poll_t cb)
        {
            _delegates.Add(cb);
            _setInputPoll(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void set_input_state(retro_input_state_t cb)
        {
            _delegates.Add(cb);
            _setInputState(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void set_audio_sample(retro_audio_sample_t cb)
        {
            _delegates.Add(cb);
            _setAudioSample(Marshal.GetFunctionPointerForDelegate(cb));
        }

        public void set_audio_sample_batch(retro_audio_sample_batch_t cb)
        {
            AudioSampleBatchBridge bridge = (IntPtr data, UIntPtr frames) => cb((short*)data, frames);
            _delegates.Add(bridge);
            _setAudioSampleBatch(Marshal.GetFunctionPointerForDelegate(bridge));
        }

        public void init()
        {
            _init();
        }

        public void get_system_info(ref retro_system_info info)
        {
            fixed (retro_system_info* p = &info)
                _getSystemInfo((IntPtr)p);
        }

        public byte load_game(ref retro_game_info game)
        {
            fixed (retro_game_info* p = &game)
                return _loadGame(p);
        }

        public void get_system_av_info(ref retro_system_av_info av)
        {
            fixed (retro_system_av_info* p = &av)
                _getSystemAvInfo((IntPtr)p);
        }

        public void run()
        {
            _run();
        }

        public void deinit()
        {
            _deinit();
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
        private delegate int EnvironmentBridge(uint cmd, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VideoRefreshBridge(IntPtr data, uint width, uint height, UIntPtr pitch);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate UIntPtr AudioSampleBatchBridge(IntPtr data, UIntPtr frames);

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
        private delegate void GetSystemInfoFunc(IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate byte LoadGameFunc(retro_game_info* game);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetSystemAvInfoFunc(IntPtr av);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RunFunc();
    }
}
