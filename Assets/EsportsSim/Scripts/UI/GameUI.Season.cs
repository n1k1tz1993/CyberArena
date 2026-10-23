using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EsportsSim
{
    // ================= СЕЗОН: МЕНЮ РЕГИОНОВ → WILD CARD → ЧЕМПИОНАТ МИРА =================
    public partial class GameUI
    {
        const float NextPanelH = 470;
        bool resetArmed;

        // ---------- меню сезона ----------

        void ShowSeason()
        {
            var s = NewScreen();
            resetArmed = false;
            TopBar(s, $"Сезон {Season.D.number}", ShowMenu);

            Button reset = null;
            reset = UIKit.Btn(s, "сброс", Pal.Panel2, () =>
            {
                if (!resetArmed) { resetArmed = true; reset.GetComponentInChildren<Text>().text = "точно?"; return; }
                Season.ResetAll(); ShowSeason();
            }, 30);
            reset.RT().At(1, 1, -100, -70, 160, 90);

            bool finished = Season.Phase == Phase.Finished;
            UIKit.Scroll(s, out var c, 24, 18).Between(140, finished ? 250 : 0);

            var head = UIKit.Node("Head", c).H(90);
            UIKit.Txt(head, Season.PhaseName(), 38, Pal.Dim, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Fill();

            if (finished) ChampionBlock(c);

            SectionTitle(c, "РЕГИОНЫ");
            for (int r = 0; r < 5; r++) RegionCard(c, r);

            SectionTitle(c, "ТУРНИРЫ");
            WildCardCard(c);
            WorldsCard(c);
            HistoryBlock(c);

            if (finished)
            {
                var bottom = UIKit.Img(s, Pal.Panel, null, "Bottom").RT().Bottom(0, 250);
                UIKit.Btn(bottom, "НОВЫЙ СЕЗОН", Pal.Accent, () => { Season.NewSeason(); ShowSeason(); }, 56, Pal.Bg)
                    .RT().Bottom(40, 170, 40, 40);
            }
        }

        RectTransform Card(RectTransform c, float h, bool grey, bool locked, Action onTap)
        {
            var card = UIKit.Panel(c, grey || locked ? Pal.A(Pal.Panel, 0.55f) : Pal.Panel);
            card.RT().H(h);
            if (onTap != null && !locked)
            {
                card.raycastTarget = true;
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => onTap());
            }
            return card.RT();
        }

        /// <summary>Делает всё внутри карточки «серым» (сыгранный регион).</summary>
        static void GreyOut(RectTransform card)
        {
            foreach (var g in card.GetComponentsInChildren<Graphic>(true))
            {
                if (g.transform == card) continue;
                var col = g.color;
                float l = col.r * 0.3f + col.g * 0.59f + col.b * 0.11f;
                g.color = new Color(l, l, l, col.a * 0.75f);
            }
        }

        void RegionCard(RectTransform c, int r)
        {
            var st = Season.StateOf(r);
            bool done = st == RegionState.Done;
            var card = Card(c, done ? 260 : 170, done, false, () =>
            {
                if (Season.StateOf(r) == RegionState.NotStarted) Season.StartRegion(r);
                ShowRegion(r);
            });
            UIKit.Img(card, Pal.Regions[r], UIKit.Round, "Stripe").RT().At(0, 1, 16, -85, 10, 110);
            var rl = UIKit.RegionLogo(card, r);
            if (rl != null) rl.At(0, 1, 100, -85, 120, 120);
            UIKit.Txt(card, Roster.Regions[r], 42, Pal.Text, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(22, 60, 180, 30);
            string status = st == RegionState.NotStarted ? "нажми, чтобы начать драфт" : Season.RegionStateName(r);
            UIKit.Txt(card, status, 28, done ? Pal.Dim : Pal.Accent, TextAnchor.MiddleLeft).RT().Top(88, 50, 180, 30);
            if (!done)
            {
                UIKit.Txt(card, ">", 50, Pal.Dim, TextAnchor.MiddleRight, FontStyle.Bold).RT().Top(50, 70, 30, 30);
                return;
            }

            // сыгран: серый, но с чемпионом — логотип команды, фото и ник игрока + путёвки
            int champ = Season.Qualified(r, 0);
            var ct = Roster.Team(champ); var cp = Season.PlayerOf(champ);
            var line = UIKit.Node("Champion", card).Top(150, 96, 30, 30);
            UIKit.Logo(line, ct).At(0, 0.5f, 44, 0, 88, 88);
            UIKit.Portrait(line, cp, null, Pal.Gold, 4).RT().At(0, 0.5f, 144, 0, 92, 92);
            UIKit.Txt(line, $"<b>{(cp != null ? cp.nickname : "")}</b>\n<size=24>{(ct != null ? ct.name : "")} · чемпион</size>",
                32, Pal.Text, TextAnchor.MiddleLeft).RT().Fill().offsetMin = new Vector2(206, 0);
            for (int place = 1; place < 3; place++)
            {
                var t = Roster.Team(Season.Qualified(r, place));
                if (t != null) UIKit.Logo(line, t).At(1, 0.5f, -44 - (2 - place) * 100, 0, 80, 80);
            }
            GreyOut(card);
        }

        void WildCardCard(RectTransform c)
        {
            var ph = Season.Phase;
            bool locked = ph == Phase.Regions;
            bool done = ph > Phase.WildCard;
            var card = Card(c, 200, done, locked, ShowWildCard);
            UIKit.Txt(card, "WILD CARD", 44, Pal.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(24, 60, 30, 30);
            string sub;
            if (locked) sub = "откроется, когда будут сыграны все 5 регионов";
            else if (!done) sub = $"«царь горы» · ждут своей очереди: {Season.WildCardWaiting}";
            else sub = "победитель — 16-я команда чемпионата мира";
            UIKit.Txt(card, sub, 27, locked ? Pal.Dim : Pal.Text, TextAnchor.MiddleLeft).RT().Top(90, 80, 30, done ? 300 : 30);
            if (done)
            {
                var t = Roster.Team(Season.D.wcWinner); var p = Season.PlayerOf(Season.D.wcWinner);
                UIKit.Logo(card, t).At(1, 0.5f, -200, 0, 110, 110);
                UIKit.Portrait(card, p, null, Pal.Gold, 4).RT().At(1, 0.5f, -80, 0, 110, 110);
                GreyOut(card);
            }
        }

        void WorldsCard(RectTransform c)
        {
            var ph = Season.Phase;
            bool locked = ph < Phase.WorldsGroups;
            var card = Card(c, 260, false, locked, ShowWorlds);
            if (Roster.WorldsLogo != null)
                UIKit.LogoPlate(card, Roster.WorldsLogo, "W", UIKit.Round).At(0, 0.5f, 130, 0, 210, 210);
            UIKit.Txt(card, "ЧЕМПИОНАТ МИРА", 42, locked ? Pal.Dim : Pal.Text, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(40, 60, 260, 20);
            string sub = locked ? "15 команд из регионов + победитель Wild Card"
                       : ph == Phase.Finished ? "чемпион мира определён" : Season.PhaseName();
            UIKit.Txt(card, sub, 28, locked ? Pal.Dim : Pal.Accent, TextAnchor.MiddleLeft).RT().Top(110, 100, 260, 20);
            if (locked) GreyOut(card);
        }

        // ---------- общие элементы ----------

        void SectionTitle(RectTransform c, string text)
        {
            UIKit.Txt(UIKit.Node("Title", c).H(56), text, 32, Pal.Dim, TextAnchor.LowerLeft, FontStyle.Bold).RT().Fill();
        }

        static string PlayerLabel(PlayerData p) =>
            p == null ? $"<color={Pal.Tag(Pal.Dim)}>игрок не выбран</color>" : $"<color={Pal.Tag(Pal.Accent)}>{p.nickname}</color>";

        static int TeamPower(int teamId)
        {
            var p = Season.PlayerOf(teamId);
            return p != null ? Roster.Power(p, Roster.Team(teamId)) : 0;
        }

        void TableHead(RectTransform c)
        {
            var head = UIKit.Node("Head", c).H(36);
            UIKit.Txt(head, "команда", 24, Pal.Dim, TextAnchor.MiddleLeft).RT().Cols(0, 0.68f).offsetMin = new Vector2(166, 0);
            UIKit.Txt(head, "В-П", 24, Pal.Dim).RT().Cols(0.68f, 0.84f);
            UIKit.Txt(head, "карты", 24, Pal.Dim).RT().Cols(0.84f, 1f);
        }

        void TableRow(RectTransform c, int place, Standing row, Color mark, string note)
        {
            var t = Roster.Team(row.team);
            var p = Season.PlayerOf(row.team);
            var r = UIKit.Panel(c, Pal.Panel).RT().H(104);
            UIKit.Img(r, mark, UIKit.Round, "Mark").RT().At(0, 0.5f, 10, 0, 8, 70);
            UIKit.Txt(r, place.ToString(), 32, Pal.Dim, TextAnchor.MiddleCenter, FontStyle.Bold).RT().At(0, 0.5f, 44, 0, 50, 60);
            UIKit.Logo(r, t).At(0, 0.5f, 112, 0, 80, 80);
            string sub = PlayerLabel(p) + (note != "" ? $"  <color={Pal.Tag(mark)}>{note}</color>" : "");
            var nm = UIKit.Txt(r, $"<b>{t.name}</b>\n<size=22>{sub}</size>", 30, Pal.Text, TextAnchor.MiddleLeft).RT().Cols(0, 0.68f);
            nm.offsetMin = new Vector2(166, 0);
            UIKit.Txt(r, $"{row.wins}-{row.losses}", 32, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold).RT().Cols(0.68f, 0.84f);
            UIKit.Txt(r, $"{row.mapsWon}:{row.mapsLost}", 28, Pal.Dim).RT().Cols(0.84f, 1f);
        }

        void MatchRow(RectTransform c, MatchRecord m, string tag, bool isNext = false)
        {
            var ta = Roster.Team(m.t1); var tb = Roster.Team(m.t2);
            var pa = Season.PlayerOf(m.t1); var pb = Season.PlayerOf(m.t2);
            var r = UIKit.Panel(c, isNext ? Pal.Panel2 : Pal.Panel).RT().H(tag != null ? 134 : 106);
            float y = 0;
            if (tag != null)
            {
                UIKit.Txt(r, tag, 24, Pal.Gold, TextAnchor.UpperCenter, FontStyle.Bold).RT().Top(8, 30);
                y = -14;
            }
            bool aw = m.played && m.Winner == m.t1, bw = m.played && m.Winner == m.t2;
            UIKit.Logo(r, ta).At(0, 0.5f, 56, y, 72, 72);
            UIKit.Txt(r, (aw ? "<b>" + ta.name + "</b>" : ta.name) + $"\n<size=20>{PlayerLabel(pa)}</size>", 26,
                m.played && !aw ? Pal.Dim : Pal.Text, TextAnchor.MiddleLeft).RT().At(0, 0.5f, 106 + 150, y, 300, 80);
            UIKit.Logo(r, tb).At(1, 0.5f, -56, y, 72, 72);
            UIKit.Txt(r, (bw ? "<b>" + tb.name + "</b>" : tb.name) + $"\n<size=20>{PlayerLabel(pb)}</size>", 26,
                m.played && !bw ? Pal.Dim : Pal.Text, TextAnchor.MiddleRight).RT().At(1, 0.5f, -106 - 150, y, 300, 80);
            string mid = m.played ? $"{m.s1} : {m.s2}" : (isNext ? "далее" : "vs");
            UIKit.Txt(r, mid, m.played ? 38 : 28, m.played ? Pal.Text : (isNext ? Pal.Accent : Pal.Dim),
                TextAnchor.MiddleCenter, FontStyle.Bold).RT().At(0.5f, 0.5f, 0, y, 140, 60);
        }

        /// <summary>Нижняя панель «следующий матч»: смотреть / быстрый результат / досчитать этап.</summary>
        void NextMatchPanel(RectTransform s, MatchRecord m, Action refresh, Action simStage)
        {
            var bottom = UIKit.Img(s, Pal.Panel, null, "Bottom").RT().Bottom(0, NextPanelH);
            var ta = Roster.Team(m.t1); var tb = Roster.Team(m.t2);
            var pa = Season.PlayerOf(m.t1); var pb = Season.PlayerOf(m.t2);
            string title = Season.MatchTitle(m).Replace($"Сезон {Season.D.number} · ", "");
            UIKit.Txt(bottom, "СЛЕДУЮЩИЙ МАТЧ · " + title, 26, Pal.Dim, TextAnchor.MiddleCenter, FontStyle.Bold).RT().Top(16, 40, 20, 20);

            UIKit.Portrait(bottom, pa, ta, Pal.P1, 4).RT().At(0, 1, 90, -130, 120, 120);
            UIKit.Portrait(bottom, pb, tb, Pal.P2, 4).RT().At(1, 1, -90, -130, 120, 120);
            UIKit.Txt(bottom, $"<b>{(pa != null ? pa.nickname : "?")}</b>\n<size=24>{ta.name}</size>", 32, Pal.P1,
                TextAnchor.MiddleLeft).RT().At(0, 1, 320, -130, 300, 130);
            UIKit.Txt(bottom, $"<b>{(pb != null ? pb.nickname : "?")}</b>\n<size=24>{tb.name}</size>", 32, Pal.P2,
                TextAnchor.MiddleRight).RT().At(1, 1, -320, -130, 300, 130);
            UIKit.Txt(bottom, "VS", 36, Pal.Dim, TextAnchor.MiddleCenter, FontStyle.Bold).RT().At(0.5f, 1, 0, -130, 80, 60);

            var bar = UIKit.Node("Buttons", bottom).Bottom(30, 230, 24, 24);
            UIKit.Btn(bar, "СМОТРЕТЬ МАТЧ", Pal.Accent, () => WatchMatch(m, refresh), 46, Pal.Bg).RT().Top(0, 120);
            var row = UIKit.Node("Row", bar).Bottom(0, 96);
            UIKit.Btn(row, "Быстрый результат", Pal.Panel2, () => { Season.QuickSim(m, rng); refresh(); }, 32).RT().Cols(0, 0.5f, 6, 0);
            UIKit.Btn(row, "Досчитать этап", Pal.Panel2, () => { simStage(); refresh(); }, 32).RT().Cols(0.5f, 1f, 6, 0);
        }

        void WatchMatch(MatchRecord m, Action back)
        {
            var a = Season.PlayerOf(m.t1); var b = Season.PlayerOf(m.t2);
            if (a == null || b == null) return;
            StartMatch(a, Roster.Team(m.t1), b, Roster.Team(m.t2), Season.MatchTitle(m),
                onFinished: (s1, s2) => { Season.Report(m, s1, s2, rng); back(); },
                onBack: back);
        }

        // ---------- регион ----------

        void ShowRegion(int r)
        {
            if (Season.StateOf(r) == RegionState.Draft) { ShowDraft(r); return; }
            var s = NewScreen();
            TopBar(s, Roster.Regions[r], ShowSeason, 44);
            var st = Season.StateOf(r);
            var next = Season.NextInRegion(r);

            UIKit.Scroll(s, out var c, 24, 18).Between(140, next != null ? NextPanelH : 0);

            var box = UIKit.Panel(c, Pal.Panel2).RT().H(150);
            UIKit.Img(box, Pal.Regions[r], UIKit.Round, "Stripe").RT().At(0, 0.5f, 18, 0, 12, 100);
            var rl = UIKit.RegionLogo(box, r);
            if (rl != null) rl.At(1, 0.5f, -80, 0, 110, 110);
            UIKit.Txt(box, Roster.Regions[r], 44, Pal.Text, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(18, 62, 50, 160);
            UIKit.Txt(box, Season.RegionStateName(r), 30, Pal.Dim, TextAnchor.MiddleLeft).RT().Top(82, 50, 50, 160);

            if (st == RegionState.Done)
            {
                SectionTitle(c, "ПУТЁВКИ НА ЧЕМПИОНАТ МИРА");
                string[] places = { "Чемпион", "2-е место", "3-е место" };
                for (int k = 0; k < 3; k++)
                {
                    int tid = Season.Qualified(r, k);
                    var t = Roster.Team(tid); var p = Season.PlayerOf(tid);
                    var row = UIKit.Panel(c, Pal.Panel).RT().H(120);
                    UIKit.Logo(row, t).At(0, 0.5f, 64, 0, 92, 92);
                    UIKit.Portrait(row, p, null, k == 0 ? Pal.Gold : Pal.Panel2, 4).RT().At(0, 0.5f, 170, 0, 96, 96);
                    UIKit.Txt(row, $"<b>{(p != null ? p.nickname : "")}</b>\n<size=24>{t.name}</size>", 32, Pal.Text, TextAnchor.MiddleLeft)
                        .RT().Fill().offsetMin = new Vector2(236, 0);
                    UIKit.Txt(row, places[k], 28, k == 0 ? Pal.Gold : Pal.Dim, TextAnchor.MiddleRight, FontStyle.Bold).RT().Fill(24);
                }
            }

            SectionTitle(c, "ТАБЛИЦА ЛИГИ");
            TableHead(c);
            var table = Season.Standings(Stage.League, r);
            for (int i = 0; i < table.Count; i++)
            {
                Color mark = i < 2 ? Pal.Gold : i < 6 ? Pal.Good : Pal.Panel;
                string note = i < 2 ? "в полуфинал" : i < 6 ? "плей-ин" : "";
                TableRow(c, i + 1, table[i], mark, note);
            }

            if (st >= RegionState.PlayIn)
            {
                SectionTitle(c, "ПЛЕЙ-ОФФ");
                foreach (var m in Season.Matches(Stage.PlayIn, r)) MatchRow(c, m, "Плей-ин", m == next);
                foreach (var m in Season.Matches(Stage.LeagueSemi, r)) MatchRow(c, m, "Полуфинал", m == next);
                foreach (var m in Season.Matches(Stage.LeagueFinal, r).OrderBy(x => -x.kind))
                    MatchRow(c, m, m.kind == 1 ? "Матч за 3-е место" : "Финал", m == next);
            }

            var all = Season.Matches(Stage.League, r);
            var played = all.Where(m => m.played).ToList();
            if (st == RegionState.League)
            {
                SectionTitle(c, $"МАТЧИ ЛИГИ · сыграно {played.Count} из {all.Count}");
                foreach (var m in all.Where(m => !m.played).Take(6)) MatchRow(c, m, null, m == next);
            }
            if (played.Count > 0)
            {
                SectionTitle(c, "РЕЗУЛЬТАТЫ ЛИГИ");
                for (int i = played.Count - 1; i >= Mathf.Max(0, played.Count - 8); i--) MatchRow(c, played[i], null);
            }

            if (next != null) NextMatchPanel(s, next, () => ShowRegion(r), () => Season.SimRegionStage(r, rng));
        }

        // ---------- драфт ----------

        void ShowDraft(int r)
        {
            if (Season.StateOf(r) != RegionState.Draft) { ShowRegion(r); return; }
            var s = NewScreen();
            TopBar(s, "Драфт · " + Roster.Regions[r], ShowSeason, 40);

            var picker = Season.CurrentPicker(r);
            var order = Season.DraftOrder(r);

            // кто выбирает
            var head = UIKit.Img(s, Pal.Panel2, null, "Picker").RT().Top(140, 170);
            UIKit.Txt(head, $"ПИК {Season.PickNumber(r)} ИЗ {order.Count} · ВЫБИРАЕТ", 26, Pal.Dim, TextAnchor.MiddleLeft, FontStyle.Bold)
                .RT().Top(14, 36, 160, 24);
            UIKit.Logo(head, picker).At(0, 0.5f, 84, 0, 120, 120);
            UIKit.Txt(head, $"<b>{picker.name}</b>", 40, Pal.Text, TextAnchor.MiddleLeft)
                .RT().Top(56, 60, 160, 24);
            UIKit.Txt(head, "нажми на игрока, чтобы выбрать его в эту команду", 24, Pal.Dim, TextAnchor.MiddleLeft).RT().Top(118, 36, 160, 24);

            UIKit.Scroll(s, out var c, 20, 14).Between(310, 200);

            // свободные игроки: сетка 4 в ряд
            var av = Season.Available(r);
            SectionTitle(c, $"СВОБОДНЫЕ ИГРОКИ · {av.Count}");
            const int Cols = 4; const float CellH = 360;
            int lines = (av.Count + Cols - 1) / Cols;
            var grid = UIKit.Node("Grid", c).H(Mathf.Max(1, lines) * CellH);
            for (int i = 0; i < av.Count; i++)
            {
                var p = av[i];
                int col = i % Cols, line = i / Cols;
                var cell = UIKit.Node("Cell", grid);
                cell.anchorMin = new Vector2((float)col / Cols, 1); cell.anchorMax = new Vector2((float)(col + 1) / Cols, 1);
                cell.pivot = new Vector2(0.5f, 1);
                cell.offsetMin = new Vector2(6, -(line + 1) * CellH); cell.offsetMax = new Vector2(-6, -line * CellH);
                var pic = UIKit.Portrait(cell, p, null, Pal.Panel2, 5);
                pic.RT().Top(0, 230, 8, 8);
                pic.raycastTarget = true;
                var b = pic.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                int id = p.id;
                b.onClick.AddListener(() => { Season.Pick(r, id, rng); ShowDraft(r); });
                var nick = UIKit.Txt(cell, p.nickname, 28, Pal.Text, TextAnchor.UpperCenter, FontStyle.Bold);
                nick.RT().Top(236, 36);
                nick.resizeTextForBestFit = true; nick.resizeTextMinSize = 16; nick.resizeTextMaxSize = 28;
                var sk = UIKit.Txt(cell, p.skill ?? "", 18, Pal.Accent, TextAnchor.UpperCenter);
                sk.RT().Top(274, 76);
                sk.lineSpacing = 0.9f;
            }

            // команды и их выбор
            SectionTitle(c, "ПОРЯДОК ВЫБОРА");
            for (int k = 0; k < order.Count; k++)
            {
                var t = order[k]; var p = Season.PlayerOf(t.id);
                bool now = picker != null && t.id == picker.id;
                var row = UIKit.Panel(c, now ? Pal.Panel2 : Pal.Panel).RT().H(100);
                UIKit.Txt(row, (k + 1).ToString(), 30, now ? Pal.Accent : Pal.Dim, TextAnchor.MiddleCenter, FontStyle.Bold).RT().At(0, 0.5f, 36, 0, 50, 60);
                UIKit.Logo(row, t).At(0, 0.5f, 106, 0, 76, 76);
                UIKit.Txt(row, t.name, 28, Pal.Text, TextAnchor.MiddleLeft)
                    .RT().Fill().offsetMin = new Vector2(160, 0);
                if (p != null)
                {
                    UIKit.Portrait(row, p, null, Pal.Panel2, 3).RT().At(1, 0.5f, -50, 0, 80, 80);
                    UIKit.Txt(row, $"<b>{p.nickname}</b>", 26, Pal.Accent, TextAnchor.MiddleRight).RT().Fill().offsetMax = new Vector2(-104, 0);
                }
                else if (now) UIKit.Txt(row, "выбирает…", 26, Pal.Accent, TextAnchor.MiddleRight).RT().Fill(24);
            }

            var bar = UIKit.Img(s, Pal.Panel, null, "Bottom").RT().Bottom(0, 200);
            var inner = UIKit.Node("Buttons", bar).Bottom(30, 140, 24, 24);
            UIKit.Btn(inner, "Автовыбор\nза команду", Pal.Panel2, () => { Season.AutoPick(r, rng); ShowDraft(r); }, 32).RT().Cols(0, 0.45f, 6, 0);
            UIKit.Btn(inner, "АВТОДРАФТ ДО КОНЦА", Pal.Accent, () => { Season.AutoDraft(r, rng); ShowRegion(r); }, 36, Pal.Bg).RT().Cols(0.45f, 1f, 6, 0);
        }

        // ---------- Wild Card ----------

        void ShowWildCard()
        {
            var s = NewScreen();
            TopBar(s, "Wild Card", ShowSeason, 44);
            var next = Season.Phase == Phase.WildCard ? Season.NextWildCard : null;
            UIKit.Scroll(s, out var c, 24, 16).Between(140, next != null ? NextPanelH : 0);

            var info = UIKit.Panel(c, Pal.Panel2).RT().H(150);
            UIKit.Txt(info, "«Царь горы»: победитель остаётся, проигравший вылетает, его место занимает следующая команда. " +
                            "Последний матч — финал. Победитель едет на чемпионат мира 16-й командой.",
                26, Pal.Dim, TextAnchor.MiddleLeft).RT().Fill(22);

            var ms = Season.Matches(Stage.WildCard);
            if (Season.D.wcWinner >= 0)
            {
                var t = Roster.Team(Season.D.wcWinner); var p = Season.PlayerOf(Season.D.wcWinner);
                var box = UIKit.Panel(c, Pal.Panel).RT().H(200);
                UIKit.Txt(box, "ПОБЕДИТЕЛЬ WILD CARD", 30, Pal.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(20, 44, 380, 20);
                UIKit.Logo(box, t).At(0, 0.5f, 100, 0, 150, 150);
                UIKit.Portrait(box, p, t, Pal.Gold, 5).RT().At(0, 0.5f, 270, 0, 150, 150);
                UIKit.Txt(box, $"<b>{t.name}</b>\n{(p != null ? p.nickname : "")}", 34, Pal.Text, TextAnchor.MiddleLeft).RT().Top(70, 110, 380, 20);
            }
            else if (next != null)
            {
                // текущий «царь горы» и его серия
                int holder = next.t1;
                int streak = 0;
                for (int i = ms.Count - 1; i >= 0 && ms[i].played && ms[i].Winner == holder; i--) streak++;
                var t = Roster.Team(holder); var p = Season.PlayerOf(holder);
                var box = UIKit.Panel(c, Pal.Panel).RT().H(190);
                UIKit.Txt(box, "ЦАРЬ ГОРЫ", 28, Pal.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(18, 40, 380, 20);
                UIKit.Logo(box, t).At(0, 0.5f, 100, 0, 140, 140);
                UIKit.Portrait(box, p, t, Pal.Gold, 5).RT().At(0, 0.5f, 260, 0, 140, 140);
                UIKit.Txt(box, $"<b>{t.name}</b> · {(p != null ? p.nickname : "")}\n<size=26>побед подряд: {streak} · ждут: {Season.WildCardWaiting}</size>",
                    32, Pal.Text, TextAnchor.MiddleLeft).RT().Top(62, 110, 380, 20);
            }

            if (ms.Count > 0)
            {
                SectionTitle(c, $"МАТЧИ · {ms.Count(m => m.played)} сыграно");
                for (int i = ms.Count - 1; i >= 0; i--) MatchRow(c, ms[i], ms[i].kind == 1 ? "ФИНАЛ WILD CARD" : null, ms[i] == next);
            }

            if (Season.Phase == Phase.WildCard && Season.D.wcNext < Season.D.wcQueue.Count)
            {
                SectionTitle(c, "ОЧЕРЕДЬ");
                foreach (var id in Season.D.wcQueue.Skip(Season.D.wcNext))
                {
                    var t = Roster.Team(id);
                    var row = UIKit.Panel(c, Pal.Panel).RT().H(84);
                    UIKit.Logo(row, t).At(0, 0.5f, 50, 0, 64, 64);
                    UIKit.Txt(row, $"{t.name}  <size=22>{PlayerLabel(Season.PlayerOf(id))}  · {Roster.Regions[t.region]}</size>", 28, Pal.Text,
                        TextAnchor.MiddleLeft).RT().Fill().offsetMin = new Vector2(100, 0);
                }
            }

            if (next != null) NextMatchPanel(s, next, ShowWildCard, () => Season.SimWildCard(rng));
        }

        // ---------- чемпионат мира ----------

        void ShowWorlds()
        {
            var s = NewScreen();
            TopBar(s, "Чемпионат мира", ShowSeason, 44);
            var next = Season.NextWorlds;
            UIKit.Scroll(s, out var c, 24, 18).Between(140, next != null ? NextPanelH : 0);

            var head = UIKit.Panel(c, Pal.Panel2).RT().H(260);
            if (Roster.WorldsLogo != null) UIKit.LogoPlate(head, Roster.WorldsLogo, "W", UIKit.Round).At(0, 0.5f, 130, 0, 220, 220);
            UIKit.Txt(head, "ЧЕМПИОНАТ МИРА", 44, Pal.Text, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(60, 64, 270, 20);
            UIKit.Txt(head, Season.PhaseName(), 30, Pal.Accent, TextAnchor.MiddleLeft).RT().Top(130, 60, 270, 20);

            if (Season.Phase == Phase.Finished) ChampionBlock(c);

            if (Season.Phase >= Phase.WorldsQuarter)
            {
                SectionTitle(c, "ПЛЕЙ-ОФФ");
                string[] qf = { "Четвертьфинал · A1 – B2", "Четвертьфинал · B1 – A2", "Четвертьфинал · C1 – D2", "Четвертьфинал · D1 – C2" };
                var q = Season.Matches(Stage.WorldsQuarter, 0);
                for (int i = 0; i < q.Count; i++) MatchRow(c, q[i], qf[Mathf.Min(i, 3)], q[i] == next);
                foreach (var m in Season.Matches(Stage.WorldsSemi, 0)) MatchRow(c, m, "Полуфинал", m == next);
                foreach (var m in Season.Matches(Stage.WorldsFinal, 0)) MatchRow(c, m, "ФИНАЛ ЧЕМПИОНАТА МИРА", m == next);
            }

            for (int g = 0; g < 4; g++)
            {
                SectionTitle(c, $"ГРУППА {Season.GroupLetter(g)}");
                TableHead(c);
                var table = Season.Standings(Stage.WorldsGroups, g);
                for (int i = 0; i < table.Count; i++)
                    TableRow(c, i + 1, table[i], i < 2 ? Pal.Good : Pal.Panel, SeedLabel(table[i].team));
            }

            if (next != null) NextMatchPanel(s, next, ShowWorlds, () => Season.SimWorldsStage(rng));
        }

        static string SeedLabel(int teamId)
        {
            if (teamId == Season.D.wcWinner) return "Wild Card";
            int i = Season.D.qualified.IndexOf(teamId);
            return i < 0 ? "" : $"{Roster.Regions[i / 3]} #{i % 3 + 1}";
        }

        void ChampionBlock(RectTransform c)
        {
            var t = Roster.Team(Season.D.champion);
            if (t == null) return;
            var p = Season.PlayerOf(t.id);
            var box = UIKit.Panel(c, Pal.Panel2).RT().H(560);
            UIKit.Txt(box, $"ЧЕМПИОН МИРА · СЕЗОН {Season.D.number}", 42, Pal.Gold, TextAnchor.MiddleCenter, FontStyle.Bold).RT().Top(22, 60);
            UIKit.Logo(box, t).At(0.5f, 1, -170, -250, 260, 260);
            UIKit.Portrait(box, p, t, Pal.Gold, 8).RT().At(0.5f, 1, 170, -250, 300, 300);
            UIKit.Txt(box, $"{t.name} · {(p != null ? p.nickname : "")}", 50, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold).RT().Top(420, 70, 20, 20);
            UIKit.Txt(box, Roster.Regions[t.region], 28, Pal.Dim).RT().Top(494, 44, 20, 20);
        }

        void HistoryBlock(RectTransform c)
        {
            var h = Season.D.historyTeams;
            if (h.Count == 0) return;
            SectionTitle(c, "ЗАЛ СЛАВЫ");
            int first = Mathf.Max(0, h.Count - 10);
            for (int i = h.Count - 1; i >= first; i--)
            {
                var t = Roster.Team(h[i]);
                if (t == null) continue;
                int seasonNo = Season.D.number - (Season.Phase == Phase.Finished ? 0 : 1) - (h.Count - 1 - i);
                string who = i < Season.D.historyPlayers.Count ? Season.D.historyPlayers[i] : "";
                var r = UIKit.Panel(c, Pal.Panel).RT().H(84);
                UIKit.Txt(r, $"Сезон {seasonNo}", 28, Pal.Dim, TextAnchor.MiddleLeft).RT().Fill(24);
                UIKit.Logo(r, t).At(0.5f, 0.5f, -60, 0, 60, 60);
                UIKit.Txt(r, $"<b>{t.name}</b>" + (who != "" ? $"  <size=22>{who}</size>" : ""), 28, Pal.Gold, TextAnchor.MiddleRight).RT().Fill(24);
            }
        }
    }
}
