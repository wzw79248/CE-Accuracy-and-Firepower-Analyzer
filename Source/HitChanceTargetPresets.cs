namespace CEHitChanceCalculator
{
    public sealed class HitChanceTargetPreset
    {
        public readonly string LabelKey;
        public readonly float HeightMeters;
        public readonly float WidthMeters;
        public readonly float ArmorSharp;
        public readonly float ArmorBlunt;
        public readonly float ArmorHeat;
        public readonly float ArmorElectric;

        public HitChanceTargetPreset(string labelKey, float heightMeters, float widthMeters, float armorSharp, float armorBlunt, float armorHeat = 0f, float armorElectric = 0f)
        {
            LabelKey = labelKey;
            HeightMeters = heightMeters;
            WidthMeters = widthMeters;
            ArmorSharp = armorSharp;
            ArmorBlunt = armorBlunt;
            ArmorHeat = armorHeat;
            ArmorElectric = armorElectric;
        }
    }

    public static class HitChanceTargetPresets
    {
        public static readonly HitChanceTargetPreset[] All =
        {
            new HitChanceTargetPreset("CEHCC_TargetPresetHumanUnarmored", 1.75f, 0.875f, 0.2f, 0.05f, 0.008f),
            new HitChanceTargetPreset("CEHCC_TargetPresetHumanFlakVest", 1.75f, 0.875f, 8.2f, 12.05f, 0.008f),
            new HitChanceTargetPreset("CEHCC_TargetPresetHumanScoutArmor", 1.75f, 0.875f, 16.2f, 34.05f, 0.468f),
            new HitChanceTargetPreset("CEHCC_TargetPresetDmsTarbosaurus", 4.266f, 3.719f, 40.8f, 131.75f, 0.8f, 0.5f),
            new HitChanceTargetPreset("CEHCC_TargetPresetMechScyther", 2.584f, 1.518f, 4f, 6f),
            new HitChanceTargetPreset("CEHCC_TargetPresetMechCentipede", 3.281f, 3.527f, 20f, 45f, 0.25f)
        };

        public static HitChanceTargetPreset GetClamped(int index)
        {
            if (All.Length == 0)
            {
                return null;
            }

            if (index < 0)
            {
                index = 0;
            }
            else if (index >= All.Length)
            {
                index = All.Length - 1;
            }

            return All[index];
        }
    }
}
