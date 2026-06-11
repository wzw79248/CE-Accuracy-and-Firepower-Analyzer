using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;

namespace CEHitChanceCalculator
{
    internal sealed class Dialog_LineOfFireObstacleEditor : Window
    {
        private sealed class ObstaclePreset
        {
            public readonly string LabelKey;
            public readonly float DepthCells;
            public readonly float WidthCells;
            public readonly float MinHeightCells;
            public readonly float MaxHeightCells;

            public ObstaclePreset(string labelKey, float depthCells, float widthCells, float minHeightCells, float maxHeightCells)
            {
                LabelKey = labelKey;
                DepthCells = depthCells;
                WidthCells = widthCells;
                MinHeightCells = minHeightCells;
                MaxHeightCells = maxHeightCells;
            }
        }

        private sealed class ObstacleBuffers
        {
            public string Distance;
            public string Depth;
            public string Offset;
            public string Width;
            public string MinHeight;
            public string MaxHeight;

            public ObstacleBuffers(LineOfFireObstacle obstacle)
            {
                Distance = Format(obstacle.DistanceCells);
                Depth = Format(obstacle.HalfDepthCells * 2f);
                Offset = Format(obstacle.CenterOffsetCells);
                Width = Format(obstacle.HalfWidthCells * 2f);
                MinHeight = Format(obstacle.MinHeightCells);
                MaxHeight = Format(obstacle.MaxHeightCells);
            }
        }

        private static readonly ObstaclePreset[] Presets =
        {
            new ObstaclePreset("CEHCC_ObstaclePresetLowCover", 0.6f, 1.2f, 0f, 0.55f),
            new ObstaclePreset("CEHCC_ObstaclePresetHalfCover", 0.7f, 1.2f, 0f, 0.9f),
            new ObstaclePreset("CEHCC_ObstaclePresetFullWall", 1f, 1f, 0f, 2f)
        };

        private readonly HitChanceInputs input;
        private readonly Dictionary<LineOfFireObstacle, ObstacleBuffers> buffersByObstacle = new Dictionary<LineOfFireObstacle, ObstacleBuffers>();
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(940f, 640f);

        public Dialog_LineOfFireObstacleEditor(HitChanceInputs input)
        {
            this.input = input;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            LineOfFireObstacleContext context = EnsureContext();

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "CEHCC_ObstacleEditorTitle".Translate());
            Text.Font = GameFont.Small;

            Rect toolbar = new Rect(inRect.x, inRect.y + 38f, inRect.width, 64f);
            DrawToolbar(toolbar, context);

            Rect outRect = new Rect(inRect.x, toolbar.yMax + 8f, inRect.width, inRect.height - toolbar.yMax - 8f);
            float rowHeight = 88f;
            float viewHeight = Mathf.Max(outRect.height, 32f + context.Obstacles.Count * (rowHeight + 8f));
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            float y = 0f;
            if (context.Obstacles.Count == 0)
            {
                Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "CEHCC_ObstacleEditorEmpty".Translate());
            }
            else
            {
                for (int i = 0; i < context.Obstacles.Count; i++)
                {
                    LineOfFireObstacle obstacle = context.Obstacles[i];
                    Rect row = new Rect(0f, y, viewRect.width, rowHeight);
                    if (DrawObstacleRow(row, context, obstacle, i))
                    {
                        break;
                    }

                    y += rowHeight + 8f;
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawToolbar(Rect rect, LineOfFireObstacleContext context)
        {
            bool dirty = false;
            Rect row1 = new Rect(rect.x, rect.y, rect.width, 28f);
            bool enabled = context.Enabled;
            Widgets.CheckboxLabeled(new Rect(row1.x, row1.y, 150f, row1.height), "CEHCC_ObstacleEditorEnabled".Translate(), ref enabled);
            if (enabled != context.Enabled)
            {
                context.Enabled = enabled;
                dirty = true;
            }

            float x = row1.x + 160f;
            if (Widgets.ButtonText(new Rect(x, row1.y, 120f, row1.height), "CEHCC_ObstacleAddManual".Translate()))
            {
                AddManualObstacle(context);
                return;
            }
            x += 126f;
            if (Widgets.ButtonText(new Rect(x, row1.y, 120f, row1.height), "CEHCC_ObstacleAddPreset".Translate()))
            {
                OpenPresetMenu(context);
                return;
            }
            x += 126f;
            if (Widgets.ButtonText(new Rect(x, row1.y, 88f, row1.height), "CEHCC_ObstacleClear".Translate()))
            {
                context.Obstacles.Clear();
                buffersByObstacle.Clear();
                MarkChanged();
                return;
            }

            Rect row2 = new Rect(rect.x, rect.y + 34f, rect.width, 28f);
            string status = context.AppliesTo(input)
                ? "CEHCC_ObstacleEditorApplies".Translate(context.UsableObstacleCount()).ToString()
                : "CEHCC_ObstacleEditorNotApplied".Translate().ToString();
            Widgets.Label(new Rect(row2.x, row2.y + 4f, row2.width, row2.height), status);

            if (dirty)
            {
                MarkChanged();
            }
        }

        private bool DrawObstacleRow(Rect rect, LineOfFireObstacleContext context, LineOfFireObstacle obstacle, int index)
        {
            Widgets.DrawMenuSection(rect);
            bool dirty = false;
            ObstacleBuffers buffers = BuffersFor(obstacle);

            float x = rect.x + 8f;
            float y = rect.y + 6f;
            Widgets.Label(new Rect(x, y + 3f, 44f, 24f), "#" + (index + 1));
            string label = obstacle.Label ?? "";
            string newLabel = Widgets.TextField(new Rect(x + 44f, y, 260f, 28f), label);
            if (newLabel != label)
            {
                obstacle.Label = newLabel;
                dirty = true;
            }

            string validLabel = obstacle.IsUsable()
                ? "CEHCC_ObstacleValid".Translate().ToString()
                : "CEHCC_ObstacleInvalid".Translate().ToString();
            Widgets.Label(new Rect(x + 314f, y + 3f, rect.width - 420f, 24f), validLabel);

            if (Widgets.ButtonText(new Rect(rect.xMax - 86f, y, 76f, 28f), "CEHCC_ObstacleDelete".Translate()))
            {
                context.Obstacles.RemoveAt(index);
                buffersByObstacle.Remove(obstacle);
                MarkChanged();
                return true;
            }

            float fieldY = rect.y + 36f;
            float gap = 6f;
            float columnWidth = (rect.width - 16f - gap * 5f) / 6f;
            float columnX = rect.x + 8f;

            float distance = obstacle.DistanceCells;
            DrawNumberField(new Rect(columnX, fieldY, columnWidth, 46f), "CEHCC_ObstacleDistance".Translate(), ref distance, ref buffers.Distance, 0.05f, 500f, ref dirty);
            obstacle.DistanceCells = distance;
            columnX += columnWidth + gap;

            float depth = Mathf.Max(0f, obstacle.HalfDepthCells * 2f);
            DrawNumberField(new Rect(columnX, fieldY, columnWidth, 46f), "CEHCC_ObstacleDepth".Translate(), ref depth, ref buffers.Depth, 0.01f, 50f, ref dirty);
            obstacle.HalfDepthCells = depth * 0.5f;
            columnX += columnWidth + gap;

            float offset = obstacle.CenterOffsetCells;
            DrawNumberField(new Rect(columnX, fieldY, columnWidth, 46f), "CEHCC_ObstacleOffset".Translate(), ref offset, ref buffers.Offset, -50f, 50f, ref dirty);
            obstacle.CenterOffsetCells = offset;
            columnX += columnWidth + gap;

            float width = Mathf.Max(0f, obstacle.HalfWidthCells * 2f);
            DrawNumberField(new Rect(columnX, fieldY, columnWidth, 46f), "CEHCC_ObstacleWidth".Translate(), ref width, ref buffers.Width, 0.01f, 50f, ref dirty);
            obstacle.HalfWidthCells = width * 0.5f;
            columnX += columnWidth + gap;

            float minHeight = obstacle.MinHeightCells;
            DrawNumberField(new Rect(columnX, fieldY, columnWidth, 46f), "CEHCC_ObstacleMinHeight".Translate(), ref minHeight, ref buffers.MinHeight, 0f, 10f, ref dirty);
            obstacle.MinHeightCells = minHeight;
            columnX += columnWidth + gap;

            float maxHeight = Mathf.Max(obstacle.MaxHeightCells, obstacle.MinHeightCells + 0.01f);
            DrawNumberField(new Rect(columnX, fieldY, columnWidth, 46f), "CEHCC_ObstacleMaxHeight".Translate(), ref maxHeight, ref buffers.MaxHeight, 0.01f, 10f, ref dirty);
            obstacle.MaxHeightCells = Mathf.Max(maxHeight, obstacle.MinHeightCells + 0.01f);

            if (dirty)
            {
                MarkChanged();
            }

            return false;
        }

        private static void DrawNumberField(Rect rect, string label, ref float value, ref string buffer, float min, float max, ref bool dirty)
        {
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), label);
            Text.Font = oldFont;

            float oldValue = value;
            Widgets.TextFieldNumeric(new Rect(rect.x, rect.y + 20f, rect.width, 26f), ref value, ref buffer, min, max);
            if (!NearlyEqual(oldValue, value))
            {
                dirty = true;
            }
        }

        private LineOfFireObstacleContext EnsureContext()
        {
            LineOfFireObstacleContext context = HitChanceObstacleBridge.CurrentContext;
            if (context != null)
            {
                return context;
            }

            context = CreateManualContext();
            HitChanceObstacleBridge.Set(context);
            return context;
        }

        private LineOfFireObstacleContext CreateManualContext()
        {
            return new LineOfFireObstacleContext
            {
                Enabled = true,
                ShooterThingId = input?.ShooterThingId ?? 0,
                TargetDistanceCells = Mathf.Max(0.1f, input?.DistanceCells ?? 30f),
                DistanceToleranceCells = 1f,
                SourceLabel = "CEHCC_ObstacleManualContext".Translate(),
                TargetLabel = "",
                SummaryLabel = "CEHCC_ObstacleManualContext".Translate()
            };
        }

        private void AddManualObstacle(LineOfFireObstacleContext context)
        {
            LineOfFireObstacle obstacle = new LineOfFireObstacle
            {
                Label = "CEHCC_ObstacleManualLabel".Translate(),
                DistanceCells = DefaultObstacleDistance(),
                HalfDepthCells = 0.5f,
                CenterOffsetCells = 0f,
                HalfWidthCells = 0.5f,
                MinHeightCells = 0f,
                MaxHeightCells = 1f
            };
            context.Obstacles.Add(obstacle);
            buffersByObstacle[obstacle] = new ObstacleBuffers(obstacle);
            MarkChanged();
        }

        private void OpenPresetMenu(LineOfFireObstacleContext context)
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < Presets.Length; i++)
            {
                ObstaclePreset preset = Presets[i];
                options.Add(new FloatMenuOption(preset.LabelKey.Translate(), delegate
                {
                    AddPresetObstacle(context, preset);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AddPresetObstacle(LineOfFireObstacleContext context, ObstaclePreset preset)
        {
            LineOfFireObstacle obstacle = new LineOfFireObstacle
            {
                Label = preset.LabelKey.Translate(),
                DistanceCells = DefaultObstacleDistance(),
                HalfDepthCells = preset.DepthCells * 0.5f,
                CenterOffsetCells = 0f,
                HalfWidthCells = preset.WidthCells * 0.5f,
                MinHeightCells = preset.MinHeightCells,
                MaxHeightCells = preset.MaxHeightCells
            };
            context.Obstacles.Add(obstacle);
            buffersByObstacle[obstacle] = new ObstacleBuffers(obstacle);
            MarkChanged();
        }

        private float DefaultObstacleDistance()
        {
            float distance = Mathf.Max(0.1f, input?.DistanceCells ?? 30f);
            return Mathf.Clamp(distance * 0.5f, 0.1f, Mathf.Max(0.1f, distance - 0.1f));
        }

        private ObstacleBuffers BuffersFor(LineOfFireObstacle obstacle)
        {
            if (!buffersByObstacle.TryGetValue(obstacle, out ObstacleBuffers buffers))
            {
                buffers = new ObstacleBuffers(obstacle);
                buffersByObstacle[obstacle] = buffers;
            }

            return buffers;
        }

        private static void MarkChanged()
        {
            HitChanceObstacleBridge.Touch();
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool NearlyEqual(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }
    }
}
