#if UNITY_EDITOR

#define FULL_KEY

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Citrine.Animation.Editor;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace GameAssets.ArtTools.Editor
{
    public class CameraAnimationClipToTimeline {
        static PlayableDirector         _playableDirector;
        static CinemachineVirtualCamera _cinemachineVirtualCamera;
        static AnimationClip            _animationClip;
        static TimelineAsset            _timelineAsset;
        static AnimationTrack           _animationTrack;

        static GameObject     _recordGameObject;
        static GameObject     _recordTargetObject;
        static AnimationClip  _recordAnimationClip;
        //static AnimationClip  _originalAnimationClip;
        static AnimationCurve _posX;
        static AnimationCurve _posY;
        static AnimationCurve _posZ;
        static AnimationCurve _rotX;
        static AnimationCurve _rotY;
        static AnimationCurve _rotZ;
        static AnimationCurve _rotW;

        static AnimationCurve _targetPosX;
        static AnimationCurve _targetPosY;
        static AnimationCurve _targetPosZ;
        static int            _startFrame;
        static float          _startTime;

        static string _cameraAnimationClipPath = string.Empty;

        static void ClearAll() {
            _playableDirector         = null;
            _cinemachineVirtualCamera = null;
            _animationClip            = null;
            _timelineAsset            = null;
            _animationTrack           = null;
            if (_recordGameObject != null) {
                Object.DestroyImmediate(_recordGameObject);
                _recordGameObject = null;
            }

            if (_recordTargetObject != null) {
                Object.DestroyImmediate(_recordTargetObject);
                _recordTargetObject = null;
            }

            if (_recordAnimationClip != null) {
                Object.DestroyImmediate(_recordAnimationClip);
                _recordAnimationClip = null;
            }

            if (_recordAnimationClip != null) {
                Object.DestroyImmediate(_recordAnimationClip);
            }

            _posX = null;
            _posY = null;
            _posZ = null;
            _rotX = null;
            _rotY = null;
            _rotZ = null;
            _rotW = null;

            _targetPosX = null;
            _targetPosY = null;
            _targetPosZ = null;
            _startFrame = 0;
            _startTime  = 0.0f;
        }

        public static void ImportCameraAnimationToTimeline([NotNull] GameObject characterObject,
                                                           [NotNull]
                                                           IReadOnlyList<CameraAnimationData> cameraAnimationData,
                                                           bool isClear, bool isKeyframe, float keyframeThreshold,
                                                           float rotationError, float positionError, float scaleError) {
            ClearAll();
            FindNessaryComponents(characterObject);

            if (!_animationTrack.inClipMode) {
                _animationTrack.CreateInfiniteClip("Camera");
            }

            CreateAnimationClip(0);

            if (isClear) {
                RemovePositionRotationAnimationInTimelineTrack(_animationTrack);
            }
            else {
                CopyPositionRotationAnimation(_animationTrack);

                foreach (CameraAnimationData animationData in cameraAnimationData) {
                    float startTime = animationData.frame / (float)_timelineAsset.editorSettings.frameRate;
                    float endTime   = startTime + animationData.clip.length;
                    _posX = CutAnimationCurve(_posX, startTime, endTime);
                    _posY = CutAnimationCurve(_posY, startTime, endTime);
                    _posZ = CutAnimationCurve(_posZ, startTime, endTime);
                    _rotX = CutAnimationCurve(_rotX, startTime, endTime);
                    _rotY = CutAnimationCurve(_rotY, startTime, endTime);
                    _rotZ = CutAnimationCurve(_rotZ, startTime, endTime);
                    _rotW = CutAnimationCurve(_rotW, startTime, endTime);
                }
            }

            foreach (CameraAnimationData animationData in cameraAnimationData) {
                _startFrame    = animationData.frame;
                _animationClip = animationData.clip;
                _startTime     = _startFrame / (float)_timelineAsset.editorSettings.frameRate;

                ParseCameraAnimation(_animationClip);
            }

            SaveAnimationClip();

            if (isKeyframe) {
                _recordAnimationClip.ReduceKeyframes(rotationError, positionError, scaleError, true);
            }

            CopyAnimationClipToAnimationTrack(_animationTrack, _recordAnimationClip);

            ClearAll();
        }

        [NotNull] static AnimationCurve CutAnimationCurve([NotNull] AnimationCurve curve, float startTime, float endTime) {
            List<Keyframe> keepKeyframes = new();
            foreach (Keyframe keyframe in curve.keys) {
                if (keyframe.time < startTime || keyframe.time > endTime) {
                    keepKeyframes.Add(keyframe);
                }
            }

            return new AnimationCurve(keepKeyframes.ToArray());
        }

        // static void ProcessKeyframe(float keyframeThreshold) {
        //     // AnimationCurve[] animationCurves =
        //     //     KeyframeReduction.Reduction(new AnimationCurve[] { _posX, _posY, _posZ });
        //     // _posX = animationCurves[0];
        //     // _posY = animationCurves[1];
        //     // _posZ = animationCurves[2];
        //
        //     AnimationCurve[] animationRotationCurves =
        //         KeyframeReduction.Reduction(new AnimationCurve[] { _rotX, _rotY, _rotZ, _rotW });
        //     _rotX = animationRotationCurves[0];
        //     _rotY = animationRotationCurves[1];
        //     _rotZ = animationRotationCurves[2];
        //     _rotW = animationRotationCurves[3];
        //
        //     // _posX = CalculateKeyFrames(_posX, keyframeThreshold);
        //     // _posY = CalculateKeyFrames(_posY, keyframeThreshold);
        //     // _posZ = CalculateKeyFrames(_posZ, keyframeThreshold);
        //     //
        //     // (_rotX, _rotY, _rotZ, _rotW) = CalculateRotationKeyframes(_rotX, _rotY, _rotZ, _rotW, 0.5f);
        // }

        static bool FindNessaryComponents(GameObject gameObject)
        {
            _playableDirector         = FindTimeline(gameObject);
            _cinemachineVirtualCamera = FindCinemachineVirtualCamera(gameObject);
            if (_playableDirector == null || _cinemachineVirtualCamera == null)
            {
                EditorUtility.DisplayDialog("警告！", "找不到 Timeline 或攝影機", "確定");
                Debug.LogWarning("找不到 Timeline 或攝影機");
                return false;
            }

            _timelineAsset = _playableDirector.playableAsset as TimelineAsset;
            if (_timelineAsset == null)
            {
                EditorUtility.DisplayDialog("警告！", "找不到 TimelineAsset", "確定");
                Debug.LogWarning("找不到 TimelineAsset");
                return false;
            }

            _animationTrack =
                FindAnimationTrack(_playableDirector, _timelineAsset, _cinemachineVirtualCamera.gameObject);
            if (_animationTrack == null)
            {
                EditorUtility.DisplayDialog("警告！", "找不到攝影機的 AnimationTrack", "確定");
                Debug.LogWarning("找不到 Camera 的 AnimationTrack");
                return false;
            }

            return true;
        }

        //[MenuItem("GameObject/DEGames/匯入攝影機動畫到 Timeline", false, -10)]
        static void ImportCameraAnimationToTimeline()
        {
            ClearAll();

            if (!FindNessaryComponents(Selection.activeGameObject)) return;

            _animationClip = FindAnimationClipAsset($"{Selection.activeGameObject.name}_TimelineCamera");
            if (_animationClip == null)
            {
                EditorUtility.DisplayDialog("警告！", "找不到攝影機動畫", "確定");
                Debug.LogWarning("找不到攝影機動畫");
                return;
            }

            if (!_animationTrack.inClipMode)
            {
                _animationTrack.CreateInfiniteClip("Camera");
            }

            CreateAnimationClip(_animationClip.frameRate);
            ParseCameraAnimation(_animationClip);
            SaveAnimationClip();
            RemovePositionRotationAnimationInTimelineTrack(_animationTrack);
            CopyAnimationClipToAnimationTrack(_animationTrack, _recordAnimationClip);
            ClearAll();

            if (EditorUtility.DisplayDialog("警告", "是否刪除攝影機動畫檔", "刪除", "不刪除"))
            {
                DeleteCameraAnimationClipFile();
            }
        }

        //[MenuItem("GameObject/DEGames/輸出VirtualCamera所屬動畫軌成 Animation Clip", false, -10)]
        static void ExportVirtualCameraTrackToAnimationClip()
        {
            if (!FindNessaryComponents(Selection.activeGameObject)) return;
            ExportVirtualCameraTrackToAnimationClip(_animationTrack);
        }

        //[MenuItem("GameObject/DEGames/輸出VirtualCamera所屬動畫軌成 FBX", false, -10)]
        static void ExportVirtualCameraTrackToFBX()
        {
            if (!FindNessaryComponents(Selection.activeGameObject)) return;
            ExportVirtualCameraTrackToAnimationClip(_animationTrack);
            //ExportVirtualCameraTrackToFBX(_animationTrack);
        }

        [CanBeNull]
        static PlayableDirector FindTimeline([NotNull] GameObject gameObject)
        {
            return gameObject.GetComponentInChildren<PlayableDirector>();
        }

        [CanBeNull]
        static CinemachineVirtualCamera FindCinemachineVirtualCamera([NotNull] GameObject gameObject)
        {
            return gameObject.GetComponentInChildren<CinemachineVirtualCamera>();
        }

        [CanBeNull]
        static AnimationClip FindAnimationClipAsset(string assetName)
        {
            string[] assetGUIDs = AssetDatabase.FindAssets("t:AnimationClip " + assetName);
            if (assetGUIDs.Length != 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);
                _cameraAnimationClipPath = assetPath;
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            }

            assetGUIDs = AssetDatabase.FindAssets(assetName);
            if (assetGUIDs.Length != 0)
            {
                string     assetPath  = AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);
                Object[]   allAssets  = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (Object asset in allAssets)
                {
                    if (asset is AnimationClip clip && clip.name == assetName)
                    {
                        Debug.Log($"找到動畫剪輯: {clip.name}");
                        _cameraAnimationClipPath = assetPath;
                        // 使用該動畫，例如將其添加到 Animator
                        return clip;
                    }
                }
            }

            return null;
        }

        [CanBeNull]
        static AnimationTrack FindAnimationTrack(
            [NotNull] PlayableDirector playableDirector,
            [NotNull] TimelineAsset timelineAsset,
            [NotNull] GameObject gameObject)
        {
            foreach (TrackAsset trackAsset in timelineAsset.GetOutputTracks())
            {
                if (trackAsset is AnimationTrack animationTrack)
                {
                    Animator bindingObject = playableDirector.GetGenericBinding(trackAsset) as Animator;
                    if (bindingObject == null) continue;
                    if (bindingObject.gameObject != gameObject) continue;
                    return animationTrack;
                }
            }

            return null;
        }

        static void RemovePositionRotationAnimationInTimelineTrack([NotNull] AnimationTrack animationTrack) {
            EditorCurveBinding[] infiniteClipEditorCurveBindings =
                AnimationUtility.GetCurveBindings(animationTrack.infiniteClip);

            // remove original clip curve data
            foreach (EditorCurveBinding editorCurveBinding in infiniteClipEditorCurveBindings)
            {
                if ((editorCurveBinding.propertyName == "m_LocalPosition.x" ||
                     editorCurveBinding.propertyName == "m_LocalPosition.y" ||
                     editorCurveBinding.propertyName == "m_LocalPosition.z" ||
                     editorCurveBinding.propertyName == "m_LocalRotation.x" ||
                     editorCurveBinding.propertyName == "m_LocalRotation.y" ||
                     editorCurveBinding.propertyName == "m_LocalRotation.z" ||
                     editorCurveBinding.propertyName == "m_LocalRotation.w") &&
                    (string.IsNullOrEmpty(editorCurveBinding.path) ||
                     editorCurveBinding.path.Equals(_cinemachineVirtualCamera.name)))
                {
                    AnimationUtility.SetEditorCurve(animationTrack.infiniteClip, editorCurveBinding, null);
                }
            }
        }

        static void CopyPositionRotationAnimation([NotNull] AnimationTrack animationTrack) {
            EditorCurveBinding[] infiniteClipEditorCurveBindings =
                AnimationUtility.GetCurveBindings(animationTrack.infiniteClip);

            // remove original clip curve data
            foreach (EditorCurveBinding editorCurveBinding in infiniteClipEditorCurveBindings)
            {
                if (string.IsNullOrEmpty(editorCurveBinding.path) ||
                    editorCurveBinding.path.Equals(_cinemachineVirtualCamera.name)) {
                    switch (editorCurveBinding.propertyName) {
                        case "m_LocalPosition.x":
                            _posX = AnimationUtility.GetEditorCurve(animationTrack.infiniteClip, editorCurveBinding);
                            break;
                        case "m_LocalPosition.y":
                            _posY = AnimationUtility.GetEditorCurve(animationTrack.infiniteClip, editorCurveBinding);
                            break;
                        case "m_LocalPosition.z":
                            _posZ = AnimationUtility.GetEditorCurve(animationTrack.infiniteClip, editorCurveBinding);
                            break;
                        case "m_LocalRotation.x":
                            _rotX = AnimationUtility.GetEditorCurve(animationTrack.infiniteClip, editorCurveBinding);
                            break;
                        case "m_LocalRotation.y":
                            _rotY = AnimationUtility.GetEditorCurve(animationTrack.infiniteClip, editorCurveBinding);
                            break;
                        case "m_LocalRotation.z":
                            _rotZ = AnimationUtility.GetEditorCurve(animationTrack.infiniteClip, editorCurveBinding);
                            break;
                        case "m_LocalRotation.w":
                            _rotW = AnimationUtility.GetEditorCurve(animationTrack.infiniteClip, editorCurveBinding);
                            break;
                    }
                }

                // if ((editorCurveBinding.propertyName == "m_LocalPosition.x" ||
                //      editorCurveBinding.propertyName == "m_LocalPosition.y" ||
                //      editorCurveBinding.propertyName == "m_LocalPosition.z" ||
                //      editorCurveBinding.propertyName == "m_LocalRotation.x" ||
                //      editorCurveBinding.propertyName == "m_LocalRotation.y" ||
                //      editorCurveBinding.propertyName == "m_LocalRotation.z" ||
                //      editorCurveBinding.propertyName == "m_LocalRotation.w") &&
                //     (string.IsNullOrEmpty(editorCurveBinding.path) ||
                //      editorCurveBinding.path.Equals(_cinemachineVirtualCamera.name)))
                // {
                //
                // }
            }
        }

        static void CopyAnimationClipToAnimationTrack(
            [NotNull] AnimationTrack animationTrack,
            [NotNull] AnimationClip animationClip)
        {
            EditorCurveBinding[] editorCurveBindings = AnimationUtility.GetCurveBindings(animationClip);
            foreach (EditorCurveBinding editorCurveBinding in editorCurveBindings)
            {
                if (typeof(Object).IsAssignableFrom(editorCurveBinding.type))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding);
                    AnimationCurve newCurve = new (curve.keys);
                    animationTrack.infiniteClip.SetCurve(
                        editorCurveBinding.path,
                        editorCurveBinding.type,
                        editorCurveBinding.propertyName,
                        newCurve);
                }
            }
        }

        static void ExportVirtualCameraTrackToAnimationClip([NotNull] AnimationTrack animationTrack)
        {
            AnimationClip animationClip = Object.Instantiate(animationTrack.infiniteClip);
            if (animationClip == null) return;

            animationClip.legacy = true;
            string assetName = $"{Selection.activeGameObject.name}_TimelineCamera.anim";
            string assetPath = Path.Combine("Assets", assetName);
            AssetDatabase.CreateAsset(animationClip, assetPath);
            AssetDatabase.SaveAssets();
        }

        [NotNull] static IReadOnlyList<Keyframe> FetchKeyframe([NotNull] AnimationClip animationClip) {
            EditorCurveBinding[] editorCurveBindings =
                AnimationUtility.GetCurveBindings(animationClip);
            List<Keyframe> keyframes = new();

            foreach (EditorCurveBinding editorCurveBinding in editorCurveBindings)
            {
                AnimationCurve animationCurve = AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding);

                foreach (Keyframe keyframe in animationCurve.keys)
                {
                    if (keyframes.Any(e => Mathf.Approximately(e.time, keyframe.time))) continue;
                    keyframes.Add(keyframe);
                }
            }

            return keyframes;
        }

        static void ParseCameraAnimation([NotNull] AnimationClip animationClip)
        {
            string[]   assetGUIDs          = AssetDatabase.FindAssets(animationClip.name);
            GameObject animationGameObject = null;
            if (assetGUIDs.Length != 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);
                animationGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
            if (animationGameObject == null) return;

#if FULL_KEY
            float timer = 0f;
            while (timer < animationClip.length)
            {
                animationClip.SampleAnimation(animationGameObject, timer);
                CameraTransformAnimation(animationGameObject.transform, timer);
                timer += 1.0f / (float)_timelineAsset.editorSettings.frameRate;
            }
#else
            RecordPositionKeyframe(animationGameObject.transform.childCount == 0, animationClip);
            RecordRotationKeyframe(animationGameObject.transform.childCount == 0, animationClip);
            // float timer = 0f;
            // while (timer < animationClip.length)
            // {
            //     animationClip.SampleAnimation(animationGameObject, timer);
            //     CameraRotationAnimation(animationGameObject.transform, timer);
            //     timer += 1.0f / (float)_timelineAsset.editorSettings.frameRate;
            // }
#endif
        }

        static void RecordPositionKeyframe(bool isFreeCamera, [NotNull] AnimationClip animationClip) {
            EditorCurveBinding[] editorCurveBindings =
                AnimationUtility.GetCurveBindings(animationClip);

            foreach (EditorCurveBinding editorCurveBinding in editorCurveBindings) {
                if (!isFreeCamera && editorCurveBinding.path.ToLower().Contains("target")) continue;

                AnimationCurve currentAnimationCurve = editorCurveBinding.propertyName switch {
                                                           "m_LocalPosition.x" => _posX,
                                                           "m_LocalPosition.y" => _posY,
                                                           "m_LocalPosition.z" => _posZ,
                                                           _ => null
                                                       };
                if (currentAnimationCurve == null) continue;
                AnimationCurve animationCurve = AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding);

                foreach (Keyframe keyframe in animationCurve.keys)
                {
                    currentAnimationCurve.AddKey(keyframe);
                }
            }
        }

        static void RecordRotationKeyframe(bool isFreeCamera, [NotNull] AnimationClip animationClip) {
            EditorCurveBinding[] editorCurveBindings =
                AnimationUtility.GetCurveBindings(animationClip);

            if (isFreeCamera) {
                AnimationCurve rotXCurve = new ();
                AnimationCurve rotYCurve = new ();
                AnimationCurve rotZCurve = new ();
                foreach (EditorCurveBinding editorCurveBinding in editorCurveBindings) {
                    AnimationCurve currentAnimationCurve = editorCurveBinding.propertyName switch {
                                                               "m_LocalRotation.x" => rotXCurve,
                                                               "m_LocalRotation.y" => rotYCurve,
                                                               "m_LocalRotation.z" => rotZCurve,
                                                               //"m_LocalRotation.w" => _rotW,
                                                               _ => null
                                                           };
                    if (currentAnimationCurve == null) continue;
                    AnimationCurve animationCurve = AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding);

                    foreach (Keyframe keyframe in animationCurve.keys)
                    {
                        currentAnimationCurve.AddKey(keyframe);
                    }
                }

                for (int i = 0; i < rotXCurve.keys.Length; i++) {
                    //Quaternion rotation =
                    //    Quaternion.Euler(rotXCurve.keys[i].value, rotYCurve.keys[i].value, rotZCurve.keys[i].value);
                    _rotX.AddKey(_startTime + rotXCurve.keys[i].time, rotXCurve.keys[i].value);
                    _rotY.AddKey(_startTime + rotXCurve.keys[i].time, rotYCurve.keys[i].value);
                    _rotZ.AddKey(_startTime + rotXCurve.keys[i].time, rotZCurve.keys[i].value);
                    //_rotW.AddKey(rotXCurve.keys[i].time, rotation.w);
                }
            }
            else {
                RecordTargetPositionKeyframe(animationClip);
                List<Keyframe> positionKeyframes = new();
                List<Keyframe> keyframes = new();
                foreach (Keyframe keyframe in _posX.keys) {
                    positionKeyframes.Add(keyframe);
                }

                foreach (Keyframe keyframe in _targetPosX.keys) {
                   keyframes.Add(keyframe);
                }

                for (int i = 0; i < positionKeyframes.Count; i++) {
                    Vector3 position = Vector3.zero;
                    position.x = _posX.keys[i].value;
                    position.y = _posY.keys[i].value;
                    position.z = _posZ.keys[i].value;
                }

                int index = 0;
                foreach (Keyframe keyframe in keyframes) {
                    //Debug.Log($"{keyframe.time} | {keyframe.value} | In Tangent : {keyframe.inTangent} | Out Tangent : {keyframe.outTangent} | weight mode : {keyframe.weightedMode}");
                    //6.599991
                    //if (Mathf.Approximately(keyframe.time, 6.599991f))
                    //    Debug.Log(_targetPosX.keys[index].value.ToString());
                    Vector3  position = Vector3.zero;
                    Keyframe keyfameValue = _posX.keys.FirstOrDefault(e => e.time.ToString().Equals(keyframe.time.ToString()));
                    if (keyfameValue.Equals(default(Keyframe))) {
                        position.x = _posX.Evaluate(keyframe.time);
                    }
                    else {
                        position.x = keyfameValue.value;
                    }

                    keyfameValue = _posY.keys.FirstOrDefault(e => e.time.ToString().Equals(keyframe.time.ToString()));
                    if (keyfameValue.Equals(default(Keyframe))) {
                        position.y = _posY.Evaluate(keyframe.time);
                    }
                    else {
                        position.y = keyfameValue.value;
                    }

                    keyfameValue = _posZ.keys.FirstOrDefault(e => e.time.ToString().Equals(keyframe.time.ToString()));
                    if (keyfameValue.Equals(default(Keyframe))) {
                        position.z = _posZ.Evaluate(keyframe.time);
                    }
                    else {
                        position.z = keyfameValue.value;
                    }

                    Vector3 targetPosition = Vector3.zero;
                    targetPosition.x = _targetPosX.keys[index].value;
                    targetPosition.y = _targetPosY.keys[index].value;
                    targetPosition.z = _targetPosZ.keys[index].value;

                    _recordGameObject.transform.position = position;
                    _recordTargetObject.transform.position = targetPosition;
                    _recordGameObject.transform.LookAt(_recordTargetObject.transform);

                    Keyframe temp = new (_startTime + keyframe.time, _recordGameObject.transform.rotation.x, keyframe.inTangent, keyframe.outTangent);
                    _rotX.AddKey(temp);//keyframe.time, _recordGameObject.transform.rotation.x);

                    temp = new (_startTime + keyframe.time, _recordGameObject.transform.rotation.y, keyframe.inTangent, keyframe.outTangent);
                    _rotY.AddKey(temp);

                    temp = new (_startTime + keyframe.time, _recordGameObject.transform.rotation.z, keyframe.inTangent, keyframe.outTangent);
                    _rotZ.AddKey(temp);

                    temp = new (_startTime + keyframe.time, _recordGameObject.transform.rotation.w, keyframe.inTangent, keyframe.outTangent);
                    _rotW.AddKey(temp);
                    index++;
                }
            }
        }

        static void RecordTargetPositionKeyframe([NotNull] AnimationClip animationClip) {
            EditorCurveBinding[] editorCurveBindings =
                AnimationUtility.GetCurveBindings(animationClip);

            foreach (EditorCurveBinding editorCurveBinding in editorCurveBindings) {
                if (!editorCurveBinding.path.ToLower().Contains("target")) continue;
                AnimationCurve currentAnimationCurve = editorCurveBinding.propertyName switch {
                                                           "m_LocalPosition.x" => _targetPosX,
                                                           "m_LocalPosition.y" => _targetPosY,
                                                           "m_LocalPosition.z" => _targetPosZ,
                                                           _ => null
                                                       };
                if (currentAnimationCurve == null) continue;
                AnimationCurve animationCurve = AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding);

                foreach (Keyframe keyframe in animationCurve.keys) {
                    currentAnimationCurve.AddKey(keyframe);
                }
            }
        }

        static void PrintAnimationClipInfo([NotNull] Transform transform)
        {
            Debug.Log($"Object : {transform.name} : {transform.position} : {transform.rotation.eulerAngles}");
            foreach (Transform tr in transform.transform)
            {
                PrintAnimationClipInfo(tr);
            }
        }

        static void CameraTransformAnimation(Transform cameraAnimationRoot, float time)
        {
            if (cameraAnimationRoot == null) return;
            if (cameraAnimationRoot.childCount == 0)
            {
                _recordGameObject.transform.position = cameraAnimationRoot.position;
                _recordGameObject.transform.rotation = cameraAnimationRoot.rotation;
            }
            else
            {
                Transform cameraTransform       = cameraAnimationRoot.GetChild(0);
                Transform cameraTargetTransform = cameraAnimationRoot.GetChild(1);
                _recordGameObject.transform.position = cameraTransform.position;
                _recordGameObject.transform.LookAt(cameraTargetTransform);
            }

            _posX.AddKey(_startTime + time, _recordGameObject.transform.position.x);
            _posY.AddKey(_startTime + time, _recordGameObject.transform.position.y);
            _posZ.AddKey(_startTime + time, _recordGameObject.transform.position.z);

            // 記錄 Rotation (Quaternion)
            Quaternion rotation = _recordGameObject.transform.rotation;
            _rotX.AddKey(_startTime + time, rotation.x);
            _rotY.AddKey(_startTime + time, rotation.y);
            _rotZ.AddKey(_startTime + time, rotation.z);
            _rotW.AddKey(_startTime + time, rotation.w);
        }

        static void CameraRotationAnimation(Transform cameraAnimationRoot, float time)
        {
            if (cameraAnimationRoot == null) return;
            if (cameraAnimationRoot.childCount == 0)
            {
                _recordGameObject.transform.position = cameraAnimationRoot.position;
                _recordGameObject.transform.rotation = cameraAnimationRoot.rotation;
            }
            else
            {
                Transform cameraTransform       = cameraAnimationRoot.GetChild(0);
                Transform cameraTargetTransform = cameraAnimationRoot.GetChild(1);
                _recordGameObject.transform.position = cameraTransform.position;
                _recordGameObject.transform.LookAt(cameraTargetTransform);
            }

            // 記錄 Rotation (Quaternion)
            Quaternion rotation = _recordGameObject.transform.rotation;
            _rotX.AddKey(_startTime + time, rotation.x);
            _rotY.AddKey(_startTime + time, rotation.y);
            _rotZ.AddKey(_startTime + time, rotation.z);
            _rotW.AddKey(_startTime + time, rotation.w);
        }

        static void CreateAnimationClip(float frameRate)
        {
            if (_recordGameObject != null) {
                Object.DestroyImmediate(_recordGameObject);
            }
            if (_recordAnimationClip != null) {
                Object.DestroyImmediate(_recordAnimationClip);
            }
            if (_recordTargetObject != null) {
                Object.DestroyImmediate(_recordTargetObject);
            }
            _recordGameObject    = new GameObject();
            _recordTargetObject  = new GameObject();
            _recordAnimationClip = new AnimationClip();//{ frameRate = frameRate };)

            // 初始化曲線
            _posX = new AnimationCurve();
            _posY = new AnimationCurve();
            _posZ = new AnimationCurve();

            _rotX = new AnimationCurve();
            _rotY = new AnimationCurve();
            _rotZ = new AnimationCurve();
            _rotW = new AnimationCurve();

            _targetPosX = new AnimationCurve();
            _targetPosY = new AnimationCurve();
            _targetPosZ = new AnimationCurve();
        }

        static void SaveAnimationClip()
        {
            // 將曲線應用到 AnimationClip
            _recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalPosition.x", _posX);
            _recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalPosition.y", _posY);
            _recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalPosition.z", _posZ);

            _recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalRotation.x", _rotX);
            _recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalRotation.y", _rotY);
            _recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalRotation.z", _rotZ);
            _recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalRotation.w", _rotW);
        }

        static void DeleteCameraAnimationClipFile()
        {
            if (string.IsNullOrEmpty(_cameraAnimationClipPath)) return;
            AssetDatabase.DeleteAsset(_cameraAnimationClipPath);
            _cameraAnimationClipPath = string.Empty;
        }

        static AnimationCurve CalculateKeyFrames(AnimationCurve curve, float threshold) {
            List<Keyframe> newKeyframes = new();
            if (newKeyframes == null) {
                throw new ArgumentNullException(nameof(newKeyframes));
            }

            newKeyframes.Add(new Keyframe(curve.keys[0].time, curve.keys[0].value));

            for (int i = 1; i < curve.keys.Length - 1; ++i) {
                float previousValue = curve.keys[i-1].value;
                float currentValue = curve.keys[i].value;
                float nextValue = curve.keys[i+1].value;

                if ((currentValue - previousValue) > threshold || (nextValue - currentValue) > threshold) {
                    newKeyframes.Add(new Keyframe(curve.keys[i].time, curve.keys[i].value));
                }
            }

            newKeyframes.Add(new Keyframe(curve.keys[^1].time, curve.keys[^1].value));

            for (int i = 0; i < newKeyframes.Count - 1; ++i) {
                Keyframe keyframe = newKeyframes[i];
                if (i > 0 && i < newKeyframes.Count - 1) {
                    float dx = newKeyframes[i+1].time - newKeyframes[i - 1].time;
                    float dy = newKeyframes[i+1].value - newKeyframes[i - 1].value;
                    keyframe.inTangent  = dy / dx;
                    keyframe.outTangent = dy / dx;
                }
                else
                {
                    keyframe.inTangent  = 0;
                    keyframe.outTangent = 0;
                }

                newKeyframes[i] = keyframe;
            }

            return new AnimationCurve(newKeyframes.ToArray());
        }

        public static (AnimationCurve x, AnimationCurve y, AnimationCurve z, AnimationCurve w)
            CalculateRotationKeyframes(AnimationCurve rotX, AnimationCurve rotY, AnimationCurve rotZ, AnimationCurve rotW, float threshold) {
            List<Keyframe> newXKeyframes = new();
            List<Keyframe> newYKeyframes = new();
            List<Keyframe> newZKeyframes = new();
            List<Keyframe> newWKeyframes = new();

            newXKeyframes.Add(new Keyframe(rotX.keys[0].time, rotX.keys[0].value));
            newYKeyframes.Add(new Keyframe(rotY.keys[0].time, rotY.keys[0].value));
            newZKeyframes.Add(new Keyframe(rotZ.keys[0].time, rotZ.keys[0].value));
            newWKeyframes.Add(new Keyframe(rotW.keys[0].time, rotW.keys[0].value));

            for (int i = 1; i < rotX.keys.Length - 1; ++i) {
                float previousXValue = rotX.keys[i-1].value;
                float currentXValue  = rotX.keys[i].value;
                float nextXValue     = rotX.keys[i+1].value;

                float previousYValue = rotY.keys[i-1].value;
                float currentYValue  = rotY.keys[i].value;
                float nextYValue     = rotY.keys[i+1].value;

                float previousZValue = rotZ.keys[i-1].value;
                float currentZValue  = rotZ.keys[i].value;
                float nextZValue     = rotZ.keys[i+1].value;

                float previousWValue = rotW.keys[i-1].value;
                float currentWValue  = rotW.keys[i].value;
                float nextWValue     = rotW.keys[i+1].value;

                Quaternion previousValue = new (previousXValue, previousYValue, previousZValue, previousWValue);
                Quaternion currentValue = new (currentXValue, currentYValue, currentZValue, currentWValue);
                Quaternion nextValue = new (nextXValue, nextYValue, nextZValue, nextWValue);

                float angleDif = Quaternion.Angle(previousValue, currentValue);

                if (angleDif < threshold) {
                    newXKeyframes.Add(new Keyframe(rotX.keys[i].time, rotX.keys[i].value));
                    newYKeyframes.Add(new Keyframe(rotY.keys[i].time, rotY.keys[i].value));
                    newZKeyframes.Add(new Keyframe(rotZ.keys[i].time, rotZ.keys[i].value));
                    newWKeyframes.Add(new Keyframe(rotW.keys[i].time, rotW.keys[i].value));
                }
            }

            newXKeyframes.Add(new Keyframe(rotX.keys[^1].time, rotX.keys[^1].value));
            newYKeyframes.Add(new Keyframe(rotY.keys[^1].time, rotY.keys[^1].value));
            newZKeyframes.Add(new Keyframe(rotZ.keys[^1].time, rotZ.keys[^1].value));
            newWKeyframes.Add(new Keyframe(rotW.keys[^1].time, rotW.keys[^1].value));

            for (int i = 0; i < newXKeyframes.Count; i++)
            {
                if (i > 0 && i < newXKeyframes.Count - 1)
                {
                    float dx = newXKeyframes[i + 1].time - newXKeyframes[i - 1].time;

                    Keyframe tempKeyframe = newXKeyframes[i];
                    tempKeyframe.inTangent  = (newXKeyframes[i + 1].value - newXKeyframes[i - 1].value) / dx;
                    tempKeyframe.outTangent = newXKeyframes[i].inTangent;
                    newXKeyframes[i] = tempKeyframe;

                    tempKeyframe            = newYKeyframes[i];
                    tempKeyframe.inTangent  = (newYKeyframes[i + 1].value - newYKeyframes[i - 1].value) / dx;
                    tempKeyframe.outTangent = newYKeyframes[i].inTangent;
                    newYKeyframes[i] = tempKeyframe;

                    tempKeyframe            = newZKeyframes[i];
                    tempKeyframe.inTangent  = (newZKeyframes[i + 1].value - newZKeyframes[i - 1].value) / dx;
                    tempKeyframe.outTangent = newZKeyframes[i].inTangent;
                    newZKeyframes[i] = tempKeyframe;

                    tempKeyframe            = newWKeyframes[i];
                    tempKeyframe.inTangent  = (newWKeyframes[i + 1].value - newWKeyframes[i - 1].value) / dx;
                    tempKeyframe.outTangent = newWKeyframes[i].inTangent;
                    newWKeyframes[i] = tempKeyframe;
                }
            }

            return (new AnimationCurve(newXKeyframes.ToArray()),
                    new AnimationCurve(newYKeyframes.ToArray()),
                    new AnimationCurve(newZKeyframes.ToArray()),
                    new AnimationCurve(newWKeyframes.ToArray()));
        }
    }
}
#endif
