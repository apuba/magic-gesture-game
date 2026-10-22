using UnityEngine;

namespace MagicMotion.UI
{
    /// <summary>
    /// User facing switches for the full screen hand tracking overlay. The values
    /// survive restarts through PlayerPrefs, and this class also draws the panel
    /// that edits them so the overlay only has to host and apply them.
    /// </summary>
    public sealed class OverlaySettings
    {
        private const string KeyShowStatus = "mm.ov.status";
        private const string KeyShowTrail = "mm.ov.trail";
        private const string KeyTrailLifetime = "mm.ov.lifetime";
        private const string KeyTrailThickness = "mm.ov.thickness";
        private const string KeyTrailDualColor = "mm.ov.dual";
        private const string KeyShowCamera = "mm.ov.camera";
        private const string KeyBackgroundRed = "mm.ov.bgR";
        private const string KeyBackgroundGreen = "mm.ov.bgG";
        private const string KeyBackgroundBlue = "mm.ov.bgB";
        private const string KeyFrameRate = "mm.ov.fps";
        private const string KeyTrackingRate = "mm.ov.track";
        private const string KeyTrackingRateVersion = "mm.ov.track.version";
        private const string KeyHoverSeconds = "mm.ov.hover";

        private static readonly Color[] Presets =
        {
            Color.black,
            new Color(0.06f, 0.09f, 0.14f),
            Color.white,
            new Color(0f, 0.32f, 0.12f)
        };

        private static readonly string[] PresetNames = { "黑色", "深蓝", "白色", "绿色" };

        // Drawing the overlay is what costs frames, so the ceiling is offered
        // as a user choice: 30 keeps the battery and the heat down, 60 matches
        // the smoothness casual games aim for.
        private static readonly int[] FrameRateValues = { 30, 45, 60 };

        // How many camera frames the tracker is allowed to consume per second.
        // The camera tops out at 30 and a single frame costs tens of
        // milliseconds, so 30 is the ceiling rather than a guarantee.
        private static readonly int[] TrackingRateValues = { 10, 15, 20, 25, 30 };

        public bool ShowStatusText { get; set; } = true;
        public bool ShowTrail { get; set; } = true;
        public float TrailLifetime { get; set; } = 0.65f;
        public float TrailThickness { get; set; } = 1f;
        public bool TrailDualColor { get; set; } = true;
        public bool ShowCamera { get; set; } = true;
        public Color BackgroundColor { get; set; } = Color.black;
        public int TargetFrameRate { get; set; } = 30;
        public int TrackingRate { get; set; } = 30;

        // How long the pointing pose must be held before the rune meter arms
        // itself. One second felt sluggish, so the default is much shorter and
        // the range stops well before the old slow end.
        public float HoverSeconds { get; set; } = 0.35f;

        public void Load()
        {
            ShowStatusText = PlayerPrefs.GetInt(KeyShowStatus, 1) == 1;
            ShowTrail = PlayerPrefs.GetInt(KeyShowTrail, 1) == 1;
            TrailLifetime = PlayerPrefs.GetFloat(KeyTrailLifetime, 0.65f);
            TrailThickness = PlayerPrefs.GetFloat(KeyTrailThickness, 1f);
            TrailDualColor = PlayerPrefs.GetInt(KeyTrailDualColor, 1) == 1;
            ShowCamera = PlayerPrefs.GetInt(KeyShowCamera, 1) == 1;
                        var storedFrameRate = PlayerPrefs.GetInt(KeyFrameRate, 30);
            TargetFrameRate =
                storedFrameRate == 45 ? 45 :
                storedFrameRate == 60 ? 60 : 30;

            // Version 1 defaulted to 20 FPS and was written on devices before
            // the control was actually connected to VisionPipeline. Migrate
            // that stale value once so existing installs receive the low-latency
            // 30 FPS default while later user choices remain persistent.
            var trackingRateVersion = PlayerPrefs.GetInt(KeyTrackingRateVersion, 1);
            var storedTrackingRate = trackingRateVersion < 2
                ? 30
                : PlayerPrefs.GetInt(KeyTrackingRate, 30);
            TrackingRate = Mathf.Clamp(storedTrackingRate, 10, 30);
            if (trackingRateVersion < 2)
            {
                PlayerPrefs.SetInt(KeyTrackingRate, TrackingRate);
                PlayerPrefs.SetInt(KeyTrackingRateVersion, 2);
                PlayerPrefs.Save();
            }
            HoverSeconds = Mathf.Clamp(PlayerPrefs.GetFloat(KeyHoverSeconds, 0.35f), 0.3f, 1f);
            BackgroundColor = new Color(
                PlayerPrefs.GetFloat(KeyBackgroundRed, 0f),
                PlayerPrefs.GetFloat(KeyBackgroundGreen, 0f),
                PlayerPrefs.GetFloat(KeyBackgroundBlue, 0f));
        }

        public void Save()
        {
            PlayerPrefs.SetInt(KeyShowStatus, ShowStatusText ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowTrail, ShowTrail ? 1 : 0);
            PlayerPrefs.SetFloat(KeyTrailLifetime, TrailLifetime);
            PlayerPrefs.SetFloat(KeyTrailThickness, TrailThickness);
            PlayerPrefs.SetInt(KeyTrailDualColor, TrailDualColor ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowCamera, ShowCamera ? 1 : 0);
            PlayerPrefs.SetInt(KeyFrameRate, TargetFrameRate);
            PlayerPrefs.SetInt(KeyTrackingRate, TrackingRate);
            PlayerPrefs.SetInt(KeyTrackingRateVersion, 2);
            PlayerPrefs.SetFloat(KeyHoverSeconds, HoverSeconds);
            PlayerPrefs.SetFloat(KeyBackgroundRed, BackgroundColor.r);
            PlayerPrefs.SetFloat(KeyBackgroundGreen, BackgroundColor.g);
            PlayerPrefs.SetFloat(KeyBackgroundBlue, BackgroundColor.b);
            PlayerPrefs.Save();
        }

        public Color TrailColor(bool fromTip)
        {
            if (!TrailDualColor)
            {
                return new Color(0.1f, 0.9f, 1f);
            }

            return fromTip
                ? new Color(1f, 0.78f, 0.15f)
                : new Color(0.1f, 0.9f, 1f);
        }
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;
        private GUIStyle buttonSelectedStyle;
        private static Font chineseFont;
        private bool dirty;
        private Vector2 scrollPosition;
        private bool touchScrollActive;
        private float touchLastY;

        /// <summary>
        /// Sizes follow the Android Material guidance: 48dp is the smallest
        /// comfortable touch target, body text is 16sp and titles are larger.
        /// The density scale turns those dp and sp values into the pixels that
        /// the immediate mode GUI works in.
        /// </summary>
        private static float DensityScale()
        {
            return Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 2f, 1f, 3f);
        }

        /// <summary>
        /// Draws the settings panel. Returns true when the close button was
        /// pressed, so the host can hide the panel again.
        /// </summary>
        public bool DrawPanel(Rect area)
        {
            var scale = DensityScale();
            EnsureStyles(scale);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;

            var padding = 16f * scale;
            var rowHeight = 56f * scale;
            var controlHeight = 48f * scale;

            // The rows add up to more than a landscape screen can show, so the
            // controls live in a scroll view and only the visible part is drawn.
            var rows = 7;
            if (ShowTrail)
            {
                rows += 3;
            }

            if (!ShowCamera)
            {
                rows += 5;
            }

            var contentHeight = padding + rows * rowHeight + controlHeight * 1.2f + padding;
            var viewRect = new Rect(0f, 0f, area.width - padding, contentHeight);
            // IMGUI scroll views on touch screens only react to the thin
            // scrollbar, so the panel is dragged with the finger directly.
            var touch = Event.current;
            if (touch.type == EventType.MouseDown || touch.type == EventType.MouseDrag)
            {
                if (area.Contains(touch.mousePosition))
                {
                    if (touch.type == EventType.MouseDown)
                    {
                        touchScrollActive = true;
                        touchLastY = touch.mousePosition.y;
                    }
                    else if (touchScrollActive)
                    {
                        scrollPosition.y -= touch.mousePosition.y - touchLastY;
                        touchLastY = touch.mousePosition.y;
                    }
                }
            }
            else if (touch.type == EventType.MouseUp)
            {
                touchScrollActive = false;
            }

            scrollPosition = GUI.BeginScrollView(area, scrollPosition, viewRect);

            var width = viewRect.width - padding;
            var x = padding;
            var cursor = padding;

            GUI.Label(new Rect(x, cursor, width, rowHeight), "叠加层设置", titleStyle);
            cursor += rowHeight;

            ShowStatusText = DrawToggle(x, cursor, width, controlHeight, "显示信息文字", ShowStatusText);
            cursor += rowHeight;

            ShowTrail = DrawToggle(x, cursor, width, controlHeight, "显示轨迹", ShowTrail);
            cursor += rowHeight;

            if (ShowTrail)
            {
                TrailLifetime = DrawSlider(x, cursor, width, rowHeight, "拖尾时长",
                    TrailLifetime, 0.2f, 2f, TrailLifetime.ToString("0.00") + " 秒");
                cursor += rowHeight;

                TrailThickness = DrawSlider(x, cursor, width, rowHeight, "轨迹粗细",
                    TrailThickness, 0.5f, 3f, TrailThickness.ToString("0.0") + " 倍");
                cursor += rowHeight;

                TrailDualColor = DrawToggle(x, cursor, width, controlHeight, "掌心与指尖双色", TrailDualColor);
                cursor += rowHeight;
            }

            ShowCamera = DrawToggle(x, cursor, width, controlHeight, "显示摄像头画面", ShowCamera);
            cursor += rowHeight;

            if (!ShowCamera)
            {
                GUI.Label(new Rect(x, cursor, width, controlHeight), "背景颜色", textStyle);
                cursor += rowHeight;

                var buttonWidth = (width - 12f * scale) * 0.25f;
                for (var index = 0; index < Presets.Length; index++)
                {
                    var rect = new Rect(x + index * (buttonWidth + 4f * scale), cursor, buttonWidth, controlHeight);
                    var tint = GUI.color;
                    GUI.color = Presets[index];
                    if (GUI.Button(rect, PresetNames[index], buttonStyle))
                    {
                        BackgroundColor = Presets[index];
                        dirty = true;
                    }

                    GUI.color = tint;
                }

                cursor += rowHeight;

                var color = BackgroundColor;
                color.r = DrawSlider(x, cursor, width, rowHeight, "红", color.r, 0f, 1f, color.r.ToString("0.00"));
                cursor += rowHeight;
                color.g = DrawSlider(x, cursor, width, rowHeight, "绿", color.g, 0f, 1f, color.g.ToString("0.00"));
                cursor += rowHeight;
                color.b = DrawSlider(x, cursor, width, rowHeight, "蓝", color.b, 0f, 1f, color.b.ToString("0.00"));
                cursor += rowHeight;
                BackgroundColor = color;
            }

            HoverSeconds = DrawSlider(x, cursor, width, rowHeight, "起笔悬停",
                HoverSeconds, 0.3f, 1f, HoverSeconds.ToString("0.00") + " 秒");
            cursor += rowHeight;

            TrackingRate = DrawChoice(x, cursor, width, controlHeight, "跟踪频率", TrackingRateValues, TrackingRate);
            cursor += rowHeight;

            TargetFrameRate = DrawChoice(x, cursor, width, controlHeight, "画面帧率", FrameRateValues, TargetFrameRate);
            cursor += rowHeight;

            var closePressed = GUI.Button(new Rect(x, cursor, width, controlHeight * 1.2f), "关闭", buttonStyle);
            if (dirty)
            {
                dirty = false;
                Save();
            }

            GUI.EndScrollView();
            return closePressed;
        }

        private bool DrawToggle(float x, float cursor, float width, float controlHeight, string label, bool value)
        {
            var buttonWidth = Mathf.Max(96f * DensityScale(), width * 0.35f);
            GUI.Label(new Rect(x, cursor, width - buttonWidth, controlHeight), label, textStyle);
            if (GUI.Button(new Rect(x + width - buttonWidth, cursor, buttonWidth, controlHeight),
                value ? "开" : "关", buttonStyle))
            {
                value = !value;
                dirty = true;
            }

            return value;
        }

        private int DrawChoice(float x, float cursor, float width, float controlHeight,
            string label, int[] values, int current)
        {
            var scale = DensityScale();
            var buttonWidth = Mathf.Max(88f * scale, width * 0.2f);
            GUI.Label(new Rect(x, cursor, width - buttonWidth * values.Length - 8f * scale, controlHeight),
                label, textStyle);
            var result = current;
            for (var index = 0; index < values.Length; index++)
            {
                var selected = values[index] == current;
                var rect = new Rect(
                    x + width - buttonWidth * (values.Length - index),
                    cursor,
                    buttonWidth - 4f * scale,
                    controlHeight);
                var style = selected ? buttonSelectedStyle : buttonStyle;
                if (GUI.Button(rect, values[index].ToString(), style))
                {
                    if (!selected)
                    {
                        result = values[index];
                        dirty = true;
                    }
                }
            }

            return result;
        }

        private float DrawSlider(float x, float cursor, float width, float rowHeight,
            string label, float value, float minimum, float maximum, string text)
        {
            var labelWidth = width * 0.32f;
            var textWidth = width * 0.18f;
            GUI.Label(new Rect(x, cursor, labelWidth, rowHeight), label, textStyle);
            var updated = GUI.HorizontalSlider(
                new Rect(x + labelWidth, cursor, width - labelWidth - textWidth, rowHeight), value, minimum, maximum);
            GUI.Label(new Rect(x + width - textWidth, cursor, textWidth, rowHeight), text, textStyle);
            if (Mathf.Abs(updated - value) > 0.0001f)
            {
                dirty = true;
            }

            return updated;
        }

        private void EnsureStyles(float scale)
        {
            if (textStyle != null)
            {
                return;
            }

            if (chineseFont == null)
            {
                chineseFont = Resources.Load<Font>("Fonts/SimHei");
            }

            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = (int)(16f * scale);
            textStyle.normal.textColor = Color.white;
            textStyle.alignment = TextAnchor.MiddleLeft;

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = (int)(22f * scale);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            titleStyle.alignment = TextAnchor.MiddleLeft;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = (int)(16f * scale);
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.alignment = TextAnchor.MiddleCenter;

            buttonSelectedStyle = new GUIStyle(buttonStyle);
            buttonSelectedStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

            if (chineseFont != null)
            {
                textStyle.font = chineseFont;
                titleStyle.font = chineseFont;
                buttonStyle.font = chineseFont;
                buttonSelectedStyle.font = chineseFont;

                // Dynamic fonts render nothing until their glyphs have been
                // rasterized, so ask for every character the panel can show
                // up front instead of letting the first frames come out blank.
                const string panelText =
                    "叠加层设置显示信息文字轨迹拖尾时长秒粗倍掌心与指尖双色摄像头画面背景颜色黑深蓝白绿红关开关闭";
                chineseFont.RequestCharactersInTexture(panelText, textStyle.fontSize);
                chineseFont.RequestCharactersInTexture(panelText, titleStyle.fontSize);
                chineseFont.RequestCharactersInTexture(panelText, buttonStyle.fontSize);
            }
        }
    }
}
