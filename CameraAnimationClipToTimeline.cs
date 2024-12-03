//#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
//using UnityEditor.Formats.Fbx.Exporter;

namespace DEGames.ArtTools.Editor
{
    public class CameraAnimationClipToTimeline
    {
        static PlayableDirector _playableDirector = null;
        static CinemachineVirtualCamera _cinemachineVirtualCamera = null;
        static AnimationClip _animationClip = null;
        static TimelineAsset _timelineAsset = null;
        static AnimationTrack _animationTrack = null;

        static GameObject _recordGameObject = null;
        static AnimationClip _recordAnimationClip = null;
        static AnimationCurve _posX = null;
        static AnimationCurve _posY = null;
        static AnimationCurve _posZ = null;
        static AnimationCurve _rotX = null;
        static AnimationCurve _rotY = null;
        static AnimationCurve _rotZ = null;
        static AnimationCurve _rotW = null;

        static string _cameraAnimationClipPath = string.Empty;

        static void ClearAll()
        {
            _playableDirector = null;
            _cinemachineVirtualCamera = null;
            _animationClip = null;
            _timelineAsset = null;
            _animationTrack = null;
            if (_recordGameObject != null)
            {
                GameObject.DestroyImmediate(_recordGameObject);
                _recordGameObject = null;
            }
            if (_recordAnimationClip != null)
            {
                GameObject.DestroyImmediate(_recordAnimationClip);
                _recordAnimationClip = null;
            }
            _posX = null;
            _posY = null;
            _posZ = null;
            _rotX = null;
            _rotY = null;
            _rotZ = null;
            _rotW = null;
        }

        static bool FindNessaryComponents()
        {
            ClearAll();

            _playableDirector = FindTimeline(Selection.activeGameObject);
            _cinemachineVirtualCamera = FindCinemachineVirtualCamera(Selection.activeGameObject);
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

        [MenuItem("GameObject/DEGames/匯入攝影機動畫到 Timeline", false, -10)]
        static void ImportCameraAnimationToTimeline()
        {
            if (!FindNessaryComponents()) return;

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
            CopyAnimationClipToAnimationTrack(_animationTrack, _recordAnimationClip);
            ClearAll();
            //DeleteCameraAnimationClipFile();
        }

        [MenuItem("GameObject/DEGames/Fetch 攝影機動畫 Keyframe data", false, -10)]
        static void FetchCameraAnimationKeyframeData()
        {
            if (!FindNessaryComponents()) return;

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
            FetchAnimationKeyframe();
            CopyAnimationClipToAnimationTrack(_animationTrack, _recordAnimationClip);
            ClearAll();
        }

        class PositionKeyFrameData
        {
            public float _time;
            public string _name;
            public Vector3 _value;
        }

        static void FetchAnimationKeyframe()
        {
            AnimationClipCurveData[]   curves               = AnimationUtility.GetAllCurves(_animationClip, true);
            List<Vector3>              cameraPosition       = new List<Vector3>();
            List<Vector3>              cameraTargetPosition = new List<Vector3>();
            AnimationClipCurveData     cameraPositionCurveData = null;
            AnimationClipCurveData     cameraTargetPositionCurveData = null;
            List<PositionKeyFrameData> cameraKeyframeData      = new List<PositionKeyFrameData>();
            List<PositionKeyFrameData> cameraTargetKeyframeData = new List<PositionKeyFrameData>();

            foreach (AnimationClipCurveData curveData in curves)
            {
                if (!curveData.propertyName.ToLower().Contains("position"))
                {
                    continue;
                }

                List<PositionKeyFrameData> tempList = cameraKeyframeData;
                if (curveData.path.ToLower().Contains("target"))
                {
                    tempList = cameraTargetKeyframeData;
                }

                Keyframe[] keyframes = curveData.curve.keys;
                foreach (Keyframe keyframe in keyframes)
                {
                    PositionKeyFrameData positionKeyFrameData =
                        tempList.FirstOrDefault(e => e._time.Equals(keyframe.time));
                    if (positionKeyFrameData == null)
                    {
                        positionKeyFrameData       = new PositionKeyFrameData();
                        positionKeyFrameData._name = curveData.path;
                        positionKeyFrameData._time = keyframe.time;
                        if (curveData.propertyName == "m_LocalPosition.x")
                            positionKeyFrameData._value.x = curveData.curve.Evaluate(keyframe.time);
                        else if (curveData.propertyName == "m_LocalPosition.y")
                            positionKeyFrameData._value.y = curveData.curve.Evaluate(keyframe.time);
                        else if (curveData.propertyName == "m_LocalPosition.z")
                            positionKeyFrameData._value.z = curveData.curve.Evaluate(keyframe.time);
                        tempList.Add(positionKeyFrameData);
                        // int index = cameraKeyframeData.FindLastIndex(e => e._time < keyframe.time);
                        // if (index < 0 || index == cameraKeyframeData.Count - 1)
                        // {
                        //     cameraKeyframeData.Add(positionKeyFrameData);
                        // }
                        // else
                        // {
                        //     if (curveData.propertyName == "m_LocalPosition.x")
                        //     {
                        //         AnimationCurve animationCurveY = new AnimationCurve();
                        //         animationCurveY.AddKey(cameraKeyframeData[index]._time, cameraKeyframeData[index]._value.y);
                        //         animationCurveY.AddKey(cameraKeyframeData[index + 1]._time, cameraKeyframeData[index + 1]._value.y);
                        //     }
                        //     else if (curveData.propertyName == "m_LocalPosition.y")
                        //         positionKeyFrameData._value.y = curveData.curve.Evaluate(keyframe.time);
                        //     else if (curveData.propertyName == "m_LocalPosition.z")
                        //         positionKeyFrameData._value.z = curveData.curve.Evaluate(keyframe.time);
                        //     AnimationCurve animationCurve = new AnimationCurve();
                        //     animationCurve.AddKey(cameraKeyframeData[index]._time, cameraKeyframeData[index]._value.x);
                        //     animationCurve.AddKey(cameraKeyframeData[index]._time, cameraKeyframeData[index]._value.y);
                        //     animationCurve.AddKey(cameraKeyframeData[index]._time, cameraKeyframeData[index]._value.z);
                        //     cameraKeyframeData.Insert(index, positionKeyFrameData);
                        // }
                    }
                    else
                    {
                        if (curveData.propertyName == "m_LocalPosition.x")
                            positionKeyFrameData._value.x = curveData.curve.Evaluate(keyframe.time);
                        else if (curveData.propertyName == "m_LocalPosition.y")
                            positionKeyFrameData._value.y = curveData.curve.Evaluate(keyframe.time);
                        else if (curveData.propertyName == "m_LocalPosition.z")
                            positionKeyFrameData._value.z = curveData.curve.Evaluate(keyframe.time);
                    }
                }
            }

            foreach (PositionKeyFrameData positionKeyFrameData in cameraKeyframeData)
            {
                Debug.Log($"Name : {positionKeyFrameData._name}, Time : {positionKeyFrameData._time}, Value : {positionKeyFrameData._value}");
            }

            foreach (PositionKeyFrameData positionKeyFrameData in cameraTargetKeyframeData)
            {
                Debug.Log($"Name : {positionKeyFrameData._name}, Time : {positionKeyFrameData._time}, Value : {positionKeyFrameData._value}");
            }

            ParseCameraAnimation(cameraKeyframeData, cameraTargetKeyframeData);
            SaveAnimationClip();
            // foreach (AnimationClipCurveData curveData in curves)
            // {
            //     if (curveData.path.ToLower().Contains("target"))
            //     {
            //         if (curveData.propertyName.ToLower().Contains("position"))
            //         {
            //             Debug.Log($"Property: {curveData.propertyName}, Path: {curveData.path}");
            //             cameraTargetPositionCurveData = curveData;
            //             Keyframe[] keyframes = curveData.curve.keys;
            //
            //             foreach (Keyframe keyframe in keyframes)
            //             {
            //                 Debug.Log($"Time: {keyframe.time}, Value: {keyframe.value}");
            //             }
            //         }
            //     }
            //     else
            //     {
            //
            //     }
            //     Debug.Log($"Property: {curveData.propertyName}, Path: {curveData.path}");
            //
            //     // 獲取曲線上的所有關鍵幀
            //     Keyframe[] keyframes = curveData.curve.keys;
            //
            //     foreach (var keyframe in keyframes)
            //     {
            //         Debug.Log($"Time: {keyframe.time}, Value: {keyframe.value}");
            //     }
            // }
        }

        static void ParseCameraAnimation(List<PositionKeyFrameData> cameraPositionData, List<PositionKeyFrameData> cameraTargetPositionData)
        {
            GameObject cameraGameObject = new GameObject();
            GameObject cameraTargetGameObject = new GameObject();
            foreach (PositionKeyFrameData positionKeyFrameData in cameraPositionData)
            {
                cameraGameObject.transform.position = positionKeyFrameData._value;
                PositionKeyFrameData targetPositionKeyFrameData =
                    cameraTargetPositionData.FirstOrDefault(e => e._time.Equals(positionKeyFrameData._time));
                if (targetPositionKeyFrameData == null)
                {
                    int index = cameraTargetPositionData.FindLastIndex(e => e._time < positionKeyFrameData._time);
                    if (index < 0 || index >= cameraTargetPositionData.Count)
                    {
                        continue;
                    }

                    Vector3        tempPosition   = Vector3.zero;
                    AnimationCurve animationCurveX = new AnimationCurve();
                    animationCurveX.AddKey(cameraTargetPositionData[index]._time,
                                          cameraTargetPositionData[index]._value.x);
                    animationCurveX.AddKey(cameraTargetPositionData[index+1]._time,
                                          cameraTargetPositionData[index+1]._value.x);
                    tempPosition.x = animationCurveX.Evaluate(positionKeyFrameData._time);

                    AnimationCurve animationCurveY = new AnimationCurve();
                    animationCurveY.AddKey(cameraTargetPositionData[index]._time,
                                           cameraTargetPositionData[index]._value.y);
                    animationCurveY.AddKey(cameraTargetPositionData[index+1]._time,
                                           cameraTargetPositionData[index+1]._value.y);
                    tempPosition.y = animationCurveY.Evaluate(positionKeyFrameData._time);

                    AnimationCurve animationCurveZ = new AnimationCurve();
                    animationCurveZ.AddKey(cameraTargetPositionData[index]._time,
                                           cameraTargetPositionData[index]._value.z);
                    animationCurveZ.AddKey(cameraTargetPositionData[index+1]._time,
                                           cameraTargetPositionData[index+1]._value.z);
                    tempPosition.z = animationCurveZ.Evaluate(positionKeyFrameData._time);

                    cameraTargetGameObject.transform.position = tempPosition;
                }
                else
                {
                    cameraTargetGameObject.transform.position = targetPositionKeyFrameData._value;
                }

                cameraGameObject.transform.LookAt(cameraTargetGameObject.transform);

                _posX.AddKey(positionKeyFrameData._time, cameraGameObject.transform.position.x);
                _posY.AddKey(positionKeyFrameData._time, cameraGameObject.transform.position.y);
                _posZ.AddKey(positionKeyFrameData._time, cameraGameObject.transform.position.z);

                // 記錄 Rotation (Quaternion)
                Quaternion rotation = cameraGameObject.transform.rotation;
                _rotX.AddKey(positionKeyFrameData._time, rotation.x);
                _rotY.AddKey(positionKeyFrameData._time, rotation.y);
                _rotZ.AddKey(positionKeyFrameData._time, rotation.z);
                _rotW.AddKey(positionKeyFrameData._time, rotation.w);
            }
            GameObject.DestroyImmediate(cameraGameObject);
            GameObject.DestroyImmediate(cameraTargetGameObject);
		}

        //[MenuItem("GameObject/DEGames/輸出VirtualCamera所屬動畫軌成 Animation Clip", false, -10)]
        static void ExportVirtualCameraTrackToAnimationClip()
        {
            if (!FindNessaryComponents()) return;
            ExportVirtualCameraTrackToAnimationClip(_animationTrack);
        }

        //[MenuItem("GameObject/DEGames/輸出VirtualCamera所屬動畫軌成 FBX", false, -10)]
        static void ExportVirtualCameraTrackToFBX()
        {
            if (!FindNessaryComponents()) return;
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

        static void CopyAnimationClipToAnimationTrack(
            [NotNull] AnimationTrack animationTrack,
            [NotNull] AnimationClip animationClip)
        {
            EditorCurveBinding[] editorCurveBindings = AnimationUtility.GetCurveBindings(animationClip);

            foreach (EditorCurveBinding editorCurveBinding in editorCurveBindings)
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(editorCurveBinding.type))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding);
                    AnimationCurve newCurve = new AnimationCurve(curve.keys);
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
            AnimationClip animationClip = GameObject.Instantiate(animationTrack.infiniteClip);
            if (animationClip == null) return;

            animationClip.legacy = true;
            string assetName = $"{Selection.activeGameObject.name}_TimelineCamera.anim";
            string assetPath = Path.Combine("Assets", assetName);
            AssetDatabase.CreateAsset(animationClip, assetPath);
            AssetDatabase.SaveAssets();
        }

        // static void ExportVirtualCameraTrackToFBX([NotNull] AnimationTrack animationTrack)
        // {
        //     string assetName = $"{Selection.activeGameObject.name}_TimelineCamera.anim";
        //     string assetPath = Path.Combine("Assets", assetName);
        //     AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        //     if (animationClip == null) return;
        //
        //     GameObject gameObject = new GameObject();
        //     Animation animation = gameObject.AddComponent<Animation>();
        //     animation.AddClip(animationClip, $"{Selection.activeGameObject.name}_TimelineCamera");
        //     Debug.Log(animation.GetClipCount());
        //     animation.clip = animationClip;
        //     Debug.Log(animation.GetClipCount());
        //
        //     //ModelExporter exporter = new ModelExporter();
        //     //ExportModelSettingsSerialize exportOptions = new ExportModelSettingsSerialize();
        //     //exportOptions.ModelAnimIncludeOption = ModelAnimIncludeOptions.ExportAnimations;
        //
        //     assetName = $"{Selection.activeGameObject.name}_TimelineCamera.fbx";
        //     assetPath = Path.Combine("Assets", assetName);
        //     ModelExporter.ExportObject(assetPath, gameObject);
        //     GameObject.DestroyImmediate(gameObject);
        // }

        static void ParseCameraAnimation([NotNull] AnimationClip animationClip)
        {
            string[]   assetGUIDs          = AssetDatabase.FindAssets(animationClip.name);
            GameObject animationGameObject = null;
            if (assetGUIDs.Length != 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);
                animationGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
            float timer = 0f;
            AnimationClipCurveData[] curves = AnimationUtility.GetAllCurves(animationClip, true);

            foreach (AnimationClipCurveData curveData in curves)
            {
                if (!curveData.propertyName.ToLower().Contains("position"))
                {
                    continue;
                }

                if (curveData.path.ToLower().Contains("target"))
                {
                    continue;
                }

                Keyframe[] keyframes = curveData.curve.keys;
                foreach (Keyframe keyframe in keyframes)
                {
                    animationClip.SampleAnimation(animationGameObject, keyframe.time);
                    CameraTransformAnimation(animationGameObject.transform, timer);
                }
            }

            // while (timer < animationClip.length)
            // {
            //     animationClip.SampleAnimation(animationGameObject, timer);
            //     CameraTransformAnimation(animationGameObject.transform, timer);
            //     //PrintAnimationClipInfo(animationGameObject.transform);
            //     timer += 1.0f / (float)_timelineAsset.editorSettings.frameRate;
            // }
            SaveAnimationClip();
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
            Transform cameraTransform = cameraAnimationRoot.GetChild(0);
            Transform cameraTargetTransform = cameraAnimationRoot.GetChild(1);

            _recordGameObject.transform.position = cameraTransform.position;
            _recordGameObject.transform.LookAt(cameraTargetTransform);

            _posX.AddKey(time, _recordGameObject.transform.position.x);
            _posY.AddKey(time, _recordGameObject.transform.position.y);
            _posZ.AddKey(time, _recordGameObject.transform.position.z);

            // 記錄 Rotation (Quaternion)
            Quaternion rotation = _recordGameObject.transform.rotation;
            _rotX.AddKey(time, rotation.x);
            _rotY.AddKey(time, rotation.y);
            _rotZ.AddKey(time, rotation.z);
            _rotW.AddKey(time, rotation.w);
        }

        static void CreateAnimationClip(float frameRate)
        {
            if (_recordGameObject != null) {
                GameObject.DestroyImmediate(_recordGameObject);
            }
            if (_recordAnimationClip != null) {
                GameObject.DestroyImmediate(_recordAnimationClip);
            }
            _recordGameObject = new GameObject();
            _recordAnimationClip = new AnimationClip();
            _recordAnimationClip.frameRate = frameRate;

            // 初始化曲線
            _posX = new AnimationCurve();
            _posY = new AnimationCurve();
            _posZ = new AnimationCurve();

            _rotX = new AnimationCurve();
            _rotY = new AnimationCurve();
            _rotZ = new AnimationCurve();
            _rotW = new AnimationCurve();
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
    }
}
//#endif
