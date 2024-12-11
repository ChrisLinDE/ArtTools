using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;

// Original: https://techblog.sega.jp/entry/2016/11/28/100000
namespace GameAssets.ArtTools.Editor {
    public class KeyframeReduction {// : AssetPostprocessor {
        //[MenuItem("Assets/KeyframeReduction")]
        static void Run() {
            Debug.Log("Keyframe Reduction...");
            foreach (AnimationClip clip in Selection.GetFiltered<AnimationClip>(SelectionMode.Editable)) {
                string path = AssetDatabase.GetAssetPath(clip);

                AnimationClip        ac          = new AnimationClip();
                EditorCurveBinding[] allBindings = AnimationUtility.GetCurveBindings(clip);
                for (int i = 0; i < allBindings.Length; i++) {
                    EditorCurveBinding binding = allBindings[i];
                    Debug.Log($"{i + 1}/{allBindings.Length}: {binding.path}");
                    int  dotLoc   = binding.propertyName.LastIndexOf('.');
                    bool isVector = dotLoc != -1 && dotLoc == binding.propertyName.Length - 2;
                    if (!isVector) {
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        curve = Reduction(new AnimationCurve[] { curve })[0];
                        ac.SetCurve(binding.path, binding.type, binding.propertyName, curve);
                    }
                    else {
                        List<EditorCurveBinding> bindings = new List<EditorCurveBinding>();
                        bindings.Add(binding);
                        string pp = binding.propertyName.Substring(0, binding.propertyName.Length - 2);
                        while (true) {
                            i++;
                            if (i >= allBindings.Length) break;
                            EditorCurveBinding b = allBindings[i];
                            if (binding.path == b.path &&
                                pp == b.propertyName.Substring(0, b.propertyName.Length - 2)) {
                                bindings.Add(b);
                            }
                            else {
                                break;
                            }
                        }

                        AnimationCurve[] curves = new AnimationCurve[bindings.Count];
                        for (int j = 0; j < bindings.Count; j++) {
                            curves[j] = AnimationUtility.GetEditorCurve(clip, bindings[j]);
                        }

                        curves = Reduction(curves);
                        for (int j = 0; j < bindings.Count; j++) {
                            EditorCurveBinding b = bindings[j];
                            ac.SetCurve(b.path, b.type, b.propertyName, curves[j]);
                        }

                        i--;
                    }
                }

                string newPath = $"{Path.GetDirectoryName(path)}/{Path.GetFileNameWithoutExtension(path)}_reduced.anim";
                Write(newPath, ac);
            }
        }

        static private Vector4 Build(float x0, float p0, float g0, float x1, float p1, float g1) {
            float dt = x1 - x0;
            float m0 = g0 * dt;
            float m1 = g1 * dt;

            Vector4 t0 = new Vector4(1, 0, 0, 0);
            Vector4 t1 = new Vector4(0, 1, 0, 0);
            Vector4 t2 = new Vector4(0, 0, 1, 0);
            Vector4 t3 = new Vector4(0, 0, 0, 1);

            Vector4 a = 2 * t3 - 3 * t2 + 1 * t0;
            Vector4 b = t3 - 2 * t2 + t1;
            Vector4 c = t3 - t2;
            Vector4 d = -2 * t3 + 3 * t2;

            Vector4 v = a * p0 + b * m0 + c * m1 + d * p1;
            return v;
        }

        static private Vector2 Integrate(float x0, float x1, Vector4 w, float sol) {
            float   dt    = x1 - x0;
            Vector4 ints  = new Vector4(1, 1 / 2.0f, 1 / 3.0f, 1 / 4.0f);
            float   area1 = Vector4.Dot(w, ints) * dt;
            if (w[3] == 0) {
                // quadratic: no sign change
                if (w[2] > 0) {
                    return new Vector4(area1, 0);
                }
                else {
                    return new Vector4(0, -area1);
                }
            }
            else {
                Vector4 w0 = w / w[3];
                // form of (x - sol)^2 * (x - X)
                float e = -w0[2] - 2 * sol;
                if (0 < e && e < 1) {
                    float   x    = Mathf.Lerp(x0, x1, e);
                    Vector2 area = Vector2.zero;
                    Vector4 ets  = ints;
                    ets.Scale(new Vector4(e, e * e, e * e * e, e * e * e * e));
                    float areaE = Vector4.Dot(w, ets) * e * dt;

                    float p, g;
                    SampleCurve(x0, x1, w, x, out p, out g);
                    if (g > 0) {
                        return new Vector2(area1 - areaE, -areaE);
                    }
                    else {
                        return new Vector2(areaE, -area1 + areaE);
                    }
                }
                else {
                    float x = (x0 + x1) / 2;
                    float p, g;
                    SampleCurve(x0, x1, w, x, out p, out g);
                    if (p > 0) {
                        return new Vector2(area1, 0);
                    }
                    else {
                        return new Vector2(0, -area1);
                    }
                }
            }
        }

        static private void Range(float x0, float x1, Vector4 w, ref float minRange, ref float maxRange) {
            float dt = x1 - x0;

            // Differentiate

            List<float> tt = new List<float>();
            tt.Add(0);
            tt.Add(1);

            float qA = w[3] * 3; // 3t^2
            float qB = w[2] * 2; // 2t
            float qC = w[1] * 1; // t
            // At^2 + Bt + C = 0

            float D = qB * qB - 4 * qA * qC;
            if (D >= 0) {
                tt.Add((-qB + Mathf.Sqrt(D)) / (2 * qA));
                tt.Add((-qB - Mathf.Sqrt(D)) / (2 * qA));
            }

            foreach (float t in tt) {
                if (t < 0 || t > 1) continue;
                Vector4 ts                  = new Vector4(1, t, t * t, t * t * t);
                float   vv                  = Vector4.Dot(w, ts);
                if (vv < minRange) minRange = vv;
                if (vv > maxRange) maxRange = vv;
            }
        }

        static private void SampleCurve(float x0, float x1, Vector4 w, float x, out float p, out float g) {
            float dt = x1 - x0;
            float t  = (x - x0) / (x1 - x0);

            float t0 = 1;
            float t1 = t;
            float t2 = t * t;
            float t3 = t * t * t;
            p = Vector4.Dot(w, new Vector4(t0, t1, t2, t3));

            t0 = 0;
            t1 = 1;
            t2 = 2 * t;
            t3 = 3 * t * t;

            g =  Vector4.Dot(w, new Vector4(t0, t1, t2, t3));
            g /= dt;
        }

        static private bool OutOfTolerance(Keyframe k0, Keyframe k1, Keyframe k2) {
            float x0  = k0.time;
            float p0  = k0.value;
            float g0  = k0.outTangent;
            float x1  = k1.time;
            float p1  = k1.value;
            float g1i = k1.inTangent;
            float g1o = k1.outTangent;
            float x2  = k2.time;
            float p2  = k2.value;
            float g2  = k2.inTangent;
            x1 -= x0;
            x2 -= x0;
            x0 =  0;
            p1 -= p0;
            p2 -= p0;
            p0 =  0;

            Vector4 w01 = Build(x0, p0, g0, x1, p1, g1i);
            Vector4 w12 = Build(x1, p1, g1o, x2, p2, g2);
            Vector4 w02 = Build(x0, p0, g0, x2, p2, g2);

            float p1m;
            float g1m;
            SampleCurve(x0, x2, w02, x1, out p1m, out g1m);

            Vector4 w01m = Build(x0, p0, g0, x1, p1m, g1m);
            Vector4 w12m = Build(x1, p1m, g1m, x2, p2, g2);

            float minRange = 0, maxRange = 0;
            Range(x0, x1, w01m - w01, ref minRange, ref maxRange);
            Range(x1, x2, w12m - w12, ref minRange, ref maxRange);

            float dt = x2 - x0;

            Vector2 area = Vector2.zero;
            area += Integrate(x0, x1, w01m - w01, 0);
            area += Integrate(x1, x2, w12m - w12, 1);

            float absError = (area.x + area.y) / dt;
            float relError = absError / (maxRange - minRange);

            bool outOfTorelance = absError > 5.0f && relError > 0.25f;
            return outOfTorelance;
        }

        static public AnimationCurve[] Reduction(AnimationCurve[] curves) {
            // forall (curve : curves) curve.length == curves[0].length
            // forall (i : curves[0].keys.length) foreall (curve : curves) curve.keys[i].time == curves[0].keys[i].time
            if (curves[0].length <= 2) return curves;

            List<int> pickIndices = new List<int>();
            pickIndices.Add(0);
            int lastPick = 0;
            for (int i = 1; i < curves[0].length - 1; i++) {
                bool pick = false;
                foreach (AnimationCurve curve in curves) {
                    if (OutOfTolerance(curve.keys[lastPick], curve.keys[i], curve.keys[i + 1])) {
                        pick = true;
                        break;
                    }
                }

                if (pick) {
                    pickIndices.Add(i);
                    lastPick = i;
                }
            }

            pickIndices.Add(curves[0].length - 1);

            Debug.Log($"Pick {pickIndices.Count}/{curves[0].length}");

            AnimationCurve[] cs = new AnimationCurve[curves.Length];
            for (int i = 0; i < cs.Length; i++) {
                cs[i] = new AnimationCurve();
                foreach (int ix in pickIndices) {
                    cs[i].AddKey(curves[i].keys[ix]);
                }
            }

            return cs;
        }

        static private void Write(string rawPath, AnimationClip clip) {
            string path = AssetDatabase.GenerateUniqueAssetPath(rawPath);
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
