using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering; // TierSettings
using UnityEngine.Rendering;

public class VketCloudSDKWizard : EditorWindow
{
    // ------------------------------------------------------------------
    // 定数
    // ------------------------------------------------------------------
    private const string RegistryName = "Vket Cloud SDK Install Wizard";
    private const string RegistryURL = "https://registry.npmjs.com";
    private const string RegistryScope = "com.hikky.vketcloudsdk-install-wizard";

    private const string PackageName = "com.hikky.vketcloudsdk-install-wizard";
    private const string RequiredPackageVersion = "1.0.0";

    // 追加パッケージ（自動追加）
    private const string DeepLinkName = "com.needle.deeplink";
    private const string DeepLinkURL = "https://github.com/needle-tools/unity-deeplink.git?path=/package";

    private const string RequiredUnityVersionDisplay = "Unity 6.0.0f1 以上";

    // ------------------------------------------------------------------
    // ステップ管理
    // ------------------------------------------------------------------
    private int step = 0;

    private bool unityVersionOK = false;
    private bool registryOK = false;
    private bool packageOK = false;

    private bool manifestLoadFailed = false;
    private bool unityWarningShown = false;

    private string manifestPath;
    private JObject manifestJson;

    // GUI
    private GUIStyle titleStyle;
    private GUIStyle boxStyle;
    private GUIStyle stepLabelStyle;
    private GUIStyle buttonPrimary;
    private GUIStyle buttonSecondary;
    private GUIStyle badgeOK;
    private GUIStyle badgeNG;

    private Texture2D iconCheck;
    private Texture2D iconWarning;

    private bool guiInitialized = false;

    private int spinnerIndex = 0;
    private double lastSpinnerTime = 0f;

    private bool completeAnimPlaying = false;
    private double completeAnimStartTime = 0f;

    // ------------------------------------------------------------------
    // メニュー
    // ------------------------------------------------------------------
    [MenuItem("Vket Cloud/Install Wizard")]
    public static void OpenWindow()
    {
        var window = GetWindow<VketCloudSDKWizard>("Vket Cloud SDK Wizard");
        window.minSize = new Vector2(520, 520);
    }

    // ------------------------------------------------------------------
    private void OnEnable()
    {
        manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
        LoadManifestJson();

        if (!IsUnity6OrNewer() && !unityWarningShown)
        {
            unityWarningShown = true;
            ShowError("このウィザードは Unity 6 以降でのみサポートされています。\n現在の Unity: " + Application.unityVersion);
        }
    }

    // ------------------------------------------------------------------
    private void LoadManifestJson()
    {
        try
        {
            if (!File.Exists(manifestPath))
            {
                manifestLoadFailed = true;
                ShowError("manifest.json が見つかりませんでした。");
                return;
            }

            manifestJson = JObject.Parse(File.ReadAllText(manifestPath));
            manifestLoadFailed = false;
        }
        catch (Exception ex)
        {
            manifestLoadFailed = true;
            ShowError("manifest.json の読み込みエラー:\n" + ex.Message);
        }
    }

    // ------------------------------------------------------------------
    private void InitGUI()
    {
        titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        boxStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(16, 16, 16, 16),
            margin = new RectOffset(10, 10, 10, 10)
        };

        stepLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16
        };

        buttonPrimary = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        buttonPrimary.normal.background = MakeTex(4, 4, new Color(0.35f, 0.45f, 1f));
        buttonPrimary.hover.background = MakeTex(4, 4, new Color(0.45f, 0.55f, 1f));

        buttonSecondary = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
        };

        badgeOK = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.15f, 0.65f, 0.2f) }
        };

        badgeNG = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.9f, 0.2f, 0.2f) }
        };

        iconCheck = EditorGUIUtility.IconContent("TestPassed").image as Texture2D
            ?? EditorGUIUtility.IconContent("Collab.Check").image as Texture2D;

        iconWarning = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D;
    }

    private Texture2D MakeTex(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h);
        tex.SetPixels(Enumerable.Repeat(c, w * h).ToArray());
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------
    private void OnGUI()
    {
        if (!guiInitialized)
        {
            InitGUI();
            guiInitialized = true;
        }

        try
        {
            DrawHeader();
            GUILayout.Space(10);

            GUILayout.BeginVertical(boxStyle);

            if (manifestLoadFailed)
            {
                EditorGUILayout.HelpBox("manifest.json を読み込めません。", MessageType.Error);
            }
            else
            {
                switch (step)
                {
                    case 0: DrawStep1_UnityCheck(); break;
                    case 1: DrawStep2_Registry(); break;
                    case 2: DrawStep3_Package(); break;
                    case 3: DrawStep4_Finish(); break;
                }
            }

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            DrawStepButtons();
        }
        catch (Exception ex)
        {
            EditorGUILayout.HelpBox("GUI Error: " + ex.Message, MessageType.Error);
        }

        if (completeAnimPlaying || step < 3)
            Repaint();
    }

    // ------------------------------------------------------------------
    private void DrawHeader()
    {
        Rect rect = GUILayoutUtility.GetRect(0, 40);
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.45f, 1f));
        GUI.Label(rect, "Vket Cloud SDK Install Wizard", titleStyle);

        if (step < 3)
        {
            Rect r = new Rect(rect.xMax - 32, rect.y + 8, 24, 24);
            DrawSpinner(r);
        }
    }

    private void DrawSpinner(Rect rect)
    {
        double t = EditorApplication.timeSinceStartup;
        if (t - lastSpinnerTime > 0.08)
        {
            lastSpinnerTime = t;
            spinnerIndex = (spinnerIndex + 1) % 12;
        }

        GUI.DrawTexture(
            rect,
            EditorGUIUtility.IconContent($"WaitSpin{spinnerIndex:00}").image,
            ScaleMode.ScaleToFit
        );
    }

    // ------------------------------------------------------------------
    private void DrawStep1_UnityCheck()
    {
        GUILayout.Label("Step 1 / 4 : Unity Version Check", stepLabelStyle);
        GUILayout.Space(6);

        string current = Application.unityVersion;
        unityVersionOK = IsUnity6OrNewer();

        EditorGUILayout.LabelField("Current Unity Version", current);
        EditorGUILayout.LabelField("Required Version", RequiredUnityVersionDisplay);

        GUILayout.Space(8);

        if (unityVersionOK)
            EditorGUILayout.LabelField("✔ Unity バージョンは要件を満たしています", badgeOK);
        else
            EditorGUILayout.LabelField("⚠ Unity 6.0.0f1 以上が必要です", badgeNG);
    }

    private bool IsUnity6OrNewer()
    {
        string v = Application.unityVersion;
        return v.StartsWith("6000.") || v.StartsWith("6.0.");
    }

    // ------------------------------------------------------------------
    private void DrawStep2_Registry()
    {
        GUILayout.Label("Step 2 / 4 : Scoped Registry", stepLabelStyle);
        GUILayout.Space(6);

        var scoped = manifestJson["scopedRegistries"] as JArray ?? new JArray();
        registryOK = scoped.Any(r => r["name"]?.ToString() == RegistryName);

        EditorGUILayout.LabelField("Name", RegistryName);
        EditorGUILayout.LabelField("URL", RegistryURL);
        EditorGUILayout.LabelField("Scope", RegistryScope);

        GUILayout.Space(8);

        if (registryOK)
        {
            EditorGUILayout.LabelField("✔ Scoped Registry は登録済みです", badgeOK);
        }
        else
        {
            EditorGUILayout.LabelField("⚠ Scoped Registry が見つかりません", badgeNG);
            GUILayout.Space(8);

            if (GUILayout.Button("Scoped Registry を追加する", buttonPrimary, GUILayout.Height(32)))
            {
                try
                {
                    var reg = new JObject
                    {
                        ["name"] = RegistryName,
                        ["url"] = RegistryURL,
                        ["scopes"] = new JArray(RegistryScope)
                    };

                    scoped.Add(reg);
                    manifestJson["scopedRegistries"] = scoped;
                    File.WriteAllText(manifestPath, manifestJson.ToString());

                    // ★ DeepLink パッケージも追加
                    var deps = manifestJson["dependencies"] as JObject;
                    if (deps != null)
                    {
                        if (deps[DeepLinkName] == null)
                        {
                            deps[DeepLinkName] = DeepLinkURL;
                            File.WriteAllText(manifestPath, manifestJson.ToString());
                        }
                    }

                    AssetDatabase.Refresh();
                    registryOK = true;

                    // 再起動確認
                    bool restart = EditorUtility.DisplayDialog(
                        "Unity を再起動しますか？",
                        "Scoped Registry と DeepLink を追加しました。\n推奨プロジェクト設定を適用して再起動します。\n\nUnity を再起動しますか？",
                        "再起動する",
                        "キャンセル"
                    );

                    if (restart)
                    {
                        ApplyProjectSettingsBeforeRestart();
                        RestartUnity();
                    }
                }
                catch (Exception ex)
                {
                    ShowError("Scoped Registry の追加エラー:\n" + ex.Message);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    private void DrawStep3_Package()
    {
        GUILayout.Label("Step 3 / 4 : Package Install / Update", stepLabelStyle);
        GUILayout.Space(6);

        var deps = manifestJson["dependencies"] as JObject;

        string installed = deps?[PackageName]?.ToString();
        packageOK = installed != null &&
                    ComparePackageVersion(installed, RequiredPackageVersion) >= 0;

        EditorGUILayout.LabelField("Package", PackageName);
        EditorGUILayout.LabelField("Required", RequiredPackageVersion);
        EditorGUILayout.LabelField("Installed", installed ?? "(not installed)");

        GUILayout.Space(8);

        if (packageOK)
        {
            EditorGUILayout.LabelField("✔ SDK は必要バージョン以上です", badgeOK);
        }
        else
        {
            EditorGUILayout.LabelField("⚠ SDK が未インストール or 古いです", badgeNG);

            GUILayout.Space(8);

            if (GUILayout.Button(installed == null ? "SDK をインストール" : "SDK を更新", buttonPrimary, GUILayout.Height(32)))
            {
                try
                {
                    deps[PackageName] = RequiredPackageVersion;
                    File.WriteAllText(manifestPath, manifestJson.ToString());
                    AssetDatabase.Refresh();
                    packageOK = true;
                }
                catch (Exception ex)
                {
                    ShowError("SDK インストール/更新エラー:\n" + ex.Message);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    private void DrawStep4_Finish()
    {
        GUILayout.Label("Step 4 / 4 : 完了", stepLabelStyle);
        GUILayout.Space(8);

        GUILayout.Label("すべてのセットアップが完了しました 🎉", EditorStyles.boldLabel);
        GUILayout.Space(12);

        DrawCompleteAnimation();
    }

    private void DrawCompleteAnimation()
    {
        if (!completeAnimPlaying)
        {
            completeAnimPlaying = true;
            completeAnimStartTime = EditorApplication.timeSinceStartup;
        }

        double elapsed = EditorApplication.timeSinceStartup - completeAnimStartTime;
        float t = Mathf.Clamp01((float)(elapsed / 1.1f));

        float centerX = position.width / 2;
        float centerY = 260f;

        Handles.BeginGUI();
        Handles.color = new Color(0.4f, 0.5f, 1f, t);
        Handles.DrawWireDisc(new Vector3(centerX, centerY), Vector3.forward, 40f);
        Handles.EndGUI();

        if (iconCheck)
        {
            float size = 40f * t;
            GUI.color = new Color(1f, 1f, 1f, t);

            GUI.DrawTexture(
                new Rect(centerX - size / 2, centerY - size / 2, size, size),
                iconCheck,
                ScaleMode.ScaleToFit
            );

            GUI.color = Color.white;
        }

        if (t >= 1f)
            completeAnimPlaying = false;
    }

    // ------------------------------------------------------------------
    private void DrawStepButtons()
    {
        GUILayout.BeginHorizontal();

        if (step > 0 && !manifestLoadFailed)
        {
            if (GUILayout.Button("戻る", buttonSecondary, GUILayout.Height(28), GUILayout.Width(120)))
            {
                step--;
            }
        }

        GUILayout.FlexibleSpace();

        bool canNext =
            (step == 0 && unityVersionOK) ||
            (step == 1 && registryOK) ||
            (step == 2 && packageOK) ||
            (step == 3);

        GUI.enabled = canNext;

        if (GUILayout.Button(step == 3 ? "閉じる" : "次へ", buttonPrimary, GUILayout.Height(32), GUILayout.Width(160)))
        {
            if (step == 3) Close();
            else step++;
        }

        GUI.enabled = true;

        GUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------------
    private int ComparePackageVersion(string a, string b)
    {
        try
        {
            var pa = a.Split('.');
            var pb = b.Split('.');

            for (int i = 0; i < 3; i++)
            {
                int ia = int.Parse(pa[i]);
                int ib = int.Parse(pb[i]);

                if (ia != ib) return ia.CompareTo(ib);
            }
        }
        catch { }

        return 0;
    }

    private void ShowError(string msg)
    {
        EditorUtility.DisplayDialog("エラー", msg, "OK");
    }

    // ------------------------------------------------------------------
    // ★ Unity 6000 対応：再起動前にプロジェクト設定を自動調整
    // ------------------------------------------------------------------
    private void ApplyProjectSettingsBeforeRestart()
    {
        try
        {
            Debug.Log("[Wizard] Applying recommended project settings (Unity 6000)...");

            // ------------------------------------------------------
            // 1. Color Space → Linear（再インポート無し）
            // ------------------------------------------------------
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                Debug.Log("[Wizard] Switching ColorSpace → Linear");
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }

            // ------------------------------------------------------
            // 2. Standard Shader Quality → Medium
            // ------------------------------------------------------
            Debug.Log("[Wizard] Setting Standard Shader Quality → Medium");

            var buildTarget = EditorUserBuildSettings.activeBuildTarget;

            for (int tier = 0; tier < 3; tier++)
            {
                var settings = EditorGraphicsSettings.GetTierSettings(buildTarget, (GraphicsTier)tier);
                settings.standardShaderQuality = ShaderQuality.Medium;
                EditorGraphicsSettings.SetTierSettings(buildTarget, (GraphicsTier)tier, settings);
            }

            // ------------------------------------------------------
            // 3. LightingSettings（Unity 6000 API）
            // ------------------------------------------------------
            Debug.Log("[Wizard] Setting LightingSettings → Medium");

            var lighting = Lightmapping.lightingSettings;

            if (lighting == null)
            {
                lighting = new LightingSettings();
                Lightmapping.lightingSettings = lighting;
            }

            lighting.bakeResolution = 40f;
            lighting.indirectResolution = 2f;

            lighting.denoiserTypeDirect = LightingSettings.DenoiserType.Optix;
            lighting.denoiserTypeIndirect = LightingSettings.DenoiserType.Optix;
            lighting.denoiserTypeAO = LightingSettings.DenoiserType.Optix;

            lighting.mixedBakeMode = MixedLightingMode.Shadowmask;
            lighting.realtimeGI = true;
            lighting.bakedGI = true;

            // ------------------------------------------------------
            // 4. Reflection Probe → Skybox 128
            // ------------------------------------------------------
            Debug.Log("[Wizard] Setting ReflectionProbe → Skybox 128");

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 128;
            RenderSettings.reflectionBounces = 1;
            RenderSettings.reflectionIntensity = 1.0f;

            // ------------------------------------------------------
            // 5. 保存のみ（再インポート無し）
            // ------------------------------------------------------
            AssetDatabase.SaveAssets();

            Debug.Log("[Wizard] Settings applied.");

        }
        catch (Exception ex)
        {
            Debug.LogError("[Wizard] Failed to apply project settings:\n" + ex);
        }
    }

    // ------------------------------------------------------------------
    private void RestartUnity()
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        AssetDatabase.SaveAssets();

        EditorApplication.OpenProject(projectPath);
    }
}
