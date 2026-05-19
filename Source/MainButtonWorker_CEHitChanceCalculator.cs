using RimWorld;
using Verse;

namespace CEHitChanceCalculator
{
    public sealed class MainButtonWorker_CEHitChanceCalculator : MainButtonWorker
    {
        public override void Activate()
        {
            Find.WindowStack.Add(new Dialog_CEHitChanceCalculator(CEHitChanceCalculatorMod.Settings.Inputs));
        }
    }
}
