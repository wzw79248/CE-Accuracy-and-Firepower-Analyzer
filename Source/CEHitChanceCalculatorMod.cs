using Verse;
using UnityEngine;

namespace CEHitChanceCalculator
{
    public sealed class CEHitChanceCalculatorMod : Mod
    {
        public static CEHitChanceCalculatorSettings Settings;

        public CEHitChanceCalculatorMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CEHitChanceCalculatorSettings>();
        }

        public override string SettingsCategory()
        {
            return "CEHCC_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Rect buttonRect = new Rect(inRect.x, inRect.y, 220f, 34f);
            if (Widgets.ButtonText(buttonRect, "CEHCC_OpenCalculator".Translate()))
            {
                Find.WindowStack.Add(new Dialog_CEHitChanceCalculator(Settings.Inputs));
            }

            Rect textRect = new Rect(inRect.x, buttonRect.yMax + 12f, inRect.width, 80f);
            Widgets.Label(textRect, "CEHCC_SettingsDescription".Translate());
        }
    }

    public sealed class CEHitChanceCalculatorSettings : ModSettings
    {
        // 设置只保存轻量输入参数，不保存地图对象、Thing 引用或分析结果。
        // 因此这个 MOD 可以在存档中自由加入/移除，不会留下需要清理的存档对象。
        private const int CurrentDefaultsVersion = 2;

        public HitChanceInputs Inputs = new HitChanceInputs();
        public bool PendingDefaultRestore;
        private int defaultsVersion = CurrentDefaultsVersion;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref defaultsVersion, "defaultsVersion", 0);
            Scribe_Values.Look(ref Inputs.DistanceCells, "distanceCells", 30f);
            Scribe_Values.Look(ref Inputs.MaxRangeCells, "maxRangeCells", 55f);
            Scribe_Values.Look(ref Inputs.ShotSpeedCellsPerSecond, "shotSpeedCellsPerSecond", 168f);
            Scribe_Values.Look(ref Inputs.ShotHeightCells, "shotHeightCells", 0.85f);
            Scribe_Values.Look(ref Inputs.GravityFactor, "gravityFactor", 1f);
            Scribe_Values.Look(ref Inputs.TargetHeightMeters, "targetHeightMeters", 1.75f);
            Scribe_Values.Look(ref Inputs.TargetWidthMeters, "targetWidthMeters", 0.88f);
            Scribe_Values.Look(ref Inputs.TargetArmorSharp, "targetArmorSharp", 0f);
            Scribe_Values.Look(ref Inputs.TargetArmorBlunt, "targetArmorBlunt", 0f);
            Scribe_Values.Look(ref Inputs.TargetArmorHeat, "targetArmorHeat", 0f);
            Scribe_Values.Look(ref Inputs.TargetArmorElectric, "targetArmorElectric", 0f);
            Scribe_Values.Look(ref Inputs.TargetMode, "targetMode", HitChanceTargetMode.Torso);
            Scribe_Values.Look(ref Inputs.SwayDegrees, "swayDegrees", 1.995f);
            Scribe_Values.Look(ref Inputs.SpreadDegrees, "spreadDegrees", 0.07f);
            Scribe_Values.Look(ref Inputs.RecoilAmount, "recoilAmount", 1.50f);
            Scribe_Values.Look(ref Inputs.ShootingAccuracy, "shootingAccuracy", 3.0f);
            Scribe_Values.Look(ref Inputs.AimingAccuracy, "aimingAccuracy", 1.0f);
            Scribe_Values.Look(ref Inputs.SightsEfficiency, "sightsEfficiency", 1.0f);
            Scribe_Values.Look(ref Inputs.AimingDelayFactor, "aimingDelayFactor", 1.0f);
            Scribe_Values.Look(ref Inputs.ReloadSpeed, "reloadSpeed", 1.0f);
            Scribe_Values.Look(ref Inputs.ReloadFactor, "reloadFactor", 1.0f);
            Scribe_Values.Look(ref Inputs.NightVisionEfficiency, "nightVisionEfficiency", 0f);
            Scribe_Values.Look(ref Inputs.AimMode, "aimMode", HitChanceAimMode.AimedShot);
            Scribe_Values.Look(ref Inputs.ShooterSuppressed, "shooterSuppressed", false);
            Scribe_Values.Look(ref Inputs.Darkness, "darkness", 0f);
            Scribe_Values.Look(ref Inputs.WeatherError, "weatherError", 0f);
            Scribe_Values.Look(ref Inputs.SmokeDensity, "smokeDensity", 0f);
            Scribe_Values.Look(ref Inputs.TargetMoveSpeedCellsPerSecond, "targetMoveSpeedCellsPerSecond", 0f);
            Scribe_Values.Look(ref Inputs.TargetMoveDirectionDegrees, "targetMoveDirectionDegrees", 90f);
            Scribe_Values.Look(ref Inputs.ShooterThingId, "shooterThingId", 0);
            Scribe_Values.Look(ref Inputs.SwayStartTick, "swayStartTick", -1);
            Scribe_Values.Look(ref Inputs.BlindFiring, "blindFiring", false);
            Scribe_Values.Look(ref Inputs.CircularMissRadiusCells, "circularMissRadiusCells", 0f);
            Scribe_Values.Look(ref Inputs.IndirectFireShiftCells, "indirectFireShiftCells", 0f);
            Scribe_Values.Look(ref Inputs.BurstShots, "burstShots", 6);
            Scribe_Values.Look(ref Inputs.Rpm, "rpm", 900f);
            Scribe_Values.Look(ref Inputs.SustainedShotsPerSecond, "sustainedShotsPerSecond", 0f);
            Scribe_Values.Look(ref Inputs.MagazineShots, "magazineShots", 30);
            Scribe_Values.Look(ref Inputs.FireWarmupSeconds, "fireWarmupSeconds", 1.1f);
            Scribe_Values.Look(ref Inputs.FireCooldownSeconds, "fireCooldownSeconds", 0.36f);
            Scribe_Values.Look(ref Inputs.ReloadSeconds, "reloadSeconds", 4f);
            Scribe_Values.Look(ref Inputs.FasterRepeatShots, "fasterRepeatShots", false);
            Scribe_Values.Look(ref Inputs.Samples, "samples", 10000);
            Scribe_Values.Look(ref Inputs.AnalysisMode, "analysisMode", HitChanceAnalysisMode.None);
            Scribe_Values.Look(ref Inputs.AnalysisSamples, "analysisSamples", 3000);
            Scribe_Values.Look(ref Inputs.Seed, "seed", 12345);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && defaultsVersion < CurrentDefaultsVersion)
            {
                // 默认值版本用于在默认武器/参数调整后刷新旧配置，
                // 避免旧存档继续拿过期默认值导致“恢复默认”和开局默认不一致。
                Inputs = new HitChanceInputs();
                defaultsVersion = CurrentDefaultsVersion;
                PendingDefaultRestore = true;
            }
        }

        public void ConsumePendingDefaultRestore()
        {
            PendingDefaultRestore = false;
        }
    }
}
