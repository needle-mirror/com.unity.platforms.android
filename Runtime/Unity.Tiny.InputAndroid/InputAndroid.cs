using Unity.Entities;
using Unity.Tiny.Input;

namespace Unity.Tiny.Android
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(AndroidWindowSystem))]
    public class AndroidInputSystem : InputSystem
    {
        private bool initialized = false;

        // these mirror the Android native MotionEvent constants
        const int ACTION_DOWN = 0;
        const int ACTION_UP = 1;
        const int ACTION_MOVE = 2;
        const int ACTION_CANCEL = 3;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            if (initialized)
                return;

            // do we need additional initialization here after window
            initialized = true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate(); // advances input state one frame
            unsafe
            {
                // touch
                int touchInfoStreamLen = 0;
                int* touchInfoStream = AndroidNativeCalls.getTouchInfoStream(ref touchInfoStreamLen);
                for (int i = 0; i < touchInfoStreamLen; i += 4)
                {
                    if (touchInfoStream[i + 1] == ACTION_DOWN)
                        m_inputState.TouchEvent(touchInfoStream[i], TouchState.Began, touchInfoStream[i + 2], touchInfoStream[i + 3]);
                    else if (touchInfoStream[i + 1] == ACTION_UP)
                        m_inputState.TouchEvent(touchInfoStream[i], TouchState.Ended, touchInfoStream[i + 2], touchInfoStream[i + 3]);
                    else if (touchInfoStream[i + 1] == ACTION_MOVE)
                        m_inputState.TouchEvent(touchInfoStream[i], TouchState.Moved, touchInfoStream[i + 2], touchInfoStream[i + 3]);
                    else if (touchInfoStream[i + 1] == ACTION_CANCEL)
                        m_inputState.TouchEvent(touchInfoStream[i], TouchState.Canceled, touchInfoStream[i + 2], touchInfoStream[i + 3]);
                }

                if (touchInfoStreamLen != 0)
                    m_inputState.hasTouch = true;
            }

            AndroidNativeCalls.resetStreams();
        }

    }
}
