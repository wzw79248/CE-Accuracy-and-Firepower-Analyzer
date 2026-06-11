using UnityEngine;
using Verse;

namespace CEHitChanceCalculator
{
    internal sealed class Dialog_CEHitChanceTools : Window
    {
        public override Vector2 InitialSize => new Vector2(520f, 300f);

        public Dialog_CEHitChanceTools()
        {
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "CEHCC_ToolsTitle".Translate());
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(inRect.x, inRect.y + 38f, inRect.width, 48f), "CEHCC_ToolsDescription".Translate());

            CEHitChanceCalculatorSettings settings = CEHitChanceCalculatorMod.Settings;
            if (settings == null)
            {
                return;
            }

            bool oldFireField = settings.ShowFireFieldGizmo;
            bool oldTargetProfile = settings.ShowTargetProfileGizmo;
            bool oldAimDebug = settings.ShowAimDebugGizmo;
            bool oldObstacleCollector = settings.ShowObstacleCollectorGizmo;

            Rect listRect = new Rect(inRect.x, inRect.y + 96f, inRect.width, inRect.height - 96f);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(listRect);
            listing.CheckboxLabeled("CEHCC_ToolShowFireFieldGizmo".Translate(), ref settings.ShowFireFieldGizmo);
            listing.CheckboxLabeled("CEHCC_ToolShowTargetProfileGizmo".Translate(), ref settings.ShowTargetProfileGizmo);
            listing.CheckboxLabeled("CEHCC_ToolShowAimDebugGizmo".Translate(), ref settings.ShowAimDebugGizmo);
            listing.CheckboxLabeled("CEHCC_ToolShowObstacleCollectorGizmo".Translate(), ref settings.ShowObstacleCollectorGizmo);
            listing.End();

            if (oldFireField != settings.ShowFireFieldGizmo
                || oldTargetProfile != settings.ShowTargetProfileGizmo
                || oldAimDebug != settings.ShowAimDebugGizmo
                || oldObstacleCollector != settings.ShowObstacleCollectorGizmo)
            {
                settings.Write();
            }
        }
    }
}
