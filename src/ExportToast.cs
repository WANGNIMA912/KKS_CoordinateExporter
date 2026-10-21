using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace KKS.CoordinateExporter
{
    /// <summary>
    /// 导出完成后在屏幕左上角显示一条短暂提示（纯 IMGUI，不依赖游戏 UI）。
    ///
    /// 字体优先用系统里的中文字体；找不到时自动改显示英文，
    /// 避免 IMGUI 默认字体缺字变成一屏方块。
    /// </summary>
    internal static class ExportToast
    {
        private const float Lifetime = 4.5f;
        private const float FadeTime = 0.9f;
        private const float FadeInTime = 0.12f;
        private const float Margin = 18f;
        private const float Spacing = 6f;
        private const int MaxEntries = 4;

        private static readonly string[] PreferredCjkFonts =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "SimSun",
            "Noto Sans CJK SC",
            "Arial Unicode MS"
        };

        private static readonly List<Entry> Entries = new List<Entry>();

        private static GUIStyle _labelStyle;
        private static GUIStyle _boxStyle;
        private static bool _stylesReady;
        private static bool _cjk;
        private static string _lastError;

        internal static void Success(string chinese, string english)
        {
            Add(chinese, english, false);
        }

        internal static void Failure(string chinese, string english)
        {
            Add(chinese, english, true);
        }

        private static void Add(string chinese, string english, bool error)
        {
            float now = Time.unscaledTime;

            Entries.Add(new Entry
            {
                Chinese = chinese,
                English = english,
                Born = now,
                Expires = now + Lifetime,
                Error = error
            });

            while (Entries.Count > MaxEntries) Entries.RemoveAt(0);
        }

        /// <summary>由插件的 OnGUI 每帧调用；没有消息时几乎零开销。</summary>
        internal static void Draw()
        {
            if (Entries.Count == 0) return;

            try
            {
                EnsureStyles();

                float now = Time.unscaledTime;
                for (int i = Entries.Count - 1; i >= 0; i--)
                {
                    if (now >= Entries[i].Expires) Entries.RemoveAt(i);
                }

                float y = Margin;
                for (int i = 0; i < Entries.Count; i++)
                {
                    Entry entry = Entries[i];
                    string text = _cjk ? entry.Chinese : entry.English;

                    GUIContent content = new GUIContent(text);
                    Vector2 size = _labelStyle.CalcSize(content);

                    float maxWidth = Mathf.Max(220f, Screen.width - Margin * 2f);
                    float width = Mathf.Min(size.x + 32f, maxWidth);
                    float height = Mathf.Max(30f, size.y + 12f);

                    float alpha = Mathf.Clamp01((now - entry.Born) / FadeInTime);
                    alpha = Mathf.Min(alpha, Mathf.Clamp01((entry.Expires - now) / FadeTime));

                    var rect = new Rect(Margin, y, width, height);
                    Color oldColor = GUI.color;

                    GUI.color = new Color(0f, 0f, 0f, 0.66f * alpha);
                    GUI.Box(rect, GUIContent.none, _boxStyle);

                    Color accent = entry.Error
                        ? new Color(1f, 0.42f, 0.38f, alpha)
                        : new Color(0.55f, 1f, 0.66f, alpha);
                    GUI.color = accent;
                    GUI.DrawTexture(new Rect(rect.x, rect.y + 3f, 3f, rect.height - 6f), Texture2D.whiteTexture);

                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    _labelStyle.normal.textColor = entry.Error
                        ? new Color(1f, 0.84f, 0.80f)
                        : new Color(1f, 1f, 1f);
                    GUI.Label(new Rect(rect.x + 14f, rect.y, rect.width - 22f, rect.height), content, _labelStyle);

                    GUI.color = oldColor;
                    y += height + Spacing;
                }
            }
            catch (Exception ex)
            {
                LogErrorOnce(ex);
            }
        }

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            Font font = TryCreateCjkFont();
            _cjk = font != null;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            _boxStyle = new GUIStyle(GUI.skin.box);

            if (font != null) _labelStyle.font = font;
        }

        private static Font TryCreateCjkFont()
        {
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                if (installed == null || installed.Length == 0) return null;

                foreach (string candidate in PreferredCjkFonts)
                {
                    if (Array.IndexOf(installed, candidate) < 0) continue;

                    Font font = Font.CreateDynamicFontFromOSFont(candidate, 14);
                    if (font != null) return font;
                }
            }
            catch (Exception ex)
            {
                LogWarning("创建系统字体失败，提示将使用英文: " + ex.Message);
            }

            return null;
        }

        private static void LogErrorOnce(Exception ex)
        {
            string message = ex.ToString();
            if (message == _lastError) return;

            _lastError = message;
            LogError("绘制导出提示时出错: " + message);
        }

        private static void LogWarning(string message)
        {
            ManualLogSource log = CoordinateExporterPlugin.Log;
            if (log != null) log.LogWarning(message);
        }

        private static void LogError(string message)
        {
            ManualLogSource log = CoordinateExporterPlugin.Log;
            if (log != null) log.LogError(message);
        }

        private struct Entry
        {
            public string Chinese;
            public string English;
            public float Born;
            public float Expires;
            public bool Error;
        }
    }
}
