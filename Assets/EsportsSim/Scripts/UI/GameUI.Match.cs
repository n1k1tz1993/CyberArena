using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EsportsSim
{
    // ================= МАТЧ (Bo3) =================
    public partial class GameUI
    {
        class Side
        {
            public PlayerData p;
            public TeamData team;
            public Color color;
            public RectTransform pic;
            public Vector2 picHome;
            public Image flash;
            public RectTransform hpFill;
            public Text hpText;
            public Image[] actBg = new Image[3];
            public Text[] actTxt = new Text[3];
            public Image[] wins = new Image[2];
            public bool hpRightToLeft;
        }

        Side sideA, sideB;
        Text roundText, logText, bannerText;
        Image bannerBg;
        RectTransform matchRoot;
        readonly Queue<string> logLines = new Queue<string>();
        float speed = 1f;
        bool skip;
        readonly List<Button> speedBtns = new List<Button>();

        static readonly string[] ActNames = { "АТАКА", "ОТХОД", "ЗАЩИТА" };

        // Раскладка экрана боя (ширина 1080):
        //  ┌ верхняя панель ─────────────────────────┐
        //  │ [фото И1]  HP И1 / ник / команда / сила │
        //  │            ЧАТ (результаты ходов)       │
        //  │ ник / команда / сила / HP И2  [фото И2] │
        //  └ скорость / пропуск ─────────────────────┘
        const float Photo = 440, Edge = 24, BarTop = 140, ButtonsH = 150;

        void StartMatch(PlayerData a, TeamData ta, PlayerData b, TeamData tb, string title,
            Action<int, int> onFinished, Action onBack, bool allowRematch = false)
        {
            var s = NewScreen();
            matchRoot = s;
            skip = false;
            logLines.Clear();
            speedBtns.Clear();

            // Фон арены (заглушка Resources/Arena/arena_bg.png — можно заменить своей картинкой)
            if (Roster.ArenaBg != null)
            {
                var bg = UIKit.Node("Arena", s).Fill().gameObject.AddComponent<RawImage>();
                bg.texture = Roster.ArenaBg; bg.raycastTarget = false;
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = (float)Roster.ArenaBg.width / Roster.ArenaBg.height;
                UIKit.Img(s, Pal.A(Pal.Bg, 0.35f), null, "Dim").RT().Fill();
            }

            TopBar(s, title, onBack, 34);

            float top = BarTop + 20;
            sideA = BuildSide(s, a, ta, Pal.P1, true, top);
            sideB = BuildSide(s, b, tb, Pal.P2, false, ButtonsH + 20);

            // Чат боя — между игроками
            float chatTop = top + Photo + 24, chatBottom = ButtonsH + 20 + Photo + 24;
            var chat = UIKit.Panel(s, Pal.A(Pal.Panel, 0.88f));
            chat.RT().Between(chatTop, chatBottom, Edge, Edge);
            roundText = UIKit.Txt(chat.transform, "", 36, Pal.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            roundText.RT().Top(10, 56);
            UIKit.Img(chat.transform, Pal.A(Pal.Dim, 0.3f), null, "Line").RT().Top(70, 2, 24, 24);
            logText = UIKit.Txt(chat.transform, "", 32, Pal.Text, TextAnchor.LowerLeft);
            logText.RT().Between(80, 18, 26, 26);
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            logText.lineSpacing = 1.15f;

            // Скорость / пропуск
            var bar = UIKit.Node("Speed", s).Bottom(16, ButtonsH - 30, Edge, Edge);
            float[] speeds = { 1, 2, 4 };
            for (int i = 0; i < 3; i++)
            {
                float sp = speeds[i];
                var btn = UIKit.Btn(bar, "x" + sp, Pal.Panel2, () => { speed = sp; PaintSpeed(); }, 44);
                btn.RT().Cols(i * 0.2f, (i + 1) * 0.2f, 8, 0);
                speedBtns.Add(btn);
            }
            UIKit.Btn(bar, "ПРОПУСТИТЬ", Pal.Panel2, () => skip = true, 38).RT().Cols(0.6f, 1f, 8, 0);
            PaintSpeed();

            // Баннер поверх чата
            bannerBg = UIKit.Img(chat.transform, Pal.A(Pal.Bg, 0.9f), UIKit.Round, "Banner");
            bannerBg.RT().At(0.5f, 0.5f, 0, 0, 900, 230);
            bannerText = UIKit.Txt(bannerBg.transform, "", 60, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            bannerText.RT().Fill(10);
            bannerBg.gameObject.SetActive(false);

            StartCoroutine(RunMatch(a, ta, b, tb, onFinished, onBack, allowRematch, title));
        }

        void PaintSpeed()
        {
            float[] speeds = { 1, 2, 4 };
            for (int i = 0; i < speedBtns.Count; i++)
                speedBtns[i].image.color = Mathf.Approximately(speeds[i], speed) ? Pal.Accent : Pal.Panel2;
        }

        /// <summary>
        /// Игрок 1 (topLeft): фото в левом верхнем углу, справа — HP, ник, команда, сила, скилл, действия.
        /// Игрок 2: фото в правом нижнем углу, слева — то же зеркально (HP внизу).
        /// offset — отступ от верха (И1) или низа (И2) экрана.
        /// </summary>
        Side BuildSide(RectTransform s, PlayerData p, TeamData team, Color col, bool topLeft, float offset)
        {
            var side = new Side { p = p, team = team, color = col };
            var align = topLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

            var pic = UIKit.Portrait(s, p, team, col, 8);
            var prt = pic.RT();
            if (topLeft) { prt.anchorMin = prt.anchorMax = new Vector2(0, 1); prt.pivot = new Vector2(0, 1); prt.anchoredPosition = new Vector2(Edge, -offset); }
            else { prt.anchorMin = prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(1, 0); prt.anchoredPosition = new Vector2(-Edge, offset); }
            prt.sizeDelta = new Vector2(Photo, Photo);
            side.pic = prt;
            side.picHome = prt.anchoredPosition;
            side.flash = UIKit.Img(pic.transform, new Color(1, 1, 1, 0), UIKit.Round, "Flash");
            side.flash.RT().Fill();

            // Колонка информации рядом с фото
            var info = UIKit.Node(topLeft ? "InfoP1" : "InfoP2", s);
            if (topLeft) info.Top(offset, Photo, Edge + Photo + 24, Edge);
            else info.Bottom(offset, Photo, Edge, Edge + Photo + 24);

            // Строки сверху вниз; у игрока 2 порядок зеркальный (HP внизу, у края экрана)
            float[] y = topLeft ? new float[] { 0, 76, 146, 196, 240, 290, 370 } : new float[] { 380, 300, 230, 180, 136, 76, 0 };
            // [0] HP, [1] ник, [2] команда, [3] сила, [4] скилл, [5] действия, [6] победы в серии

            var hpBg = UIKit.Img(info, Pal.A(Pal.Bg, 0.85f), UIKit.Round, "HpBg");
            hpBg.RT().Top(y[0], 60);
            var fill = UIKit.Img(hpBg.transform, Pal.Good, UIKit.Round, "HpFill");
            side.hpFill = fill.RT();
            side.hpFill.anchorMin = Vector2.zero; side.hpFill.anchorMax = Vector2.one;
            side.hpFill.offsetMin = new Vector2(5, 5); side.hpFill.offsetMax = new Vector2(-5, -5);
            if (!topLeft) { side.hpFill.pivot = new Vector2(1, 0.5f); }
            side.hpText = UIKit.Txt(hpBg.transform, "100", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            side.hpText.RT().Fill();
            side.hpText.gameObject.AddComponent<Shadow>();
            side.hpRightToLeft = !topLeft;

            var nick = UIKit.Txt(info, p.nickname, 56, col, align, FontStyle.Bold);
            nick.RT().Top(y[1], 64);
            var flag = UIKit.FlagIcon(info, p);
            if (flag != null)
            {
                // флаг рядом с ником: у игрока 1 слева от ника, у игрока 2 — справа
                flag.anchorMin = flag.anchorMax = new Vector2(topLeft ? 0 : 1, 1);
                flag.pivot = new Vector2(topLeft ? 0 : 1, 1);
                flag.anchoredPosition = new Vector2(0, -y[1] - 10);
                flag.sizeDelta = new Vector2(66, 44);
                if (topLeft) nick.RT().offsetMin = new Vector2(80, nick.RT().offsetMin.y);
                else nick.RT().offsetMax = new Vector2(-80, nick.RT().offsetMax.y);
            }
            nick.resizeTextForBestFit = true; nick.resizeTextMinSize = 28; nick.resizeTextMaxSize = 56;
            nick.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            UIKit.TeamTag(info, team, 30, Pal.Text, align).Top(y[2], 46);
            UIKit.Txt(info, Roster.Regions[p.region], 28, Pal.Regions[p.region], align).RT().Top(y[3], 40);
            UIKit.Txt(info, string.IsNullOrEmpty(p.skill) ? "" : p.skill, 26, Pal.Accent, align).RT().Top(y[4], 40);

            for (int i = 0; i < 3; i++)
            {
                var bg = UIKit.Img(info, Pal.A(Pal.Panel, 0.9f), UIKit.Round, "Act");
                var rt = bg.RT();
                rt.anchorMin = rt.anchorMax = new Vector2(topLeft ? 0 : 1, 1);
                rt.pivot = new Vector2(topLeft ? 0 : 1, 1);
                int k = topLeft ? i : 2 - i;
                rt.anchoredPosition = new Vector2((topLeft ? 1 : -1) * k * 180, -y[5]);
                rt.sizeDelta = new Vector2(170, 64);
                var t = UIKit.Txt(bg.transform, ActNames[i], 26, Pal.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.RT().Fill();
                side.actBg[i] = bg; side.actTxt[i] = t;
            }

            for (int i = 0; i < 2; i++)
            {
                var w = UIKit.Img(info, Pal.A(Pal.Panel2, 0.9f), UIKit.Circle, "Win");
                var rt = w.RT();
                rt.anchorMin = rt.anchorMax = new Vector2(topLeft ? 0 : 1, 1);
                rt.pivot = new Vector2(topLeft ? 0 : 1, 1);
                rt.anchoredPosition = new Vector2((topLeft ? 1 : -1) * i * 56, -y[6] - 10);
                rt.sizeDelta = new Vector2(44, 44);
                side.wins[i] = w;
            }
            return side;
        }

        // ---------- ход матча ----------

        IEnumerator RunMatch(PlayerData a, TeamData ta, PlayerData b, TeamData tb, Action<int, int> onFinished, Action onBack,
            bool allowRematch, string title)
        {
            int w1 = 0, w2 = 0, round = 1;
            while (w1 < 2 && w2 < 2)
            {
                var f1 = new Fighter(a, ta);
                var f2 = new Fighter(b, tb);
                SetHp(sideA, f1.hp); SetHp(sideB, f2.hp);
                ShowActions(sideA, default); ShowActions(sideB, default);
                roundText.text = $"РАУНД {round}   ·   {w1} : {w2}";

                if (!skip)
                {
                    Log($"<color={Pal.Tag(Pal.Gold)}>— Раунд {round} —</color>");
                    yield return Banner($"РАУНД {round}", Pal.Text, 1.0f);
                }

                int winner;
                if (skip) winner = Combat.FinishRound(f1, f2, rng);
                else
                {
                    while (f1.Alive && f2.Alive && !skip)
                    {
                        var rep = Combat.Tick(f1, f2, rng);
                        PresentTick(rep, f1, f2);
                        yield return Wait(Combat.TickSeconds);
                    }
                    winner = Combat.FinishRound(f1, f2, rng); // если нажали «пропустить» посреди раунда
                }

                SetHp(sideA, f1.hp); SetHp(sideB, f2.hp);
                var ws = winner == 1 ? sideA : sideB;
                if (winner == 1) w1++; else w2++;
                ws.wins[(winner == 1 ? w1 : w2) - 1].color = Pal.Gold;
                roundText.text = $"РАУНД {round}   ·   {w1} : {w2}";

                if (!skip)
                {
                    Log($"<color={Pal.Tag(ws.color)}><b>{ws.p.nickname}</b></color> выигрывает раунд {round}!");
                    yield return Banner($"Раунд {round}\n<color={Pal.Tag(ws.color)}>{ws.p.nickname}</color>", Pal.Text, 1.6f);
                }
                round++;
            }

            ShowResult(a, ta, b, tb, w1, w2, onFinished, onBack, allowRematch, title);
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds && !skip)
            {
                t += Time.deltaTime * speed;
                yield return null;
            }
        }

        IEnumerator Banner(string text, Color c, float seconds)
        {
            bannerText.text = text;
            bannerText.color = c;
            bannerBg.gameObject.SetActive(true);
            yield return Wait(seconds);
            bannerBg.gameObject.SetActive(false);
        }

        void PresentTick(TickReport rep, Fighter f1, Fighter f2)
        {
            ShowActions(sideA, rep.a1);
            ShowActions(sideB, rep.a2);

            if (rep.a1.attack) PresentStrike(sideA, sideB, rep.onP2, 1);
            if (rep.a2.attack) PresentStrike(sideB, sideA, rep.onP1, -1);
            if (!rep.a1.attack && !rep.a2.attack)
                Log($"<color={Pal.Tag(Pal.Dim)}>…игроки выжидают</color>");

            SetHp(sideA, f1.hp);
            SetHp(sideB, f2.hp);
        }

        void PresentStrike(Side atk, Side def, Strike s, int dir)
        {
            StartCoroutine(Lunge(atk, dir));
            string an = $"<color={Pal.Tag(atk.color)}><b>{atk.p.nickname}</b></color>";
            string dn = $"<color={Pal.Tag(def.color)}><b>{def.p.nickname}</b></color>";
            int dmg = Mathf.RoundToInt(s.damage);
            string crit = s.crit ? " <color=#FFC940>КРИТ!</color>" : "";
            switch (s.result)
            {
                case HitResult.Dodged:
                    Log($"{an} бьёт — {dn} уклоняется");
                    Float(def, "УКЛОН", Pal.Accent);
                    break;
                case HitResult.Blocked:
                    Log($"{an} бьёт — {dn} блокирует, -{dmg}{crit}");
                    Float(def, "БЛОК -" + dmg, Pal.Dim);
                    StartCoroutine(Flash(def, new Color(0.6f, 0.8f, 1f, 0.35f)));
                    break;
                case HitResult.Hit:
                    Log($"{an} попадает по {dn}: -{dmg}{crit}");
                    Float(def, (s.crit ? "КРИТ -" : "-") + dmg, s.crit ? Pal.Gold : Pal.Bad);
                    StartCoroutine(Flash(def, new Color(1f, 0.2f, 0.25f, 0.5f)));
                    StartCoroutine(Shake(def));
                    break;
            }
        }

        void ShowActions(Side side, TickAction a)
        {
            bool[] on = { a.attack, a.retreat, a.defend };
            Color[] cols = { Pal.Bad, Pal.Accent, Pal.P2 };
            for (int i = 0; i < 3; i++)
            {
                side.actBg[i].color = on[i] ? cols[i] : Pal.Panel;
                side.actTxt[i].color = on[i] ? Color.white : Pal.Dim;
            }
        }

        void SetHp(Side side, float hp)
        {
            float k = Mathf.Clamp01(hp / Combat.MaxHp);
            if (side.hpRightToLeft) { side.hpFill.anchorMin = new Vector2(1 - Mathf.Max(k, 0.0001f), 0); side.hpFill.anchorMax = Vector2.one; }
            else { side.hpFill.anchorMin = Vector2.zero; side.hpFill.anchorMax = new Vector2(Mathf.Max(k, 0.0001f), 1); }
            side.hpFill.GetComponent<Image>().color = k > 0.5f ? Pal.Good : k > 0.25f ? Pal.Gold : Pal.Bad;
            side.hpFill.gameObject.SetActive(k > 0.01f);
            side.hpText.text = Mathf.CeilToInt(Mathf.Max(0, hp)).ToString();
        }

        void Log(string line)
        {
            logLines.Enqueue(line);
            while (logLines.Count > 14) logLines.Dequeue();
            logText.text = string.Join("\n", logLines);
        }

        // ---------- анимации ----------

        IEnumerator Lunge(Side s, int dir)
        {
            float d = 0.18f, t = 0;
            while (t < d)
            {
                t += Time.deltaTime * speed;
                float k = Mathf.Sin(Mathf.Clamp01(t / d) * Mathf.PI);
                if (s.pic) s.pic.anchoredPosition = s.picHome + new Vector2(dir * 40 * k, -dir * 40 * k);
                yield return null;
            }
            if (s.pic) s.pic.anchoredPosition = s.picHome;
        }

        IEnumerator Shake(Side s)
        {
            float d = 0.2f, t = 0;
            while (t < d)
            {
                t += Time.deltaTime * speed;
                if (s.pic) s.pic.anchoredPosition = s.picHome + UnityEngine.Random.insideUnitCircle * 14 * (1 - t / d);
                yield return null;
            }
            if (s.pic) s.pic.anchoredPosition = s.picHome;
        }

        IEnumerator Flash(Side s, Color c)
        {
            float d = 0.3f, t = 0;
            while (t < d)
            {
                t += Time.deltaTime * speed;
                if (s.flash) s.flash.color = Pal.A(c, c.a * (1 - t / d));
                yield return null;
            }
            if (s.flash) s.flash.color = new Color(1, 1, 1, 0);
        }

        void Float(Side s, string text, Color c)
        {
            var t = UIKit.Txt(matchRoot, text, 58, c, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            var rt = t.RT();
            rt.anchorMin = rt.anchorMax = s.pic.anchorMin;
            rt.sizeDelta = new Vector2(420, 80);
            var center = s.picHome + new Vector2((0.5f - s.pic.pivot.x) * s.pic.sizeDelta.x, (0.5f - s.pic.pivot.y) * s.pic.sizeDelta.y);
            rt.anchoredPosition = center + new Vector2(UnityEngine.Random.Range(-60f, 60f), 40);
            StartCoroutine(FloatUp(t));
        }

        IEnumerator FloatUp(Text t)
        {
            float d = 0.9f, e = 0;
            var rt = t.RT();
            var start = rt.anchoredPosition;
            var c = t.color;
            while (e < d && t)
            {
                e += Time.deltaTime * speed;
                float k = e / d;
                rt.anchoredPosition = start + new Vector2(0, 120 * k);
                t.color = Pal.A(c, 1 - k * k);
                yield return null;
            }
            if (t) Destroy(t.gameObject);
        }

        // ---------- итог ----------

        void ShowResult(PlayerData a, TeamData ta, PlayerData b, TeamData tb, int s1, int s2, Action<int, int> onFinished, Action onBack,
            bool allowRematch, string title)
        {
            var winner = s1 > s2 ? a : b;
            var wteam = s1 > s2 ? ta : tb;
            var wcol = s1 > s2 ? Pal.P1 : Pal.P2;

            var overlay = UIKit.Img(matchRoot, Pal.A(Pal.Bg, 0.94f), null, "Result");
            overlay.RT().Fill();
            overlay.raycastTarget = true; // блокируем нажатия под оверлеем

            UIKit.Txt(overlay.transform, "ПОБЕДИТЕЛЬ", 64, Pal.Gold, TextAnchor.MiddleCenter, FontStyle.Bold)
                .RT().At(0.5f, 0.5f, 0, 620, 1000, 90);
            UIKit.Portrait(overlay.transform, winner, wteam, Pal.Gold, 10).RT().At(0.5f, 0.5f, 0, 300, 480, 480);
            UIKit.Txt(overlay.transform, winner.nickname, 80, wcol, TextAnchor.MiddleCenter, FontStyle.Bold)
                .RT().At(0.5f, 0.5f, 0, -10, 1000, 100);
            UIKit.Txt(overlay.transform, winner.skill ?? "", 30, Pal.Accent).RT().At(0.5f, 0.5f, 0, -72, 1000, 46);
            UIKit.TeamTag(overlay.transform, wteam, 34, Pal.Text).At(0.5f, 0.5f, 0, -124, 1000, 50);
            UIKit.Txt(overlay.transform,
                    $"<color={Pal.Tag(Pal.P1)}>{a.nickname}</color>  <b>{s1} : {s2}</b>  <color={Pal.Tag(Pal.P2)}>{b.nickname}</color>",
                    52, Pal.Text, TextAnchor.MiddleCenter)
                .RT().At(0.5f, 0.5f, 0, -200, 1000, 80);

            float y = -330;
            if (allowRematch)
            {
                UIKit.Btn(overlay.transform, "РЕВАНШ", Pal.Panel2,
                        () => StartMatch(a, ta, b, tb, title, onFinished, onBack, true), 48)
                    .RT().At(0.5f, 0.5f, 0, y, 760, 140);
                y -= 170;
            }
            UIKit.Btn(overlay.transform, "ДАЛЕЕ", Pal.Accent, () => onFinished(s1, s2), 52, Pal.Bg)
                .RT().At(0.5f, 0.5f, 0, y, 760, 140);
        }
    }
}
