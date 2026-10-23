using System;
using System.Collections.Generic;
using UnityEngine;

namespace EsportsSim
{
    [Serializable]
    public class PlayerData
    {
        public int id;
        public string nickname;
        public string skill;    // скилл / инфа об игроке (просто текст)
        public string country;  // код страны для флага: "KR", "CN", "RU"... (Resources/Flags/kr.png)
        public int power;       // 50 (слабый) .. 75 (сильный)
        public int region;      // 0..4
        public string portrait; // картинка игрока: Resources/Portraits/<portrait>.png

        [NonSerialized] public Texture2D portraitTex;

    }

    [Serializable]
    public class TeamData
    {
        public int id;
        public string name;
        public int region;       // 0..4
        public int bonus;        // +1 .. +10 к силе
        public string logo;      // Resources/Logos/<logo>.png

        [NonSerialized] public Texture2D logoTex;
    }

    [Serializable] public class RosterFile { public string[] regions; public List<PlayerData> players; }
    [Serializable] public class TeamsFile { public List<TeamData> teams; }

    /// <summary>Игроки и команды. Данные: Resources/roster.json и Resources/teams.json.</summary>
    public static class Roster
    {
        public static string[] Regions = { "Регион 1", "Регион 2", "Регион 3", "Регион 4", "Регион 5" };
        public static List<PlayerData> Players = new List<PlayerData>();
        public static List<TeamData> Teams = new List<TeamData>();
        static readonly Dictionary<int, PlayerData> byId = new Dictionary<int, PlayerData>();
        static readonly Dictionary<int, TeamData> teamById = new Dictionary<int, TeamData>();
        static readonly Texture2D[] regionLogos = new Texture2D[5]; // Resources/Regions/r1..r5.png


        public static void Load()
        {
            var file = LoadJson<RosterFile>("roster");
            if (file == null || file.players == null || file.players.Count == 0) file = Fallback();
            if (file.regions != null && file.regions.Length >= 5) Regions = file.regions;
            Players = file.players;
            byId.Clear();
            foreach (var p in Players)
            {
                p.power = Mathf.Clamp(p.power, 50, 75);
                p.region = Mathf.Clamp(p.region, 0, 4);
                if (!string.IsNullOrEmpty(p.portrait)) p.portraitTex = Resources.Load<Texture2D>("Portraits/" + p.portrait);
                byId[p.id] = p;
            }

            for (int r = 0; r < 5; r++) regionLogos[r] = Resources.Load<Texture2D>("Regions/r" + (r + 1));
            WorldsLogo = Resources.Load<Texture2D>("Regions/worlds");
            ArenaBg = Resources.Load<Texture2D>("Arena/arena_bg");

            var tf = LoadJson<TeamsFile>("teams");
            Teams = tf?.teams ?? new List<TeamData>();
            if (Teams.Count == 0) Teams = FallbackTeams();
            teamById.Clear();
            foreach (var t in Teams)
            {
                t.bonus = Mathf.Clamp(t.bonus, 1, 10);
                t.region = Mathf.Clamp(t.region, 0, 4);
                if (!string.IsNullOrEmpty(t.logo)) t.logoTex = Resources.Load<Texture2D>("Logos/" + t.logo);
                teamById[t.id] = t;
            }
            ApplyOverrides();
        }

        // ================= пользовательские настройки силы / бонусов =================

        [Serializable]
        class Overrides
        {
            public List<int> players = new List<int>(); public List<int> powers = new List<int>();
            public List<int> teams = new List<int>(); public List<int> bonuses = new List<int>();
            public List<int> skillPlayers = new List<int>(); public List<string> skills = new List<string>();
        }

        const string OverridesKey = "esports_overrides_v1";
        static readonly Dictionary<int, int> basePower = new Dictionary<int, int>();
        static readonly Dictionary<int, int> baseBonus = new Dictionary<int, int>();
        static readonly Dictionary<int, string> baseSkill = new Dictionary<int, string>();

        public static string DefaultSkill(int playerId) => baseSkill.TryGetValue(playerId, out var v) ? v : "";

        public static void SetSkill(int playerId, string text)
        {
            var p = Get(playerId); if (p == null) return;
            p.skill = (text ?? "").Trim(); SaveOverrides();
        }

        public static int DefaultPower(int playerId) => basePower.TryGetValue(playerId, out var v) ? v : 0;
        public static int DefaultBonus(int teamId) => baseBonus.TryGetValue(teamId, out var v) ? v : 0;

        public static void SetPower(int playerId, int value)
        {
            var p = Get(playerId); if (p == null) return;
            p.power = Mathf.Clamp(value, 50, 75); SaveOverrides();
        }

        public static void SetBonus(int teamId, int value)
        {
            var t = Team(teamId); if (t == null) return;
            t.bonus = Mathf.Clamp(value, 1, 10); SaveOverrides();
        }

        public static void ResetOverrides()
        {
            foreach (var p in Players) p.power = DefaultPower(p.id);
            foreach (var t in Teams) t.bonus = DefaultBonus(t.id);
            foreach (var p in Players) p.skill = DefaultSkill(p.id);
            PlayerPrefs.DeleteKey(OverridesKey); PlayerPrefs.Save();
        }

        static void SaveOverrides()
        {
            var o = new Overrides();
            foreach (var p in Players) if (p.power != DefaultPower(p.id)) { o.players.Add(p.id); o.powers.Add(p.power); }
            foreach (var t in Teams) if (t.bonus != DefaultBonus(t.id)) { o.teams.Add(t.id); o.bonuses.Add(t.bonus); }
            foreach (var p in Players) if ((p.skill ?? "") != DefaultSkill(p.id)) { o.skillPlayers.Add(p.id); o.skills.Add(p.skill ?? ""); }
            PlayerPrefs.SetString(OverridesKey, JsonUtility.ToJson(o)); PlayerPrefs.Save();
        }

        static void ApplyOverrides()
        {
            basePower.Clear(); baseBonus.Clear(); baseSkill.Clear();
            foreach (var p in Players) { basePower[p.id] = p.power; baseSkill[p.id] = p.skill ?? ""; }
            foreach (var t in Teams) baseBonus[t.id] = t.bonus;
            var json = PlayerPrefs.GetString(OverridesKey, "");
            if (string.IsNullOrEmpty(json)) return;
            Overrides o;
            try { o = JsonUtility.FromJson<Overrides>(json); } catch { return; }
            if (o == null) return;
            for (int i = 0; i < o.players.Count && i < o.powers.Count; i++)
                if (byId.TryGetValue(o.players[i], out var p)) p.power = Mathf.Clamp(o.powers[i], 50, 75);
            for (int i = 0; i < o.teams.Count && i < o.bonuses.Count; i++)
                if (teamById.TryGetValue(o.teams[i], out var t)) t.bonus = Mathf.Clamp(o.bonuses[i], 1, 10);
            if (o.skillPlayers != null && o.skills != null)
                for (int i = 0; i < o.skillPlayers.Count && i < o.skills.Count; i++)
                    if (byId.TryGetValue(o.skillPlayers[i], out var p)) p.skill = o.skills[i];
        }

        static T LoadJson<T>(string name) where T : class
        {
            var ta = Resources.Load<TextAsset>(name);
            if (ta == null) return null;
            try { return JsonUtility.FromJson<T>(ta.text); }
            catch (Exception e) { Debug.LogError(name + ".json: " + e.Message); return null; }
        }

        public static PlayerData Get(int id) => byId.TryGetValue(id, out var p) ? p : null;
        public static bool Has(int id) => byId.ContainsKey(id);
        public static List<PlayerData> InRegion(int r) => Players.FindAll(p => p.region == r);

        public static Texture2D RegionLogo(int r) => r >= 0 && r < 5 ? regionLogos[r] : null;
        public static Texture2D WorldsLogo;   // Resources/Regions/worlds.png
        public static Texture2D ArenaBg;      // Resources/Arena/arena_bg.png

        static readonly Dictionary<string, Texture2D> flags = new Dictionary<string, Texture2D>();

        /// <summary>Флаг страны игрока (null, если страна не указана или картинки нет).</summary>
        public static Texture2D Flag(PlayerData p)
        {
            if (p == null || string.IsNullOrEmpty(p.country)) return null;
            string code = p.country.Trim().ToLowerInvariant();
            if (!flags.TryGetValue(code, out var tex))
            {
                tex = Resources.Load<Texture2D>("Flags/" + code);
                flags[code] = tex;
            }
            return tex;
        }

        public static TeamData Team(int id) => teamById.TryGetValue(id, out var t) ? t : null;
        public static bool HasTeam(int id) => teamById.ContainsKey(id);
        public static List<TeamData> TeamsInRegion(int r) => Teams.FindAll(t => t.region == r);

        /// <summary>Итоговая сила = сила игрока + бонус команды.</summary>
        public static int Power(PlayerData p, TeamData t) => p.power + (t != null ? t.bonus : 0);

        public static string PowerText(PlayerData p, TeamData t) =>
            t != null ? $"{p.power} +{t.bonus} = <b>{Power(p, t)}</b>" : $"<b>{p.power}</b>";

        static RosterFile Fallback()
        {
            var f = new RosterFile { players = new List<PlayerData>() };
            for (int i = 0; i < 100; i++)
                f.players.Add(new PlayerData
                {
                    id = i + 1, nickname = "Player" + (i + 1), skill = "—",
                    power = 50 + (i * 7) % 26, region = i / 20
                });
            return f;
        }

        static List<TeamData> FallbackTeams()
        {
            var list = new List<TeamData>();
            for (int r = 0; r < 5; r++)
            for (int k = 1; k <= 10; k++)
                list.Add(new TeamData
                {
                    id = r * 10 + k, name = $"Регион {r + 1} Команда {k}", region = r,
                    bonus = Mathf.Max(2, 11 - k)
                });
            return list;
        }
    }
}
