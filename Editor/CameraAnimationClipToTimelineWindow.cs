using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameAssets.ArtTools.Editor
{
    [Serializable]
    public class CameraAnimationData
    {
        public int           frame;
        public AnimationClip clip;
    }

    public class CameraAnimationClipToTimelineWindow : EditorWindow {
        public float                     keyframeThreshold = 0.01f;
        public float                     rotationError     = 0.5f;
        public float                     positionError     = 0.5f;
        public float                     scaleError        = 0.5f;
        public bool                      isClear           = true;
        public bool                      isKeyframe        = true;
        public GameObject                characterPrefab;
        public List<CameraAnimationData> refAnimationDataList;
        SerializedObject                 _obj;
        SerializedProperty               _characterProperty;
        SerializedProperty               _refAnimationDataListProperty;
        SerializedProperty               _isClearProperty;
        SerializedProperty               _isKeyframeProperty;
        SerializedProperty               _keyframeThresholdProperty;
        SerializedProperty               _rotationErrorProperty;
        SerializedProperty               _positionErrorProperty;
        SerializedProperty               _scaleErrorProperty;

        [MenuItem("DEGames/攝影機動畫匯入Timeline工具")]
        public static void InvokeCameraAnimationClipToTimelineWindow()
        {
            CameraAnimationClipToTimelineWindow window =
                GetWindow<CameraAnimationClipToTimelineWindow>("攝影機動畫匯入Timeline工具");
            window.Show();
        }

        void OnEnable()
        {
            _obj                          = new SerializedObject(this);
            _characterProperty            = _obj.FindProperty("characterPrefab");
            _refAnimationDataListProperty = _obj.FindProperty("refAnimationDataList");
            _isClearProperty              = _obj.FindProperty("isClear");
            _isKeyframeProperty           = _obj.FindProperty("isKeyframe");
            _keyframeThresholdProperty    = _obj.FindProperty("keyframeThreshold");
            _rotationErrorProperty        = _obj.FindProperty("rotationError");
            _positionErrorProperty        = _obj.FindProperty("positionError");
            _scaleErrorProperty           = _obj.FindProperty("scaleError");
        }

        void OnDisable()
        {
        }

        void OnGUI()
        {
            if (_obj.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            EditorGUILayout.ObjectField(_characterProperty, new GUIContent("指定要匯入的角色"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_refAnimationDataListProperty, new GUIContent("設定要匯入的動畫資料"), true);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_isKeyframeProperty, new GUIContent("計算關鍵幀"));
            if (isKeyframe) {
                EditorGUI.indentLevel++;
                //EditorGUILayout.PropertyField(_keyframeThresholdProperty, new GUIContent("關鍵幀閥值"));
                EditorGUILayout.PropertyField(_rotationErrorProperty, new GUIContent("旋轉容錯閥值"));
                EditorGUILayout.PropertyField(_positionErrorProperty, new GUIContent("位移容錯閥值"));
                EditorGUILayout.PropertyField(_scaleErrorProperty, new GUIContent("縮放容錯閥值"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_isClearProperty, new GUIContent("匯入前清除攝影機動畫"));

            _obj.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("滙入")) {
                if (characterPrefab == null) {
                    EditorUtility.DisplayDialog("警告！", "沒有設定角色物件", "確定");
                    return;
                }

                if (refAnimationDataList == null || refAnimationDataList.Count == 0) {
                    EditorUtility.DisplayDialog("警告！", "沒有設定動畫檔", "確定");
                    return;
                }

                CameraAnimationClipToTimeline.ImportCameraAnimationToTimeline(characterPrefab,
                                                                              refAnimationDataList,
                                                                              isClear,
                                                                              isKeyframe,
                                                                              keyframeThreshold,
                                                                              rotationError,
                                                                              positionError,
                                                                              scaleError);
            }
        }
    }
}
