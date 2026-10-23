using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EsportsSim
{
    /// <summary>
    /// Точка входа. Создаётся автоматически при запуске любой сцены —
    /// ничего настраивать в редакторе не нужно. Весь интерфейс строится из кода.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        static bool booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { booted = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (booted) return;
            booted = true;
            var go = new GameObject("EsportsSim");
            DontDestroyOnLoad(go);
            go.AddComponent<GameUI>();
        }

        readonly System.Random rng = new System.Random();
        RectTransform safeRoot;
        RectTransform screen;
        Rect lastSafe;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            UIKit.Init();
            Roster.Load();
            Season.Load();

            EnsureCamera();
            EnsureEventSystem();
            BuildCanvas();
            ShowMenu();
        }

        void EnsureCamera()
        {
            if (Camera.main != null) { Camera.main.backgroundColor = Pal.Bg; return; }
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Bg;
            cam.orthographic = true;
            DontDestroyOnLoad(cam.gameObject);
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            DontDestroyOnLoad(es);
        }

        void BuildCanvas()
        {
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // ширина фиксирована, высота «гуляет» под разные телефоны
            cgo.AddComponent<GraphicRaycaster>();

            UIKit.Img(cgo.transform, Pal.Bg, null, "Background").RT().Fill();
            safeRoot = UIKit.Node("SafeArea", cgo.transform).Fill();
            ApplySafeArea();
        }

        void Update()
        {
            if (Screen.safeArea != lastSafe) ApplySafeArea();
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var min = lastSafe.position;
            var max = lastSafe.position + lastSafe.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            safeRoot.anchorMin = min; safeRoot.anchorMax = max;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        RectTransform NewScreen()
        {
            StopAllCoroutines();
            if (screen != null) Destroy(screen.gameObject);
            screen = UIKit.Node("Screen", safeRoot).Fill();
            return screen;
        }

        /// <summary>Верхняя панель с кнопкой «назад» и заголовком. Высота 140.</summary>
        RectTransform TopBar(RectTransform parent, string title, Action back, int fontSize = 50)
        {
            var bar = UIKit.Img(parent, Pal.Panel, null, "TopBar").RT().Top(0, 140);
            if (back != null)
                UIKit.Btn(bar, "<", Pal.Panel2, back, 60).RT().At(0, 0.5f, 90, 0, 120, 110);
            var t = UIKit.Txt(bar, title, fontSize, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.RT().Fill(); t.RT().offsetMin = new Vector2(170, 0); t.RT().offsetMax = new Vector2(-170, 0);
            return bar;
        }

        // ================= ГЛАВНОЕ МЕНЮ =================

        void ShowMenu()
        {
            var s = NewScreen();

            var logo = UIKit.Txt(s, "КИБЕР\nАРЕНА", 140, Pal.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            logo.lineSpacing = 0.85f;
            logo.RT().Top(220, 340);
            UIKit.Txt(s, "симулятор киберспортивных турниров", 38, Pal.Dim).RT().Top(570, 60);

            var col = UIKit.Node("Buttons", s).At(0.5f, 0.5f, 0, -170, 820, 620);
            UIKit.Btn(col, "СВОБОДНЫЙ БОЙ", Pal.P1, ShowSelect, 56).RT().Top(0, 170);

            string seasonLabel = $"СЕЗОН {Season.D.number}\n<size=34><color={Pal.Tag(Pal.Dim)}>" +
                                 $"{Season.PhaseName()}</color></size>";
            UIKit.Btn(col, seasonLabel, Pal.P2, ShowSeason, 56).RT().Top(210, 200);
            UIKit.Btn(col, "НАСТРОЙКИ", Pal.Panel2, () => ShowSettings(0), 44).RT().Top(450, 130);

            var ht = Season.D.historyTeams;
            if (ht.Count > 0)
            {
                var last = Roster.Team(ht[ht.Count - 1]);
                string who = Season.D.historyPlayers.Count >= ht.Count ? Season.D.historyPlayers[ht.Count - 1] : "";
                if (last != null)
                    UIKit.Txt(s, $"Последний чемпион мира: <color={Pal.Tag(Pal.Gold)}><b>{last.name}</b></color>" +
                                 (who != "" ? $" ({who})" : ""), 34, Pal.Dim).RT().Bottom(60, 60, 30, 30);
            }
        }
    }
}
