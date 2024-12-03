using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DEGames.ArtTools.Editor
{
    public class CameraAnimationClipToTimelineWindow : EditorWindow {

        List<AnimationClip> _refClips;
        SerializedObject    _obj;
        SerializedProperty  _refClipsProperty;

        [MenuItem("DEGames/Camera Animation To Timeline Window")]
        public static void CameraAnimationClipToTimeline()
        {
            CameraAnimationClipToTimelineWindow window =
                GetWindow<CameraAnimationClipToTimelineWindow>("Camera Animation To Timeline");
            window.Show();
        }

        void OnEnable()
        {
            _obj = new SerializedObject(this);
            _refClipsProperty = _obj.FindProperty("_refClips");
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

            if (GUILayout.Button(("+")))
            {
                if (_refClipsProperty.isArray) {
                    _refClipsProperty.arraySize++;
                }
            }
        }
    }
}
