using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace CEHitChanceCalculator
{
    // 主窗口是 RimWorld OnGUI 窗口，会在每帧重绘。
    // 重计算、蒙特卡洛和纹理生成都必须走缓存，否则打开弹道图时会持续卡顿。
    public sealed class Dialog_CEHitChanceCalculator : Window
    {
        private enum BallisticDisplayMode
        {
            ImpactHeatmap,
            TrajectoryDensity,
            TrajectoryCloud,
            SingleBurst
        }

        private sealed class ComparisonSnapshot
        {
            public HitChanceInputs Input;
            public DamageProfile DamageProfile = DamageProfile.Empty;
            public string Label;
            public HitChanceResult Result;
            public ThingWithComps Weapon;
            public CEAmmoChoice AmmoChoice;
            public float? WeaponSwayFactor;
            public QualityCategory Quality;
            public bool WeaponHasQuality;
            public bool WeaponHasBipod;
            public bool WeaponHasAttachments;
            public bool BipodDeployed;
            public List<AttachmentDef> Attachments = new List<AttachmentDef>();
            public string ProjectileWarning;
        }

        private readonly struct HeaderButton
        {
            public readonly string Label;
            public readonly Action Action;

            public HeaderButton(string label, Action action)
            {
                Label = label;
                Action = action;
            }
        }

        private static readonly Color TopObstacleFillColor = new Color(1f, 0.48f, 0.18f, 0.12f);
        private static readonly Color TopObstacleBorderColor = new Color(1f, 0.72f, 0.34f, 0.45f);
        private static readonly Color SideObstacleFillColor = new Color(1f, 0.36f, 0.18f, 0.28f);
        private static readonly Color SideObstacleBorderColor = new Color(1f, 0.68f, 0.34f, 0.9f);

        private static ThingWithComps sessionLoadedWeapon;
        private static CEAmmoChoice sessionLoadedAmmoChoice;
        private static float? sessionLoadedWeaponSwayFactor;
        private static QualityCategory sessionSelectedQuality = QualityCategory.Normal;
        private static bool sessionLoadedWeaponHasQuality;
        private static bool sessionLoadedWeaponIsTurret;
        private static bool sessionLoadedWeaponHasBipod;
        private static bool sessionLoadedWeaponHasAttachments;
        private static bool sessionSelectedBipodDeployed;
        private static List<AttachmentDef> sessionSelectedAttachments = new List<AttachmentDef>();
        private static string sessionLoadedShooterLabel;
        private static string sessionLoadedProjectileWarning;
        private static DamageProfile sessionLoadedDamageProfile = DamageProfile.Empty;
        private static ComparisonSnapshot sessionComparisonSnapshot;
        private static BallisticDisplayMode sessionBallisticDisplayMode = BallisticDisplayMode.SingleBurst;
        private static int sessionBallisticSampleIndex;

        private readonly HitChanceInputs input;
        private Vector2 scrollPosition;
        private Vector2 outputScrollPosition;
        private string distanceBuffer;
        private string maxRangeBuffer;
        private string speedBuffer;
        private string shotHeightBuffer;
        private string shooterMaxHeightBuffer;
        private string gravityFactorBuffer;
        private string targetHeightBuffer;
        private string targetWidthBuffer;
        private string targetArmorSharpBuffer;
        private string targetArmorBluntBuffer;
        private string targetArmorHeatBuffer;
        private string targetArmorElectricBuffer;
        private string swayBuffer;
        private string spreadBuffer;
        private string recoilBuffer;
        private string shootingAccuracyBuffer;
        private string aimingAccuracyBuffer;
        private string sightsEfficiencyBuffer;
        private string aimingDelayFactorBuffer;
        private string reloadSpeedBuffer;
        private string reloadFactorBuffer;
        private string nightVisionEfficiencyBuffer;
        private string darknessBuffer;
        private string weatherBuffer;
        private string smokeBuffer;
        private string targetMoveSpeedBuffer;
        private string targetMoveDirectionBuffer;
        private string circularBuffer;
        private string indirectBuffer;
        private string burstBuffer;
        private string rpmBuffer;
        private string shooterThingIdBuffer;
        private string swayStartTickBuffer;
        private string samplesBuffer;
        private string analysisSamplesBuffer;
        private string seedBuffer;
        private bool showAdvanced;
        // 结果和弹道图按输入签名缓存；不要在 DoWindowContents 的普通绘制路径里直接重跑蒙特卡洛。
        private HitChanceResult cachedResult;
        private HitChanceAnalysisPoint[] cachedDistanceCurve;
        private BallisticDistribution cachedBallisticDistribution;
        private Texture2D cachedTopBallisticTexture;
        private Texture2D cachedSideBallisticTexture;
        private string cachedTopBallisticTextureKey;
        private string cachedSideBallisticTextureKey;
        private string cachedSignature;
        private bool forceRecalculate = true;
        private BallisticDisplayMode ballisticDisplayMode = BallisticDisplayMode.SingleBurst;
        private int ballisticSampleIndex;
        private ThingWithComps loadedWeapon;
        private CEAmmoChoice loadedAmmoChoice;
        private float? loadedWeaponSwayFactor;
        private QualityCategory selectedQuality = QualityCategory.Normal;
        private bool loadedWeaponHasQuality;
        private bool loadedWeaponIsTurret;
        private bool loadedWeaponHasBipod;
        private bool loadedWeaponHasAttachments;
        private bool selectedBipodDeployed;
        private List<AttachmentDef> selectedAttachments = new List<AttachmentDef>();
        private string loadedShooterLabel;
        private string loadedProjectileWarning;
        private DamageProfile loadedDamageProfile = DamageProfile.Empty;
        private ComparisonSnapshot comparisonSnapshot;
        private List<CEBurstModeChoice> burstModes = new List<CEBurstModeChoice>();
        private int burstModeIndex = -1;
        private bool minimized;
        private Vector2 expandedWindowSize;
        private static readonly Texture2D MinimizeIcon = TexButton.Minus;
        private static readonly Texture2D RestoreIcon = TexButton.Plus;

        private const float CurrentLoadoutHeight = 72f;
        private const float MinimizedWindowWidth = 560f;
        private const float MinimizedWindowHeight = 72f;

        public override Vector2 InitialSize => new Vector2(1180f, 720f);

        public Dialog_CEHitChanceCalculator(HitChanceInputs input)
        {
            this.input = input;
            RestoreSessionState();
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            draggable = true;
            doCloseX = true;
            InitBuffers();
            bool pendingDefaultRestore = CEHitChanceCalculatorMod.Settings?.PendingDefaultRestore == true;
            if (pendingDefaultRestore || ShouldLoadDefaultAssaultRifleOnOpen())
            {
                RestoreDefaults();
                if (pendingDefaultRestore)
                {
                    CEHitChanceCalculatorMod.Settings.ConsumePendingDefaultRestore();
                }
            }
        }

        public override void PostClose()
        {
            StoreSessionState();
            DestroyCachedBallisticTextures();
            base.PostClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawTitleBar(inRect);
            if (minimized)
            {
                return;
            }

            float buttonsHeight = HeaderButtonsHeight(inRect.width);
            Rect topButtons = new Rect(inRect.x, inRect.y + 38f, inRect.width, buttonsHeight);
            DrawHeaderButtons(topButtons);

            EnsureCalculated();
            HitChanceResult result = cachedResult;
            Rect bodyRect = new Rect(inRect.x, topButtons.yMax + 8f, inRect.width, inRect.height - topButtons.yMax - 8f);
            float gap = 12f;
            float rightWidth = Mathf.Clamp(bodyRect.width * 0.38f, 390f, 500f);
            float leftWidth = bodyRect.width - rightWidth - gap;
            if (leftWidth < 430f)
            {
                leftWidth = bodyRect.width * 0.55f;
                rightWidth = bodyRect.width - leftWidth - gap;
            }

            Rect outputOutRect = new Rect(bodyRect.x, bodyRect.y, leftWidth, bodyRect.height);
            float outputHeight = GetOutputViewHeight(result, outputOutRect.width - 16f);
            Rect outputViewRect = new Rect(0f, 0f, outputOutRect.width - 16f, outputHeight);
            Widgets.BeginScrollView(outputOutRect, ref outputScrollPosition, outputViewRect);
            float outputY = 0f;
            Rect currentLoadoutRect = new Rect(0f, outputY, outputViewRect.width, CurrentLoadoutHeight);
            DrawCurrentLoadout(currentLoadoutRect);
            outputY = currentLoadoutRect.yMax + 8f;
            Rect coreRect = new Rect(0f, outputY, outputViewRect.width, CoreSummaryHeight(result));
            DrawCoreSummary(coreRect, result);
            outputY = coreRect.yMax + 8f;
            DrawAnalysisPanel(ref outputY, outputViewRect.width, result);
            Rect firstHitRect = new Rect(0f, outputY, outputViewRect.width, FirstHitAnalysisHeight(result));
            DrawFirstHitAnalysis(firstHitRect, result);
            outputY = firstHitRect.yMax + 8f;
            Rect armorRect = new Rect(0f, outputY, outputViewRect.width, ArmorResultsHeight(outputViewRect.width));
            DrawArmorResults(armorRect);
            outputY = armorRect.yMax + 8f;
            Rect dpsRect = new Rect(0f, outputY, outputViewRect.width, DpsHeight(outputViewRect.width, result));
            DrawDpsResults(dpsRect, result);
            outputY = dpsRect.yMax + 8f;
            Rect technicalRect = new Rect(0f, outputY, outputViewRect.width, TechnicalDetailsHeight(result));
            DrawTechnicalDetails(technicalRect, result);
            outputY = technicalRect.yMax + 8f;
            Widgets.EndScrollView();

            Rect outRect = new Rect(outputOutRect.xMax + gap, bodyRect.y, rightWidth, bodyRect.height);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, GetScrollViewHeight(false));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            float y = 0f;
            DrawAnalysisMode(ref y, viewRect.width);
            if (input.AnalysisMode == HitChanceAnalysisMode.Timeline)
            {
                DrawTimelineWindowMode(ref y, viewRect.width);
            }
            if (input.AnalysisMode == HitChanceAnalysisMode.DistanceCurve || input.AnalysisMode == HitChanceAnalysisMode.BallisticDistribution)
            {
                DrawInt(ref y, viewRect.width, "CEHCC_AnalysisSamples".Translate(), ref input.AnalysisSamples, ref analysisSamplesBuffer, 100, 50000);
            }

            DrawSection(ref y, viewRect.width, "CEHCC_SectionShot".Translate());
            DrawFloat(ref y, viewRect.width, "CEHCC_DistanceCells".Translate(), ref input.DistanceCells, ref distanceBuffer, 0.1f, 300f);
            DrawFloat(ref y, viewRect.width, "CEHCC_MaxRangeCells".Translate(), ref input.MaxRangeCells, ref maxRangeBuffer, 1f, 500f);
            DrawFloat(ref y, viewRect.width, "CEHCC_ProjectileSpeed".Translate(), ref input.ShotSpeedCellsPerSecond, ref speedBuffer, 1f, 1000f);
            DrawFloat(ref y, viewRect.width, "CEHCC_ShotHeight".Translate(), ref input.ShotHeightCells, ref shotHeightBuffer, 0f, 5f);
            DrawFloat(ref y, viewRect.width, "CEHCC_ShooterMaxHeight".Translate(), ref input.ShooterMaxHeightCells, ref shooterMaxHeightBuffer, 0f, 5f);
            DrawFloat(ref y, viewRect.width, "CEHCC_GravityFactor".Translate(), ref input.GravityFactor, ref gravityFactorBuffer, 0f, 10f);

            DrawSection(ref y, viewRect.width, "CEHCC_SectionWeaponShooter".Translate());
            DrawFloat(ref y, viewRect.width, "CEHCC_SwayDegrees".Translate(), ref input.SwayDegrees, ref swayBuffer, 0f, 20f);
            DrawFloat(ref y, viewRect.width, "CEHCC_SpreadDegrees".Translate(), ref input.SpreadDegrees, ref spreadBuffer, 0f, 20f);
            DrawFloat(ref y, viewRect.width, "CEHCC_RecoilAmount".Translate(), ref input.RecoilAmount, ref recoilBuffer, 0f, 20f);
            if (loadedWeaponHasAttachments)
            {
                DrawAttachments(ref y, viewRect.width);
            }
            if (loadedWeaponHasBipod)
            {
                DrawBipod(ref y, viewRect.width);
            }
            if (loadedWeaponHasQuality)
            {
                DrawQuality(ref y, viewRect.width);
            }
            DrawFloat(ref y, viewRect.width, "CEHCC_ShootingAccuracy".Translate(), ref input.ShootingAccuracy, ref shootingAccuracyBuffer, 0f, 4.5f);
            DrawFloat(ref y, viewRect.width, "CEHCC_AimingAccuracy".Translate(), ref input.AimingAccuracy, ref aimingAccuracyBuffer, 0f, 1.5f);
            DrawFloat(ref y, viewRect.width, "CEHCC_SightsEfficiency".Translate(), ref input.SightsEfficiency, ref sightsEfficiencyBuffer, 0.02f, 10f);
            DrawFloat(ref y, viewRect.width, "CEHCC_AimingDelayFactor".Translate(), ref input.AimingDelayFactor, ref aimingDelayFactorBuffer, 0.01f, 2f);
            DrawFloat(ref y, viewRect.width, "CEHCC_ReloadSpeed".Translate(), ref input.ReloadSpeed, ref reloadSpeedBuffer, 0.001f, 10f);
            DrawAimMode(ref y, viewRect.width);

            DrawSection(ref y, viewRect.width, "CEHCC_SectionTargetSimulation".Translate());
            DrawFloat(ref y, viewRect.width, "CEHCC_TargetHeight".Translate(), ref input.TargetHeightMeters, ref targetHeightBuffer, 0.05f, 10f);
            DrawFloat(ref y, viewRect.width, "CEHCC_TargetWidth".Translate(), ref input.TargetWidthMeters, ref targetWidthBuffer, 0.05f, 10f);
            DrawTargetMode(ref y, viewRect.width);
            DrawBurstMode(ref y, viewRect.width);
            DrawFloat(ref y, viewRect.width, "CEHCC_Rpm".Translate(), ref input.Rpm, ref rpmBuffer, 0f, 3000f);
            DrawInt(ref y, viewRect.width, "CEHCC_MonteCarloSamples".Translate(), ref input.Samples, ref samplesBuffer, 100, 200000);
            DrawInt(ref y, viewRect.width, "CEHCC_RandomSeed".Translate(), ref input.Seed, ref seedBuffer, 0, int.MaxValue);

            if (showAdvanced)
            {
                DrawSection(ref y, viewRect.width, "CEHCC_SectionAdvanced".Translate());
                DrawFloat(ref y, viewRect.width, "CEHCC_Darkness".Translate(), ref input.Darkness, ref darknessBuffer, 0f, 1f);
                DrawFloat(ref y, viewRect.width, "CEHCC_NightVisionEfficiency".Translate(), ref input.NightVisionEfficiency, ref nightVisionEfficiencyBuffer, 0f, 1f);
                DrawFloat(ref y, viewRect.width, "CEHCC_ReloadFactor".Translate(), ref input.ReloadFactor, ref reloadFactorBuffer, 0.01f, 10f);
                DrawFloat(ref y, viewRect.width, "CEHCC_TargetArmorSharp".Translate(), ref input.TargetArmorSharp, ref targetArmorSharpBuffer, 0f, 10000f);
                DrawFloat(ref y, viewRect.width, "CEHCC_TargetArmorBlunt".Translate(), ref input.TargetArmorBlunt, ref targetArmorBluntBuffer, 0f, 10000f);
                DrawFloat(ref y, viewRect.width, "CEHCC_TargetArmorHeat".Translate(), ref input.TargetArmorHeat, ref targetArmorHeatBuffer, 0f, 10000f);
                DrawFloat(ref y, viewRect.width, "CEHCC_TargetArmorElectric".Translate(), ref input.TargetArmorElectric, ref targetArmorElectricBuffer, 0f, 10000f);
                DrawFloat(ref y, viewRect.width, "CEHCC_WeatherError".Translate(), ref input.WeatherError, ref weatherBuffer, 0f, 1f);
                DrawFloat(ref y, viewRect.width, "CEHCC_SmokeDensity".Translate(), ref input.SmokeDensity, ref smokeBuffer, 0f, 50f);
                DrawFloat(ref y, viewRect.width, "CEHCC_TargetMoveSpeed".Translate(), ref input.TargetMoveSpeedCellsPerSecond, ref targetMoveSpeedBuffer, 0f, 20f);
                DrawFloat(ref y, viewRect.width, "CEHCC_TargetMoveDirection".Translate(), ref input.TargetMoveDirectionDegrees, ref targetMoveDirectionBuffer, 0f, 360f);
                DrawInt(ref y, viewRect.width, "CEHCC_ShooterThingId".Translate(), ref input.ShooterThingId, ref shooterThingIdBuffer, int.MinValue, int.MaxValue);
                DrawInt(ref y, viewRect.width, "CEHCC_SwayStartTick".Translate(), ref input.SwayStartTick, ref swayStartTickBuffer, -1, int.MaxValue);
                DrawCheckbox(ref y, viewRect.width, "CEHCC_ShooterSuppressed".Translate(), ref input.ShooterSuppressed);
                DrawCheckbox(ref y, viewRect.width, "CEHCC_BlindFiring".Translate(), ref input.BlindFiring);
                DrawCheckbox(ref y, viewRect.width, "CEHCC_FasterRepeatShots".Translate(), ref input.FasterRepeatShots);
                DrawCheckbox(ref y, viewRect.width, "CEHCC_InstantProjectile".Translate(), ref input.InstantProjectile);
                DrawCheckbox(ref y, viewRect.width, "CEHCC_InstantIgnoresMechanicalSpread".Translate(), ref input.InstantProjectileIgnoresMechanicalSpread);
                DrawFloat(ref y, viewRect.width, "CEHCC_CircularMissRadius".Translate(), ref input.CircularMissRadiusCells, ref circularBuffer, 0f, 100f);
                DrawFloat(ref y, viewRect.width, "CEHCC_IndirectFireShift".Translate(), ref input.IndirectFireShiftCells, ref indirectBuffer, 0f, 100f);
            }

            MarkDirtyIfInputsChanged();
            Widgets.EndScrollView();
        }

        private void DrawTitleBar(Rect inRect)
        {
            const float iconSize = 28f;
            const float iconGap = 6f;
            Rect glossaryButton = new Rect(inRect.xMax - iconSize, inRect.y + 2f, iconSize, iconSize);
            Rect toolsButton = new Rect(glossaryButton.x - iconGap - iconSize, glossaryButton.y, iconSize, iconSize);
            Rect minifyButton = new Rect(toolsButton.x - iconGap - iconSize, glossaryButton.y, iconSize, iconSize);

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, Mathf.Max(0f, minifyButton.x - inRect.x - 6f), 34f), "CEHCC_WindowTitle".Translate());
            Text.Font = GameFont.Small;

            if (DrawTitleIconButton(minifyButton, minimized ? RestoreIcon : MinimizeIcon, minimized ? "CEHCC_RestoreWindow".Translate() : "CEHCC_MinimizeWindow".Translate()))
            {
                SetMinimized(!minimized);
            }
            if (DrawTitleIconButton(toolsButton, TexButton.OpenInspectSettings, "CEHCC_Tools".Translate()))
            {
                OpenToolsWindow();
            }
            if (DrawTitleIconButton(glossaryButton, TexButton.Info, "CEHCC_GlossaryTooltip".Translate()))
            {
                Find.WindowStack.Add(new Dialog_CEHitChanceGlossary());
            }
        }

        private static bool DrawTitleIconButton(Rect rect, Texture2D icon, TaggedString tooltip)
        {
            bool clicked = Widgets.ButtonImage(rect, icon);
            TooltipHandler.TipRegion(rect, tooltip);
            return clicked;
        }

        private void SetMinimized(bool value)
        {
            if (minimized == value)
            {
                return;
            }

            minimized = value;
            if (minimized)
            {
                expandedWindowSize = new Vector2(windowRect.width, windowRect.height);
                windowRect.width = Mathf.Min(windowRect.width, MinimizedWindowWidth);
                windowRect.height = MinimizedWindowHeight;
                closeOnClickedOutside = false;
                preventCameraMotion = false;
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
            }
            else
            {
                windowRect.width = Mathf.Max(expandedWindowSize.x, MinimizedWindowWidth);
                windowRect.height = Mathf.Max(expandedWindowSize.y, InitialSize.y);
                closeOnClickedOutside = true;
                preventCameraMotion = true;
            }

            ClampWindowToScreen();
            Widgets.mouseOverScrollViewStack.Clear();
        }

        private void ClampWindowToScreen()
        {
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, UI.screenWidth - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, UI.screenHeight - windowRect.height));
        }

        private List<HeaderButton> HeaderButtons()
        {
            var buttons = new List<HeaderButton>
            {
                new HeaderButton("CEHCC_RestoreDefaults".Translate().ToString(), RestoreDefaults),
                new HeaderButton("CEHCC_ChooseWeapon".Translate().ToString(), ChooseWeaponFromMenu),
                new HeaderButton("CEHCC_LoadSelectedWeapon".Translate().ToString(), LoadSelectedWeaponData),
                new HeaderButton("CEHCC_LoadSelectedShooter".Translate().ToString(), LoadSelectedShooterData),
                new HeaderButton("CEHCC_LoadSelectedTarget".Translate().ToString(), LoadSelectedTargetData),
                new HeaderButton("CEHCC_ToggleAdvanced".Translate().ToString(), delegate { showAdvanced = !showAdvanced; }),
                new HeaderButton("CEHCC_TargetPresets".Translate().ToString(), ChooseTargetPreset),
                new HeaderButton("CEHCC_ObstacleEditor".Translate().ToString(), OpenObstacleEditor),
                new HeaderButton("CEHCC_Recalculate".Translate().ToString(), delegate { forceRecalculate = true; }),
                new HeaderButton("CEHCC_SaveComparisonSnapshot".Translate().ToString(), SaveCurrentComparisonSnapshot),
                new HeaderButton("CEHCC_CompareSelectedShooter".Translate().ToString(), LoadComparisonShooterData)
            };

            if (comparisonSnapshot != null)
            {
                buttons.Add(new HeaderButton("CEHCC_ComparisonSetBaseline".Translate().ToString(), SetComparisonAsBaseline));
                buttons.Add(new HeaderButton("CEHCC_ComparisonClear".Translate().ToString(), delegate
                {
                    comparisonSnapshot = null;
                    StoreSessionState();
                }));
            }

            return buttons;
        }

        private float HeaderButtonsHeight(float width)
        {
            int rows = HeaderButtonRows(HeaderButtons().Count, width);
            return rows <= 0 ? 0f : rows * 30f + (rows - 1) * 6f;
        }

        private static int HeaderButtonRows(int count, float width)
        {
            if (count <= 0)
            {
                return 0;
            }

            const float minButtonWidth = 150f;
            const float gap = 6f;
            int maxPerRow = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (minButtonWidth + gap)));
            return Mathf.CeilToInt(count / (float)maxPerRow);
        }

        private void DrawHeaderButtons(Rect rect)
        {
            List<HeaderButton> buttons = HeaderButtons();
            int rows = HeaderButtonRows(buttons.Count, rect.width);
            if (rows <= 0)
            {
                return;
            }

            const float rowHeight = 30f;
            const float gap = 6f;
            int index = 0;
            int columns = Mathf.CeilToInt(buttons.Count / (float)rows);
            for (int row = 0; row < rows; row++)
            {
                int rowCount = Mathf.Min(columns, buttons.Count - index);
                float buttonWidth = (rect.width - gap * (rowCount - 1)) / rowCount;
                float y = rect.y + row * (rowHeight + gap);
                for (int col = 0; col < rowCount; col++)
                {
                    HeaderButton button = buttons[index++];
                    Rect buttonRect = new Rect(rect.x + col * (buttonWidth + gap), y, buttonWidth, rowHeight);
                    if (Widgets.ButtonText(buttonRect, button.Label))
                    {
                        button.Action?.Invoke();
                    }
                }
            }
        }

        private void OpenToolsWindow()
        {
            Find.WindowStack.Add(new Dialog_CEHitChanceTools());
        }

        private void OpenObstacleEditor()
        {
            Find.WindowStack.Add(new Dialog_LineOfFireObstacleEditor(input));
        }

        private void DrawCurrentLoadout(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            float x = rect.x + 12f;
            float y = rect.y + 8f;
            Rect iconRect = new Rect(x, y, 56f, 56f);
            Widgets.DrawBoxSolid(iconRect, new Color(0.05f, 0.055f, 0.06f));
            Widgets.DrawBox(iconRect, 1);
            if (loadedWeapon != null)
            {
                Widgets.ThingIcon(new Rect(iconRect.x + 4f, iconRect.y + 4f, iconRect.width - 8f, iconRect.height - 8f), loadedWeapon, 1f, null, false, 1f, false);
                TooltipHandler.TipRegion(iconRect, loadedWeapon.LabelCap);
            }
            else
            {
                TextAnchor oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(iconRect, "-");
                Text.Anchor = oldAnchor;
            }

            float textX = iconRect.xMax + 12f;
            float width = rect.width - (textX - rect.x) - 12f;
            string weapon = loadedWeapon != null ? loadedWeapon.LabelCap.ToString() : "CEHCC_CurrentManualData".Translate().ToString();
            string ammo = loadedAmmoChoice != null ? loadedAmmoChoice.Label : "CEHCC_AmmoChoiceDefault".Translate().ToString();
            string shooter = string.IsNullOrEmpty(loadedShooterLabel) ? "CEHCC_CurrentManualData".Translate().ToString() : loadedShooterLabel;

            Text.Font = GameFont.Small;
            DrawEllipsesWithTooltip(new Rect(textX, y, width, 24f), "CEHCC_CurrentWeapon".Translate(weapon).ToString());
            float half = (width - 12f) / 2f;
            DrawEllipsesWithTooltip(new Rect(textX, y + 30f, half, 24f), "CEHCC_CurrentAmmo".Translate(ammo).ToString());
            DrawEllipsesWithTooltip(new Rect(textX + half + 12f, y + 30f, half, 24f), "CEHCC_CurrentShooter".Translate(shooter).ToString());
        }

        private static void DrawEllipsesWithTooltip(Rect rect, string label)
        {
            Widgets.LabelEllipses(rect, label);
            if (Text.CalcSize(label).x > rect.width)
            {
                TooltipHandler.TipRegion(rect, label);
            }
        }

        private void DrawCoreSummary(Rect rect, HitChanceResult result)
        {
            Widgets.DrawMenuSection(rect);
            float x = rect.x + 12f;
            float y = rect.y + 8f;
            float width = rect.width - 24f;
            float line = 24f;
            DamageProfile damageProfile = loadedDamageProfile ?? DamageProfile.Empty;
            DpsResult dps = DpsCalculator.Calculate(damageProfile, input, result);
            DpsResult compareDps = comparisonSnapshot?.Result != null
                ? DpsCalculator.Calculate(comparisonSnapshot.DamageProfile, comparisonSnapshot.Input, comparisonSnapshot.Result)
                : null;

            Widgets.Label(new Rect(x, y, width, line), "CEHCC_CoreSummaryTitle".Translate());
            y += line;
            float gap = 8f;
            float metricWidth = (width - gap * 2f) / 3f;
            if (comparisonSnapshot != null && compareDps != null)
            {
                DrawMetricWithDelta(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreFirstHit".Translate(), Percent(result.MonteCarloSingle), SignedPercent(result.MonteCarloSingle - comparisonSnapshot.Result.MonteCarloSingle), result.MonteCarloSingle - comparisonSnapshot.Result.MonteCarloSingle, true, new Color(0.78f, 0.88f, 1f));
                DrawMetricWithDelta(new Rect(x + metricWidth + gap, y, metricWidth, 44f), "CEHCC_CoreMagazineTime".Translate(), dps.MagazineFireSeconds.ToString("0.##") + "s", SignedNumber(dps.MagazineFireSeconds - compareDps.MagazineFireSeconds, "0.##") + "s", dps.MagazineFireSeconds - compareDps.MagazineFireSeconds, true, new Color(0.95f, 0.86f, 0.68f));
                DrawDamageMetricWithDelta(new Rect(x + (metricWidth + gap) * 2f, y, metricWidth, 44f), "CEHCC_CoreMagazineDps".Translate(), damageProfile.HasDamage, dps.TotalExpectedDpsMagazine, compareDps.TotalExpectedDpsMagazine, true, "", "0.##");
            }
            else
            {
                DrawMetric(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreFirstHit".Translate(), Percent(result.MonteCarloSingle), new Color(0.78f, 0.88f, 1f));
                DrawMetric(new Rect(x + metricWidth + gap, y, metricWidth, 44f), "CEHCC_CoreMagazineTime".Translate(), dps.MagazineFireSeconds.ToString("0.##") + "s", new Color(0.95f, 0.86f, 0.68f));
                DrawMetric(new Rect(x + (metricWidth + gap) * 2f, y, metricWidth, 44f), "CEHCC_CoreMagazineDps".Translate(), damageProfile.HasDamage ? dps.TotalExpectedDpsMagazine.ToString("0.##") : "-", new Color(0.95f, 0.86f, 0.68f));
            }
            y += 48f;
            if (comparisonSnapshot != null && compareDps != null)
            {
                DrawDamageMetricWithDelta(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreDps60".Translate(), damageProfile.HasDamage, dps.TotalExpectedDps60s, compareDps.TotalExpectedDps60s, true, "", "0.##");
                DrawMetricWithDelta(new Rect(x + metricWidth + gap, y, metricWidth, 44f), "CEHCC_CoreFireRate".Translate(), dps.ShotsPerSecond.ToString("0.###") + "/s", SignedNumber(dps.ShotsPerSecond - compareDps.ShotsPerSecond, "0.###") + "/s", dps.ShotsPerSecond - compareDps.ShotsPerSecond, true, new Color(0.95f, 0.86f, 0.68f));
                DrawDamageMetricWithDelta(new Rect(x + (metricWidth + gap) * 2f, y, metricWidth, 44f), "CEHCC_CoreMagazineDamage".Translate(), damageProfile.HasDamage, dps.TotalExpectedDamagePerMagazine, compareDps.TotalExpectedDamagePerMagazine, true, "", "0.##");
                y += 48f;
                DrawMetricWithDelta(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreAverageHit".Translate(), Percent(dps.AverageDirectHitChance), SignedPercent(dps.AverageDirectHitChance - compareDps.AverageDirectHitChance), dps.AverageDirectHitChance - compareDps.AverageDirectHitChance, true, new Color(0.78f, 0.88f, 1f));
            }
            else
            {
                DrawMetric(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreDps60".Translate(), damageProfile.HasDamage ? dps.TotalExpectedDps60s.ToString("0.##") : "-", new Color(0.95f, 0.86f, 0.68f));
                DrawMetric(new Rect(x + metricWidth + gap, y, metricWidth, 44f), "CEHCC_CoreFireRate".Translate(), dps.ShotsPerSecond.ToString("0.###") + "/s", new Color(0.95f, 0.86f, 0.68f));
                DrawMetric(new Rect(x + (metricWidth + gap) * 2f, y, metricWidth, 44f), "CEHCC_CoreMagazineDamage".Translate(), damageProfile.HasDamage ? dps.TotalExpectedDamagePerMagazine.ToString("0.##") : "-", new Color(0.95f, 0.86f, 0.68f));
                y += 48f;
                DrawMetric(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreAverageHit".Translate(), Percent(dps.AverageDirectHitChance), new Color(0.78f, 0.88f, 1f));
            }
            if (comparisonSnapshot != null)
            {
                bool compareHasDamage = comparisonSnapshot.DamageProfile != null && comparisonSnapshot.DamageProfile.HasDamage;
                y += 52f;
                Widgets.Label(new Rect(x, y, width, line), "CEHCC_ComparisonTitle".Translate(comparisonSnapshot.Label));
                y += line;
                DrawMetric(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreFirstHit".Translate(), Percent(comparisonSnapshot.Result.MonteCarloSingle), new Color(0.78f, 0.88f, 1f));
                DrawMetric(new Rect(x + metricWidth + gap, y, metricWidth, 44f), "CEHCC_CoreMagazineTime".Translate(), compareDps.MagazineFireSeconds.ToString("0.##") + "s", new Color(0.95f, 0.86f, 0.68f));
                DrawMetric(new Rect(x + (metricWidth + gap) * 2f, y, metricWidth, 44f), "CEHCC_CoreMagazineDps".Translate(), compareHasDamage ? compareDps.TotalExpectedDpsMagazine.ToString("0.##") : "-", new Color(0.95f, 0.86f, 0.68f));
                y += 48f;
                DrawMetric(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreDps60".Translate(), compareHasDamage ? compareDps.TotalExpectedDps60s.ToString("0.##") : "-", new Color(0.95f, 0.86f, 0.68f));
                DrawMetric(new Rect(x + metricWidth + gap, y, metricWidth, 44f), "CEHCC_CoreFireRate".Translate(), compareDps.ShotsPerSecond.ToString("0.###") + "/s", new Color(0.95f, 0.86f, 0.68f));
                DrawMetric(new Rect(x + (metricWidth + gap) * 2f, y, metricWidth, 44f), "CEHCC_CoreMagazineDamage".Translate(), compareHasDamage ? compareDps.TotalExpectedDamagePerMagazine.ToString("0.##") : "-", new Color(0.95f, 0.86f, 0.68f));
                y += 48f;
                DrawMetric(new Rect(x, y, metricWidth, 44f), "CEHCC_CoreAverageHit".Translate(), Percent(compareDps.AverageDirectHitChance), new Color(0.78f, 0.88f, 1f));
            }
        }

        private static void DrawMetric(Rect rect, string label, string value, Color valueColor)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.42f, 0.42f, 0.42f));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), label);
            Text.Font = GameFont.Small;
            Color oldColor = GUI.color;
            GUI.color = valueColor;
            Widgets.Label(new Rect(rect.x, rect.y + 18f, rect.width, 24f), value);
            GUI.color = oldColor;
        }

        private static void DrawDamageMetricWithDelta(Rect rect, string label, bool hasDamage, float value, float compareValue, bool higherIsBetter, string suffix = "", string format = "0.##")
        {
            if (!hasDamage)
            {
                DrawMetric(rect, label, "-", new Color(0.95f, 0.86f, 0.68f));
                return;
            }

            float delta = value - compareValue;
            DrawMetricWithDelta(rect, label, value.ToString(format) + suffix, SignedNumber(delta, format) + suffix, delta, higherIsBetter, new Color(0.95f, 0.86f, 0.68f));
        }

        private static void DrawMetricWithDelta(Rect rect, string label, string valueText, string deltaText, float delta, bool higherIsBetter, Color valueColor)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.42f, 0.42f, 0.42f));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), label);
            Text.Font = GameFont.Small;
            Color oldColor = GUI.color;
            GUI.color = valueColor;
            Widgets.Label(new Rect(rect.x, rect.y + 18f, rect.width, 24f), valueText);
            Vector2 valueSize = Text.CalcSize(valueText + " ");
            GUI.color = DeltaColor(delta, higherIsBetter);
            Widgets.Label(new Rect(rect.x + Mathf.Min(valueSize.x, rect.width * 0.55f), rect.y + 18f, rect.width, 24f), "(" + deltaText + ")");
            GUI.color = oldColor;
        }

        private static void DrawMetricComparison(Rect rect, string label, float compareValue, float baseValue, bool higherIsBetter, bool percent, string suffix = "", string format = "0.##")
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.42f, 0.42f, 0.42f));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), label);
            Text.Font = GameFont.Small;
            string valueText = percent ? Percent(compareValue) : compareValue.ToString(format) + suffix;
            float delta = compareValue - baseValue;
            string deltaText = percent ? SignedPercent(delta) : SignedNumber(delta, format) + suffix;
            Color oldColor = GUI.color;
            Widgets.Label(new Rect(rect.x, rect.y + 18f, rect.width, 24f), valueText);
            Vector2 valueSize = Text.CalcSize(valueText + " ");
            GUI.color = DeltaColor(delta, higherIsBetter);
            Widgets.Label(new Rect(rect.x + Mathf.Min(valueSize.x, rect.width * 0.55f), rect.y + 18f, rect.width, 24f), "(" + deltaText + ")");
            GUI.color = oldColor;
        }

        private void DrawFirstHitAnalysis(Rect rect, HitChanceResult result)
        {
            Widgets.DrawMenuSection(rect);
            float x = rect.x + 12f;
            float y = rect.y + 8f;
            float line = 24f;

            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_FirstHitTitle".Translate());
            y += line;
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_FirstHitSummary".Translate(Percent(result.MonteCarloSingle), Percent(result.CeEstimatedSingle)));
            y += line;
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultTargetBox".Translate(result.TargetWidthCellsForCeMath.ToString("0.###"), result.TargetHeightCells.ToString("0.###"), result.TargetAimHeightCells.ToString("0.###"), result.VisibilityScalarCells.ToString("0.###")));
            y += line;
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultAxisBreakdown".Translate(Percent(result.MonteCarloHorizontalFirst), Percent(result.MonteCarloVerticalFirst)));
            y += line;
            if (result.LineOfFireObstaclesApplied)
            {
                Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultObstacleFilter".Translate(result.LineOfFireObstacleCount, Percent(result.LineOfFireBlockedSingle), Percent(result.LineOfFireBlockedBurstAny), result.LineOfFireObstacleLabel ?? ""));
                y += line;
            }
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultAxisDiagnosis".Translate(AxisLabel(result.DominantAxis), SaturationLabel(result)));
            y += line;
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultDerivedErrors".Translate(result.VisibilityErrorCells.ToString("0.###"), result.LeadErrorCells.ToString("0.###"), result.RangeErrorCells.ToString("0.###")));
            y += line;
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultHint".Translate(ResultHint(result)));
            y += line;
            if (!string.IsNullOrEmpty(loadedProjectileWarning))
            {
                Widgets.Label(new Rect(x, y, rect.width - 24f, line), loadedProjectileWarning);
                y += line;
            }

            if (result.TargetModeForcedTorso && input.TargetMode != HitChanceTargetMode.Torso)
            {
                Widgets.Label(new Rect(x, y, rect.width - 24f, line), TargetModeForcedReason());
                y += line;
            }

            if (comparisonSnapshot != null)
            {
                Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ComparisonFirstHit".Translate(
                    comparisonSnapshot.Label,
                    Percent(comparisonSnapshot.Result.MonteCarloSingle),
                    SignedPercent(comparisonSnapshot.Result.MonteCarloSingle - result.MonteCarloSingle),
                    Percent(comparisonSnapshot.Result.MonteCarloHorizontalFirst),
                    SignedPercent(comparisonSnapshot.Result.MonteCarloHorizontalFirst - result.MonteCarloHorizontalFirst),
                    Percent(comparisonSnapshot.Result.MonteCarloVerticalFirst),
                    SignedPercent(comparisonSnapshot.Result.MonteCarloVerticalFirst - result.MonteCarloVerticalFirst)));
            }
        }

        private void DrawTechnicalDetails(Rect rect, HitChanceResult result)
        {
            Widgets.DrawMenuSection(rect);
            float x = rect.x + 12f;
            float y = rect.y + 8f;
            float line = 24f;

            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_TechnicalDetailsTitle".Translate());
            y += line;
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultRealFireInputs".Translate(result.EffectiveSwayDegrees.ToString("0.###"), result.EffectiveSpreadDegrees.ToString("0.###"), result.BurstShotIntervalTicks, result.GravityPerWidth.ToString("0.###")));
            y += line;
            if (result.InstantProjectile)
            {
                Widgets.Label(new Rect(x, y, rect.width - 24f, line), (result.InstantProjectileIgnoresMechanicalSpread ? "CEHCC_ResultInstantProjectileNoSpread" : "CEHCC_ResultInstantProjectile").Translate());
                y += line;
            }
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultRangeVertical".Translate(result.RangeVerticalErrorCells.ToString("0.####"), (result.RangeVerticalErrorCells * HitChanceCalculator.MetersPerCellHeight).ToString("0.###")));
            y += line;
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultTargetCoverAdjustment".Translate(result.TargetCoverHeightCells.ToString("0.###"), result.AdjustedShotHeightCells.ToString("0.###")));
            y += line;
            if (result.MonteCarloPerShot != null && result.MonteCarloPerShot.Length > 1)
            {
                string perShot = "";
                for (int i = 0; i < result.MonteCarloPerShot.Length; i++)
                {
                    if (i > 0)
                    {
                        perShot += "  ";
                    }
                    perShot += "#" + (i + 1) + " " + Percent(result.MonteCarloPerShot[i]);
                }
                Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultPerShot".Translate(perShot));
                y += line;
            }
            else
            {
                Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ResultShotAngle".Translate((result.ShotAngleRadians * Mathf.Rad2Deg).ToString("0.###")));
                y += line;
            }
            Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_TechnicalFormulaReference".Translate(Percent(result.CeEstimatedSingle)));
            y += line;
            if (comparisonSnapshot != null)
            {
                Widgets.Label(new Rect(x, y, rect.width - 24f, line), "CEHCC_ComparisonTechnical".Translate(
                    comparisonSnapshot.Label,
                    comparisonSnapshot.Result.EffectiveSwayDegrees.ToString("0.###"),
                    SignedNumber(comparisonSnapshot.Result.EffectiveSwayDegrees - result.EffectiveSwayDegrees, "0.###"),
                    comparisonSnapshot.Result.RangeErrorCells.ToString("0.###"),
                    SignedNumber(comparisonSnapshot.Result.RangeErrorCells - result.RangeErrorCells, "0.###"),
                    DpsCalculator.EffectiveWarmupSeconds(comparisonSnapshot.Input).ToString("0.##"),
                    SignedNumber(DpsCalculator.EffectiveWarmupSeconds(comparisonSnapshot.Input) - DpsCalculator.EffectiveWarmupSeconds(input), "0.##"),
                    DpsCalculator.EffectiveReloadSeconds(comparisonSnapshot.Input).ToString("0.##"),
                    SignedNumber(DpsCalculator.EffectiveReloadSeconds(comparisonSnapshot.Input) - DpsCalculator.EffectiveReloadSeconds(input), "0.##")));
            }
        }

        private void DrawDpsResults(Rect rect, HitChanceResult result)
        {
            Widgets.DrawMenuSection(rect);
            float x = rect.x + 12f;
            float y = rect.y + 8f;
            float width = rect.width - 24f;
            float line = 24f;
            DamageProfile damageProfile = loadedDamageProfile ?? DamageProfile.Empty;
            DpsResult dps = DpsCalculator.Calculate(damageProfile, input, result);

            Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsTitle".Translate());
            y += line;

            if (!damageProfile.HasDamage)
            {
                Widgets.Label(new Rect(x, y, width, line), "CEHCC_ResultNoDamageData".Translate());
                return;
            }

            Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsRate".Translate(dps.ShotsPerSecond.ToString("0.###")));
            y += line;
            Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsAverageHit".Translate(Percent(dps.AverageDirectHitChance)));
            y += line;
            if (input.FasterRepeatShots)
            {
                Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsFasterRepeatShots".Translate((dps.RepeatWarmupReduction * 100f).ToString("0.#")));
                y += line;
            }
            DrawWrappedLabel(ref y, x, width, "CEHCC_DpsDirectDamage".Translate(damageProfile.DirectLabel, dps.DirectEffectiveDamagePerShot.ToString("0.##"), dps.DirectArmoredDamagePerShot.ToString("0.##")));
            if (dps.HasLaserDamageFalloff)
            {
                Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsLaserFalloff".Translate(Percent(dps.LaserDamageFalloffMultiplier)));
                y += line;
            }
            Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsDirectDpsStats".Translate(dps.TotalExpectedDpsMagazine.ToString("0.##"), dps.TotalExpectedDps60s.ToString("0.##"), dps.TotalExpectedDps.ToString("0.##")));
            y += line;
            Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsMagazineStats".Translate(dps.TotalExpectedDamagePerMagazine.ToString("0.##"), dps.MagazineFireSeconds.ToString("0.##"), dps.TotalExpectedDamagePerBurst.ToString("0.##")));
            y += line;
            Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsNearMissStats".Translate(dps.NearMissDirectDps.ToString("0.##"), Percent(dps.NearMissAverageChance), dps.NearMissDirectDamagePerBurst.ToString("0.##")));
            y += line;
            if (damageProfile.RangeLines.Count > 0)
            {
                Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsRangeStats".Translate(dps.RangeDamagePerShot.ToString("0.##"), dps.RangeExpectedDps.ToString("0.##"), dps.RangeExpectedDamagePerBurst.ToString("0.##")));
                y += line;
                DrawDamageLines(ref y, x, width, damageProfile.RangeLines);
            }
            if (damageProfile.FragmentLines.Count > 0)
            {
                Widgets.Label(new Rect(x, y, width, line), "CEHCC_DpsFragmentStats".Translate(dps.FragmentPaperDamagePerShot.ToString("0.##"), dps.FragmentPaperDps.ToString("0.##")));
                y += line;
                DrawDamageLines(ref y, x, width, damageProfile.FragmentLines);
            }
            if (comparisonSnapshot != null)
            {
                DpsResult compareDps = DpsCalculator.Calculate(comparisonSnapshot.DamageProfile, comparisonSnapshot.Input, comparisonSnapshot.Result);
                y += 4f;
                DrawWrappedLabel(ref y, x, width, "CEHCC_ComparisonDps".Translate(
                    comparisonSnapshot.Label,
                    Percent(compareDps.AverageDirectHitChance),
                    SignedPercent(compareDps.AverageDirectHitChance - dps.AverageDirectHitChance),
                    compareDps.TotalExpectedDpsMagazine.ToString("0.##"),
                    SignedNumber(compareDps.TotalExpectedDpsMagazine - dps.TotalExpectedDpsMagazine, "0.##"),
                    compareDps.TotalExpectedDps60s.ToString("0.##"),
                    SignedNumber(compareDps.TotalExpectedDps60s - dps.TotalExpectedDps60s, "0.##"),
                    compareDps.NearMissDirectDps.ToString("0.##"),
                    SignedNumber(compareDps.NearMissDirectDps - dps.NearMissDirectDps, "0.##")));
            }
        }

        private void DrawArmorResults(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            float x = rect.x + 12f;
            float y = rect.y + 8f;
            float width = rect.width - 24f;
            float line = 24f;
            DamageProfile damageProfile = loadedDamageProfile ?? DamageProfile.Empty;
            ArmorDamageResult armor = DpsCalculator.CalculatePrimaryArmorResult(damageProfile, input);

            Widgets.Label(new Rect(x, y, width, line), "CEHCC_ArmorTitle".Translate());
            y += line;
            Widgets.Label(new Rect(x, y, width, line), "CEHCC_ArmorTarget".Translate(input.TargetArmorSharp.ToString("0.###"), input.TargetArmorBlunt.ToString("0.###"), input.TargetArmorHeat.ToString("0.###"), input.TargetArmorElectric.ToString("0.###")));
            y += line;

            if (!damageProfile.HasDamage || armor.PaperDamage <= 0f)
            {
                Widgets.Label(new Rect(x, y, width, line), "CEHCC_ArmorNoDirectDamage".Translate());
                return;
            }

            Widgets.Label(new Rect(x, y, width, line), "CEHCC_ArmorDirectResult".Translate(armor.PaperDamage.ToString("0.##"), armor.PostArmorDamage.ToString("0.##")));
            y += line;
            DrawWrappedLabel(ref y, x, width, "CEHCC_ArmorPenetration".Translate(ArmorPenetrationText(armor)));
            DrawWrappedLabel(ref y, x, width, "CEHCC_ArmorBreakdown".Translate(ArmorBreakdownText(armor)));
            Widgets.Label(new Rect(x, y, width, line), "CEHCC_ArmorStatus".Translate(ArmorStatusLabel(armor)));
            y += line;
            Widgets.Label(new Rect(x, y, width, line), "CEHCC_ArmorApproxNote".Translate());
        }

        private static void DrawDamageLines(ref float y, float x, float width, List<DamageLine> lines)
        {
            float line = 24f;
            if (lines.Count == 0)
            {
                return;
            }

            int count = Mathf.Min(lines.Count, 6);
            for (int i = 0; i < count; i++)
            {
                DamageLine damageLine = lines[i];
                if (damageLine == null)
                {
                    continue;
                }
                DrawWrappedLabel(ref y, x + 12f, width - 12f, "CEHCC_DpsFollowupLine".Translate(damageLine.Label, damageLine.DamagePerShot.ToString("0.##")));
            }
            if (lines.Count > count)
            {
                Widgets.Label(new Rect(x + 12f, y, width - 12f, line), "CEHCC_DpsMoreLines".Translate(lines.Count - count));
                y += line;
            }
        }

        private float CoreSummaryHeight(HitChanceResult result)
        {
            return comparisonSnapshot == null ? 16f + 24f + 48f + 48f + 44f : 16f + 24f + 48f + 48f + 52f + 24f + 48f + 48f + 44f;
        }

        private float FirstHitAnalysisHeight(HitChanceResult result)
        {
            int lines = 7;
            if (!string.IsNullOrEmpty(loadedProjectileWarning))
            {
                lines++;
            }
            if (result != null && result.TargetModeForcedTorso && input.TargetMode != HitChanceTargetMode.Torso)
            {
                lines++;
            }
            if (result != null && result.LineOfFireObstaclesApplied)
            {
                lines++;
            }
            if (comparisonSnapshot != null)
            {
                lines++;
            }
            return 16f + lines * 24f;
        }

        private float ArmorResultsHeight(float width)
        {
            DamageProfile damageProfile = loadedDamageProfile ?? DamageProfile.Empty;
            if (!damageProfile.HasDamage)
            {
                return 16f + 3f * 24f;
            }

            float innerWidth = width - 24f;
            ArmorDamageResult armor = DpsCalculator.CalculatePrimaryArmorResult(damageProfile, input);
            return 16f + 5f * 24f
                + WrappedLabelHeight("CEHCC_ArmorPenetration".Translate(ArmorPenetrationText(armor)), innerWidth)
                + WrappedLabelHeight("CEHCC_ArmorBreakdown".Translate(ArmorBreakdownText(armor)), innerWidth);
        }

        private float TechnicalDetailsHeight(HitChanceResult result)
        {
            return 16f + (comparisonSnapshot == null ? 6f : 7f) * 24f;
        }

        private float DpsHeight(float width, HitChanceResult result)
        {
            float innerWidth = width - 24f;
            DamageProfile damageProfile = loadedDamageProfile ?? DamageProfile.Empty;
            float height = 16f + 24f;
            if (!damageProfile.HasDamage)
            {
                return height + 24f;
            }

            DpsResult dps = DpsCalculator.Calculate(damageProfile, input, result);
            height += WrappedLabelHeight("CEHCC_DpsDirectDamage".Translate(damageProfile.DirectLabel, dps.DirectEffectiveDamagePerShot.ToString("0.##"), "999.99"), innerWidth);
            height += 144f;
            if (dps.HasLaserDamageFalloff)
            {
                height += 24f;
            }
            if (input.FasterRepeatShots)
            {
                height += 24f;
            }
            if (damageProfile.RangeLines.Count > 0)
            {
                height += 24f + DamageLinesHeight(damageProfile.RangeLines, innerWidth - 12f);
            }
            if (damageProfile.FragmentLines.Count > 0)
            {
                height += 24f + DamageLinesHeight(damageProfile.FragmentLines, innerWidth - 12f);
            }
            if (comparisonSnapshot != null)
            {
                height += 4f + WrappedLabelHeight("CEHCC_ComparisonDps".Translate(comparisonSnapshot.Label, "100.00%", "+100.00%", "999.99", "+999.99", "999.99", "+999.99", "999.99", "+999.99"), innerWidth);
            }
            return height;
        }

        private static float DamageLinesHeight(List<DamageLine> lines, float width)
        {
            if (lines.Count == 0)
            {
                return 24f;
            }

            float height = 0f;
            int count = Mathf.Min(lines.Count, 6);
            for (int i = 0; i < count; i++)
            {
                DamageLine damageLine = lines[i];
                if (damageLine != null)
                {
                    height += WrappedLabelHeight("CEHCC_DpsFollowupLine".Translate(damageLine.Label, damageLine.DamagePerShot.ToString("0.##")), width);
                }
            }
            if (lines.Count > count)
            {
                height += 24f;
            }
            return height;
        }

        private static void DrawWrappedLabel(ref float y, float x, float width, string label)
        {
            float height = WrappedLabelHeight(label, width);
            Widgets.Label(new Rect(x, y, width, height), label);
            y += height;
        }

        private static float WrappedLabelHeight(string label, float width)
        {
            return Mathf.Max(24f, Text.CalcHeight(label, width));
        }

        private static void DrawSection(ref float y, float width, string label)
        {
            y += 8f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, y, width, 24f), label);
            y += 28f;
        }

        private float GetOutputViewHeight(HitChanceResult result, float width)
        {
            float height = CurrentLoadoutHeight + 8f + CoreSummaryHeight(result) + 8f + FirstHitAnalysisHeight(result) + 8f + ArmorResultsHeight(width) + 8f + DpsHeight(width, result) + 8f + TechnicalDetailsHeight(result) + 8f;
            if (input.AnalysisMode != HitChanceAnalysisMode.None)
            {
                height += CalculateAnalysisHeight() + 8f;
            }
            return height + 40f;
        }

        private float GetScrollViewHeight(bool includeAnalysisPanel)
        {
            const float headerAndPadding = 36f;
            const float rowHeight = 30f;
            float weaponRows = 9f + (loadedWeaponHasAttachments ? 1f : 0f) + (loadedWeaponHasQuality ? 1f : 0f) + (loadedWeaponHasBipod ? 1f : 0f);
            float analysisRows = 1f;
            if (input.AnalysisMode == HitChanceAnalysisMode.Timeline)
            {
                analysisRows += 1f;
            }
            if (input.AnalysisMode == HitChanceAnalysisMode.DistanceCurve || input.AnalysisMode == HitChanceAnalysisMode.BallisticDistribution)
            {
                analysisRows += 1f;
            }
            float baseRows = analysisRows + 6f + weaponRows + 7f;
            const float baseSections = 3f;
            const float advancedRows = 18f;
            const float advancedSections = 1f;
            float height = headerAndPadding + baseRows * rowHeight + baseSections * 36f;
            if (includeAnalysisPanel)
            {
                height += CalculateAnalysisHeight();
            }

            if (showAdvanced)
            {
                height += advancedRows * rowHeight + advancedSections * 36f;
            }

            return height + 80f;
        }

        private static void DrawFloat(ref float y, float width, string label, ref float value, ref string buffer, float min, float max)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), label);
            Widgets.TextFieldNumeric(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), ref value, ref buffer, min, max);
            y += 30f;
        }

        private static void DrawInt(ref float y, float width, string label, ref int value, ref string buffer, int min, int max)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), label);
            Widgets.TextFieldNumeric(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), ref value, ref buffer, min, max);
            y += 30f;
        }

        private static void DrawCheckbox(ref float y, float width, string label, ref bool value)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.CheckboxLabeled(new Rect(row.x, row.y, width * 0.88f, row.height), label, ref value);
            y += 30f;
        }

        private void DrawAnalysisMode(ref float y, float width)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), "CEHCC_AnalysisMode".Translate());
            if (Widgets.ButtonText(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), AnalysisModeLabel(input.AnalysisMode)))
            {
                OpenAnalysisModeMenu();
            }
            y += 30f;
        }

        private void DrawAnalysisPanel(ref float y, float width, HitChanceResult result)
        {
            if (input.AnalysisMode == HitChanceAnalysisMode.None)
            {
                return;
            }

            y += 4f;
            if (input.AnalysisMode == HitChanceAnalysisMode.DistanceCurve)
            {
                HitChanceAnalysisPoint[] points = cachedDistanceCurve ?? new HitChanceAnalysisPoint[0];
                Rect panel = DrawAnalysisBox(ref y, width, Mathf.Max(1, points.Length));
                float innerY = panel.y + 8f;
                float innerX = panel.x + 12f;
                float innerWidth = panel.width - 24f;
                DrawAnalysisHeader(ref innerY, innerX, innerWidth, "CEHCC_AnalysisDistanceTitle".Translate(HitChanceCalculator.CalculateAnalysisSamples(input).ToString()));
                for (int i = 0; i < points.Length; i++)
                {
                    DrawBarRow(ref innerY, innerX, innerWidth, points[i].Label, points[i].Single, points[i].BurstAny, true);
                }
                y = panel.yMax + 8f;
                return;
            }

            if (input.AnalysisMode == HitChanceAnalysisMode.Timeline)
            {
                DrawTimelineAnalysis(ref y, width);
                return;
            }

            if (input.AnalysisMode == HitChanceAnalysisMode.BallisticDistribution)
            {
                DrawBallisticDistributionAnalysis(ref y, width);
                return;
            }

            if (result.MonteCarloPerShot == null || result.MonteCarloPerShot.Length <= 1)
            {
                Rect panel = DrawAnalysisBox(ref y, width, 0);
                float innerY = panel.y + 8f;
                DrawAnalysisHeader(ref innerY, panel.x + 12f, panel.width - 24f, "CEHCC_AnalysisBurstSingleShot".Translate());
                y = panel.yMax + 8f;
                return;
            }

            Rect burstPanel = DrawAnalysisBox(ref y, width, result.MonteCarloPerShot.Length);
            float burstY = burstPanel.y + 8f;
            float burstX = burstPanel.x + 12f;
            float burstWidth = burstPanel.width - 24f;
            DrawAnalysisHeader(ref burstY, burstX, burstWidth, "CEHCC_AnalysisBurstTitle".Translate());
            for (int i = 0; i < result.MonteCarloPerShot.Length; i++)
            {
                DrawBarRow(ref burstY, burstX, burstWidth, "#" + (i + 1), result.MonteCarloPerShot[i], result.MonteCarloBurstAny, false);
            }
            y = burstPanel.yMax + 8f;
        }

        private static Rect DrawAnalysisBox(ref float y, float width, int rowCount)
        {
            Rect panel = new Rect(0f, y, width, BoxedAnalysisHeight(rowCount));
            Widgets.DrawMenuSection(panel);
            return panel;
        }

        private static float BoxedAnalysisHeight(int rowCount)
        {
            return 16f + 26f + Mathf.Max(0, rowCount) * 26f;
        }

        private static void DrawAnalysisHeader(ref float y, float width, string label)
        {
            DrawAnalysisHeader(ref y, 0f, width, label);
        }

        private static void DrawAnalysisHeader(ref float y, float x, float width, string label)
        {
            Widgets.Label(new Rect(x, y, width, 24f), label);
            y += 26f;
        }

        private static void DrawBarRow(ref float y, float width, string label, float primary, float secondary, bool showSecondary)
        {
            DrawBarRow(ref y, 0f, width, label, primary, secondary, showSecondary);
        }

        private static void DrawBarRow(ref float y, float x, float width, string label, float primary, float secondary, bool showSecondary)
        {
            Rect row = new Rect(x, y, width, 24f);
            Widgets.Label(new Rect(row.x, row.y, 72f, row.height), label);

            float barX = 78f;
            float barWidth = Mathf.Max(80f, width * 0.42f);
            DrawPercentBar(new Rect(row.x + barX, row.y + 4f, barWidth, 14f), primary, new Color(0.48f, 0.63f, 0.78f));

            string text = showSecondary
                ? "CEHCC_AnalysisDistanceRow".Translate(Percent(primary), Percent(secondary))
                : "CEHCC_AnalysisBurstRow".Translate(Percent(primary));
            Widgets.Label(new Rect(row.x + barX + barWidth + 12f, row.y, width - barX - barWidth - 12f, row.height), text);
            y += 26f;
        }

        private void DrawBallisticDistributionAnalysis(ref float y, float width)
        {
            BallisticDistribution distribution = cachedBallisticDistribution ?? HitChanceCalculator.BuildBallisticDistribution(input);
            float topHeight = TopDistributionPlotHeight(distribution.DistanceCells);
            const float sideHeight = 184f;
            DrawAnalysisHeader(ref y, width, "CEHCC_AnalysisBallisticTitle".Translate(distribution.SampleCount, distribution.BurstShots));
            Widgets.Label(new Rect(0f, y, width, 24f), BallisticHintKey().Translate());
            y += 26f;
            DrawBallisticDisplayMode(ref y, width, distribution);
            DrawBallisticLegend(ref y, width, distribution.BurstShots);
            DrawTopDistributionPlot(new Rect(0f, y, width, topHeight), distribution);
            y += topHeight + 8f;
            DrawSideDistributionPlot(new Rect(0f, y, width, sideHeight), distribution);
            y += sideHeight + 8f;
        }

        private static float TopDistributionPlotHeight(float distanceCells)
        {
            return Mathf.Clamp(112f + Mathf.Max(0.1f, distanceCells) * 5.2f, 220f, 460f);
        }

        private void DrawBallisticDisplayMode(ref float y, float width, BallisticDistribution distribution)
        {
            ballisticSampleIndex = Mathf.Clamp(ballisticSampleIndex, 0, Mathf.Max(0, distribution.SampleCount - 1));
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.28f, row.height), "CEHCC_BallisticViewMode".Translate());
            string modeLabel = BallisticDisplayModeLabel();
            if (Widgets.ButtonText(new Rect(width * 0.3f, row.y, 140f, row.height), modeLabel))
            {
                ballisticDisplayMode = NextBallisticDisplayMode(ballisticDisplayMode);
                StoreSessionState();
            }

            if (IsSingleBurstMode)
            {
                if (Widgets.ButtonText(new Rect(width * 0.3f + 150f, row.y, 54f, row.height), "<"))
                {
                    ballisticSampleIndex = Mathf.Max(0, ballisticSampleIndex - 1);
                    StoreSessionState();
                }
                Widgets.Label(new Rect(width * 0.3f + 212f, row.y + 4f, 150f, row.height), "CEHCC_BallisticSample".Translate(ballisticSampleIndex + 1, distribution.SampleCount));
                if (Widgets.ButtonText(new Rect(width * 0.3f + 370f, row.y, 54f, row.height), ">"))
                {
                    ballisticSampleIndex = Mathf.Min(Mathf.Max(0, distribution.SampleCount - 1), ballisticSampleIndex + 1);
                    StoreSessionState();
                }
            }
            y += 32f;
        }

        private string BallisticHintKey()
        {
            switch (ballisticDisplayMode)
            {
                case BallisticDisplayMode.ImpactHeatmap:
                    return "CEHCC_AnalysisBallisticImpactHint";
                case BallisticDisplayMode.TrajectoryDensity:
                    return "CEHCC_AnalysisBallisticDensityHint";
                case BallisticDisplayMode.SingleBurst:
                    return "CEHCC_AnalysisBallisticSingleHint";
                default:
                    return "CEHCC_AnalysisBallisticHint";
            }
        }

        private string BallisticDisplayModeLabel()
        {
            switch (ballisticDisplayMode)
            {
                case BallisticDisplayMode.ImpactHeatmap:
                    return "CEHCC_BallisticViewImpactHeat".Translate();
                case BallisticDisplayMode.TrajectoryDensity:
                    return "CEHCC_BallisticViewTrajectoryHeat".Translate();
                case BallisticDisplayMode.SingleBurst:
                    return "CEHCC_BallisticViewSingle".Translate();
                default:
                    return "CEHCC_BallisticViewCloud".Translate();
            }
        }

        private static BallisticDisplayMode NextBallisticDisplayMode(BallisticDisplayMode mode)
        {
            switch (mode)
            {
                case BallisticDisplayMode.ImpactHeatmap:
                    return BallisticDisplayMode.TrajectoryDensity;
                case BallisticDisplayMode.TrajectoryDensity:
                    return BallisticDisplayMode.TrajectoryCloud;
                case BallisticDisplayMode.TrajectoryCloud:
                    return BallisticDisplayMode.SingleBurst;
                default:
                    return BallisticDisplayMode.ImpactHeatmap;
            }
        }

        private bool IsSingleBurstMode => ballisticDisplayMode == BallisticDisplayMode.SingleBurst;

        private void DrawBallisticLegend(ref float y, float width, int burstShots)
        {
            if (ballisticDisplayMode == BallisticDisplayMode.ImpactHeatmap || ballisticDisplayMode == BallisticDisplayMode.TrajectoryDensity)
            {
                float heatLegendX = 0f;
                DrawLegendItem(new Rect(heatLegendX, y + 4f, 12f, 12f), HeatColor(0.25f), "CEHCC_BallisticLegendSparse".Translate());
                heatLegendX += Mathf.Min(140f, width * 0.24f);
                DrawLegendItem(new Rect(heatLegendX, y + 4f, 12f, 12f), HeatColor(0.95f), "CEHCC_BallisticLegendDense".Translate());
                y += 24f;
            }
            else
            {
                float x = 0f;
                DrawLegendItem(new Rect(x, y + 4f, 12f, 12f), BallisticShotColor(0, burstShots, 0.85f), "CEHCC_BallisticLegendEarly".Translate());
                x += Mathf.Min(140f, width * 0.24f);
                DrawLegendItem(new Rect(x, y + 4f, 12f, 12f), BallisticShotColor(Mathf.Max(0, burstShots / 2), burstShots, 0.85f), "CEHCC_BallisticLegendMiddle".Translate());
                x += Mathf.Min(140f, width * 0.24f);
                DrawLegendItem(new Rect(x, y + 4f, 12f, 12f), BallisticShotColor(Mathf.Max(0, burstShots - 1), burstShots, 0.85f), "CEHCC_BallisticLegendLate".Translate());
                y += 24f;
            }

            if (HitChanceObstacleBridge.TryGetFor(input, out LineOfFireObstacleContext obstacleContext))
            {
                DrawLegendItem(new Rect(0f, y + 4f, 12f, 12f), SideObstacleFillColor, "CEHCC_BallisticLegendObstacle".Translate(obstacleContext.ValidObstacleCount(input.DistanceCells)));
                y += 24f;
            }
        }

        private void DrawTopDistributionPlot(Rect rect, BallisticDistribution distribution)
        {
            Widgets.DrawMenuSection(rect);
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), "CEHCC_BallisticTopView".Translate());
            Rect plot = new Rect(rect.x + 28f, rect.y + 34f, rect.width - 56f, rect.height - 48f);

            float distance = Mathf.Max(0.1f, distribution.DistanceCells);
            float distanceScale = TopPlotDistanceScale(plot, distance);
            float horizontalExtent = TopPlotHorizontalExtent(distribution);
            float horizontalScale = plot.width * 0.48f / horizontalExtent;
            float originX = plot.center.x;
            float originY = plot.yMax - 8f;
            float targetY = originY - distance * distanceScale;
            DrawTopPlotGrid(plot, distance, distanceScale, horizontalScale, horizontalExtent, originX, originY);
            Widgets.DrawBoxSolid(new Rect(originX - 1f, plot.y, 2f, plot.height), new Color(0.62f, 0.62f, 0.62f, 0.35f));
            Widgets.DrawBoxSolid(new Rect(plot.x, targetY, plot.width, 1f), new Color(0.72f, 0.72f, 0.72f, 0.45f));

            GUI.DrawTexture(plot, GetTopBallisticTexture(plot, distribution, originX - plot.x, originY - plot.y, targetY - plot.y, horizontalScale));
            DrawTopObstacleOverlay(plot, distribution, originX, originY, distanceScale, horizontalScale);

            float targetHalf = distribution.TargetWidthCells * 0.5f * horizontalScale;
            DrawTargetOverlay(new Rect(originX - targetHalf, targetY - 5f, targetHalf * 2f, 10f));
            DrawSingleBurstTopMarkers(plot, distribution, originX, targetY, horizontalScale);

            Widgets.DrawBoxSolid(new Rect(originX - 3f, originY - 3f, 6f, 6f), new Color(0.92f, 0.92f, 0.92f));
            Widgets.Label(new Rect(plot.x, plot.yMax - 20f, 120f, 20f), "CEHCC_BallisticShooter".Translate());
            Widgets.Label(new Rect(plot.xMax - 150f, plot.y + 2f, 150f, 20f), "CEHCC_BallisticTargetPlane".Translate(distribution.DistanceCells.ToString("0.#")));
        }

        private void DrawSideDistributionPlot(Rect rect, BallisticDistribution distribution)
        {
            Widgets.DrawMenuSection(rect);
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), "CEHCC_BallisticSideView".Translate());
            Rect plot = new Rect(rect.x + 34f, rect.y + 34f, rect.width - 62f, rect.height - 48f);

            float minY = distribution.VerticalMinCells;
            float maxY = Mathf.Max(minY + 0.1f, distribution.VerticalMaxCells);
            float gravity = HitChanceCalculator.CalculateEffectiveGravityPerWidth(input);
            float speed = Mathf.Max(1f, input.ShotSpeedCellsPerSecond);
            float distance = Mathf.Max(0.1f, distribution.DistanceCells);
            DrawSidePlotGrid(plot, distance, minY, maxY);

            float muzzleY = HeightToPlotY(plot, distribution.ShotHeightCells, minY, maxY);
            Widgets.DrawBoxSolid(new Rect(plot.x, muzzleY, plot.width, 1f), new Color(0.72f, 0.72f, 0.72f, 0.35f));
            float aimY = HeightToPlotY(plot, distribution.TargetAimHeightCells, minY, maxY);
            Widgets.DrawBoxSolid(new Rect(plot.x, aimY, plot.width, 1f), new Color(0.72f, 0.64f, 0.36f, 0.35f));

            GUI.DrawTexture(plot, GetSideBallisticTexture(plot, distribution, speed, gravity, distance, minY, maxY));
            DrawSideObstacleOverlay(plot, distribution, minY, maxY);

            float targetX = plot.xMax - 4f;
            float targetTop = HeightToPlotY(plot, distribution.TargetHeightCells, minY, maxY);
            float targetBottom = HeightToPlotY(plot, 0f, minY, maxY);
            DrawTargetOverlay(new Rect(targetX - 6f, targetTop, 12f, Mathf.Max(2f, targetBottom - targetTop)));
            DrawSingleBurstSideMarkers(plot, distribution, targetX, minY, maxY);

            Widgets.Label(new Rect(plot.x, plot.yMax - 20f, 140f, 20f), "CEHCC_BallisticMuzzleHeight".Translate(distribution.ShotHeightCells.ToString("0.##")));
            Widgets.Label(new Rect(plot.xMax - 130f, plot.y + 2f, 130f, 20f), "CEHCC_BallisticTargetHeight".Translate(distribution.TargetHeightCells.ToString("0.##")));
        }

        private void DrawTopObstacleOverlay(Rect plot, BallisticDistribution distribution, float originX, float originY, float distanceScale, float horizontalScale)
        {
            if (!HitChanceObstacleBridge.TryGetFor(input, out LineOfFireObstacleContext obstacleContext))
            {
                return;
            }

            float distance = Mathf.Max(0.1f, distribution.DistanceCells);
            foreach (LineOfFireObstacle obstacle in obstacleContext.Obstacles)
            {
                if (obstacle == null || !obstacle.IsValid(distance))
                {
                    continue;
                }

                float minDistance = Mathf.Max(0f, obstacle.DistanceCells - obstacle.HalfDepthCells);
                float maxDistance = Mathf.Min(distance, obstacle.DistanceCells + obstacle.HalfDepthCells);
                float yTop = originY - maxDistance * distanceScale;
                float yBottom = originY - minDistance * distanceScale;
                float xMin = originX + (obstacle.CenterOffsetCells - obstacle.HalfWidthCells) * horizontalScale;
                float xMax = originX + (obstacle.CenterOffsetCells + obstacle.HalfWidthCells) * horizontalScale;
                Rect rect = ClampRectToPlot(new Rect(xMin, yTop, xMax - xMin, yBottom - yTop), plot, 4f, 4f);
                DrawObstacleOverlay(rect, obstacle, TopObstacleFillColor, TopObstacleBorderColor);
            }
        }

        private void DrawSideObstacleOverlay(Rect plot, BallisticDistribution distribution, float minY, float maxY)
        {
            if (!HitChanceObstacleBridge.TryGetFor(input, out LineOfFireObstacleContext obstacleContext))
            {
                return;
            }

            float distance = Mathf.Max(0.1f, distribution.DistanceCells);
            foreach (LineOfFireObstacle obstacle in obstacleContext.Obstacles)
            {
                if (obstacle == null || !obstacle.IsValid(distance))
                {
                    continue;
                }

                float minDistance = Mathf.Max(0f, obstacle.DistanceCells - obstacle.HalfDepthCells);
                float maxDistance = Mathf.Min(distance, obstacle.DistanceCells + obstacle.HalfDepthCells);
                float xMin = plot.x + minDistance / distance * plot.width;
                float xMax = plot.x + maxDistance / distance * plot.width;
                float yTop = HeightToPlotY(plot, obstacle.MaxHeightCells, minY, maxY);
                float yBottom = HeightToPlotY(plot, obstacle.MinHeightCells, minY, maxY);
                Rect rect = ClampRectToPlot(new Rect(xMin, yTop, xMax - xMin, yBottom - yTop), plot, 4f, 4f);
                DrawObstacleOverlay(rect, obstacle, SideObstacleFillColor, SideObstacleBorderColor);
            }
        }

        private static Rect ClampRectToPlot(Rect rect, Rect plot, float minWidth, float minHeight)
        {
            if (rect.width < minWidth)
            {
                rect.x -= (minWidth - rect.width) * 0.5f;
                rect.width = minWidth;
            }
            if (rect.height < minHeight)
            {
                rect.y -= (minHeight - rect.height) * 0.5f;
                rect.height = minHeight;
            }

            float xMin = Mathf.Clamp(rect.xMin, plot.xMin, plot.xMax);
            float xMax = Mathf.Clamp(rect.xMax, plot.xMin, plot.xMax);
            float yMin = Mathf.Clamp(rect.yMin, plot.yMin, plot.yMax);
            float yMax = Mathf.Clamp(rect.yMax, plot.yMin, plot.yMax);
            if (xMax - xMin < minWidth)
            {
                float center = Mathf.Clamp((xMin + xMax) * 0.5f, plot.xMin + minWidth * 0.5f, plot.xMax - minWidth * 0.5f);
                xMin = center - minWidth * 0.5f;
                xMax = center + minWidth * 0.5f;
            }
            if (yMax - yMin < minHeight)
            {
                float center = Mathf.Clamp((yMin + yMax) * 0.5f, plot.yMin + minHeight * 0.5f, plot.yMax - minHeight * 0.5f);
                yMin = center - minHeight * 0.5f;
                yMax = center + minHeight * 0.5f;
            }

            return new Rect(xMin, yMin, Mathf.Max(1f, xMax - xMin), Mathf.Max(1f, yMax - yMin));
        }

        private static void DrawObstacleOverlay(Rect rect, LineOfFireObstacle obstacle, Color fillColor, Color borderColor)
        {
            Widgets.DrawBoxSolid(rect, fillColor);
            GUI.color = borderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(rect, "CEHCC_BallisticObstacleTip".Translate(
                obstacle.Label ?? "",
                obstacle.DistanceCells.ToString("0.##"),
                (obstacle.HalfWidthCells * 2f).ToString("0.##"),
                obstacle.MinHeightCells.ToString("0.##"),
                obstacle.MaxHeightCells.ToString("0.##")));
        }

        private void DrawSingleBurstTopMarkers(Rect plot, BallisticDistribution distribution, float originX, float targetY, float scale)
        {
            if (!IsSingleBurstMode)
            {
                return;
            }

            foreach (BallisticDistributionPoint point in SingleBurstPoints(distribution))
            {
                float x = originX + point.HorizontalMissCells * scale;
                Color color = BallisticShotColor(point.ShotIndex, distribution.BurstShots, 0.95f);
                Widgets.DrawBoxSolid(new Rect(x - 3f, targetY - 3f, 6f, 6f), color);
                if (ShouldLabelShot(point.ShotIndex, distribution.BurstShots))
                {
                    float labelX = Mathf.Clamp(x + (point.ShotIndex % 2 == 0 ? 5f : -35f), plot.x, plot.xMax - 34f);
                    float labelY = Mathf.Clamp(targetY + 8f + (point.ShotIndex % 3) * 13f, plot.y + 14f, plot.yMax - 18f);
                    DrawMarkerLabel(new Rect(labelX, labelY, 34f, 18f), "#" + (point.ShotIndex + 1), color);
                }
            }
        }

        private void DrawSingleBurstSideMarkers(Rect plot, BallisticDistribution distribution, float targetX, float minY, float maxY)
        {
            if (!IsSingleBurstMode)
            {
                return;
            }

            foreach (BallisticDistributionPoint point in SingleBurstPoints(distribution))
            {
                float y = HeightToPlotY(plot, point.HeightAtTargetCells, minY, maxY);
                Color color = BallisticShotColor(point.ShotIndex, distribution.BurstShots, 0.95f);
                Widgets.DrawBoxSolid(new Rect(targetX - 3f, y - 3f, 6f, 6f), color);
                if (ShouldLabelShot(point.ShotIndex, distribution.BurstShots))
                {
                    float labelX = Mathf.Clamp(targetX - 48f - (point.ShotIndex % 2) * 22f, plot.x, plot.xMax - 44f);
                    float labelY = Mathf.Clamp(y - 8f + (point.ShotIndex % 3 - 1) * 12f, plot.y, plot.yMax - 18f);
                    DrawMarkerLabel(new Rect(labelX, labelY, 44f, 18f), "#" + (point.ShotIndex + 1), color);
                }
            }
        }

        private IEnumerable<BallisticDistributionPoint> SingleBurstPoints(BallisticDistribution distribution)
        {
            int start = Mathf.Clamp(ballisticSampleIndex, 0, Mathf.Max(0, distribution.SampleCount - 1)) * distribution.BurstShots;
            int end = Mathf.Min(distribution.Points.Count, start + distribution.BurstShots);
            for (int i = start; i < end; i++)
            {
                yield return distribution.Points[i];
            }
        }

        private static bool ShouldLabelShot(int shotIndex, int burstShots)
        {
            return burstShots <= 12 || shotIndex == 0 || shotIndex == burstShots - 1 || (shotIndex + 1) % 5 == 0;
        }

        private static void DrawMarkerLabel(Rect rect, string label, Color color)
        {
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Widgets.DrawBoxSolid(rect, new Color(0.02f, 0.025f, 0.028f, 0.78f));
            GUI.color = color;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Font = oldFont;
        }

        private Texture2D GetTopBallisticTexture(Rect plot, BallisticDistribution distribution, float originX, float originY, float targetY, float cellScale)
        {
            // 弹道图先画到 Texture2D，再由 GUI.DrawTexture 贴上去。
            // 这样滚动/鼠标悬停时不会每帧重新跑密度图。
            int width = Mathf.Max(1, Mathf.RoundToInt(plot.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(plot.height));
            string key = BallisticTextureKey("top", width, height);
            if (cachedTopBallisticTexture != null && cachedTopBallisticTextureKey == key)
            {
                return cachedTopBallisticTexture;
            }

            cachedTopBallisticTexture = CreateBallisticTexture(cachedTopBallisticTexture, width, height);
            cachedTopBallisticTextureKey = key;
            Color[] pixels = TransparentPixels(width, height);
            float scaleX = width / Mathf.Max(1f, plot.width);
            float scaleY = height / Mathf.Max(1f, plot.height);
            Vector2 origin = new Vector2(originX * scaleX, originY * scaleY);
            float target = targetY * scaleY;
            if (IsSingleBurstMode)
            {
                int start = Mathf.Clamp(ballisticSampleIndex, 0, Mathf.Max(0, distribution.SampleCount - 1)) * distribution.BurstShots;
                int end = Mathf.Min(distribution.Points.Count, start + distribution.BurstShots);
                for (int i = start; i < end; i++)
                {
                    BallisticDistributionPoint point = distribution.Points[i];
                    float x = (originX + point.HorizontalMissCells * cellScale) * scaleX;
                    Vector2 impact = new Vector2(x, target);
                    Color color = BallisticShotColor(point.ShotIndex, distribution.BurstShots, 0.72f);
                    DrawLinePixels(pixels, width, height, origin, impact, color, 2f);
                    DrawPointPixels(pixels, width, height, impact, color, 3);
                }
            }
            else if (ballisticDisplayMode == BallisticDisplayMode.ImpactHeatmap)
            {
                DrawTopImpactHeatmapPixels(pixels, width, height, distribution, originX, target, cellScale, scaleX);
            }
            else if (ballisticDisplayMode == BallisticDisplayMode.TrajectoryDensity)
            {
                DrawTopTrajectoryDensityPixels(pixels, width, height, distribution, origin, originX, target, cellScale, scaleX);
            }
            else
            {
                int perShotBudget = Mathf.Max(1, Mathf.CeilToInt(260f / Mathf.Max(1, distribution.BurstShots)));
                int sampleStep = Mathf.Max(1, Mathf.CeilToInt(distribution.SampleCount / (float)perShotBudget));
                for (int shot = 0; shot < distribution.BurstShots; shot++)
                {
                    for (int sample = 0; sample < distribution.SampleCount; sample += sampleStep)
                    {
                        int index = sample * distribution.BurstShots + shot;
                        if (index < 0 || index >= distribution.Points.Count)
                        {
                            continue;
                        }
                        BallisticDistributionPoint point = distribution.Points[index];
                        float x = (originX + point.HorizontalMissCells * cellScale) * scaleX;
                        Color color = BallisticShotColor(point.ShotIndex, distribution.BurstShots, point.Hit ? 0.2f : 0.09f);
                        DrawLinePixels(pixels, width, height, origin, new Vector2(x, target), color, 1f);
                    }
                }
            }
            cachedTopBallisticTexture.SetPixels(pixels);
            cachedTopBallisticTexture.Apply(false);
            return cachedTopBallisticTexture;
        }

        private Texture2D GetSideBallisticTexture(Rect plot, BallisticDistribution distribution, float speed, float gravity, float distance, float minY, float maxY)
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(plot.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(plot.height));
            string key = BallisticTextureKey("side", width, height);
            if (cachedSideBallisticTexture != null && cachedSideBallisticTextureKey == key)
            {
                return cachedSideBallisticTexture;
            }

            cachedSideBallisticTexture = CreateBallisticTexture(cachedSideBallisticTexture, width, height);
            cachedSideBallisticTextureKey = key;
            Color[] pixels = TransparentPixels(width, height);
            Rect localPlot = new Rect(0f, 0f, width, height);
            if (IsSingleBurstMode)
            {
                int start = Mathf.Clamp(ballisticSampleIndex, 0, Mathf.Max(0, distribution.SampleCount - 1)) * distribution.BurstShots;
                int end = Mathf.Min(distribution.Points.Count, start + distribution.BurstShots);
                for (int i = start; i < end; i++)
                {
                    BallisticDistributionPoint point = distribution.Points[i];
                    Color color = BallisticShotColor(point.ShotIndex, distribution.BurstShots, 0.74f);
                    Vector2 impact = DrawBallisticCurvePixels(pixels, width, height, localPlot, distribution.ShotHeightCells, point.FinalShotAngleRadians, speed, gravity, distance, minY, maxY, color, 2f);
                    DrawPointPixels(pixels, width, height, impact, color, 3);
                }
            }
            else if (ballisticDisplayMode == BallisticDisplayMode.ImpactHeatmap)
            {
                DrawSideImpactHeatmapPixels(pixels, width, height, distribution, localPlot, minY, maxY);
            }
            else if (ballisticDisplayMode == BallisticDisplayMode.TrajectoryDensity)
            {
                DrawSideTrajectoryDensityPixels(pixels, width, height, distribution, localPlot, speed, gravity, distance, minY, maxY);
            }
            else
            {
                int perShotBudget = Mathf.Max(1, Mathf.CeilToInt(180f / Mathf.Max(1, distribution.BurstShots)));
                int sampleStep = Mathf.Max(1, Mathf.CeilToInt(distribution.SampleCount / (float)perShotBudget));
                for (int shot = 0; shot < distribution.BurstShots; shot++)
                {
                    for (int sample = 0; sample < distribution.SampleCount; sample += sampleStep)
                    {
                        int index = sample * distribution.BurstShots + shot;
                        if (index < 0 || index >= distribution.Points.Count)
                        {
                            continue;
                        }
                        BallisticDistributionPoint point = distribution.Points[index];
                        Color color = BallisticShotColor(point.ShotIndex, distribution.BurstShots, point.Hit ? 0.22f : 0.1f);
                        DrawBallisticCurvePixels(pixels, width, height, localPlot, distribution.ShotHeightCells, point.FinalShotAngleRadians, speed, gravity, distance, minY, maxY, color, 1f);
                    }
                }
            }
            cachedSideBallisticTexture.SetPixels(pixels);
            cachedSideBallisticTexture.Apply(false);
            return cachedSideBallisticTexture;
        }

        private static void DrawTopImpactHeatmapPixels(Color[] pixels, int width, int height, BallisticDistribution distribution, float originX, float targetY, float cellScale, float scaleX)
        {
            float[] density = new float[pixels.Length];
            foreach (BallisticDistributionPoint point in distribution.Points)
            {
                float x = (originX + point.HorizontalMissCells * cellScale) * scaleX;
                AddDensityEllipse(density, width, height, Mathf.RoundToInt(x), Mathf.RoundToInt(targetY), 5, 7, 1f);
            }
            PaintDensityPixels(density, pixels, width, height, 0.38f);
        }

        private static void DrawSideImpactHeatmapPixels(Color[] pixels, int width, int height, BallisticDistribution distribution, Rect plot, float minY, float maxY)
        {
            float[] density = new float[pixels.Length];
            int targetX = Mathf.RoundToInt(plot.xMax - 4f);
            foreach (BallisticDistributionPoint point in distribution.Points)
            {
                float y = HeightToPlotY(plot, point.HeightAtTargetCells, minY, maxY);
                AddDensityEllipse(density, width, height, targetX, Mathf.RoundToInt(y), 7, 4, 1f);
            }
            PaintDensityPixels(density, pixels, width, height, 0.38f);
        }

        private static void DrawTopTrajectoryDensityPixels(Color[] pixels, int width, int height, BallisticDistribution distribution, Vector2 origin, float originX, float targetY, float cellScale, float scaleX)
        {
            float[] density = new float[pixels.Length];
            foreach (int index in BallisticPointIndices(distribution, 900))
            {
                BallisticDistributionPoint point = distribution.Points[index];
                float x = (originX + point.HorizontalMissCells * cellScale) * scaleX;
                AddLineDensity(density, width, height, origin, new Vector2(x, targetY), 1f);
            }
            PaintDensityPixels(density, pixels, width, height, 0.14f);
        }

        private static void DrawSideTrajectoryDensityPixels(Color[] pixels, int width, int height, BallisticDistribution distribution, Rect plot, float speed, float gravity, float distance, float minY, float maxY)
        {
            float[] density = new float[pixels.Length];
            foreach (int index in BallisticPointIndices(distribution, 700))
            {
                BallisticDistributionPoint point = distribution.Points[index];
                AddBallisticCurveDensity(density, width, height, plot, distribution.ShotHeightCells, point.FinalShotAngleRadians, speed, gravity, distance, minY, maxY, 1f);
            }
            PaintDensityPixels(density, pixels, width, height, 0.14f);
        }

        private static IEnumerable<int> BallisticPointIndices(BallisticDistribution distribution, int maxDrawn)
        {
            int burstShots = Mathf.Max(1, distribution.BurstShots);
            int sampleCount = Mathf.Max(1, distribution.SampleCount);
            int perShotBudget = Mathf.Max(1, Mathf.CeilToInt(maxDrawn / (float)burstShots));
            int sampleStep = Mathf.Max(1, Mathf.CeilToInt(sampleCount / (float)perShotBudget));
            for (int shot = 0; shot < burstShots; shot++)
            {
                for (int sample = 0; sample < sampleCount; sample += sampleStep)
                {
                    int index = sample * burstShots + shot;
                    if (index >= 0 && index < distribution.Points.Count)
                    {
                        yield return index;
                    }
                }
            }
        }

        private string BallisticTextureKey(string view, int width, int height)
        {
            // key 必须包含输入签名、视图类型、尺寸和显示模式；否则拖动窗口或切换模式会复用旧图。
            return (cachedSignature ?? BuildInputSignature()) + "|" + view + "|" + width + "x" + height + "|" + (int)ballisticDisplayMode + "|" + ballisticSampleIndex;
        }

        private static Texture2D CreateBallisticTexture(Texture2D current, int width, int height)
        {
            if (current != null && current.width == width && current.height == height)
            {
                return current;
            }

            if (current != null)
            {
                UnityEngine.Object.Destroy(current);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private void DestroyCachedBallisticTextures()
        {
            // Texture2D 是 Unity 原生资源，不是普通托管数组。
            // 替换或关闭窗口时必须显式 Destroy，避免长时间游戏后积累显存。
            if (cachedTopBallisticTexture != null)
            {
                UnityEngine.Object.Destroy(cachedTopBallisticTexture);
                cachedTopBallisticTexture = null;
            }
            if (cachedSideBallisticTexture != null)
            {
                UnityEngine.Object.Destroy(cachedSideBallisticTexture);
                cachedSideBallisticTexture = null;
            }
            cachedTopBallisticTextureKey = null;
            cachedSideBallisticTextureKey = null;
        }

        private static Color[] TransparentPixels(int width, int height)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }
            return pixels;
        }

        private static void DrawTargetOverlay(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.53f, 0.9f, 0.58f, 0.22f));
            Rect outer = new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f);
            GUI.color = new Color(0.01f, 0.015f, 0.012f, 0.95f);
            Widgets.DrawBox(outer, 1);
            GUI.color = new Color(0.82f, 1f, 0.84f, 1f);
            Widgets.DrawBox(rect, 2);
            GUI.color = Color.white;
        }

        private static Vector2 DrawBallisticCurvePixels(Color[] pixels, int width, int height, Rect plot, float shotHeight, float angle, float speed, float gravity, float distance, float minHeight, float maxHeight, Color color, float lineWidth)
        {
            // 侧视图画的是近似轨迹云，不做真实碰撞检测。
            // 命中/近失判断仍以 HitChanceCalculator 的目标面采样为准。
            float cos = Mathf.Max(0.001f, Mathf.Cos(angle));
            Vector2 previous = new Vector2(plot.x, HeightToPlotY(plot, shotHeight, minHeight, maxHeight));
            const int segments = 8;
            Vector2 current = previous;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float xDistance = distance * t;
                float shotY = shotHeight + xDistance * Mathf.Tan(angle) - gravity * xDistance * xDistance / (2f * speed * speed * cos * cos);
                current = new Vector2(plot.x + t * plot.width, HeightToPlotY(plot, shotY, minHeight, maxHeight));
                DrawLinePixels(pixels, width, height, previous, current, color, lineWidth);
                previous = current;
            }
            return current;
        }

        private static void AddBallisticCurveDensity(float[] density, int width, int height, Rect plot, float shotHeight, float angle, float speed, float gravity, float distance, float minHeight, float maxHeight, float weight)
        {
            float cos = Mathf.Max(0.001f, Mathf.Cos(angle));
            Vector2 previous = new Vector2(plot.x, HeightToPlotY(plot, shotHeight, minHeight, maxHeight));
            const int segments = 8;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float xDistance = distance * t;
                float shotY = shotHeight + xDistance * Mathf.Tan(angle) - gravity * xDistance * xDistance / (2f * speed * speed * cos * cos);
                Vector2 current = new Vector2(plot.x + t * plot.width, HeightToPlotY(plot, shotY, minHeight, maxHeight));
                AddLineDensity(density, width, height, previous, current, weight);
                previous = current;
            }
        }

        private static void AddLineDensity(float[] density, int width, int height, Vector2 start, Vector2 end, float weight)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            int segments = Mathf.Clamp(Mathf.CeilToInt(length / 2f), 1, 512);
            for (int i = 0; i <= segments; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)segments);
                AddDensityEllipse(density, width, height, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), 1, 1, weight);
            }
        }

        private static void AddDensityEllipse(float[] density, int width, int height, int centerX, int centerY, int radiusX, int radiusY, float weight)
        {
            radiusX = Mathf.Max(1, radiusX);
            radiusY = Mathf.Max(1, radiusY);
            for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height)
                    {
                        continue;
                    }
                    float nx = (x - centerX) / (float)radiusX;
                    float ny = (y - centerY) / (float)radiusY;
                    float dist = nx * nx + ny * ny;
                    if (dist > 1f)
                    {
                        continue;
                    }
                    int index = (height - 1 - y) * width + x;
                    density[index] += weight * (1f - dist * 0.55f);
                }
            }
        }

        private static void PaintDensityPixels(float[] density, Color[] pixels, int width, int height, float capFraction)
        {
            float max = 0f;
            for (int i = 0; i < density.Length; i++)
            {
                if (density[i] > max)
                {
                    max = density[i];
                }
            }
            if (max <= 0f)
            {
                return;
            }

            float cap = Mathf.Max(1f, max * capFraction);
            for (int i = 0; i < density.Length; i++)
            {
                if (density[i] <= 0f)
                {
                    continue;
                }
                float normalized = Mathf.Pow(Mathf.Clamp01(density[i] / cap), 0.42f);
                pixels[i] = HeatColor(normalized);
            }
        }

        private static Color HeatColor(float value)
        {
            value = Mathf.Clamp01(value);
            Color low = new Color(0.06f, 0.25f, 0.68f, 0.2f);
            Color mid = new Color(0.08f, 0.78f, 0.9f, 0.68f);
            Color high = new Color(1f, 0.86f, 0.18f, 0.95f);
            Color peak = new Color(1f, 0.98f, 0.82f, 1f);
            if (value < 0.45f)
            {
                return Color.Lerp(low, mid, value / 0.45f);
            }
            if (value < 0.82f)
            {
                return Color.Lerp(mid, high, (value - 0.45f) / 0.37f);
            }
            return Color.Lerp(high, peak, (value - 0.82f) / 0.18f);
        }

        private static void DrawLinePixels(Color[] pixels, int width, int height, Vector2 start, Vector2 end, Color color, float lineWidth)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            int segments = Mathf.Clamp(Mathf.CeilToInt(length / 2f), 1, 512);
            int radius = Mathf.Max(0, Mathf.RoundToInt(lineWidth * 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)segments);
                int centerX = Mathf.RoundToInt(point.x);
                int centerY = Mathf.RoundToInt(point.y);
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    for (int x = centerX - radius; x <= centerX + radius; x++)
                    {
                        BlendPixel(pixels, width, height, x, y, color);
                    }
                }
            }
        }

        private static void BlendPixel(Color[] pixels, int width, int height, int x, int y, Color source)
        {
            if (x < 0 || x >= width || y < 0 || y >= height || source.a <= 0f)
            {
                return;
            }

            int index = (height - 1 - y) * width + x;
            Color dest = pixels[index];
            float outAlpha = source.a + dest.a * (1f - source.a);
            if (outAlpha <= 0f)
            {
                return;
            }
            float keep = dest.a * (1f - source.a);
            pixels[index] = new Color(
                (source.r * source.a + dest.r * keep) / outAlpha,
                (source.g * source.a + dest.g * keep) / outAlpha,
                (source.b * source.a + dest.b * keep) / outAlpha,
                outAlpha);
        }

        private static void DrawPointPixels(Color[] pixels, int width, int height, Vector2 point, Color color, int radius)
        {
            int centerX = Mathf.RoundToInt(point.x);
            int centerY = Mathf.RoundToInt(point.y);
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        BlendPixel(pixels, width, height, x, y, color);
                    }
                }
            }
        }

        private static void DrawPlotGrid(Rect plot)
        {
            Widgets.DrawBoxSolid(plot, new Color(0.08f, 0.09f, 0.1f));
            for (int i = 1; i < 4; i++)
            {
                float x = plot.x + plot.width * i / 4f;
                float y = plot.y + plot.height * i / 4f;
                Widgets.DrawBoxSolid(new Rect(x, plot.y, 1f, plot.height), new Color(0.34f, 0.34f, 0.34f, 0.28f));
                Widgets.DrawBoxSolid(new Rect(plot.x, y, plot.width, 1f), new Color(0.34f, 0.34f, 0.34f, 0.28f));
            }
            Widgets.DrawBox(plot);
        }

        private static float TopPlotDistanceScale(Rect plot, float distanceCells)
        {
            return (plot.height - 16f) / Mathf.Max(0.1f, distanceCells);
        }

        private static float TopPlotHorizontalExtent(BallisticDistribution distribution)
        {
            float targetReadableHalf = Mathf.Max(0.45f, distribution.TargetWidthCells * 1.8f);
            float spreadReadableHalf = Mathf.Max(1f, distribution.HorizontalExtentCells * 1.25f);
            return Mathf.Max(targetReadableHalf, spreadReadableHalf, 2.5f);
        }

        private static void DrawTopPlotGrid(Rect plot, float distanceCells, float distanceScale, float horizontalScale, float visibleHalfWidth, float originX, float originY)
        {
            Widgets.DrawBoxSolid(plot, new Color(0.08f, 0.09f, 0.1f));
            Color major = new Color(0.42f, 0.42f, 0.42f, 0.38f);
            Color minor = new Color(0.34f, 0.34f, 0.34f, 0.24f);
            float xStep = NiceStep(visibleHalfWidth / 3f);
            float xStart = Mathf.Ceil(-visibleHalfWidth / xStep) * xStep;
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            for (float xCell = xStart; xCell <= visibleHalfWidth + 0.001f; xCell += xStep)
            {
                float x = originX + xCell * horizontalScale;
                if (x < plot.x || x > plot.xMax)
                {
                    continue;
                }
                Widgets.DrawBoxSolid(new Rect(x, plot.y, 1f, plot.height), Mathf.Abs(xCell) < 0.001f ? major : minor);
                if (Mathf.Abs(xCell) > 0.001f)
                {
                    Widgets.Label(new Rect(x - 28f, plot.yMax - 18f, 56f, 18f), xCell.ToString("0.#"));
                }
            }

            float distanceStep = NiceStep(distanceCells / 4f);
            for (float d = 0f; d <= distanceCells + 0.001f; d += distanceStep)
            {
                float y = originY - d * distanceScale;
                if (y < plot.y || y > plot.yMax)
                {
                    continue;
                }
                Widgets.DrawBoxSolid(new Rect(plot.x, y, plot.width, 1f), d <= 0.001f ? major : minor);
                Widgets.Label(new Rect(plot.x + 2f, y - 18f, 72f, 18f), d.ToString("0.#") + "格");
            }
            Widgets.DrawBox(plot);
            Text.Font = oldFont;
        }

        private static void DrawSidePlotGrid(Rect plot, float distanceCells, float minHeight, float maxHeight)
        {
            Widgets.DrawBoxSolid(plot, new Color(0.08f, 0.09f, 0.1f));
            Color major = new Color(0.42f, 0.42f, 0.42f, 0.38f);
            Color minor = new Color(0.34f, 0.34f, 0.34f, 0.24f);
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;

            float distanceStep = NiceStep(distanceCells / 4f);
            for (float d = 0f; d <= distanceCells + 0.001f; d += distanceStep)
            {
                float x = plot.x + Mathf.Clamp01(d / Mathf.Max(0.1f, distanceCells)) * plot.width;
                Widgets.DrawBoxSolid(new Rect(x, plot.y, 1f, plot.height), d <= 0.001f ? major : minor);
                if (d > 0.001f)
                {
                    Widgets.Label(new Rect(x - 24f, plot.yMax - 18f, 56f, 18f), d.ToString("0.#") + "格");
                }
            }

            float heightSpan = Mathf.Max(0.1f, maxHeight - minHeight);
            float heightStep = NiceStep(heightSpan / 4f);
            float heightStart = Mathf.Ceil(minHeight / heightStep) * heightStep;
            for (float h = heightStart; h <= maxHeight + 0.001f; h += heightStep)
            {
                float y = HeightToPlotY(plot, h, minHeight, maxHeight);
                Widgets.DrawBoxSolid(new Rect(plot.x, y, plot.width, 1f), Mathf.Abs(h) < 0.001f ? major : minor);
                Widgets.Label(new Rect(plot.x + 2f, y - 18f, 60f, 18f), h.ToString("0.#"));
            }

            Widgets.DrawBox(plot);
            Text.Font = oldFont;
        }

        private static float NiceStep(float desired)
        {
            desired = Mathf.Max(0.001f, desired);
            float exponent = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(desired)));
            float fraction = desired / exponent;
            if (fraction <= 1f)
            {
                return exponent;
            }
            if (fraction <= 2f)
            {
                return 2f * exponent;
            }
            if (fraction <= 5f)
            {
                return 5f * exponent;
            }
            return 10f * exponent;
        }

        private static float HeightToPlotY(Rect plot, float height, float minHeight, float maxHeight)
        {
            return plot.yMax - Mathf.Clamp01((height - minHeight) / Mathf.Max(0.001f, maxHeight - minHeight)) * plot.height;
        }

        private static Color BallisticShotColor(int shotIndex, int burstShots, float alpha)
        {
            float t = burstShots <= 1 ? 0f : Mathf.Clamp01(shotIndex / (float)(burstShots - 1));
            Color early = new Color(0.22f, 0.45f, 0.78f);
            Color middle = new Color(0.72f, 0.72f, 0.68f);
            Color late = new Color(0.82f, 0.22f, 0.16f);
            Color color = t < 0.5f
                ? Color.Lerp(early, middle, t / 0.5f)
                : Color.Lerp(middle, late, (t - 0.5f) / 0.5f);
            color.a = alpha;
            return color;
        }

        private void DrawTimelineAnalysis(ref float y, float width)
        {
            FireTimeline timeline = input.Timeline60SecondMode
                ? DpsCalculator.BuildFireTimeline(input, 60f)
                : DpsCalculator.BuildFireTimeline(input);
            string title = timeline.IsWindow
                ? "CEHCC_AnalysisTimelineWindowTitle".Translate(timeline.TotalShotsFired, timeline.TotalSeconds.ToString("0.##"))
                : "CEHCC_AnalysisTimelineTitle".Translate(timeline.MagazineShots, timeline.TotalSeconds.ToString("0.##"));
            DrawAnalysisHeader(ref y, width, title);
            Widgets.Label(new Rect(0f, y, width, 24f), "CEHCC_AnalysisTimelineSummary".Translate(
                input.FireWarmupSeconds.ToString("0.##"),
                DpsCalculator.EffectiveWarmupSeconds(input).ToString("0.##"),
                (timeline.AimedExtraTicks / 60f).ToString("0.##"),
                timeline.TicksBetweenShots,
                input.FireCooldownSeconds.ToString("0.##"),
                DpsCalculator.EffectiveReloadSeconds(input).ToString("0.##")));
            y += 26f;
            DrawTimelineLegend(ref y, width);
            DrawTimelineAxis(ref y, width, timeline);
            DrawTimelineBurstDetails(ref y, width, timeline);
        }

        private static void DrawTimelineLegend(ref float y, float width)
        {
            float x = 0f;
            DrawLegendItem(new Rect(x, y + 4f, 12f, 12f), TimelineWarmupColor(), "CEHCC_TimelineLegendWarmup".Translate());
            x += Mathf.Min(128f, width * 0.2f);
            DrawLegendItem(new Rect(x, y + 4f, 12f, 12f), TimelineSecondAimColor(), "CEHCC_TimelineLegendSecondAim".Translate());
            x += Mathf.Min(128f, width * 0.2f);
            DrawLegendItem(new Rect(x, y + 4f, 12f, 12f), TimelineFireColor(), "CEHCC_TimelineLegendFire".Translate());
            x += Mathf.Min(110f, width * 0.18f);
            DrawLegendItem(new Rect(x, y + 4f, 12f, 12f), TimelineCooldownColor(), "CEHCC_TimelineLegendCooldown".Translate());
            x += Mathf.Min(110f, width * 0.18f);
            DrawLegendItem(new Rect(x, y + 4f, 12f, 12f), TimelineReloadColor(), "CEHCC_TimelineLegendReload".Translate());
            y += 24f;
        }

        private static void DrawLegendItem(Rect swatch, Color color, string label)
        {
            Widgets.DrawBoxSolid(swatch, color);
            Widgets.Label(new Rect(swatch.xMax + 4f, swatch.y - 5f, 130f, 22f), label);
        }

        private static void DrawTimelineAxis(ref float y, float width, FireTimeline timeline)
        {
            Rect rect = new Rect(0f, y, width, 118f);
            Widgets.DrawMenuSection(rect);
            float labelWidth = 62f;
            float chartX = rect.x + labelWidth;
            float chartWidth = Mathf.Max(80f, rect.width - labelWidth - 12f);
            float total = Mathf.Max(0.001f, timeline.TotalSeconds);
            float warmupY = rect.y + 22f;
            float fireY = rect.y + 52f;
            float cooldownY = rect.y + 82f;

            Widgets.Label(new Rect(rect.x + 8f, warmupY - 7f, labelWidth - 10f, 22f), "CEHCC_TimelineRowAim".Translate());
            Widgets.Label(new Rect(rect.x + 8f, fireY - 7f, labelWidth - 10f, 22f), "CEHCC_TimelineRowFire".Translate());
            Widgets.Label(new Rect(rect.x + 8f, cooldownY - 7f, labelWidth - 10f, 22f), "CEHCC_TimelineRowCooldown".Translate());

            Widgets.DrawBoxSolid(new Rect(chartX, warmupY + 5f, chartWidth, 1f), new Color(0.36f, 0.36f, 0.36f));
            Widgets.DrawBoxSolid(new Rect(chartX, fireY + 5f, chartWidth, 1f), new Color(0.36f, 0.36f, 0.36f));
            Widgets.DrawBoxSolid(new Rect(chartX, cooldownY + 5f, chartWidth, 1f), new Color(0.36f, 0.36f, 0.36f));

            for (int i = 0; i < timeline.Bursts.Count; i++)
            {
                FireTimelineBurst burst = timeline.Bursts[i];
                DrawTimelineSegment(chartX, chartWidth, total, warmupY, burst.WarmupStartSeconds, burst.FirstAimEndSeconds, TimelineWarmupColor());
                DrawTimelineSegment(chartX, chartWidth, total, warmupY, burst.FirstAimEndSeconds, burst.SecondAimEndSeconds, TimelineSecondAimColor());
                DrawTimelineSegment(chartX, chartWidth, total, fireY, burst.FirstShotSeconds, burst.LastShotSeconds, TimelineFireColor());
                DrawTimelineSegment(chartX, chartWidth, total, cooldownY, burst.LastShotSeconds, burst.CooldownEndSeconds, TimelineCooldownColor());
                DrawShotTicks(chartX, chartWidth, total, fireY, burst, timeline.TicksBetweenShots);
                float burstX = TimelineX(chartX, chartWidth, total, burst.FirstShotSeconds);
                Widgets.DrawBoxSolid(new Rect(burstX - 1f, rect.y + 8f, 2f, 92f), new Color(0.82f, 0.82f, 0.82f, 0.45f));
            }
            for (int i = 0; i < timeline.Reloads.Count; i++)
            {
                FireTimelineReload reload = timeline.Reloads[i];
                DrawTimelineSegment(chartX, chartWidth, total, cooldownY, reload.StartSeconds, reload.EndSeconds, TimelineReloadColor());
            }

            Widgets.Label(new Rect(chartX, rect.y + 96f, 90f, 20f), "0s");
            string endLabel = timeline.IsWindow
                ? "CEHCC_TimelineWindowEnd".Translate(timeline.TotalSeconds.ToString("0.##"))
                : "CEHCC_TimelineMagazineEmpty".Translate(timeline.TotalSeconds.ToString("0.##"));
            Widgets.Label(new Rect(chartX + chartWidth - 150f, rect.y + 96f, 150f, 20f), endLabel);
            y += rect.height + 8f;
        }

        private static void DrawTimelineSegment(float chartX, float chartWidth, float total, float y, float start, float end, Color color)
        {
            if (end <= start)
            {
                return;
            }
            float x1 = TimelineX(chartX, chartWidth, total, Mathf.Max(0f, start));
            float x2 = TimelineX(chartX, chartWidth, total, Mathf.Min(total, end));
            Widgets.DrawBoxSolid(new Rect(x1, y, Mathf.Max(2f, x2 - x1), 10f), color);
        }

        private static void DrawShotTicks(float chartX, float chartWidth, float total, float y, FireTimelineBurst burst, int ticksBetweenShots)
        {
            float interval = ticksBetweenShots / 60f;
            for (int i = 0; i < burst.ShotCount; i++)
            {
                float shotTime = burst.FirstShotSeconds + i * interval;
                if (shotTime > total)
                {
                    break;
                }
                float x = TimelineX(chartX, chartWidth, total, shotTime);
                Widgets.DrawBoxSolid(new Rect(x - 0.5f, y - 6f, 1f, 22f), new Color(0.9f, 0.94f, 1f));
            }
        }

        private static float TimelineX(float chartX, float chartWidth, float total, float time)
        {
            return chartX + Mathf.Clamp01(time / total) * chartWidth;
        }

        private static void DrawTimelineBurstDetails(ref float y, float width, FireTimeline timeline)
        {
            int shown = Mathf.Min(5, timeline.Bursts.Count);
            for (int i = 0; i < shown; i++)
            {
                DrawTimelineBurstLine(ref y, width, timeline.Bursts[i], timeline.HasSecondAim);
            }
            if (timeline.Bursts.Count > shown)
            {
                Widgets.Label(new Rect(0f, y, width, 24f), "CEHCC_TimelineHiddenBursts".Translate(timeline.Bursts.Count - shown));
                y += 24f;
                DrawTimelineBurstLine(ref y, width, timeline.Bursts[timeline.Bursts.Count - 1], timeline.HasSecondAim);
            }
        }

        private static void DrawTimelineBurstLine(ref float y, float width, FireTimelineBurst burst, bool hasSecondAim)
        {
            string shotRange = burst.ShotCount == 1
                ? "#" + (burst.FirstShotIndex + 1)
                : "#" + (burst.FirstShotIndex + 1) + "-" + (burst.FirstShotIndex + burst.ShotCount);
            string label = hasSecondAim
                ? "CEHCC_TimelineBurstLineAimed".Translate(
                    burst.BurstIndex + 1,
                    shotRange,
                    burst.WarmupStartSeconds.ToString("0.##"),
                    burst.FirstAimEndSeconds.ToString("0.##"),
                    burst.SecondAimEndSeconds.ToString("0.##"),
                    burst.FirstShotSeconds.ToString("0.##"),
                    burst.LastShotSeconds.ToString("0.##"),
                    burst.CooldownEndSeconds.ToString("0.##"))
                : "CEHCC_TimelineBurstLine".Translate(
                    burst.BurstIndex + 1,
                    shotRange,
                    burst.WarmupStartSeconds.ToString("0.##"),
                    burst.FirstAimEndSeconds.ToString("0.##"),
                    burst.FirstShotSeconds.ToString("0.##"),
                    burst.LastShotSeconds.ToString("0.##"),
                    burst.CooldownEndSeconds.ToString("0.##"));
            DrawWrappedLabel(ref y, 0f, width, label);
        }

        private static Color TimelineWarmupColor()
        {
            return new Color(0.66f, 0.52f, 0.28f);
        }

        private static Color TimelineSecondAimColor()
        {
            return new Color(0.44f, 0.63f, 0.84f);
        }

        private static Color TimelineFireColor()
        {
            return new Color(0.82f, 0.45f, 0.32f);
        }

        private static Color TimelineCooldownColor()
        {
            return new Color(0.42f, 0.5f, 0.46f);
        }

        private static Color TimelineReloadColor()
        {
            return new Color(0.62f, 0.48f, 0.72f);
        }

        private static void DrawPercentBar(Rect rect, float value, Color fillColor)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.09f, 0.1f));
            Rect fill = rect;
            fill.width *= Mathf.Clamp01(value);
            Widgets.DrawBoxSolid(fill, fillColor);
            Widgets.DrawBox(rect);
        }

        private float CalculateAnalysisHeight()
        {
            if (input.AnalysisMode == HitChanceAnalysisMode.DistanceCurve)
            {
                int rowCount = cachedDistanceCurve != null ? cachedDistanceCurve.Length : 1;
                return 4f + BoxedAnalysisHeight(Mathf.Max(1, rowCount)) + 8f;
            }
            if (input.AnalysisMode == HitChanceAnalysisMode.BurstPerShot)
            {
                int rowCount = input.BurstShots <= 1 ? 0 : Mathf.Clamp(input.BurstShots, 1, 30);
                return 4f + BoxedAnalysisHeight(rowCount) + 8f;
            }

            float height = 30f;
            if (input.AnalysisMode == HitChanceAnalysisMode.Timeline)
            {
                FireTimeline timeline = input.Timeline60SecondMode
                    ? DpsCalculator.BuildFireTimeline(input, 60f)
                    : DpsCalculator.BuildFireTimeline(input);
                int lines = Mathf.Min(5, timeline.Bursts.Count) + (timeline.Bursts.Count > 5 ? 2 : 0);
                height += 30f + 24f + 126f + lines * 24f;
            }
            else if (input.AnalysisMode == HitChanceAnalysisMode.BallisticDistribution)
            {
                height += 30f + 56f + TopDistributionPlotHeight(input.DistanceCells) + 184f + 24f;
                if (HitChanceObstacleBridge.TryGetFor(input, out _))
                {
                    height += 24f;
                }
            }
            return height;
        }

        private void DrawTimelineWindowMode(ref float y, float width)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), "CEHCC_TimelineWindowMode".Translate());
            string label = input.Timeline60SecondMode ? "CEHCC_TimelineWindow60s".Translate() : "CEHCC_TimelineWindowMagazine".Translate();
            if (Widgets.ButtonText(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), label))
            {
                input.Timeline60SecondMode = !input.Timeline60SecondMode;
            }
            y += 30f;
        }

        private void DrawAimMode(ref float y, float width)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), "CEHCC_AimMode".Translate());
            if (Widgets.ButtonText(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), AimModeLabel(input.AimMode)))
            {
                input.AimMode = NextAimMode(input.AimMode);
            }
            y += 30f;
        }

        private void DrawAttachments(ref float y, float width)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), "CEHCC_Attachments".Translate());
            if (Widgets.ButtonText(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), "CEHCC_AttachmentCount".Translate(selectedAttachments.Count)))
            {
                OpenAttachmentsMenu();
            }
            y += 30f;
        }

        private void DrawBipod(ref float y, float width)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), "CEHCC_BipodState".Translate());
            string label = selectedBipodDeployed ? "CEHCC_BipodDeployed".Translate() : "CEHCC_BipodUndeployed".Translate();
            if (Widgets.ButtonText(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), label))
            {
                selectedBipodDeployed = !selectedBipodDeployed;
                ReapplyLoadedWeapon();
            }
            y += 30f;
        }

        private void DrawQuality(ref float y, float width)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), "CEHCC_WeaponQuality".Translate());
            if (Widgets.ButtonText(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), CEWeaponDataLoader.QualityLabel(selectedQuality)))
            {
                OpenQualityMenu();
            }
            y += 30f;
        }

        private void DrawTargetMode(ref float y, float width)
        {
            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), "CEHCC_TargetMode".Translate());
            if (Widgets.ButtonText(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), TargetModeLabel(input.TargetMode)))
            {
                input.TargetMode = NextTargetMode(input.TargetMode);
            }
            y += 30f;
        }

        private void DrawBurstMode(ref float y, float width)
        {
            if (burstModes == null || burstModes.Count == 0)
            {
                DrawInt(ref y, width, "CEHCC_BurstShotsManual".Translate(), ref input.BurstShots, ref burstBuffer, 1, 60);
                return;
            }

            Rect row = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(row.x, row.y, width * 0.58f, row.height), "CEHCC_BurstMode".Translate());
            int index = FindBurstModeIndex(input.BurstShots);
            if (index >= 0)
            {
                burstModeIndex = index;
            }
            string label = burstModeIndex >= 0 && burstModeIndex < burstModes.Count
                ? burstModes[burstModeIndex].Label
                : "CEHCC_BurstModeCurrent".Translate(input.BurstShots);
            if (Widgets.ButtonText(new Rect(width * 0.6f, row.y, width * 0.28f, row.height), label))
            {
                OpenBurstModeMenu();
            }
            y += 30f;
        }

        private void OpenBurstModeMenu()
        {
            if (burstModes == null || burstModes.Count == 0)
            {
                return;
            }

            var options = new List<FloatMenuOption>();
            for (int i = 0; i < burstModes.Count; i++)
            {
                int optionIndex = i;
                CEBurstModeChoice choice = burstModes[i];
                options.Add(new FloatMenuOption(choice.Label, delegate
                {
                    burstModeIndex = optionIndex;
                    input.BurstShots = choice.Shots;
                    burstBuffer = input.BurstShots.ToString();
                    forceRecalculate = true;
                    StoreSessionState();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private int FindBurstModeIndex(int shots)
        {
            if (burstModes == null)
            {
                return -1;
            }

            for (int i = 0; i < burstModes.Count; i++)
            {
                if (burstModes[i].Shots == shots)
                {
                    return i;
                }
            }
            return -1;
        }

        private static HitChanceTargetMode NextTargetMode(HitChanceTargetMode mode)
        {
            switch (mode)
            {
                case HitChanceTargetMode.Torso:
                    return HitChanceTargetMode.Head;
                case HitChanceTargetMode.Head:
                    return HitChanceTargetMode.Legs;
                default:
                    return HitChanceTargetMode.Torso;
            }
        }

        private static string TargetModeLabel(HitChanceTargetMode mode)
        {
            switch (mode)
            {
                case HitChanceTargetMode.Head:
                    return "CEHCC_TargetModeHead".Translate();
                case HitChanceTargetMode.Legs:
                    return "CEHCC_TargetModeLegs".Translate();
                default:
                    return "CEHCC_TargetModeTorso".Translate();
            }
        }

        private static HitChanceAimMode NextAimMode(HitChanceAimMode mode)
        {
            switch (mode)
            {
                case HitChanceAimMode.AimedShot:
                    return HitChanceAimMode.Snapshot;
                case HitChanceAimMode.Snapshot:
                    return HitChanceAimMode.SuppressFire;
                default:
                    return HitChanceAimMode.AimedShot;
            }
        }

        private static string AimModeLabel(HitChanceAimMode mode)
        {
            switch (mode)
            {
                case HitChanceAimMode.AimedShot:
                    return "CEHCC_AimModeAimedShot".Translate();
                case HitChanceAimMode.Snapshot:
                    return "CEHCC_AimModeSnapshot".Translate();
                case HitChanceAimMode.SuppressFire:
                    return "CEHCC_AimModeSuppressFire".Translate();
                default:
                    return "CEHCC_AimModeAimedShot".Translate();
            }
        }

        private void OpenAnalysisModeMenu()
        {
            HitChanceAnalysisMode[] modes =
            {
                HitChanceAnalysisMode.None,
                HitChanceAnalysisMode.DistanceCurve,
                HitChanceAnalysisMode.BurstPerShot,
                HitChanceAnalysisMode.Timeline,
                HitChanceAnalysisMode.BallisticDistribution
            };
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < modes.Length; i++)
            {
                HitChanceAnalysisMode mode = modes[i];
                options.Add(new FloatMenuOption(AnalysisModeLabel(mode), delegate
                {
                    if (input.AnalysisMode == mode)
                    {
                        return;
                    }

                    input.AnalysisMode = mode;
                    forceRecalculate = true;
                    StoreSessionState();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string AnalysisModeLabel(HitChanceAnalysisMode mode)
        {
            switch (mode)
            {
                case HitChanceAnalysisMode.DistanceCurve:
                    return "CEHCC_AnalysisModeDistance".Translate();
                case HitChanceAnalysisMode.BurstPerShot:
                    return "CEHCC_AnalysisModeBurst".Translate();
                case HitChanceAnalysisMode.Timeline:
                    return "CEHCC_AnalysisModeTimeline".Translate();
                case HitChanceAnalysisMode.BallisticDistribution:
                    return "CEHCC_AnalysisModeBallistic".Translate();
                default:
                    return "CEHCC_AnalysisModeNone".Translate();
            }
        }

        private void OpenQualityMenu()
        {
            QualityCategory[] qualities =
            {
                QualityCategory.Awful,
                QualityCategory.Poor,
                QualityCategory.Normal,
                QualityCategory.Good,
                QualityCategory.Excellent,
                QualityCategory.Masterwork,
                QualityCategory.Legendary
            };
            var options = new System.Collections.Generic.List<FloatMenuOption>();
            for (int i = 0; i < qualities.Length; i++)
            {
                QualityCategory quality = qualities[i];
                options.Add(new FloatMenuOption(CEWeaponDataLoader.QualityLabel(quality), delegate
                {
                    selectedQuality = quality;
                    ReapplyLoadedWeaponForQuality();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenAttachmentsMenu()
        {
            if (loadedWeapon == null)
            {
                return;
            }

            List<AttachmentDef> attachments = CEWeaponDataLoader.AvailableAttachments(loadedWeapon);
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CEHCC_AttachmentClear".Translate(), delegate
                {
                    selectedAttachments.Clear();
                    ReapplyLoadedWeapon();
                })
            };

            for (int i = 0; i < attachments.Count; i++)
            {
                AttachmentDef attachment = attachments[i];
                bool selected = selectedAttachments.Contains(attachment);
                bool canAdd = selected || CEWeaponDataLoader.CanAddAttachment(loadedWeapon, selectedAttachments, attachment);
                string prefix = selected ? "CEHCC_AttachmentSelected".Translate() : "CEHCC_AttachmentUnselected".Translate();
                string label = prefix + " " + attachment.LabelCap;
                if (!canAdd)
                {
                    label += " " + "CEHCC_AttachmentIncompatible".Translate();
                }

                options.Add(new FloatMenuOption(label, delegate
                {
                    if (selectedAttachments.Contains(attachment))
                    {
                        selectedAttachments.Remove(attachment);
                    }
                    else if (CEWeaponDataLoader.CanAddAttachment(loadedWeapon, selectedAttachments, attachment))
                    {
                        selectedAttachments.Add(attachment);
                    }
                    ReapplyLoadedWeapon();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void RestoreDefaults()
        {
            CopyInputs(new HitChanceInputs(), input);
            selectedQuality = QualityCategory.Normal;
            loadedWeapon = null;
            loadedAmmoChoice = null;
            loadedWeaponSwayFactor = null;
            loadedWeaponHasQuality = false;
            loadedWeaponIsTurret = false;
            loadedWeaponHasBipod = false;
            loadedWeaponHasAttachments = false;
            selectedBipodDeployed = false;
            selectedAttachments.Clear();
            loadedShooterLabel = null;
            loadedProjectileWarning = null;
            loadedDamageProfile = DamageProfile.Empty;
            comparisonSnapshot = null;
            burstModes.Clear();
            burstModeIndex = -1;
            if (!TryLoadDefaultAssaultRifle())
            {
                ApplyFallbackAssaultRifleDefaults();
            }
            forceRecalculate = true;
            InitBuffers();
            if (CEHitChanceCalculatorMod.Settings != null)
            {
                CEHitChanceCalculatorMod.Settings.DefaultLoadoutInputSignature = BuildInputSignature(input, false);
            }
            StoreSessionState();
        }

        private bool ShouldLoadDefaultAssaultRifleOnOpen()
        {
            if (loadedWeapon != null)
            {
                return false;
            }

            string signature = BuildInputSignature(input, false);
            string defaultLoadoutSignature = CEHitChanceCalculatorMod.Settings?.DefaultLoadoutInputSignature;
            return IsPlainDefaultInput(input)
                || (!string.IsNullOrEmpty(defaultLoadoutSignature) && defaultLoadoutSignature == signature);
        }

        private bool TryLoadDefaultAssaultRifle()
        {
            ThingDef assaultRifleDef = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            if (assaultRifleDef == null)
            {
                return false;
            }

            ThingWithComps weapon = CEWeaponDataLoader.MakeWeaponForDef(assaultRifleDef, selectedQuality);
            if (weapon == null)
            {
                return false;
            }

            selectedBipodDeployed = CEWeaponDataLoader.IsBipodDeployed(weapon);
            selectedAttachments = CEWeaponDataLoader.InstalledAttachments(weapon);
            List<CEAmmoChoice> choices = CEWeaponDataLoader.AmmoChoicesFor(weapon);
            CEAmmoChoice choice = choices.FirstOrDefault(x => x.Projectile != null && x.Projectile.defName == "Bullet_556x45mmNATO_FMJ")
                ?? choices.FirstOrDefault(x => x.Projectile != null)
                ?? (choices.Count > 0 ? choices[0] : null);
            if (!CEWeaponDataLoader.TryApplyWeapon(weapon, choice, selectedQuality, selectedBipodDeployed, selectedAttachments, input, out float swayFactor, out string message))
            {
                return false;
            }

            loadedWeapon = weapon;
            loadedAmmoChoice = choice;
            loadedWeaponSwayFactor = swayFactor;
            loadedWeaponHasQuality = CEWeaponDataLoader.HasQuality(weapon);
            loadedWeaponHasBipod = CEWeaponDataLoader.HasBipod(weapon);
            loadedWeaponHasAttachments = CEWeaponDataLoader.HasAttachments(weapon);
            loadedProjectileWarning = CEWeaponDataLoader.ProjectileApproximationWarning(weapon, choice);
            loadedDamageProfile = CEWeaponDataLoader.DamageProfileFor(weapon, choice, selectedQuality, selectedBipodDeployed, selectedAttachments);
            RefreshBurstModes(input.BurstShots);
            return true;
        }

        private void ApplyFallbackAssaultRifleDefaults()
        {
            input.MaxRangeCells = 55f;
            input.ShotSpeedCellsPerSecond = 168f;
            input.ShotHeightCells = 0.85f;
            input.ShooterMaxHeightCells = 1f;
            input.GravityFactor = 1f;
            input.SpreadDegrees = 0.07f;
            input.RecoilAmount = 1.50f;
            input.SightsEfficiency = 1.0f;
            input.SwayDegrees = HitChanceCalculator.CalculateSwayAmplitude(input.ShootingAccuracy, 1.33f);
            input.BurstShots = 6;
            input.Rpm = 900f;
            input.SustainedShotsPerSecond = 0f;
            input.MagazineShots = 30;
            input.FireWarmupSeconds = 1.1f;
            input.FireCooldownSeconds = 0.36f;
            input.ReloadSeconds = 4f;
            loadedDamageProfile = new DamageProfile
            {
                SourceLabel = "5.56mm NATO bullet (FMJ)",
                DirectDamagePerShot = 14f,
                DirectLabel = "14"
            };
            loadedDamageProfile.DirectLines.Add(new DamageLine
            {
                Label = "14",
                DamagePerShot = 14f,
                UsesDirectHitChance = true,
                Count = 1,
                ArmorKind = ArmorDamageKind.Sharp,
                ArmorPenetrationSharp = 5.4f,
                ArmorPenetrationBlunt = 16.54f
            });
        }

        private void ChooseWeaponFromMenu()
        {
            var defs = CEWeaponDataLoader.AllRangedWeaponDefs();
            if (defs.Count == 0)
            {
                Messages.Message("CEHCC_ChooseWeaponNone".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var options = new System.Collections.Generic.List<FloatMenuOption>();
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef def = defs[i];
                options.Add(new FloatMenuOption(def.LabelCap, delegate
                {
                    ThingWithComps weapon = CEWeaponDataLoader.MakeWeaponForDef(def, selectedQuality);
                    selectedBipodDeployed = CEWeaponDataLoader.IsBipodDeployed(weapon);
                    selectedAttachments = CEWeaponDataLoader.InstalledAttachments(weapon);
                    ChooseAmmoAndApplyWeapon(weapon);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void LoadSelectedWeaponData()
        {
            if (!CEWeaponDataLoader.TryFindSelectedWeapon(out ThingWithComps weapon, out bool fromTurret, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (weapon.TryGetQuality(out QualityCategory quality))
            {
                selectedQuality = quality;
            }
            else
            {
                selectedQuality = QualityCategory.Normal;
            }
            selectedBipodDeployed = CEWeaponDataLoader.IsBipodDeployed(weapon);
            selectedAttachments = CEWeaponDataLoader.InstalledAttachments(weapon);

            ChooseAmmoAndApplyWeapon(weapon, fromTurret);
        }

        private void ChooseAmmoAndApplyWeapon(ThingWithComps weapon, bool fromTurret = false)
        {
            var choices = CEWeaponDataLoader.AmmoChoicesFor(weapon);
            if (choices.Count > 1)
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>();
                for (int i = 0; i < choices.Count; i++)
                {
                    CEAmmoChoice choice = choices[i];
                    options.Add(new FloatMenuOption(choice.Label, delegate
                    {
                        ApplyWeaponData(weapon, choice, fromTurret);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
                return;
            }

            ApplyWeaponData(weapon, choices.Count == 1 ? choices[0] : null, fromTurret);
        }

        private void LoadSelectedShooterData()
        {
            if (!CEWeaponDataLoader.TryFindSelectedShooterSource(out Thing shooter, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!CEWeaponDataLoader.TryApplyShooter(shooter, input, loadedWeaponSwayFactor, out message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            loadedShooterLabel = CEWeaponDataLoader.ShooterSourceLabel(shooter);
            InitBuffers();
            forceRecalculate = true;
            StoreSessionState();
            Messages.Message(message, MessageTypeDefOf.TaskCompletion, false);
        }

        private void LoadComparisonShooterData()
        {
            if (!CEWeaponDataLoader.TryFindSelectedShooterSource(out Thing shooter, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            HitChanceInputs compareInput = CloneInputs(input);
            if (!CEWeaponDataLoader.TryApplyShooter(shooter, compareInput, loadedWeaponSwayFactor, out message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            comparisonSnapshot = CreateComparisonSnapshot(compareInput, loadedDamageProfile, "CEHCC_ComparisonShooterLabel".Translate(CEWeaponDataLoader.ShooterSourceLabel(shooter)).ToString());
            StoreSessionState();
            Messages.Message("CEHCC_ComparisonLoaded".Translate(comparisonSnapshot.Label), MessageTypeDefOf.TaskCompletion, false);
        }

        private void SaveCurrentComparisonSnapshot()
        {
            EnsureCalculated();
            HitChanceInputs compareInput = CloneInputs(input);
            string label = loadedWeapon != null
                ? "CEHCC_ComparisonSnapshotLabel".Translate(loadedWeapon.LabelCap).ToString()
                : "CEHCC_ComparisonSnapshotManual".Translate().ToString();
            comparisonSnapshot = CreateComparisonSnapshot(compareInput, loadedDamageProfile, label);
            StoreSessionState();
            Messages.Message("CEHCC_ComparisonLoaded".Translate(comparisonSnapshot.Label), MessageTypeDefOf.TaskCompletion, false);
        }

        private void LoadComparisonWeaponData()
        {
            if (!CEWeaponDataLoader.TryFindSelectedWeapon(out ThingWithComps weapon, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            QualityCategory quality = selectedQuality;
            if (weapon.TryGetQuality(out QualityCategory weaponQuality))
            {
                quality = weaponQuality;
            }
            bool bipodDeployed = CEWeaponDataLoader.IsBipodDeployed(weapon);
            List<AttachmentDef> attachments = CEWeaponDataLoader.InstalledAttachments(weapon);
            ChooseAmmoAndApplyComparisonWeapon(weapon, quality, bipodDeployed, attachments);
        }

        private void ChooseAmmoAndApplyComparisonWeapon(ThingWithComps weapon, QualityCategory quality, bool bipodDeployed, List<AttachmentDef> attachments)
        {
            var choices = CEWeaponDataLoader.AmmoChoicesFor(weapon);
            if (choices.Count > 1)
            {
                var options = new List<FloatMenuOption>();
                for (int i = 0; i < choices.Count; i++)
                {
                    CEAmmoChoice choice = choices[i];
                    options.Add(new FloatMenuOption(choice.Label, delegate
                    {
                        ApplyComparisonWeaponData(weapon, choice, quality, bipodDeployed, attachments);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
                return;
            }

            ApplyComparisonWeaponData(weapon, choices.Count == 1 ? choices[0] : null, quality, bipodDeployed, attachments);
        }

        private void ApplyComparisonWeaponData(ThingWithComps weapon, CEAmmoChoice choice, QualityCategory quality, bool bipodDeployed, List<AttachmentDef> attachments)
        {
            HitChanceInputs compareInput = CloneInputs(input);
            if (!CEWeaponDataLoader.TryApplyWeapon(weapon, choice, quality, bipodDeployed, attachments, compareInput, out float swayFactor, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            DamageProfile damageProfile = CEWeaponDataLoader.DamageProfileFor(weapon, choice, quality, bipodDeployed, attachments);
            comparisonSnapshot = CreateComparisonSnapshot(compareInput, damageProfile, "CEHCC_ComparisonWeaponLabel".Translate(weapon.LabelCap).ToString());
            comparisonSnapshot.Weapon = weapon;
            comparisonSnapshot.AmmoChoice = choice;
            comparisonSnapshot.WeaponSwayFactor = swayFactor;
            comparisonSnapshot.Quality = quality;
            comparisonSnapshot.WeaponHasQuality = CEWeaponDataLoader.HasQuality(weapon);
            comparisonSnapshot.WeaponHasBipod = CEWeaponDataLoader.HasBipod(weapon);
            comparisonSnapshot.WeaponHasAttachments = CEWeaponDataLoader.HasAttachments(weapon);
            comparisonSnapshot.BipodDeployed = bipodDeployed;
            comparisonSnapshot.Attachments = attachments != null ? new List<AttachmentDef>(attachments) : new List<AttachmentDef>();
            comparisonSnapshot.ProjectileWarning = CEWeaponDataLoader.ProjectileApproximationWarning(weapon, choice);
            StoreSessionState();
            Messages.Message("CEHCC_ComparisonLoaded".Translate(comparisonSnapshot.Label), MessageTypeDefOf.TaskCompletion, false);
        }

        private ComparisonSnapshot CreateComparisonSnapshot(HitChanceInputs compareInput, DamageProfile damageProfile, string label)
        {
            var snapshot = new ComparisonSnapshot
            {
                Input = compareInput,
                DamageProfile = damageProfile ?? DamageProfile.Empty,
                Label = label,
                Result = HitChanceCalculator.Calculate(compareInput),
                Weapon = loadedWeapon,
                AmmoChoice = loadedAmmoChoice,
                WeaponSwayFactor = loadedWeaponSwayFactor,
                Quality = selectedQuality,
                WeaponHasQuality = loadedWeaponHasQuality,
                WeaponHasBipod = loadedWeaponHasBipod,
                WeaponHasAttachments = loadedWeaponHasAttachments,
                BipodDeployed = selectedBipodDeployed,
                Attachments = new List<AttachmentDef>(selectedAttachments),
                ProjectileWarning = loadedProjectileWarning
            };
            return snapshot;
        }

        private void SetComparisonAsBaseline()
        {
            if (comparisonSnapshot == null)
            {
                return;
            }

            CopyInputs(comparisonSnapshot.Input, input);
            loadedDamageProfile = comparisonSnapshot.DamageProfile ?? DamageProfile.Empty;
            loadedWeapon = comparisonSnapshot.Weapon;
            loadedAmmoChoice = comparisonSnapshot.AmmoChoice;
            loadedWeaponSwayFactor = comparisonSnapshot.WeaponSwayFactor;
            selectedQuality = comparisonSnapshot.Quality;
            loadedWeaponHasQuality = comparisonSnapshot.WeaponHasQuality;
            loadedWeaponHasBipod = comparisonSnapshot.WeaponHasBipod;
            loadedWeaponHasAttachments = comparisonSnapshot.WeaponHasAttachments;
            selectedBipodDeployed = comparisonSnapshot.BipodDeployed;
            selectedAttachments = new List<AttachmentDef>(comparisonSnapshot.Attachments);
            loadedProjectileWarning = comparisonSnapshot.ProjectileWarning;
            RefreshBurstModes(input.BurstShots);
            comparisonSnapshot = null;
            InitBuffers();
            forceRecalculate = true;
            StoreSessionState();
        }

        private void LoadSelectedTargetData()
        {
            if (!CETargetDataLoader.TryLoadSelectedTarget(input, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            RefreshTargetBuffers();
            forceRecalculate = true;
            Messages.Message(message, MessageTypeDefOf.TaskCompletion, false);
        }

        private void ChooseTargetPreset()
        {
            var options = new List<FloatMenuOption>();
            HitChanceTargetPreset[] presets = HitChanceTargetPresets.All;
            for (int i = 0; i < presets.Length; i++)
            {
                HitChanceTargetPreset preset = presets[i];
                options.Add(new FloatMenuOption(TargetPresetMenuLabel(preset), delegate
                {
                    ApplyTargetPreset(preset);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyTargetPreset(HitChanceTargetPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            input.TargetHeightMeters = preset.HeightMeters;
            input.TargetWidthMeters = preset.WidthMeters;
            input.TargetArmorSharp = preset.ArmorSharp;
            input.TargetArmorBlunt = preset.ArmorBlunt;
            input.TargetArmorHeat = preset.ArmorHeat;
            input.TargetArmorElectric = preset.ArmorElectric;
            RefreshTargetBuffers();
            forceRecalculate = true;
            StoreSessionState();
            Messages.Message("CEHCC_TargetPresetLoaded".Translate(preset.LabelKey.Translate()), MessageTypeDefOf.TaskCompletion, false);
        }

        private static string TargetPresetMenuLabel(HitChanceTargetPreset preset)
        {
            return "CEHCC_TargetPresetMenuLine".Translate(
                preset.LabelKey.Translate(),
                preset.HeightMeters.ToString("0.##"),
                preset.WidthMeters.ToString("0.##"),
                preset.ArmorSharp.ToString("0.##"),
                preset.ArmorBlunt.ToString("0.##"),
                preset.ArmorHeat.ToString("0.##"),
                preset.ArmorElectric.ToString("0.##")).ToString();
        }

        private void RefreshTargetBuffers()
        {
            targetHeightBuffer = input.TargetHeightMeters.ToString("0.###");
            targetWidthBuffer = input.TargetWidthMeters.ToString("0.###");
            targetArmorSharpBuffer = input.TargetArmorSharp.ToString("0.###");
            targetArmorBluntBuffer = input.TargetArmorBlunt.ToString("0.###");
            targetArmorHeatBuffer = input.TargetArmorHeat.ToString("0.###");
            targetArmorElectricBuffer = input.TargetArmorElectric.ToString("0.###");
        }

        private void ApplyWeaponData(ThingWithComps weapon, CEAmmoChoice choice, bool fromTurret = false)
        {
            bool hasQuality = CEWeaponDataLoader.HasQuality(weapon);
            bool hasBipod = CEWeaponDataLoader.HasBipod(weapon);
            bool hasAttachments = CEWeaponDataLoader.HasAttachments(weapon);
            if (!hasQuality)
            {
                selectedQuality = QualityCategory.Normal;
            }
            if (!hasBipod)
            {
                selectedBipodDeployed = false;
            }
            if (!hasAttachments)
            {
                selectedAttachments.Clear();
            }
            if (!CEWeaponDataLoader.TryApplyWeapon(weapon, choice, selectedQuality, selectedBipodDeployed, selectedAttachments, input, out float swayFactor, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (fromTurret)
            {
                input.AimMode = CEWeaponDataLoader.AiAimModeForWeapon(weapon);
                message = "CEHCC_LoadTurretWeaponSuccess".Translate(
                    weapon.LabelCap,
                    choice?.Label ?? "CEHCC_AmmoChoiceDefault".Translate().ToString(),
                    hasQuality ? CEWeaponDataLoader.QualityLabel(selectedQuality) : "CEHCC_QualityNone".Translate().ToString(),
                    AimModeLabel(input.AimMode));
            }

            loadedWeapon = weapon;
            loadedAmmoChoice = choice;
            loadedWeaponSwayFactor = swayFactor;
            loadedWeaponHasQuality = hasQuality;
            loadedWeaponIsTurret = fromTurret;
            loadedWeaponHasBipod = hasBipod;
            loadedWeaponHasAttachments = hasAttachments;
            loadedProjectileWarning = CEWeaponDataLoader.ProjectileApproximationWarning(weapon, choice);
            loadedDamageProfile = CEWeaponDataLoader.DamageProfileFor(weapon, choice, selectedQuality, selectedBipodDeployed, selectedAttachments);
            RefreshBurstModes(input.BurstShots);
            InitBuffers();
            forceRecalculate = true;
            StoreSessionState();
            Messages.Message(message, MessageTypeDefOf.TaskCompletion, false);
        }

        private void ReapplyLoadedWeaponForQuality()
        {
            ReapplyLoadedWeapon();
        }

        private void ReapplyLoadedWeapon()
        {
            if (loadedWeapon == null)
            {
                forceRecalculate = true;
                return;
            }

            int previousBurstShots = input.BurstShots;
            if (!loadedWeaponHasQuality)
            {
                selectedQuality = QualityCategory.Normal;
            }
            if (!CEWeaponDataLoader.TryApplyWeapon(loadedWeapon, loadedAmmoChoice, selectedQuality, selectedBipodDeployed, selectedAttachments, input, out float swayFactor, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            loadedWeaponSwayFactor = swayFactor;
            loadedDamageProfile = CEWeaponDataLoader.DamageProfileFor(loadedWeapon, loadedAmmoChoice, selectedQuality, selectedBipodDeployed, selectedAttachments);
            RefreshBurstModes(previousBurstShots);
            InitBuffers();
            forceRecalculate = true;
            StoreSessionState();
        }

        private void RefreshBurstModes(int preferredShots)
        {
            if (loadedWeapon == null)
            {
                burstModes = new List<CEBurstModeChoice>();
                burstModeIndex = -1;
                return;
            }

            burstModes = CEWeaponDataLoader.BurstModeChoicesFor(loadedWeapon, selectedQuality, selectedBipodDeployed, selectedAttachments);
            if (burstModes.Count == 0)
            {
                burstModeIndex = -1;
                return;
            }

            burstModeIndex = FindBurstModeIndex(preferredShots);
            if (burstModeIndex < 0)
            {
                burstModeIndex = FindBurstModeIndex(input.BurstShots);
            }
            if (burstModeIndex < 0)
            {
                burstModeIndex = burstModes.Count - 1;
            }
            input.BurstShots = burstModes[burstModeIndex].Shots;
            burstBuffer = input.BurstShots.ToString();
        }

        private void InitBuffers()
        {
            distanceBuffer = input.DistanceCells.ToString("0.###");
            maxRangeBuffer = input.MaxRangeCells.ToString("0.###");
            speedBuffer = input.ShotSpeedCellsPerSecond.ToString("0.###");
            shotHeightBuffer = input.ShotHeightCells.ToString("0.###");
            shooterMaxHeightBuffer = input.ShooterMaxHeightCells.ToString("0.###");
            gravityFactorBuffer = input.GravityFactor.ToString("0.###");
            targetHeightBuffer = input.TargetHeightMeters.ToString("0.###");
            targetWidthBuffer = input.TargetWidthMeters.ToString("0.###");
            targetArmorSharpBuffer = input.TargetArmorSharp.ToString("0.###");
            targetArmorBluntBuffer = input.TargetArmorBlunt.ToString("0.###");
            targetArmorHeatBuffer = input.TargetArmorHeat.ToString("0.###");
            targetArmorElectricBuffer = input.TargetArmorElectric.ToString("0.###");
            swayBuffer = input.SwayDegrees.ToString("0.###");
            spreadBuffer = input.SpreadDegrees.ToString("0.###");
            recoilBuffer = input.RecoilAmount.ToString("0.###");
            shootingAccuracyBuffer = input.ShootingAccuracy.ToString("0.###");
            aimingAccuracyBuffer = input.AimingAccuracy.ToString("0.###");
            sightsEfficiencyBuffer = input.SightsEfficiency.ToString("0.###");
            aimingDelayFactorBuffer = input.AimingDelayFactor.ToString("0.###");
            reloadSpeedBuffer = input.ReloadSpeed.ToString("0.###");
            reloadFactorBuffer = input.ReloadFactor.ToString("0.###");
            nightVisionEfficiencyBuffer = input.NightVisionEfficiency.ToString("0.###");
            darknessBuffer = input.Darkness.ToString("0.###");
            weatherBuffer = input.WeatherError.ToString("0.###");
            smokeBuffer = input.SmokeDensity.ToString("0.###");
            targetMoveSpeedBuffer = input.TargetMoveSpeedCellsPerSecond.ToString("0.###");
            targetMoveDirectionBuffer = input.TargetMoveDirectionDegrees.ToString("0.###");
            shooterThingIdBuffer = input.ShooterThingId.ToString();
            swayStartTickBuffer = input.SwayStartTick.ToString();
            circularBuffer = input.CircularMissRadiusCells.ToString("0.###");
            indirectBuffer = input.IndirectFireShiftCells.ToString("0.###");
            burstBuffer = input.BurstShots.ToString();
            rpmBuffer = input.Rpm.ToString("0.###");
            samplesBuffer = input.Samples.ToString();
            analysisSamplesBuffer = input.AnalysisSamples.ToString();
            seedBuffer = input.Seed.ToString();
        }

        private static string Percent(float value)
        {
            return (value * 100f).ToString("0.##") + "%";
        }

        private static string ArmorStatusLabel(ArmorDamageResult armor)
        {
            if (armor == null || armor.Kind == ArmorDamageKind.None)
            {
                return "CEHCC_ArmorStatusNoArmorModel".Translate();
            }
            if (armor.Kind == ArmorDamageKind.Blunt)
            {
                return "CEHCC_ArmorStatusBlunt".Translate();
            }
            if (armor.Kind == ArmorDamageKind.Heat)
            {
                return "CEHCC_ArmorStatusHeat".Translate();
            }
            if (armor.Kind == ArmorDamageKind.Electric)
            {
                return "CEHCC_ArmorStatusElectric".Translate();
            }
            if (armor.Kind == ArmorDamageKind.Composite)
            {
                return "CEHCC_ArmorStatusComposite".Translate();
            }
            if (armor.SharpDeflected)
            {
                return "CEHCC_ArmorStatusDeflected".Translate();
            }
            if (armor.SharpPenetrated)
            {
                return "CEHCC_ArmorStatusPenetrated".Translate();
            }
            return "CEHCC_ArmorStatusNoArmorModel".Translate();
        }

        private static string ArmorBreakdownText(ArmorDamageResult armor)
        {
            if (armor.Contributions != null && armor.Contributions.Count > 0)
            {
                string contributions = "";
                for (int i = 0; i < armor.Contributions.Count; i++)
                {
                    ArmorDamageContribution contribution = armor.Contributions[i];
                    if (contribution == null || contribution.PostArmorDamage <= 0.0049f)
                    {
                        continue;
                    }
                    string label = ShortContributionLabel(contribution.Label, contribution.Kind);
                    AppendText(ref contributions, label + " " + contribution.PostArmorDamage.ToString("0.##"));
                }
                if (!contributions.NullOrEmpty())
                {
                    return contributions;
                }
            }

            string text = "";
            AppendAmount(ref text, "锐伤", armor.SharpDamage, "0.##");
            AppendAmount(ref text, "钝伤", armor.BluntDamage, "0.##");
            AppendAmount(ref text, "热伤", armor.HeatDamage, "0.##");
            AppendAmount(ref text, "电伤", armor.ElectricDamage, "0.##");
            AppendAmount(ref text, "未建模伤害", armor.UnmodeledDamage, "0.##");
            return text.NullOrEmpty() ? "无有效伤害" : text;
        }

        private static string ShortContributionLabel(string label, ArmorDamageKind kind)
        {
            if (label.NullOrEmpty())
            {
                return ArmorKindLabel(kind);
            }
            string result = label.Replace("，概率 100%", "").Replace(", 概率 100%", "").Trim();
            return result.NullOrEmpty() ? ArmorKindLabel(kind) : result;
        }

        private static string ArmorKindLabel(ArmorDamageKind kind)
        {
            switch (kind)
            {
                case ArmorDamageKind.Sharp:
                    return "锐伤";
                case ArmorDamageKind.Blunt:
                    return "钝伤";
                case ArmorDamageKind.Heat:
                    return "热伤";
                case ArmorDamageKind.Electric:
                    return "电伤";
                default:
                    return "未建模伤害";
            }
        }

        private static string ArmorPenetrationText(ArmorDamageResult armor)
        {
            string text = "";
            if (armor.SharpPenetration > 0.0001f || armor.SharpDamage > 0.0001f)
            {
                AppendText(ref text, "锐穿 " + armor.SharpPenetration.ToString("0.###") + " mm RHA");
            }
            if (armor.BluntPenetration > 0.0001f || armor.BluntDamage > 0.0001f)
            {
                AppendText(ref text, "钝穿 " + armor.BluntPenetration.ToString("0.###") + " MPa");
            }
            if (armor.HeatPenetration > 0.0001f || armor.HeatDamage > 0.0001f)
            {
                AppendText(ref text, "热穿 " + armor.HeatPenetration.ToString("0.###"));
            }
            if (armor.ElectricPenetration > 0.0001f || armor.ElectricDamage > 0.0001f)
            {
                AppendText(ref text, "电穿 " + armor.ElectricPenetration.ToString("0.###"));
            }
            if (armor.EffectiveBluntPenetration > 0.0001f && armor.BluntDamage > 0.0001f)
            {
                AppendText(ref text, "有效钝穿 " + armor.EffectiveBluntPenetration.ToString("0.###") + " MPa");
            }
            return text.NullOrEmpty() ? "无" : text;
        }

        private static void AppendAmount(ref string text, string label, float value, string format)
        {
            if (value <= 0.0049f)
            {
                return;
            }
            AppendText(ref text, label + " " + value.ToString(format));
        }

        private static void AppendText(ref string text, string value)
        {
            if (!text.NullOrEmpty())
            {
                text += "，";
            }
            text += value;
        }

        private static string SignedPercent(float value)
        {
            return SignedNumber(value * 100f, "0.##") + "%";
        }

        private static string SignedNumber(float value, string format)
        {
            return (value >= 0f ? "+" : "") + value.ToString(format);
        }

        private static Color DeltaColor(float delta, bool higherIsBetter)
        {
            if (Mathf.Abs(delta) < 0.0001f)
            {
                return new Color(0.82f, 0.82f, 0.82f);
            }
            bool better = higherIsBetter ? delta > 0f : delta < 0f;
            return better ? new Color(0.45f, 0.86f, 0.48f) : new Color(0.95f, 0.38f, 0.34f);
        }

        private void RestoreSessionState()
        {
            loadedWeapon = sessionLoadedWeapon;
            loadedAmmoChoice = sessionLoadedAmmoChoice;
            loadedWeaponSwayFactor = sessionLoadedWeaponSwayFactor;
            selectedQuality = sessionSelectedQuality;
            loadedWeaponHasQuality = sessionLoadedWeaponHasQuality;
            loadedWeaponIsTurret = sessionLoadedWeaponIsTurret;
            loadedWeaponHasBipod = sessionLoadedWeaponHasBipod;
            loadedWeaponHasAttachments = sessionLoadedWeaponHasAttachments;
            selectedBipodDeployed = sessionSelectedBipodDeployed;
            selectedAttachments = new List<AttachmentDef>(sessionSelectedAttachments ?? new List<AttachmentDef>());
            loadedShooterLabel = sessionLoadedShooterLabel;
            loadedProjectileWarning = sessionLoadedProjectileWarning;
            loadedDamageProfile = sessionLoadedDamageProfile ?? DamageProfile.Empty;
            comparisonSnapshot = sessionComparisonSnapshot;
            ballisticDisplayMode = sessionBallisticDisplayMode;
            ballisticSampleIndex = sessionBallisticSampleIndex;
            RefreshBurstModes(input.BurstShots);
        }

        private void StoreSessionState()
        {
            sessionLoadedWeapon = loadedWeapon;
            sessionLoadedAmmoChoice = loadedAmmoChoice;
            sessionLoadedWeaponSwayFactor = loadedWeaponSwayFactor;
            sessionSelectedQuality = selectedQuality;
            sessionLoadedWeaponHasQuality = loadedWeaponHasQuality;
            sessionLoadedWeaponIsTurret = loadedWeaponIsTurret;
            sessionLoadedWeaponHasBipod = loadedWeaponHasBipod;
            sessionLoadedWeaponHasAttachments = loadedWeaponHasAttachments;
            sessionSelectedBipodDeployed = selectedBipodDeployed;
            sessionSelectedAttachments = new List<AttachmentDef>(selectedAttachments ?? new List<AttachmentDef>());
            sessionLoadedShooterLabel = loadedShooterLabel;
            sessionLoadedProjectileWarning = loadedProjectileWarning;
            sessionLoadedDamageProfile = loadedDamageProfile ?? DamageProfile.Empty;
            sessionComparisonSnapshot = comparisonSnapshot;
            sessionBallisticDisplayMode = ballisticDisplayMode;
            sessionBallisticSampleIndex = ballisticSampleIndex;
        }

        private void EnsureCalculated()
        {
            // DoWindowContents 会频繁调用；只有输入签名变化或玩家手动重算时才刷新结果。
            string signature = BuildInputSignature();
            if (!forceRecalculate && cachedResult != null && cachedSignature == signature)
            {
                return;
            }

            cachedResult = HitChanceCalculator.Calculate(input);
            cachedDistanceCurve = input.AnalysisMode == HitChanceAnalysisMode.DistanceCurve
                ? HitChanceCalculator.BuildDistanceCurve(input)
                : null;
            cachedBallisticDistribution = input.AnalysisMode == HitChanceAnalysisMode.BallisticDistribution
                ? HitChanceCalculator.BuildBallisticDistribution(input)
                : null;
            cachedSignature = BuildInputSignature();
            forceRecalculate = false;
        }

        private void MarkDirtyIfInputsChanged()
        {
            if (cachedSignature != BuildInputSignature())
            {
                forceRecalculate = true;
            }
        }

        private string BuildInputSignature()
        {
            // 签名只收录会影响结果或图表的输入。新增字段时一定要同步加入这里，
            // 否则 UI 会继续显示旧缓存，表现为“改了数值但结果没变”。
            return BuildInputSignature(input, true);
        }

        private static bool IsPlainDefaultInput(HitChanceInputs value)
        {
            return BuildInputSignature(value, false) == BuildInputSignature(new HitChanceInputs(), false);
        }

        private static string BuildInputSignature(HitChanceInputs value, bool includeObstacleVersion)
        {
            string signature = Join(
                value.DistanceCells,
                value.MaxRangeCells,
                value.ShotSpeedCellsPerSecond,
                value.ShotHeightCells,
                value.ShooterMaxHeightCells,
                value.GravityFactor,
                value.InstantProjectile ? 1 : 0,
                value.InstantProjectileIgnoresMechanicalSpread ? 1 : 0,
                value.TargetHeightMeters,
                value.TargetWidthMeters,
                value.TargetArmorSharp,
                value.TargetArmorBlunt,
                value.TargetArmorHeat,
                value.TargetArmorElectric,
                (int)value.TargetMode,
                value.SwayDegrees,
                value.SpreadDegrees,
                value.RecoilAmount,
                value.ShootingAccuracy,
                value.AimingAccuracy,
                value.SightsEfficiency,
                value.AimingDelayFactor,
                value.ReloadSpeed,
                value.ReloadFactor,
                value.NightVisionEfficiency,
                (int)value.AimMode,
                value.ShooterSuppressed ? 1 : 0,
                value.Darkness,
                value.WeatherError,
                value.SmokeDensity,
                value.TargetMoveSpeedCellsPerSecond,
                value.TargetMoveDirectionDegrees,
                value.BlindFiring ? 1 : 0,
                value.CircularMissRadiusCells,
                value.IndirectFireShiftCells,
                value.BurstShots,
                value.Rpm,
                value.SustainedShotsPerSecond,
                value.MagazineShots,
                value.FireWarmupSeconds,
                value.FireCooldownSeconds,
                value.ReloadSeconds,
                value.FasterRepeatShots ? 1 : 0,
                value.Timeline60SecondMode ? 1 : 0,
                value.ShooterThingId,
                value.SwayStartTick,
                value.Samples,
                value.Seed,
                (int)value.AnalysisMode,
                value.AnalysisSamples);
            return includeObstacleVersion ? Join(signature, HitChanceObstacleBridge.Version) : signature;
        }

        private static string Join(params object[] values)
        {
            string result = "";
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    result += "|";
                }
                if (values[i] is float floatValue)
                {
                    result += floatValue.ToString("R", CultureInfo.InvariantCulture);
                }
                else
                {
                    result += Convert.ToString(values[i], CultureInfo.InvariantCulture);
                }
            }
            return result;
        }

        private static HitChanceInputs CloneInputs(HitChanceInputs source)
        {
            var clone = new HitChanceInputs();
            CopyInputs(source, clone);
            return clone;
        }

        private static void CopyInputs(HitChanceInputs source, HitChanceInputs target)
        {
            target.DistanceCells = source.DistanceCells;
            target.MaxRangeCells = source.MaxRangeCells;
            target.ShotSpeedCellsPerSecond = source.ShotSpeedCellsPerSecond;
            target.ShotHeightCells = source.ShotHeightCells;
            target.ShooterMaxHeightCells = source.ShooterMaxHeightCells;
            target.GravityFactor = source.GravityFactor;
            target.InstantProjectile = source.InstantProjectile;
            target.InstantProjectileIgnoresMechanicalSpread = source.InstantProjectileIgnoresMechanicalSpread;
            target.TargetHeightMeters = source.TargetHeightMeters;
            target.TargetWidthMeters = source.TargetWidthMeters;
            target.TargetArmorSharp = source.TargetArmorSharp;
            target.TargetArmorBlunt = source.TargetArmorBlunt;
            target.TargetArmorHeat = source.TargetArmorHeat;
            target.TargetArmorElectric = source.TargetArmorElectric;
            target.TargetMode = source.TargetMode;
            target.SwayDegrees = source.SwayDegrees;
            target.SpreadDegrees = source.SpreadDegrees;
            target.RecoilAmount = source.RecoilAmount;
            target.ShootingAccuracy = source.ShootingAccuracy;
            target.AimingAccuracy = source.AimingAccuracy;
            target.SightsEfficiency = source.SightsEfficiency;
            target.AimingDelayFactor = source.AimingDelayFactor;
            target.ReloadSpeed = source.ReloadSpeed;
            target.ReloadFactor = source.ReloadFactor;
            target.NightVisionEfficiency = source.NightVisionEfficiency;
            target.AimMode = source.AimMode;
            target.ShooterSuppressed = source.ShooterSuppressed;
            target.Darkness = source.Darkness;
            target.WeatherError = source.WeatherError;
            target.SmokeDensity = source.SmokeDensity;
            target.TargetMoveSpeedCellsPerSecond = source.TargetMoveSpeedCellsPerSecond;
            target.TargetMoveDirectionDegrees = source.TargetMoveDirectionDegrees;
            target.BlindFiring = source.BlindFiring;
            target.CircularMissRadiusCells = source.CircularMissRadiusCells;
            target.IndirectFireShiftCells = source.IndirectFireShiftCells;
            target.BurstShots = source.BurstShots;
            target.Rpm = source.Rpm;
            target.SustainedShotsPerSecond = source.SustainedShotsPerSecond;
            target.MagazineShots = source.MagazineShots;
            target.FireWarmupSeconds = source.FireWarmupSeconds;
            target.FireCooldownSeconds = source.FireCooldownSeconds;
            target.ReloadSeconds = source.ReloadSeconds;
            target.FasterRepeatShots = source.FasterRepeatShots;
            target.Timeline60SecondMode = source.Timeline60SecondMode;
            target.ShooterThingId = source.ShooterThingId;
            target.SwayStartTick = source.SwayStartTick;
            target.Samples = source.Samples;
            target.Seed = source.Seed;
            target.AnalysisMode = source.AnalysisMode;
            target.AnalysisSamples = source.AnalysisSamples;
        }

        private static string AxisLabel(HitChanceLimitAxis axis)
        {
            switch (axis)
            {
                case HitChanceLimitAxis.Horizontal:
                    return "CEHCC_AxisHorizontal".Translate();
                case HitChanceLimitAxis.Vertical:
                    return "CEHCC_AxisVertical".Translate();
                default:
                    return "CEHCC_AxisMixed".Translate();
            }
        }

        private static string SaturationLabel(HitChanceResult result)
        {
            if (result.HorizontalSaturated && result.VerticalSaturated)
            {
                return "CEHCC_SaturatedBoth".Translate();
            }
            if (result.HorizontalSaturated)
            {
                return "CEHCC_SaturatedHorizontal".Translate();
            }
            if (result.VerticalSaturated)
            {
                return "CEHCC_SaturatedVertical".Translate();
            }
            return "CEHCC_SaturatedNone".Translate();
        }

        private string TargetModeForcedReason()
        {
            if (input.AimMode == HitChanceAimMode.SuppressFire)
            {
                return "CEHCC_TargetModeForcedSuppress".Translate();
            }
            return "CEHCC_TargetModeForcedSkill".Translate(input.ShootingAccuracy.ToString("0.###"));
        }

        private string ResultHint(HitChanceResult result)
        {
            if (input.BurstShots > 1 && input.RecoilAmount > 0f && result.MonteCarloPerShot != null && result.MonteCarloPerShot.Length > 1 && result.MonteCarloPerShot[0] > result.MonteCarloPerShot[1] + 0.15f)
            {
                return "CEHCC_HintBurstRecoil".Translate();
            }
            if (result.HorizontalSaturated && !result.VerticalSaturated)
            {
                return "CEHCC_HintVerticalLimited".Translate();
            }
            if (result.VerticalSaturated && !result.HorizontalSaturated)
            {
                return "CEHCC_HintHorizontalLimited".Translate();
            }
            if (result.VerticalSaturated && result.RangeErrorCells > 0.01f)
            {
                return "CEHCC_HintRangeVerticalSaturated".Translate();
            }
            if (input.Darkness <= 0.001f && input.WeatherError <= 0.001f && input.SmokeDensity <= 0.001f && input.TargetMoveSpeedCellsPerSecond <= 0.001f)
            {
                return "CEHCC_HintNoVisibilityInputs".Translate();
            }
            if (result.MonteCarloSingle >= 0.995f)
            {
                return "CEHCC_HintSingleSaturated".Translate();
            }
            if (result.MonteCarloSingle <= 0.05f)
            {
                return "CEHCC_HintVeryLow".Translate();
            }
            return "CEHCC_HintMixed".Translate();
        }
    }

    internal sealed class Dialog_CEHitChanceGlossary : Window
    {
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(720f, 640f);

        public Dialog_CEHitChanceGlossary()
        {
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "CEHCC_GlossaryTitle".Translate());
            Text.Font = GameFont.Small;

            Rect outRect = new Rect(inRect.x, inRect.y + 42f, inRect.width, inRect.height - 42f);
            string body = "CEHCC_GlossaryBody".Translate();
            float bodyWidth = outRect.width - 28f;
            float bodyHeight = Mathf.Max(outRect.height, Text.CalcHeight(body, bodyWidth) + 24f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, bodyHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, bodyWidth, bodyHeight), body);
            Widgets.EndScrollView();
        }
    }
}
