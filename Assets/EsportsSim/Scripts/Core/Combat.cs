using System;

namespace EsportsSim
{
    public class Fighter
    {
        public PlayerData data;
        public TeamData team;
        public int power;   // сила игрока + бонус команды
        public float hp;
        public Fighter(PlayerData d, TeamData t)
        {
            data = d; team = t; power = Roster.Power(d, t); hp = Combat.MaxHp;
        }
        public bool Alive => hp > 0f;
    }

    /// <summary>Что игрок решил делать в этот тик (всё одновременно, каждое со своим шансом).</summary>
    public struct TickAction
    {
        public bool attack, retreat, defend;
    }

    public enum HitResult { None, Dodged, Blocked, Hit }

    public struct Strike
    {
        public HitResult result;
        public float damage;
        public bool crit;
    }

    public class TickReport
    {
        public TickAction a1, a2;
        public Strike onP2; // удар игрока 1 по игроку 2
        public Strike onP1; // удар игрока 2 по игроку 1
    }

    /// <summary>
    /// Вся механика боя. Тик = 1 с. В каждом тике каждый игрок одновременно
    /// с разной вероятностью атакует, отступает (уклон) и защищается (блок).
    /// Баланс: равные игроки выдерживают ~6-7 полных ударов (100 HP / ~15 урона).
    /// </summary>
    public static class Combat
    {
        public const float MaxHp = 100f;
        public const float TickSeconds = 1f;   // один ход в секунду
        public const float BlockFactor = 0.4f;   // блок оставляет 40% урона
        public const float CritChance = 0.10f;
        public const float CritMultiplier = 1.7f;

        static float K(int power) => (power - 50) / 25f; // 0 при 50, 1 при 75, 1.4 при 85 (75 + бонус 10)

        public static float AttackChance(int p) => 0.50f + 0.12f * K(p);  // 50% .. 62%
        public static float RetreatChance(int p) => 0.14f + 0.06f * K(p); // 14% .. 20%
        public static float DefendChance(int p) => 0.30f + 0.08f * K(p);  // 30% .. 38%
        public static float BaseDamage(int p) => 8f + p * 0.12f;          // 14 .. 17

        static float R(Random r) => (float)r.NextDouble();

        public static TickAction Roll(int power, Random r) => new TickAction
        {
            attack = R(r) < AttackChance(power),
            retreat = R(r) < RetreatChance(power),
            defend = R(r) < DefendChance(power),
        };

        public static Strike Resolve(int attackerPower, TickAction defender, Random r)
        {
            bool crit = R(r) < CritChance;
            float dmg = BaseDamage(attackerPower) * (0.7f + 0.6f * R(r)) * (crit ? CritMultiplier : 1f);
            if (defender.retreat) return new Strike { result = HitResult.Dodged, damage = 0f };
            if (defender.defend) return new Strike { result = HitResult.Blocked, damage = dmg * BlockFactor, crit = crit };
            return new Strike { result = HitResult.Hit, damage = dmg, crit = crit };
        }

        public static TickReport Tick(Fighter f1, Fighter f2, Random r)
        {
            var rep = new TickReport { a1 = Roll(f1.power, r), a2 = Roll(f2.power, r) };
            if (rep.a1.attack) rep.onP2 = Resolve(f1.power, rep.a2, r);
            if (rep.a2.attack) rep.onP1 = Resolve(f2.power, rep.a1, r);
            f2.hp -= rep.onP2.damage;
            f1.hp -= rep.onP1.damage;
            return rep;
        }

        /// <summary>1 или 2. Если оба упали в один тик — побеждает тот, у кого осталось больше.</summary>
        public static int RoundWinner(Fighter f1, Fighter f2, Random r)
        {
            if (f1.Alive && !f2.Alive) return 1;
            if (f2.Alive && !f1.Alive) return 2;
            if (Math.Abs(f1.hp - f2.hp) < 0.001f) return r.Next(2) + 1;
            return f1.hp > f2.hp ? 1 : 2;
        }

        /// <summary>Досчитать раунд мгновенно (с текущих HP).</summary>
        public static int FinishRound(Fighter f1, Fighter f2, Random r)
        {
            int guard = 0;
            while (f1.Alive && f2.Alive && guard++ < 10000) Tick(f1, f2, r);
            return RoundWinner(f1, f2, r);
        }

        /// <summary>Мгновенный матч до 2 побед (Bo3). Возвращает счёт.</summary>
        public static void SimMatch(PlayerData a, TeamData ta, PlayerData b, TeamData tb, Random r, out int s1, out int s2)
        {
            s1 = 0; s2 = 0;
            while (s1 < 2 && s2 < 2)
            {
                if (FinishRound(new Fighter(a, ta), new Fighter(b, tb), r) == 1) s1++; else s2++;
            }
        }
    }
}
