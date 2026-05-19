using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace CEHitChanceCalculator
{
    // 目标读取器只提取分析器需要的碰撞箱和代表护甲，不改目标本体。
    // 真实 CE 命中会按身体部位和衣物层继续细分，这里只做报表级近似。
    internal static class CETargetDataLoader
    {
        private static StatDef ElectricArmorStat => DefDatabase<StatDef>.GetNamedSilentFail("ArmorRating_Electric");

        public static bool TryLoadSelectedTarget(HitChanceInputs input, out string message)
        {
            // CE 的命中判定使用水平碰撞宽度和垂直高度；界面显示用米，
            // 模型内部再按 RimWorld 的垂直格高换回格数。
            message = null;
            Thing target = FindSelectedTarget();
            if (target == null)
            {
                message = "CEHCC_LoadTargetNoSelection".Translate();
                return false;
            }

            CollisionVertical vertical = new CollisionVertical(target);
            float heightCells = Mathf.Max(0.05f, vertical.Max);
            float widthCells = CalculateTargetWidthCells(target);

            input.TargetHeightMeters = heightCells * HitChanceCalculator.MetersPerCellHeight;
            input.TargetWidthMeters = widthCells * HitChanceCalculator.MetersPerCellHeight;
            LoadTargetArmor(target, input);
            message = "CEHCC_LoadTargetSuccess".Translate(
                target.LabelCap,
                input.TargetWidthMeters.ToString("0.###"),
                input.TargetHeightMeters.ToString("0.###"),
                input.TargetArmorSharp.ToString("0.###"),
                input.TargetArmorBlunt.ToString("0.###"),
                input.TargetArmorHeat.ToString("0.###"),
                input.TargetArmorElectric.ToString("0.###"));
            return true;
        }

        private static Thing FindSelectedTarget()
        {
            List<object> selected = Find.Selector?.SelectedObjectsListForReading;
            if (selected == null || selected.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is Pawn pawn)
                {
                    return pawn;
                }
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is Thing thing)
                {
                    return thing;
                }
            }

            return null;
        }

        private static float CalculateTargetWidthCells(Thing target)
        {
            if (target is Pawn)
            {
                return Mathf.Max(0.05f, CE_Utility.GetCollisionWidth(target));
            }

            Bounds bounds = CE_Utility.GetBoundsFor(target);
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            if (width > 0.05f)
            {
                return width;
            }

            return 1f;
        }

        private static void LoadTargetArmor(Thing target, HitChanceInputs input)
        {
            // Pawn 护甲按当前目标部位模式选择一个代表部位。
            // 非 Pawn（例如炮塔/建筑）则直接读取 Thing 自身的护甲 stat。
            if (target is Pawn pawn)
            {
                BodyPartRecord part = FindRepresentativePart(pawn, input.TargetMode);
                input.TargetArmorSharp = CalculatePawnArmor(pawn, StatDefOf.ArmorRating_Sharp, part);
                input.TargetArmorBlunt = CalculatePawnArmor(pawn, StatDefOf.ArmorRating_Blunt, part);
                input.TargetArmorHeat = CalculatePawnArmor(pawn, StatDefOf.ArmorRating_Heat, part);
                input.TargetArmorElectric = CalculatePawnArmor(pawn, ElectricArmorStat, part);
                return;
            }

            input.TargetArmorSharp = SafeStat(target, StatDefOf.ArmorRating_Sharp);
            input.TargetArmorBlunt = SafeStat(target, StatDefOf.ArmorRating_Blunt);
            input.TargetArmorHeat = SafeStat(target, StatDefOf.ArmorRating_Heat);
            input.TargetArmorElectric = SafeStat(target, ElectricArmorStat);
        }

        private static float CalculatePawnArmor(Pawn pawn, StatDef stat, BodyPartRecord part)
        {
            // PartialStat 已包含天然护甲；覆盖目标部位的衣物再叠加。
            // 这仍是代表部位估算，不等价于 CE 对每一层衣物的逐层随机结算。
            if (pawn == null)
            {
                return 0f;
            }

            if (part == null)
            {
                return SafeStat(pawn, stat);
            }

            float armor = Mathf.Max(0f, pawn.PartialStat(stat, part));
            List<Apparel> apparel = pawn.apparel?.WornApparel;
            if (apparel != null)
            {
                for (int i = 0; i < apparel.Count; i++)
                {
                    Apparel app = apparel[i];
                    if (app != null && app.def?.apparel != null && app.def.apparel.CoversBodyPart(part))
                    {
                        armor += Mathf.Max(0f, app.PartialStat(stat, part));
                    }
                }
            }
            return armor;
        }

        private static BodyPartRecord FindRepresentativePart(Pawn pawn, HitChanceTargetMode mode)
        {
            // 技能不足或压制射击时，命中模型会退回躯干；这里仅用于读取对应部位护甲。
            // 找不到指定部位时退到任意外部部位，避免少见种族直接读数失败。
            if (pawn?.RaceProps?.body?.AllParts == null)
            {
                return null;
            }

            string primary = mode == HitChanceTargetMode.Head ? "Head" : mode == HitChanceTargetMode.Legs ? "Leg" : "Torso";
            for (int i = 0; i < pawn.RaceProps.body.AllParts.Count; i++)
            {
                BodyPartRecord part = pawn.RaceProps.body.AllParts[i];
                if (part?.def != null && part.def.defName == primary)
                {
                    return part;
                }
            }

            if (mode == HitChanceTargetMode.Legs)
            {
                for (int i = 0; i < pawn.RaceProps.body.AllParts.Count; i++)
                {
                    BodyPartRecord part = pawn.RaceProps.body.AllParts[i];
                    if (part?.def != null && (part.def.defName == "Foot" || part.def.defName == "Tail"))
                    {
                        return part;
                    }
                }
            }

            for (int i = 0; i < pawn.RaceProps.body.AllParts.Count; i++)
            {
                BodyPartRecord part = pawn.RaceProps.body.AllParts[i];
                if (part?.depth == BodyPartDepth.Outside)
                {
                    return part;
                }
            }
            return null;
        }

        private static float SafeStat(Thing thing, StatDef stat)
        {
            if (thing == null || stat == null)
            {
                return 0f;
            }

            try
            {
                return Mathf.Max(0f, thing.GetStatValue(stat));
            }
            catch
            {
                return 0f;
            }
        }
    }
}
