package com.unity3d.tinyplayer;

import android.app.Activity;
import android.app.AlertDialog;
import android.os.Bundle;
import android.os.Process;
import android.util.Log;
import android.view.MotionEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.view.Surface;
import android.view.KeyEvent;
import android.view.OrientationEventListener;
import android.hardware.SensorManager;
import android.content.Context;
import android.content.res.AssetManager;
import android.content.res.Configuration;
import android.content.pm.ActivityInfo;
import android.content.DialogInterface;
import java.io.File;
import java.util.concurrent.Semaphore;

public class UnityTinyActivity extends Activity {

    static UnityTinyActivity sActivity;

    UnityTinyView mView;
    AssetManager mAssetManager;
    OrientationEventListener mOrientationListener;
    WindowManager mWindowManager;

    private static String TAG = "UnityTinyActivity";

    @Override protected void onCreate(Bundle bundle) {
        super.onCreate(bundle);

        sActivity = this;
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        mAssetManager = getAssets();
        mView = new UnityTinyView(mAssetManager, getCacheDir().getAbsolutePath(), this);
        mView.setOnTouchListener(new View.OnTouchListener() {

            public boolean onTouch(View v, MotionEvent event) {
                int action = event.getActionMasked();
                switch (action) {
                    case MotionEvent.ACTION_DOWN:
                    case MotionEvent.ACTION_POINTER_DOWN:
                    case MotionEvent.ACTION_UP:
                    case MotionEvent.ACTION_POINTER_UP:
                    case MotionEvent.ACTION_CANCEL: {
                        int index = event.getActionIndex();
                        if (action == MotionEvent.ACTION_POINTER_DOWN) action = MotionEvent.ACTION_DOWN;
                        else if (action == MotionEvent.ACTION_POINTER_UP) action = MotionEvent.ACTION_UP;
                        UnityTinyAndroidJNILib.touchevent(event.getPointerId(index), action, (int)event.getX(index), (int)event.getY(index));
                    }
                        break;
                    case MotionEvent.ACTION_MOVE: {
                        for (int i = 0; i < event.getPointerCount(); ++i) {
                            int pointerId = event.getPointerId(i);
                            UnityTinyAndroidJNILib.touchevent(pointerId, action, (int)event.getX(i), (int)event.getY(i));
                        }
                    }
                        break;
                }
                return true;
            }
        });
        setContentView(mView);
        mView.requestFocus();

        mOrientationListener = new OrientationEventListener(this, SensorManager.SENSOR_DELAY_NORMAL) {

            @Override
            public void onOrientationChanged(int angle)
            {
                processOrientationChange(angle);
            }
        };

        if (mOrientationListener.canDetectOrientation())
        {
            mOrientationListener.enable();
        }
        else
        {
            Log.v(TAG, "Cannot detect orientation");
            mOrientationListener.disable();
        }

        mWindowManager = (WindowManager)getSystemService(Context.WINDOW_SERVICE);
        Configuration config = getResources().getConfiguration();
        mNaturalOrientation = getNaturalOrientation(config.orientation);
        Log.d(TAG, "Natural device orientation: " + (mNaturalOrientation == ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE ? "Landscape" : "Portrait"));
        mDeviceOrientation = getActualOrientation(config.orientation);
        UnityTinyAndroidJNILib.screenOrientationChanged(mDeviceOrientation);
        UnityTinyAndroidJNILib.deviceOrientationChanged(mDeviceOrientation);
    }

    @Override protected void onPause() {
        mView.onPause();
        super.onPause();
    }

    @Override protected void onResume() {
        super.onResume();
        mView.onResume();
    }

    @Override protected void onDestroy() {
        mOrientationListener.disable();
        mView.onDestroy();
        super.onDestroy();
        Process.killProcess(Process.myPid());
    }

    @Override public boolean onKeyUp(int keyCode, KeyEvent event)
    {
        UnityTinyAndroidJNILib.keyevent(event.getKeyCode(), event.getScanCode(), event.getAction(), event.getMetaState());
        // volume up/down keys need to be processed by system
        return event.getKeyCode() != KeyEvent.KEYCODE_VOLUME_DOWN &&
               event.getKeyCode() != KeyEvent.KEYCODE_VOLUME_UP;
    }

    @Override public boolean onKeyDown(int keyCode, KeyEvent event)
    {
        UnityTinyAndroidJNILib.keyevent(event.getKeyCode(), event.getScanCode(), event.getAction(), event.getMetaState());
        // volume up/down keys need to be processed by system
        return event.getKeyCode() != KeyEvent.KEYCODE_VOLUME_DOWN &&
               event.getKeyCode() != KeyEvent.KEYCODE_VOLUME_UP;
    }

    private final int k_AngleThreshold = 25;
    private int mDeviceOrientation;
    private int mNaturalOrientation;
    private void processOrientationChange(int angle)
    {
        if (angle == -1)
        {
            // angle unknown, do nothing
            return;
        }

        int deviceOrientation = mDeviceOrientation;
        if (mNaturalOrientation == ActivityInfo.SCREEN_ORIENTATION_PORTRAIT)
        {
            if (angle < k_AngleThreshold || angle > 360 - k_AngleThreshold)
            {
                deviceOrientation = ActivityInfo.SCREEN_ORIENTATION_PORTRAIT;
            }
            else if (angle > 90 - k_AngleThreshold && angle < 90 + k_AngleThreshold)
            {
                deviceOrientation = ActivityInfo.SCREEN_ORIENTATION_REVERSE_LANDSCAPE;
            }
            else if (angle > 180 - k_AngleThreshold && angle < 180 + k_AngleThreshold)
            {
                deviceOrientation = ActivityInfo.SCREEN_ORIENTATION_REVERSE_PORTRAIT;
            }
            else if (angle > 270 - k_AngleThreshold && angle < 270 + k_AngleThreshold)
            {
                deviceOrientation = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;
            }
        }
        else // ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE
        {
            if (angle < k_AngleThreshold || angle > 360 - k_AngleThreshold)
            {
                deviceOrientation = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;
            }
            else if (angle > 90 - k_AngleThreshold && angle < 90 + k_AngleThreshold)
            {
                deviceOrientation = ActivityInfo.SCREEN_ORIENTATION_PORTRAIT;
            }
            else if (angle > 180 - k_AngleThreshold && angle < 180 + k_AngleThreshold)
            {
                deviceOrientation = ActivityInfo.SCREEN_ORIENTATION_REVERSE_LANDSCAPE;
            }
            else if (angle > 270 - k_AngleThreshold && angle < 270 + k_AngleThreshold)
            {
                deviceOrientation = ActivityInfo.SCREEN_ORIENTATION_REVERSE_PORTRAIT;
            }
        }
        if (deviceOrientation != mDeviceOrientation)
        {
            Log.d(TAG, "deviceOrientationChanged " + deviceOrientation);
            UnityTinyAndroidJNILib.deviceOrientationChanged(deviceOrientation);
            mDeviceOrientation = deviceOrientation;
        }
    }

    @Override
    public void onConfigurationChanged(Configuration newConfig)
    {
        super.onConfigurationChanged(newConfig);
        int newOrientation = getActualOrientation(newConfig.orientation);
        Log.d(TAG, "screenOrientationChanged " + newOrientation);
        UnityTinyAndroidJNILib.screenOrientationChanged(newOrientation);
    }

    public static void changeOrientation(int orientation)
    {
        sActivity.setRequestedOrientation(orientation);
    }

    public static int getNaturalOrientation()
    {
        return sActivity.mNaturalOrientation;
    }

    private int getNaturalOrientation(int orientation)
    {
        int angle = mWindowManager.getDefaultDisplay().getRotation();
        if (((angle == Surface.ROTATION_0 || angle == Surface.ROTATION_180) && orientation == Configuration.ORIENTATION_LANDSCAPE) ||
            ((angle == Surface.ROTATION_90 || angle == Surface.ROTATION_270) && orientation == Configuration.ORIENTATION_PORTRAIT))
        {
            return ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;
        }
        else
        {
            return ActivityInfo.SCREEN_ORIENTATION_PORTRAIT;
        }
    }

    private int getActualOrientation(int orientation)
    {
        int angle = mWindowManager.getDefaultDisplay().getRotation();
        if (mNaturalOrientation == ActivityInfo.SCREEN_ORIENTATION_PORTRAIT)
        {
            if (orientation == Configuration.ORIENTATION_LANDSCAPE)
            {
                if (angle == Surface.ROTATION_270)
                    return ActivityInfo.SCREEN_ORIENTATION_REVERSE_LANDSCAPE;
                else
                    return ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;
            }
            if (orientation == Configuration.ORIENTATION_PORTRAIT)
            {
                if (angle == Surface.ROTATION_180)
                    return ActivityInfo.SCREEN_ORIENTATION_REVERSE_PORTRAIT;
                else
                    return ActivityInfo.SCREEN_ORIENTATION_PORTRAIT;
            }
        }
        else // ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE
        {
            if (orientation == Configuration.ORIENTATION_LANDSCAPE)
            {
                if (angle == Surface.ROTATION_180)
                    return ActivityInfo.SCREEN_ORIENTATION_REVERSE_LANDSCAPE;
                else
                    return ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;
            }
            if (orientation == Configuration.ORIENTATION_PORTRAIT)
            {
                if (angle == Surface.ROTATION_90)
                    return ActivityInfo.SCREEN_ORIENTATION_REVERSE_PORTRAIT;
                else
                    return ActivityInfo.SCREEN_ORIENTATION_PORTRAIT;
            }
        }
        // unknown
        return mNaturalOrientation;
    }

    private AlertDialog debugDialog;
    private final Semaphore dialogSemaphore = new Semaphore(0, true);
    private void debugDialog(String message)
    {
        debugDialog = null;
        Runnable debugDialogProcess = new Runnable()
        {
            public void run()
            {
                debugDialog = new AlertDialog.Builder(sActivity).create();
                debugDialog.setTitle("Debug");
                debugDialog.setMessage(message);
                debugDialog.setButton(DialogInterface.BUTTON_NEUTRAL, "OK",
                    new DialogInterface.OnClickListener()
                    {
                        @Override
                        public void onClick(DialogInterface dialog, int which)
                        {
                            dialogSemaphore.release();
                        }
                    });
                debugDialog.setCancelable(false);
                debugDialog.show();
            }
        };

        runOnUiThread(debugDialogProcess);

        try
        {
            dialogSemaphore.acquire();
        }
        catch (InterruptedException e)
        {
            if (debugDialog != null)
            {
                debugDialog.dismiss();
            }
        }
    }

    public static void showDebugDialog(String message)
    {
        sActivity.debugDialog(message);
    }
}
