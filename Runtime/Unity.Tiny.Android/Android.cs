using System;
using System.Diagnostics;
using Unity.Entities;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Tiny.Core;
using Unity.Tiny.Core2D;

namespace Unity.Tiny.Android
{
    public class AndroidWindowSystem : WindowSystem
    {
        private static AndroidWindowSystem sWindowSystem;
        public AndroidWindowSystem()
        {
            initialized = false;
            sWindowSystem = this;
        }

        public override IntPtr GetPlatformWindowHandle()
        {
            return (IntPtr)AndroidNativeCalls.getNativeWindow();
        }

        private static MainLoopDelegate staticM;

        [MonoPInvokeCallbackAttribute]
        static bool ManagedRAFCallback()
        {
#if UNITY_DOTSPLAYER
            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.FreeTempMemory();
#endif
            return !sWindowSystem.Enabled ? true : staticM();
        }

        public delegate void OnPauseDelegate(int pause);

        private static OnPauseDelegate onPauseM;

        [MonoPInvokeCallbackAttribute]
        static void ManagedOnPauseCallback(int pause)
        {
            onPauseM(pause);
        }

        private static Action onDestroyM;

        [MonoPInvokeCallbackAttribute]
        static void ManagedOnDestroyCallback()
        {
            onDestroyM();
        }

        internal class MonoPInvokeCallbackAttribute : Attribute
        {
        }

        public override void InfiniteMainLoop(MainLoopDelegate m)
        {
            staticM = m;
            AndroidNativeCalls.set_animation_frame_callback(Marshal.GetFunctionPointerForDelegate((MainLoopDelegate)ManagedRAFCallback));
        }

        public void SetOnPauseCallback(OnPauseDelegate m)
        {
            onPauseM = m;
            AndroidNativeCalls.set_pause_callback(Marshal.GetFunctionPointerForDelegate((OnPauseDelegate)ManagedOnPauseCallback));
        }

        public void SetOnDestroyCallback(Action m)
        {
            onDestroyM = m;
            AndroidNativeCalls.set_destroy_callback(Marshal.GetFunctionPointerForDelegate((Action)ManagedOnDestroyCallback));
        }

        public override void DebugReadbackImage(out int w, out int h, out NativeArray<byte> pixels)
        {
            var env = World.GetExistingSystem<TinyEnvironment>();
            var config = env.GetConfigData<DisplayInfo>();
            pixels = new NativeArray<byte>(config.framebufferWidth*config.framebufferHeight*4, Allocator.Persistent);
            unsafe
            {
                AndroidNativeCalls.debugReadback(config.framebufferWidth, config.framebufferHeight, pixels.GetUnsafePtr());
            }

            w = config.framebufferWidth;
            h = config.framebufferHeight;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            // setup window
            Console.WriteLine("Android Window init.");

            var env = World.GetExistingSystem<TinyEnvironment>();
            var config = env.GetConfigData<DisplayInfo>();

            try
            {
                initialized = AndroidNativeCalls.init();
            } catch
            {
                Console.WriteLine("  Exception during initialization.");
                initialized = false;
            }
            if (!initialized)
            {
                Console.WriteLine("  Failed.");
                World.QuitUpdate = true;
                return;
            }
            int winw = 0, winh = 0;
            AndroidNativeCalls.getWindowSize(ref winw, ref winh);
            config.focused = true;
            config.visible = true;
            config.orientation = winw >= winh ? DisplayOrientation.Horizontal : DisplayOrientation.Vertical;
            config.frameWidth = winw;
            config.frameHeight = winh;
            int sw = 0, sh = 0;
            AndroidNativeCalls.getScreenSize(ref sw, ref sh);
            config.screenWidth = sw;
            config.screenHeight = sh;
            config.width = winw;
            config.height = winh;
            int fbw = 0, fbh = 0;
            AndroidNativeCalls.getFramebufferSize(ref fbw, ref fbh);
            config.framebufferWidth = fbw;
            config.framebufferHeight = fbh;
            config.renderMode = RenderMode.BGFX;
            env.SetConfigData(config);

            frameTime = AndroidNativeCalls.time();
        }

        protected override void OnDestroy()
        {
            // close window
            if (initialized)
            {
                Console.WriteLine("Android Window shutdown.");
                AndroidNativeCalls.shutdown(0);
                initialized = false;
            }
        }

        protected override void OnUpdate()
        {
            if (!initialized)
                return;

            var env = World.GetExistingSystem<TinyEnvironment>();
            var config = env.GetConfigData<DisplayInfo>();
            int winw = 0, winh = 0;
            AndroidNativeCalls.getWindowSize(ref winw, ref winh);
            if (winw != config.width || winh != config.height)
            {
                if (config.autoSizeToFrame)
                {
                    Console.WriteLine("Android Window update size.");
                    config.orientation = winw >= winh ? DisplayOrientation.Horizontal : DisplayOrientation.Vertical;
                    config.width = winw;
                    config.height = winh;
                    config.frameWidth = winw;
                    config.frameHeight = winh;
                    int fbw = 0, fbh = 0;
                    AndroidNativeCalls.getFramebufferSize(ref fbw, ref fbh);
                    config.framebufferWidth = fbw;
                    config.framebufferHeight = fbh;
                    config.renderMode = RenderMode.BGFX;
                    env.SetConfigData(config);
                }
                else
                {
                    AndroidNativeCalls.resize(config.width, config.height);
                }
            }
            if (!AndroidNativeCalls.messagePump())
            {
                Console.WriteLine("Android message pump exit.");
                AndroidNativeCalls.shutdown(1);
                World.QuitUpdate = true;
                initialized = false;
                return;
            }
#if DEBUG
            AndroidNativeCalls.debugClear();
#endif
            double newFrameTime = AndroidNativeCalls.time();
            var timeData = env.StepWallRealtimeFrame(newFrameTime - frameTime);
            World.SetTime(timeData);
            frameTime = newFrameTime;
        }

        private bool initialized;
        private double frameTime;
    }

    public static class AndroidNativeCalls
    {
        [DllImport("lib_unity_tiny_android", EntryPoint = "init_android")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool init();

        [DllImport("lib_unity_tiny_android", EntryPoint = "getWindowSize_android")]
        public static extern void getWindowSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_android", EntryPoint = "getScreenSize_android")]
        public static extern void getScreenSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_android", EntryPoint = "getFramebufferSize_android")]
        public static extern void getFramebufferSize(ref int w, ref int h);

        [DllImport("lib_unity_tiny_android", EntryPoint = "getWindowFrameSize_android")]
        public static extern void getWindowFrameSize(ref int left, ref int top, ref int right, ref int bottom);

        [DllImport("lib_unity_tiny_android", EntryPoint = "shutdown_android")]
        public static extern void shutdown(int exitCode);

        [DllImport("lib_unity_tiny_android", EntryPoint = "resize_android")]
        public static extern void resize(int width, int height);

        [DllImport("lib_unity_tiny_android", EntryPoint = "messagePump_android")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool messagePump();

        [DllImport("lib_unity_tiny_android", EntryPoint = "swapBuffers_android")]
        public static extern void swapBuffers();

        [DllImport("lib_unity_tiny_android", EntryPoint = "debugClear_android")]
        public static extern void debugClear();

        [DllImport("lib_unity_tiny_android", EntryPoint = "debugReadback_android")]
        public static unsafe extern void debugReadback(int w, int h, void *pixels);

        [DllImport("lib_unity_tiny_android", EntryPoint = "time_android")]
        public static extern double time();

        [DllImport("lib_unity_tiny_android", EntryPoint = "rafcallbackinit_android")]
        public static extern bool set_animation_frame_callback(IntPtr func);

        [DllImport("lib_unity_tiny_android", EntryPoint = "pausecallbacksinit_android")]
        public static extern bool set_pause_callback(IntPtr func);

        [DllImport("lib_unity_tiny_android", EntryPoint = "destroycallbacksinit_android")]
        public static extern bool set_destroy_callback(IntPtr func);

        [DllImport("lib_unity_tiny_android", EntryPoint = "get_touch_info_stream_android")]
        public static extern unsafe int * getTouchInfoStream(ref int len);

        [DllImport("lib_unity_tiny_android", EntryPoint = "get_native_window_android")]
        public static extern int getNativeWindow();

        [DllImport("lib_unity_tiny_android", EntryPoint = "reset_android_input")]
        public static extern void resetStreams();
    }

}

