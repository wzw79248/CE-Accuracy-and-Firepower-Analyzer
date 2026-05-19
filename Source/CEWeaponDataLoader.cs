using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace CEHitChanceCalculator
{
    internal sealed class CEAmmoChoice
    {
        public AmmoDef Ammo;
        public ThingDef Projectile;
        public bool IsCurrent;

        public string Label
        {
            get
            {
                if (Ammo == null)
                {
                    return "CEHCC_AmmoChoiceDefault".Translate();
                }

                string projectileLabel = Projectile != null ? " / " + Projectile.LabelCap : "";
                string current = IsCurrent ? "CEHCC_AmmoChoiceCurrent".Translate() : "";
                return Ammo.LabelCap + projectileLabel + current;
            }
        }
    }

    internal enum CEBurstModeKind
    {
        Single,
        Burst,
        Auto
    }

    internal sealed class CEBurstModeChoice
    {
        public CEBurstModeKind Kind;
        public int Shots;

        public string Label
        {
            get
            {
                switch (Kind)
                {
                    case CEBurstModeKind.Single:
                        return "CEHCC_BurstModeSingle".Translate(Shots);
                    case CEBurstModeKind.Burst:
                        return "CEHCC_BurstModeShort".Translate(Shots);
                    default:
                        return "CEHCC_BurstModeAuto".Translate(Shots);
                }
            }
        }
    }

    // 武器数据读取器：把 RimWorld/CE 的实时 Thing、Def 和弹药链路转换成分析器输入。
    // 尽量使用公开 API；CE 射击模式等版本差异较大的字段才用反射，并且失败时降级而不是报红。
    internal static class CEWeaponDataLoader
    {
        // 普通爆炸沿用 CE 的伤害 * 0.3 穿透下限；secondary explosive 用更保守的经验系数。
        // 这两个值只影响报表护甲近似，不改变游戏内真实弹药。
        private const float SecondaryExplosionPenPerDamage = 0.8f;
        private const float ExplosionPenPerDamage = 0.3f;

        public static bool TryFindSelectedWeapon(out ThingWithComps weapon, out string message)
        {
            return TryFindSelectedWeapon(out weapon, out bool _, out message);
        }

        public static bool TryFindSelectedWeapon(out ThingWithComps weapon, out bool fromTurret, out string message)
        {
            // 选中对象优先级：殖民者手持武器 > CE 炮塔枪体 > 地面武器。
            // 这样玩家点炮塔或点地上武器时，按钮行为都尽量符合直觉。
            weapon = null;
            fromTurret = false;
            message = null;

            List<object> selected = Find.Selector?.SelectedObjectsListForReading;
            if (selected == null || selected.Count == 0)
            {
                message = "CEHCC_LoadWeaponNoSelection".Translate();
                return false;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is Pawn pawn && pawn.equipment?.Primary != null)
                {
                    weapon = pawn.equipment.Primary;
                    return true;
                }
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is Building_TurretGunCE turret && TryGetTurretGun(turret, out weapon))
                {
                    fromTurret = true;
                    return true;
                }
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is ThingWithComps thing && thing.GetComp<CompEquippable>() != null)
                {
                    weapon = thing;
                    return true;
                }
            }

            message = "CEHCC_LoadWeaponNoWeapon".Translate();
            return false;
        }

        public static bool TryFindSelectedShooterSource(out Thing shooter, out string message)
        {
            shooter = null;
            message = null;

            List<object> selected = Find.Selector?.SelectedObjectsListForReading;
            if (selected == null || selected.Count == 0)
            {
                message = "CEHCC_LoadShooterNoSelection".Translate();
                return false;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is Pawn pawn)
                {
                    shooter = pawn;
                    return true;
                }
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is Building_TurretGunCE turret)
                {
                    shooter = turret;
                    return true;
                }
            }

            message = "CEHCC_LoadShooterNoPawn".Translate();
            return false;
        }

        public static bool TryFindSelectedShooter(out Pawn shooter, out string message)
        {
            shooter = null;
            message = null;

            List<object> selected = Find.Selector?.SelectedObjectsListForReading;
            if (selected == null || selected.Count == 0)
            {
                message = "CEHCC_LoadShooterNoSelection".Translate();
                return false;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is Pawn pawn)
                {
                    shooter = pawn;
                    return true;
                }
            }

            message = "CEHCC_LoadShooterNoPawn".Translate();
            return false;
        }

        private static bool TryGetTurretGun(Building_TurretGunCE turret, out ThingWithComps weapon)
        {
            weapon = null;
            if (turret == null)
            {
                return false;
            }

            weapon = turret.Gun as ThingWithComps;
            return weapon?.GetComp<CompEquippable>() != null;
        }

        public static List<ThingDef> AllRangedWeaponDefs()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def != null && def.IsRangedWeapon && def.Verbs != null && def.Verbs.Any(x => x != null && !x.IsMeleeAttack))
                .OrderBy(def => def.LabelCap.ToString())
                .ToList();
        }

        public static ThingWithComps MakeWeaponForDef(ThingDef weaponDef, QualityCategory quality)
        {
            ThingDef stuff = weaponDef.MadeFromStuff ? GenStuff.DefaultStuffFor(weaponDef) : null;
            var weapon = ThingMaker.MakeThing(weaponDef, stuff) as ThingWithComps;
            SetQualityIfPossible(weapon, quality);
            return weapon;
        }

        public static List<CEAmmoChoice> AmmoChoicesFor(ThingWithComps weapon)
        {
            var choices = new List<CEAmmoChoice>();
            CompAmmoUser ammoUser = weapon.GetComp<CompAmmoUser>();
            AmmoSetDef ammoSet = ammoUser?.CurAmmoSet ?? weapon.def.GetCompProperties<CompProperties_AmmoUser>()?.ammoSet;
            if (ammoSet?.ammoTypes != null && ammoSet.ammoTypes.Count > 0)
            {
                for (int i = 0; i < ammoSet.ammoTypes.Count; i++)
                {
                    AmmoLink link = ammoSet.ammoTypes[i];
                    if (link?.projectile == null)
                    {
                        continue;
                    }

                    choices.Add(new CEAmmoChoice
                    {
                        Ammo = link.ammo,
                        Projectile = link.projectile,
                        IsCurrent = ammoUser != null && link.ammo != null && (link.ammo == ammoUser.CurrentAmmo || link.ammo == ammoUser.SelectedAmmo)
                    });
                }
            }

            if (choices.Count == 0)
            {
                choices.Add(new CEAmmoChoice
                {
                    Projectile = GetPrimaryRangedVerbProps(weapon)?.defaultProjectile
                });
            }

            return choices;
        }

        public static List<CEBurstModeChoice> BurstModeChoicesFor(ThingWithComps weapon, QualityCategory quality, bool bipodDeployed, List<AttachmentDef> attachments)
        {
            var choices = new List<CEBurstModeChoice>();
            ThingWithComps statWeapon = WeaponForStats(weapon, quality, bipodDeployed, attachments);
            VerbProperties verbProps = GetPrimaryRangedVerbProps(statWeapon);
            VerbPropertiesCE verbPropsCE = verbProps as VerbPropertiesCE;
            if (statWeapon == null || verbProps == null || !HasFireModes(statWeapon))
            {
                return choices;
            }

            int fullBurst = CalculateBurstShotCount(statWeapon, verbProps, verbPropsCE);
            int aimedBurst = CalculateAimedBurstShotCount(statWeapon, fullBurst);
            AddBurstModeChoice(choices, CEBurstModeKind.Single, fullBurst, aimedBurst);
            if (aimedBurst > 1)
            {
                AddBurstModeChoice(choices, CEBurstModeKind.Burst, fullBurst, aimedBurst);
            }
            if (fullBurst > 1)
            {
                AddBurstModeChoice(choices, CEBurstModeKind.Auto, fullBurst, aimedBurst);
            }

            return choices;
        }

        public static bool TryApplyWeapon(ThingWithComps weapon, CEAmmoChoice ammoChoice, QualityCategory quality, bool bipodDeployed, List<AttachmentDef> attachments, HitChanceInputs input, out float swayFactor, out string message)
        {
            message = null;
            swayFactor = 1f;
            ThingWithComps statWeapon = WeaponForStats(weapon, quality, bipodDeployed, attachments);
            VerbProperties verbProps = GetPrimaryRangedVerbProps(statWeapon);
            if (verbProps == null)
            {
                message = "CEHCC_LoadWeaponNoRangedVerb".Translate(weapon.LabelCap);
                return false;
            }

            ThingDef projectile = ammoChoice?.Projectile ?? verbProps.defaultProjectile;
            ProjectileProperties projectileProps = projectile?.projectile;
            ProjectilePropertiesCE projectilePropsCE = projectileProps as ProjectilePropertiesCE;
            VerbPropertiesCE verbPropsCE = verbProps as VerbPropertiesCE;
            CompProperties_BipodComp bipodProps = statWeapon.def.GetCompProperties<CompProperties_BipodComp>();

            input.MaxRangeCells = CalculateEffectiveRange(statWeapon, verbProps, projectilePropsCE, bipodProps, bipodDeployed);
            if (projectileProps != null)
            {
                input.ShotSpeedCellsPerSecond = Mathf.Max(1f, projectileProps.speed);
            }
            input.GravityFactor = projectilePropsCE?.gravityFactor ?? 1f;
            input.SpreadDegrees = CalculateSpread(statWeapon, projectilePropsCE);
            input.RecoilAmount = CalculateRecoil(statWeapon, verbPropsCE, projectilePropsCE, bipodProps, bipodDeployed);
            input.SightsEfficiency = Mathf.Max(0.02f, statWeapon.GetStatValue(CE_StatDefOf.SightsEfficiency));
            input.ReloadFactor = Mathf.Max(0.01f, statWeapon.GetStatValue(CE_StatDefOf.CE_RangedWeapon_ReloadFactor));
            input.NightVisionEfficiency = Mathf.Max(input.NightVisionEfficiency, Mathf.Clamp01(statWeapon.GetStatValue(CE_StatDefOf.NightVisionEfficiency_Weapon)));
            swayFactor = statWeapon.GetStatValue(CE_StatDefOf.SwayFactor);
            input.SwayDegrees = HitChanceCalculator.CalculateSwayAmplitude(input.ShootingAccuracy, swayFactor);
            input.BurstShots = CalculateBurstShotCount(statWeapon, verbProps, verbPropsCE);
            input.Rpm = CalculateRpm(statWeapon, verbProps, verbPropsCE);
            ApplySustainedFireTiming(statWeapon, verbProps, verbPropsCE, projectilePropsCE, input);

            string ammoLabel = ammoChoice?.Ammo != null ? ammoChoice.Ammo.LabelCap.ToString() : "CEHCC_AmmoChoiceDefault".Translate().ToString();
            message = HasQuality(weapon)
                ? "CEHCC_LoadWeaponSuccess".Translate(weapon.LabelCap, ammoLabel, QualityLabel(quality))
                : "CEHCC_LoadWeaponSuccessNoQuality".Translate(weapon.LabelCap, ammoLabel);
            return true;
        }

        public static DamageProfile DamageProfileFor(ThingWithComps weapon, CEAmmoChoice ammoChoice, QualityCategory quality, bool bipodDeployed, List<AttachmentDef> attachments)
        {
            // 伤害拆分只收集报表需要的纸面值：直击、范围、破片分开记录。
            // 破片是独立弹丸云，目前只列纸面量，不进入护甲后直击伤害。
            ThingWithComps statWeapon = WeaponForStats(weapon, quality, bipodDeployed, attachments);
            VerbProperties verbProps = GetPrimaryRangedVerbProps(statWeapon);
            ThingDef projectile = ammoChoice?.Projectile ?? verbProps?.defaultProjectile;
            ProjectileProperties projectileProps = projectile?.projectile;
            ProjectilePropertiesCE projectilePropsCE = projectileProps as ProjectilePropertiesCE;
            if (statWeapon == null || projectileProps == null)
            {
                return DamageProfile.Empty;
            }

            int pelletCount = Mathf.Max(1, projectilePropsCE?.pelletCount ?? 1);
            int directDamage = Mathf.Max(0, projectileProps.GetDamageAmount(statWeapon, null));
            bool hasExplosion = projectileProps.explosionRadius > 0f;
            var profile = new DamageProfile
            {
                SourceLabel = projectile != null ? projectile.LabelCap.ToString() : "",
                DirectDamagePerShot = hasExplosion ? 0f : directDamage * pelletCount,
                DirectLabel = hasExplosion ? "CEHCC_DamageNone".Translate().ToString() : DirectDamageLabel(projectileProps, pelletCount)
            };
            if (!hasExplosion && directDamage > 0)
            {
                profile.DirectLines.Add(MakeDamageLine(
                    profile.DirectLabel,
                    directDamage * pelletCount,
                    projectileProps.damageDef,
                    projectilePropsCE,
                    pelletCount,
                    true));
            }

            if (hasExplosion)
            {
                profile.RangeLines.Add(MakeExplosionDamageLine(
                    "CEHCC_DamageExplosionLine".Translate(projectileProps.damageDef?.LabelCap ?? "CEHCC_DamageUnknown".Translate(), projectileProps.explosionRadius.ToString("0.##")),
                    directDamage,
                    projectileProps.damageDef,
                    projectilePropsCE,
                    true));
            }

            CompProperties_ExplosiveCE explosiveComp = projectile?.GetCompProperties<CompProperties_ExplosiveCE>();
            if (explosiveComp != null && explosiveComp.explosiveRadius > 0f)
            {
                float explosiveDamage = explosiveComp.damageAmountBase > 0f
                    ? explosiveComp.damageAmountBase
                    : Mathf.Max(0, explosiveComp.explosiveDamageType?.defaultDamage ?? 0);
                if (explosiveDamage > 0f)
                {
                    DamageDef damageDef = explosiveComp.explosiveDamageType ?? DamageDefOf.Bomb;
                    profile.RangeLines.Add(MakeExplosionDamageLine(
                        "CEHCC_DamageCompExplosionLine".Translate(damageDef.LabelCap, explosiveComp.explosiveRadius.ToString("0.##")),
                        explosiveDamage,
                        damageDef,
                        null,
                        true));
                }
            }

            if (projectilePropsCE?.secondaryDamage != null)
            {
                // CE secondaryDamage 属于“直击附加伤害”，例如 APHE 的附带炸伤。
                // 它不是大口径破甲弹那类范围次级爆炸，因此按主体弹丸命中率计入直击侧。
                for (int i = 0; i < projectilePropsCE.secondaryDamage.Count; i++)
                {
                    SecondaryDamage secondary = projectilePropsCE.secondaryDamage[i];
                    if (secondary == null || secondary.def == null || secondary.amount <= 0)
                    {
                        continue;
                    }

                    float chance = Mathf.Clamp01(secondary.chance <= 0f ? 1f : secondary.chance);
                    string label = "CEHCC_DamageSecondaryLine".Translate(secondary.def.LabelCap, Percent(chance));
                    if (secondary.def.isExplosive)
                    {
                        ArmorDamageKind explosiveKind = ExplosiveArmorKindFor(secondary.def);
                        AddDirectDamage(
                            profile,
                            label,
                            secondary.amount * chance * pelletCount,
                            secondary.def,
                            null,
                            pelletCount,
                            explosiveKind,
                            0f,
                            secondary.amount * SecondaryExplosionPenPerDamage,
                            secondary.amount,
                            chance * pelletCount);
                    }
                    else if (secondary.def.armorCategory == DamageArmorCategoryDefOf.Sharp)
                    {
                        AddDirectDamage(
                            profile,
                            label,
                            secondary.amount * chance * pelletCount,
                            secondary.def,
                            projectilePropsCE,
                            pelletCount,
                            ArmorDamageKind.Sharp,
                            Mathf.Max(0f, projectilePropsCE?.armorPenetrationSharp ?? secondary.def.defaultArmorPenetration),
                            Mathf.Max(0f, projectilePropsCE?.armorPenetrationBlunt ?? secondary.def.defaultArmorPenetration),
                            secondary.amount,
                            chance * pelletCount);
                    }
                    else
                    {
                        AddDirectDamage(
                            profile,
                            label,
                            secondary.amount * chance * pelletCount,
                            secondary.def,
                            null,
                            pelletCount,
                            ArmorKindFor(secondary.def),
                            0f,
                            0f,
                            secondary.amount,
                            chance * pelletCount);
                    }
                }
            }

            if (projectileProps.extraDamages != null)
            {
                // 原版 extraDamages 可能是附加直击，也可能是爆炸效果。
                // 这里按 DamageDef 是否 explosive 分流，避免把范围伤害混进直击护甲后伤害。
                for (int i = 0; i < projectileProps.extraDamages.Count; i++)
                {
                    ExtraDamage extra = projectileProps.extraDamages[i];
                    if (extra == null || extra.def == null || extra.amount <= 0f)
                    {
                        continue;
                    }

                    float chance = Mathf.Clamp01(extra.chance <= 0f ? 1f : extra.chance);
                    string label = "CEHCC_DamageExtraLine".Translate(extra.def.LabelCap, Percent(chance));
                    if (extra.def.isExplosive)
                    {
                        AddRangeDamage(profile, label, extra.amount * chance * pelletCount, extra.def, null, pelletCount);
                    }
                    else
                    {
                        AddDirectDamage(profile, label, extra.amount * chance * pelletCount, extra.def, null, pelletCount);
                    }
                }
            }

            CompProperties_Fragments fragments = projectile?.GetCompProperties<CompProperties_Fragments>();
            if (fragments?.fragments != null)
            {
                // CE 破片的命中分布与主体弹丸不同。当前报表只显示纸面破片量，
                // 不把破片假装成“必然沿主体弹道命中目标”的伤害。
                for (int i = 0; i < fragments.fragments.Count; i++)
                {
                    ThingDefCountClass fragment = fragments.fragments[i];
                    ThingDef fragmentDef = fragment?.thingDef;
                    ProjectileProperties fragmentProps = fragmentDef?.projectile;
                    if (fragmentDef == null || fragmentProps == null || fragment.count <= 0)
                    {
                        continue;
                    }

                    int fragmentDamage = Mathf.Max(0, fragmentProps.GetDamageAmount((Thing)null, null));
                    if (fragmentDamage <= 0)
                    {
                        continue;
                    }

                    profile.FragmentLines.Add(new DamageLine
                    {
                        Label = "CEHCC_DamageFragmentLine".Translate(fragmentDef.LabelCap, fragment.count),
                        DamagePerShot = fragmentDamage * fragment.count,
                        UsesDirectHitChance = false
                    });
                }
            }

            return profile;
        }

        public static string ProjectileApproximationWarning(ThingWithComps weapon, CEAmmoChoice ammoChoice)
        {
            VerbProperties verbProps = GetPrimaryRangedVerbProps(weapon);
            ThingDef projectile = ammoChoice?.Projectile ?? verbProps?.defaultProjectile;
            ProjectilePropertiesCE props = projectile?.projectile as ProjectilePropertiesCE;
            if (props == null)
            {
                return null;
            }

            string reasons = "";
            AddReason(ref reasons, props.isInstant, "CEHCC_SpecialProjectileInstant".Translate());
            AddReason(ref reasons, props.pelletCount > 1, "CEHCC_SpecialProjectilePellets".Translate(props.pelletCount));
            AddReason(ref reasons, props.fuelTicks > 0 || Mathf.Abs(props.speedGain) > 0.001f, "CEHCC_SpecialProjectilePowered".Translate());
            AddReason(ref reasons, props.trajectoryWorker != null || !props.lerpPosition.NullOrEmpty(), "CEHCC_SpecialProjectileTrajectory".Translate());
            AddReason(ref reasons, props.shellingProps != null, "CEHCC_SpecialProjectileShelling".Translate());
            AddReason(ref reasons, props.aimHeightOffset != 0f || props.airburstDistanceOffset != 0f, "CEHCC_SpecialProjectileFuze".Translate());

            return reasons.NullOrEmpty() ? null : "CEHCC_WarnSpecialProjectile".Translate(reasons);
        }

        private static void AddDirectDamage(DamageProfile profile, string label, float damage, DamageDef damageDef, ProjectilePropertiesCE projectilePropsCE, int count)
        {
            AddDirectDamage(profile, label, damage, damageDef, projectilePropsCE, count, null, 0f, 0f, 0f, 0f);
        }

        private static void AddDirectDamage(DamageProfile profile, string label, float damage, DamageDef damageDef, ProjectilePropertiesCE projectilePropsCE, int count, ArmorDamageKind? armorKind, float armorPenetrationSharp, float armorPenetrationBlunt, float armorDamagePerEvent, float armorEventMultiplier)
        {
            if (profile == null || damage <= 0f)
            {
                return;
            }

            profile.DirectDamagePerShot += damage;
            profile.DirectLines.Add(MakeDamageLine(label, damage, damageDef, projectilePropsCE, count, true, armorKind, armorPenetrationSharp, armorPenetrationBlunt, armorDamagePerEvent, armorEventMultiplier));
            if (profile.DirectLabel.NullOrEmpty() || profile.DirectLabel == "CEHCC_DamageNone".Translate().ToString())
            {
                profile.DirectLabel = label;
            }
            else
            {
                profile.DirectLabel += " + " + label;
            }
        }

        private static void AddRangeDamage(DamageProfile profile, string label, float damage, DamageDef damageDef, ProjectilePropertiesCE projectilePropsCE, int count)
        {
            AddRangeDamage(profile, label, damage, damageDef, projectilePropsCE, count, null, 0f, 0f, 0f, 0f);
        }

        private static void AddRangeDamage(DamageProfile profile, string label, float damage, DamageDef damageDef, ProjectilePropertiesCE projectilePropsCE, int count, ArmorDamageKind? armorKind, float armorPenetrationSharp, float armorPenetrationBlunt, float armorDamagePerEvent, float armorEventMultiplier)
        {
            if (profile == null || damage <= 0f)
            {
                return;
            }

            profile.RangeLines.Add(MakeDamageLine(label, damage, damageDef, projectilePropsCE, count, true, armorKind, armorPenetrationSharp, armorPenetrationBlunt, armorDamagePerEvent, armorEventMultiplier));
        }

        private static DamageLine MakeDamageLine(string label, float damage, DamageDef damageDef, ProjectilePropertiesCE projectilePropsCE, int count, bool usesDirectHitChance)
        {
            return MakeDamageLine(label, damage, damageDef, projectilePropsCE, count, usesDirectHitChance, null, 0f, 0f, 0f, 0f);
        }

        private static DamageLine MakeDamageLine(string label, float damage, DamageDef damageDef, ProjectilePropertiesCE projectilePropsCE, int count, bool usesDirectHitChance, ArmorDamageKind? armorKind, float armorPenetrationSharp, float armorPenetrationBlunt, float armorDamagePerEvent, float armorEventMultiplier)
        {
            ArmorDamageKind kind = armorKind ?? ArmorKindFor(damageDef);
            float defaultPen = Mathf.Max(0f, damageDef?.defaultArmorPenetration ?? 0f);
            return new DamageLine
            {
                Label = label,
                DamagePerShot = damage,
                UsesDirectHitChance = usesDirectHitChance,
                Count = Mathf.Max(1, count),
                ArmorDamagePerEvent = armorDamagePerEvent,
                ArmorEventMultiplier = armorEventMultiplier,
                ArmorKind = kind,
                ArmorPenetrationSharp = armorKind.HasValue ? Mathf.Max(0f, armorPenetrationSharp) : projectilePropsCE?.armorPenetrationSharp ?? defaultPen,
                ArmorPenetrationBlunt = armorKind.HasValue ? Mathf.Max(0f, armorPenetrationBlunt) : DefaultSecondaryPenetration(kind, projectilePropsCE, defaultPen)
            };
        }

        private static DamageLine MakeExplosionDamageLine(string label, float damage, DamageDef damageDef, ProjectilePropertiesCE projectilePropsCE, bool usesDirectHitChance)
        {
            // CE ExplosionCE 的普通爆炸穿透下限可理解为 max(伤害 * 0.3, 显式钝穿)。
            // 这只用于护甲后伤害估算；爆炸范围、遮挡和多碰撞箱暂不在本模型里展开。
            float explicitPen = Mathf.Max(0f, projectilePropsCE?.armorPenetrationBlunt ?? 0f);
            float penetration = Mathf.Max(damage * ExplosionPenPerDamage, explicitPen);
            ArmorDamageKind kind = ExplosiveArmorKindFor(damageDef);
            return MakeDamageLine(label, damage, damageDef, null, 1, usesDirectHitChance, kind, 0f, penetration, damage, 1f);
        }

        private static float DefaultSecondaryPenetration(ArmorDamageKind kind, ProjectilePropertiesCE projectilePropsCE, float defaultPen)
        {
            if (kind == ArmorDamageKind.Blunt)
            {
                return projectilePropsCE?.armorPenetrationBlunt ?? defaultPen;
            }
            if (kind == ArmorDamageKind.Heat || kind == ArmorDamageKind.Electric)
            {
                return defaultPen;
            }
            return projectilePropsCE?.armorPenetrationBlunt ?? defaultPen;
        }

        private static ArmorDamageKind ExplosiveArmorKindFor(DamageDef damageDef)
        {
            ArmorDamageKind kind = ArmorKindFor(damageDef);
            if (kind == ArmorDamageKind.Heat || kind == ArmorDamageKind.Electric)
            {
                return kind;
            }
            return ArmorDamageKind.Blunt;
        }

        private static ArmorDamageKind ArmorKindFor(DamageDef damageDef)
        {
            if (damageDef?.armorCategory == DamageArmorCategoryDefOf.Sharp)
            {
                return ArmorDamageKind.Sharp;
            }
            if (damageDef?.armorCategory != null && damageDef.armorCategory.defName == "Blunt")
            {
                return ArmorDamageKind.Blunt;
            }
            if (damageDef?.armorCategory != null && damageDef.armorCategory.defName == "Heat")
            {
                return ArmorDamageKind.Heat;
            }
            if (damageDef?.armorCategory != null && damageDef.armorCategory.defName == "Electric")
            {
                return ArmorDamageKind.Electric;
            }
            return ArmorDamageKind.None;
        }

        private static void AddReason(ref string reasons, bool condition, string label)
        {
            if (!condition)
            {
                return;
            }
            if (!reasons.NullOrEmpty())
            {
                reasons += "、";
            }
            reasons += label;
        }

        private static string DirectDamageLabel(ProjectileProperties projectileProps, int pelletCount)
        {
            string label = projectileProps.damageDef != null ? projectileProps.damageDef.LabelCap.ToString() : "CEHCC_DamageUnknown".Translate().ToString();
            if (pelletCount > 1)
            {
                label += " x" + pelletCount;
            }
            return label;
        }

        private static string Percent(float value)
        {
            return (value * 100f).ToString("0.##") + "%";
        }

        public static bool HasQuality(ThingWithComps weapon)
        {
            return weapon?.TryGetComp<CompQuality>() != null;
        }

        public static bool HasBipod(ThingWithComps weapon)
        {
            return weapon?.def.GetCompProperties<CompProperties_BipodComp>() != null;
        }

        public static HitChanceAimMode AiAimModeForWeapon(ThingWithComps weapon)
        {
            object value = ReadMember(FireModesProps(weapon), "aiAimMode");
            string mode = value?.ToString();
            if (string.Equals(mode, "Snapshot", StringComparison.OrdinalIgnoreCase))
            {
                return HitChanceAimMode.Snapshot;
            }
            if (string.Equals(mode, "SuppressFire", StringComparison.OrdinalIgnoreCase))
            {
                return HitChanceAimMode.SuppressFire;
            }
            return HitChanceAimMode.AimedShot;
        }

        public static bool IsBipodDeployed(ThingWithComps weapon)
        {
            return weapon?.TryGetComp<BipodComp>()?.IsSetUpRn ?? false;
        }

        public static bool HasAttachments(ThingWithComps weapon)
        {
            WeaponPlatform platform = weapon as WeaponPlatform;
            return platform?.Platform?.attachmentLinks != null && platform.Platform.attachmentLinks.Count > 0;
        }

        public static List<AttachmentDef> InstalledAttachments(ThingWithComps weapon)
        {
            var result = new List<AttachmentDef>();
            WeaponPlatform platform = weapon as WeaponPlatform;
            if (platform == null)
            {
                return result;
            }

            AttachmentLink[] links = platform.CurLinks;
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i]?.attachment != null)
                {
                    result.Add(links[i].attachment);
                }
            }
            return result;
        }

        public static List<AttachmentDef> AvailableAttachments(ThingWithComps weapon)
        {
            var result = new List<AttachmentDef>();
            WeaponPlatform platform = weapon as WeaponPlatform;
            if (platform?.Platform?.attachmentLinks == null)
            {
                return result;
            }

            for (int i = 0; i < platform.Platform.attachmentLinks.Count; i++)
            {
                AttachmentDef attachment = platform.Platform.attachmentLinks[i]?.attachment;
                if (attachment != null)
                {
                    result.Add(attachment);
                }
            }
            return result.OrderBy(x => x.LabelCap.ToString()).ToList();
        }

        public static bool CanAddAttachment(ThingWithComps weapon, List<AttachmentDef> selected, AttachmentDef attachment)
        {
            WeaponPlatform platform = weapon as WeaponPlatform;
            if (platform?.Platform == null || attachment == null)
            {
                return false;
            }

            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] == attachment)
                {
                    return true;
                }
                if (!platform.Platform.AttachmentsCompatible(selected[i], attachment))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool TryApplyShooter(Pawn shooter, HitChanceInputs input, float? swayFactor, out string message)
        {
            if (shooter == null)
            {
                message = "CEHCC_LoadShooterNoPawn".Translate();
                return false;
            }

            input.ShootingAccuracy = Mathf.Min(shooter.GetStatValue(StatDefOf.ShootingAccuracyPawn), 4.5f);
            input.AimingAccuracy = Mathf.Min(shooter.GetStatValue(CE_StatDefOf.AimingAccuracy), 1.5f);
            input.AimingDelayFactor = Mathf.Clamp(shooter.GetStatValue(StatDefOf.AimingDelayFactor), 0.01f, 2f);
            input.ReloadSpeed = Mathf.Max(0.001f, shooter.GetStatValue(CE_StatDefOf.ReloadSpeed));
            input.NightVisionEfficiency = Mathf.Clamp01(shooter.GetStatValue(CE_StatDefOf.NightVisionEfficiency));
            input.ShooterThingId = shooter.thingIDNumber;
            input.ShotHeightCells = new CollisionVertical(shooter).shotHeight;
            if (swayFactor.HasValue)
            {
                input.SwayDegrees = HitChanceCalculator.CalculateSwayAmplitude(input.ShootingAccuracy, swayFactor.Value);
            }

            message = "CEHCC_LoadShooterSuccess".Translate(shooter.LabelCap);
            return true;
        }

        public static bool TryApplyShooter(Thing shooterSource, HitChanceInputs input, float? swayFactor, out string message)
        {
            if (shooterSource is Pawn pawn)
            {
                return TryApplyShooter(pawn, input, swayFactor, out message);
            }

            if (shooterSource is Building_TurretGunCE turret)
            {
                return TryApplyTurretShooter(turret, input, swayFactor, out message);
            }

            message = "CEHCC_LoadShooterNoPawn".Translate();
            return false;
        }

        public static string ShooterSourceLabel(Thing shooterSource)
        {
            if (shooterSource is Building_TurretGunCE turret)
            {
                Pawn operatorPawn = CE_Utility.TryGetTurretOperator(turret);
                if (operatorPawn != null)
                {
                    return "CEHCC_ShooterLabelTurretManned".Translate(turret.LabelCap, operatorPawn.LabelCap).ToString();
                }
                if (turret.MannableComp != null)
                {
                    return "CEHCC_ShooterLabelTurretUnmanned".Translate(turret.LabelCap).ToString();
                }
                return "CEHCC_ShooterLabelTurretAuto".Translate(turret.LabelCap).ToString();
            }

            return shooterSource != null ? shooterSource.LabelCap.ToString() : "";
        }

        private static bool TryApplyTurretShooter(Building_TurretGunCE turret, HitChanceInputs input, float? swayFactor, out string message)
        {
            // 炮塔射手有两条路径：有人操作时读操作员属性；无人/自动炮塔读炮塔自身 stat。
            // 对玩家来说提示来源比硬塞成人类射手更重要。
            if (turret == null)
            {
                message = "CEHCC_LoadShooterNoPawn".Translate();
                return false;
            }

            Pawn operatorPawn = CE_Utility.TryGetTurretOperator(turret);
            if (operatorPawn != null)
            {
                input.ShootingAccuracy = Mathf.Min(operatorPawn.GetStatValue(StatDefOf.ShootingAccuracyPawn), 4.5f);
                input.AimingAccuracy = Mathf.Min(operatorPawn.GetStatValue(CE_StatDefOf.AimingAccuracy), 1.5f);
                input.ReloadSpeed = Mathf.Max(0.001f, operatorPawn.GetStatValue(CE_StatDefOf.ReloadSpeed));
                input.NightVisionEfficiency = Mathf.Clamp01(operatorPawn.GetStatValue(CE_StatDefOf.NightVisionEfficiency));
                input.ShooterThingId = operatorPawn.thingIDNumber;
                message = "CEHCC_LoadShooterTurretMannedSuccess".Translate(turret.LabelCap, operatorPawn.LabelCap);
            }
            else
            {
                input.ShootingAccuracy = Mathf.Min(turret.GetStatValue(StatDefOf.ShootingAccuracyTurret), 4.5f);
                input.AimingAccuracy = Mathf.Min(turret.GetStatValue(CE_StatDefOf.AimingAccuracy), 1.5f);
                input.ReloadSpeed = Mathf.Max(0.001f, SafeStat(turret, CE_StatDefOf.ReloadSpeed, 1f));
                input.NightVisionEfficiency = Mathf.Clamp01(SafeStat(turret, CE_StatDefOf.NightVisionEfficiency, 0f));
                input.ShooterThingId = turret.thingIDNumber;
                message = turret.MannableComp != null
                    ? "CEHCC_LoadShooterTurretUnmannedSuccess".Translate(turret.LabelCap)
                    : "CEHCC_LoadShooterTurretAutoSuccess".Translate(turret.LabelCap);
            }

            input.AimingDelayFactor = 1f;
            input.ShotHeightCells = new CollisionVertical(turret).shotHeight;
            if (swayFactor.HasValue)
            {
                input.SwayDegrees = HitChanceCalculator.CalculateSwayAmplitude(input.ShootingAccuracy, swayFactor.Value);
            }
            return true;
        }

        private static float SafeStat(Thing thing, StatDef stat, float fallback)
        {
            if (thing == null || stat == null)
            {
                return fallback;
            }

            try
            {
                return thing.GetStatValue(stat);
            }
            catch
            {
                return fallback;
            }
        }

        private static VerbProperties GetPrimaryRangedVerbProps(ThingWithComps weapon)
        {
            CompEquippable equippable = weapon.GetComp<CompEquippable>();
            if (equippable?.PrimaryVerb?.verbProps != null && !equippable.PrimaryVerb.verbProps.IsMeleeAttack)
            {
                return equippable.PrimaryVerb.verbProps;
            }

            return weapon.def.Verbs?.FirstOrDefault(x => x != null && !x.IsMeleeAttack);
        }

        private static float CalculateEffectiveRange(ThingWithComps weapon, VerbProperties verbProps, ProjectilePropertiesCE projectileProps, CompProperties_BipodComp bipodProps, bool bipodDeployed)
        {
            float weaponRangeMultiplier = weapon.GetStatValue(StatDefOf.RangedWeapon_RangeMultiplier);
            float projectileRangeMultiplier = projectileProps?.effectiveRangeMultiplier ?? 1f;
            float projectileRangeOffset = projectileProps?.effectiveRangeOffset ?? 0f;
            float combinedMultiplier = weaponRangeMultiplier + projectileRangeMultiplier - 1f;
            float verbRange = verbProps.range + ((bipodDeployed && bipodProps != null) ? bipodProps.additionalrange : 0f);
            return Mathf.Max(0f, verbRange * combinedMultiplier + projectileRangeOffset);
        }

        private static float CalculateSpread(ThingWithComps weapon, ProjectilePropertiesCE projectileProps)
        {
            float projectileSpreadMultiplier = projectileProps != null ? projectileProps.spreadMult : 0f;
            return Mathf.Max(0f, weapon.GetStatValue(CE_StatDefOf.ShotSpread) * projectileSpreadMultiplier);
        }

        private static float CalculateRecoil(ThingWithComps weapon, VerbPropertiesCE verbProps, ProjectilePropertiesCE projectileProps, CompProperties_BipodComp bipodProps, bool bipodDeployed)
        {
            if (verbProps == null)
            {
                return 0f;
            }

            float verbRecoil = verbProps.recoilAmount;
            if (bipodProps != null)
            {
                verbRecoil *= bipodDeployed ? bipodProps.recoilMulton : bipodProps.recoilMultoff;
            }
            float recoilMultiplier = weapon.GetStatValue(CE_StatDefOf.CE_RangedWeapon_RecoilMultiplier);
            float projectileMultiplier = projectileProps?.recoilMultiplier ?? 1f;
            float projectileOffset = projectileProps?.recoilOffset ?? 0f;
            float recoil = Mathf.Max(0f, verbRecoil * (recoilMultiplier + projectileMultiplier - 1f) + projectileOffset);
            if (verbProps.useEquipmentStatValues)
            {
                float statRecoil = weapon.GetStatValue(CE_StatDefOf.Recoil);
                if (statRecoil > 0f)
                {
                    recoil = statRecoil;
                }
            }
            return recoil;
        }

        private static int CalculateBurstShotCount(ThingWithComps weapon, VerbProperties verbProps, VerbPropertiesCE verbPropsCE)
        {
            float burstShotCount = verbProps.burstShotCount;
            if (verbPropsCE != null && verbPropsCE.useEquipmentStatValues)
            {
                float statBurst = weapon.GetStatValue(CE_StatDefOf.BurstShotCount);
                if (statBurst > 0f)
                {
                    burstShotCount = statBurst;
                }
            }
            return Mathf.Clamp(Mathf.RoundToInt(burstShotCount), 1, 60);
        }

        private static int CalculateAimedBurstShotCount(ThingWithComps weapon, int fullBurst)
        {
            object props = FireModesProps(weapon);
            int fallback = Mathf.Clamp(Mathf.Min(3, fullBurst), 1, 60);
            return Mathf.Clamp(ReadIntMember(props, "aimedBurstShotCount", fallback), 1, 60);
        }

        private static bool HasFireModes(ThingWithComps weapon)
        {
            return FireModesProps(weapon) != null;
        }

        private static object FireModesProps(ThingWithComps weapon)
        {
            // CE 射击模式组件在不同版本/兼容包中可能改字段名或类型名。
            // 这里只找组件本体，具体字段读取交给 ReadMember 做宽松反射。
            if (weapon?.def?.comps == null)
            {
                return null;
            }

            for (int i = 0; i < weapon.def.comps.Count; i++)
            {
                object props = weapon.def.comps[i];
                Type type = props?.GetType();
                if (type != null && (type.Name == "CompProperties_FireModes" || type.FullName == "CombatExtended.CompProperties_FireModes"))
                {
                    return props;
                }
            }
            return null;
        }

        private static void AddBurstModeChoice(List<CEBurstModeChoice> choices, CEBurstModeKind kind, int fullBurst, int aimedBurst)
        {
            int shots = 1;
            if (kind == CEBurstModeKind.Burst)
            {
                shots = aimedBurst;
            }
            else if (kind == CEBurstModeKind.Auto)
            {
                shots = fullBurst;
            }
            shots = Mathf.Clamp(shots, 1, 60);

            if (choices.Any(x => x.Shots == shots))
            {
                return;
            }

            choices.Add(new CEBurstModeChoice
            {
                Kind = kind,
                Shots = shots
            });
        }

        private static object ReadMember(object target, string name)
        {
            // 反射读取只用于兼容 CE 私有/内部字段。失败返回 null，
            // 调用方必须提供保守 fallback，不能让一次字段变化破坏整个窗口。
            if (target == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            FieldInfo field = type.GetField(name, flags);
            return field != null ? field.GetValue(target) : null;
        }

        private static int ReadIntMember(object target, string name, int fallback)
        {
            object value = ReadMember(target, name);
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static float CalculateRpm(ThingWithComps weapon, VerbProperties verbProps, VerbPropertiesCE verbPropsCE)
        {
            float ticks = verbProps.ticksBetweenBurstShots;
            if (verbPropsCE != null && verbPropsCE.useEquipmentStatValues)
            {
                float statTicks = weapon.GetStatValue(CE_StatDefOf.TicksBetweenBurstShots);
                if (statTicks > 0f)
                {
                    ticks = statTicks;
                }
            }

            return ticks > 0f ? Mathf.Round(3600f / ticks) : 600f;
        }

        private static void ApplySustainedFireTiming(ThingWithComps weapon, VerbProperties verbProps, VerbPropertiesCE verbPropsCE, ProjectilePropertiesCE projectileProps, HitChanceInputs input)
        {
            // 从真实武器和弹药读取弹匣、预热、冷却、换弹，再让 DpsCalculator 统一计算持续射速。
            // 不在这里手算 DPS，是为了让时间轴、60 秒窗口和长期 DPS 共用同一套节奏。
            input.MagazineShots = 0;
            input.FireWarmupSeconds = CalculateWarmupSeconds(weapon, verbProps, projectileProps);
            input.FireCooldownSeconds = CalculateCooldownSeconds(weapon, verbProps);
            input.ReloadSeconds = 0f;
            input.ReloadFactor = Mathf.Max(0.01f, weapon.GetStatValue(CE_StatDefOf.CE_RangedWeapon_ReloadFactor));
            input.SustainedShotsPerSecond = 0f;

            CompAmmoUser ammoUser = weapon.GetComp<CompAmmoUser>();
            CompProperties_AmmoUser ammoProps = ammoUser?.Props ?? weapon.def.GetCompProperties<CompProperties_AmmoUser>();
            if (ammoProps == null || ammoProps.magazineSize <= 0)
            {
                return;
            }

            int ammoConsumedPerShot = Mathf.Max(1, ammoProps.ammoSet?.ammoConsumedPerShot ?? 1);
            input.MagazineShots = Mathf.Max(1, ammoProps.magazineSize / ammoConsumedPerShot);
            bool ammoUserReloadIncludesProjectile = ammoUser != null;
            float reloadTime = ammoUserReloadIncludesProjectile ? ammoUser.ReloadTime : Mathf.Max(0f, weapon.GetStatValue(CE_StatDefOf.ReloadTime));
            if (reloadTime <= 0f)
            {
                reloadTime = ammoProps.reloadTime;
            }
            input.ReloadSeconds = reloadTime * (ammoUserReloadIncludesProjectile ? 1f : (projectileProps?.reloadTimeMultiplier ?? 1f));

            int ignored;
            input.SustainedShotsPerSecond = DpsCalculator.CalculateShotsPerSecond(input, out ignored);
        }

        private static float CalculateCooldownSeconds(ThingWithComps weapon, VerbProperties verbProps)
        {
            float verbCooldown = Mathf.Max(0f, verbProps.defaultCooldownTime);
            if (weapon?.def?.statBases != null && weapon.def.statBases.Any(x => x != null && x.stat == StatDefOf.RangedWeapon_Cooldown))
            {
                return Mathf.Max(0f, weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            }
            return verbCooldown;
        }

        private static float CalculateWarmupSeconds(ThingWithComps weapon, VerbProperties verbProps, ProjectilePropertiesCE projectileProps)
        {
            float warmup = Mathf.Max(0f, verbProps.warmupTime);
            float weaponMultiplier = 1f;
            if (weapon != null)
            {
                weaponMultiplier = Mathf.Max(0f, weapon.GetStatValue(StatDefOf.RangedWeapon_WarmupMultiplier));
            }

            float projectileMultiplier = projectileProps?.warmupMultiplier ?? 1f;
            float projectileOffset = projectileProps?.warmupOffset ?? 0f;
            return Mathf.Max(0f, warmup * (weaponMultiplier + projectileMultiplier - 1f) + projectileOffset);
        }

        private static ThingWithComps WeaponForStats(ThingWithComps weapon, QualityCategory quality, bool bipodDeployed, List<AttachmentDef> attachments)
        {
            // 品质、脚架和定制配件会影响 stat。需要试算时创建临时 clone，
            // 避免为了读数去修改玩家当前存档里的真实武器。
            if (weapon == null)
            {
                return null;
            }

            bool hasQuality = HasQuality(weapon);
            bool hasBipod = HasBipod(weapon);
            bool hasAttachmentConfig = HasAttachments(weapon) && attachments != null;
            if (!hasAttachmentConfig && !hasBipod && (!hasQuality || !weapon.TryGetQuality(out QualityCategory currentQuality) || currentQuality == quality))
            {
                return weapon;
            }

            var clone = ThingMaker.MakeThing(weapon.def, weapon.Stuff) as ThingWithComps;
            if (hasQuality)
            {
                SetQualityIfPossible(clone, quality);
            }
            if (hasAttachmentConfig)
            {
                SetWeaponPlatformAttachments(clone, attachments);
            }
            else
            {
                CopyWeaponPlatformAttachments(weapon, clone);
            }
            BipodComp bipod = clone?.TryGetComp<BipodComp>();
            if (bipod != null)
            {
                bipod.IsSetUpRn = bipodDeployed;
            }
            return clone ?? weapon;
        }

        private static void CopyWeaponPlatformAttachments(ThingWithComps source, ThingWithComps target)
        {
            WeaponPlatform sourcePlatform = source as WeaponPlatform;
            WeaponPlatform targetPlatform = target as WeaponPlatform;
            if (sourcePlatform == null || targetPlatform == null)
            {
                return;
            }

            targetPlatform.attachments.Clear();
            AttachmentLink[] sourceLinks = sourcePlatform.CurLinks;
            for (int i = 0; i < sourceLinks.Length; i++)
            {
                AttachmentDef attachment = sourceLinks[i]?.attachment;
                if (attachment == null)
                {
                    continue;
                }

                AttachmentLink targetLink = targetPlatform.GetLink(attachment);
                if (targetLink != null)
                {
                    targetPlatform.attachments.Add(targetLink);
                }
            }
            targetPlatform.UpdateConfiguration();
        }

        private static void SetWeaponPlatformAttachments(ThingWithComps target, List<AttachmentDef> attachments)
        {
            WeaponPlatform targetPlatform = target as WeaponPlatform;
            if (targetPlatform == null)
            {
                return;
            }

            targetPlatform.attachments.Clear();
            for (int i = 0; i < attachments.Count; i++)
            {
                AttachmentLink targetLink = targetPlatform.GetLink(attachments[i]);
                if (targetLink != null)
                {
                    targetPlatform.attachments.Add(targetLink);
                }
            }
            targetPlatform.UpdateConfiguration();
        }

        public static void SetQualityIfPossible(ThingWithComps weapon, QualityCategory quality)
        {
            CompQuality compQuality = weapon?.TryGetComp<CompQuality>();
            compQuality?.SetQuality(quality, ArtGenerationContext.Outsider);
        }

        public static string QualityLabel(QualityCategory quality)
        {
            switch (quality)
            {
                case QualityCategory.Awful:
                    return "CEHCC_QualityAwful".Translate();
                case QualityCategory.Poor:
                    return "CEHCC_QualityPoor".Translate();
                case QualityCategory.Good:
                    return "CEHCC_QualityGood".Translate();
                case QualityCategory.Excellent:
                    return "CEHCC_QualityExcellent".Translate();
                case QualityCategory.Masterwork:
                    return "CEHCC_QualityMasterwork".Translate();
                case QualityCategory.Legendary:
                    return "CEHCC_QualityLegendary".Translate();
                default:
                    return "CEHCC_QualityNormal".Translate();
            }
        }
    }
}
