/*
ArFoundationArvosController.cs - MonoBehaviour for ARpoise - ArFoundation - handling.

Copyright (C) 2022, Tamiko Thiel and Peter Graf - All Rights Reserved

ARpoise - Augmented Reality point of interest service environment 

This file is part of ARpoise.

    ARpoise is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    ARpoise is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with ARpoise.  If not, see <https://www.gnu.org/licenses/>.

For more information on 

Tamiko Thiel, see www.TamikoThiel.com/
Peter Graf, see www.mission-base.com/peter/
ARpoise, see www.ARpoise.com/

*/

using com.arpoise.arpoiseapp;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARFoundation.Samples;
using UnityEngine.XR.ARSubsystems;

public class ArFoundationArvosController : ArBehaviourSlam
{
#if HAS_AR_FOUNDATION_4_2
    public ArvosVisualizer ArvosVisualizer;
    public GameObject ArSessionOrigin;

    private ARPlaneManager _arPlaneManager;
    private ARRaycastManager _arRaycastManager;
    private PlaceOnPlane _placeOnPlane;

    private readonly Dictionary<string, ArvosVisualizer> _imageVisualizers = new Dictionary<string, ArvosVisualizer>();
    private readonly Dictionary<int, ArvosVisualizer> _slamVisualizers = new Dictionary<int, ArvosVisualizer>();

    /// <summary>
    /// The Unity Awake() method.
    /// </summary>
    public void Awake()
    {
        // Enable ARCore to target 60fps camera capture frame rate on supported devices.
        // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
        Application.targetFrameRate = 60;

        _arPlaneManager = ArSessionOrigin.GetComponent<ARPlaneManager>();
        _arRaycastManager = ArSessionOrigin.GetComponent<ARRaycastManager>();
        _placeOnPlane = ArSessionOrigin.GetComponent<PlaceOnPlane>();
        ArSessionOriginScript = ArSessionOrigin.GetComponent<ARSessionOrigin>();

        ArTrackedImageManager = ArSessionOrigin.GetComponent<ARTrackedImageManager>();
    }

    #region Start
    protected override void Start()
    {
        _arPlaneManager = ArSessionOrigin.GetComponent<ARPlaneManager>();
        _arRaycastManager = ArSessionOrigin.GetComponent<ARRaycastManager>();
        _placeOnPlane = ArSessionOrigin.GetComponent<PlaceOnPlane>();

        ArTrackedImageManager = ArSessionOrigin.GetComponent<ARTrackedImageManager>();
        ArMutableLibrary = ArTrackedImageManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;

        base.Start();
    }
    #endregion

    private int _slamHitCount = 0;
    private string _layerWebUrl = null;

    #region Update
    protected override void Update()
    {
        base.Update();

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
            if (_imageVisualizers.Any())
            {
                foreach (var visualizer in _imageVisualizers.Values)
                {
                    GameObject.Destroy(visualizer.gameObject);
                }
                _imageVisualizers.Clear();
            }
        }

        if (!IsSlam)
        {
            if (_arPlaneManager.enabled != IsSlam)
            {
                _arPlaneManager.enabled = IsSlam;
                SetAllPlanesActive(IsSlam);
            }
            if (_arRaycastManager.enabled != IsSlam)
            {
                _arRaycastManager.enabled = IsSlam;
            }

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

        // Make sure the correct images are used for tracking
        //
        if (ArTrackedImageManager.referenceLibrary != ArMutableLibrary)
        {
            if (_imageVisualizers.Any())
            {
                foreach (var visualizer in _imageVisualizers.Values)
                {
                    GameObject.Destroy(visualizer.gameObject);
                }
                _imageVisualizers.Clear();
            }
            if (ArMutableLibrary != null && ArMutableLibrary.count > 0)
            {
                ArTrackedImageManager.referenceLibrary = ArMutableLibrary;
                EnableImageManager(true);
            }
            else if (ArTrackedImageManager.enabled)
            {
                EnableImageManager(false);
                ArTrackedImageManager.referenceLibrary = null;
            }
        }

        if (!HasTriggerImages)
        {
            if (_imageVisualizers.Any())
            {
                foreach (var visualizer in _imageVisualizers.Values)
                {
                    GameObject.Destroy(visualizer.gameObject);
                }
                _imageVisualizers.Clear();
            }
            if (!IsSlam)
            {
                return;
            }
        }

        if (IsSlam)
        {
            var slamObjectsAvailable = AvailableSlamObjects.Any();
            if (!slamObjectsAvailable)
            {
                if (_arPlaneManager.enabled != slamObjectsAvailable)
                {
                    _arPlaneManager.enabled = slamObjectsAvailable;
                    SetAllPlanesActive(slamObjectsAvailable);
                }
                if (_arRaycastManager.enabled != slamObjectsAvailable)
                {
                    _arRaycastManager.enabled = slamObjectsAvailable;
                }

                SetInfoText("All augments placed.");
                return;
            }

            if (_arPlaneManager.enabled != IsSlam)
            {
                _arPlaneManager.enabled = IsSlam;
            }
            if (_arRaycastManager.enabled != IsSlam)
            {
                _arRaycastManager.enabled = IsSlam;
            }

            if (!_slamVisualizers.Any())
            {
                SetInfoText("Please tap on a plane.");
            }

            if (HasHitOnObject)
            {
                return;
            }

            // If the player has not touched the screen, we are done with this update.
            if (Input.touchCount < 1 || Input.GetTouch(0).phase != TouchPhase.Began)
            {
                return;
            }
            if (!_placeOnPlane.TryGetTouchPosition(out Vector2 touchPosition))
            {
                return;
            }

            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            if (_arRaycastManager.Raycast(touchPosition, hits, TrackableType.Planes))
            {
                // Raycast hits are sorted by distance, so the first one
                // will be the closest hit.
                var hitPose = hits[0].pose;

                int index = _slamHitCount++ % AvailableSlamObjects.Count;
                TriggerObject triggerObject = AvailableSlamObjects[index];

                ArvosVisualizer visualizer = Instantiate(ArvosVisualizer, hitPose.position, hitPose.rotation);
                visualizer.Pose = hitPose;
                visualizer.TriggerObject = triggerObject;
                visualizer.ArBehaviour = this;

                _slamVisualizers.Add(_slamHitCount, visualizer);
                VisualizedSlamObjects.Add(triggerObject);
            }
        }

        // Delete non active image visualizers
        foreach (var visualizer in _imageVisualizers.Values)
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

        if (FitToScanOverlay != null)
        {
            // Show the fit-to-scan overlay if there are no images that are Tracking and visible.
            var hasTriggerImages = ArTrackedImageManager.enabled && ArTrackedImageManager.referenceLibrary != null && ArTrackedImageManager.referenceLibrary.count > 0;
            var hasActiveObjects = _imageVisualizers.Values.Any(x => x.gameObject.activeSelf);
            var setActive = hasTriggerImages && !hasActiveObjects && !LayerPanelIsActive;
            if (FitToScanOverlay.activeSelf != setActive)
            {
                FitToScanOverlay.SetActive(setActive);
            }

            if (MenuEnabled.HasValue && MenuEnabled.Value && LayerItemList != null && LayerItemList.Any())
            {
                int timeoutSeconds = 20;
                if (FitToScanOverlay.activeSelf)
                {
                    long nowTicks = DateTime.Now.Ticks;
                    var second = nowTicks / 10000000L;
                    if (!_fitToScanOverlayActivationSecond.HasValue)
                    {
                        _fitToScanOverlayActivationSecond = second;
                    }
                    else
                    {
                        var value = _fitToScanOverlayActivationSecond.Value;
                        if (value + timeoutSeconds <= second)
                        {
                            var triggerObjects = TriggerObjects;
                            if (triggerObjects != null)
                            {
                                foreach (var t in triggerObjects.Values)
                                {
                                    t.isActive = false;
                                }
                            }
                            _fitToScanOverlayActivationSecond = null;
                            MenuButtonClick = new MenuButtonClickActivity { ArBehaviour = this };
                        }
                        else
                        {
                            SetInfoText($"Timeout in {value + timeoutSeconds - second} seconds.");
                        }
                    }
                }
                else
                {
                    _fitToScanOverlayActivationSecond = null;
                }
            }
        }
    }

    private long? _fitToScanOverlayActivationSecond = null;

    private void EnableImageManager(bool enable)
    {
        if (ArTrackedImageManager.enabled)
        {
            if (!enable)
            {
                ArTrackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
                ArTrackedImageManager.enabled = enable;
            }
        }
        else
        {
            if (enable)
            {
                ArTrackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
                ArTrackedImageManager.enabled = enable;
            }
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var image in eventArgs.added.Union(eventArgs.updated))
        {
            ArvosVisualizer visualizer = null;
            var name = image.referenceImage.name;
            _imageVisualizers.TryGetValue(name, out visualizer);
            if (image.trackingState == TrackingState.Tracking && visualizer == null)
            {
                TriggerObject triggerObject = TriggerObjects.Values.FirstOrDefault(x => x.triggerImageURL == name);
                if (triggerObject == null)
                {
                    ErrorMessage = "No trigger object for url " + image.referenceImage.name;
                    return;
                }
                if (!triggerObject.isActive || triggerObject.layerWebUrl != _layerWebUrl)
                {
                    // This image was loaded for a different layer
                    continue;
                }

                visualizer = Instantiate(ArvosVisualizer, image.transform.position, image.transform.rotation);
                visualizer.Image = image;
                visualizer.TriggerObject = triggerObject;
                visualizer.ArBehaviour = this;
                visualizer.TriggerObject.LastUpdateTime = DateTime.Now;
                visualizer.TriggerObject.ActivationTime = DateTime.Now;

                _imageVisualizers.Add(name, visualizer);
            }
            else if (image.trackingState == TrackingState.Tracking && visualizer != null)
            {
                visualizer.TriggerObject.LastUpdateTime = DateTime.Now;
            }
            //else if (image.trackingState == TrackingState.None && visualizer != null)
            //{
            //    _imageVisualizers.Remove(name);
            //    GameObject.Destroy(visualizer.gameObject);
            //}
        }
        //foreach (var trackedImage in eventArgs.removed)
        //{
        //    ErrorMessage = $"R {trackedImage.referenceImage.name}";
        //    return;
        //}
    }

    private void SetAllPlanesActive(bool value)
    {
        foreach (var plane in _arPlaneManager.trackables)
            plane.gameObject.SetActive(value);
    }
    #endregion
#endif
}
