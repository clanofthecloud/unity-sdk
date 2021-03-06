/**
 * Copyright 2015 Google Inc. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

package com.clanofthecloud.cotcpushnotifications;

import android.app.Activity;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.NotificationChannel;
import android.app.Notification;

import android.content.Context;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;

import android.graphics.Bitmap; 
import android.graphics.BitmapFactory; 

import android.media.RingtoneManager;

import android.net.Uri;

import android.os.Build;
import android.os.Bundle;

import android.support.v4.app.NotificationCompat;

import android.util.Log;

import com.google.android.gms.gcm.GcmListenerService;

import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

public class MyGcmListenerService extends GcmListenerService {

    private static final String TAG = "MyGcmListenerService";
    private NotificationManager notifManager = null;
    /**
     * Called when message is received.
     *
     * @param from SenderID of the sender.
     * @param data Data bundle containing message data as key/value pairs.
     *             For Set of keys use data.keySet().
     */
    // [START receive_message]
    @Override
    public void onMessageReceived(String from, Bundle data) {
        String message = data.getString("message");
        // Message received -> show a notification
        sendNotification(message);
    }
    // [END receive_message]

    /**
     * Create and show a simple notification containing the received GCM message.
     *
     * @param message GCM message received.
     */
    private void sendNotification(String message) {
	    Activity currentAct = UnityPlayer.currentActivity;
	    Class activityToOpen = currentAct != null ? currentAct.getClass() : UnityPlayerActivity.class;
        Intent intent = new Intent(this, activityToOpen);
        intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        PendingIntent pendingIntent = PendingIntent.getActivity(this, 0 /* Request code */, intent,
                PendingIntent.FLAG_ONE_SHOT);

        ApplicationInfo ai = null;
        try {
            ai = getPackageManager().getApplicationInfo(getPackageName(), PackageManager.GET_META_DATA);
            int notificationIcon = ai.metaData.getInt("cotc.GcmNotificationIcon", -1);
            if (notificationIcon == -1) {
                Log.e(TAG, "!!!!!!!!! cotc.GcmNotificationIcon not configured in manifest, push notifications won't work !!!!!!!!!");
                return;
            }
            int notificationLargeIcon = ai.metaData.getInt("cotc.GcmNotificationLargeIcon", -1);
            if (notificationLargeIcon == -1) {
                Log.e(TAG, "There is no large icon for push notifs, will only use default icon");
                return;
            }

            String pushNotifName = ai.metaData.getString("cotc.GcmNotificationTitle");
            if (pushNotifName == null) {
                Log.e(TAG, "!!!!!!!!! cotc.GcmNotificationTitle not configured in manifest, push notifications won't work !!!!!!!!!");
                return;
            }

            if(notifManager == null)
                notifManager = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
            NotificationCompat.Builder notificationBuilder;

            if(Build.VERSION.SDK_INT >= Build.VERSION_CODES.O)
            {
                int importance = NotificationManager.IMPORTANCE_HIGH;
                NotificationChannel channel = new NotificationChannel("CotC Channel", "CotC Channel", importance);
                channel.setDescription("CotC Channel");
                notifManager.createNotificationChannel(channel);
                notificationBuilder = new NotificationCompat.Builder(this, "CotC Channel");
            }
            else
                notificationBuilder = new NotificationCompat.Builder(this);

            Uri defaultSoundUri = RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION);

            notificationBuilder.setSmallIcon(notificationIcon)
                .setContentTitle(pushNotifName)
                .setContentText(message)
                .setAutoCancel(true)
                .setSound(defaultSoundUri)
                .setContentIntent(pendingIntent)
                .setPriority(Notification.PRIORITY_HIGH);
            if(notificationLargeIcon != -1)
                notificationBuilder.setLargeIcon(BitmapFactory.decodeResource(currentAct.getResources(), notificationLargeIcon));

            notifManager.notify(0 /* ID of notification */, notificationBuilder.build());
        } catch (Exception e) {
            Log.w(TAG, "Failed to handle push notification", e);
        }
    }
}
