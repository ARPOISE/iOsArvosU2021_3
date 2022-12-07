//-----------------------------------------------------------------------
// <copyright file="AugmentedImageExampleController.cs" company="Google">
//
// Copyright 2018 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

/*
AugmentedImageExampleController.cs - MonoBehaviour for setting image triggers of the Android version of image trigger ARpoise, aka AR-vos.

This file is part of ARpoise.

This file is derived from image trigger example of the Google ARCore SDK for Unity

https://github.com/google-ar/arcore-unity-sdk

The license of the original file is shown above.

For more information on 

Tamiko Thiel, see www.TamikoThiel.com/
Peter Graf, see www.mission-base.com/peter/
ARpoise, see www.ARpoise.com/

*/

namespace GoogleARCore.Examples.AugmentedImage
{
    using com.arpoise.arpoiseapp;
#if HAS_AR_CORE
    using GoogleARCore;
    using GoogleARCore.Examples.Common;
    using System;
#endif
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Controller for AugmentedImage example.
    /// </summary>
    public class AugmentedImageExampleController : ArBehaviourSlam
    {
#if HAS_AR_CORE

        public Camera FirstPersonCamera;

        /// <summary>
        /// A prefab for visualizing an AugmentedImage.
        /// </summary>
        public AugmentedImageVisualizer AugmentedImageVisualizerPrefab;

        private readonly Dictionary<int, AugmentedImageVisualizer> _visualizers = new Dictionary<int, AugmentedImageVisualizer>();

        private readonly Dictionary<int, AugmentedImageVisualizer> _slamVisualizers = new Dictionary<int, AugmentedImageVisualizer>();

        /// <summary>
        /// The Unity Awake() method.
        /// </summary>
        public void Awake()
        {
            // Enable ARCore to target 60fps camera capture frame rate on supported devices.
            // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
            Application.targetFrameRate = 60;
        }

        /// <summary>
        /// True if the app is in the process of quitting due to an ARCore connection error, otherwise false.
        /// </summary>
        private bool _isQuitting = false;

        protected override void Start()
        {
            base.Start();
        }

        private int _slamHitCount = 0;
        private string _layerWebUrl = null;

        //private bool _depthConfigured = false;

        /// <summary>
        /// The Unity Update method.
        /// </summary>
        protected override void Update()
        {
            base.Update();

            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (_isQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                ShowAndroidToastMessage("Camera permission is needed to run this application.");
                _isQuitting = true;
                Invoke("DoQuit", 1.5f);
            }
            else if (Session.Status.IsError())
            {
                ShowAndroidToastMessage("ARCore encountered a problem connecting. Please start the app again.");
                _isQuitting = true;
                Invoke("DoQuit", 1.5f);
            }

            var fitToScanOverlay = FitToScanOverlay;

            if (_layerWebUrl != LayerWebUrl)
            {
                _layerWebUrl = LayerWebUrl;
                if (_slamVisualizers.Any())
                {
                    foreach (var visualizer in _slamVisualizers.Values)
                    {
                        GameObject.Destroy(visualizer.gameObject);
                    }
                    _slamVisualizers.Clear();
                    VisualizedSlamObjects.Clear();
                }
                if (_visualizers.Any())
                {
                    foreach (var visualizer in _visualizers.Values)
                    {
                        GameObject.Destroy(visualizer.gameObject);
                    }
                    _visualizers.Clear();
                }
            }

            if (!IsSlam)
            {
                _slamHitCount = 0;
                if (_slamVisualizers.Any())
                {
                    foreach (var visualizer in _slamVisualizers.Values)
                    {
                        GameObject.Destroy(visualizer.gameObject);
                    }
                    _slamVisualizers.Clear();
                    VisualizedSlamObjects.Clear();
                }
            }

            if (!HasTriggerImages)
            {
                if (fitToScanOverlay != null)
                {
                    if (fitToScanOverlay.activeSelf != false)
                    {
                        fitToScanOverlay.SetActive(false);
                    }
                }
                if (_visualizers.Any())
                {
                    foreach (var visualizer in _visualizers.Values)
                    {
                        GameObject.Destroy(visualizer.gameObject);
                    }
                    _visualizers.Clear();
                }
                if (!IsSlam)
                {
                    return;
                }
            }

            if (IsSlam)
            {
                //if (!_depthConfigured)
                //{
                //    _depthConfigured = true;
                //    bool depthEnabled = Session.IsDepthModeSupported(DepthMode.Automatic);
                //    var camera = ArCamera;
                //    if (camera != null)
                //    {
                //        (camera.GetComponent(typeof(DepthEffect)) as MonoBehaviour).enabled = depthEnabled;
                //    }
                //}
                var slamObjectsAvailable = AvailableSlamObjects.Any();
                if (!slamObjectsAvailable)
                {
                    SetInfoText("All augments placed.");
                    return;
                }

                if (!_slamVisualizers.Any() && PlaneDiscovery != null)
                {
                    var planeDiscoveryGuide = PlaneDiscovery.GetComponent<PlaneDiscoveryGuide>();
                    if (planeDiscoveryGuide != null && planeDiscoveryGuide.HasDetectedPlanes)
                    {
                        SetInfoText("Please tap on a plane.");
                    }
                }

                if (HasHitOnObject)
                {
                    return;
                }

                // If the player has not touched the screen, we are done with this update.
                Touch touch;
                if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
                {
                    return;
                }

                // Raycast against the location the player touched to search for planes.
                TrackableHit hit;
                TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                    TrackableHitFlags.FeaturePointWithSurfaceNormal;
#if HAS_AR_CORE_2
                // Allows the depth image to be queried for hit tests.
                raycastFilter |= TrackableHitFlags.Depth;
#endif
                bool foundHit = Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit);
                if (foundHit)
                {
                    //Debug.Log("foundHit hit");

                    // Use hit pose and camera pose to check if hittest is from the
                    // back of the plane, if it is, no need to create the anchor.
                    if ((hit.Trackable is DetectedPlane) &&
                        Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                            hit.Pose.rotation * Vector3.up) < 0)
                    {
                        //Debug.Log("Hit at back of the current DetectedPlane");
                    }
                    else
                    {
                        // Create an anchor to allow ARCore to track the hitpoint as understanding of
                        // the physical world evolves.
                        var anchor = hit.Trackable.CreateAnchor(hit.Pose);

                        int index = _slamHitCount++ % AvailableSlamObjects.Count;
                        TriggerObject triggerObject = AvailableSlamObjects[index];

                        AugmentedImageVisualizer visualizer = Instantiate(AugmentedImageVisualizerPrefab, anchor.transform);
                        visualizer.Pose = hit.Pose;
                        visualizer.TriggerObject = triggerObject;
                        visualizer.ArBehaviour = this;

                        _slamVisualizers.Add(_slamHitCount, visualizer);
                        VisualizedSlamObjects.Add(triggerObject);
                    }
                }
                return;
            }

#if HAS_AR_CORE_2
            // Get updated augmented images for this frame.
            var images = new List<AugmentedImage>();
            Session.GetTrackables<AugmentedImage>(images, TrackableQueryFilter.All);

            foreach (var image in images.ToArray())
            {
                AugmentedImageVisualizer visualizer = null;
                _visualizers.TryGetValue(image.DatabaseIndex, out visualizer);
                if (image.TrackingState == TrackingState.Tracking)
                {
                    if (visualizer == null && image.TrackingMethod == AugmentedImageTrackingMethod.FullTracking)
                    {
                        TriggerObject triggerObject = null;
                        if (!TriggerObjects.TryGetValue(image.DatabaseIndex, out triggerObject))
                        {
                            ErrorMessage = "No trigger object for database index " + image.DatabaseIndex;
                            return;
                        }
                        if (!triggerObject.isActive || triggerObject.layerWebUrl != _layerWebUrl)
                        {
                            // This image was loaded for a different layer
                            images.Remove(image);
                        }
                    }
                }
            }

            var hasFullTrackingImage = images.Any(x => x.TrackingMethod == AugmentedImageTrackingMethod.FullTracking);

            // Create visualizers and anchors for updated augmented images that are tracking and do not previously
            // have a visualizer. Remove visualizers for stopped images.
            foreach (var image in images)
            {
                AugmentedImageVisualizer visualizer = null;
                _visualizers.TryGetValue(image.DatabaseIndex, out visualizer);
                if (image.TrackingState == TrackingState.Tracking)
                {
                    if (image.TrackingMethod == AugmentedImageTrackingMethod.FullTracking)
                    {
                        if (visualizer == null)
                        {
                            TriggerObject triggerObject = null;
                            if (!TriggerObjects.TryGetValue(image.DatabaseIndex, out triggerObject))
                            {
                                ErrorMessage = "No trigger object for database index " + image.DatabaseIndex;
                                return;
                            }

                            // Create an anchor to ensure that ARCore keeps tracking this augmented image.
                            Anchor anchor = image.CreateAnchor(image.CenterPose);
                            visualizer = Instantiate(AugmentedImageVisualizerPrefab, anchor.transform);
                            visualizer.Image = image;
                            visualizer.TriggerObject = triggerObject;
                            visualizer.ArBehaviour = this;
                            visualizer.TriggerObject.LastUpdateTime = DateTime.Now;
                            visualizer.TriggerObject.ActivationTime = DateTime.Now;

                            _visualizers.Add(image.DatabaseIndex, visualizer);
                        }
                        else
                        {
                            visualizer.TriggerObject.LastUpdateTime = DateTime.Now;
                            if (!visualizer.gameObject.activeSelf)
                            {
                                visualizer.gameObject.SetActive(true);
                            }
                        }
                    }
                    else if (image.TrackingMethod == AugmentedImageTrackingMethod.LastKnownPose)
                    {
                        if (!hasFullTrackingImage)
                        {
                            var keepLastKnownPose = visualizer.TriggerObject?.poi?.KeepLastKnownPose;
                            if (keepLastKnownPose.HasValue && keepLastKnownPose.Value)
                            {
                                visualizer.TriggerObject.LastUpdateTime = DateTime.Now;
                            }
                        }
                    }
                }
                else if (visualizer != null)
                {
                    if (visualizer.gameObject.activeSelf)
                    {
                        visualizer.gameObject.SetActive(false);
                    }
                }
            }
#else
            // Get updated augmented images for this frame.
            var images = new List<AugmentedImage>();
            Session.GetTrackables<AugmentedImage>(images, TrackableQueryFilter.Updated);

            // Create visualizers and anchors for updated augmented images that are tracking and do not previously
            // have a visualizer. Remove visualizers for stopped images.
            foreach (var image in images)
            {
                AugmentedImageVisualizer visualizer = null;
                _visualizers.TryGetValue(image.DatabaseIndex, out visualizer);
                if (image.TrackingState == TrackingState.Tracking && visualizer == null)
                {
                    TriggerObject triggerObject = null;
                    if (!TriggerObjects.TryGetValue(image.DatabaseIndex, out triggerObject))
                    {
                        ErrorMessage = "No trigger object for database index " + image.DatabaseIndex;
                        return;
                    }
                    if (!triggerObject.isActive || triggerObject.layerWebUrl != _layerWebUrl)
                    {
                        // This image was loaded for a different layer
                        continue;
                    }

                    // Create an anchor to ensure that ARCore keeps tracking this augmented image.
                    Anchor anchor = image.CreateAnchor(image.CenterPose);
                    visualizer = Instantiate(AugmentedImageVisualizerPrefab, anchor.transform);
                    visualizer.Image = image;
                    visualizer.TriggerObject = triggerObject;
                    visualizer.ArBehaviour = this;
                    visualizer.TriggerObject.LastUpdateTime = DateTime.Now;

                    _visualizers.Add(image.DatabaseIndex, visualizer);
                }
                else if (image.TrackingState == TrackingState.Tracking && visualizer != null)
                {
                    visualizer.TriggerObject.LastUpdateTime = DateTime.Now;
                }
                else if (image.TrackingState == TrackingState.Stopped && visualizer != null)
                {
                    _visualizers.Remove(image.DatabaseIndex);
                    GameObject.Destroy(visualizer.gameObject);
                }
            }
#endif
            // Delete non active image visualizers
            foreach (var visualizer in _visualizers.Values.ToList())
            {
                if (visualizer.TriggerObject?.poi != null)
                {
                    var trackingTimeout = visualizer.TriggerObject.poi.TrackingTimeout;
                    if (trackingTimeout > 0)
                    {
                        if (visualizer.TriggerObject.LastUpdateTime.AddMilliseconds(trackingTimeout) < DateTime.Now)
                        {
                            if (visualizer.gameObject.activeSelf)
                            {
                                visualizer.gameObject.SetActive(false);
                            }
                        }
                    }
                }
            }

            if (fitToScanOverlay != null)
            {
                // Show the fit-to-scan overlay if there are no images that are Tracking and visible.
                var hasActiveObjects = _visualizers.Values.Any(x => x.Image.TrackingState == TrackingState.Tracking && x.gameObject.activeSelf);
                var setActive = !hasActiveObjects && !LayerPanelIsActive;
                if (fitToScanOverlay.activeSelf != setActive)
                {
                    fitToScanOverlay.SetActive(setActive);
                }
            }
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                        message, 0);
                    toastObject.Call("show");
                }));
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void DoQuit()
        {
            Application.Quit();
        }
#endif
    }
}
