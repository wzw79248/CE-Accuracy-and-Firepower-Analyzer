using System;

namespace CEHitChanceCalculator.Tests
{
    internal static class TestProgram
    {
        private static int failures;

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("CE Hit Chance Calculator trend tests");
            Console.WriteLine("------------------------------------");

            Run("baseline", HitChanceCalculator.Calculate(Baseline()));
            Trend("distance 20 -> 40 lowers hit chance", Baseline(), x => x.DistanceCells = 40f, expectLower: true);
            Trend("spread 0.08 -> 2.0 lowers hit chance", SpreadSensitiveBaseline(), x => x.SpreadDegrees = 2.0f, expectLower: true);
            Trend("snapshot sway 0.2 -> 2.0 lowers hit chance", SwaySensitiveBaseline(), x => x.SwayDegrees = 2.0f, expectLower: true);
            Trend("darkness 0 -> 0.8 lowers hit chance", BaselineNoDarkness(), x => x.Darkness = 0.8f, expectLower: true);
            Trend("night vision reduces darkness penalty", DarknessSensitiveBaseline(), x => x.NightVisionEfficiency = 0.8f, expectLower: false);
            Trend("smoke 0 -> 2 lowers hit chance", BaselineNoDarkness(), x => x.SmokeDensity = 2f, expectLower: true);
            Trend("burst recoil lowers later shots", BurstBaseline(), CheckBurstRecoil);
            CheckWeaponHandlingImprovesBurst();
            CheckRangeErrorDrops();
            CheckRangeErrorAffectsHorizontalWithLeadOffset();
            Trend("target moving away has less horizontal miss than lateral movement", LateralMovementBaseline(), x => x.TargetMoveDirectionDegrees = 0f, expectLower: false);
            CheckRpmChangesBurst();
            CheckAimModeSway();
            CheckTargetModeAimHeight();
            CheckTargetCoverAdjustsAimHeights();
            CheckGravityFactorChangesBallistics();
            CheckInstantProjectileUsesRayApproximation();
            CheckInstantDamageFalloffIgnoresMechanicalSpread();
            CheckAxisDiagnostics();
            CheckShooterThingIdChangesSwayPhase();
            CheckRpmToWholeTicks();
            CheckLightingCurvePoints();
            CheckDistanceCurveUsesCappedSamples();
            CheckDpsUsesDirectHitChance();
            CheckLaserDamageFalloffReducesDirectDamage();
            CheckArmorDamageFormula();
            CheckExplosiveArmorPenetrationRules();
            CheckAttachedExplosiveDamageCountsAsDirectArmorDamage();
            CheckHeatAndElectricArmorFormula();
            CheckAimModeTimingAffectsSustainedDps();
            CheckShooterTimingStatsAffectDps();
            CheckFasterRepeatShotsShortensRepeatWarmup();
            CheckTimelineMatchesMagazineFireTime();
            CheckTimelineWindowIncludesReloads();
            CheckBallisticDistributionSupportsLongBursts();

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("PASS: all trend checks passed.");
                return 0;
            }

            Console.WriteLine("FAIL: " + failures + " trend check(s) failed.");
            return 1;
        }

        private static HitChanceInputs Baseline()
        {
            return new HitChanceInputs
            {
                DistanceCells = 35f,
                MaxRangeCells = 100f,
                ShotSpeedCellsPerSecond = 80f,
                ShotHeightCells = 0.85f,
                ShooterMaxHeightCells = 1f,
                TargetHeightMeters = 1.75f,
                TargetWidthMeters = 0.88f,
                SwayDegrees = 0.92f,
                SpreadDegrees = 0.08f,
                RecoilAmount = 1.49f,
                ShootingAccuracy = 3f,
                AimingAccuracy = 1f,
                SightsEfficiency = 2f,
                Darkness = 0.2f,
                WeatherError = 0f,
                SmokeDensity = 0f,
                TargetMoveSpeedCellsPerSecond = 0f,
                BurstShots = 1,
                Samples = 30000,
                Seed = 12345
            };
        }

        private static HitChanceInputs BaselineNoDarkness()
        {
            HitChanceInputs input = Baseline();
            input.Darkness = 0f;
            return input;
        }

        private static HitChanceInputs DarknessSensitiveBaseline()
        {
            HitChanceInputs input = BaselineNoDarkness();
            input.Darkness = 0.8f;
            input.DistanceCells = 55f;
            input.TargetMoveSpeedCellsPerSecond = 4f;
            input.TargetMoveDirectionDegrees = 90f;
            return input;
        }

        private static HitChanceInputs BurstBaseline()
        {
            HitChanceInputs input = Baseline();
            input.DistanceCells = 25f;
            input.BurstShots = 5;
            input.RecoilAmount = 2f;
            return input;
        }

        private static HitChanceInputs SpreadSensitiveBaseline()
        {
            HitChanceInputs input = BaselineNoDarkness();
            input.DistanceCells = 45f;
            input.SwayDegrees = 0.1f;
            input.SpreadDegrees = 0.08f;
            input.MaxRangeCells = 300f;
            return input;
        }

        private static HitChanceInputs SwaySensitiveBaseline()
        {
            HitChanceInputs input = BaselineNoDarkness();
            input.DistanceCells = 45f;
            input.MaxRangeCells = 400f;
            input.TargetWidthMeters = 0.88f;
            input.TargetHeightMeters = 1.75f;
            input.SwayDegrees = 0.2f;
            input.SpreadDegrees = 0.02f;
            input.AimMode = HitChanceAimMode.Snapshot;
            input.AimingAccuracy = 1.5f;
            input.SwayStartTick = 71;
            return input;
        }

        private static HitChanceInputs BurstBaselineLowHandling()
        {
            HitChanceInputs input = BurstBaseline();
            input.ShootingAccuracy = 1.5f;
            input.DistanceCells = 35f;
            input.MaxRangeCells = 400f;
            input.TargetWidthMeters = 0.3f;
            input.TargetHeightMeters = 1.4f;
            input.SwayDegrees = 0.1f;
            input.SpreadDegrees = 0.02f;
            input.RecoilAmount = 5f;
            input.BurstShots = 5;
            return input;
        }

        private static HitChanceInputs BaselineRangeSensitive()
        {
            HitChanceInputs input = Baseline();
            input.DistanceCells = 55f;
            input.MaxRangeCells = 40f;
            input.ShotSpeedCellsPerSecond = 50f;
            return input;
        }

        private static HitChanceInputs RangeHorizontalSensitive()
        {
            HitChanceInputs input = BaselineNoDarkness();
            input.DistanceCells = 55f;
            input.MaxRangeCells = 40f;
            input.ShotSpeedCellsPerSecond = 120f;
            input.SwayDegrees = 0.05f;
            input.SpreadDegrees = 0.02f;
            input.Darkness = 0f;
            input.SightsEfficiency = 0.6f;
            input.AimingAccuracy = 0.8f;
            input.TargetMoveSpeedCellsPerSecond = 8f;
            input.TargetMoveDirectionDegrees = 90f;
            input.TargetWidthMeters = 0.35f;
            input.TargetHeightMeters = 4f;
            return input;
        }

        private static HitChanceInputs LateralMovementBaseline()
        {
            HitChanceInputs input = RangeHorizontalSensitive();
            input.MaxRangeCells = 400f;
            input.TargetMoveDirectionDegrees = 90f;
            return input;
        }

        private static HitChanceInputs RpmBurstBaseline()
        {
            HitChanceInputs input = BaselineNoDarkness();
            input.DistanceCells = 45f;
            input.MaxRangeCells = 500f;
            input.TargetWidthMeters = 0.88f;
            input.TargetHeightMeters = 1.3f;
            input.SwayDegrees = 0.5f;
            input.SpreadDegrees = 0f;
            input.RecoilAmount = 0f;
            input.BurstShots = 5;
            input.Rpm = 60f;
            input.AimMode = HitChanceAimMode.Snapshot;
            input.AimingAccuracy = 1.5f;
            input.SwayStartTick = 71;
            return input;
        }

        private static void Trend(string name, HitChanceInputs before, Action<HitChanceInputs> mutate, bool expectLower, bool useBurstAny = false)
        {
            HitChanceInputs after = Clone(before);
            mutate(after);
            HitChanceResult beforeResult = HitChanceCalculator.Calculate(before);
            HitChanceResult afterResult = HitChanceCalculator.Calculate(after);
            float beforeValue = useBurstAny ? beforeResult.MonteCarloBurstAny : beforeResult.MonteCarloSingle;
            float afterValue = useBurstAny ? afterResult.MonteCarloBurstAny : afterResult.MonteCarloSingle;
            bool passed = expectLower ? afterValue < beforeValue : afterValue > beforeValue;
            PrintTrend(name, beforeResult, afterResult, beforeValue, afterValue, passed);
        }

        private static void Trend(string name, HitChanceInputs input, Func<HitChanceResult, bool> check)
        {
            HitChanceResult result = HitChanceCalculator.Calculate(input);
            bool passed = check(result);
            Run(name, result, passed);
        }

        private static bool CheckBurstRecoil(HitChanceResult result)
        {
            return result.MonteCarloPerShot.Length >= 5
                && result.MonteCarloPerShot[4] < result.MonteCarloPerShot[0];
        }

        private static void CheckWeaponHandlingImprovesBurst()
        {
            HitChanceInputs before = BurstBaselineLowHandling();
            HitChanceInputs after = Clone(before);
            after.ShootingAccuracy = 4f;
            HitChanceResult beforeResult = HitChanceCalculator.Calculate(before);
            HitChanceResult afterResult = HitChanceCalculator.Calculate(after);
            bool passed = afterResult.MonteCarloPerShot.Length >= 5
                && beforeResult.MonteCarloPerShot.Length >= 5
                && afterResult.MonteCarloPerShot[4] > beforeResult.MonteCarloPerShot[4];

            Console.WriteLine("weapon handling 1.5 -> 4 improves later burst shots");
            Console.WriteLine("  before #5=" + Percent(beforeResult.MonteCarloPerShot[4])
                + " burstAny=" + Percent(beforeResult.MonteCarloBurstAny)
                + "  after #5=" + Percent(afterResult.MonteCarloPerShot[4])
                + " burstAny=" + Percent(afterResult.MonteCarloBurstAny));
            Mark(passed);
        }

        private static void CheckRangeErrorDrops()
        {
            HitChanceResult beforeResult = HitChanceCalculator.Calculate(BaselineRangeSensitive());
            HitChanceInputs after = BaselineRangeSensitive();
            after.MaxRangeCells = 400f;
            HitChanceResult afterResult = HitChanceCalculator.Calculate(after);
            bool passed = afterResult.RangeErrorCells < beforeResult.RangeErrorCells
                && afterResult.RangeVerticalErrorCells < beforeResult.RangeVerticalErrorCells;
            Console.WriteLine("max range 40 -> 400 lowers range error");
            Console.WriteLine("  before range error=" + beforeResult.RangeErrorCells.ToString("0.###") + ", vertical=" + beforeResult.RangeVerticalErrorCells.ToString("0.####") + ", hit=" + Percent(beforeResult.MonteCarloSingle));
            Console.WriteLine("  after  range error=" + afterResult.RangeErrorCells.ToString("0.###") + ", vertical=" + afterResult.RangeVerticalErrorCells.ToString("0.####") + ", hit=" + Percent(afterResult.MonteCarloSingle));
            Mark(passed);
        }

        private static void CheckRangeErrorAffectsHorizontalWithLeadOffset()
        {
            HitChanceInputs before = RangeHorizontalSensitive();
            HitChanceInputs after = Clone(before);
            after.MaxRangeCells = 400f;
            HitChanceResult beforeResult = HitChanceCalculator.Calculate(before);
            HitChanceResult afterResult = HitChanceCalculator.Calculate(after);
            bool passed = afterResult.MonteCarloHorizontalFirst > beforeResult.MonteCarloHorizontalFirst
                && afterResult.RangeErrorCells < beforeResult.RangeErrorCells;

            Console.WriteLine("max range affects horizontal chance when lead offset exists");
            Console.WriteLine("  before horizontal=" + Percent(beforeResult.MonteCarloHorizontalFirst)
                + ", first=" + Percent(beforeResult.MonteCarloSingle)
                + ", rangeErr=" + beforeResult.RangeErrorCells.ToString("0.###")
                + ", lead=" + beforeResult.LeadErrorCells.ToString("0.###"));
            Console.WriteLine("  after  horizontal=" + Percent(afterResult.MonteCarloHorizontalFirst)
                + ", first=" + Percent(afterResult.MonteCarloSingle)
                + ", rangeErr=" + afterResult.RangeErrorCells.ToString("0.###")
                + ", lead=" + afterResult.LeadErrorCells.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckRpmChangesBurst()
        {
            HitChanceResult beforeResult = HitChanceCalculator.Calculate(RpmBurstBaseline());
            HitChanceInputs after = RpmBurstBaseline();
            after.Rpm = 1200f;
            HitChanceResult afterResult = HitChanceCalculator.Calculate(after);
            bool passed = Math.Abs(afterResult.MonteCarloBurstAny - beforeResult.MonteCarloBurstAny) > 0.001f;

            Console.WriteLine("higher RPM changes burst sway timing");
            Console.WriteLine("  before rpm=60 burstAny=" + Percent(beforeResult.MonteCarloBurstAny));
            Console.WriteLine("  after  rpm=1200 burstAny=" + Percent(afterResult.MonteCarloBurstAny));
            Mark(passed);
        }

        private static void CheckAimModeSway()
        {
            HitChanceInputs quick = BaselineNoDarkness();
            quick.SwayDegrees = 2f;
            quick.AimMode = HitChanceAimMode.Snapshot;

            HitChanceInputs aimed = Clone(quick);
            aimed.AimMode = HitChanceAimMode.AimedShot;
            aimed.AimingAccuracy = 0.75f;
            aimed.SightsEfficiency = 2f;

            HitChanceInputs suppressed = Clone(aimed);
            suppressed.ShooterSuppressed = true;

            HitChanceResult quickResult = HitChanceCalculator.Calculate(quick);
            HitChanceResult aimedResult = HitChanceCalculator.Calculate(aimed);
            HitChanceResult suppressedResult = HitChanceCalculator.Calculate(suppressed);
            bool passed = aimedResult.EffectiveSwayDegrees < quickResult.EffectiveSwayDegrees
                && suppressedResult.EffectiveSwayDegrees > quickResult.EffectiveSwayDegrees;

            Console.WriteLine("aim mode changes effective sway like CE Verb_ShootCE");
            Console.WriteLine("  quick=" + quickResult.EffectiveSwayDegrees.ToString("0.###")
                + " aimed=" + aimedResult.EffectiveSwayDegrees.ToString("0.###")
                + " suppressed=" + suppressedResult.EffectiveSwayDegrees.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckTargetModeAimHeight()
        {
            HitChanceInputs torso = BaselineNoDarkness();
            torso.TargetHeightMeters = 1.75f;
            torso.ShootingAccuracy = 3f;
            torso.TargetMode = HitChanceTargetMode.Torso;

            HitChanceInputs head = Clone(torso);
            head.TargetMode = HitChanceTargetMode.Head;

            HitChanceInputs legs = Clone(torso);
            legs.TargetMode = HitChanceTargetMode.Legs;

            HitChanceInputs suppressedHead = Clone(head);
            suppressedHead.AimMode = HitChanceAimMode.SuppressFire;

            HitChanceResult torsoResult = HitChanceCalculator.Calculate(torso);
            HitChanceResult headResult = HitChanceCalculator.Calculate(head);
            HitChanceResult legsResult = HitChanceCalculator.Calculate(legs);
            HitChanceResult suppressedResult = HitChanceCalculator.Calculate(suppressedHead);
            bool passed = headResult.TargetAimHeightCells > torsoResult.TargetAimHeightCells
                && torsoResult.TargetAimHeightCells > legsResult.TargetAimHeightCells
                && Near(suppressedResult.TargetAimHeightCells, torsoResult.TargetAimHeightCells);

            Console.WriteLine("target mode changes CE-style aim height");
            Console.WriteLine("  legs=" + legsResult.TargetAimHeightCells.ToString("0.###")
                + " torso=" + torsoResult.TargetAimHeightCells.ToString("0.###")
                + " head=" + headResult.TargetAimHeightCells.ToString("0.###")
                + " suppressed-head=" + suppressedResult.TargetAimHeightCells.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckTargetCoverAdjustsAimHeights()
        {
            HitChanceInputs input = BaselineNoDarkness();
            input.ShotHeightCells = 0.45f;
            input.ShooterMaxHeightCells = 1f;
            input.TargetHeightMeters = 1.75f;
            input.TargetMode = HitChanceTargetMode.Torso;
            input.ShooterThingId = 123;

            var obstacles = new LineOfFireObstacleContext
            {
                ShooterThingId = 999,
                TargetDistanceCells = input.DistanceCells + 20f,
                DistanceToleranceCells = 0.05f
            };
            obstacles.Obstacles.Add(new LineOfFireObstacle
            {
                DistanceCells = input.DistanceCells - 2f,
                HalfDepthCells = 0.5f,
                CenterOffsetCells = 0f,
                HalfWidthCells = 0.5f,
                MinHeightCells = 0f,
                MaxHeightCells = 0.8f,
                Label = "test cover"
            });

            HitChanceResult result = HitChanceCalculator.Calculate(input, obstacles);
            HitChanceInputs shorterDistance = Clone(input);
            shorterDistance.DistanceCells = 5f;
            HitChanceResult shorterResult = HitChanceCalculator.Calculate(shorterDistance, obstacles);
            bool passed = Near(result.TargetCoverHeightCells, 0.8f)
                && Near(result.TargetAimHeightCells, 0.9f)
                && Near(result.AdjustedShotHeightCells, 0.9f)
                && Near(shorterResult.TargetCoverHeightCells, 0.8f)
                && Near(shorterResult.TargetAimHeightCells, 0.9f)
                && Near(shorterResult.AdjustedShotHeightCells, 0.9f);

            Console.WriteLine("target cover is calculator data, not bound to source shooter or distance");
            Console.WriteLine("  cover=" + result.TargetCoverHeightCells.ToString("0.###")
                + " targetAim=" + result.TargetAimHeightCells.ToString("0.###")
                + " shotHeight=" + result.AdjustedShotHeightCells.ToString("0.###")
                + " shorterCover=" + shorterResult.TargetCoverHeightCells.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckGravityFactorChangesBallistics()
        {
            HitChanceInputs normal = BaselineRangeSensitive();
            normal.GravityFactor = 1f;
            HitChanceInputs highGravity = Clone(normal);
            highGravity.GravityFactor = 2f;

            HitChanceResult normalResult = HitChanceCalculator.Calculate(normal);
            HitChanceResult highGravityResult = HitChanceCalculator.Calculate(highGravity);
            bool passed = Near(normalResult.GravityPerWidth, HitChanceCalculator.GravityConst)
                && Near(highGravityResult.GravityPerWidth, HitChanceCalculator.GravityConst * 2f)
                && highGravityResult.RangeVerticalErrorCells > normalResult.RangeVerticalErrorCells;

            Console.WriteLine("projectile gravity factor changes ballistic vertical error");
            Console.WriteLine("  normalGravity=" + normalResult.GravityPerWidth.ToString("0.###")
                + " vertical=" + normalResult.RangeVerticalErrorCells.ToString("0.####")
                + " highGravity=" + highGravityResult.GravityPerWidth.ToString("0.###")
                + " vertical=" + highGravityResult.RangeVerticalErrorCells.ToString("0.####"));
            Mark(passed);
        }

        private static void CheckInstantProjectileUsesRayApproximation()
        {
            HitChanceInputs input = BaselineRangeSensitive();
            input.GravityFactor = 3f;
            input.TargetMoveSpeedCellsPerSecond = 6f;
            input.TargetMoveDirectionDegrees = 90f;
            input.InstantProjectile = true;

            HitChanceResult result = HitChanceCalculator.Calculate(input);
            bool passed = Near(result.GravityPerWidth, 0f)
                && Near(result.LeadErrorCells, 0f)
                && result.InstantProjectile;

            Console.WriteLine("instant projectile uses straight ray approximation");
            Console.WriteLine("  gravity=" + result.GravityPerWidth.ToString("0.###")
                + " lead=" + result.LeadErrorCells.ToString("0.###")
                + " verticalErr=" + result.RangeVerticalErrorCells.ToString("0.####"));
            Mark(passed);
        }

        private static void CheckInstantDamageFalloffIgnoresMechanicalSpread()
        {
            HitChanceInputs lowSpread = SpreadSensitiveBaseline();
            lowSpread.InstantProjectile = true;
            lowSpread.InstantProjectileIgnoresMechanicalSpread = true;
            lowSpread.SpreadDegrees = 0.05f;

            HitChanceInputs highSpread = Clone(lowSpread);
            highSpread.SpreadDegrees = 5f;

            HitChanceResult lowResult = HitChanceCalculator.Calculate(lowSpread);
            HitChanceResult highResult = HitChanceCalculator.Calculate(highSpread);
            bool passed = Near(lowResult.EffectiveSpreadDegrees, 0f)
                && Near(highResult.EffectiveSpreadDegrees, 0f)
                && Near(lowResult.MonteCarloSingle, highResult.MonteCarloSingle);

            Console.WriteLine("instant damage falloff mode ignores mechanical spread as hit spread");
            Console.WriteLine("  lowSpread=" + Percent(lowResult.MonteCarloSingle)
                + " highSpread=" + Percent(highResult.MonteCarloSingle)
                + " effectiveSpread=" + highResult.EffectiveSpreadDegrees.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckAxisDiagnostics()
        {
            HitChanceResult result = HitChanceCalculator.Calculate(Baseline());
            bool passed = result.DominantAxis == HitChanceLimitAxis.Horizontal && result.VerticalSaturated;

            Console.WriteLine("axis diagnostics identify saturated vertical / horizontal-limited cases");
            Console.WriteLine("  dominant=" + result.DominantAxis
                + " horizontal=" + Percent(result.MonteCarloHorizontalFirst)
                + " vertical=" + Percent(result.MonteCarloVerticalFirst)
                + " verticalSaturated=" + result.VerticalSaturated);
            Mark(passed);
        }

        private static void CheckShooterThingIdChangesSwayPhase()
        {
            HitChanceInputs first = RpmBurstBaseline();
            first.SwayStartTick = 1000;
            first.ShooterThingId = 0;

            HitChanceInputs second = Clone(first);
            second.ShooterThingId = 72;

            HitChanceResult firstResult = HitChanceCalculator.Calculate(first);
            HitChanceResult secondResult = HitChanceCalculator.Calculate(second);
            bool passed = Math.Abs(firstResult.MonteCarloSingle - secondResult.MonteCarloSingle) > 0.001f;

            Console.WriteLine("shooter thingIDNumber changes deterministic sway phase");
            Console.WriteLine("  id0 first=" + Percent(firstResult.MonteCarloSingle)
                + " id72 first=" + Percent(secondResult.MonteCarloSingle));
            Mark(passed);
        }

        private static void CheckRpmToWholeTicks()
        {
            bool passed = HitChanceCalculator.CalculateTicksBetweenShots(600f) == 6
                && HitChanceCalculator.CalculateTicksBetweenShots(1200f) == 3
                && HitChanceCalculator.CalculateTicksBetweenShots(0f) == 6;

            Console.WriteLine("RPM input converts to whole burst-shot ticks");
            Console.WriteLine("  600rpm=" + HitChanceCalculator.CalculateTicksBetweenShots(600f)
                + " 1200rpm=" + HitChanceCalculator.CalculateTicksBetweenShots(1200f)
                + " 0rpm fallback=" + HitChanceCalculator.CalculateTicksBetweenShots(0f));
            Mark(passed);
        }

        private static void CheckLightingCurvePoints()
        {
            bool passed =
                Near(HitChanceCalculator.LightingRangeMultiplier(5f), 0.05f)
                && Near(HitChanceCalculator.LightingRangeMultiplier(10f), 0.15f)
                && Near(HitChanceCalculator.LightingRangeMultiplier(22f), 0.475f)
                && Near(HitChanceCalculator.LightingRangeMultiplier(35f), 1f)
                && Near(HitChanceCalculator.LightingRangeMultiplier(60f), 1.2f)
                && Near(HitChanceCalculator.LightingRangeMultiplier(90f), 2f);

            Console.WriteLine("lighting range multiplier matches CE curve points");
            Console.WriteLine("  5=" + HitChanceCalculator.LightingRangeMultiplier(5f).ToString("0.###")
                + " 10=" + HitChanceCalculator.LightingRangeMultiplier(10f).ToString("0.###")
                + " 22=" + HitChanceCalculator.LightingRangeMultiplier(22f).ToString("0.###")
                + " 35=" + HitChanceCalculator.LightingRangeMultiplier(35f).ToString("0.###")
                + " 60=" + HitChanceCalculator.LightingRangeMultiplier(60f).ToString("0.###")
                + " 90=" + HitChanceCalculator.LightingRangeMultiplier(90f).ToString("0.###"));
            Mark(passed);
        }

        private static void CheckDistanceCurveUsesCappedSamples()
        {
            HitChanceInputs input = BaselineNoDarkness();
            input.MaxRangeCells = 35f;
            input.Samples = 10000;
            input.AnalysisSamples = 3000;
            HitChanceAnalysisPoint[] points = HitChanceCalculator.BuildDistanceCurve(input);
            bool passed = points.Length == 3
                && points[0].X == 10f
                && points[2].X == 30f
                && HitChanceCalculator.CalculateAnalysisSamples(input) == 3000;

            Console.WriteLine("distance curve uses fixed points under max range and capped samples");
            Console.WriteLine("  points=" + points.Length
                + " first=" + points[0].Label
                + " last=" + points[points.Length - 1].Label
                + " samples=" + HitChanceCalculator.CalculateAnalysisSamples(input));
            Mark(passed);
        }

        private static void CheckDpsUsesDirectHitChance()
        {
            HitChanceInputs input = BurstBaseline();
            input.Rpm = 600f;
            input.SustainedShotsPerSecond = 10f;
            input.MagazineShots = 0;
            input.FireWarmupSeconds = 0f;
            input.FireCooldownSeconds = 0f;
            input.ReloadSeconds = 0f;
            HitChanceResult result = HitChanceCalculator.Calculate(input);
            var profile = new DamageProfile
            {
                DirectDamagePerShot = 10f,
                DirectLabel = "test"
            };
            profile.RangeLines.Add(new DamageLine
            {
                Label = "blast",
                DamagePerShot = 5f,
                UsesDirectHitChance = true
            });
            profile.FragmentLines.Add(new DamageLine
            {
                Label = "fragment",
                DamagePerShot = 5f,
                UsesDirectHitChance = false
            });

            DpsResult dps = DpsCalculator.Calculate(profile, input, result);
            bool passed = dps.DirectPaperDps > dps.DirectExpectedDps
                && Near(dps.DirectPaperDps, 100f)
                && dps.RangeExpectedDps < 50f
                && Near(dps.TotalExpectedDps, dps.DirectExpectedDps + dps.RangeExpectedDps)
                && Near(dps.TotalExpectedDamagePerBurst, dps.DirectExpectedDamagePerBurst + dps.RangeExpectedDamagePerBurst)
                && dps.TotalExpectedDpsMagazine > dps.DirectExpectedDpsMagazine
                && dps.TotalExpectedDps60s > dps.DirectExpectedDps60s
                && dps.TotalExpectedDamagePerMagazine > dps.DirectExpectedDamagePerMagazine
                && Near(dps.FragmentPaperDps, 50f)
                && dps.DirectExpectedDamagePerBurst > 10f
                && dps.DirectExpectedDpsMagazine > 0f
                && dps.DirectExpectedDps60s > 0f
                && dps.DirectExpectedDamagePerMagazine > 0f
                && dps.MagazineFireSeconds > 0f
                && dps.NearMissDirectDps >= 0f
                && dps.NearMissAverageChance >= 0f;

            Console.WriteLine("DPS model applies hit chance to direct/range and keeps fragments paper-only");
            Console.WriteLine("  paperDirectDps=" + dps.DirectPaperDps.ToString("0.###")
                + " expectedDirectDps=" + dps.DirectExpectedDps.ToString("0.###")
                + " rangeExpectedDps=" + dps.RangeExpectedDps.ToString("0.###")
                + " totalExpectedDps=" + dps.TotalExpectedDps.ToString("0.###")
                + " fragmentPaperDps=" + dps.FragmentPaperDps.ToString("0.###")
                + " burstDirect=" + dps.DirectExpectedDamagePerBurst.ToString("0.###")
                + " nearMissDps=" + dps.NearMissDirectDps.ToString("0.###")
                + " magDps=" + dps.DirectExpectedDpsMagazine.ToString("0.###")
                + " dps60=" + dps.DirectExpectedDps60s.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckLaserDamageFalloffReducesDirectDamage()
        {
            HitChanceInputs input = BaselineNoDarkness();
            input.DistanceCells = 40f;
            input.SpreadDegrees = 1.2f;
            input.SustainedShotsPerSecond = 1f;
            input.MagazineShots = 0;
            input.TargetArmorSharp = 0f;
            input.TargetArmorBlunt = 0f;
            var hitResult = HitChanceCalculator.Calculate(input);
            var profile = new DamageProfile
            {
                DirectDamagePerShot = 100f,
                DirectLabel = "laser"
            };
            profile.DirectLines.Add(new DamageLine
            {
                Label = "laser",
                DamagePerShot = 100f,
                ArmorKind = ArmorDamageKind.Sharp,
                ArmorPenetrationSharp = 100f,
                ArmorPenetrationBlunt = 10f,
                UsesLaserDamageFalloff = true
            });

            DpsResult dps = DpsCalculator.Calculate(profile, input, hitResult);
            ArmorDamageResult armor = DpsCalculator.CalculatePrimaryArmorResult(profile, input);
            float expectedMultiplier = DpsCalculator.CalculateLaserDamageFalloffMultiplier(input.DistanceCells, input.SpreadDegrees);
            bool passed = dps.HasLaserDamageFalloff
                && expectedMultiplier < 1f
                && Near(dps.LaserDamageFalloffMultiplier, expectedMultiplier)
                && Near(dps.DirectEffectiveDamagePerShot, 100f * expectedMultiplier)
                && Near(armor.PaperDamage, 100f * expectedMultiplier)
                && armor.PostArmorDamage < 100f;

            Console.WriteLine("laser damage falloff reduces direct and post-armor damage");
            Console.WriteLine("  multiplier=" + Percent(dps.LaserDamageFalloffMultiplier)
                + " effective=" + dps.DirectEffectiveDamagePerShot.ToString("0.###")
                + " armorPaper=" + armor.PaperDamage.ToString("0.###")
                + " postArmor=" + armor.PostArmorDamage.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckAimModeTimingAffectsSustainedDps()
        {
            HitChanceInputs input = BurstBaseline();
            input.DistanceCells = 100f;
            input.MagazineShots = 10;
            input.BurstShots = 1;
            input.Rpm = 600f;
            input.FireWarmupSeconds = 1f;
            input.FireCooldownSeconds = 1f;
            input.ReloadSeconds = 5f;

            input.AimMode = HitChanceAimMode.AimedShot;
            int aimedTicks;
            float aimed = DpsCalculator.CalculateShotsPerSecond(input, out aimedTicks);

            input.AimMode = HitChanceAimMode.Snapshot;
            int snapshotTicks;
            float snapshot = DpsCalculator.CalculateShotsPerSecond(input, out snapshotTicks);

            input.AimMode = HitChanceAimMode.SuppressFire;
            int suppressTicks;
            float suppress = DpsCalculator.CalculateShotsPerSecond(input, out suppressTicks);

            bool passed = aimedTicks == 240
                && snapshotTicks == 0
                && suppressTicks == 0
                && aimed < snapshot
                && Near(snapshot, suppress);

            Console.WriteLine("aimed shot adds CE second aim cycle to sustained DPS");
            Console.WriteLine("  aimedTicks=" + aimedTicks
                + " aimed/s=" + aimed.ToString("0.###")
                + " snapshot/s=" + snapshot.ToString("0.###")
                + " suppress/s=" + suppress.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckFasterRepeatShotsShortensRepeatWarmup()
        {
            HitChanceInputs normal = BurstBaseline();
            normal.DistanceCells = 50f;
            normal.MagazineShots = 10;
            normal.BurstShots = 1;
            normal.Rpm = 600f;
            normal.FireWarmupSeconds = 1f;
            normal.FireCooldownSeconds = 1f;
            normal.ReloadSeconds = 5f;
            normal.AimMode = HitChanceAimMode.AimedShot;
            normal.RecoilAmount = 1.49f;

            HitChanceInputs faster = Clone(normal);
            faster.FasterRepeatShots = true;

            int normalTicks;
            float normalReduction;
            float normalRate = DpsCalculator.CalculateShotsPerSecond(normal, out normalTicks, out normalReduction);
            int fasterTicks;
            float fasterReduction;
            float fasterRate = DpsCalculator.CalculateShotsPerSecond(faster, out fasterTicks, out fasterReduction);

            bool passed = normalTicks == fasterTicks
                && Near(normalReduction, 1f)
                && Near(fasterReduction, 0.25f)
                && fasterRate > normalRate;

            Console.WriteLine("FasterRepeatShots shortens repeated same-direction warmups");
            Console.WriteLine("  normal/s=" + normalRate.ToString("0.###")
                + " faster/s=" + fasterRate.ToString("0.###")
                + " reduction=" + fasterReduction.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckShooterTimingStatsAffectDps()
        {
            HitChanceInputs slow = BurstBaseline();
            slow.MagazineShots = 10;
            slow.BurstShots = 1;
            slow.Rpm = 600f;
            slow.FireWarmupSeconds = 1f;
            slow.FireCooldownSeconds = 0.5f;
            slow.ReloadSeconds = 5f;
            slow.AimMode = HitChanceAimMode.Snapshot;
            slow.AimingDelayFactor = 1.5f;
            slow.ReloadSpeed = 0.5f;
            slow.ReloadFactor = 1.2f;

            HitChanceInputs fast = Clone(slow);
            fast.AimingDelayFactor = 0.5f;
            fast.ReloadSpeed = 2f;

            int slowTicks;
            float slowRate = DpsCalculator.CalculateShotsPerSecond(slow, out slowTicks);
            int fastTicks;
            float fastRate = DpsCalculator.CalculateShotsPerSecond(fast, out fastTicks);
            bool passed = DpsCalculator.EffectiveWarmupSeconds(fast) < DpsCalculator.EffectiveWarmupSeconds(slow)
                && DpsCalculator.EffectiveReloadSeconds(fast) < DpsCalculator.EffectiveReloadSeconds(slow)
                && fastRate > slowRate
                && slowTicks == fastTicks;

            Console.WriteLine("shooter aiming delay and reload speed affect sustained DPS");
            Console.WriteLine("  slowWarmup=" + DpsCalculator.EffectiveWarmupSeconds(slow).ToString("0.###")
                + " slowReload=" + DpsCalculator.EffectiveReloadSeconds(slow).ToString("0.###")
                + " slow/s=" + slowRate.ToString("0.###")
                + " fastWarmup=" + DpsCalculator.EffectiveWarmupSeconds(fast).ToString("0.###")
                + " fastReload=" + DpsCalculator.EffectiveReloadSeconds(fast).ToString("0.###")
                + " fast/s=" + fastRate.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckTimelineMatchesMagazineFireTime()
        {
            HitChanceInputs input = BurstBaseline();
            input.DistanceCells = 50f;
            input.MagazineShots = 7;
            input.BurstShots = 3;
            input.Rpm = 600f;
            input.FireWarmupSeconds = 1f;
            input.FireCooldownSeconds = 0.5f;
            input.AimMode = HitChanceAimMode.AimedShot;
            input.FasterRepeatShots = true;
            input.RecoilAmount = 1.49f;

            int aimedTicks;
            float reduction;
            DpsCalculator.CalculateShotsPerSecond(input, out aimedTicks, out reduction);
            float fireTime = DpsCalculator.CalculateMagazineFireTimeSeconds(input, aimedTicks, reduction);
            FireTimeline timeline = DpsCalculator.BuildFireTimeline(input);
            bool passed = timeline.MagazineShots == 7
                && timeline.Bursts.Count == 3
                && timeline.Bursts[0].ShotCount == 3
                && timeline.Bursts[2].ShotCount == 1
                && timeline.AimedExtraTicks == aimedTicks
                && Near(timeline.RepeatWarmupReduction, reduction)
                && Near(timeline.TotalSeconds, fireTime)
                && timeline.Bursts[0].FirstAimEndSeconds < timeline.Bursts[0].SecondAimEndSeconds
                && timeline.Bursts[1].WarmupMultiplier < timeline.Bursts[0].WarmupMultiplier;

            Console.WriteLine("timeline matches magazine fire timing and exposes aim phases");
            Console.WriteLine("  bursts=" + timeline.Bursts.Count
                + " total=" + timeline.TotalSeconds.ToString("0.###")
                + " fireTime=" + fireTime.ToString("0.###")
                + " aimedTicks=" + timeline.AimedExtraTicks
                + " repeat=" + timeline.RepeatWarmupReduction.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckTimelineWindowIncludesReloads()
        {
            HitChanceInputs input = BurstBaseline();
            input.MagazineShots = 5;
            input.BurstShots = 5;
            input.Rpm = 600f;
            input.FireWarmupSeconds = 1f;
            input.FireCooldownSeconds = 0.5f;
            input.ReloadSeconds = 3f;
            input.AimMode = HitChanceAimMode.Snapshot;

            FireTimeline timeline = DpsCalculator.BuildFireTimeline(input, 60f);
            bool passed = timeline.IsWindow
                && Near(timeline.TotalSeconds, 60f)
                && timeline.Reloads.Count > 0
                && timeline.Bursts.Count > 1
                && timeline.TotalShotsFired > input.MagazineShots
                && timeline.Reloads[0].StartSeconds >= timeline.Bursts[0].CooldownEndSeconds;

            Console.WriteLine("timeline 60s window includes reloads and repeated magazines");
            Console.WriteLine("  bursts=" + timeline.Bursts.Count
                + " reloads=" + timeline.Reloads.Count
                + " shots=" + timeline.TotalShotsFired
                + " total=" + timeline.TotalSeconds.ToString("0.###"));
            Mark(passed);
        }

        private static void CheckBallisticDistributionSupportsLongBursts()
        {
            HitChanceInputs input = BurstBaseline();
            input.BurstShots = 40;
            input.AnalysisSamples = 200;
            input.Samples = 1000;
            BallisticDistribution distribution = HitChanceCalculator.BuildBallisticDistribution(input);
            bool passed = distribution.BurstShots == 40
                && distribution.SampleCount == 200
                && distribution.Points.Count == 8000
                && distribution.HorizontalExtentCells > 0f
                && distribution.VerticalMaxCells > distribution.VerticalMinCells;

            Console.WriteLine("ballistic distribution supports long bursts");
            Console.WriteLine("  burst=" + distribution.BurstShots
                + " samples=" + distribution.SampleCount
                + " points=" + distribution.Points.Count
                + " horizontalExtent=" + distribution.HorizontalExtentCells.ToString("0.###")
                + " vertical=" + distribution.VerticalMinCells.ToString("0.###") + "->" + distribution.VerticalMaxCells.ToString("0.###"));
            Mark(passed);
        }

        private static bool Near(float actual, float expected)
        {
            return Math.Abs(actual - expected) < 0.0001f;
        }

        private static void Run(string name, HitChanceResult result, bool passed = true)
        {
            Console.WriteLine(name);
            Console.WriteLine("  first=" + Percent(result.MonteCarloSingle)
                + ", burstAny=" + Percent(result.MonteCarloBurstAny)
                + ", ceFormula=" + Percent(result.CeEstimatedSingle)
                + ", horizontal=" + Percent(result.MonteCarloHorizontalFirst)
                + ", vertical=" + Percent(result.MonteCarloVerticalFirst));
            Mark(passed);
        }

        private static void PrintTrend(string name, HitChanceResult beforeResult, HitChanceResult afterResult, float beforeValue, float afterValue, bool passed)
        {
            Console.WriteLine(name);
            Console.WriteLine("  before=" + Percent(beforeValue)
                + "  after=" + Percent(afterValue)
                + "  rangeErr=" + beforeResult.RangeErrorCells.ToString("0.###") + "->" + afterResult.RangeErrorCells.ToString("0.###")
                + "  verticalErr=" + beforeResult.RangeVerticalErrorCells.ToString("0.####") + "->" + afterResult.RangeVerticalErrorCells.ToString("0.####"));
            Mark(passed);
        }

        private static void Mark(bool passed)
        {
            if (passed)
            {
                Console.WriteLine("  OK");
            }
            else
            {
                failures++;
                Console.WriteLine("  FAILED");
            }
            Console.WriteLine();
        }

        private static void CheckArmorDamageFormula()
        {
            Console.WriteLine("CE armor formula approximates sharp penetration and deflection");
            float noArmor = DpsCalculator.CalculatePostArmorDamage(80f, ArmorDamageKind.Sharp, 70f, 3849.48f, 0f, 0f);
            float mediumArmor = DpsCalculator.CalculatePostArmorDamage(80f, ArmorDamageKind.Sharp, 70f, 3849.48f, 40f, 0f);
            float deflected = DpsCalculator.CalculatePostArmorDamage(80f, ArmorDamageKind.Sharp, 70f, 3849.48f, 70f, 0f);
            float bluntArmor = DpsCalculator.CalculatePostArmorDamage(80f, ArmorDamageKind.Sharp, 70f, 3849.48f, 70f, 1000f);
            bool passed = Near(noArmor, 80f)
                && Math.Abs(mediumArmor - 57.54f) < 0.08f
                && Math.Abs(deflected - 33.77f) < 0.08f
                && bluntArmor < deflected;
            Console.WriteLine("  noArmor=" + noArmor.ToString("0.###")
                + " 40RHA=" + mediumArmor.ToString("0.###")
                + " 70RHA=" + deflected.ToString("0.###")
                + " 70RHA+1000MPa=" + bluntArmor.ToString("0.###"));
            if (!passed)
            {
                failures++;
                Console.WriteLine("  FAILED");
            }
            else
            {
                Console.WriteLine("  OK");
            }
            Console.WriteLine();
        }

        private static void CheckExplosiveArmorPenetrationRules()
        {
            Console.WriteLine("CE explosive armor penetration uses distinct secondary/range rules");
            float secondaryExplosion = DpsCalculator.CalculatePostArmorDamage(88f, ArmorDamageKind.Blunt, 0f, 88f * 0.8f, 0f, 50f);
            float rangeExplosion = DpsCalculator.CalculatePostArmorDamage(88f, ArmorDamageKind.Blunt, 0f, 88f * 0.3f, 0f, 50f);
            float explicitRangeExplosion = DpsCalculator.CalculatePostArmorDamage(88f, ArmorDamageKind.Blunt, 0f, Math.Max(88f * 0.3f, 40f), 0f, 50f);
            bool passed = secondaryExplosion > 20f
                && Near(rangeExplosion, 0f)
                && Near(explicitRangeExplosion, 0f);

            Console.WriteLine("  secondaryAP=" + (88f * 0.8f).ToString("0.###")
                + " secondaryDamage=" + secondaryExplosion.ToString("0.###")
                + " rangeAP=" + (88f * 0.3f).ToString("0.###")
                + " rangeDamage=" + rangeExplosion.ToString("0.###")
                + " explicit40Damage=" + explicitRangeExplosion.ToString("0.###"));
            if (!passed)
            {
                failures++;
                Console.WriteLine("  FAILED");
            }
            else
            {
                Console.WriteLine("  OK");
            }
            Console.WriteLine();
        }

        private static void CheckAttachedExplosiveDamageCountsAsDirectArmorDamage()
        {
            Console.WriteLine("attached explosive damage is direct post-armor damage, fragments are not");
            var profile = new DamageProfile
            {
                DirectDamagePerShot = 13f,
                DirectLabel = "AP-HE"
            };
            profile.DirectLines.Add(new DamageLine
            {
                Label = "bullet",
                DamagePerShot = 10f,
                ArmorKind = ArmorDamageKind.Sharp,
                ArmorPenetrationSharp = 20f,
                ArmorPenetrationBlunt = 10f,
                ArmorDamagePerEvent = 10f,
                ArmorEventMultiplier = 1f
            });
            profile.DirectLines.Add(new DamageLine
            {
                Label = "attached explosion",
                DamagePerShot = 3f,
                ArmorKind = ArmorDamageKind.Blunt,
                ArmorPenetrationBlunt = 2.4f,
                ArmorDamagePerEvent = 3f,
                ArmorEventMultiplier = 1f
            });
            profile.FragmentLines.Add(new DamageLine
            {
                Label = "fragment",
                DamagePerShot = 100f
            });

            var input = new HitChanceInputs();
            ArmorDamageResult armor = DpsCalculator.CalculatePrimaryArmorResult(profile, input);
            bool passed = Near(armor.PaperDamage, 13f)
                && Near(armor.PostArmorDamage, 13f)
                && armor.Kind == ArmorDamageKind.Composite
                && armor.Contributions.Count == 2
                && armor.Contributions[1].Label == "attached explosion";

            Console.WriteLine("  paperDirect=" + armor.PaperDamage.ToString("0.###")
                + " postArmorDirect=" + armor.PostArmorDamage.ToString("0.###")
                + " fragmentPaperIgnored=100");
            if (!passed)
            {
                failures++;
                Console.WriteLine("  FAILED");
            }
            else
            {
                Console.WriteLine("  OK");
            }
            Console.WriteLine();
        }

        private static void CheckHeatAndElectricArmorFormula()
        {
            Console.WriteLine("heat/electric armor categories use their own armor ratings");
            float heatReduced = DpsCalculator.CalculatePostArmorDamage(20f, ArmorDamageKind.Heat, 0f, 0.5f, 0f, 0f, 0.25f, 0f);
            float electricBlocked = DpsCalculator.CalculatePostArmorDamage(20f, ArmorDamageKind.Electric, 0f, 0.5f, 0f, 0f, 0f, 1.0f);
            float zeroPenElectric = DpsCalculator.CalculatePostArmorDamage(20f, ArmorDamageKind.Electric, 0f, 0f, 0f, 0f, 0f, 1.0f);
            float ambientBurnReduced = DpsCalculator.CalculatePostArmorDamage(20f, ArmorDamageKind.Heat, 0f, 0f, 0f, 0f, 0.25f, 0f, true);
            float ambientElectricReduced = DpsCalculator.CalculatePostArmorDamage(20f, ArmorDamageKind.Electric, 0f, 0.5f, 0f, 0f, 0f, 1.0f, true);
            bool passed = Near(heatReduced, 10f)
                && Near(electricBlocked, 0f)
                && Near(zeroPenElectric, 20f)
                && Near(ambientBurnReduced, 15f)
                && Near(ambientElectricReduced, 10f);

            Console.WriteLine("  heatArmor0.25=" + heatReduced.ToString("0.###")
                + " electricArmor1=" + electricBlocked.ToString("0.###")
                + " zeroPenElectric=" + zeroPenElectric.ToString("0.###")
                + " ambientBurn0.25=" + ambientBurnReduced.ToString("0.###")
                + " ambientElectric1=" + ambientElectricReduced.ToString("0.###"));
            if (!passed)
            {
                failures++;
                Console.WriteLine("  FAILED");
            }
            else
            {
                Console.WriteLine("  OK");
            }
            Console.WriteLine();
        }

        private static HitChanceInputs Clone(HitChanceInputs source)
        {
            return new HitChanceInputs
            {
                DistanceCells = source.DistanceCells,
                MaxRangeCells = source.MaxRangeCells,
                ShotSpeedCellsPerSecond = source.ShotSpeedCellsPerSecond,
                ShotHeightCells = source.ShotHeightCells,
                ShooterMaxHeightCells = source.ShooterMaxHeightCells,
                GravityFactor = source.GravityFactor,
                InstantProjectile = source.InstantProjectile,
                InstantProjectileIgnoresMechanicalSpread = source.InstantProjectileIgnoresMechanicalSpread,
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
                ShooterThingId = source.ShooterThingId,
                SwayStartTick = source.SwayStartTick,
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
                Samples = source.Samples,
                Seed = source.Seed
            };
        }

        private static string Percent(float value)
        {
            return (value * 100f).ToString("0.##") + "%";
        }
    }
}
