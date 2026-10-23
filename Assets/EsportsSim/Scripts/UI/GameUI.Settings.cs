using System;
using UnityEngine;
using UnityEngine.UI;

namespace EsportsSim
{
    // ================= НАСТРОЙКИ: СИЛА И ОПИСАНИЯ ИГРОКОВ, БОНУСЫ КОМАНД =================
    // Единственное место в игре, где видны числа. Изменения сохраняются на устройстве
    // и применяются поверх roster.json / teams.json (кнопка «Сбросить» возвращает исходные).
    public partial class GameUI
    {
        static readonly string[] TabNames = { "LCS", "LEC", "КЛ", "LPL", "LCK", "Команды" };
        bool settingsResetArmed;

        void ShowSettings(int tab)
        {
            var s = NewScreen();
            settingsResetArmed = false;
            TopBar(s, "Настройки", ShowMenu);

            // вкладки: 5 регионов + команды
            var tabs = UIKit.Node("Tabs", s).Top(150, 100, 16, 16);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int k = i;
                float w = 1f / TabNames.Length;
                var b = UIKit.Btn(tabs, k < 5 ? (Roster.Regions[k].Length <= 4 ? Roster.Regions[k] : TabNames[k]) : TabNames[k],
                    k == tab ? Pal.Accent : Pal.Panel2, () => ShowSettings(k), 28, k == tab ? Pal.Bg : Pal.Text);
                b.RT().Cols(i * w, (i + 1) * w, 4, 0);
            }

            UIKit.Scroll(s, out var c, 20, 12).Between(260, 190);
            if (tab < 5)
            {
                UIKit.Txt(UIKit.Node("Hint", c).H(70), $"{Roster.Regions[tab]} · сила (50–75) и описание (нажми, чтобы изменить)", 28, Pal.Dim, TextAnchor.MiddleLeft).RT().Fill();
                foreach (var p in Roster.InRegion(tab)) PlayerPowerRow(c, p, tab);
            }
            else
            {
                UIKit.Txt(UIKit.Node("Hint", c).H(70), "Бонус команды к силе игрока (от 1 до 10)", 28, Pal.Dim, TextAnchor.MiddleLeft).RT().Fill();
                for (int r = 0; r < 5; r++)
                {
                    UIKit.Txt(UIKit.Node("Region", c).H(56), Roster.Regions[r].ToUpper(), 30, Pal.Regions[r], TextAnchor.LowerLeft, FontStyle.Bold).RT().Fill();
                    foreach (var t in Roster.TeamsInRegion(r)) TeamBonusRow(c, t);
                }
            }

            var bar = UIKit.Img(s, Pal.Panel, null, "Bottom").RT().Bottom(0, 190);
            UIKit.Txt(bar, "Сохраняется автоматически", 26, Pal.Dim).RT().Top(14, 40);
            Button reset = null;
            reset = UIKit.Btn(bar, "Сбросить всё к исходным", Pal.Panel2, () =>
            {
                if (!settingsResetArmed) { settingsResetArmed = true; reset.GetComponentInChildren<Text>().text = "Точно сбросить?"; return; }
                Roster.ResetOverrides(); ShowSettings(tab);
            }, 34);
            reset.RT().Bottom(30, 110, 40, 40);
        }

        void PlayerPowerRow(RectTransform c, PlayerData p, int tab)
        {
            var row = UIKit.Panel(c, Pal.Panel).RT().H(150);
            UIKit.Portrait(row, p, null, Pal.Panel2, 4).RT().At(0, 0.5f, 66, 0, 108, 108);
            UIKit.Txt(row, $"<b>{p.nickname}</b>", 32, Pal.Text, TextAnchor.MiddleLeft).RT().Top(14, 48, 136, 330);

            // описание — нажми, чтобы изменить
            bool changed = (p.skill ?? "") != Roster.DefaultSkill(p.id);
            var sk = UIKit.Img(row, Pal.Panel2, UIKit.Round, "SkillBtn");
            sk.RT().Top(66, 70, 130, 330);
            sk.raycastTarget = true;
            var btn = sk.gameObject.AddComponent<Button>();
            btn.targetGraphic = sk;
            btn.onClick.AddListener(() => EditSkill(p, tab));
            string label = string.IsNullOrEmpty(p.skill) ? $"<color={Pal.Tag(Pal.Dim)}>добавить описание…</color>" : p.skill;
            var st = UIKit.Txt(sk.transform, label + "  >", 22, changed ? Pal.Gold : Pal.Accent, TextAnchor.MiddleLeft);
            st.RT().Fill(12);

            Stepper(row, p.power, 50, 75, () => Roster.DefaultPower(p.id), v => Roster.SetPower(p.id, v));
        }

        /// <summary>Окно редактирования описания игрока (поле ввода + Сохранить/Отмена/Исходное).</summary>
        void EditSkill(PlayerData p, int tab)
        {
            var overlay = UIKit.Img(screen, Pal.A(Pal.Bg, 0.96f), null, "EditSkill");
            overlay.RT().Fill();
            overlay.raycastTarget = true;
            var root = overlay.RT();

            UIKit.Portrait(root, p, null, Pal.Panel2, 6).RT().At(0.5f, 1, 0, -230, 220, 220);
            UIKit.Txt(root, p.nickname, 50, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold).RT().Top(360, 70);
            UIKit.Txt(root, "Описание игрока (показывается в профиле и в бою)", 28, Pal.Dim).RT().Top(440, 50, 40, 40);

            // поле ввода
            var box = UIKit.Img(root, Pal.Panel2, UIKit.Round, "Input");
            box.RT().Top(510, 200, 40, 40);
            box.raycastTarget = true;
            var input = box.gameObject.AddComponent<InputField>();
            var text = UIKit.Txt(box.transform, "", 36, Pal.Text, TextAnchor.UpperLeft);
            text.RT().Fill(22);
            text.supportRichText = false;
            text.raycastTarget = false;
            var ph = UIKit.Txt(box.transform, "например: Ветеран. Огромный боевой опыт.", 32, Pal.Dim, TextAnchor.UpperLeft, FontStyle.Italic);
            ph.RT().Fill(22);
            input.textComponent = text;
            input.placeholder = ph;
            input.lineType = InputField.LineType.MultiLineNewline;
            input.characterLimit = 80;
            input.targetGraphic = box;
            input.text = p.skill ?? "";

            var bar = UIKit.Node("Buttons", root).Top(740, 130, 40, 40);
            UIKit.Btn(bar, "Отмена", Pal.Panel2, () => Destroy(overlay.gameObject), 36).RT().Cols(0, 0.3f, 6, 0);
            UIKit.Btn(bar, "Исходное", Pal.Panel2, () => input.text = Roster.DefaultSkill(p.id), 32).RT().Cols(0.3f, 0.6f, 6, 0);
            UIKit.Btn(bar, "СОХРАНИТЬ", Pal.Accent, () =>
            {
                Roster.SetSkill(p.id, input.text);
                ShowSettings(tab);
            }, 38, Pal.Bg).RT().Cols(0.6f, 1f, 6, 0);
        }

        void TeamBonusRow(RectTransform c, TeamData t)
        {
            var row = UIKit.Panel(c, Pal.Panel).RT().H(110);
            UIKit.Logo(row, t).At(0, 0.5f, 60, 0, 84, 84);
            var name = UIKit.Txt(row, $"<b>{t.name}</b>", 30, Pal.Text, TextAnchor.MiddleLeft).RT();
            name.Fill(); name.offsetMin = new Vector2(118, 0); name.offsetMax = new Vector2(-330, 0);
            Stepper(row, t.bonus, 1, 10, () => Roster.DefaultBonus(t.id), v => Roster.SetBonus(t.id, v));
        }

        /// <summary>[−] число [+] справа в строке. Число золотое, если отличается от исходного.</summary>
        void Stepper(RectTransform row, int start, int min, int max, Func<int> def, Action<int> set)
        {
            int value = start;
            var box = UIKit.Node("Stepper", row).At(1, 0.5f, -170, 0, 310, 90);
            var txt = UIKit.Txt(box, "", 44, Pal.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            txt.RT().Cols(0.32f, 0.68f);
            void Paint() { txt.text = value.ToString(); txt.color = value != def() ? Pal.Gold : Pal.Text; }
            void Change(int d)
            {
                int v = Mathf.Clamp(value + d, min, max);
                if (v == value) return;
                value = v; set(v); Paint();
            }
            UIKit.Btn(box, "-", Pal.Panel2, () => Change(-1), 52).RT().Cols(0f, 0.32f, 2, 0);
            UIKit.Btn(box, "+", Pal.Panel2, () => Change(+1), 52).RT().Cols(0.68f, 1f, 2, 0);
            Paint();
        }
    }
}
