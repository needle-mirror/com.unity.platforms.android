#if UNITY_ANDROID

using System;
using Unity.Entities;
using Unity.Collections;
using Unity.Tiny.Core;
using Unity.Tiny.Scenes;
using Unity.Tiny.Debugging;
using Unity.Tiny.Android;

namespace Unity.Tiny.EntryPoint
{
    public static class ProgramAndroid
    {
        private static AndroidWindowSystem sWindowSystem = null;

        private static void Main()
        {
            Program.CreateWorldAndRunMainLoop();

            sWindowSystem = Program.GetExistingSystem<WindowSystem>() as AndroidWindowSystem;
            if (sWindowSystem != null)
            {
                sWindowSystem.SetOnPauseCallback(OnPause);
                sWindowSystem.SetOnDestroyCallback(OnDestroy);
            }
            else
            {
                Debug.Log("No android window system found.");
            }
        }

        private static void OnPause(int pause)
        {
            if (sWindowSystem != null)
            {
                sWindowSystem.Enabled = (pause == 0);
            }
        }

        private static void OnDestroy()
        {
            Program.DestroyWorld();
        }
    }
}

#endif