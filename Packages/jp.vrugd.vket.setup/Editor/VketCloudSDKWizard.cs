using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

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

    // Unity 6 固定（表示用）
    private const string RequiredUnityVersionDisplay = "Unity 6.0.0f1 以上";

    // ------------------------------------------------------------------
    // ステップ管理
    // ------------------------------------------------------------------
    private int step = 0; // 0: Unity, 1: Registry, 2: Package, 3: Finish

    // 状態フラグ
    private bool unityVersionOK = false;
    private bool registryOK = false;
    private bool packageOK = false;

    private bool manifestLoadFailed = false;
    private bool unityWarningShown = false;

    // manifest.json
    private string manifestPath;
    private JObject manifestJson;

    // ------------------------------------------------------------------
    // UI スタイル
    // ------------------------------------------------------------------
    private GUIStyle titleStyle;
    private GUIStyle boxStyle;
    private GUIStyle stepLabelStyle;
    private GUIStyle buttonPrimary;
    private GUIStyle buttonSecondary;
    private GUIStyle badgeOK;
    private GUIStyle badgeNG;

    // ------------------------------------------------------------------
    // アイコン
    // ------------------------------------------------------------------
    private Texture2D iconCheck;
    private Texture2D iconWarning;

    // ------------------------------------------------------------------
    // スピナー（プログレスアニメーション）
    // ------------------------------------------------------------------
    private int spinnerIndex = 0;
    private double lastSpinnerTime = 0f;

    // ------------------------------------------------------------------
    // 完了アニメ（Lottie風）
    // ------------------------------------------------------------------
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
    // ライフサイクル
    // ------------------------------------------------------------------
    private void OnEnable()
    {
        manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
        LoadManifestJson();
        SetupStyles();
        LoadIcons();

        // Unity 6 以外なら一度だけ警告
        if (!IsUnity6OrNewer() && !unityWarningShown)
        {
            unityWarningShown = true;
            ShowError("このウィザードは Unity 6 以降でのみサポートされています。\n現在の Unity: " + Application.unityVersion);
        }
    }

    private void LoadManifestJson()
    {
        try
        {
            if (!File.Exists(manifestPath))
            {
                manifestLoadFailed = true;
                ShowError("manifest.json が見つかりませんでした。\nPackages/manifest.json を確認してください。");
                return;
            }

            string json = File.ReadAllText(manifestPath);
            manifestJson = JObject.Parse(json);
            manifestLoadFailed = false;
        }
        catch (Exception ex)
        {
            manifestLoadFailed = true;
            ShowError("manifest.json の読み込み中にエラーが発生しました:\n" + ex.Message);
        }
    }

    private void SetupStyles()
    {
        // タイトルバー
        titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        // カードボックス
        boxStyle = new GUIStyle("HelpBox")
        {
            padding = new RectOffset(16, 16, 16, 16),
            margin = new RectOffset(10, 10, 10, 10)
        };

        // ステップラベル
        stepLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16
        };

        // Primary Button
        buttonPrimary = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        buttonPrimary.normal.background = MakeTex(4, 4, new Color(0.35f, 0.45f, 1f));
        buttonPrimary.hover.background = MakeTex(4, 4, new Color(0.45f, 0.55f, 1f));

        // Secondary Button
        buttonSecondary = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
        };

        // バッジ
        badgeOK = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.15f, 0.65f, 0.2f) }
        };

        badgeNG = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.9f, 0.2f, 0.2f) }
        };
    }

    private void LoadIcons()
    {
        // Unity の標準アイコンを利用
        iconCheck = EditorGUIUtility.IconContent("TestPassed").image as Texture2D;
        if (iconCheck == null)
            iconCheck = EditorGUIUtility.IconContent("Collab.Check").image as Texture2D;

        iconWarning = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D;
    }

    // 単色テクスチャ
    private Texture2D MakeTex(int width, int height, Color color)
    {
        var tex = new Texture2D(width, height);
        var col = Enumerable.Repeat(color, width * height).ToArray();
        tex.SetPixels(col);
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------
    // GUI
    // ------------------------------------------------------------------
    private void OnGUI()
    {
        DrawHeader();

        GUILayout.Space(10);

        GUILayout.BeginVertical(boxStyle);
        if (manifestLoadFailed)
        {
            EditorGUILayout.HelpBox("manifest.json を読み込めないため、セットアップを続行できません。", MessageType.Error);
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

        // アニメーション用に再描画
        if (completeAnimPlaying || step < 3)
        {
            Repaint();
        }
    }

    // タイトルバー + スピナー
    private void DrawHeader()
    {
        Rect rect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.45f, 1f)); // Vket風ブルー

        GUI.Label(rect, "Vket Cloud SDK Install Wizard", titleStyle);

        // 右上にスピナー（ステップ 0〜2 の間だけ）
        if (step < 3 && !manifestLoadFailed)
        {
            Rect spinRect = new Rect(rect.xMax - 32, rect.y + 8, 24, 24);
            DrawSpinner(spinRect);
        }
        else if (step >= 3 && iconCheck != null)
        {
            // 完了時はチェック
            Rect iconRect = new Rect(rect.xMax - 32, rect.y + 8, 24, 24);
            GUI.DrawTexture(iconRect, iconCheck, ScaleMode.ScaleToFit, true);
        }
    }

    // スピナー描画（WaitSpin00〜11）
    private void DrawSpinner(Rect rect)
    {
        double t = EditorApplication.timeSinceStartup;
        if (t - lastSpinnerTime > 0.08f)
        {
            lastSpinnerTime = t;
            spinnerIndex = (spinnerIndex + 1) % 12;
        }

        var content = EditorGUIUtility.IconContent($"WaitSpin{spinnerIndex:00}");
        if (content != null && content.image != null)
        {
            GUI.DrawTexture(rect, content.image, ScaleMode.ScaleToFit, true);
        }
    }

    // ------------------------------------------------------------------
    // STEP 1 : Unity Version
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

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(4);
        if (unityVersionOK)
        {
            if (iconCheck != null)
                GUILayout.Label(iconCheck, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Unity バージョンは要件を満たしています。", badgeOK);
        }
        else
        {
            if (iconWarning != null)
                GUILayout.Label(iconWarning, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Unity 6.0.0f1 以上が必要です。", badgeNG);
        }
        EditorGUILayout.EndHorizontal();
    }

    // Unity 6 固定チェック
    private bool IsUnity6OrNewer()
    {
        var v = Application.unityVersion;

        // Unity 6 系は "6000." 始まり（正式リリース）や "6.0." 形式の可能性あり
        if (v.StartsWith("6000.")) return true;
        if (v.StartsWith("6.0.")) return true;

        // その他の 6.x を許容したい場合はここで拡張
        // if (v.StartsWith("6.")) return true;

        return false;
    }

    // ------------------------------------------------------------------
    // STEP 2 : Scoped Registry
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
            EditorGUILayout.BeginHorizontal();
            if (iconCheck != null)
                GUILayout.Label(iconCheck, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Scoped Registry はすでに登録されています。", badgeOK);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            if (iconWarning != null)
                GUILayout.Label(iconWarning, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Scoped Registry が見つかりません。追加が必要です。", badgeNG);
            EditorGUILayout.EndHorizontal();

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
                    AssetDatabase.Refresh();
                    registryOK = true;
                }
                catch (Exception ex)
                {
                    ShowError("Scoped Registry の追加中にエラーが発生しました:\n" + ex.Message);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // STEP 3 : Package Install / Update
    // ------------------------------------------------------------------
    private void DrawStep3_Package()
    {
        GUILayout.Label("Step 3 / 4 : Package Install / Update", stepLabelStyle);
        GUILayout.Space(6);

        var deps = manifestJson["dependencies"] as JObject;
        if (deps == null)
        {
            EditorGUILayout.HelpBox("manifest.json に dependencies セクションがありません。", MessageType.Error);
            packageOK = false;
            return;
        }

        string installedVersion = deps[PackageName]?.ToString();
        packageOK = installedVersion != null &&
                    ComparePackageVersion(installedVersion, RequiredPackageVersion) >= 0;

        EditorGUILayout.LabelField("Package", PackageName);
        EditorGUILayout.LabelField("Required Version", RequiredPackageVersion);
        EditorGUILayout.LabelField("Installed Version", installedVersion ?? "(not installed)");

        GUILayout.Space(8);

        if (packageOK)
        {
            EditorGUILayout.BeginHorizontal();
            if (iconCheck != null)
                GUILayout.Label(iconCheck, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("SDK は必要なバージョン以上がインストールされています。", badgeOK);
            EditorGUILayout.EndHorizontal();
        }
        else if (installedVersion == null)
        {
            EditorGUILayout.BeginHorizontal();
            if (iconWarning != null)
                GUILayout.Label(iconWarning, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("SDK がインストールされていません。", badgeNG);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("SDK をインストール", buttonPrimary, GUILayout.Height(32)))
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
                    ShowError("SDK インストール中にエラーが発生しました:\n" + ex.Message);
                }
            }
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            if (iconWarning != null)
                GUILayout.Label(iconWarning, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label($"SDK バージョンが古いです（{installedVersion}）。更新が必要です。", badgeNG);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("SDK を更新", buttonPrimary, GUILayout.Height(32)))
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
                    ShowError("SDK 更新中にエラーが発生しました:\n" + ex.Message);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // STEP 4 : Finish + Lottie風アニメ
    // ------------------------------------------------------------------
    private void DrawStep4_Finish()
    {
        GUILayout.Label("Step 4 / 4 : 完了", stepLabelStyle);
        GUILayout.Space(6);

        // Color Space チェック
        if (PlayerSettings.colorSpace != ColorSpace.Linear)
        {
            EditorGUILayout.BeginHorizontal();
            if (iconWarning != null)
                GUILayout.Label(iconWarning, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Color Space が Linear ではありません。Linear に変更してください。", badgeNG);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("Color Space を Linear に変更", buttonPrimary, GUILayout.Height(32)))
            {
                try
                {
                    PlayerSettings.colorSpace = ColorSpace.Linear;
                    AssetDatabase.Refresh();
                }
                catch (Exception ex)
                {
                    ShowError("Color Space 変更中にエラーが発生しました:\n" + ex.Message);
                }
            }
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            if (iconCheck != null)
                GUILayout.Label(iconCheck, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Color Space は Linear に設定されています。", badgeOK);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(16);
        GUILayout.Label("すべてのセットアップが完了しました！🎉", EditorStyles.boldLabel);
        GUILayout.Space(12);

        // Lottie 風アニメーション
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
        float t = Mathf.Clamp01((float)(elapsed / 1.2f)); // 1.2秒で収束

        float centerX = position.width / 2f;
        float centerY = 260f;
        float radius = 40f;

        // リング（WireDisc）
        Handles.BeginGUI();
        Handles.color = new Color(0.4f, 0.5f, 1f, Mathf.SmoothStep(0f, 1f, t));
        Handles.DrawWireDisc(new Vector3(centerX, centerY, 0), Vector3.forward, radius);
        Handles.EndGUI();

        // チェックアイコンの Scale + Fade
        if (iconCheck != null)
        {
            float scale = Mathf.SmoothStep(0f, 1f, t);
            float alpha = Mathf.SmoothStep(0f, 1f, t);

            float size = 40f * scale;
            Rect r = new Rect(centerX - size / 2f, centerY - size / 2f, size, size);

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(r, iconCheck, ScaleMode.ScaleToFit, true);
            GUI.color = prev;
        }

        if (t >= 1f)
        {
            completeAnimPlaying = false;
        }
    }

    // ------------------------------------------------------------------
    // STEP ボタン
    // ------------------------------------------------------------------
    private void DrawStepButtons()
    {
        GUILayout.BeginHorizontal();

        if (step > 0 && !manifestLoadFailed)
        {
            if (GUILayout.Button("戻る", buttonSecondary, GUILayout.Height(28), GUILayout.Width(120)))
            {
                step--;
                if (step < 3) completeAnimPlaying = false;
            }
        }

        GUILayout.FlexibleSpace();

        bool canNext = false;

        if (!manifestLoadFailed)
        {
            canNext =
                (step == 0 && unityVersionOK) ||
                (step == 1 && registryOK) ||
                (step == 2 && packageOK) ||
                (step == 3);
        }

        GUI.enabled = canNext;

        if (GUILayout.Button(step == 3 ? "閉じる" : "次へ", buttonPrimary, GUILayout.Height(32), GUILayout.Width(160)))
        {
            if (step == 3)
            {
                Close();
            }
            else
            {
                step++;
                if (step == 3)
                {
                    completeAnimPlaying = false; // 次の OnGUI で初期化し直す
                }
            }
        }

        GUI.enabled = true;

        GUILayout.EndHorizontal();
        GUILayout.Space(10);
    }

    // ------------------------------------------------------------------
    // ユーティリティ
    // ------------------------------------------------------------------
    private int ComparePackageVersion(string a, string b)
    {
        try
        {
            var pa = a.Split('.');
            var pb = b.Split('.');

            for (int i = 0; i < 3; i++)
            {
                int ia = (i < pa.Length) ? int.Parse(pa[i]) : 0;
                int ib = (i < pb.Length) ? int.Parse(pb[i]) : 0;
                if (ia != ib) return ia.CompareTo(ib);
            }
        }
        catch
        {
            // パースできない場合は安全側に倒して「古い」とみなす
            return -1;
        }

        return 0;
    }

    private void ShowError(string msg)
    {
        EditorUtility.DisplayDialog("エラー", msg, "OK");
    }
}
