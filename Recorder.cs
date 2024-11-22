using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

#if UNITY_RECORDER
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif

namespace DEGames
{
    public class Recorder
    {
        static object _recorderController     = null;
        static Type   _recorderControllerType = null;
        static int    _outputWidth            = 1280;
        static int    _outputHeight           = 720;
        static string _folderName             = "../SampleRecordings";
        static string _fileName               = "";
        static bool   _isRecording            = false;
        
        [MenuItem("DEGames/Recorder/Start Game View Recorder", validate = false)]
        public static void StartRecording()
        {
            if (_recorderController == null)
            {
                PrepareRecoderController();
            }

            _isRecording = false;

            MethodInfo prepareRecordingMethod =
                _recorderControllerType.GetMethod("PrepareRecording", 
                                                  BindingFlags.Instance | BindingFlags.Public);
            MethodInfo startRecordingMethod =
                _recorderControllerType.GetMethod("StartRecording", 
                                                  BindingFlags.Instance | BindingFlags.Public);
            if (startRecordingMethod == null || prepareRecordingMethod == null) return;
            prepareRecordingMethod.Invoke(_recorderController, null);
            startRecordingMethod.Invoke(_recorderController, null);
            _isRecording = true;
        }

        [MenuItem("DEGames/Recorder/Start Game View Recorder", validate = true)]
        public static bool ValidateStartRecording()
        {
            return Application.isPlaying && !_isRecording;
        }

        [MenuItem("DEGames/Recorder/Stop Game View Recorder", validate = false)]
        public static void StopRecording()
        {
            if (_recorderController == null) return;

            MethodInfo stopRecordingMethod =
                _recorderControllerType.GetMethod("StopRecording", 
                                                  BindingFlags.Instance | BindingFlags.Public);
            if (stopRecordingMethod != null)
            {
                stopRecordingMethod.Invoke(_recorderController, null);
            }
            _recorderControllerType = null;
            _recorderController     = null;
            _isRecording            = false;
        }
        
        [MenuItem("DEGames/Recorder/Stop Game View Recorder", validate = true)]
        public static bool ValidateStopRecording()
        {
            return Application.isPlaying && _isRecording;
        }

        public static int outputWidth
        {
            get => _outputWidth;
            set => _outputWidth = value;
        }
        
        public static int outputHeight
        {
            get => _outputHeight;
            set => _outputHeight = value;
        }
        
        public static string fileName
        {
            get => _fileName;
            set => _fileName = value;
        }
        
        public static string folderName
        {
            get => _folderName;
            set => _folderName = value;
        }
        
        static void PrepareRecoderController()
        {
            Type recorderWindowType = Type.GetType("UnityEditor.Recorder.RecorderWindow, Unity.Recorder.Editor");
            if (recorderWindowType == null)
            {
                Debug.LogError("Unity Recorder 未安裝或 Recorder 類不可用！");
                return;
            }

            _recorderControllerType = Type.GetType("UnityEditor.Recorder.RecorderController, Unity.Recorder.Editor");
            if (_recorderControllerType == null)
            {
                Debug.LogError("Recorder Controller 類不可用");
                return;
            }

            Type recorderControllerSettingsType =
                Type.GetType("UnityEditor.Recorder.RecorderControllerSettings, Unity.Recorder.Editor");
            if (recorderControllerSettingsType == null)
            {
                Debug.LogError("RecorderControllerSettings 類不可用");
                return;
            }

            object recorderControllerSettings =
                recorderControllerSettingsType.GetConstructor(Type.EmptyTypes)!.Invoke(null);
            if (recorderControllerSettings == null)
            {
                Debug.LogError("無法創建 RecorderControllerSettings");
                return;
            }

            _recorderController =
                Activator.CreateInstance(_recorderControllerType, recorderControllerSettings);
            if (_recorderController == null)
            {
                Debug.LogError("無法創建 RecorderController");
                return;
            }

            // 創建錄製設定
            Type settingsType      = Type.GetType("UnityEditor.Recorder.RecorderSettings, Unity.Recorder.Editor");
            Type movieRecorderType = Type.GetType("UnityEditor.Recorder.MovieRecorderSettings, Unity.Recorder.Editor");
            if (settingsType == null || movieRecorderType == null)
            {
                Debug.LogError("Recorder 設定類型不可用！");
                return;
            }

            // 使用反射實例化 MovieRecorderSettings
            ScriptableObject movieRecorderSettings = ScriptableObject.CreateInstance(movieRecorderType);
            if (movieRecorderSettings == null)
            {
                Debug.LogError("無法創建 MovieRecorderSettings！");
                return;
            }

            // 設置錄製參數
            PropertyInfo outputFileProp =
                movieRecorderType.GetProperty("OutputFile", BindingFlags.Instance | BindingFlags.Public);
            if (outputFileProp != null)
            {
                string mediaOutputFolder = Application.dataPath + folderName;
                string outputName        = fileName;
                if (string.IsNullOrEmpty(fileName))
                {
                    outputName =
                        $"Recording_{DateTime.Now.ToString("yyyyMMddHHmmss")}";
                }
                outputFileProp.SetValue(movieRecorderSettings, $"{mediaOutputFolder}/{outputName}", null);
            }

            PropertyInfo nameProp =
                movieRecorderType.GetProperty("name", BindingFlags.Instance | BindingFlags.Public);
            if (nameProp != null)
            {
                nameProp.SetValue(movieRecorderSettings, "GameView Recorder", null);
            }

            PropertyInfo enableProp =
                movieRecorderType.GetProperty("Enabled", BindingFlags.Instance | BindingFlags.Public);
            if (enableProp != null)
            {
                enableProp.SetValue(movieRecorderSettings, true, null);
            }

            PropertyInfo outputFormatProp =
                movieRecorderType.GetProperty("OutputFormat", BindingFlags.Instance | BindingFlags.Public);
            if (outputFormatProp != null)
            {
                outputFormatProp.SetValue(movieRecorderSettings, 0, null);
            }

            PropertyInfo videoRecorderSettingsProp =
                movieRecorderType.GetProperty("VideoBitRateMode",
                                              BindingFlags.Instance | BindingFlags.Public);
            if (videoRecorderSettingsProp != null)
            {
                videoRecorderSettingsProp.SetValue(movieRecorderSettings, VideoBitrateMode.High, null);
            }

            PropertyInfo imageInputSettingsProp =
                movieRecorderType.GetProperty("ImageInputSettings",
                                              BindingFlags.Instance | BindingFlags.Public);
            if (imageInputSettingsProp != null)
            {
                Type gameViewInputSettingsType =
                    Type.GetType("UnityEditor.Recorder.Input.GameViewInputSettings, Unity.Recorder.Editor");
                if (gameViewInputSettingsType != null)
                {
                    object gameViewInputSettings =
                        gameViewInputSettingsType.GetConstructor(Type.EmptyTypes)!.Invoke(null);
                    if (gameViewInputSettings != null)
                    {
                        PropertyInfo outputWidthProp =
                            gameViewInputSettingsType.GetProperty("OutputWidth",
                                                                  BindingFlags.Instance | BindingFlags.Public);
                        if (outputWidthProp != null)
                        {
                            outputWidthProp.SetValue(gameViewInputSettings, _outputWidth, null);
                        }

                        PropertyInfo outputHeightProp =
                            gameViewInputSettingsType.GetProperty("OutputHeight",
                                                                  BindingFlags.Instance | BindingFlags.Public);
                        if (outputHeightProp != null)
                        {
                            outputHeightProp.SetValue(gameViewInputSettings, _outputHeight, null);
                        }

                        imageInputSettingsProp.SetValue(movieRecorderSettings, gameViewInputSettings, null);
                    }
                }
            }

            MethodInfo addRecorderSettingsMethod =
                recorderControllerSettingsType.GetMethod("AddRecorderSettings",
                                                         BindingFlags.Instance | BindingFlags.Public);
            MethodInfo setRecordModeToManualMethod =
                recorderControllerSettingsType.GetMethod("SetRecordModeToManual",
                                                         BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo settingsProp =
                _recorderControllerType.GetProperty("Settings",
                                                    BindingFlags.Instance | BindingFlags.Public);
            if (addRecorderSettingsMethod != null && settingsProp != null && setRecordModeToManualMethod != null)
            {
                addRecorderSettingsMethod.Invoke(settingsProp.GetValue(_recorderController), 
                                                 new object[] { movieRecorderSettings });
                setRecordModeToManualMethod.Invoke(settingsProp.GetValue(_recorderController), null);
            }
        }
    }
}