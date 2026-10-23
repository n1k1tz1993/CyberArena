using System;
using UnityEngine;
using UnityEngine.UI;

namespace EsportsSim
{
    public static class Pal
    {
        public static readonly Color Bg = Hex("0E1220");
        public static readonly Color Panel = Hex("1A2135");
        public static readonly Color Panel2 = Hex("252E4A");
        public static readonly Color Text = Hex("EEF1FA");
        public static readonly Color Dim = Hex("8A93AD");
        public static readonly Color Accent = Hex("2EE6D6");
        public static readonly Color P1 = Hex("FF5468");
        public static readonly Color P2 = Hex("4D8DFF");
        public static readonly Color Gold = Hex("FFC940");
        public static readonly Color Good = Hex("4CD97B");
        public static readonly Color Bad = Hex("FF5468");
        public static readonly Color[] Regions =
        {
            Hex("FF6B6B"), Hex("FFB84D"), Hex("4CD97B"), Hex("4DB8FF"), Hex("B57BFF")
        };

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            return c;
        }

        public static Color A(Color c, float a) { c.a = a; return c; }
        public static string Tag(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Мелкие помощники для сборки uGUI из кода.</summary>
    public static class UIKit
    {
        public static Font Font;
        public static Sprite Round;   // скруглённый прямоугольник (9-slice)
        public static Sprite Circle;

        public static void Init()
        {
#if UNITY_2022_2_OR_NEWER
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            Round = MakeRounded(64, 22, true);
            Circle = MakeRounded(128, 64, false);
        }

        static Sprite MakeRounded(int size, int radius, bool sliced)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, radius, size - radius);
                float cy = Mathf.Clamp(fy, radius, size - radius);
                float d = Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                SpriteMeshType.FullRect, border);
        }

        // ---------- RectTransform ----------

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform RT(this Component c) => (RectTransform)c.transform;

        public static RectTransform Fill(this RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        /// <summary>Полоса на всю ширину, отступ y от верха родителя, высота h.</summary>
        public static RectTransform Top(this RectTransform rt, float y, float h, float l = 0, float r = 0)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(l, -y - h); rt.offsetMax = new Vector2(-r, -y);
            return rt;
        }

        /// <summary>Полоса на всю ширину, отступ y от низа родителя, высота h.</summary>
        public static RectTransform Bottom(this RectTransform rt, float y, float h, float l = 0, float r = 0)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(l, y); rt.offsetMax = new Vector2(-r, y + h);
            return rt;
        }

        /// <summary>Растянуть между отступом сверху и отступом снизу.</summary>
        public static RectTransform Between(this RectTransform rt, float top, float bottom, float l = 0, float r = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, bottom); rt.offsetMax = new Vector2(-r, -top);
            return rt;
        }

        /// <summary>Фиксированный размер в точке-якоре (ax, ay в 0..1) со смещением (x, y).</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Горизонтальный участок родителя [x0..x1] (доли), по высоте целиком.</summary>
        public static RectTransform Cols(this RectTransform rt, float x0, float x1, float padX = 0, float padY = 0)
        {
            rt.anchorMin = new Vector2(x0, 0); rt.anchorMax = new Vector2(x1, 1);
            rt.offsetMin = new Vector2(padX, padY); rt.offsetMax = new Vector2(-padX, -padY);
            return rt;
        }

        public static RectTransform H(this RectTransform rt, float h)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h; le.minHeight = h;
            return rt;
        }

        // ---------- элементы ----------

        public static Image Img(Transform parent, Color c, Sprite s = null, string name = "Img")
        {
            var rt = Node(name, parent);
            var i = rt.gameObject.AddComponent<Image>();
            i.color = c;
            if (s != null)
            {
                i.sprite = s;
                i.type = s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }
            i.raycastTarget = false;
            return i;
        }

        public static Image Panel(Transform parent, Color c) => Img(parent, c, Round, "Panel");

        public static Text Txt(Transform parent, string s, int size, Color c,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Node("Txt", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(Transform parent, string label, Color bg, Action onClick,
            int fontSize = 42, Color? fg = null)
        {
            var img = Img(parent, bg, Round, "Btn");
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (!string.IsNullOrEmpty(label))
            {
                var t = Txt(img.transform, label, fontSize, fg ?? Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.RT().Fill(6);
            }
            return b;
        }

        /// <summary>
        /// Фото игрока в скруглённой рамке. Если задана команда — в правом нижнем углу значок её логотипа.
        /// </summary>
        public static Image Portrait(Transform parent, PlayerData p, TeamData team, Color frame, float border = 6)
        {
            var root = Img(parent, frame, Round, "Portrait");
            var maskRt = Node("Mask", root.transform).Fill(border);
            var maskImg = maskRt.gameObject.AddComponent<Image>();
            maskImg.sprite = Round; maskImg.type = Image.Type.Sliced; maskImg.raycastTarget = false;
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            Color regionCol = p != null ? Pal.Regions[p.region] : Pal.Panel2;
            Img(maskRt, Color.Lerp(regionCol, Color.black, 0.45f), null, "Bg").RT().Fill();

            if (p != null && p.portraitTex != null)
            {
                var raw = Node("Photo", maskRt).Fill().gameObject.AddComponent<RawImage>();
                raw.texture = p.portraitTex; raw.raycastTarget = false;
            }
            else
            {
                string letter = p != null && !string.IsNullOrEmpty(p.nickname) ? p.nickname.Substring(0, 1).ToUpper() : "?";
                var t = Txt(maskRt, letter, 80, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.resizeTextForBestFit = true; t.resizeTextMaxSize = 200; t.resizeTextMinSize = 10;
                t.RT().Fill(10);
            }

            var flag = Roster.Flag(p);
            if (flag != null)
            {
                // флаг страны в левом верхнем углу фото (3:2, с тонкой тёмной рамкой)
                var fr = Img(root.transform, Pal.A(Pal.Bg, 0.9f), null, "FlagFrame").RT();
                fr.anchorMin = new Vector2(0.04f, 0.78f); fr.anchorMax = new Vector2(0.34f, 0.98f);
                fr.offsetMin = fr.offsetMax = Vector2.zero;
                var fi = Node("Flag", fr).Fill(2).gameObject.AddComponent<RawImage>();
                fi.texture = flag; fi.raycastTarget = false;
            }

            if (team != null)
            {
                // значок команды: логотип на светлом круге, масштабируется вместе с портретом
                var badge = Logo(root.transform, team, Circle);
                badge.anchorMin = new Vector2(0.66f, 0.02f); badge.anchorMax = new Vector2(0.98f, 0.34f);
                badge.offsetMin = badge.offsetMax = Vector2.zero;
            }
            return root;
        }

        static readonly Color LogoBg = Pal.Hex("EEF1F7");

        /// <summary>Картинка на светлой подложке (чтобы тёмные логотипы были видны на тёмном интерфейсе).</summary>
        public static RectTransform LogoPlate(Transform parent, Texture tex, string fallback, Sprite shape = null)
        {
            bool round = shape == Circle;
            var bg = Img(parent, LogoBg, shape ?? Round, "Logo");
            if (tex != null)
            {
                var raw = Node("Pic", bg.transform).gameObject.AddComponent<RawImage>();
                raw.texture = tex; raw.raycastTarget = false;
                float pad = round ? 0.17f : 0.1f;  // в круг квадрат вписывается с запасом
                var rt = raw.RT();
                rt.anchorMin = new Vector2(pad, pad); rt.anchorMax = new Vector2(1 - pad, 1 - pad);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else
            {
                var txt = Txt(bg.transform, string.IsNullOrEmpty(fallback) ? "?" : fallback.Substring(0, 1), 40, Pal.Bg,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                txt.resizeTextForBestFit = true; txt.resizeTextMinSize = 8; txt.resizeTextMaxSize = 80;
                txt.RT().Fill(4);
            }
            return bg.RT();
        }

        /// <summary>Флаг игрока (рамка 3:2) или null, если страны/картинки нет.</summary>
        public static RectTransform FlagIcon(Transform parent, PlayerData p)
        {
            var tex = Roster.Flag(p);
            if (tex == null) return null;
            var fr = Img(parent, Pal.A(Pal.Bg, 0.9f), null, "FlagIcon").RT();
            var fi = Node("Flag", fr).Fill(2).gameObject.AddComponent<RawImage>();
            fi.texture = tex; fi.raycastTarget = false;
            return fr;
        }

        /// <summary>Логотип команды на светлой подложке. Без картинки — первая буква названия.</summary>
        public static RectTransform Logo(Transform parent, TeamData t, Sprite shape = null) =>
            LogoPlate(parent, t != null ? t.logoTex : null, t != null ? t.name : "?", shape);

        /// <summary>Логотип региона (лиги). null, если картинки нет.</summary>
        public static RectTransform RegionLogo(Transform parent, int region)
        {
            var tex = Roster.RegionLogo(region);
            return tex != null ? LogoPlate(parent, tex, "", Round) : null;
        }

        /// <summary>Строка «логотип + название команды». Возвращает корень (ставь его через Top/At).</summary>
        public static RectTransform TeamTag(Transform parent, TeamData t, int fontSize, Color textColor, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var root = Node("TeamTag", parent);
            string label = t != null ? t.name : "без команды";
            var txt = Txt(root, label, fontSize, t != null ? textColor : Pal.Dim, align);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            float logo = fontSize * 1.5f;
            var lg = Logo(root, t);
            // логотип слева от текста: текст занимает всю ширину, отступ слева под логотип
            float textW = Mathf.Min(txt.preferredWidth, 2000);
            lg.anchorMin = lg.anchorMax = new Vector2(align == TextAnchor.MiddleLeft ? 0 : align == TextAnchor.MiddleRight ? 1 : 0.5f, 0.5f);
            lg.sizeDelta = new Vector2(logo, logo);
            float x = align == TextAnchor.MiddleLeft ? logo / 2
                    : align == TextAnchor.MiddleRight ? -textW - logo / 2 - 8
                    : -(textW + logo + 8) / 2 + logo / 2;
            lg.anchoredPosition = new Vector2(x, 0);
            txt.RT().Fill();
            if (align == TextAnchor.MiddleLeft) txt.RT().offsetMin = new Vector2(logo + 10, 0);
            else if (align == TextAnchor.MiddleCenter) txt.RT().anchoredPosition = new Vector2((logo + 8) / 2, 0);
            return root;
        }

        /// <summary>Вертикальный скролл. Элементы добавляются в content и должны иметь высоту (H()).</summary>
        public static RectTransform Scroll(Transform parent, out RectTransform content, int padding = 24, float spacing = 18)
        {
            var root = Node("Scroll", parent);
            var sr = root.gameObject.AddComponent<ScrollRect>();
            var vp = Node("Viewport", root).Fill();
            var vpImg = vp.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.001f);
            vp.gameObject.AddComponent<RectMask2D>();

            content = Node("Content", vp);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childControlHeight = true; vl.childControlWidth = true;
            vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            vl.spacing = spacing;
            vl.padding = new RectOffset(padding, padding, 16, 40);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vp; sr.content = content;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40;
            return root;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
