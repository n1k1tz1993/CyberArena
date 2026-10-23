using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EsportsSim
{
    // ================= СВОБОДНЫЙ БОЙ: ВЫБОР ИГРОКА → ВЫБОР КОМАНДЫ =================
    public partial class GameUI
    {
        int selA = -1, selB = -1;           // игроки
        int teamA = -1, teamB = -1;         // их команды
        readonly Dictionary<int, RectTransform> cellPics = new Dictionary<int, RectTransform>();
        readonly Dictionary<int, Text> cellBadges = new Dictionary<int, Text>();
        RectTransform selectBottom;

        const float SelectBottomH = 640;

        void ShowSelect()
        {
            var s = NewScreen();
            TopBar(s, "Свободный бой", ShowMenu);
            cellPics.Clear(); cellBadges.Clear();

            var scroll = UIKit.Scroll(s, out var content, 20, 14);
            scroll.Between(140, SelectBottomH);

            for (int r = 0; r < 5; r++)
            {
                var players = Roster.InRegion(r);
                int lines = Mathf.Max(1, (players.Count + 4) / 5);
                const float CellH = 290;
                var row = UIKit.Node("Region" + r, content).H(64 + lines * CellH);
                var rc = Pal.Regions[r];
                UIKit.Img(row, rc, UIKit.Round, "Bar").RT().At(0, 1, 14, -30, 12, 44);
                var rl = UIKit.RegionLogo(row, r);
                if (rl != null) rl.At(0, 1, 62, -31, 52, 52);
                UIKit.Txt(row, Roster.Regions[r].ToUpper(), 38, rc, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(8, 46, rl != null ? 100 : 36);

                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    int col = i % 5, line = i / 5;
                    var cell = UIKit.Node("Cell", row);
                    cell.anchorMin = new Vector2(col / 5f, 1); cell.anchorMax = new Vector2((col + 1) / 5f, 1);
                    cell.pivot = new Vector2(0.5f, 1);
                    cell.offsetMin = new Vector2(6, -64 - (line + 1) * CellH); cell.offsetMax = new Vector2(-6, -64 - line * CellH);

                    var holder = UIKit.Node("PicHolder", cell).Top(0, 190);
                    cellPics[p.id] = holder;

                    var hit = UIKit.Img(holder, new Color(0, 0, 0, 0.001f), null, "Hit");
                    hit.RT().Fill(); hit.raycastTarget = true;
                    var btn = hit.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    int id = p.id;
                    btn.onClick.AddListener(() => OnPick(id));

                    var badge = UIKit.Txt(holder, "", 44, Color.white, TextAnchor.UpperRight, FontStyle.Bold);
                    badge.RT().Fill(14);
                    badge.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
                    cellBadges[p.id] = badge;

                    var nick = UIKit.Txt(cell, p.nickname, 30, Pal.Text, TextAnchor.UpperCenter, FontStyle.Bold);
                    nick.RT().Top(198, 40);
                    nick.resizeTextForBestFit = true; nick.resizeTextMinSize = 18; nick.resizeTextMaxSize = 30;
                }
            }

            selectBottom = UIKit.Img(s, Pal.Panel, null, "Bottom").RT().Bottom(0, SelectBottomH);
            RefreshSelection();
        }

        public static Color PowerColor(int power)
        {
            float t = Mathf.Clamp01((power - 50) / 25f);
            return t < 0.5f ? Color.Lerp(Pal.Dim, Pal.Text, t * 2) : Color.Lerp(Pal.Text, Pal.Gold, (t - 0.5f) * 2);
        }

        void OnPick(int id)
        {
            // повторное нажатие снимает выбор
            if (selA == id) { selA = -1; teamA = -1; RefreshSelection(); return; }
            if (selB == id) { selB = -1; teamB = -1; RefreshSelection(); return; }

            int slot = selA < 0 ? 1 : selB < 0 ? 2 : 2; // оба заняты — меняем второго
            var p = Roster.Get(id);
            int current = slot == 1 ? teamA : teamB;
            TeamPicker(p, current, null, teamId =>
            {
                if (slot == 1) { selA = id; teamA = teamId; } else { selB = id; teamB = teamId; }
                RefreshSelection();
            });
        }

        void ClearSlot(int slot)
        {
            if (slot == 1) { selA = -1; teamA = -1; } else { selB = -1; teamB = -1; }
            RefreshSelection();
        }

        void RefreshSelection()
        {
            foreach (var kv in cellPics)
            {
                bool a = kv.Key == selA, b = kv.Key == selB;
                var holder = kv.Value;
                // пересобираем портрет: выбранный игрок показывается в форме своей команды
                var old = holder.Find("Portrait");
                if (old) Destroy(old.gameObject);
                var p = Roster.Get(kv.Key);
                var team = a ? Roster.Team(teamA) : b ? Roster.Team(teamB) : null;
                var pic = UIKit.Portrait(holder, p, team, a ? Pal.P1 : b ? Pal.P2 : Pal.Panel2, 6);
                pic.RT().Fill();
                pic.transform.SetAsFirstSibling();
                cellBadges[kv.Key].text = a ? "1" : b ? "2" : "";
            }

            UIKit.Clear(selectBottom);
            var pa = selA >= 0 ? Roster.Get(selA) : null;
            var pb = selB >= 0 ? Roster.Get(selB) : null;
            var ta = Roster.Team(teamA);
            var tb = Roster.Team(teamB);

            SlotCard(selectBottom, pa, ta, 1, 0f, 0.5f);
            SlotCard(selectBottom, pb, tb, 2, 0.5f, 1f);
            UIKit.Txt(selectBottom, "VS", 44, Pal.Dim, TextAnchor.MiddleCenter, FontStyle.Bold).RT().At(0.5f, 1, 0, -230, 90, 60);

            var fight = UIKit.Btn(selectBottom, "В БОЙ!", Pal.Accent, () => StartFreeMatch(pa, ta, pb, tb), 60, Pal.Bg);
            fight.RT().Bottom(26, 140, 40, 40);
            fight.interactable = pa != null && pb != null && ta != null && tb != null;
        }

        void SlotCard(RectTransform parent, PlayerData p, TeamData t, int slot, float x0, float x1)
        {
            var col = slot == 1 ? Pal.P1 : Pal.P2;
            var card = UIKit.Panel(parent, Pal.Panel2);
            card.RT().anchorMin = new Vector2(x0, 1); card.RT().anchorMax = new Vector2(x1, 1);
            card.RT().pivot = new Vector2(0.5f, 1);
            card.RT().offsetMin = new Vector2(slot == 1 ? 24 : 12, -450);
            card.RT().offsetMax = new Vector2(slot == 1 ? -12 : -24, -20);
            UIKit.Img(card.transform, col, UIKit.Round, "Stripe").RT().Top(0, 10, 30, 30);

            if (p == null)
            {
                UIKit.Txt(card.transform, $"Игрок {slot}\n<size=28>нажми на портрет,\nпотом выбери команду</size>", 40, Pal.Dim).RT().Fill();
                return;
            }

            card.raycastTarget = true;
            var b = card.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => ClearSlot(slot)); // нажатие по карточке снимает выбор

            UIKit.Portrait(card.transform, p, t, col, 5).RT().At(0.5f, 1, 0, -105, 160, 160);
            UIKit.Txt(card.transform, p.nickname, 40, Pal.Text, TextAnchor.UpperCenter, FontStyle.Bold).RT().Top(192, 46, 10, 10);
            UIKit.TeamTag(card.transform, t, 26, Pal.Text).Top(242, 40, 10, 10);
            UIKit.Txt(card.transform, Roster.Regions[p.region], 28, Pal.Regions[p.region], TextAnchor.UpperCenter)
                .RT().Top(288, 36, 10, 10);
            UIKit.Txt(card.transform, p.skill ?? "", 24, Pal.Accent, TextAnchor.UpperCenter).RT().Top(330, 70, 14, 14);
        }

        void StartFreeMatch(PlayerData a, TeamData ta, PlayerData b, TeamData tb)
        {
            if (a == null || b == null || ta == null || tb == null) return;
            StartMatch(a, ta, b, tb, "Свободный бой · до 2 побед",
                onFinished: (s1, s2) => ShowSelect(),
                onBack: ShowSelect,
                allowRematch: true);
        }

        // ================= ОКНО ВЫБОРА КОМАНДЫ (общее для свободного боя и сезона) =================

        /// <summary>
        /// Модальное окно: 10 команд региона игрока. Сверху — превью игрока в форме выбранной команды.
        /// isTaken(teamId) — команда занята (недоступна); может быть null.
        /// </summary>
        void TeamPicker(PlayerData p, int currentTeam, Func<int, bool> isTaken, Action<int> onPick)
        {
            var overlay = UIKit.Img(screen, Pal.A(Pal.Bg, 0.96f), null, "TeamPicker");
            overlay.RT().Fill();
            overlay.raycastTarget = true;
            var root = overlay.RT();

            var teams = Roster.TeamsInRegion(p.region);
            int chosen = currentTeam;
            if (Roster.Team(chosen) == null || (isTaken != null && isTaken(chosen))) chosen = -1;

            UIKit.Txt(root, $"Команда для {p.nickname}", 44, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold).RT().Top(20, 70);
            UIKit.Txt(root, Roster.Regions[p.region], 30, Pal.Regions[p.region]).RT().Top(86, 40);
            var prl = UIKit.RegionLogo(root, p.region);
            if (prl != null) prl.At(0, 1, 70, -55, 80, 80);

            var preview = UIKit.Node("Preview", root).Top(140, 360);
            var rows = new Dictionary<int, Image>();
            Button ok = null;

            void Repaint()
            {
                UIKit.Clear(preview);
                var t = Roster.Team(chosen);
                UIKit.Portrait(preview, p, t, t != null ? Pal.Gold : Pal.Panel2, 8).RT().At(0, 0.5f, 210, 0, 340, 340);
                var info = UIKit.Node("Info", preview);
                info.anchorMin = Vector2.zero; info.anchorMax = Vector2.one;
                info.offsetMin = new Vector2(410, 0); info.offsetMax = new Vector2(-30, 0);
                UIKit.Txt(info, p.nickname, 48, Pal.Text, TextAnchor.MiddleLeft, FontStyle.Bold).RT().Top(30, 60);
                UIKit.Txt(info, t != null ? t.name : "выбери команду ниже", 32, t != null ? Pal.Text : Pal.Dim, TextAnchor.MiddleLeft)
                    .RT().Top(100, 80);
                UIKit.Txt(info, p.skill ?? "", 28, Pal.Accent, TextAnchor.MiddleLeft)
                    .RT().Top(190, 50);
                foreach (var kv in rows) kv.Value.color = kv.Key == chosen ? Pal.Panel2 : Pal.Panel;
                if (ok) ok.interactable = t != null;
            }

            UIKit.Scroll(root, out var list, 24, 12).Between(520, 200);
            foreach (var t in teams)
            {
                bool taken = isTaken != null && isTaken(t.id) && t.id != currentTeam;
                var row = UIKit.Panel(list, Pal.Panel);
                row.RT().H(130);
                rows[t.id] = row;
                UIKit.Logo(row.transform, t).At(0, 0.5f, 74, 0, 100, 100);
                UIKit.Txt(row.transform, t.name, 34, taken ? Pal.Dim : Pal.Text, TextAnchor.MiddleLeft, FontStyle.Bold)
                    .RT().Between(14, 56, 140, 200);
                if (taken) UIKit.Txt(row.transform, "занята", 26, Pal.Bad, TextAnchor.MiddleLeft).RT().At(0, 0, 240, 36, 200, 36);

                if (!taken)
                {
                    row.raycastTarget = true;
                    var b = row.gameObject.AddComponent<Button>();
                    b.transition = Selectable.Transition.None;
                    int id = t.id;
                    b.onClick.AddListener(() => { chosen = id; Repaint(); });
                }
            }

            var bar = UIKit.Node("Buttons", root).Bottom(30, 140, 24, 24);
            UIKit.Btn(bar, "Отмена", Pal.Panel2, () => Destroy(overlay.gameObject), 40).RT().Cols(0, 0.4f, 6, 0);
            ok = UIKit.Btn(bar, "ВЫБРАТЬ", Pal.Accent, () =>
            {
                if (Roster.Team(chosen) == null) return;
                Destroy(overlay.gameObject);
                onPick(chosen);
            }, 46, Pal.Bg);
            ok.RT().Cols(0.4f, 1f, 6, 0);
            Repaint();
        }
    }
}
