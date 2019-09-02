package com.unity3d.tinyplayer;

import android.app.Activity;
import android.os.Bundle;
import android.os.Process;
import android.util.Log;
import android.view.MotionEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.content.res.AssetManager;

import java.io.File;

public class UnityTinyActivity extends Activity {

    UnityTinyView mView;
    AssetManager mAssetManager;

    @Override protected void onCreate(Bundle bundle) {
        super.onCreate(bundle);

        requestWindowFeature(Window.FEATURE_NO_TITLE);
        mAssetManager = getAssets();
        mView = new UnityTinyView(mAssetManager, getCacheDir().getAbsolutePath(), this);
        mView.setOnTouchListener(new View.OnTouchListener() {

            public boolean onTouch(View v, MotionEvent event) {
                int action = event.getAction();
                if (action == MotionEvent.ACTION_DOWN ||
                        action == MotionEvent.ACTION_MOVE ||
                        action == MotionEvent.ACTION_UP ||
                        action == MotionEvent.ACTION_CANCEL)
                    for (int i = 0; i < event.getPointerCount(); ++i) {
                        int index = event.getPointerId(i);
                        UnityTinyAndroidJNILib.touchevent(index, action, (int)event.getX(i), (int)event.getY(i));
                    }
                return true;
            }
        });
        setContentView(mView);
        mView.requestFocus();
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
        mView.onDestroy();
        super.onDestroy();
        Process.killProcess(Process.myPid());
    }
}
