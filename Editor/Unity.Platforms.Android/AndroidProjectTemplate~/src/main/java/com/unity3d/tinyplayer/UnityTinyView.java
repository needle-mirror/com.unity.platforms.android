package com.unity3d.tinyplayer;

import android.app.Activity;
import android.content.Context;
import android.content.res.AssetManager;
import android.graphics.Canvas;
import android.util.AttributeSet;
import android.util.Log;
import android.view.View;
import android.view.SurfaceView;
import android.view.SurfaceHolder;

class UnityTinyView extends SurfaceView implements SurfaceHolder.Callback
{
     private class TinySurfaceThread extends Thread
     {
        private boolean mThreadRun = false;

        public TinySurfaceThread() {}

        public void setRunning(boolean running)
        {
            mThreadRun = running;
        }

        @Override
        public void run()
        {
            while (mThreadRun)
            {
                Canvas canvas = null;
                try
                {
                    if (getContext() instanceof Activity)
                    {
                        ((Activity)getContext()).runOnUiThread(new Runnable() {
                            public void run()
                            {
                                UnityTinyAndroidJNILib.step();
                            }
                        });
                    }
                    sleep(16); // 60FPS ?
                }
                catch (InterruptedException e)
                {
                }
            }
        }
    }

    private static String TAG = "UnityTinyView";
    public static UnityTinyView sSurfaceView;
    private TinySurfaceThread mThread;
    private boolean mInitialized = false;
    private boolean mStarted = false;

    public UnityTinyView(AssetManager assetManager, String path, Context context)
    {
        super(context);
        init(assetManager, path, false, 0, 0);
    }

    public UnityTinyView(AssetManager assetManager, Context context, String path, boolean translucent, int depth, int stencil)
    {
        super(context);
        init(assetManager, path, translucent, depth, stencil);
    }

    private void init(AssetManager assetManager, String path, boolean translucent, int depth, int stencil)
    {
        getHolder().addCallback(this);
        UnityTinyAndroidJNILib.setAssetManager(assetManager);
        sSurfaceView = this;
    }

    @Override
    public void surfaceChanged(SurfaceHolder holder, int format, int width, int height)
    {
        Log.d(TAG, "surfaceChanged " + width + " x " + height);
        UnityTinyAndroidJNILib.init(holder.getSurface(), width, height);
        mInitialized = true;
    }

    @Override
    public void surfaceCreated(SurfaceHolder holder)
    {
        Log.d(TAG, "surfaceCreated");
        if (!mStarted)
        {
            mStarted = true;
            UnityTinyAndroidJNILib.start();
        }
        mThread = new TinySurfaceThread();
        mThread.setRunning(true);
        mThread.start();
    }

    @Override
    public void surfaceDestroyed(SurfaceHolder holder)
    {
        Log.d(TAG, "surfaceDestroyed");
        UnityTinyAndroidJNILib.init(null, 0, 0);
        boolean retry = true;
        mThread.setRunning(false);
        while (retry)
        {
            try
            {
                mThread.join();
                retry = false;
            }
            catch (InterruptedException e)
            {
            }
        }
    }

    public void onPause()
    {
        UnityTinyAndroidJNILib.pause(1);
    }

    public void onResume()
    {
        UnityTinyAndroidJNILib.pause(0);
    }

    public void onDestroy()
    {
        UnityTinyAndroidJNILib.destroy();
    }
}
