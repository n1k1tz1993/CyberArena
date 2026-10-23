using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EsportsSim
{
    /// <summary>Тип матча (поле MatchRecord.stage).</summary>
    public enum Stage
    {
        League = 0,        // лига региона: каждый с каждым
        PlayIn = 1,        // 3 vs 6, 4 vs 5
        LeagueSemi = 2,    // 1-е и 2-е места против победителей плей-ина
        LeagueFinal = 3,   // финал (kind 0) и матч за 3-е место (kind 1)
        WildCard = 4,      // «царь горы»; kind 1 — финал
        WorldsGroups = 5,
        WorldsQuarter = 6,
        WorldsSemi = 7,
        WorldsFinal = 8
    }

    /// <summary>Состояние региона в сезоне.</summary>
    public enum RegionState { NotStarted = 0, Draft = 1, League = 2, PlayIn = 3, Semi = 4, Final = 5, Done = 6 }

    /// <summary>Общая фаза сезона.</summary>
    public enum Phase { Regions = 0, WildCard = 1, WorldsGroups = 2, WorldsQuarter = 3, WorldsSemi = 4, WorldsFinal = 5, Finished = 6 }

    [Serializable]
    public class MatchRecord
    {
        public int stage;
        public int group;  // регион (для лиг) или группа ЧМ (0..3)
        public int t1, t2; // id команд
        public int s1, s2;
        public bool played;
        public int kind;
        public int Winner => s1 > s2 ? t1 : t2;
        public int Loser => s1 > s2 ? t2 : t1;
    }

    public class Standing
    {
        public int team, played, wins, losses, mapsWon, mapsLost;
        public int Diff => mapsWon - mapsLost;
    }

    [Serializable]
    public class SeasonData
    {
        public int number = 1;
        public int phase;
        public List<int> regionState = new List<int> { 0, 0, 0, 0, 0 };
        public List<MatchRecord> matches = new List<MatchRecord>();
        public List<int> draftPlayers = new List<int>(); // draftPlayers[i] играет за draftTeams[i]
        public List<int> draftTeams = new List<int>();
        public List<int> qualified = new List<int>();     // [регион*3 + место], -1 — ещё нет
        public List<int> wcQueue = new List<int>();       // порядок выхода команд в Wild Card
        public int wcNext;                                // индекс следующего претендента
        public int wcWinner = -1;
        public List<int> groups = new List<int>();        // 16 команд, группа = индекс / 4
        public int champion = -1;
        public string championPlayer = "";
        public List<int> historyTeams = new List<int>();
        public List<string> historyPlayers = new List<string>();
    }

    /// <summary>
    /// Сезон: 5 регионов в любом порядке (драфт → лига → плей-офф, топ-3 едут на ЧМ),
    /// затем Wild Card «царь горы» среди всех остальных команд (победитель — 16-я команда ЧМ),
    /// затем чемпионат мира: 4 группы по корзинам, каждый с каждым дважды, плей-офф.
    /// </summary>
    public static class Season
    {
        const string Key = "esports_season_v4";
        public static SeasonData D;

        public static Phase Phase => (Phase)D.phase;
        public static RegionState StateOf(int r) => (RegionState)D.regionState[r];
        public static bool AllRegionsDone => Enumerable.Range(0, 5).All(r => StateOf(r) == RegionState.Done);
        public static string GroupLetter(int g) => "ABCD".Substring(Mathf.Clamp(g, 0, 3), 1);

        // ================= сохранение =================

        public static void Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            D = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { D = JsonUtility.FromJson<SeasonData>(json); } catch { D = null; }
            }
            bool broken = D == null || D.matches == null || D.regionState == null || D.regionState.Count != 5
                          || D.draftPlayers == null || D.draftTeams == null || D.draftPlayers.Count != D.draftTeams.Count
                          || D.matches.Any(m => !Roster.HasTeam(m.t1) || !Roster.HasTeam(m.t2))
                          || D.draftPlayers.Any(p => !Roster.Has(p)) || D.draftTeams.Any(t => !Roster.HasTeam(t));
            if (broken) { D = Fresh(1, null, null); Save(); }
            if (D.historyTeams == null) D.historyTeams = new List<int>();
            if (D.historyPlayers == null) D.historyPlayers = new List<string>();
            if (D.wcQueue == null) D.wcQueue = new List<int>();
            if (D.groups == null) D.groups = new List<int>();
            if (D.qualified == null || D.qualified.Count != 15) D.qualified = Enumerable.Repeat(-1, 15).ToList();
        }

        static SeasonData Fresh(int number, List<int> ht, List<string> hp) => new SeasonData
        {
            number = number,
            qualified = Enumerable.Repeat(-1, 15).ToList(),
            historyTeams = ht ?? new List<int>(),
            historyPlayers = hp ?? new List<string>()
        };

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(D));
            PlayerPrefs.Save();
        }

        public static void NewSeason() { D = Fresh(D.number + 1, D.historyTeams, D.historyPlayers); Save(); }
        public static void ResetAll() { D = Fresh(1, null, null); Save(); }

        // ================= игроки команд =================

        public static PlayerData PlayerOf(int teamId)
        {
            int i = D.draftTeams.IndexOf(teamId);
            return i >= 0 ? Roster.Get(D.draftPlayers[i]) : null;
        }

        public static int TeamIdOf(int playerId)
        {
            int i = D.draftPlayers.IndexOf(playerId);
            return i >= 0 ? D.draftTeams[i] : -1;
        }

        public static TeamData TeamOf(int playerId) => Roster.Team(TeamIdOf(playerId));

        // ================= драфт региона =================

        public static void StartRegion(int r)
        {
            if (Phase != Phase.Regions || StateOf(r) != RegionState.NotStarted) return;
            D.regionState[r] = (int)RegionState.Draft;
            Save();
        }

        /// <summary>Очерёдность выбора: команды в порядке списка региона.</summary>
        public static List<TeamData> DraftOrder(int r) => Roster.TeamsInRegion(r);

        /// <summary>Команда, которая выбирает сейчас (null — драфт завершён).</summary>
        public static TeamData CurrentPicker(int r) => DraftOrder(r).FirstOrDefault(t => PlayerOf(t.id) == null);

        public static List<PlayerData> Available(int r) =>
            Roster.InRegion(r).Where(p => TeamIdOf(p.id) < 0).OrderByDescending(p => p.power).ToList();

        public static int PickNumber(int r) => DraftOrder(r).Count(t => PlayerOf(t.id) != null) + 1;

        public static bool Pick(int r, int playerId, System.Random rng)
        {
            if (StateOf(r) != RegionState.Draft) return false;
            var team = CurrentPicker(r);
            var p = Roster.Get(playerId);
            if (team == null || p == null || p.region != r || TeamIdOf(playerId) >= 0) return false;
            D.draftPlayers.Add(playerId);
            D.draftTeams.Add(team.id);
            if (CurrentPicker(r) == null) StartLeague(r);
            Save();
            return true;
        }

        /// <summary>Выбор за команду: сильнейший с небольшим случайным разбросом.</summary>
        public static bool AutoPick(int r, System.Random rng)
        {
            var av = Available(r);
            if (av.Count == 0) return false;
            var best = av.OrderByDescending(p => p.power + rng.NextDouble() * 8).First();
            return Pick(r, best.id, rng);
        }

        public static void AutoDraft(int r, System.Random rng)
        {
            int guard = 0;
            while (StateOf(r) == RegionState.Draft && guard++ < 100 && AutoPick(r, rng)) { }
        }

        static void StartLeague(int r)
        {
            D.regionState[r] = (int)RegionState.League;
            D.matches.AddRange(RoundRobin(DraftOrder(r).Select(t => t.id).ToList(), Stage.League, r, false));
        }

        // ================= запросы =================

        public static List<MatchRecord> Matches(Stage st, int group) =>
            D.matches.Where(m => m.stage == (int)st && m.group == group).ToList();

        public static List<MatchRecord> Matches(Stage st) => D.matches.Where(m => m.stage == (int)st).ToList();

        public static MatchRecord NextInRegion(int r) =>
            D.matches.FirstOrDefault(m => !m.played && m.group == r && m.stage <= (int)Stage.LeagueFinal);

        public static MatchRecord NextWildCard => D.matches.FirstOrDefault(m => !m.played && m.stage == (int)Stage.WildCard);

        public static MatchRecord NextWorlds => D.matches.FirstOrDefault(m => !m.played && m.stage >= (int)Stage.WorldsGroups);

        public static int Qualified(int r, int place) => D.qualified[r * 3 + place];

        public static List<int> GroupTeams(Stage st, int group)
        {
            var ids = new List<int>();
            foreach (var m in Matches(st, group))
            {
                if (!ids.Contains(m.t1)) ids.Add(m.t1);
                if (!ids.Contains(m.t2)) ids.Add(m.t2);
            }
            return ids;
        }

        public static List<Standing> Standings(Stage st, int group)
        {
            var ms = Matches(st, group);
            var table = GroupTeams(st, group).Select(id => new Standing { team = id }).ToDictionary(s => s.team);
            foreach (var m in ms.Where(m => m.played))
            {
                var a = table[m.t1]; var b = table[m.t2];
                a.played++; b.played++;
                a.mapsWon += m.s1; a.mapsLost += m.s2;
                b.mapsWon += m.s2; b.mapsLost += m.s1;
                if (m.s1 > m.s2) { a.wins++; b.losses++; } else { b.wins++; a.losses++; }
            }
            var list = table.Values.ToList();
            list.Sort((x, y) =>
            {
                if (x.team == y.team) return 0;
                if (x.wins != y.wins) return y.wins.CompareTo(x.wins);
                if (x.Diff != y.Diff) return y.Diff.CompareTo(x.Diff);
                if (x.mapsWon != y.mapsWon) return y.mapsWon.CompareTo(x.mapsWon);
                int h = 0;
                foreach (var m in ms.Where(m => m.played && ((m.t1 == x.team && m.t2 == y.team) || (m.t1 == y.team && m.t2 == x.team))))
                    h += m.Winner == x.team ? -1 : 1;
                if (h != 0) return h;
                return x.team.CompareTo(y.team);
            });
            return list;
        }

        public static string RegionStateName(int r)
        {
            switch (StateOf(r))
            {
                case RegionState.NotStarted: return "не начат";
                case RegionState.Draft: return $"драфт · пик {PickNumber(r)} из {DraftOrder(r).Count}";
                case RegionState.League:
                    var ms = Matches(Stage.League, r);
                    return $"лига · {ms.Count(m => m.played)} из {ms.Count}";
                case RegionState.PlayIn: return "плей-ин";
                case RegionState.Semi: return "полуфиналы";
                case RegionState.Final: return "финал";
                default: return "сыгран";
            }
        }

        public static string PhaseName()
        {
            switch (Phase)
            {
                case Phase.Regions:
                    int done = Enumerable.Range(0, 5).Count(r => StateOf(r) == RegionState.Done);
                    return $"регионы · сыграно {done} из 5";
                case Phase.WildCard: return "Wild Card";
                case Phase.WorldsGroups: return "ЧМ · групповой этап";
                case Phase.WorldsQuarter: return "ЧМ · четвертьфиналы";
                case Phase.WorldsSemi: return "ЧМ · полуфиналы";
                case Phase.WorldsFinal: return "ЧМ · финал";
                default: return "сезон завершён";
            }
        }

        public static string MatchTitle(MatchRecord m)
        {
            string reg = m.group >= 0 && m.group < 5 ? Roster.Regions[m.group] : "";
            string name;
            switch ((Stage)m.stage)
            {
                case Stage.League: name = $"{reg} · лига"; break;
                case Stage.PlayIn: name = $"{reg} · плей-ин"; break;
                case Stage.LeagueSemi: name = $"{reg} · полуфинал"; break;
                case Stage.LeagueFinal: name = m.kind == 1 ? $"{reg} · матч за 3-е место" : $"{reg} · финал"; break;
                case Stage.WildCard: name = m.kind == 1 ? "Wild Card · финал" : "Wild Card"; break;
                case Stage.WorldsGroups: name = $"ЧМ · группа {GroupLetter(m.group)}"; break;
                case Stage.WorldsQuarter: name = "ЧМ · четвертьфинал"; break;
                case Stage.WorldsSemi: name = "ЧМ · полуфинал"; break;
                default: name = "ЧМ · финал"; break;
            }
            return $"Сезон {D.number} · {name}";
        }

        // ================= ход сезона =================

        public static void Report(MatchRecord m, int s1, int s2, System.Random rng)
        {
            if (m.played) return;
            m.s1 = s1; m.s2 = s2; m.played = true;
            if (m.stage <= (int)Stage.LeagueFinal) AdvanceRegion(m.group, rng);
            else if (m.stage == (int)Stage.WildCard) AdvanceWildCard(m, rng);
            else AdvanceWorlds();
            Save();
        }

        public static void QuickSim(MatchRecord m, System.Random rng)
        {
            var a = PlayerOf(m.t1); var b = PlayerOf(m.t2);
            if (a == null || b == null || m.played) return;
            Combat.SimMatch(a, Roster.Team(m.t1), b, Roster.Team(m.t2), rng, out int x, out int y);
            Report(m, x, y, rng);
        }

        /// <summary>Досчитать текущий этап региона (лига / плей-ин / полуфиналы / финалы).</summary>
        public static void SimRegionStage(int r, System.Random rng)
        {
            var st = StateOf(r);
            if (st < RegionState.League || st == RegionState.Done) return;
            MatchRecord m;
            int guard = 0;
            while (StateOf(r) == st && (m = NextInRegion(r)) != null && guard++ < 500) QuickSim(m, rng);
        }

        public static void SimWildCard(System.Random rng)
        {
            MatchRecord m;
            int guard = 0;
            while (Phase == Phase.WildCard && (m = NextWildCard) != null && guard++ < 500) QuickSim(m, rng);
        }

        public static void SimWorldsStage(System.Random rng)
        {
            var ph = Phase;
            MatchRecord m;
            int guard = 0;
            while (Phase == ph && (m = NextWorlds) != null && guard++ < 500) QuickSim(m, rng);
        }

        static void AdvanceRegion(int r, System.Random rng)
        {
            if (NextInRegion(r) != null) return;
            switch (StateOf(r))
            {
                case RegionState.League:
                {
                    var t = Standings(Stage.League, r);
                    D.regionState[r] = (int)RegionState.PlayIn;
                    Add(Stage.PlayIn, r, t[2].team, t[5].team);
                    Add(Stage.PlayIn, r, t[3].team, t[4].team);
                    break;
                }
                case RegionState.PlayIn:
                {
                    var seeds = Standings(Stage.League, r).Select(s => s.team).ToList();
                    var winners = Matches(Stage.PlayIn, r).Select(m => m.Winner).OrderBy(x => seeds.IndexOf(x)).ToList();
                    D.regionState[r] = (int)RegionState.Semi;
                    Add(Stage.LeagueSemi, r, seeds[0], winners[1]); // 1-е место — против самого низкого посева
                    Add(Stage.LeagueSemi, r, seeds[1], winners[0]);
                    break;
                }
                case RegionState.Semi:
                {
                    var semis = Matches(Stage.LeagueSemi, r);
                    D.regionState[r] = (int)RegionState.Final;
                    Add(Stage.LeagueFinal, r, semis[0].Loser, semis[1].Loser, 1);   // за 3-е место
                    Add(Stage.LeagueFinal, r, semis[0].Winner, semis[1].Winner, 0); // финал
                    break;
                }
                case RegionState.Final:
                {
                    var fin = Matches(Stage.LeagueFinal, r);
                    var final = fin.First(m => m.kind == 0);
                    var third = fin.First(m => m.kind == 1);
                    D.qualified[r * 3] = final.Winner;
                    D.qualified[r * 3 + 1] = final.Loser;
                    D.qualified[r * 3 + 2] = third.Winner;
                    D.regionState[r] = (int)RegionState.Done;
                    if (AllRegionsDone) StartWildCard(rng);
                    break;
                }
            }
        }

        // ---------- Wild Card: «царь горы» ----------

        public static List<int> WildCardTeams() => Roster.Teams.Select(t => t.id).Where(id => !D.qualified.Contains(id)).ToList();

        static void StartWildCard(System.Random rng)
        {
            D.phase = (int)Phase.WildCard;
            D.wcQueue = WildCardTeams().OrderBy(_ => rng.Next()).ToList();
            D.wcNext = 2;
            Add(Stage.WildCard, 0, D.wcQueue[0], D.wcQueue[1], D.wcQueue.Count == 2 ? 1 : 0);
        }

        /// <summary>Сколько претендентов ещё ждут своей очереди.</summary>
        public static int WildCardWaiting => Mathf.Max(0, D.wcQueue.Count - D.wcNext);

        static void AdvanceWildCard(MatchRecord m, System.Random rng)
        {
            if (D.wcNext < D.wcQueue.Count)
            {
                int challenger = D.wcQueue[D.wcNext++];
                bool final = D.wcNext >= D.wcQueue.Count;
                Add(Stage.WildCard, 0, m.Winner, challenger, final ? 1 : 0);
                return;
            }
            D.wcWinner = m.Winner;
            DrawWorlds(rng);
        }

        // ---------- чемпионат мира ----------

        /// <summary>Жеребьёвка: в каждой группе 1-е, 2-е и 3-е место разных лиг + одна из «остальных».</summary>
        static void DrawWorlds(System.Random rng)
        {
            List<int> Pot(int place) => Enumerable.Range(0, 5).Select(r => Qualified(r, place)).OrderBy(_ => rng.Next()).ToList();
            var firsts = Pot(0); var seconds = Pot(1); var thirds = Pot(2);
            var pot4 = new List<int> { firsts[4], seconds[4], thirds[4], D.wcWinner }.OrderBy(_ => rng.Next()).ToList();
            D.groups = new List<int>();
            for (int g = 0; g < 4; g++)
            {
                D.groups.Add(firsts[g]); D.groups.Add(seconds[g]); D.groups.Add(thirds[g]); D.groups.Add(pot4[g]);
            }
            D.phase = (int)Phase.WorldsGroups;
            var perGroup = new List<List<MatchRecord>>();
            for (int g = 0; g < 4; g++)
            {
                var ids = D.groups.Skip(g * 4).Take(4).ToList();
                var list = RoundRobin(ids, Stage.WorldsGroups, g, false);
                list.AddRange(RoundRobin(ids, Stage.WorldsGroups, g, true));
                perGroup.Add(list);
            }
            for (int k = 0; k < perGroup[0].Count; k += 2)        // туры групп идут вперемешку
                for (int g = 0; g < 4; g++)
                    D.matches.AddRange(perGroup[g].Skip(k).Take(2));
        }

        public static int GroupOf(int teamId)
        {
            int i = D.groups.IndexOf(teamId);
            return i >= 0 ? i / 4 : -1;
        }

        static void AdvanceWorlds()
        {
            if (NextWorlds != null) return;
            switch (Phase)
            {
                case Phase.WorldsGroups:
                {
                    var top = new int[4, 2];
                    for (int g = 0; g < 4; g++)
                    {
                        var t = Standings(Stage.WorldsGroups, g);
                        top[g, 0] = t[0].team; top[g, 1] = t[1].team;
                    }
                    D.phase = (int)Phase.WorldsQuarter;
                    Add(Stage.WorldsQuarter, 0, top[0, 0], top[1, 1]); // A1 – B2
                    Add(Stage.WorldsQuarter, 0, top[1, 0], top[0, 1]); // B1 – A2
                    Add(Stage.WorldsQuarter, 0, top[2, 0], top[3, 1]); // C1 – D2
                    Add(Stage.WorldsQuarter, 0, top[3, 0], top[2, 1]); // D1 – C2
                    break;
                }
                case Phase.WorldsQuarter:
                {
                    var q = Matches(Stage.WorldsQuarter, 0);
                    D.phase = (int)Phase.WorldsSemi;
                    Add(Stage.WorldsSemi, 0, q[0].Winner, q[2].Winner);
                    Add(Stage.WorldsSemi, 0, q[1].Winner, q[3].Winner);
                    break;
                }
                case Phase.WorldsSemi:
                {
                    var s = Matches(Stage.WorldsSemi, 0);
                    D.phase = (int)Phase.WorldsFinal;
                    Add(Stage.WorldsFinal, 0, s[0].Winner, s[1].Winner);
                    break;
                }
                case Phase.WorldsFinal:
                {
                    D.champion = Matches(Stage.WorldsFinal, 0).Last().Winner;
                    var p = PlayerOf(D.champion);
                    D.championPlayer = p != null ? p.nickname : "";
                    D.phase = (int)Phase.Finished;
                    D.historyTeams.Add(D.champion);
                    D.historyPlayers.Add(D.championPlayer);
                    break;
                }
            }
        }

        // ================= служебное =================

        static void Add(Stage st, int group, int a, int b, int kind = 0) =>
            D.matches.Add(new MatchRecord { stage = (int)st, group = group, t1 = a, t2 = b, kind = kind });

        /// <summary>Круговая система (метод «карусели»): команда не играет два матча подряд.</summary>
        static List<MatchRecord> RoundRobin(List<int> ids, Stage st, int group, bool swap)
        {
            var res = new List<MatchRecord>();
            var list = new List<int>(ids);
            if (list.Count % 2 == 1) list.Add(-1);
            int n = list.Count;
            for (int round = 0; round < n - 1; round++)
            {
                for (int i = 0; i < n / 2; i++)
                {
                    int a = list[i], b = list[n - 1 - i];
                    if (a < 0 || b < 0) continue;
                    bool flip = (round % 2 == 1) ^ swap;
                    res.Add(new MatchRecord { stage = (int)st, group = group, t1 = flip ? b : a, t2 = flip ? a : b });
                }
                int last = list[n - 1];
                list.RemoveAt(n - 1);
                list.Insert(1, last);
            }
            return res;
        }
    }
}
