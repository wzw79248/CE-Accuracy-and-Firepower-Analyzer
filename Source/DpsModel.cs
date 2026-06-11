using System.Collections.Generic;
using UnityEngine;

namespace CEHitChanceCalculator
{
    public enum ArmorDamageKind
    {
        None = 0,
        Sharp = 1,
        Blunt = 2,
        Heat = 3,
        Electric = 4,
        Composite = 5
    }

    public sealed class DamageLine
    {
        public string Label;
        public float DamagePerShot;
        public bool UsesDirectHitChance;
        public int Count = 1;
        public float ArmorDamagePerEvent;
        public float ArmorEventMultiplier;
        public ArmorDamageKind ArmorKind = ArmorDamageKind.None;
        public float ArmorPenetrationSharp;
        public float ArmorPenetrationBlunt;
        public bool UsesAmbientArmorFormula;
        public bool UsesLaserDamageFalloff;
    }

    public sealed class DamageProfile
    {
        public static readonly DamageProfile Empty = new DamageProfile();

        public string SourceLabel;
        public float DirectDamagePerShot;
        public string DirectLabel;
        public readonly List<DamageLine> DirectLines = new List<DamageLine>();
        public readonly List<DamageLine> RangeLines = new List<DamageLine>();
        public readonly List<DamageLine> FragmentLines = new List<DamageLine>();

        public bool HasDamage => DirectDamagePerShot > 0f || RangeLines.Count > 0 || FragmentLines.Count > 0;
    }

    public sealed class DpsResult
    {
        public float ShotsPerSecond;
        public float AverageDirectHitChance;
        public float DirectEffectiveDamagePerShot;
        public float DirectArmoredDamagePerShot;
        public float RangeArmoredDamagePerShot;
        public float DirectPaperDps;
        public float DirectExpectedDps;
        public float DirectExpectedDamagePerBurst;
        public float DirectExpectedDpsMagazine;
        public float DirectExpectedDps60s;
        public float DirectExpectedDamagePerMagazine;
        public float TotalExpectedDps;
        public float TotalExpectedDamagePerBurst;
        public float TotalExpectedDpsMagazine;
        public float TotalExpectedDps60s;
        public float TotalExpectedDamagePerMagazine;
        public float MagazineFireSeconds;
        public float NearMissAverageChance;
        public float NearMissDirectDps;
        public float NearMissDirectDamagePerBurst;
        public float RangeDamagePerShot;
        public float RangeExpectedDps;
        public float RangeExpectedDamagePerBurst;
        public float FragmentPaperDamagePerShot;
        public float FragmentPaperDps;
        public int AimedExtraTicks;
        public float RepeatWarmupReduction = 1f;
        public bool HasLaserDamageFalloff;
        public float LaserDamageFalloffMultiplier = 1f;
    }

    public sealed class ArmorDamageResult
    {
        public float PaperDamage;
        public float PostArmorDamage;
        public ArmorDamageKind Kind;
        public float SharpPenetration;
        public float BluntPenetration;
        public float HeatPenetration;
        public float ElectricPenetration;
        public float SharpDamage;
        public float BluntDamage;
        public float HeatDamage;
        public float ElectricDamage;
        public float UnmodeledDamage;
        public float EffectiveBluntPenetration;
        public bool SharpPenetrated;
        public bool SharpDeflected;
        public readonly List<ArmorDamageContribution> Contributions = new List<ArmorDamageContribution>();
    }

    public sealed class ArmorDamageContribution
    {
        public string Label;
        public ArmorDamageKind Kind;
        public float PostArmorDamage;
        public float Penetration;
        public float EffectiveBluntPenetration;
    }

    public sealed class FireTimelineBurst
    {
        public int BurstIndex;
        public int FirstShotIndex;
        public int ShotCount;
        public float WarmupStartSeconds;
        public float FirstAimEndSeconds;
        public float SecondAimEndSeconds;
        public float FirstShotSeconds;
        public float LastShotSeconds;
        public float CooldownEndSeconds;
        public float WarmupMultiplier = 1f;
    }

    public sealed class FireTimelineReload
    {
        public int ReloadIndex;
        public float StartSeconds;
        public float EndSeconds;
    }

    public sealed class FireTimeline
    {
        public readonly List<FireTimelineBurst> Bursts = new List<FireTimelineBurst>();
        public readonly List<FireTimelineReload> Reloads = new List<FireTimelineReload>();
        public int MagazineShots;
        public int TotalShotsFired;
        public int TicksBetweenShots;
        public int AimedExtraTicks;
        public float RepeatWarmupReduction = 1f;
        public float TotalSeconds;
        public bool IsWindow;
        public float WindowSeconds;
        public bool HasSecondAim => AimedExtraTicks > 0;
    }

    // 火力报表模型：把命中率结果、伤害拆分、护甲近似和射击节奏合并成玩家能读的 DPS。
    // 这里不重新模拟弹道，所有命中概率都来自 HitChanceCalculator，避免两套命中模型互相打架。
    public static class DpsCalculator
    {
        public static DpsResult Calculate(DamageProfile profile, HitChanceInputs input, HitChanceResult hitResult)
        {
            profile = profile ?? DamageProfile.Empty;
            int aimedExtraTicks;
            float repeatWarmupReduction;
            float shotsPerSecond = CalculateShotsPerSecond(input, out aimedExtraTicks, out repeatWarmupReduction);
            float averageHitChance = AveragePerShotChance(hitResult);
            float hitChanceSum = SumPerShotChance(hitResult);
            float averageNearMissChance = AverageNearMissChance(hitResult);
            float nearMissChanceSum = SumNearMissChance(hitResult);
            float directEffectiveDamage = SumEffectiveDamage(profile.DirectLines, input, profile.DirectDamagePerShot);
            float directDamage = SumArmoredDamage(profile.DirectLines, input, profile.DirectDamagePerShot);
            float rangeDamage = SumArmoredDamage(profile.RangeLines, input, SumDamage(profile.RangeLines));
            float totalDamage = directDamage + rangeDamage;
            float fragmentDamage = SumDamage(profile.FragmentLines);
            float magazineFireSeconds = CalculateMagazineFireTimeSeconds(input, aimedExtraTicks, repeatWarmupReduction);
            float directMagazineDamage = CalculateMagazineExpectedDamage(directDamage, input, hitResult);
            float totalMagazineDamage = CalculateMagazineExpectedDamage(totalDamage, input, hitResult);
            float directMagazineDps = magazineFireSeconds > 0.001f ? directMagazineDamage / magazineFireSeconds : 0f;
            float totalMagazineDps = magazineFireSeconds > 0.001f ? totalMagazineDamage / magazineFireSeconds : 0f;
            float directDps60s = CalculateWindowExpectedDamage(directDamage, input, hitResult, 60f, aimedExtraTicks, repeatWarmupReduction) / 60f;
            float totalDps60s = CalculateWindowExpectedDamage(totalDamage, input, hitResult, 60f, aimedExtraTicks, repeatWarmupReduction) / 60f;

            return new DpsResult
            {
                ShotsPerSecond = shotsPerSecond,
                AverageDirectHitChance = averageHitChance,
                DirectEffectiveDamagePerShot = directEffectiveDamage,
                DirectArmoredDamagePerShot = directDamage,
                RangeArmoredDamagePerShot = rangeDamage,
                DirectPaperDps = directEffectiveDamage * shotsPerSecond,
                DirectExpectedDps = directDamage * averageHitChance * shotsPerSecond,
                DirectExpectedDamagePerBurst = directDamage * hitChanceSum,
                DirectExpectedDpsMagazine = directMagazineDps,
                DirectExpectedDps60s = directDps60s,
                DirectExpectedDamagePerMagazine = directMagazineDamage,
                TotalExpectedDps = totalDamage * averageHitChance * shotsPerSecond,
                TotalExpectedDamagePerBurst = totalDamage * hitChanceSum,
                TotalExpectedDpsMagazine = totalMagazineDps,
                TotalExpectedDps60s = totalDps60s,
                TotalExpectedDamagePerMagazine = totalMagazineDamage,
                MagazineFireSeconds = magazineFireSeconds,
                NearMissAverageChance = averageNearMissChance,
                NearMissDirectDps = directDamage * averageNearMissChance * shotsPerSecond,
                NearMissDirectDamagePerBurst = directDamage * nearMissChanceSum,
                RangeDamagePerShot = rangeDamage,
                RangeExpectedDps = rangeDamage * averageHitChance * shotsPerSecond,
                RangeExpectedDamagePerBurst = rangeDamage * hitChanceSum,
                FragmentPaperDamagePerShot = fragmentDamage,
                FragmentPaperDps = fragmentDamage * shotsPerSecond,
                AimedExtraTicks = aimedExtraTicks,
                RepeatWarmupReduction = repeatWarmupReduction,
                HasLaserDamageFalloff = HasLaserDamageFalloff(profile.DirectLines),
                LaserDamageFalloffMultiplier = LaserDamageFalloffMultiplier(profile.DirectLines, input)
            };
        }

        public static float CalculateShotsPerSecond(HitChanceInputs input, out int aimedExtraTicks)
        {
            float repeatWarmupReduction;
            return CalculateShotsPerSecond(input, out aimedExtraTicks, out repeatWarmupReduction);
        }

        public static float CalculateShotsPerSecond(HitChanceInputs input, out int aimedExtraTicks, out float repeatWarmupReduction)
        {
            // 持续射速优先按弹匣循环估算：预热、二段瞄准、连发间隔、冷却和换弹都参与。
            // 没有可靠弹匣/节奏数据时，才回退到手动持续射速或 RPM。
            aimedExtraTicks = HitChanceCalculator.CalculateAimedExtraTicks(input.DistanceCells, input.AimMode, input.ShooterSuppressed);
            repeatWarmupReduction = CalculateRepeatWarmupReduction(input);
            if (input.MagazineShots > 0 && (input.FireWarmupSeconds > 0f || input.FireCooldownSeconds > 0f || input.ReloadSeconds > 0f))
            {
                float timeToEmpty = CalculateMagazineFireTimeSeconds(input, aimedExtraTicks, repeatWarmupReduction);
                float cycleSeconds = timeToEmpty + EffectiveReloadSeconds(input);
                if (cycleSeconds > 0.001f)
                {
                    return input.MagazineShots / cycleSeconds;
                }
            }

            if (input.SustainedShotsPerSecond > 0f)
            {
                return input.SustainedShotsPerSecond;
            }

            return Mathf.Max(0f, input.Rpm) / 60f;
        }

        public static float CalculateRepeatWarmupReduction(HitChanceInputs input)
        {
            // CE 的 FasterRepeatShots 近似：默认认为后续轮次继续向同一方向射击，
            // 因此重复预热被缩短。若未来要区分换目标/转火，应从这里拆分。
            if (!input.FasterRepeatShots)
            {
                return 1f;
            }

            float baseReduction = input.AimMode == HitChanceAimMode.SuppressFire ? 0.1f : 0.25f;
            return Mathf.Clamp(Mathf.Max(baseReduction, Mathf.Max(0f, input.RecoilAmount) / 45f), 0.01f, 1f);
        }

        public static float CalculateMagazineFireTimeSeconds(HitChanceInputs input, int aimedExtraTicks, float repeatWarmupReduction)
        {
            int shotsRemaining = EffectiveMagazineShots(input);
            int burstSize = Mathf.Max(1, input.BurstShots);
            int ticksBetweenShots = HitChanceCalculator.CalculateTicksBetweenShots(input.Rpm);
            float baseWarmupSeconds = EffectiveWarmupSeconds(input);
            float fireTime = 0f;
            int burstIndex = 0;
            while (shotsRemaining > 0)
            {
                int shotsThisBurst = Mathf.Min(burstSize, shotsRemaining);
                float warmupMultiplier = input.FasterRepeatShots && burstIndex > 0 ? repeatWarmupReduction : 1f;
                fireTime += baseWarmupSeconds * warmupMultiplier;
                fireTime += aimedExtraTicks / 60f * warmupMultiplier;
                fireTime += Mathf.Max(0, shotsThisBurst - 1) * ticksBetweenShots / 60f;
                fireTime += input.FireCooldownSeconds;
                shotsRemaining -= shotsThisBurst;
                burstIndex++;
            }
            return fireTime;
        }

        public static FireTimeline BuildFireTimeline(HitChanceInputs input)
        {
            return BuildFireTimeline(input, 0f);
        }

        public static FireTimeline BuildFireTimeline(HitChanceInputs input, float windowSeconds)
        {
            // 时间轴是显示用模型，但必须和 DPS 使用同一套节奏参数。
            // 这样 UI 上的瞄准/射击/冷却节点不会和报表数值分叉。
            int aimedExtraTicks = HitChanceCalculator.CalculateAimedExtraTicks(input.DistanceCells, input.AimMode, input.ShooterSuppressed);
            float repeatWarmupReduction = CalculateRepeatWarmupReduction(input);
            int magazineShots = EffectiveMagazineShots(input);
            int burstSize = Mathf.Max(1, input.BurstShots);
            int ticksBetweenShots = HitChanceCalculator.CalculateTicksBetweenShots(input.Rpm);
            float baseWarmupSeconds = EffectiveWarmupSeconds(input);
            float effectiveReloadSeconds = EffectiveReloadSeconds(input);
            bool isWindow = windowSeconds > 0.001f;
            var timeline = new FireTimeline
            {
                MagazineShots = magazineShots,
                TicksBetweenShots = ticksBetweenShots,
                AimedExtraTicks = aimedExtraTicks,
                RepeatWarmupReduction = repeatWarmupReduction,
                IsWindow = isWindow,
                WindowSeconds = isWindow ? windowSeconds : 0f
            };

            int shotsRemaining = magazineShots;
            int burstIndex = 0;
            int shotIndex = 0;
            int reloadIndex = 0;
            float time = 0f;
            int guard = 0;
            while ((isWindow ? time < windowSeconds : shotsRemaining > 0) && guard++ < 2000)
            {
                if (shotsRemaining <= 0)
                {
                    if (effectiveReloadSeconds > 0f)
                    {
                        timeline.Reloads.Add(new FireTimelineReload
                        {
                            ReloadIndex = reloadIndex,
                            StartSeconds = time,
                            EndSeconds = time + effectiveReloadSeconds
                        });
                        time += effectiveReloadSeconds;
                        reloadIndex++;
                    }
                    else if (baseWarmupSeconds <= 0f && input.FireCooldownSeconds <= 0f && ticksBetweenShots <= 0)
                    {
                        break;
                    }

                    shotsRemaining = magazineShots;
                    if (!isWindow)
                    {
                        break;
                    }
                    continue;
                }

                int shotsThisBurst = Mathf.Min(burstSize, shotsRemaining);
                float warmupMultiplier = input.FasterRepeatShots && burstIndex > 0 ? repeatWarmupReduction : 1f;
                float warmupSeconds = baseWarmupSeconds * warmupMultiplier;
                float secondAimSeconds = aimedExtraTicks / 60f * warmupMultiplier;
                float burstStart = time;
                float firstAimEnd = burstStart + warmupSeconds;
                float secondAimEnd = firstAimEnd + secondAimSeconds;
                float firstShot = secondAimEnd;
                float lastShot = firstShot + Mathf.Max(0, shotsThisBurst - 1) * ticksBetweenShots / 60f;
                float cooldownEnd = lastShot + input.FireCooldownSeconds;

                timeline.Bursts.Add(new FireTimelineBurst
                {
                    BurstIndex = burstIndex,
                    FirstShotIndex = shotIndex,
                    ShotCount = shotsThisBurst,
                    WarmupStartSeconds = burstStart,
                    FirstAimEndSeconds = firstAimEnd,
                    SecondAimEndSeconds = secondAimEnd,
                    FirstShotSeconds = firstShot,
                    LastShotSeconds = lastShot,
                    CooldownEndSeconds = cooldownEnd,
                    WarmupMultiplier = warmupMultiplier
                });

                time = cooldownEnd;
                shotsRemaining -= shotsThisBurst;
                shotIndex += shotsThisBurst;
                timeline.TotalShotsFired += shotsThisBurst;
                burstIndex++;
                if (!isWindow && shotsRemaining <= 0)
                {
                    break;
                }
                if (cooldownEnd <= burstStart && shotsRemaining > 0)
                {
                    break;
                }
            }

            timeline.TotalSeconds = isWindow ? windowSeconds : time;
            return timeline;
        }

        private static float CalculateMagazineExpectedDamage(float damagePerShot, HitChanceInputs input, HitChanceResult hitResult)
        {
            if (damagePerShot <= 0f)
            {
                return 0f;
            }

            int shots = EffectiveMagazineShots(input);
            float sum = 0f;
            for (int i = 0; i < shots; i++)
            {
                sum += ChanceForShot(hitResult?.MonteCarloPerShot, i, hitResult?.MonteCarloSingle ?? 0f);
            }
            return damagePerShot * sum;
        }

        private static float CalculateWindowExpectedDamage(float damagePerShot, HitChanceInputs input, HitChanceResult hitResult, float windowSeconds, int aimedExtraTicks, float repeatWarmupReduction)
        {
            // 固定窗口 DPS（当前 UI 使用 60 秒）按完整弹匣循环 + 剩余时间内的部分弹匣计算。
            // 换弹时间计入窗口，但弹匣内命中率仍按当前连发模型读取每发概率。
            if (damagePerShot <= 0f || windowSeconds <= 0f)
            {
                return 0f;
            }

            int magazineShots = EffectiveMagazineShots(input);
            float magazineFireSeconds = CalculateMagazineFireTimeSeconds(input, aimedExtraTicks, repeatWarmupReduction);
            if (magazineFireSeconds <= 0.001f)
            {
                return 0f;
            }

            float magazineDamage = CalculateMagazineExpectedDamage(damagePerShot, input, hitResult);
            float cycleSeconds = magazineFireSeconds + EffectiveReloadSeconds(input);
            if (cycleSeconds <= 0.001f)
            {
                return 0f;
            }

            int fullCycles = Mathf.FloorToInt(windowSeconds / cycleSeconds);
            float damage = fullCycles * magazineDamage;
            float remainder = windowSeconds - fullCycles * cycleSeconds;
            if (remainder > 0f)
            {
                damage += CalculatePartialMagazineExpectedDamage(damagePerShot, input, hitResult, remainder, magazineShots, aimedExtraTicks, repeatWarmupReduction);
            }
            return damage;
        }

        private static float CalculatePartialMagazineExpectedDamage(float damagePerShot, HitChanceInputs input, HitChanceResult hitResult, float availableSeconds, int magazineShots, int aimedExtraTicks, float repeatWarmupReduction)
        {
            int shotsRemaining = magazineShots;
            int burstSize = Mathf.Max(1, input.BurstShots);
            int ticksBetweenShots = HitChanceCalculator.CalculateTicksBetweenShots(input.Rpm);
            float baseWarmupSeconds = EffectiveWarmupSeconds(input);
            float time = 0f;
            float damage = 0f;
            int burstIndex = 0;
            int shotIndex = 0;
            while (shotsRemaining > 0)
            {
                int shotsThisBurst = Mathf.Min(burstSize, shotsRemaining);
                float warmupMultiplier = input.FasterRepeatShots && burstIndex > 0 ? repeatWarmupReduction : 1f;
                time += baseWarmupSeconds * warmupMultiplier;
                time += aimedExtraTicks / 60f * warmupMultiplier;
                if (time > availableSeconds)
                {
                    break;
                }

                for (int i = 0; i < shotsThisBurst; i++)
                {
                    if (i > 0)
                    {
                        time += ticksBetweenShots / 60f;
                        if (time > availableSeconds)
                        {
                            return damage;
                        }
                    }
                    damage += damagePerShot * ChanceForShot(hitResult?.MonteCarloPerShot, shotIndex, hitResult?.MonteCarloSingle ?? 0f);
                    shotIndex++;
                }

                time += input.FireCooldownSeconds;
                shotsRemaining -= shotsThisBurst;
                burstIndex++;
            }
            return damage;
        }

        private static int EffectiveMagazineShots(HitChanceInputs input)
        {
            return Mathf.Max(1, input.MagazineShots > 0 ? input.MagazineShots : input.BurstShots);
        }

        public static float EffectiveWarmupSeconds(HitChanceInputs input)
        {
            return Mathf.Max(0f, input.FireWarmupSeconds) * Mathf.Clamp(input.AimingDelayFactor, 0.01f, 2f);
        }

        public static float EffectiveReloadSeconds(HitChanceInputs input)
        {
            return Mathf.Max(0f, input.ReloadSeconds) * Mathf.Max(0.01f, input.ReloadFactor) / Mathf.Max(0.001f, input.ReloadSpeed);
        }

        private static float ChanceForShot(float[] chances, int shotIndex, float fallback)
        {
            if (chances == null || chances.Length == 0)
            {
                return Mathf.Clamp01(fallback);
            }
            return Mathf.Clamp01(chances[shotIndex % chances.Length]);
        }

        private static float AveragePerShotChance(HitChanceResult result)
        {
            if (result?.MonteCarloPerShot == null || result.MonteCarloPerShot.Length == 0)
            {
                return result?.MonteCarloSingle ?? 0f;
            }

            return SumPerShotChance(result) / result.MonteCarloPerShot.Length;
        }

        private static float SumPerShotChance(HitChanceResult result)
        {
            if (result?.MonteCarloPerShot == null || result.MonteCarloPerShot.Length == 0)
            {
                return result?.MonteCarloSingle ?? 0f;
            }

            float sum = 0f;
            for (int i = 0; i < result.MonteCarloPerShot.Length; i++)
            {
                sum += Mathf.Clamp01(result.MonteCarloPerShot[i]);
            }
            return sum;
        }

        private static float AverageNearMissChance(HitChanceResult result)
        {
            if (result?.MonteCarloNearMissPerShot == null || result.MonteCarloNearMissPerShot.Length == 0)
            {
                return result?.MonteCarloNearMissSingle ?? 0f;
            }

            return SumNearMissChance(result) / result.MonteCarloNearMissPerShot.Length;
        }

        private static float SumNearMissChance(HitChanceResult result)
        {
            if (result?.MonteCarloNearMissPerShot == null || result.MonteCarloNearMissPerShot.Length == 0)
            {
                return result?.MonteCarloNearMissSingle ?? 0f;
            }

            float sum = 0f;
            for (int i = 0; i < result.MonteCarloNearMissPerShot.Length; i++)
            {
                sum += Mathf.Clamp01(result.MonteCarloNearMissPerShot[i]);
            }
            return sum;
        }

        private static float SumDamage(List<DamageLine> lines)
        {
            float sum = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                DamageLine line = lines[i];
                if (line != null)
                {
                    sum += Mathf.Max(0f, line.DamagePerShot);
                }
            }
            return sum;
        }

        private static float SumEffectiveDamage(List<DamageLine> lines, HitChanceInputs input, float fallback)
        {
            if (lines == null || lines.Count == 0)
            {
                return Mathf.Max(0f, fallback);
            }

            float sum = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                DamageLine line = lines[i];
                if (line != null)
                {
                    sum += Mathf.Max(0f, line.DamagePerShot) * DamageMultiplierForLine(line, input);
                }
            }
            return sum;
        }

        private static float SumArmoredDamage(List<DamageLine> lines, HitChanceInputs input, float fallback)
        {
            if (lines == null || lines.Count == 0)
            {
                return Mathf.Max(0f, fallback);
            }

            float sum = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                DamageLine line = lines[i];
                if (line == null || line.DamagePerShot <= 0f)
                {
                    continue;
                }

                float each = ArmorEventDamage(line) * DamageMultiplierForLine(line, input);
                float multiplier = ArmorEventMultiplier(line);
                sum += CalculatePostArmorDamage(each, line.ArmorKind, line.ArmorPenetrationSharp, line.ArmorPenetrationBlunt, input.TargetArmorSharp, input.TargetArmorBlunt, input.TargetArmorHeat, input.TargetArmorElectric, line.UsesAmbientArmorFormula) * multiplier;
            }
            return sum;
        }

        public static float CalculateLaserDamageFalloffMultiplier(float distanceCells, float spreadDegrees)
        {
            // CE 的 LaserBeamCE 在 RayCast 中用光束孔径和散布角计算 DamageModifier。
            // 这里按目标距离取近似值，并把大于纸面的极端近距离结果夹到 100%。
            const float apertureSize = 0.03f;
            const float baseAngleRadians = 0.00052359875f;
            float distance = Mathf.Max(1f, distanceCells);
            float spreadSin = Mathf.Sin(Mathf.Max(0f, spreadDegrees) * 0.5f * Mathf.Deg2Rad);
            float baseRadius = Mathf.Sin(baseAngleRadians) + apertureSize;
            float radius = distance * spreadSin + apertureSize;
            if (radius <= 0.0001f)
            {
                return 1f;
            }
            return Mathf.Clamp01((baseRadius * baseRadius) / (radius * radius));
        }

        private static float DamageMultiplierForLine(DamageLine line, HitChanceInputs input)
        {
            if (line != null && line.UsesLaserDamageFalloff)
            {
                return CalculateLaserDamageFalloffMultiplier(input?.DistanceCells ?? 0f, input?.SpreadDegrees ?? 0f);
            }
            return 1f;
        }

        private static bool HasLaserDamageFalloff(List<DamageLine> lines)
        {
            if (lines == null)
            {
                return false;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i]?.UsesLaserDamageFalloff == true)
                {
                    return true;
                }
            }
            return false;
        }

        private static float LaserDamageFalloffMultiplier(List<DamageLine> lines, HitChanceInputs input)
        {
            return HasLaserDamageFalloff(lines) ? CalculateLaserDamageFalloffMultiplier(input?.DistanceCells ?? 0f, input?.SpreadDegrees ?? 0f) : 1f;
        }

        private static float ArmorEventDamage(DamageLine line)
        {
            if (line == null)
            {
                return 0f;
            }
            if (line.ArmorDamagePerEvent > 0f)
            {
                return line.ArmorDamagePerEvent;
            }
            return Mathf.Max(0f, line.DamagePerShot) / Mathf.Max(1, line.Count);
        }

        private static float ArmorEventMultiplier(DamageLine line)
        {
            if (line == null)
            {
                return 0f;
            }
            if (line.ArmorEventMultiplier > 0f)
            {
                return line.ArmorEventMultiplier;
            }
            return Mathf.Max(1, line.Count);
        }

        public static float CalculatePostArmorDamage(float damage, ArmorDamageKind kind, float sharpPen, float bluntPen, float sharpArmor, float bluntArmor, float heatArmor = 0f, float electricArmor = 0f, bool useAmbientFormula = false)
        {
            // 护甲后伤害是报表近似：按一个代表部位的综合护甲计算。
            // 真实 CE 会按命中部位、衣物层、天然护甲和随机判定逐层结算。
            damage = Mathf.Max(0f, damage);
            sharpArmor = Mathf.Max(0f, sharpArmor);
            bluntArmor = Mathf.Max(0f, bluntArmor);
            heatArmor = Mathf.Max(0f, heatArmor);
            electricArmor = Mathf.Max(0f, electricArmor);

            if (damage <= 0f || kind == ArmorDamageKind.None)
            {
                return damage;
            }

            if (useAmbientFormula)
            {
                if (kind == ArmorDamageKind.Sharp)
                {
                    return damage * AmbientPenetrationMultiplier(sharpPen, sharpArmor);
                }
                if (kind == ArmorDamageKind.Blunt)
                {
                    return damage * AmbientPenetrationMultiplier(bluntPen, bluntArmor);
                }
                if (kind == ArmorDamageKind.Heat)
                {
                    return damage * AmbientPenetrationMultiplier(bluntPen, heatArmor);
                }
                if (kind == ArmorDamageKind.Electric)
                {
                    return damage * AmbientPenetrationMultiplier(bluntPen, electricArmor);
                }
            }

            if (kind == ArmorDamageKind.Blunt)
            {
                return damage * PenetrationMultiplier(bluntPen, bluntArmor);
            }
            if (kind == ArmorDamageKind.Heat)
            {
                return damage * PenetrationMultiplier(bluntPen, heatArmor);
            }
            if (kind == ArmorDamageKind.Electric)
            {
                return damage * PenetrationMultiplier(bluntPen, electricArmor);
            }

            if (sharpPen <= 0.001f)
            {
                return damage;
            }

            if (sharpArmor < sharpPen)
            {
                // 锐伤穿透时，穿过护甲的比例保留为锐伤；被护甲挡下的比例转成钝伤近似。
                // 这个钝伤量主要由有效钝穿决定，不直接等于“被挡下的锐伤数值”。
                float sharpDamage = damage * Mathf.Clamp01((sharpPen - sharpArmor) / sharpPen);
                float absorbedFraction = Mathf.Clamp01(sharpArmor / sharpPen);
                float partialBluntPen = bluntPen * absorbedFraction * absorbedFraction;
                float partialBluntDamage = DeflectedBluntDamage(partialBluntPen) * PenetrationMultiplier(partialBluntPen, bluntArmor);
                return sharpDamage + partialBluntDamage;
            }

            return DeflectedBluntDamage(bluntPen) * PenetrationMultiplier(bluntPen, bluntArmor);
        }

        private static float DeflectedBluntDamage(float bluntPen)
        {
            if (bluntPen <= 0.001f)
            {
                return 0f;
            }
            return Mathf.Pow(bluntPen * 10000f, 1f / 3f) / 10f;
        }

        private static float PenetrationMultiplier(float pen, float armor)
        {
            if (pen <= 0.001f)
            {
                return 1f;
            }
            return Mathf.Clamp01((pen - armor) / pen);
        }

        private static float AmbientPenetrationMultiplier(float pen, float armor)
        {
            // CE 的 ambient damage 使用 1 + penetration - armorRating，而不是普通穿透比例。
            return Mathf.Clamp01(1f + Mathf.Max(0f, pen) - Mathf.Max(0f, armor));
        }

        public static ArmorDamageResult CalculatePrimaryArmorResult(DamageProfile profile, HitChanceInputs input)
        {
            var result = new ArmorDamageResult();
            bool found = false;
            ArmorDamageKind firstKind = ArmorDamageKind.None;
            bool multipleKinds = false;
            if (profile?.DirectLines != null)
            {
                for (int i = 0; i < profile.DirectLines.Count; i++)
                {
                    DamageLine line = profile.DirectLines[i];
                    if (line == null || line.DamagePerShot <= 0f)
                    {
                        continue;
                    }

                    float multiplier = ArmorEventMultiplier(line);
                    float damageMultiplier = DamageMultiplierForLine(line, input);
                    float each = ArmorEventDamage(line) * damageMultiplier;
                    ArmorDamageResult eachResult = CalculateArmorBreakdown(each, line.ArmorKind, line.ArmorPenetrationSharp, line.ArmorPenetrationBlunt, input.TargetArmorSharp, input.TargetArmorBlunt, input.TargetArmorHeat, input.TargetArmorElectric, line.UsesAmbientArmorFormula);
                    result.PaperDamage += Mathf.Max(0f, line.DamagePerShot) * damageMultiplier;
                    result.PostArmorDamage += eachResult.PostArmorDamage * multiplier;
                    result.SharpDamage += eachResult.SharpDamage * multiplier;
                    result.BluntDamage += eachResult.BluntDamage * multiplier;
                    result.HeatDamage += eachResult.HeatDamage * multiplier;
                    result.ElectricDamage += eachResult.ElectricDamage * multiplier;
                    result.UnmodeledDamage += eachResult.UnmodeledDamage * multiplier;
                    result.SharpPenetration = Mathf.Max(result.SharpPenetration, eachResult.SharpPenetration);
                    result.BluntPenetration = Mathf.Max(result.BluntPenetration, eachResult.BluntPenetration);
                    result.HeatPenetration = Mathf.Max(result.HeatPenetration, eachResult.HeatPenetration);
                    result.ElectricPenetration = Mathf.Max(result.ElectricPenetration, eachResult.ElectricPenetration);
                    result.EffectiveBluntPenetration = Mathf.Max(result.EffectiveBluntPenetration, eachResult.EffectiveBluntPenetration);
                    result.SharpPenetrated |= eachResult.SharpPenetrated;
                    result.SharpDeflected |= eachResult.SharpDeflected;
                    if (eachResult.PostArmorDamage > 0.0049f)
                    {
                        result.Contributions.Add(new ArmorDamageContribution
                        {
                            Label = line.Label,
                            Kind = line.ArmorKind,
                            PostArmorDamage = eachResult.PostArmorDamage * multiplier,
                            Penetration = ContributionPenetration(eachResult, line.ArmorKind),
                            EffectiveBluntPenetration = eachResult.EffectiveBluntPenetration
                        });
                    }
                    if (!found)
                    {
                        firstKind = line.ArmorKind;
                    }
                    else if (line.ArmorKind != firstKind)
                    {
                        multipleKinds = true;
                    }
                    found = true;
                }
            }

            if (!found)
            {
                result.PaperDamage = Mathf.Max(0f, profile?.DirectDamagePerShot ?? 0f);
                result.PostArmorDamage = result.PaperDamage;
                result.UnmodeledDamage = result.PostArmorDamage;
                return result;
            }

            result.Kind = multipleKinds ? ArmorDamageKind.Composite : firstKind;
            return result;
        }

        private static float ContributionPenetration(ArmorDamageResult result, ArmorDamageKind kind)
        {
            if (kind == ArmorDamageKind.Sharp)
            {
                return result.SharpPenetration;
            }
            if (kind == ArmorDamageKind.Heat)
            {
                return result.HeatPenetration;
            }
            if (kind == ArmorDamageKind.Electric)
            {
                return result.ElectricPenetration;
            }
            return result.BluntPenetration;
        }

        private static ArmorDamageResult CalculateArmorBreakdown(float damage, ArmorDamageKind kind, float sharpPen, float bluntPen, float sharpArmor, float bluntArmor, float heatArmor, float electricArmor, bool useAmbientFormula)
        {
            // 与 CalculatePostArmorDamage 使用同一套公式，但保留锐伤/钝伤/热伤/电伤拆分给 UI 解释。
            // 如果以后调整护甲公式，两个函数必须同步改。
            var result = new ArmorDamageResult
            {
                PaperDamage = Mathf.Max(0f, damage),
                Kind = kind,
                SharpPenetration = Mathf.Max(0f, sharpPen),
                BluntPenetration = Mathf.Max(0f, bluntPen)
            };

            if (damage <= 0f || kind == ArmorDamageKind.None)
            {
                result.PostArmorDamage = Mathf.Max(0f, damage);
                result.UnmodeledDamage = result.PostArmorDamage;
                return result;
            }

            if (useAmbientFormula)
            {
                if (kind == ArmorDamageKind.Sharp)
                {
                    result.SharpDamage = damage * AmbientPenetrationMultiplier(result.SharpPenetration, sharpArmor);
                    result.PostArmorDamage = result.SharpDamage;
                    return result;
                }
                if (kind == ArmorDamageKind.Blunt)
                {
                    result.EffectiveBluntPenetration = result.BluntPenetration;
                    result.BluntDamage = damage * AmbientPenetrationMultiplier(result.BluntPenetration, bluntArmor);
                    result.PostArmorDamage = result.BluntDamage;
                    return result;
                }
                if (kind == ArmorDamageKind.Heat)
                {
                    result.HeatPenetration = result.BluntPenetration;
                    result.HeatDamage = damage * AmbientPenetrationMultiplier(result.HeatPenetration, heatArmor);
                    result.PostArmorDamage = result.HeatDamage;
                    return result;
                }
                if (kind == ArmorDamageKind.Electric)
                {
                    result.ElectricPenetration = result.BluntPenetration;
                    result.ElectricDamage = damage * AmbientPenetrationMultiplier(result.ElectricPenetration, electricArmor);
                    result.PostArmorDamage = result.ElectricDamage;
                    return result;
                }
            }

            if (kind == ArmorDamageKind.Blunt)
            {
                result.EffectiveBluntPenetration = result.BluntPenetration;
                result.BluntDamage = damage * PenetrationMultiplier(result.BluntPenetration, bluntArmor);
                result.PostArmorDamage = result.BluntDamage;
                return result;
            }
            if (kind == ArmorDamageKind.Heat)
            {
                result.HeatPenetration = result.BluntPenetration;
                result.HeatDamage = damage * PenetrationMultiplier(result.HeatPenetration, heatArmor);
                result.PostArmorDamage = result.HeatDamage;
                return result;
            }
            if (kind == ArmorDamageKind.Electric)
            {
                result.ElectricPenetration = result.BluntPenetration;
                result.ElectricDamage = damage * PenetrationMultiplier(result.ElectricPenetration, electricArmor);
                result.PostArmorDamage = result.ElectricDamage;
                return result;
            }

            if (result.SharpPenetration <= 0.001f)
            {
                result.PostArmorDamage = damage;
                result.SharpDamage = damage;
                return result;
            }

            if (sharpArmor < result.SharpPenetration)
            {
                result.SharpPenetrated = true;
                result.SharpDamage = damage * Mathf.Clamp01((result.SharpPenetration - sharpArmor) / result.SharpPenetration);
                float absorbedFraction = Mathf.Clamp01(sharpArmor / result.SharpPenetration);
                result.EffectiveBluntPenetration = result.BluntPenetration * absorbedFraction * absorbedFraction;
                result.BluntDamage = DeflectedBluntDamage(result.EffectiveBluntPenetration) * PenetrationMultiplier(result.EffectiveBluntPenetration, bluntArmor);
                result.PostArmorDamage = result.SharpDamage + result.BluntDamage;
                return result;
            }

            result.SharpDeflected = true;
            result.EffectiveBluntPenetration = result.BluntPenetration;
            result.BluntDamage = DeflectedBluntDamage(result.BluntPenetration) * PenetrationMultiplier(result.BluntPenetration, bluntArmor);
            result.PostArmorDamage = result.BluntDamage;
            return result;
        }
    }
}
