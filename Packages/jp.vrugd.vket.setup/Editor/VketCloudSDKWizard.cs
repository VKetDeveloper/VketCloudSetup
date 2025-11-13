using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
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

    // 追加パッケージ
    private const string DeepLinkName = "com.needle.deeplink";
    private const string DeepLinkURL = "https://github.com/needle-tools/unity-deeplink.git?path=/package";

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
            ShowError("このウィザードは Unity 6 以降専用です。\n現在: " + Application.unityVersion);
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
                ShowError("manifest.json が見つかりません。");
                return;
            }

            manifestJson = JObject.Parse(File.ReadAllText(manifestPath));
            manifestLoadFailed = false;
        }
        catch (Exception ex)
        {
            manifestLoadFailed = true;
            ShowError("manifest.json 読み込みエラー:\n" + ex.Message);
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
            padding = new RectOffset(16,16,16,16),
            margin = new RectOffset(10,10,10,10)
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
        buttonPrimary.normal.background = MakeTex(4,4,new Color(0.35f,0.45f,1f));
        buttonPrimary.hover.background = MakeTex(4,4,new Color(0.45f,0.55f,1f));

        buttonSecondary = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14
        };

        badgeOK = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.2f, 0.8f, 0.2f) }
        };
        badgeNG = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(1f, 0.3f, 0.3f) }
        };

        iconCheck = EditorGUIUtility.IconContent("TestPassed").image as Texture2D
            ?? EditorGUIUtility.IconContent("Collab.Check").image as Texture2D;

        iconWarning = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D;
    }

    private Texture2D MakeTex(int w, int h, Color c)
    {
        var tex = new Texture2D(w,h);
        tex.SetPixels(Enumerable.Repeat(c, w*h).ToArray());
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

        if (completeAnimPlaying || step < 3)
            Repaint();
    }

    // ------------------------------------------------------------------
    private void DrawHeader()
    {
        Rect rect = GUILayoutUtility.GetRect(0, 40);
        EditorGUI.DrawRect(rect, new Color(0.35f,0.45f,1f));
        GUI.Label(rect, "Vket Cloud SDK Install Wizard", titleStyle);

        if (step < 3)
        {
            var r = new Rect(rect.xMax-32, rect.y+8, 24,24);
            DrawSpinner(r);
        }
    }

    private void DrawSpinner(Rect r)
    {
        if (EditorApplication.timeSinceStartup - lastSpinnerTime > 0.08)
        {
            lastSpinnerTime = EditorApplication.timeSinceStartup;
            spinnerIndex = (spinnerIndex+1) % 12;
        }

        GUI.DrawTexture(r, EditorGUIUtility.IconContent($"WaitSpin{spinnerIndex:00}").image);
    }

    // ------------------------------------------------------------------
    private void DrawStep1_UnityCheck()
    {
        GUILayout.Label("Step 1 / 4 : Unity Version Check", stepLabelStyle);

        unityVersionOK = IsUnity6OrNewer();

        EditorGUILayout.LabelField("現在", Application.unityVersion);
        EditorGUILayout.LabelField("必要バージョン", "Unity 6.0.0f1 以上");

        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            unityVersionOK ? "✔ OK" : "⚠ Unity 6 以上が必要",
            unityVersionOK ? badgeOK : badgeNG
        );
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

        var scoped = manifestJson["scopedRegistries"] as JArray ?? new JArray();

        registryOK = scoped.Any(r => r["name"]?.ToString() == RegistryName);

        if (registryOK)
        {
            EditorGUILayout.LabelField("✔ Registry は登録済みです", badgeOK);
            return;
        }

        EditorGUILayout.LabelField("⚠ Registry が登録されていません", badgeNG);
        GUILayout.Space(10);

        if (GUILayout.Button("Registry を追加", buttonPrimary, GUILayout.Height(32)))
        {
            try
            {
                // Registry 追加
                scoped.Add(new JObject {
                    ["name"] = RegistryName,
                    ["url"] = RegistryURL,
                    ["scopes"] = new JArray(RegistryScope)
                });
                manifestJson["scopedRegistries"] = scoped;

                // unity-deeplink 追加
                var deps = manifestJson["dependencies"] as JObject;
                if (deps != null && deps[DeepLinkName] == null)
                {
                    deps[DeepLinkName] = DeepLinkURL;
                }

                File.WriteAllText(manifestPath, manifestJson.ToString());
                AssetDatabase.Refresh();

                registryOK = true;

                // 再起動
                if (EditorUtility.DisplayDialog(
                    "Unity を再起動しますか？",
                    "Registry と DeepLink を追加しました。\n推奨設定を適用して Unity を再起動します。",
                    "再起動する",
                    "キャンセル"))
                {
                    ApplyProjectSettingsBeforeRestart();
                    RestartUnity();
                }

            }
            catch (Exception ex)
            {
                ShowError("Registry 追加中エラー:\n" + ex.Message);
            }
        }
    }

    // ------------------------------------------------------------------
    private void DrawStep3_Package()
    {
        GUILayout.Label("Step 3 / 4 : Package", stepLabelStyle);

        var deps = manifestJson["dependencies"] as JObject;
        string installed = deps?[PackageName]?.ToString();

        packageOK = installed != null &&
                    ComparePackageVersion(installed, RequiredPackageVersion) >= 0;

        EditorGUILayout.LabelField("現在", installed ?? "未インストール");
        EditorGUILayout.LabelField("必要", RequiredPackageVersion);

        if (packageOK)
        {
            EditorGUILayout.LabelField("✔ OK", badgeOK);
            return;
        }

        GUILayout.Space(10);

        if (GUILayout.Button("SDK をインストール / 更新", buttonPrimary, GUILayout.Height(32)))
        {
            deps[PackageName] = RequiredPackageVersion;
            File.WriteAllText(manifestPath, manifestJson.ToString());
            AssetDatabase.Refresh();
            packageOK = true;
        }
    }

    // ------------------------------------------------------------------
    private void DrawStep4_Finish()
    {
        GUILayout.Label("Step 4 / 4 : 完了", stepLabelStyle);
        GUILayout.Space(10);

        GUILayout.Label("セットアップ完了 🎉", EditorStyles.boldLabel);

        DrawCompleteAnimation();
    }

    private void DrawCompleteAnimation()
    {
        if (!completeAnimPlaying)
        {
            completeAnimPlaying = true;
            completeAnimStartTime = EditorApplication.timeSinceStartup;
        }

        float t = Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - completeAnimStartTime) / 1.2f);
        float cx = position.width / 2;
        float cy = 250;

        Handles.BeginGUI();
        Handles.color = new Color(0.4f,0.5f,1f,t);
        Handles.DrawWireDisc(new Vector3(cx,cy), Vector3.forward, 40);
        Handles.EndGUI();

        if (iconCheck)
        {
            float size = 40 * t;
            GUI.color = new Color(1,1,1,t);
            GUI.DrawTexture(new Rect(cx-size/2, cy-size/2, size, size), iconCheck);
            GUI.color = Color.white;
        }
    }

    // ------------------------------------------------------------------
    private void DrawStepButtons()
    {
        GUILayout.BeginHorizontal();

        if (step > 0)
        {
            if (GUILayout.Button("戻る", buttonSecondary, GUILayout.Height(28), GUILayout.Width(120)))
                step--;
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
            for (int i=0; i<3; i++)
            {
                int ia = int.Parse(pa[i]);
                int ib = int.Parse(pb[i]);
                if (ia != ib) return ia.CompareTo(ib);
            }
        }
        catch {}

        return 0;
    }

    private void ShowError(string msg)
    {
        EditorUtility.DisplayDialog("エラー", msg, "OK");
    }

    // ------------------------------------------------------------------
    // ★ Unity 6000 対応：再起動前の設定
    // ------------------------------------------------------------------
    private void ApplyProjectSettingsBeforeRestart()
    {
        try
        {
            Debug.Log("[Wizard] Apply settings (Unity 6000)");

            // 1. ColorSpace → Linear
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                PlayerSettings.colorSpace = ColorSpace.Linear;

            // 2. Standard Shader Quality → Medium（TierSettings）
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;

            for (int tier = 0; tier < 3; tier++)
            {
                var ts = EditorGraphicsSettings.GetTierSettings(group, (GraphicsTier)tier);
                ts.standardShaderQuality = ShaderQuality.Medium;
                EditorGraphicsSettings.SetTierSettings(group, (GraphicsTier)tier, ts);
            }

            // 3. ReflectionProbe（Skybox 128）
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 128;

            AssetDatabase.SaveAssets();
        }
        catch (Exception ex)
        {
            Debug.LogError("[Wizard] Failed ApplyProjectSettings:\n" + ex);
        }
    }

    // ------------------------------------------------------------------
    private void RestartUnity()
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        EditorApplication.OpenProject(projectPath);
    }
}
