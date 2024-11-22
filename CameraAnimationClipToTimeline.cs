using System.IO;
using Cinemachine;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
//using UnityEditor.Formats.Fbx.Exporter;

namespace DEGames
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
        // static AnimationCurve _scaleX = null;
        // static AnimationCurve _scaleY = null;
        // static AnimationCurve _scaleZ = null;

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
            ExportVirtualCameraTrackToFBX(_animationTrack);
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
                    Debug.Log(animationTrack.inClipMode);
                    if (bindingObject.gameObject != gameObject) continue;
                    Debug.Log(bindingObject.name);
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
                    Debug.Log(
                        $"{editorCurveBinding.path} | {editorCurveBinding.propertyName} | {editorCurveBinding.type} |");
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
            if (assetGUIDs.Length != 0) {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);
                animationGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
            float timer = 0f;
            while (timer < animationClip.length) {
                animationClip.SampleAnimation(animationGameObject, timer);
                CameraTransformAnimation(animationGameObject.transform, timer);
                PrintAnimationClipInfo(animationGameObject.transform);
                timer += 1.0f / 60.0f;
            }
            SaveAnimationClip();
        }

        static void PrintAnimationClipInfo([NotNull] Transform transform) {
            Debug.Log($"Object : {transform.name} : {transform.position} : {transform.rotation.eulerAngles}");
            foreach (Transform tr in transform.transform) {
                PrintAnimationClipInfo(tr);
            }
        }

        static void CameraTransformAnimation(Transform cameraAnimationRoot, float time)
        {
            Transform cameraTransform = cameraAnimationRoot.GetChild(0);
            Transform cameraTargetTransform = cameraAnimationRoot.GetChild(1);

            _recordGameObject.transform.position = cameraTransform.position;
            //_recordGameObject.transform.rotation = cameraTransform.rotation;
            _recordGameObject.transform.LookAt(cameraTargetTransform);
            //Debug.Log($"Object : {_recordGameObject.name} : {_recordGameObject.transform.position} : {_recordGameObject.transform.rotation.eulerAngles}");

            _posX.AddKey(time, _recordGameObject.transform.position.x);
            _posY.AddKey(time, _recordGameObject.transform.position.y);
            _posZ.AddKey(time, _recordGameObject.transform.position.z);

            // 記錄 Rotation (Quaternion)
            Quaternion rotation = _recordGameObject.transform.rotation;
            _rotX.AddKey(time, rotation.x);
            _rotY.AddKey(time, rotation.y);
            _rotZ.AddKey(time, rotation.z);
            _rotW.AddKey(time, rotation.w);

            // 記錄 Scale
            // _scaleX.AddKey(time, _recordGameObject.transform.localScale.x);
            // _scaleY.AddKey(time, _recordGameObject.transform.localScale.y);
            // _scaleZ.AddKey(time, _recordGameObject.transform.localScale.z);
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

            // _scaleX = new AnimationCurve();
            // _scaleY = new AnimationCurve();
            // _scaleZ = new AnimationCurve();
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

            //_recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalScale.x", _scaleX);
            //_recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalScale.y", _scaleY);
            //_recordAnimationClip.SetCurve("", typeof(Transform), "m_LocalScale.z", _scaleZ);
        }
    }
}
