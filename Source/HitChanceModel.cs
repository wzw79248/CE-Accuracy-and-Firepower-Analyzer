using System;
using System.Collections.Generic;
using UnityEngine;

namespace CEHitChanceCalculator
{
    public enum HitChanceAimMode
    {
        AimedShot = 0,
        Snapshot = 1,
        SuppressFire = 2
    }

    public enum HitChanceTargetMode
    {
        Torso = 0,
        Head = 1,
        Legs = 2
    }

    public enum HitChanceLimitAxis
    {
        Mixed = 0,
        Horizontal = 1,
        Vertical = 2
    }

    public enum HitChanceAnalysisMode
    {
        None = 0,
        DistanceCurve = 1,
        BurstPerShot = 2,
        Timeline = 3,
        BallisticDistribution = 4
    }

    public sealed class HitChanceInputs
    {
        public float DistanceCells = 30f;
        public float MaxRangeCells = 55f;
        public float ShotSpeedCellsPerSecond = 168f;
        public float ShotHeightCells = 0.85f;
        public float GravityFactor = 1f;
        public float TargetHeightMeters = 1.75f;
        public float TargetWidthMeters = 0.88f;
        public HitChanceTargetMode TargetMode = HitChanceTargetMode.Torso;
        public float TargetArmorSharp = 0f;
        public float TargetArmorBlunt = 0f;
        public float TargetArmorHeat = 0f;
        public float TargetArmorElectric = 0f;
        public float SwayDegrees = 1.995f;
        public float SpreadDegrees = 0.07f;
        public float RecoilAmount = 1.50f;
        public float ShootingAccuracy = 3.0f;
        public float AimingAccuracy = 1.0f;
        public float SightsEfficiency = 1.0f;
        public float AimingDelayFactor = 1.0f;
        public float ReloadSpeed = 1.0f;
        public float ReloadFactor = 1.0f;
        public float NightVisionEfficiency = 0f;
        public HitChanceAimMode AimMode = HitChanceAimMode.AimedShot;
        public bool ShooterSuppressed = false;
        public float Darkness = 0f;
        public float WeatherError = 0f;
        public float SmokeDensity = 0f;
        public float TargetMoveSpeedCellsPerSecond = 0f;
        public float TargetMoveDirectionDegrees = 90f;
        public bool BlindFiring = false;
        public float CircularMissRadiusCells = 0f;
        public float IndirectFireShiftCells = 0f;
        public int BurstShots = 6;
        public float Rpm = 900f;
        public float SustainedShotsPerSecond = 0f;
        public int MagazineShots = 30;
        public float FireWarmupSeconds = 1.1f;
        public float FireCooldownSeconds = 0.36f;
        public float ReloadSeconds = 4f;
        public bool FasterRepeatShots = false;
        public bool Timeline60SecondMode = false;
        public int ShooterThingId = 0;
        public int SwayStartTick = -1;
        public int Samples = 10000;
        public int Seed = 12345;
        public HitChanceAnalysisMode AnalysisMode = HitChanceAnalysisMode.None;
        public int AnalysisSamples = 3000;
    }

    public sealed class HitChanceResult
    {
        public float CeEstimatedSingle;
        public float MonteCarloSingle;
        public float MonteCarloBurstAny;
        public float[] MonteCarloPerShot;
        public float[] MonteCarloNearMissPerShot;
        public float MonteCarloNearMissSingle;
        public bool LineOfFireObstaclesApplied;
        public int LineOfFireObstacleCount;
        public float LineOfFireBlockedSingle;
        public float LineOfFireBlockedBurstAny;
        public float[] LineOfFireBlockedPerShot;
        public string LineOfFireObstacleLabel;
        public float MonteCarloHorizontalFirst;
        public float MonteCarloVerticalFirst;
        public float TargetWidthCellsForCeMath;
        public float TargetHeightCells;
        public float TargetAimHeightCells;
        public float ShotAngleRadians;
        public float VisibilityScalarCells;
        public float VisibilityErrorCells;
        public float LeadErrorCells;
        public float RangeErrorCells;
        public float EnvironmentShift;
        public float LightingRangeMultiplier;
        public float RangeVerticalErrorCells;
        public float EffectiveSwayDegrees;
        public int BurstShotIntervalTicks;
        public float GravityPerWidth;
        public HitChanceLimitAxis DominantAxis;
        public bool HorizontalSaturated;
        public bool VerticalSaturated;
        public bool TargetModeForcedTorso;
    }

    public sealed class HitChanceAnalysisPoint
    {
        public string Label;
        public float X;
        public float Single;
        public float BurstAny;
        public float[] PerShot;
    }

    public sealed class BallisticDistributionPoint
    {
        public int ShotIndex;
        public float HorizontalMissCells;
        public float HeightAtTargetCells;
        public float FinalShotAngleRadians;
        public bool HorizontalHit;
        public bool VerticalHit;
        public bool Hit => HorizontalHit && VerticalHit;
    }

    public sealed class BallisticDistribution
    {
        public readonly List<BallisticDistributionPoint> Points = new List<BallisticDistributionPoint>();
        public int SampleCount;
        public int BurstShots;
        public float DistanceCells;
        public float ShotHeightCells;
        public float TargetWidthCells;
        public float TargetHeightCells;
        public float TargetAimHeightCells;
        public float HorizontalExtentCells;
        public float VerticalMinCells;
        public float VerticalMaxCells;
    }

    public sealed class LineOfFireObstacle
    {
        public float DistanceCells;
        public float HalfDepthCells;
        public float CenterOffsetCells;
        public float HalfWidthCells;
        public float MinHeightCells;
        public float MaxHeightCells;
        public string Label;

        public bool IsValid(float targetDistanceCells)
        {
            return DistanceCells > 0.05f
                && DistanceCells < targetDistanceCells - 0.05f
                && HalfWidthCells > 0f
                && MaxHeightCells > MinHeightCells;
        }
    }

    public sealed class LineOfFireObstacleContext
    {
        public readonly List<LineOfFireObstacle> Obstacles = new List<LineOfFireObstacle>();
        public bool Enabled = true;
        public int ShooterThingId;
        public float TargetDistanceCells;
        public float DistanceToleranceCells = 1f;
        public string SourceLabel;
        public string TargetLabel;
        public string SummaryLabel;

        public bool AppliesTo(HitChanceInputs input)
        {
            if (!Enabled || input == null)
            {
                return false;
            }

            if (ShooterThingId != 0 && input.ShooterThingId != 0 && ShooterThingId != input.ShooterThingId)
            {
                return false;
            }

            if (TargetDistanceCells > 0f && Mathf.Abs(TargetDistanceCells - input.DistanceCells) > Mathf.Max(0.05f, DistanceToleranceCells))
            {
                return false;
            }

            return ValidObstacleCount(input.DistanceCells) > 0;
        }

        public int ValidObstacleCount(float targetDistanceCells)
        {
            int count = 0;
            for (int i = 0; i < Obstacles.Count; i++)
            {
                if (Obstacles[i] != null && Obstacles[i].IsValid(targetDistanceCells))
                {
                    count++;
                }
            }

            return count;
        }

        public string DisplayLabel()
        {
            if (!string.IsNullOrEmpty(SummaryLabel))
            {
                return SummaryLabel;
            }

            if (!string.IsNullOrEmpty(SourceLabel) || !string.IsNullOrEmpty(TargetLabel))
            {
                return (SourceLabel ?? "") + " -> " + (TargetLabel ?? "");
            }

            return "";
        }
    }

    public static class HitChanceObstacleBridge
    {
        private static LineOfFireObstacleContext currentContext;

        public static int Version { get; private set; }

        public static LineOfFireObstacleContext CurrentContext => currentContext;

        public static void Set(LineOfFireObstacleContext context)
        {
            currentContext = context;
            Version++;
        }

        public static void Clear()
        {
            currentContext = null;
            Version++;
        }

        public static void Touch()
        {
            Version++;
        }

        public static bool TryGetFor(HitChanceInputs input, out LineOfFireObstacleContext context)
        {
            context = currentContext;
            return context != null && context.AppliesTo(input);
        }
    }

    // 命中率核心模型：这里同时维护“公式近似参考”和蒙特卡洛模拟。
    // 注意：目标不是逐帧复刻 CE，而是把已确认的 CE 机制转成稳定、可解释的估算。
    public static class HitChanceCalculator
    {
        public const float GravityConst = 9.8f / 5f;
        public const float MetersPerCellHeight = 1.75f;

        private static readonly float Sqrt2 = Mathf.Sqrt(2f);
        private static readonly float Deg2RadOverSqrt2 = Mathf.Deg2Rad / Mathf.Sqrt(2f);

        public static HitChanceResult Calculate(HitChanceInputs input)
        {
            HitChanceObstacleBridge.TryGetFor(input, out LineOfFireObstacleContext obstacleContext);
            return Calculate(input, obstacleContext);
        }

        public static HitChanceResult Calculate(HitChanceInputs input, LineOfFireObstacleContext obstacleContext)
        {
            Sanitize(input);
            LineOfFireObstacleContext activeObstacles = obstacleContext != null && obstacleContext.AppliesTo(input) ? obstacleContext : null;

            // CE 的悬浮窗会把目标宽度按对角线思路放大；蒙特卡洛则使用真实碰撞宽度。
            // 两套值都保留，是为了让界面同时显示“公式参考”和“模拟命中率”。
            float targetCollisionWidthCells = input.TargetWidthMeters / MetersPerCellHeight;
            float targetWidthForCeMath = Mathf.Sqrt(targetCollisionWidthCells * targetCollisionWidthCells * 2f);
            float targetHeightCells = input.TargetHeightMeters / MetersPerCellHeight;
            float targetAimHeight = CalculateTargetAimHeight(input, targetHeightCells);
            float offset = targetAimHeight;
            float gravityPerWidth = CalculateGravityPerWidth(input.GravityFactor);
            float shotAngle = SolveLowArcShotAngle(input.DistanceCells, input.ShotHeightCells, targetAimHeight, input.ShotSpeedCellsPerSecond, gravityPerWidth);
            float rangeError = CalculateRangeError(input.DistanceCells, input.MaxRangeCells, input.AimingAccuracy, input.SightsEfficiency);
            float lightingRangeMultiplier = LightingRangeMultiplier(input.DistanceCells);
            float environmentShift = CalculateEnvironmentShift(input, lightingRangeMultiplier);
            float visibilityError = CalculateVisibilityError(input, environmentShift);
            float leadError = CalculateLeadError(input, lightingRangeMultiplier);
            float effectiveSway = CalculateEffectiveSway(input);
            int burstShotIntervalTicks = CalculateTicksBetweenShots(input.Rpm);
            float visibilityScalar = Mathf.Sqrt(
                visibilityError * visibilityError
                + input.CircularMissRadiusCells * input.CircularMissRadiusCells
                + input.IndirectFireShiftCells * input.IndirectFireShiftCells
                + leadError * leadError);

            var result = new HitChanceResult
            {
                TargetWidthCellsForCeMath = targetWidthForCeMath,
                TargetHeightCells = targetHeightCells,
                TargetAimHeightCells = targetAimHeight,
                ShotAngleRadians = shotAngle,
                VisibilityScalarCells = visibilityScalar,
                VisibilityErrorCells = visibilityError,
                LeadErrorCells = leadError,
                RangeErrorCells = rangeError,
                EnvironmentShift = environmentShift,
                LightingRangeMultiplier = lightingRangeMultiplier,
                RangeVerticalErrorCells = CalculateRangeVerticalError(input, rangeError, targetAimHeight, gravityPerWidth),
                EffectiveSwayDegrees = effectiveSway,
                BurstShotIntervalTicks = burstShotIntervalTicks,
                GravityPerWidth = gravityPerWidth,
                TargetModeForcedTorso = IsTargetModeForcedTorso(input),
                LineOfFireObstaclesApplied = activeObstacles != null,
                LineOfFireObstacleCount = activeObstacles?.ValidObstacleCount(input.DistanceCells) ?? 0,
                LineOfFireObstacleLabel = activeObstacles?.DisplayLabel(),
                CeEstimatedSingle = CalculateCeHitPercent(
                    input.DistanceCells,
                    targetWidthForCeMath,
                    targetHeightCells,
                    offset,
                    input.ShotSpeedCellsPerSecond,
                    shotAngle,
                    effectiveSway,
                    input.SpreadDegrees,
                    visibilityScalar,
                    gravityPerWidth)
            };

            RunMonteCarlo(input, result, targetCollisionWidthCells, targetHeightCells, targetAimHeight, visibilityError, leadError, rangeError, effectiveSway, burstShotIntervalTicks, gravityPerWidth, activeObstacles);
            SetAxisDiagnostics(result);
            return result;
        }

        public static HitChanceAnalysisPoint[] BuildDistanceCurve(HitChanceInputs input)
        {
            float[] candidates = { 10f, 20f, 30f, 40f, 50f };
            float maxRange = Mathf.Max(1f, input.MaxRangeCells);
            int count = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] <= maxRange)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                count = 1;
            }

            var points = new HitChanceAnalysisPoint[count];
            int pointIndex = 0;
            for (int i = 0; i < candidates.Length && pointIndex < count; i++)
            {
                if (candidates[i] > maxRange)
                {
                    continue;
                }

                points[pointIndex] = CalculateDistancePoint(input, candidates[i], pointIndex);
                pointIndex++;
            }

            if (pointIndex == 0)
            {
                points[0] = CalculateDistancePoint(input, maxRange, 0);
            }

            return points;
        }

        private static HitChanceAnalysisPoint CalculateDistancePoint(HitChanceInputs input, float distance, int index)
        {
            HitChanceInputs clone = CloneForAnalysis(input);
            clone.DistanceCells = distance;
            clone.Samples = CalculateAnalysisSamples(input);
            clone.Seed = input.Seed + index * 9973;
            HitChanceResult result = Calculate(clone);
            return new HitChanceAnalysisPoint
            {
                Label = distance.ToString("0.#") + "格",
                X = distance,
                Single = result.MonteCarloSingle,
                BurstAny = result.MonteCarloBurstAny,
                PerShot = result.MonteCarloPerShot
            };
        }

        public static int CalculateAnalysisSamples(HitChanceInputs input)
        {
            return Mathf.Clamp(input.AnalysisSamples, 100, Mathf.Min(input.Samples, 50000));
        }

        public static int CalculateDistributionSamples(HitChanceInputs input)
        {
            return Mathf.Clamp(input.AnalysisSamples, 100, Mathf.Min(input.Samples, 5000));
        }

        public static BallisticDistribution BuildBallisticDistribution(HitChanceInputs source)
        {
            HitChanceInputs input = CloneForAnalysis(source);
            Sanitize(input);

            // 弹道分布只服务于报表可视化，不参与命中率/DPS 结果。
            // 这里复用单发模拟，让图里的轨迹云和蒙特卡洛概率来自同一套近似模型。
            float targetCollisionWidthCells = input.TargetWidthMeters / MetersPerCellHeight;
            float targetHeightCells = input.TargetHeightMeters / MetersPerCellHeight;
            float targetAimHeight = CalculateTargetAimHeight(input, targetHeightCells);
            float gravityPerWidth = CalculateGravityPerWidth(input.GravityFactor);
            float rangeError = CalculateRangeError(input.DistanceCells, input.MaxRangeCells, input.AimingAccuracy, input.SightsEfficiency);
            float lightingRangeMultiplier = LightingRangeMultiplier(input.DistanceCells);
            float environmentShift = CalculateEnvironmentShift(input, lightingRangeMultiplier);
            float visibilityError = CalculateVisibilityError(input, environmentShift);
            float leadError = CalculateLeadError(input, lightingRangeMultiplier);
            float effectiveSway = CalculateEffectiveSway(input);
            int burstShotIntervalTicks = CalculateTicksBetweenShots(input.Rpm);
            int burstShots = Mathf.Clamp(input.BurstShots, 1, 60);
            int samples = CalculateDistributionSamples(input);
            var distribution = new BallisticDistribution
            {
                SampleCount = samples,
                BurstShots = burstShots,
                DistanceCells = input.DistanceCells,
                ShotHeightCells = input.ShotHeightCells,
                TargetWidthCells = targetCollisionWidthCells,
                TargetHeightCells = targetHeightCells,
                TargetAimHeightCells = targetAimHeight,
                HorizontalExtentCells = Mathf.Max(1f, targetCollisionWidthCells),
                VerticalMinCells = Mathf.Min(0f, input.ShotHeightCells, targetAimHeight),
                VerticalMaxCells = Mathf.Max(targetHeightCells, input.ShotHeightCells, targetAimHeight)
            };

            var rng = new System.Random(input.Seed);
            for (int sample = 0; sample < samples; sample++)
            {
                float estimatedDistance = input.DistanceCells + Range(rng, -rangeError, rangeError);
                float burstStartTick = input.SwayStartTick >= 0 ? input.SwayStartTick : Range(rng, 0f, 20000f);
                float shooterPhase = input.ShooterThingId;
                bool hasLockedBaseAim = false;
                float lockedBaseRotationDegrees = 0f;
                float lockedBaseShotAngle = 0f;
                for (int shot = 0; shot < burstShots; shot++)
                {
                    ShotSample shotSample = SimulateShot(
                        input,
                        rng,
                        shot,
                        estimatedDistance,
                        burstStartTick,
                        shooterPhase,
                        burstShotIntervalTicks,
                        targetCollisionWidthCells,
                        targetHeightCells,
                        targetAimHeight,
                        visibilityError,
                        leadError,
                        effectiveSway,
                        gravityPerWidth,
                        hasLockedBaseAim,
                        lockedBaseRotationDegrees,
                        lockedBaseShotAngle);
                    if (!hasLockedBaseAim)
                    {
                        lockedBaseRotationDegrees = shotSample.BaseRotationDegrees;
                        lockedBaseShotAngle = shotSample.BaseShotAngle;
                        hasLockedBaseAim = true;
                    }

                    distribution.Points.Add(new BallisticDistributionPoint
                    {
                        ShotIndex = shot,
                        HorizontalMissCells = shotSample.HorizontalMissCells,
                        HeightAtTargetCells = shotSample.HeightAtTargetCells,
                        FinalShotAngleRadians = shotSample.FinalShotAngleRadians,
                        HorizontalHit = shotSample.HorizontalHit,
                        VerticalHit = shotSample.VerticalHit
                    });
                    distribution.HorizontalExtentCells = Mathf.Max(distribution.HorizontalExtentCells, Mathf.Abs(shotSample.HorizontalMissCells));
                    distribution.VerticalMinCells = Mathf.Min(distribution.VerticalMinCells, shotSample.HeightAtTargetCells);
                    distribution.VerticalMaxCells = Mathf.Max(distribution.VerticalMaxCells, shotSample.HeightAtTargetCells);
                }
            }

            distribution.HorizontalExtentCells = Mathf.Max(distribution.HorizontalExtentCells * 1.08f, targetCollisionWidthCells * 0.65f, 0.5f);
            distribution.VerticalMinCells -= 0.1f;
            distribution.VerticalMaxCells += 0.1f;
            return distribution;
        }

        private static HitChanceInputs CloneForAnalysis(HitChanceInputs source)
        {
            return new HitChanceInputs
            {
                DistanceCells = source.DistanceCells,
                MaxRangeCells = source.MaxRangeCells,
                ShotSpeedCellsPerSecond = source.ShotSpeedCellsPerSecond,
                ShotHeightCells = source.ShotHeightCells,
                GravityFactor = source.GravityFactor,
                TargetHeightMeters = source.TargetHeightMeters,
                TargetWidthMeters = source.TargetWidthMeters,
                TargetArmorSharp = source.TargetArmorSharp,
                TargetArmorBlunt = source.TargetArmorBlunt,
                TargetArmorHeat = source.TargetArmorHeat,
                TargetArmorElectric = source.TargetArmorElectric,
                TargetMode = source.TargetMode,
                SwayDegrees = source.SwayDegrees,
                SpreadDegrees = source.SpreadDegrees,
                RecoilAmount = source.RecoilAmount,
                ShootingAccuracy = source.ShootingAccuracy,
                AimingAccuracy = source.AimingAccuracy,
                SightsEfficiency = source.SightsEfficiency,
                AimingDelayFactor = source.AimingDelayFactor,
                ReloadSpeed = source.ReloadSpeed,
                ReloadFactor = source.ReloadFactor,
                NightVisionEfficiency = source.NightVisionEfficiency,
                AimMode = source.AimMode,
                ShooterSuppressed = source.ShooterSuppressed,
                Darkness = source.Darkness,
                WeatherError = source.WeatherError,
                SmokeDensity = source.SmokeDensity,
                TargetMoveSpeedCellsPerSecond = source.TargetMoveSpeedCellsPerSecond,
                TargetMoveDirectionDegrees = source.TargetMoveDirectionDegrees,
                BlindFiring = source.BlindFiring,
                CircularMissRadiusCells = source.CircularMissRadiusCells,
                IndirectFireShiftCells = source.IndirectFireShiftCells,
                BurstShots = source.BurstShots,
                Rpm = source.Rpm,
                SustainedShotsPerSecond = source.SustainedShotsPerSecond,
                MagazineShots = source.MagazineShots,
                FireWarmupSeconds = source.FireWarmupSeconds,
                FireCooldownSeconds = source.FireCooldownSeconds,
                ReloadSeconds = source.ReloadSeconds,
                FasterRepeatShots = source.FasterRepeatShots,
                Timeline60SecondMode = source.Timeline60SecondMode,
                ShooterThingId = source.ShooterThingId,
                SwayStartTick = source.SwayStartTick,
                Samples = source.Samples,
                Seed = source.Seed,
                AnalysisMode = source.AnalysisMode,
                AnalysisSamples = source.AnalysisSamples
            };
        }

        private static void SetAxisDiagnostics(HitChanceResult result)
        {
            result.HorizontalSaturated = result.MonteCarloHorizontalFirst >= 0.995f;
            result.VerticalSaturated = result.MonteCarloVerticalFirst >= 0.995f;

            if (result.MonteCarloHorizontalFirst + 0.05f < result.MonteCarloVerticalFirst)
            {
                result.DominantAxis = HitChanceLimitAxis.Horizontal;
            }
            else if (result.MonteCarloVerticalFirst + 0.05f < result.MonteCarloHorizontalFirst)
            {
                result.DominantAxis = HitChanceLimitAxis.Vertical;
            }
            else
            {
                result.DominantAxis = HitChanceLimitAxis.Mixed;
            }
        }

        public static float CalculateGravityPerWidth(float gravityFactor)
        {
            return GravityConst * Mathf.Max(0f, gravityFactor);
        }

        public static float CalculateAccuracyFactor(float aimingAccuracy, float sightsEfficiency)
        {
            return (1.5f - aimingAccuracy) / Mathf.Max(0.02f, sightsEfficiency);
        }

        public static float CalculateRangeError(float distanceCells, float maxRangeCells, float aimingAccuracy, float sightsEfficiency)
        {
            float accuracyFactor = CalculateAccuracyFactor(aimingAccuracy, sightsEfficiency);
            return distanceCells * (distanceCells / Math.Max(maxRangeCells, 20f)) * Mathf.Min(accuracyFactor * 0.5f, 0.8f);
        }

        public static float CalculateSwayAmplitude(float shootingAccuracy, float swayFactor)
        {
            return Mathf.Max(0f, (4.5f - Mathf.Min(shootingAccuracy, 4.5f)) * swayFactor);
        }

        public static float CalculateEffectiveSway(HitChanceInputs input)
        {
            float baseSway = Mathf.Max(0f, input.SwayDegrees);
            if (input.ShooterSuppressed)
            {
                return baseSway * 1.5f;
            }

            if (input.AimMode == HitChanceAimMode.AimedShot)
            {
                return baseSway * Mathf.Max(0f, 1f - input.AimingAccuracy) / Mathf.Max(1f, input.SightsEfficiency);
            }

            return baseSway;
        }

        public static float CalculateTargetAimHeight(HitChanceInputs input, float targetHeightCells)
        {
            if (IsTargetModeForcedTorso(input))
            {
                return TorsoAimHeight(targetHeightCells);
            }

            switch (input.TargetMode)
            {
                case HitChanceTargetMode.Head:
                    return (targetHeightCells * 0.85f + targetHeightCells) * 0.5f;
                case HitChanceTargetMode.Legs:
                    return (0f + targetHeightCells * 0.45f) * 0.5f;
                default:
                    return TorsoAimHeight(targetHeightCells);
            }
        }

        public static bool IsTargetModeForcedTorso(HitChanceInputs input)
        {
            return input.AimMode == HitChanceAimMode.SuppressFire || input.ShootingAccuracy < 2.45f;
        }

        private static float TorsoAimHeight(float targetHeightCells)
        {
            return (targetHeightCells * 0.45f + targetHeightCells * 0.85f) * 0.5f;
        }

        public static float CalculateEnvironmentShift(HitChanceInputs input, float lightingRangeMultiplier)
        {
            // 黑暗、天气、烟雾和盲射没有完全照搬 CE 内部每个分支。
            // 这里保留为可解释的综合误差，距离越远影响越明显；夜视效率只削减黑暗项。
            float darkness = input.BlindFiring ? 1f : CalculateEffectiveDarkness(input.Darkness, input.NightVisionEfficiency);
            return ((darkness * 7f) + Mathf.Clamp01(input.WeatherError) * 1.5f) * lightingRangeMultiplier + Mathf.Max(0f, input.SmokeDensity);
        }

        public static float CalculateEffectiveDarkness(float darkness, float nightVisionEfficiency)
        {
            return Mathf.Max(0f, Mathf.Clamp01(darkness) * (1f - Mathf.Clamp01(nightVisionEfficiency)));
        }

        public static float CalculateVisibilityError(HitChanceInputs input, float environmentShift)
        {
            float sightsEfficiency = Mathf.Max(0.02f, input.SightsEfficiency);
            return environmentShift * (input.DistanceCells / 50f / sightsEfficiency) * (2f - input.AimingAccuracy);
        }

        public static float CalculateLeadError(HitChanceInputs input, float lightingRangeMultiplier)
        {
            if (input.BlindFiring)
            {
                return 0f;
            }

            float leadDistance = CalculateLeadDistance(input);
            float accuracyFactor = CalculateAccuracyFactor(input.AimingAccuracy, input.SightsEfficiency);
            float darkness = CalculateEffectiveDarkness(input.Darkness, input.NightVisionEfficiency);
            return leadDistance * Mathf.Min(accuracyFactor * 0.25f, 2.5f)
                + Mathf.Min(darkness * lightingRangeMultiplier * leadDistance * 0.25f, 2.0f)
                + Mathf.Min(Mathf.Max(0f, input.SmokeDensity) * 0.5f, 2.0f);
        }

        public static float CalculateLeadDistance(HitChanceInputs input)
        {
            return Mathf.Max(0f, input.TargetMoveSpeedCellsPerSecond) * input.DistanceCells / Mathf.Max(1f, input.ShotSpeedCellsPerSecond);
        }

        public static float LightingRangeMultiplier(float range)
        {
            // CE 悬浮窗里黑暗/能见度误差会随距离放大。
            // 这里把反编译得到的分段趋势固化成曲线，避免 UI 每次再去依赖 CE 内部实现。
            return EvaluateLightingCurve(range);
        }

        private static float EvaluateLightingCurve(float range)
        {
            if (range <= 5f)
            {
                return 0.05f;
            }
            if (range <= 10f)
            {
                return Lerp(5f, 0.05f, 10f, 0.15f, range);
            }
            if (range <= 22f)
            {
                return Lerp(10f, 0.15f, 22f, 0.475f, range);
            }
            if (range <= 35f)
            {
                return Lerp(22f, 0.475f, 35f, 1f, range);
            }
            if (range <= 60f)
            {
                return Lerp(35f, 1f, 60f, 1.2f, range);
            }
            if (range <= 90f)
            {
                return Lerp(60f, 1.2f, 90f, 2f, range);
            }
            return 2f;
        }

        private static float Lerp(float x0, float y0, float x1, float y1, float x)
        {
            return y0 + (y1 - y0) * ((x - x0) / (x1 - x0));
        }

        public static float CalculateRangeVerticalError(HitChanceInputs input, float rangeError, float targetCenterHeight, float gravityPerWidth)
        {
            // 射程误差在本模型里主要影响垂直落点：射手估错距离后会用错误距离解仰角，
            // 真正飞到当前目标平面时就表现为高低偏差。
            if (rangeError <= 0f)
            {
                return 0f;
            }

            float lowDistance = Mathf.Max(0.1f, input.DistanceCells - rangeError);
            float highDistance = Mathf.Max(0.1f, input.DistanceCells + rangeError);
            float lowAngle = SolveLowArcShotAngle(lowDistance, input.ShotHeightCells, targetCenterHeight, input.ShotSpeedCellsPerSecond, gravityPerWidth);
            float highAngle = SolveLowArcShotAngle(highDistance, input.ShotHeightCells, targetCenterHeight, input.ShotSpeedCellsPerSecond, gravityPerWidth);
            float lowError = ProjectileHeightAtDistance(input.DistanceCells, input.ShotHeightCells, input.ShotSpeedCellsPerSecond, lowAngle, gravityPerWidth) - targetCenterHeight;
            float highError = ProjectileHeightAtDistance(input.DistanceCells, input.ShotHeightCells, input.ShotSpeedCellsPerSecond, highAngle, gravityPerWidth) - targetCenterHeight;
            return Mathf.Max(Mathf.Abs(lowError), Mathf.Abs(highError));
        }

        private static void RunMonteCarlo(HitChanceInputs input, HitChanceResult result, float targetWidthCells, float targetHeightCells, float targetAimHeight, float visibilityError, float leadError, float rangeError, float effectiveSway, int burstShotIntervalTicks, float gravityPerWidth, LineOfFireObstacleContext obstacleContext)
        {
            // 蒙特卡洛负责把水平误差、垂直弹道、连发后坐力和近失分开统计。
            // 采样量越高越稳定，但 OnGUI 中不能反复重算，调用方必须依赖缓存。
            int burstShots = Mathf.Clamp(input.BurstShots, 1, 60);
            int samples = Mathf.Clamp(input.Samples, 100, 200000);
            var rng = new System.Random(input.Seed);
            var perShotHits = new int[burstShots];
            var perShotNearMisses = new int[burstShots];
            var perShotBlocked = obstacleContext != null ? new int[burstShots] : null;
            int firstShotHorizontalHits = 0;
            int firstShotVerticalHits = 0;
            int burstAnyHits = 0;
            int burstAnyBlocked = 0;

            for (int sample = 0; sample < samples; sample++)
            {
                bool burstHit = false;
                bool burstBlocked = false;
                float estimatedDistance = input.DistanceCells + Range(rng, -rangeError, rangeError);
                float burstStartTick = input.SwayStartTick >= 0 ? input.SwayStartTick : Range(rng, 0f, 20000f);
                float shooterPhase = input.ShooterThingId;
                bool hasLockedBaseAim = false;
                float lockedBaseRotationDegrees = 0f;
                float lockedBaseShotAngle = 0f;
                for (int shot = 0; shot < burstShots; shot++)
                {
                    ShotSample shotSample = SimulateShot(
                        input,
                        rng,
                        shot,
                        estimatedDistance,
                        burstStartTick,
                        shooterPhase,
                        burstShotIntervalTicks,
                        targetWidthCells,
                        targetHeightCells,
                        targetAimHeight,
                        visibilityError,
                        leadError,
                        effectiveSway,
                        gravityPerWidth,
                        hasLockedBaseAim,
                        lockedBaseRotationDegrees,
                        lockedBaseShotAngle);
                    if (!hasLockedBaseAim)
                    {
                        lockedBaseRotationDegrees = shotSample.BaseRotationDegrees;
                        lockedBaseShotAngle = shotSample.BaseShotAngle;
                        hasLockedBaseAim = true;
                    }

                    if (shot == 0)
                    {
                        if (shotSample.HorizontalHit)
                        {
                            firstShotHorizontalHits++;
                        }
                        if (shotSample.VerticalHit)
                        {
                            firstShotVerticalHits++;
                        }
                    }

                    bool blocked = IsBlockedByObstacle(input, obstacleContext, shotSample, gravityPerWidth);
                    if (blocked)
                    {
                        perShotBlocked[shot]++;
                        burstBlocked = true;
                    }
                    else if (shotSample.Hit)
                    {
                        perShotHits[shot]++;
                        burstHit = true;
                    }
                    else if (!shotSample.HorizontalHit && shotSample.VerticalHit)
                    {
                        perShotNearMisses[shot]++;
                    }
                }

                if (burstHit)
                {
                    burstAnyHits++;
                }
                if (burstBlocked)
                {
                    burstAnyBlocked++;
                }
            }

            result.MonteCarloPerShot = new float[burstShots];
            result.MonteCarloNearMissPerShot = new float[burstShots];
            if (obstacleContext != null)
            {
                result.LineOfFireBlockedPerShot = new float[burstShots];
            }
            for (int i = 0; i < burstShots; i++)
            {
                result.MonteCarloPerShot[i] = perShotHits[i] / (float)samples;
                result.MonteCarloNearMissPerShot[i] = perShotNearMisses[i] / (float)samples;
                if (obstacleContext != null)
                {
                    result.LineOfFireBlockedPerShot[i] = perShotBlocked[i] / (float)samples;
                }
            }

            result.MonteCarloSingle = result.MonteCarloPerShot[0];
            result.MonteCarloNearMissSingle = result.MonteCarloNearMissPerShot[0];
            result.MonteCarloBurstAny = burstAnyHits / (float)samples;
            if (obstacleContext != null)
            {
                result.LineOfFireBlockedSingle = result.LineOfFireBlockedPerShot[0];
                result.LineOfFireBlockedBurstAny = burstAnyBlocked / (float)samples;
            }
            result.MonteCarloHorizontalFirst = firstShotHorizontalHits / (float)samples;
            result.MonteCarloVerticalFirst = firstShotVerticalHits / (float)samples;
        }

        public static int CalculateTicksBetweenShots(float rpm)
        {
            return rpm > 0f ? Mathf.Max(1, Mathf.RoundToInt(3600f / rpm)) : 6;
        }

        public static int CalculateAimedExtraTicks(float distanceCells, HitChanceAimMode aimMode, bool shooterSuppressed)
        {
            if (shooterSuppressed || aimMode != HitChanceAimMode.AimedShot)
            {
                return 0;
            }

            return (int)Mathf.Lerp(30f, 240f, Mathf.Clamp01(Mathf.Max(0f, distanceCells) / 100f));
        }

        private static ShotSample SimulateShot(HitChanceInputs input, System.Random rng, int shotIndex, float estimatedDistance, float burstStartTick, float shooterPhase, int burstShotIntervalTicks, float targetWidthCells, float targetHeightCells, float targetAimHeight, float visibilityError, float leadError, float effectiveSway, float gravityPerWidth, bool hasLockedBaseAim, float lockedBaseRotationDegrees, float lockedBaseShotAngle)
        {
            Vector2 sourceLoc = Vector2.zero;
            Vector2 actualTargetLoc = new Vector2(0f, input.DistanceCells);
            Vector2 visibility = RandomInCircle(rng, visibilityError + input.CircularMissRadiusCells + input.IndirectFireShiftCells);
            Vector2 shiftedTargetLoc = actualTargetLoc + visibility;

            if (estimatedDistance > 0f && shiftedTargetLoc.sqrMagnitude > 0.0001f)
            {
                shiftedTargetLoc = sourceLoc + shiftedTargetLoc.normalized * estimatedDistance;
            }

            if (!input.BlindFiring)
            {
                shiftedTargetLoc += CalculateLeadVector(input, CalculateLeadDistance(input) + Range(rng, -leadError, leadError));
            }

            float baseRotationDegrees = RotationDegreesTo(shiftedTargetLoc);
            float shiftedTargetDistance = Mathf.Max(0.01f, shiftedTargetLoc.magnitude);
            float baseShotAngle = SolveLowArcShotAngle(shiftedTargetDistance, input.ShotHeightCells, targetAimHeight, input.ShotSpeedCellsPerSecond, gravityPerWidth);
            float sampledBaseRotationDegrees = baseRotationDegrees;
            float sampledBaseShotAngle = baseShotAngle;
            // 同一次连发只在首发时重新估计基础瞄准点；后续弹沿用该瞄准基准，
            // 再叠加散布、枪口摆动和后坐力。这是目前最贴近 CE 连发体感的近似。
            if (hasLockedBaseAim)
            {
                baseRotationDegrees = lockedBaseRotationDegrees;
                baseShotAngle = lockedBaseShotAngle;
            }

            Vector2 spread = RandomInsideUnitCircle(rng, input.SpreadDegrees);
            Vector2 sway = SwayAtTick(effectiveSway, burstStartTick + shooterPhase + shotIndex * burstShotIntervalTicks);
            Vector2 recoil = RandomRecoil(rng, input.RecoilAmount, input.ShootingAccuracy, shotIndex);

            float finalRotationDegrees = baseRotationDegrees + spread.x + sway.x + recoil.x;
            float finalShotAngle = baseShotAngle + (spread.y + sway.y + recoil.y) * Mathf.Deg2Rad;

            float rotationRadians = finalRotationDegrees * Mathf.Deg2Rad;
            float cosRotation = Mathf.Cos(rotationRadians);
            if (cosRotation <= 0.001f)
            {
                return new ShotSample(false, false, 9999f, -9999f, finalShotAngle, sampledBaseRotationDegrees, sampledBaseShotAngle, rotationRadians);
            }

            float horizontalTravelToTargetPlane = input.DistanceCells / cosRotation;
            float futureTargetX = input.BlindFiring ? 0f : CalculateLeadVector(input, CalculateLeadDistance(input)).x;
            float horizontalMiss = Mathf.Tan(rotationRadians) * input.DistanceCells - futureTargetX;
            float projectileHeightAtTarget = ProjectileHeightAtDistance(horizontalTravelToTargetPlane, input.ShotHeightCells, input.ShotSpeedCellsPerSecond, finalShotAngle, gravityPerWidth);

            bool horizontalHit = Mathf.Abs(horizontalMiss) <= targetWidthCells * 0.5f;
            bool verticalHit = projectileHeightAtTarget >= 0f && projectileHeightAtTarget <= targetHeightCells;
            return new ShotSample(horizontalHit, verticalHit, horizontalMiss, projectileHeightAtTarget, finalShotAngle, sampledBaseRotationDegrees, sampledBaseShotAngle, rotationRadians);
        }

        private static bool IsBlockedByObstacle(HitChanceInputs input, LineOfFireObstacleContext obstacleContext, ShotSample shotSample, float gravityPerWidth)
        {
            if (obstacleContext == null || !obstacleContext.Enabled || obstacleContext.Obstacles.Count == 0)
            {
                return false;
            }

            float cosRotation = Mathf.Cos(shotSample.FinalRotationRadians);
            if (cosRotation <= 0.001f)
            {
                return false;
            }

            float tanRotation = Mathf.Tan(shotSample.FinalRotationRadians);
            for (int i = 0; i < obstacleContext.Obstacles.Count; i++)
            {
                LineOfFireObstacle obstacle = obstacleContext.Obstacles[i];
                if (obstacle == null || !obstacle.IsValid(input.DistanceCells))
                {
                    continue;
                }

                float horizontalAtObstacle = tanRotation * obstacle.DistanceCells;
                if (Mathf.Abs(horizontalAtObstacle - obstacle.CenterOffsetCells) > obstacle.HalfWidthCells)
                {
                    continue;
                }

                float travelDistance = obstacle.DistanceCells / cosRotation;
                float heightAtObstacle = ProjectileHeightAtDistance(travelDistance, input.ShotHeightCells, input.ShotSpeedCellsPerSecond, shotSample.FinalShotAngleRadians, gravityPerWidth);
                if (heightAtObstacle >= obstacle.MinHeightCells && heightAtObstacle <= obstacle.MaxHeightCells)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct ShotSample
        {
            public readonly bool HorizontalHit;
            public readonly bool VerticalHit;
            public readonly float HorizontalMissCells;
            public readonly float HeightAtTargetCells;
            public readonly float FinalShotAngleRadians;
            public readonly float FinalRotationRadians;
            public readonly float BaseRotationDegrees;
            public readonly float BaseShotAngle;
            public bool Hit => HorizontalHit && VerticalHit;

            public ShotSample(bool horizontalHit, bool verticalHit, float horizontalMissCells = 0f, float heightAtTargetCells = 0f, float finalShotAngleRadians = 0f, float baseRotationDegrees = 0f, float baseShotAngle = 0f, float finalRotationRadians = 0f)
            {
                HorizontalHit = horizontalHit;
                VerticalHit = verticalHit;
                HorizontalMissCells = horizontalMissCells;
                HeightAtTargetCells = heightAtTargetCells;
                FinalShotAngleRadians = finalShotAngleRadians;
                FinalRotationRadians = finalRotationRadians;
                BaseRotationDegrees = baseRotationDegrees;
                BaseShotAngle = baseShotAngle;
            }
        }

        private static Vector2 SwayAtTick(float swayDegrees, float tick)
        {
            // 枪口摆动近似为两个不同频率的正弦波；thingID/tick 只改变相位。
            // 这不是 CE 的逐字段复刻，但可以稳定表现“同一射手同一时刻摆动一致”的性质。
            return new Vector2(
                swayDegrees * Mathf.Sin(tick * 0.022f),
                0.25f * swayDegrees * Mathf.Sin(tick * 0.0165f));
        }

        private static Vector2 RandomRecoil(System.Random rng, float recoilAmount, float shootingAccuracy, int shotIndex)
        {
            // 首发不吃后坐力；后续发按 CE 体感近似累积，到第 10 发左右封顶。
            // shootingAccuracy 在这里代表“武器掌握/压枪能力”，越高后坐力增长越慢。
            if (shotIndex <= 0 || recoilAmount <= 0f)
            {
                return Vector2.zero;
            }

            float recoilMagnitude = Mathf.Pow(5f - Mathf.Min(shootingAccuracy, 4.5f), Mathf.Min(10, shotIndex) / 6.25f);
            float x = recoilMagnitude * Range(rng, -recoilAmount * 0.5f, recoilAmount * 0.5f);
            float y = recoilMagnitude * Range(rng, -recoilAmount / 3f, recoilAmount);
            return new Vector2(x, y);
        }

        private static Vector2 RandomInCircle(System.Random rng, float radius)
        {
            if (radius <= 0f)
            {
                return Vector2.zero;
            }

            double angle = rng.NextDouble() * Math.PI * 2.0;
            double range = rng.NextDouble() * radius;
            return new Vector2((float)(range * Math.Cos(angle)), (float)(range * Math.Sin(angle)));
        }

        private static Vector2 RandomInsideUnitCircle(System.Random rng, float radius)
        {
            if (radius <= 0f)
            {
                return Vector2.zero;
            }

            double angle = rng.NextDouble() * Math.PI * 2.0;
            double range = Math.Sqrt(rng.NextDouble()) * radius;
            return new Vector2((float)(range * Math.Cos(angle)), (float)(range * Math.Sin(angle)));
        }

        private static Vector2 CalculateLeadVector(HitChanceInputs input, float leadDistance)
        {
            float radians = input.TargetMoveDirectionDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * leadDistance;
        }

        private static float ApproximateShotAngle(float distance, float shotHeight, float targetHeight, float shotSpeed, float gravityPerWidth)
        {
            float heightDelta = targetHeight - shotHeight;
            float gravityTerm = gravityPerWidth * distance / (2f * shotSpeed * shotSpeed);
            return Mathf.Atan(heightDelta / Mathf.Max(distance, 0.01f) + gravityTerm);
        }

        private static float SolveLowArcShotAngle(float distance, float shotHeight, float targetHeight, float shotSpeed, float gravityPerWidth)
        {
            float heightDelta = targetHeight - shotHeight;
            float speedSquared = shotSpeed * shotSpeed;
            float discriminant = speedSquared * speedSquared - gravityPerWidth * (gravityPerWidth * distance * distance + 2f * heightDelta * speedSquared);

            if (discriminant < 0f)
            {
                return ApproximateShotAngle(distance, shotHeight, targetHeight, shotSpeed, gravityPerWidth);
            }

            if (gravityPerWidth <= 0.0001f)
            {
                return Mathf.Atan(heightDelta / Mathf.Max(distance, 0.01f));
            }

            float tanTheta = (speedSquared - Mathf.Sqrt(discriminant)) / (gravityPerWidth * Mathf.Max(distance, 0.01f));
            return Mathf.Atan(tanTheta);
        }

        private static float RotationDegreesTo(Vector2 target)
        {
            return Mathf.Atan2(target.x, target.y) * Mathf.Rad2Deg;
        }

        private static float ProjectileHeightAtDistance(float distance, float shotHeight, float shotSpeed, float shotAngle, float gravityPerWidth)
        {
            float horizontalSpeed = Mathf.Max(0.01f, shotSpeed * Mathf.Cos(shotAngle));
            float time = distance / horizontalSpeed;
            return shotHeight + shotSpeed * Mathf.Sin(shotAngle) * time - gravityPerWidth * time * time * 0.5f;
        }

        private static float CalculateCeHitPercent(float dist, float w, float h, float offset, float shotSpeed, float shotAngle, float swayDegrees, float spreadDegrees, float visibilityShift, float gravity)
        {
            // 公式近似参考：把多个误差项合成为近似正态分布，再分别算水平/垂直穿过目标盒的概率。
            // 这项主要用于和 CE 悬浮窗数值对照，真正的连发和弹道可视化以后面的蒙特卡洛为准。
            float sigmaSwayXz = swayDegrees * Deg2RadOverSqrt2;
            float sigmaSwayTheta = sigmaSwayXz / 4.0f;
            float sigmaSpread = spreadDegrees * Deg2RadOverSqrt2 / 2.0f;
            float sigmaTargetXz = visibilityShift / dist / Sqrt2;

            float sigmaXz = Mathf.Sqrt(sigmaSwayXz * sigmaSwayXz + sigmaSpread * sigmaSpread + sigmaTargetXz * sigmaTargetXz);
            float sigmaTheta = Mathf.Sqrt(sigmaSwayTheta * sigmaSwayTheta + sigmaSpread * sigmaSpread);
            float sigmaDist = visibilityShift / Sqrt2;
            float dPrime = Mathf.Sqrt(dist * dist + sigmaDist * sigmaDist);
            float sigmaHorizontal = sigmaXz * dPrime;
            float sigmaY = sigmaTheta * dPrime;

            float cosTheta = Mathf.Cos(shotAngle);
            float sigmaGravity = gravity * dist * sigmaDist / (2f * shotSpeed * shotSpeed * cosTheta * cosTheta);
            float sigmaVertical = Mathf.Sqrt(sigmaY * sigmaY + sigmaGravity * sigmaGravity);

            float pHorizontal = sigmaHorizontal > 0f
                ? 2f * NormalCdf((w * 0.5f) / sigmaHorizontal) - 1f
                : 1f;

            float pVertical = 1f;
            if (sigmaVertical > 0f)
            {
                float higher = (h - offset) / sigmaVertical;
                float lower = -offset / sigmaVertical;
                pVertical = NormalCdf(higher) - NormalCdf(lower);
            }

            return Mathf.Clamp01(pHorizontal * pVertical);
        }

        private static float NormalCdf(float x)
        {
            return (1f + Erf(x / Sqrt2)) / 2f;
        }

        private static float Erf(float x)
        {
            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;

            int sign = 1;
            if (x < 0f)
            {
                sign = -1;
                x = -x;
            }

            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
            return (float)(sign * y);
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private static void Sanitize(HitChanceInputs input)
        {
            input.DistanceCells = Mathf.Max(0.1f, input.DistanceCells);
            input.MaxRangeCells = Mathf.Max(1f, input.MaxRangeCells);
            input.ShotSpeedCellsPerSecond = Mathf.Max(1f, input.ShotSpeedCellsPerSecond);
            input.GravityFactor = Mathf.Max(0f, input.GravityFactor);
            input.TargetHeightMeters = Mathf.Max(0.05f, input.TargetHeightMeters);
            input.TargetWidthMeters = Mathf.Max(0.05f, input.TargetWidthMeters);
            if (!Enum.IsDefined(typeof(HitChanceTargetMode), input.TargetMode))
            {
                input.TargetMode = HitChanceTargetMode.Torso;
            }
            input.SightsEfficiency = Mathf.Max(0.02f, input.SightsEfficiency);
            input.AimingDelayFactor = Mathf.Clamp(input.AimingDelayFactor, 0.01f, 2f);
            input.ReloadSpeed = Mathf.Max(0.001f, input.ReloadSpeed);
            input.ReloadFactor = Mathf.Max(0.01f, input.ReloadFactor);
            input.NightVisionEfficiency = Mathf.Clamp01(input.NightVisionEfficiency);
            input.Darkness = Mathf.Clamp01(input.Darkness);
            input.WeatherError = Mathf.Clamp01(input.WeatherError);
            input.SmokeDensity = Mathf.Max(0f, input.SmokeDensity);
            input.TargetMoveSpeedCellsPerSecond = Mathf.Max(0f, input.TargetMoveSpeedCellsPerSecond);
            input.TargetMoveDirectionDegrees = Mathf.Repeat(input.TargetMoveDirectionDegrees, 360f);
            input.Rpm = Mathf.Max(0f, input.Rpm);
            input.SustainedShotsPerSecond = Mathf.Max(0f, input.SustainedShotsPerSecond);
            input.MagazineShots = Mathf.Max(0, input.MagazineShots);
            input.FireWarmupSeconds = Mathf.Max(0f, input.FireWarmupSeconds);
            input.FireCooldownSeconds = Mathf.Max(0f, input.FireCooldownSeconds);
            input.ReloadSeconds = Mathf.Max(0f, input.ReloadSeconds);
            input.SwayStartTick = Mathf.Max(-1, input.SwayStartTick);
            input.AnalysisSamples = Mathf.Clamp(input.AnalysisSamples, 100, 50000);
            if (!Enum.IsDefined(typeof(HitChanceAimMode), input.AimMode))
            {
                input.AimMode = HitChanceAimMode.AimedShot;
            }
            if (!Enum.IsDefined(typeof(HitChanceAnalysisMode), input.AnalysisMode))
            {
                input.AnalysisMode = HitChanceAnalysisMode.None;
            }
            input.BurstShots = Mathf.Clamp(input.BurstShots, 1, 60);
            input.Samples = Mathf.Clamp(input.Samples, 100, 200000);
        }
    }
}
