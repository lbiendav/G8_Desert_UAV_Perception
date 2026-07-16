using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DesertEnv
{
    /// <summary>
    /// Runtime control panel for the WeatherDirector: one button per
    /// weather state, an auto-cycle toggle button and a live readout of
    /// temperature, wind, rain and dust. The whole uGUI hierarchy (canvas,
    /// event system with InputSystemUIInputModule, buttons) is built in
    /// code at Start, so the scene only needs this one component and the
    /// panel works with the new Input System (the project runs with
    /// activeInputHandler = Input System only, which silently disables
    /// IMGUI - do not use OnGUI here).
    /// </summary>
    [DisallowMultipleComponent]
    public class WeatherControlPanel : MonoBehaviour
    {
        [SerializeField] private WeatherDirector m_Director;
        [SerializeField] private AirTemperatureModel m_Temperature;
        [SerializeField] private DesertWindController m_Wind;
        [SerializeField] private WeatherSystem m_RainSystem;
        [SerializeField] private SandstormSystem m_Sandstorm;
        [SerializeField] private DustDevilSystem m_DustDevils;
        [SerializeField] private ThunderstormSystem m_Thunder;

        private static readonly Color s_PanelColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color s_ButtonColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);
        private static readonly Color s_ButtonActiveColor = new Color(0.15f, 0.55f, 0.2f, 0.95f);
        private static readonly Color s_ButtonOffColor = new Color(0.6f, 0.14f, 0.12f, 0.95f);

        private static readonly DesertWeather[] s_Order =
        {
            DesertWeather.Clear,
            DesertWeather.Breezy,
            DesertWeather.Windstorm,
            DesertWeather.Sandstorm,
            DesertWeather.Rain
        };

        private Font m_Font;
        private Text m_StatusText;
        private Text m_AutoButtonText;
        private Text m_MasterButtonText;
        private Image m_MasterButtonImage;
        private Button m_AutoButton;
        private readonly Image[] m_ButtonImages = new Image[5];
        private readonly Button[] m_Buttons = new Button[5];

        private void Awake()
        {
            if (m_Director == null) m_Director = FindFirstObjectByType<WeatherDirector>();
            if (m_Temperature == null) m_Temperature = FindFirstObjectByType<AirTemperatureModel>();
            if (m_Wind == null) m_Wind = FindFirstObjectByType<DesertWindController>();
            if (m_RainSystem == null) m_RainSystem = FindFirstObjectByType<WeatherSystem>();
            if (m_Sandstorm == null) m_Sandstorm = FindFirstObjectByType<SandstormSystem>();
            if (m_DustDevils == null) m_DustDevils = FindFirstObjectByType<DustDevilSystem>();
            if (m_Thunder == null) m_Thunder = FindFirstObjectByType<ThunderstormSystem>();
        }

        private static string LabelOf(DesertWeather w)
        {
            switch (w)
            {
                case DesertWeather.Clear: return "Trời quang";
                case DesertWeather.Breezy: return "Gió nhẹ";
                case DesertWeather.Windstorm: return "Gió lớn";
                case DesertWeather.Sandstorm: return "Bão cát";
                case DesertWeather.Rain: return "Mưa";
                default: return w.ToString();
            }
        }

        private void Start()
        {
            if (m_Director == null)
            {
                Debug.LogWarning("[WeatherControlPanel] No WeatherDirector in scene - panel disabled.", this);
                enabled = false;
                return;
            }

            m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUi();
        }

        private void Update()
        {
            if (m_StatusText == null)
            {
                return;
            }

            bool fx = m_Director.EffectsEnabled;

            string line1 = fx ? "Đang: " + LabelOf(m_Director.Current) : "Hiệu ứng đã tắt";
            if (fx && m_Director.AutoCycle)
            {
                line1 += "  (còn " + Mathf.CeilToInt(m_Director.SecondsUntilChange) + "s)";
            }

            string line2 = "";
            if (m_Temperature != null)
            {
                line2 += m_Temperature.Celsius.ToString("0.0") + "°C";
            }
            if (m_Wind != null)
            {
                line2 += "   Gió " + Mathf.RoundToInt(m_Wind.EffectiveStrength01 * 100f) + "%";
            }

            string line3 = "";
            if (m_RainSystem != null)
            {
                line3 += "Mưa " + Mathf.RoundToInt(m_RainSystem.RainIntensity01 * 100f) + "%";
            }
            if (m_Sandstorm != null)
            {
                line3 += "   Bụi " + Mathf.RoundToInt(m_Sandstorm.Intensity01 * 100f) + "%";
            }

            string line4 = "";
            if (m_DustDevils != null && m_DustDevils.ActiveCount > 0)
            {
                line4 += "Lốc cát: " + m_DustDevils.ActiveCount;
            }
            if (m_Thunder != null && m_Thunder.IsStormActive)
            {
                line4 += (line4.Length > 0 ? "   " : "") + "Dông sét!";
            }

            m_StatusText.text = line1 + "\n" + line2 + "\n" + line3
                + (line4.Length > 0 ? "\n" + line4 : "");

            for (int i = 0; i < s_Order.Length; i++)
            {
                if (m_ButtonImages[i] != null)
                {
                    m_ButtonImages[i].color = fx && m_Director.Current == s_Order[i]
                        ? s_ButtonActiveColor
                        : s_ButtonColor;
                }
                if (m_Buttons[i] != null)
                {
                    m_Buttons[i].interactable = fx;
                }
            }

            if (m_AutoButtonText != null)
            {
                m_AutoButtonText.text = m_Director.AutoCycle ? "Tự động: BẬT" : "Tự động: TẮT";
            }
            if (m_AutoButton != null)
            {
                m_AutoButton.interactable = fx;
            }

            if (m_MasterButtonText != null)
            {
                m_MasterButtonText.text = fx ? "Hiệu ứng: BẬT" : "Hiệu ứng: TẮT";
            }
            if (m_MasterButtonImage != null)
            {
                m_MasterButtonImage.color = fx ? s_ButtonActiveColor : s_ButtonOffColor;
            }
        }

        /// <summary>uGUI needs an EventSystem with the Input System UI
        /// module to receive clicks under the new Input System.</summary>
        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var esGo = new GameObject("EventSystem (runtime)");
            esGo.transform.SetParent(transform, false);
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("WeatherPanelCanvas (runtime)");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // panel anchored to the top-left corner
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -16f);
            rt.sizeDelta = new Vector2(240f, 0f);
            panel.GetComponent<Image>().color = s_PanelColor;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.spacing = 6f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(panel.transform, "Thời tiết sa mạc", 20, FontStyle.Bold);
            m_StatusText = AddText(panel.transform, "...", 15, FontStyle.Normal);

            // master on/off switch for every weather effect
            m_MasterButtonImage = AddButton(panel.transform, "Hiệu ứng: BẬT",
                () => m_Director.EffectsEnabled = !m_Director.EffectsEnabled);
            m_MasterButtonText = m_MasterButtonImage.GetComponentInChildren<Text>();

            for (int i = 0; i < s_Order.Length; i++)
            {
                DesertWeather w = s_Order[i];
                m_ButtonImages[i] = AddButton(panel.transform, LabelOf(w), () => m_Director.SetWeather(w));
                m_Buttons[i] = m_ButtonImages[i].GetComponent<Button>();
            }

            Image autoImg = AddButton(panel.transform, "Tự động: BẬT",
                () => m_Director.AutoCycle = !m_Director.AutoCycle);
            m_AutoButtonText = autoImg.GetComponentInChildren<Text>();
            m_AutoButton = autoImg.GetComponent<Button>();
        }

        private Text AddText(Transform parent, string content, int size, FontStyle style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = m_Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private Image AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = s_ButtonColor;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = 32f;

            Text text = AddText(go.transform, label, 16, FontStyle.Normal);
            text.alignment = TextAnchor.MiddleCenter;
            var textRt = (RectTransform)text.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return image;
        }
    }
}
