using BlueprintCore.Utils;
using CodexLib;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.EquipmentEnchants;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using static HolyVindicator.Mechanics;
using Helper = CodexLib.Helper;

namespace HolyVindicator
{
    public static partial class Extensions
    {
        public static bool IsOf(this ChannelEnergyEngine.ChannelType this_type, ChannelEnergyEngine.ChannelType channel_type)
        {
            return (this_type & channel_type) != 0;
        }

        public static bool IsStrictlyOf(this ChannelEnergyEngine.ChannelType this_type, ChannelEnergyEngine.ChannelType channel_type)
        {
            return (~this_type & channel_type) == 0;
        }


        public static bool IsNotOf(this ChannelEnergyEngine.ChannelType this_type, ChannelEnergyEngine.ChannelType channel_type)
        {
            return (this_type & channel_type) == 0;
        }


        public static bool IsOnly(this ChannelEnergyEngine.ChannelType this_type, ChannelEnergyEngine.ChannelType channel_type)
        {
            return (this_type & ~channel_type) == 0;
        }


        public static bool IsBase(this ChannelEnergyEngine.ChannelType this_type)
        {

            return this_type == ChannelEnergyEngine.ChannelType.PositiveHeal || this_type == ChannelEnergyEngine.ChannelType.NegativeHeal
                   || this_type == ChannelEnergyEngine.ChannelType.PositiveHarm || this_type == ChannelEnergyEngine.ChannelType.NegativeHarm;
        }
    }

    public static class ChannelEnergyEngine
    {
        [Flags]
        public enum ChannelProperty
        {
            None = 0,
            Bloodfire = 1,
            Bloodrain = 2,
            All = ~None
        }

        [Flags]
        public enum ChannelType
        {
            None = 0,
            Positive = 1,
            Negative = 2,
            Heal = 4,
            Harm = 8,
            Smite = 16,
            Quick = 32,
            BackToTheGrave = 64,
            Cone = 128,
            Line = 256,
            HolyVindicatorShield = 512,
            Form = Cone | Line,
            PositiveHeal = Positive | Heal,
            PositiveHarm = Positive | Harm,
            NegativeHeal = Negative | Heal,
            NegativeHarm = Negative | Harm,
            SwiftPositiveChannel = 1024,
            All = ~None
        }

        public class ChannelEntry
        {
            public readonly BlueprintAbility ability;
            public readonly BlueprintAbility base_ability;
            public readonly BlueprintFeature parent_feature;
            public readonly ChannelType channel_type;
            internal ChannelProperty properties;
            public readonly BlueprintUnitFact[] required_facts = new BlueprintUnitFact[0];

            public ChannelEntry(string ability_guid, string parent_feature_guid, ChannelType type, ChannelProperty property, params BlueprintUnitFact[] req_facts)
            {
                ability = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(ability_guid);
                parent_feature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(parent_feature_guid);
                base_ability = ability.Parent;
                channel_type = type;
                properties = property;
                required_facts = req_facts;
            }

            public ChannelEntry(BlueprintAbility channel_ability, BlueprintFeature channel_parent_feature, ChannelType type, ChannelProperty property, params BlueprintUnitFact[] req_facts)
            {
                ability = channel_ability;
                parent_feature = channel_parent_feature;
                base_ability = ability.Parent;
                channel_type = type;
                properties = property;
                required_facts = req_facts;
            }

            public bool ScalesWithClass(BlueprintCharacterClass character_class)
            {
                var scaling = ability.GetComponent<ContextCalculateAbilityParamsBasedOnClasses>();
                if (scaling == null)
                {
                    return false;
                }
                else
                {
                    return scaling.CharacterClasses.Contains(character_class);
                }
            }
        }

        static List<ChannelEntry> channel_entries = new List<ChannelEntry>();

        static BlueprintFeature selective_channel = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("fd30c69417b434d47b6b03b9c1f568ff"); // SelectiveChannel

        static Dictionary<string, string> normal_quick_channel_map = new Dictionary<string, string>();
        static Dictionary<string, string> normal_smite_map = new Dictionary<string, string>();

        static public BlueprintFeature quick_channel = null;
        static public BlueprintFeature channel_smite = null;
        static public BlueprintFeature improved_channel = null;
        static public BlueprintFeature sacred_conduit = null;
        static public BlueprintFeature xavorns_cross_feature = null;
        static public BlueprintFeature channeling_scourge = null;

        static BlueprintFeature witch_channel_negative;
        static BlueprintFeature witch_channel_positive;

        static BlueprintFeature back_to_the_grave_base = null;
        static public BlueprintFeature versatile_channeler = null;
        static internal BlueprintFeature versatile_channeler_positive = null;
        static internal BlueprintFeature versatile_channeler_negative = null;
        static internal BlueprintFeature versatile_channeler_positive_warpriest = null;
        static internal BlueprintFeature versatile_channeler_negative_warpriest = null;

        static internal BlueprintFeature holy_vindicator_shield = null;
        static readonly SpellDescriptor holy_vindicator_shield_descriptor = (SpellDescriptor)AdditionalSpellDescriptors.ExtraSpellDescriptor.HolyVindicatorShield;

        static BlueprintFeature stigmata_feature = null;
        static GameAction blood_positive_action = null;
        static GameAction blood_negative_action = null;
        static GameAction blood_smite_positive_action = null;
        static GameAction blood_smite_negative_action = null;

        static BlueprintFeature versatile_channel;
        static AbilityDeliverProjectile negative_line_projectile;
        static AbilityDeliverProjectile positive_line_projectile;

        static AbilityDeliverProjectile negative_cone_projectile;
        static AbilityDeliverProjectile positive_cone_projectile;

        static BlueprintFact[] channel_resistances = new BlueprintFeature[] { ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("a9ac84c6f48b491438f91bb237bc9212") }; // ChannelResistance4 (TODO: Maybe more?)

        static public BlueprintFeature swift_positive_channel;
        static public BlueprintFeature swift_positive_channel_resource;

        static public BlueprintBuff desecrate_buff, consecrate_buff;
        static List<BlueprintCharacterClass> extra_progressing_classes = new List<BlueprintCharacterClass>();

        internal static void init()
        {
            // TODO angelfire apostle (maybe, might just be additional ways to spend the resource)
            var empyreal_sorc_channel_feature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("7d49d7f590dc9a948b3bd1c8b7979854"); // ChannelEnergyEmpyrealFeature
            var cleric_negative_channel_feature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("3adb2c906e031ee41a01bfc1d5fb7eea"); // ChannelNegativeFeature
            var cleric_positive_channel_feature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("a79013ff4bcd4864cb669622a29ddafb"); // ChannelEnergyFeature
            var paladin_channel_feature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("cb6d55dda5ab906459d18a435994a760"); // ChannelEnergyPaladinFeature
            var hospitalier_channel_feature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("a9ab1bbc79ecb174d9a04699986ce8d5"); // ChannelEnergyHospitalerFeature

            var empyreal_sorc_positive_heal = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("574cf074e8b65e84d9b69a8c6f1af27b"); // ChannelEnergyEmpyrealHeal
            var empyreal_sorc_positive_harm = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("e1536ee240c5d4141bf9f9485a665128"); // ChannelEnergyEmpyrealHarm
            var cleric_positive_heal = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("f5fc9a1a2a3c1a946a31b320d1dd31b2"); // ChannelEnergy
            var cleric_positive_harm = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("279447a6bf2d3544d93a0a39c3b8e91d"); // ChannelPositiveHarm
            var cleric_negative_heal = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("9be3aa47a13d5654cbcb8dbd40c325f2"); // ChannelNegativeHeal
            var cleric_negative_harm = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("89df18039ef22174b81052e2e419c728"); // ChannelNegativeEnergy
            var paladin_positive_heal = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("6670f0f21a1d7f04db2b8b115e8e6abf"); // ChannelEnergyPaladinHeal
            var paladin_positive_harm = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("4937473d1cfd7774a979b625fb833b47"); // ChannelEnergyPaladinHarm
            var hospitalier_positive_heal = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("0c0cf7fcb356d2448b7d57f2c4db3c0c"); // ChannelEnergyHospitalerHeal
            var hospitalier_positive_harm = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("cc17243b2185f814aa909ac6b6599eaa"); // ChannelEnergyHospitalerHarm

            var empyreal_sorc_positive_heal_base = help.createVariantWrapper("EmpyrealSorcerorPositiveChannelHealBase", empyreal_sorc_positive_heal.ToAny());
            var empyreal_sorc_positive_harm_base = help.createVariantWrapper("EmpyrealSorcerorPositiveChannelHarmBase", empyreal_sorc_positive_harm.ToAny());

            var cleric_positive_heal_base = help.createVariantWrapper("ClericPositiveChannelHealBase", cleric_positive_heal.ToAny());
            var cleric_positive_harm_base = help.createVariantWrapper("ClericPositiveChannelHarmBase", cleric_positive_harm.ToAny());
            var cleric_negative_heal_base = help.createVariantWrapper("ClericNegativeChannelHealBase", cleric_negative_heal.ToAny());
            var cleric_negative_harm_base = help.createVariantWrapper("ClericNegativeChannelHarmBase", cleric_negative_harm.ToAny());

            var paladin_positive_heal_base = help.createVariantWrapper("PaladinPositiveChannelHealBase", paladin_positive_heal.ToAny());
            var paladin_positive_harm_base = help.createVariantWrapper("PaladinPositiveChannelHarmBase", paladin_positive_harm.ToAny());

            var hospitalier_positive_heal_base = help.createVariantWrapper("HospitalierPositiveChannelHealBase", hospitalier_positive_heal.ToAny());
            var hospitalier_positive_harm_base = help.createVariantWrapper("HospitalierPositiveChannelHarmBase", hospitalier_positive_harm.ToAny());

            empyreal_sorc_channel_feature.GetComponent<AddFacts>().m_Facts = new BlueprintUnitFactReference[] { empyreal_sorc_positive_harm_base.ToAny(), empyreal_sorc_positive_heal_base.ToAny() };
            paladin_channel_feature.GetComponent<AddFacts>().m_Facts = new BlueprintUnitFactReference[] { paladin_positive_harm_base.ToAny(), paladin_positive_heal_base.ToAny() };
            hospitalier_channel_feature.GetComponent<AddFacts>().m_Facts = new BlueprintUnitFactReference[] { hospitalier_positive_harm_base.ToAny(), hospitalier_positive_heal_base.ToAny() };

            cleric_positive_channel_feature.GetComponent<AddFacts>().m_Facts[1] = cleric_positive_heal_base.ToAny();
            cleric_positive_channel_feature.GetComponent<AddFacts>().m_Facts[2] = cleric_positive_harm_base.ToAny();
            cleric_negative_channel_feature.GetComponent<AddFacts>().m_Facts[1] = cleric_negative_harm_base.ToAny();
            cleric_negative_channel_feature.GetComponent<AddFacts>().m_Facts[2] = cleric_negative_heal_base.ToAny();

            storeChannel(empyreal_sorc_positive_heal, empyreal_sorc_channel_feature, ChannelType.PositiveHeal);
            storeChannel(empyreal_sorc_positive_harm, empyreal_sorc_channel_feature, ChannelType.PositiveHarm);

            storeChannel(paladin_positive_heal, paladin_channel_feature, ChannelType.PositiveHeal);
            storeChannel(paladin_positive_harm, paladin_channel_feature, ChannelType.PositiveHarm);

            storeChannel(hospitalier_positive_heal, hospitalier_channel_feature, ChannelType.PositiveHeal);
            storeChannel(hospitalier_positive_harm, hospitalier_channel_feature, ChannelType.PositiveHarm);

            storeChannel(cleric_positive_heal, cleric_positive_channel_feature, ChannelType.PositiveHeal);
            storeChannel(cleric_positive_harm, cleric_positive_channel_feature, ChannelType.PositiveHarm);
            storeChannel(cleric_negative_heal, cleric_negative_channel_feature, ChannelType.NegativeHeal);
            storeChannel(cleric_negative_harm, cleric_negative_channel_feature, ChannelType.NegativeHarm);

            var channels_to_fix = new string[]
            {
                    "cbd03c874e39e6c4795fe0093544f2a2", //BreathOfLifeTouch
                    "9c7ebca48b7340242a761a9f53e2f010", //FmaewardenEmbersTouch
                    "788d72e7713cf90418ee1f38449416dc", //InspiringRecovery (Shouldn't resurrect dhampirs)
                    "0e370822d9e0ff54f897e7fdf24cffb8", //KineticRevificationAbility (Shouldn't resurrect dhampirs)

                    "f5fc9a1a2a3c1a946a31b320d1dd31b2", //ChannelEnergy
                    "e1536ee240c5d4141bf9f9485a665128", //ChannelEnergyEmpyrealHarm
                    "574cf074e8b65e84d9b69a8c6f1af27b", //ChannelEnergyEmpyrealHeal
                    "cc17243b2185f814aa909ac6b6599eaa", //ChannelEnergyHospitalerHarm
                    "0c0cf7fcb356d2448b7d57f2c4db3c0c", //ChannelEnergyHospitalerHeal
                    "4937473d1cfd7774a979b625fb833b47", //ChannelEnergyPaladinHarm
                    "6670f0f21a1d7f04db2b8b115e8e6abf", //ChannelEnergyPaladinHeal
                    "89df18039ef22174b81052e2e419c728", //ChannelNegativeEnergy
                    "9be3aa47a13d5654cbcb8dbd40c325f2", //ChannelNegativeHeal
                    "90db8c09596a6734fa3b281399d672f7", //ChannelOnCritAbility
                    "279447a6bf2d3544d93a0a39c3b8e91d", //ChannelPositiveHarm
                    "0d657aa811b310e4bbd8586e60156a2d", //CureCriticalWounds
                    "1f173a16120359e41a20fc75bb53d449", //CureCriticalWoundsMass
                    "0e41945b6701d7643b4f19c145d7d9e1", //CureCriticalWoundsPretendPotion
                    "47808d23c67033d4bbab86a1070fd62f", //CureLightWounds
                    "5d3d689392e4ff740a761ef346815074", //CureLightWoundsMass
                    "1c1ebf5370939a9418da93176cc44cd9", //CureModerateWounds
                    "571221cc141bc21449ae96b3944652aa", //CureModerateWoundsMass
                    "6e81a6679a0889a429dec9cedcf3729c", //CureSeriousWounds
                    "0cea35de4d553cc439ae80b3a8724397", //CureSeriousWoundsMass
                    "c8feb35c95f9f83438655f93552b900b", //DivineHunterDistantMercyAbility
                    "ff8f1534f66559c478448723e16b6624", //Heal
                    "caae1dc6fcf7b37408686971ee27db13", //LayOnHandsOthers
                    "8d6073201e5395d458b8251386d72df1", //LayOnHandsSelf
                    "8337cea04c8afd1428aad69defbfc365", //LayOnHandsSelfOrTroth
                    "a8666d26bbbd9b640958284e0eee3602", //LifeBlast
                    "02a98da52a022534b94604dfb06e6fe9", //VeilOfPositiveEnergySwift

                    "a44adb55cfca17d488faefeffd321b07", //FriendlyInflictCriticalWounds
                    "137af566f68fd9b428e2e12da43c1482", //Harm
                    "db611ffeefb8f1e4f88e7d5393fc651d", //HealingBurstAbility
                    "867524328b54f25488d371214eea0d90", //HealMass
                    "3cf05ef7606f06446ad357845cb4d430", //InflictCriticalWounds
                    "5ee395a2423808c4baf342a4f8395b19", //InflictCriticalWoundsMass
                    "244a214d3b0188e4eb43d3a72108b67b", //InflictLightWounds_PrologueTrap
                    "e5cb4c4459e437e49a4cd73fde6b9063", //InflictLightWounds
                    "9da37873d79ef0a468f969e4e5116ad2", //InflictLightWoundsMass
                    "14d749ecacca90a42b6bf1c3f580bb0c", //InflictModerateWounds
                    "03944622fbe04824684ec29ff2cec6a7", //InflictModerateWoundsMass
                    "b0b8a04a3d74e03489862b03f4e467a6", //InflictSeriousWounds
                    "820170444d4d2a14abc480fcbdb49535" //InflictSeriousWoundsMass
                    //"5c24b3e2633a8f74f8419492eda5bf11", //ZombieLairChannelNegativeHarm
                    //"ab32492b5b46dea4199fe724efa5f800", //ZombieLairChannelNegativeHeal
            };

            var undead = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("734a29b693e9ec346ba2951b27987e33"); // UndeadType
            foreach (var c_guid in channels_to_fix)
            {
                var c = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(c_guid);
                var new_actions = help.changeAction<Conditional>(c.GetComponent<AbilityEffectRunAction>().Actions.Actions,
                                                    a =>
                                                    {
                                                        var checker = Helper.CreateConditionsChecker(Operation.Or, a.ConditionsChecker.Conditions);
                                                        checker.Operation = a.ConditionsChecker.Operation;

                                                        for (int i = 0; i < checker.Conditions.Length; i++)
                                                        {
                                                            var has_fact = checker.Conditions[i] as ContextConditionHasFact;
                                                            if (has_fact != null && has_fact.Fact == undead)
                                                            {
                                                                checker.Conditions[i] = help.CreateHNEA(e => e.Not = has_fact.Not);
                                                            }
                                                        }
                                                        a.ConditionsChecker = checker;
                                                    }
                                                    );
                c.ReplaceComponent<AbilityEffectRunAction>(Helper.CreateAbilityEffectRunAction(SavingThrowType.Unknown, new_actions));
            }

            ChannelEnergyEngine.createImprovedChannel();
            // Removed: ChannelEnergyEngine.createQuickChannel();
            ChannelEnergyEngine.createChannelSmite();

            // Fix paladin capstone that maximizes channel heal
            var paladin = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("bfa11238e7ae3544bbeb4d0b92e897ec"); // PaladinClass
            var holy_champion = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("eff3b63f744868845a2f511e9929f0de"); // HolyChampion
            var channels = ChannelEnergyEngine.getChannelAbilities(e => e.ScalesWithClass(paladin)).ToArray();
            holy_champion.GetComponent<AutoMetamagic>().Abilities.AddRange(new BlueprintAbilityReference[] { hospitalier_positive_heal_base.ToAny(), hospitalier_positive_harm_base.ToAny(), paladin_positive_harm_base.ToAny(), paladin_positive_heal_base.ToAny() });
            holy_champion.GetComponent<AutoMetamagic>().Abilities.AddRange(Array.ConvertAll(channels, x => x.ToRef()));
            holy_champion.GetComponent<AutoMetamagic>().Abilities = holy_champion.GetComponent<AutoMetamagic>().Abilities.Distinct().ToList();
        }

        static public void addChannelResitance(BlueprintUnitFact new_cr)
        {
            channel_resistances = channel_resistances.AddToArray(new_cr);
            foreach (var c in channel_entries)
            {
                addToSpecificChannelResistance(c, new_cr);
            }
        }

        static internal void addVersatileChannel(BlueprintFeature versatile_channel_feat)
        {
            versatile_channel = versatile_channel_feat;
            var negative_line_proj = ResourcesLibrary.TryGetBlueprint<BlueprintProjectile>("3d13c52359bcd6645988dd88a120d7c0"); // UmbralStrike00
            var positive_line_proj = ResourcesLibrary.TryGetBlueprint<BlueprintProjectile>("c4b0d8b4786a1244d9fbc4b424931b83"); // Sunbeam00
            var negative_cone_proj = ResourcesLibrary.TryGetBlueprint<BlueprintProjectile>("4d601ab51e167c04a8c6883260e872ee"); // NecromancyCone30Feet00
            var positive_cone_proj = ResourcesLibrary.TryGetBlueprint<BlueprintProjectile>("7363081f6144d604da3645a6ea94fcb1"); // ChannelEnergyCone30Feet00

            // Options
            // f8f8f0402c24a584bb076a2f222d4270 ChannelNegativeEnergyCone30Feet00 (Negative Harm) - ShieldBearer
            // 8c3c664b2bd74654e82fc70d3457e10d EnchantmentCone30Feet00 (Used for heals) - ShieldBearer
            // 7363081f6144d604da3645a6ea94fcb1 ChannelEnergyCone30Feet00 (Positive harm) - ShieldBearer
            // c4b0d8b4786a1244d9fbc4b424931b83 Sunbeam00
            // 1288a9729b18d3e4682d0f784e5fbd55 Kinetic_PlasmaLine00
            // 3d13c52359bcd6645988dd88a120d7c0 UmbralStrike00

            var lightning_bolt = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("d2cff9243a7ee804cb6d5be47af30c73"); // LightningBolt
            var waves_of_fatigue = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("8878d0c46dfbd564e9d5756349d5e439"); // WavesOfFatigue

            negative_line_projectile = lightning_bolt.GetComponent<AbilityDeliverProjectile>().Clone();
            negative_line_projectile.m_Projectiles = new BlueprintProjectileReference[] { negative_line_proj.ToAny() };

            positive_line_projectile = lightning_bolt.GetComponent<AbilityDeliverProjectile>().Clone();
            positive_line_projectile.m_Projectiles = new BlueprintProjectileReference[] { positive_line_proj.ToAny() };

            negative_cone_projectile = waves_of_fatigue.GetComponent<AbilityDeliverProjectile>().Clone();
            negative_cone_projectile.m_Projectiles = new BlueprintProjectileReference[] { negative_cone_proj.ToAny() };

            positive_cone_projectile = waves_of_fatigue.GetComponent<AbilityDeliverProjectile>().Clone();
            positive_cone_projectile.m_Projectiles = new BlueprintProjectileReference[] { positive_cone_proj.ToAny() };

            foreach (var entry in channel_entries.ToArray())
            {
                addToVersatileChannel(entry);
            }
        }

        static void addToVersatileChannel(ChannelEntry entry)
        {
            if (versatile_channel == null)
            {
                return;
            }

            if (entry.channel_type.IsOf(ChannelType.Form) || entry.channel_type.IsOf(ChannelType.Smite) || entry.channel_type.IsOf(ChannelType.HolyVindicatorShield))
            {
                return;
            }

            var spray_form = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("a240a6d61e1aee040bf7d132bfe1dc07"); // FanOfFlamesFireBlastAbility
            var torrent_form = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("2aad85320d0751340a0786de073ee3d5"); // TorrentInfusionFeature
            var cone_ability = help.Clone<BlueprintAbility>(entry.ability, "Cone" + entry.ability.name);
            var line_ability = help.Clone<BlueprintAbility>(entry.ability, "Line" + entry.ability.name);

            cone_ability.m_DisplayName = Helper.CreateString($"{cone_ability.Name}, 30-Foot Cone");
            cone_ability.m_Icon = spray_form.Icon;
            line_ability.m_DisplayName = Helper.CreateString($"{line_ability.Name}, 120-Foot Line");
            line_ability.m_Icon = torrent_form.Icon;
            cone_ability.Range = AbilityRange.Projectile;
            line_ability.Range = AbilityRange.Projectile;
            cone_ability.setMiscAbilityParametersRangedDirectional();
            line_ability.setMiscAbilityParametersRangedDirectional();
            if (entry.channel_type.IsOf(ChannelType.Positive))
            {
                cone_ability.ReplaceComponent<AbilityTargetsAround>(positive_cone_projectile);
                line_ability.ReplaceComponent<AbilityTargetsAround>(positive_line_projectile);
            }
            else
            {
                cone_ability.ReplaceComponent<AbilityTargetsAround>(negative_cone_projectile);
                line_ability.ReplaceComponent<AbilityTargetsAround>(negative_line_projectile);
            }

            line_ability.RemoveComponents<AbilitySpawnFx>();
            cone_ability.RemoveComponents<AbilitySpawnFx>();
            line_ability.AddComponent(help.CreateAbilityShowIfCasterHasFacts(
                Array.ConvertAll(entry.required_facts.AddToArray(versatile_channel), x => x.ToRef<BlueprintUnitFactReference>())));
            cone_ability.AddComponent(help.CreateAbilityShowIfCasterHasFacts(
                Array.ConvertAll(entry.required_facts.AddToArray(versatile_channel), x => x.ToRef<BlueprintUnitFactReference>())));
            entry.base_ability.addToAbilityVariants(line_ability.ToAny(), cone_ability.ToAny());

            updateItemsForChannelDerivative(entry.ability, line_ability);
            updateItemsForChannelDerivative(entry.ability, cone_ability);

            
            storeChannel(line_ability, entry.parent_feature, entry.channel_type | ChannelType.Line, entry.properties);
            storeChannel(cone_ability, entry.parent_feature, entry.channel_type | ChannelType.Cone, entry.properties);
        }

        static internal void addBloodfireAndBloodrainActions(GameAction positive_action, GameAction negative_action, GameAction smite_positive_action, GameAction smite_negative_action)
        {
            blood_positive_action = positive_action;
            blood_negative_action = negative_action;
            blood_smite_positive_action = smite_positive_action;
            blood_smite_negative_action = smite_negative_action;

            foreach (var entry in channel_entries)
            {
                addToBloodfireAndBloodrain(entry);
            }
        }

        static void addToBloodfireAndBloodrain(ChannelEntry entry)
        {
            if (blood_negative_action == null)
            {
                return;
            }

            if (entry.channel_type.IsOf(ChannelType.Smite) && !entry.properties.HasFlag(ChannelProperty.Bloodfire))
            {
                var buff = (entry.ability.GetComponent<AbilityEffectRunAction>().Actions.Actions[0] as ContextActionApplyBuff).Buff;

                if (entry.channel_type.IsOf(ChannelType.Positive))
                {
                    buff.ReplaceComponent<AddInitiatorAttackWithWeaponTrigger>(a => a.Action =
                                                 help.CreateActionList(help.addMatchingAction<ContextActionDealDamage>(a.Action.Actions, blood_smite_positive_action)));
                }
                else
                {
                    buff.ReplaceComponent<AddInitiatorAttackWithWeaponTrigger>(a => a.Action =
                                                 help.CreateActionList(help.addMatchingAction<ContextActionDealDamage>(a.Action.Actions, blood_smite_negative_action)));
                }
                entry.properties = entry.properties | ChannelProperty.Bloodfire;
            }
            else if (entry.channel_type.IsNotOf(ChannelType.HolyVindicatorShield) && entry.channel_type.IsOf(ChannelType.Harm) && !entry.properties.HasFlag(ChannelProperty.Bloodrain))
            {
                if (entry.channel_type.IsOf(ChannelType.Positive))
                {
                    entry.ability.ReplaceComponent<AbilityEffectRunAction>(a => a.Actions =
                                                 help.CreateActionList(help.addMatchingAction<ContextActionDealDamage>(a.Actions.Actions, blood_positive_action)));
                }
                else
                {
                    entry.ability.ReplaceComponent<AbilityEffectRunAction>(a => a.Actions =
                                                 help.CreateActionList(help.addMatchingAction<ContextActionDealDamage>(a.Actions.Actions, blood_negative_action)));
                }
                entry.properties = entry.properties | ChannelProperty.Bloodrain;
            }
        }

        static public void addClassToChannelEnergyProgression(BlueprintCharacterClass cls)
        {
            if (extra_progressing_classes.Contains(cls))
            {
                return;
            }

            var cleric = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0");
            var blessing_of_the_faithful_long = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("3ef665bb337d96946bcf98a11103f32f");
            ClassToProgression.addClassToAbility(cls, new BlueprintArchetype[0], blessing_of_the_faithful_long, cleric);

            foreach (var c in channel_entries)
            {
                addClassChannelEnergyProgressionToChannel(c, cls);
            }
        }

        static void addClassChannelEnergyProgressionToChannel(ChannelEntry entry, BlueprintCharacterClass cls)
        {
            if (cls == null)
            {
                return;
            }

            if (entry.ScalesWithClass(cls))
            {
                return;
            }

            var context_rank_config = entry.ability.GetComponent<ContextRankConfig>();
            if (context_rank_config != null)
            {
                context_rank_config = context_rank_config.Clone();
            }

            var classes = context_rank_config.m_Class;
            if (classes.Length != 0 && classes[0] != null)
            {
                classes = classes.AddToArray(cls.ToRef());
                context_rank_config.m_Class = classes;
            }
            entry.ability.ReplaceComponent<ContextRankConfig>(context_rank_config);

            var scaling = entry.ability.GetComponent<ContextCalculateAbilityParamsBasedOnClasses>().Clone();
            {
                scaling.CharacterClasses = scaling.CharacterClasses.AddToArray(cls);
            }
            entry.ability.ReplaceComponent<ContextCalculateAbilityParamsBasedOnClasses>(scaling);

            if (entry.channel_type.IsOf(ChannelType.HolyVindicatorShield | ChannelType.Smite))
            {
                //look for buff
                var buff = (entry.ability.GetComponent<AbilityEffectRunAction>().Actions.Actions[0] as ContextActionApplyBuff).Buff;

                if (context_rank_config != null)
                {
                    buff.ReplaceComponent<ContextRankConfig>(context_rank_config);
                }

                buff.ReplaceComponent<ContextCalculateAbilityParamsBasedOnClasses>(scaling);
            }
        }

        static internal void createHolyVindicatorShield()
        {
            var sacred_numbus_icon = Helper.StealIcon("bf74b3b54c21a9344afe9947546e036f"); // SacredNimbus
            holy_vindicator_shield = Helper.CreateBlueprintFeature("HolyVindicatorShieldFeature",
                displayName: LocalizationTool.GetString("HV.Shield.Name"),
                description: LocalizationTool.GetString("HV.Shield.Description"),
                icon: sacred_numbus_icon);
            foreach (var c in channel_entries.ToArray())
            {
                addToHolyVindicatorShield(c);
            }
        }

        static void addToHolyVindicatorShield(ChannelEntry entry)
        {
            if (holy_vindicator_shield == null)
            {
                return;
            }

            if (!entry.channel_type.IsBase())
            {
                return;
            }

            ModifierDescriptor bonus_descriptor = ModifierDescriptor.Sacred;
            var icon = holy_vindicator_shield.Icon;
            var fx_buff = ResourcesLibrary.TryGetBlueprint<BlueprintBuff>("57b1c6a69c53f4d4ea9baec7d0a3a93a").FxOnStart; // SacredNimbusBuff

            if (entry.channel_type.IsOf(ChannelType.Negative))
            {
                bonus_descriptor = ModifierDescriptor.Profane;
                icon = Helper.StealIcon("b56521d58f996cd4299dab3f38d5fe31"); // ProfaneNimbus
                fx_buff = ResourcesLibrary.TryGetBlueprint<BlueprintBuff>("bb08ad05d0b4505488775090954c2317").FxOnStart;// ProfaneNimbusBuff
            }

            var buff = Helper.CreateBlueprintBuff(entry.ability.name + "HolyVindicatorShieldBuff",
                $"{bonus_descriptor.ToString()} {LocalizationTool.GetString("HV.Shield.Name")} ({entry.ability.Name})",
                holy_vindicator_shield.Description, icon, fx_buff);
            buff.SetComponents
                (
                new AddStatBonusIfHasShield() { descriptor = bonus_descriptor, value = Helper.CreateContextValue(), stat = StatType.AC },
                entry.ability.GetComponent<ContextRankConfig>(),
                entry.ability.GetComponent<ContextCalculateAbilityParamsBasedOnClasses>(),
                help.CreateAddTargetAttackWithWeaponTrigger(
                    Helper.CreateActionList(new ContextActionRemoveSelf()), 
                    Helper.CreateActionList(), 
                    not_reach: false, 
                    only_melee: false), 
                Helper.CreateSpellDescriptorComponent(holy_vindicator_shield_descriptor)
                );

            var apply_buff_action = Helper.CreateContextActionApplyBuff(buff, 
                duration: Helper.CreateContextDurationValue(bonus: Helper.CreateContextValue(1), rate: Kingmaker.UnitLogic.Mechanics.DurationRate.Days), 
                dispellable: false);

            var ability = Helper.CreateBlueprintAbility(entry.ability.name + "HolyVindicatorShieldAbility",
                displayName: buff.Name, description: buff.Description, buff.Icon,
                AbilityType.Supernatural, Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Standard,
                AbilityRange.Personal, Helper.CreateString("24 hours"), savingThrow: null);
            ability.SetComponents
                (
                Helper.CreateAbilityEffectRunAction(save: SavingThrowType.Unknown, apply_buff_action), 
                entry.ability.GetComponent<ContextRankConfig>(),
                entry.ability.GetComponent<ContextCalculateAbilityParamsBasedOnClasses>(),
                new AbilityShowIfCasterHasFacts() { m_UnitFacts = Array.ConvertAll(entry.required_facts.AddToArray(holy_vindicator_shield), x => x.ToRef()) },
                Helper.CreateAbilityExecuteActionOnCast(new GameAction[] { help.CreateContextActionRemoveBuffsByDescriptor(holy_vindicator_shield_descriptor) }),
                new AbilityRequirementHasItemInHands() { m_Type = AbilityRequirementHasItemInHands.RequirementType.HasShield }
                );
            
            ability.TargetSelf();

            entry.base_ability.addToAbilityVariants(ability.ToRef());
            
            foreach (var checker in entry.ability.GetComponents<IAbilityRestriction>())
            {
                ability.AddComponent(checker as BlueprintComponent);
            }

            storeChannel(ability, entry.parent_feature, entry.channel_type | ChannelType.HolyVindicatorShield, entry.properties);
        }

        // Removed Evangelist archetype feature
        static internal void createVersatileChanneler()
        {
            var channel_positive = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("a79013ff4bcd4864cb669622a29ddafb"); // ChannelEnergyFeature
            var channel_negative = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("3adb2c906e031ee41a01bfc1d5fb7eea"); // ChannelNegativeFeature

            //separate spontaneous cure/harm from channel
            var spontaneous_heal = channel_positive.GetComponent<AddFacts>().Facts[3];
            var spontaneous_harm = channel_negative.GetComponent<AddFacts>().Facts[3];

            channel_positive.GetComponent<AddFacts>().m_Facts = channel_positive.GetComponent<AddFacts>().m_Facts.Take(3).ToArray();
            channel_positive.AddComponent(Helper.CreateAddFeatureIfHasFact(spontaneous_harm, spontaneous_heal, not: true));

            channel_negative.GetComponent<AddFacts>().m_Facts = channel_negative.GetComponent<AddFacts>().m_Facts.Take(3).ToArray();
            channel_negative.AddComponent(Helper.CreateAddFeatureIfHasFact(spontaneous_heal, spontaneous_harm, not: true));

            var warpriest_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("30b5e47d47a0e37438cc5a80c96cfb99"); // WarpriestClass
            var warpriest_positive = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("bd588bc544d2f8547a02bb82ad9f466a"); // WarpriestChannelEnergyFeature
            var warpriest_negative = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("e02c8a7336a542f4baffa116b6506950"); // WarpriestChannelNegativeFeature

            var cleric = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0"); // ClericClass
            var allow_positive = channel_positive.GetComponent<PrerequisiteFeature>().Feature;
            var allow_negative = channel_negative.GetComponent<PrerequisiteFeature>().Feature;
            allow_positive.m_DisplayName = LocalizationTool.GetString("CEE.Allow.Positive");
            allow_negative.m_DisplayName = LocalizationTool.GetString("CEE.Allow.Negative");
            versatile_channeler_negative = Helper.CreateBlueprintFeature("VersatileChannelerNegativeFeature");
            versatile_channeler_negative.SetComponents
                (
                Helper.CreateAddFacts(channel_negative),
                new ContextIncreaseCasterLevelForSelectedSpells() { spells = new BlueprintAbility[0], value = Helper.CreateContextValue(-2) } 
                );
            versatile_channeler_negative.HideInCharacterSheetAndLevelUp = true;

            versatile_channeler_positive = Helper.CreateBlueprintFeature("VersatileChannelerPositiveFeature");
            versatile_channeler_positive.SetComponents
                (
                Helper.CreateAddFacts(channel_positive),
                new ContextIncreaseCasterLevelForSelectedSpells() { spells = new BlueprintAbility[0], value = Helper.CreateContextValue(-2) }
                );
            versatile_channeler_positive.HideInCharacterSheetAndLevelUp = true;

            versatile_channeler_positive_warpriest = Helper.CreateBlueprintFeature("VersatileChannelerPositiveWarpriestFeature");
            versatile_channeler_positive_warpriest.SetComponents
                (
                Helper.CreateAddFacts(warpriest_positive),
                new ContextIncreaseCasterLevelForSelectedSpells() { spells = new BlueprintAbility[0], value = Helper.CreateContextValue(-2) }
                );
            versatile_channeler_positive_warpriest.HideInCharacterSheetAndLevelUp = true;


            versatile_channeler_negative_warpriest = Helper.CreateBlueprintFeature("VersatileChannelerNegativeWarpriestFeature");
            versatile_channeler_negative_warpriest.SetComponents
                (
                Helper.CreateAddFacts(warpriest_negative),
                new ContextIncreaseCasterLevelForSelectedSpells() { spells = new BlueprintAbility[0], value = Helper.CreateContextValue(-2) }
                );
            versatile_channeler_negative_warpriest.HideInCharacterSheetAndLevelUp = true;

            versatile_channeler = Helper.CreateBlueprintFeature("VersatileChannelerFeature",
                displayName: LocalizationTool.GetString("CEE.Versatile.Name"),
                description: LocalizationTool.GetString("CEE.Versatile.Description"), icon: null, group: FeatureGroup.Feat);
            versatile_channeler.SetComponents
                (
                help.CreateAddFeatureIfHasFactAndNotHasFact(channel_negative, channel_positive, versatile_channeler_positive),
                help.CreateAddFeatureIfHasFactAndNotHasFact(channel_positive, channel_negative, versatile_channeler_negative),
                help.CreateAddFeatureIfHasFactAndNotHasFact(warpriest_positive, warpriest_negative, versatile_channeler_negative_warpriest),
                help.CreateAddFeatureIfHasFactAndNotHasFact(warpriest_negative, warpriest_positive, versatile_channeler_positive_warpriest),
                //channel_positive.GetComponent<PrerequisiteFeature>(),
                //channel_negative.GetComponent<PrerequisiteFeature>(),
                Helper.CreatePrerequisiteFeaturesFromList(any: true, channel_negative.ToRef(), channel_positive.ToRef(), warpriest_positive.ToRef(), warpriest_negative.ToRef()),
                Helper.CreatePrerequisiteClassLevel(cleric, 3, true),
                Helper.CreatePrerequisiteClassLevel(warpriest_class, 5, true),
                help.CreatePrerequisiteAlignment(AlignmentMaskType.ChaoticNeutral | AlignmentMaskType.LawfulNeutral | AlignmentMaskType.TrueNeutral)
                );
            Helper.AddFeats(versatile_channeler);
            foreach (var c in channel_entries)
            {
                addToVersatileChanneler(c);
            }
        }

        static void addToVersatileChanneler(ChannelEntry entry)
        {
            var warpriest = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("30b5e47d47a0e37438cc5a80c96cfb99"); // WarpriestClass
            if (versatile_channeler == null)
            {
                return;
            }
            var cleric = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0");

            if (entry.ScalesWithClass(cleric))
            {
                if (entry.channel_type.IsOf(ChannelType.Positive))
                {
                    var comp = versatile_channeler_positive.GetComponent<ContextIncreaseCasterLevelForSelectedSpells>();
                    comp.spells = comp.spells.AddToArray(entry.ability);
                }
                else if (entry.channel_type.IsOf(ChannelType.Negative))
                {
                    var comp = versatile_channeler_negative.GetComponent<ContextIncreaseCasterLevelForSelectedSpells>();
                    comp.spells = comp.spells.AddToArray(entry.ability);
                }
            }

            if (entry.ScalesWithClass(warpriest))
            {
                if (entry.channel_type.IsOf(ChannelType.Positive))
                {
                    var comp = versatile_channeler_positive_warpriest.GetComponent<ContextIncreaseCasterLevelForSelectedSpells>();
                    comp.spells = comp.spells.AddToArray(entry.ability);
                }
                else if (entry.channel_type.IsOf(ChannelType.Negative))
                {
                    var comp = versatile_channeler_negative_warpriest.GetComponent<ContextIncreaseCasterLevelForSelectedSpells>();
                    comp.spells = comp.spells.AddToArray(entry.ability);
                }
            }
        }

        //this function will only create ability without storing it
        //user will need to put it inside wrapper and then call storeChannel
        public static BlueprintAbility createChannelEnergy(ChannelType channel_type, string name, string display_name, string description, string guid,
                                                           ContextRankConfig rank_config, ContextCalculateAbilityParamsBasedOnClasses dc_scaling,
                                                           AbilityResourceLogic resource_logic)
        {
            string original_guid = "";
            switch (channel_type)
            {
                case ChannelType.PositiveHeal:
                    original_guid = "f5fc9a1a2a3c1a946a31b320d1dd31b2"; // ChannelEnergy
                    break;
                case ChannelType.PositiveHarm:
                    original_guid = "279447a6bf2d3544d93a0a39c3b8e91d"; // ChannelPositiveHarm
                    break;
                case ChannelType.NegativeHarm:
                    original_guid = "89df18039ef22174b81052e2e419c728"; // ChannelNegativeEnergy
                    break;
                case ChannelType.NegativeHeal:
                    original_guid = "9be3aa47a13d5654cbcb8dbd40c325f2"; // ChannelNegativeHeal
                    break;
                default:
                    throw Main.Error("Only base channel abilities can be created");
            }

            var ability = help.Clone<BlueprintAbility>(ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(original_guid), name);

            if (display_name != "")
            {
                ability.m_DisplayName = Helper.CreateString(display_name);
            }

            if (description != "")
            {
                ability.m_Description = Helper.CreateString(description);
            }

            ability.ReplaceComponent<ContextRankConfig>(rank_config);
            ability.ReplaceComponent<ContextCalculateAbilityParamsBasedOnClasses>(dc_scaling);

            if (resource_logic != null)
            {
                ability.ReplaceComponent<AbilityResourceLogic>(resource_logic);
            }
            else
            {
                ability.RemoveComponents<AbilityResourceLogic>();
            }

            updateItemsForChannelDerivative(ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(original_guid), ability);

            return ability;
        }

        public static void storeChannel(BlueprintAbility ability, BlueprintFeature parent_feature, ChannelType channel_type, ChannelProperty property = ChannelProperty.None)
        {
            var entry = new ChannelEntry(ability, parent_feature, channel_type, property);
            channel_entries.Add(entry);

            addToImprovedChannel(entry);
            addToChannelingScourge(entry);

            addToWitchImprovedChannelHexScaling(entry);
            /* Removed since quick is now Vanilla
            if (!channel_type.IsOf(ChannelType.Form))
            { //form channel will be created from quick and not vice versa
                addToQuickChannel(entry);
                addToSwiftPositiveChannel(entry);
            }
            */
            addToVersatileChannel(entry);

            addToChannelSmite(entry);

            addToImprovedChannel(entry);

            help.AddFeaturePrerequisiteAny(selective_channel, parent_feature);

            addToBackToTheGrave(entry);
            addToVersatileChanneler(entry);
            addToHolyVindicatorShield(entry);
            foreach (var cls in extra_progressing_classes)
            {
                addClassChannelEnergyProgressionToChannel(entry, cls);
            }
            addToStigmataPrerequisites(entry);


            //should be last
            addToBloodfireAndBloodrain(entry);
            addToChannelResistance(entry);


            //ecclicitheurge scaling (no bonus at lvl 3)
            var bonded = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("aa34ca4f3cd5e5d49b2475fcfdf56b24");
            var cleric = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0");
            var penalty = bonded.GetComponent<ContextIncreaseCasterLevelForSelectedSpells>();
            if (penalty != null && entry.ScalesWithClass(cleric))
            {
                penalty.spells = penalty.spells.AddToArray(entry.ability);
            }

            addIncreasedSpellDamageFromSunDomain(entry);

            addToDesecreate(entry);
            addToConsecrate(entry);
        }

        static void addToDesecreate(ChannelEntry entry)
        {
            if (desecrate_buff == null)
            {
                return;
            }
            if (entry.channel_type.IsOf(ChannelType.NegativeHarm))
            {
                var dc_bonus = desecrate_buff.GetComponent<IncreaseSpecifiedSpellsDC>();
                dc_bonus.spells = dc_bonus.spells.AddToArray(entry.ability);
            }
        }

        static void addToConsecrate(ChannelEntry entry)
        {
            if (consecrate_buff == null)
            {
                return;
            }
            if (entry.channel_type.IsOf(ChannelType.PositiveHarm))
            {
                var dc_bonus = consecrate_buff.GetComponent<IncreaseSpecifiedSpellsDC>();
                dc_bonus.spells = dc_bonus.spells.AddToArray(entry.ability);
            }
        }

        internal static void addIncreasedSpellDamageFromSunDomain(ChannelEntry entry)
        {
            if (!entry.channel_type.IsOf(ChannelType.PositiveHarm))
            {
                return;
            }

            var sun_domain_dmg_bonus = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("3d8e38c9ed54931469281ab0cec506e9"); // SunDomainBaseFeature
            var component = sun_domain_dmg_bonus.GetComponent<IncreaseSpellDamageByClassLevel>();

            if (component.Spells.Contains(entry.ability))
            {
                return;
            }

            component.m_Spells = component.m_Spells.AddToArray(entry.ability.ToRef());
        }

        internal static void addToSpecificChannelResistance(ChannelEntry entry, BlueprintFact cr)
        {
            if (!entry.channel_type.IsStrictlyOf(ChannelType.Positive | ChannelType.Harm))
            {
                return;
            }

            var comp = cr.GetComponent<SavingThrowBonusAgainstSpecificSpells>();
            if (comp != null && !comp.Spells.Contains(entry.ability))
            {
                comp.m_Spells = comp.m_Spells.AddToArray(entry.ability.ToRef());
            }

            var comp2 = cr.GetComponent<ContextSavingThrowBonusAgainstSpecificSpells>();
            if (comp2 != null && !comp2.Spells.Contains(entry.ability))
            {
                comp2.Spells = comp2.Spells.AddToArray(entry.ability);
            }
        }

        static void addToChannelResistance(ChannelEntry entry)
        {
            foreach (var cr in channel_resistances)
            {
                addToSpecificChannelResistance(entry, cr);
            }
        }

        internal static void registerStigmata(BlueprintFeature stigmata)
        {
            stigmata_feature = stigmata;

            foreach (var c in channel_entries)
            {
                addToStigmataPrerequisites(c);
            }
        }

        static void addToStigmataPrerequisites(ChannelEntry entry)
        {
            if (!entry.channel_type.IsBase())
            {
                return;
            }

            if (stigmata_feature == null)
            {
                return;
            }

            var sacred_stigmata_prerequisites = stigmata_feature.GetComponents<AddFeatureIfHasFactsFromList>().ElementAt(0);
            if (entry.channel_type.IsOf(ChannelType.Positive) && !sacred_stigmata_prerequisites.CheckedFacts.Contains(entry.parent_feature))
            {
                sacred_stigmata_prerequisites.CheckedFacts = sacred_stigmata_prerequisites.CheckedFacts.AddToArray(entry.parent_feature);
            }


            var profane_stigmata_prerequisites = stigmata_feature.GetComponents<AddFeatureIfHasFactsFromList>().ElementAt(1);
            if (entry.channel_type.IsOf(ChannelType.Negative) && !profane_stigmata_prerequisites.CheckedFacts.Contains(entry.parent_feature))
            {
                profane_stigmata_prerequisites.CheckedFacts = profane_stigmata_prerequisites.CheckedFacts.AddToArray(entry.parent_feature);
            }
        }

        public static void addToBackToTheGrave(ChannelEntry entry)
        {
            if (entry.channel_type.IsOf(ChannelType.BackToTheGrave))
            {
                if (back_to_the_grave_base == null)
                {
                    back_to_the_grave_base = entry.base_ability.ToAny();
                }
                else
                {
                    BlueprintAbility back_to_the_grave_typed = back_to_the_grave_base.ToAny();
                    back_to_the_grave_typed.addToAbilityVariants(entry.ability.ToRef());
                }
            }
        }

        public static List<ChannelEntry> getChannels(Predicate<ChannelEntry> p)
        {
            return channel_entries.Where(c => p(c)).ToList();
        }

        public static List<BlueprintAbility> getChannelAbilities(Predicate<ChannelEntry> p)
        {
            List<BlueprintAbility> abilities = new List<BlueprintAbility>();

            foreach (var c in channel_entries)
            {
                if (p(c))
                {
                    abilities.Add(c.ability);
                }
            }
            return abilities;
        }

        internal static void setWitchImprovedChannelHex(BlueprintFeature positive_channel, BlueprintFeature negative_channel)
        {
            witch_channel_negative = negative_channel;
            witch_channel_positive = positive_channel;

            foreach (var c in channel_entries)
            {
                addToWitchImprovedChannelHexScaling(c);
            }
        }

        static void addToWitchImprovedChannelHexScaling(ChannelEntry entry)
        {
            var witch = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("1b9873f1e7bfe5449bc84d03e9c8e3cc"); // WitchClass
            if (witch_channel_negative == null)
            {
                return;
            }
            if (!entry.ScalesWithClass(witch))
            {
                return;
            }
            if (entry.channel_type.IsOf(ChannelType.Negative))
            {
                var comp = witch_channel_negative.GetComponent<ContextIncreaseCasterLevelForSelectedSpells>();
                comp.spells = comp.spells.AddToArray(entry.ability);
            }
            if (entry.channel_type.IsOf(ChannelType.Positive))
            {
                var comp = witch_channel_positive.GetComponent<ContextIncreaseCasterLevelForSelectedSpells>();
                comp.spells = comp.spells.AddToArray(entry.ability);
            }
        }

        // TODO: Archetypes reference (removing because evangelist)
        internal static void createChannelingScourge()
        {
            var cleric = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0"); // ClericClass
            var inquisitor = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("f1a70d9e1b0b41e49874e1fa9052a1ce"); // InquisitorClass
            var cleric_channel = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("d332c1748445e8f4f9e92763123e31bd"); // ChannelEnergySelection

            var harm_undead = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("279447a6bf2d3544d93a0a39c3b8e91d"); // ChannelPositiveHarm
            var harm_living = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("89df18039ef22174b81052e2e419c728"); // ChannelNegativeEnergy


            var channels = getChannelAbilities(c => c.channel_type.IsOf(ChannelType.Harm | ChannelType.Smite) && c.channel_type.IsNotOf(ChannelType.BackToTheGrave) && c.ScalesWithClass(cleric));

            channeling_scourge = Helper.CreateBlueprintFeature("ChannelingScourgeFeature",
                displayName: LocalizationTool.GetString("CEE.Scourge.Name"),
                description: LocalizationTool.GetString("CEE.Scourge.Description"), icon: null, group: FeatureGroup.Feat);
            channeling_scourge.SetComponents
                (
                new ContextIncreaseCasterLevelForSelectedSpells() { spells = channels.ToArray(), value = Helper.CreateContextValue(AbilityRankType.StatBonus) },
                new ContextIncreaseCasterLevelForSelectedSpells() { spells = channels.ToArray(), value = Helper.CreateContextValue(AbilityRankType.DamageBonus), multiplier = -1 },
                Helper.CreateContextRankConfig(baseValueType: ContextRankBaseValueType.ClassLevel,
                    classes: new BlueprintCharacterClassReference[] { inquisitor.ToRef(), cleric.ToRef() },
                    type: AbilityRankType.StatBonus),
                Helper.CreateContextRankConfig(baseValueType: ContextRankBaseValueType.ClassLevel,
                    classes: new BlueprintCharacterClassReference[] { cleric.ToRef() },
                    type: AbilityRankType.DamageBonus),
                Helper.CreatePrerequisiteClassLevel(cleric, 1),
                Helper.CreatePrerequisiteClassLevel(inquisitor, 1),
                Helper.CreatePrerequisiteFeature(cleric_channel, any: true)
                );
            Helper.AddFeats(channeling_scourge);
        }

        static void addToChannelingScourge(ChannelEntry c)
        {
            if (channeling_scourge == null)
            {
                return;
            }
            var cleric = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0"); // ClericClass
            if (c.channel_type.IsOf(ChannelType.Harm) && c.channel_type.IsNotOf(ChannelType.BackToTheGrave) && c.ScalesWithClass(cleric))
            {
                var components = channeling_scourge.GetComponents<ContextIncreaseCasterLevelForSelectedSpells>();
                foreach (var component in components)
                {
                    component.spells = component.spells.AddToArray(c.ability);
                }
            }
        }

        public static void addToImprovedChannel(BlueprintAbility ability, BlueprintFeature feature, bool not_negative = false)
        {
            if (improved_channel == null)
            {
                return;
            }


            var prereq = improved_channel.GetComponent<PrerequisiteFeaturesFromList>();
            if (!prereq.Features.Contains(feature))
            {
                prereq.m_Features = prereq.m_Features.AddToArray(feature.ToRef());
            }

            var abilities = improved_channel.GetComponent<IncreaseSpecifiedSpellsDC>();

            if (!abilities.spells.Contains(ability))
            {
                abilities.spells = abilities.spells.AddToArray(ability);
            }


            if (sacred_conduit == null)
            {
                return;
            }

            abilities = sacred_conduit.GetComponent<IncreaseSpecifiedSpellsDC>();

            if (!abilities.spells.Contains(ability))
            {
                abilities.spells = abilities.spells.AddToArray(ability);
            }


            if (xavorns_cross_feature == null || not_negative)
            {
                return;
            }

            abilities = xavorns_cross_feature.GetComponent<IncreaseSpecifiedSpellsDC>();

            if (!abilities.spells.Contains(ability))
            {
                abilities.spells = abilities.spells.AddToArray(ability);
            }
        }

        static void addToImprovedChannel(ChannelEntry c)
        {
            if (improved_channel == null)
            {
                return;
            }


            var prereq = improved_channel.GetComponent<PrerequisiteFeaturesFromList>();
            if (!prereq.Features.Contains(c.parent_feature) && !c.channel_type.IsOf(ChannelType.BackToTheGrave))
            {
                prereq.m_Features = prereq.m_Features.AddToArray(c.parent_feature.ToRef());
            }

            var abilities = improved_channel.GetComponent<IncreaseSpecifiedSpellsDC>();

            if (!abilities.spells.Contains(c.ability))
            {
                abilities.spells = abilities.spells.AddToArray(c.ability);
            }


            if (sacred_conduit == null)
            {
                return;
            }

            abilities = sacred_conduit.GetComponent<IncreaseSpecifiedSpellsDC>();

            if (!abilities.spells.Contains(c.ability))
            {
                abilities.spells = abilities.spells.AddToArray(c.ability);
            }

            if (xavorns_cross_feature == null || !c.channel_type.IsOf(ChannelType.Negative))
            {
                return;
            }

            abilities = xavorns_cross_feature.GetComponent<IncreaseSpecifiedSpellsDC>();

            if (!abilities.spells.Contains(c.ability))
            {
                abilities.spells = abilities.spells.AddToArray(c.ability);
            }
        }

        internal static void registerDesecreate(BlueprintBuff buff)
        {
            desecrate_buff = buff;
            buff.AddComponent(new IncreaseSpecifiedSpellsDC() { BonusDC = 3, spells = new BlueprintAbility[0] });
            foreach (var c in channel_entries)
            {
                addToDesecreate(c);
            }
        }

        internal static void registerConsecrate(BlueprintBuff buff)
        {
            consecrate_buff = buff;

            buff.AddComponent(new IncreaseSpecifiedSpellsDC() { BonusDC = 3, spells = new BlueprintAbility[0] });
            foreach (var c in channel_entries)
            {
                addToConsecrate(c);
            }
        }

        internal static void createImprovedChannel()
        {
            var turn_undead = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("71b8898b1d26d654b9a3eeac87e3e2f8"); // TurnUndeadNecromancy
            var necromancy_school = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("927707dce06627d4f880c90b5575125f"); // NecromancySchoolBaseFeature
            improved_channel = Helper.CreateBlueprintFeature("ImprovedChannelFeature",
                displayName: LocalizationTool.GetString("CEE.Improved.Name"),
                description: LocalizationTool.GetString("CEE.Improved.Description"), icon: null, group: FeatureGroup.Feat);
            improved_channel.SetComponents
                (
                new IncreaseSpecifiedSpellsDC() { BonusDC = 2, spells = new BlueprintAbility[] { turn_undead } }, 
                Helper.CreatePrerequisiteFeaturesFromList(necromancy_school) 
                );

            sacred_conduit = Helper.CreateBlueprintFeature("SacredConduitTrait",
                displayName: LocalizationTool.GetString("CEE.Improved.Name"),
                description: LocalizationTool.GetString("CEE.Improved.Description"), icon: null, group: FeatureGroup.Trait);
            sacred_conduit.SetComponents( new IncreaseSpecifiedSpellsDC() { BonusDC = 2, spells = new BlueprintAbility[] { turn_undead } } );

            xavorns_cross_feature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("35f473c44b5a94b42898be80f3248ca0");
            xavorns_cross_feature.RemoveComponents<IncreaseSpellDC>();
            xavorns_cross_feature.AddComponent(new IncreaseSpecifiedSpellsDC() { BonusDC = 2, spells = new BlueprintAbility[] { turn_undead } });

            foreach (var c in channel_entries)
            {
                addToImprovedChannel(c);
            }

            Helper.AddFeats(improved_channel);
        }

        // Snipped: CreateCommandUndead (Feature for another feat/class, unused

        internal static void createChannelSmite()
        {
            var resounding_blow = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("9047cb1797639924487ec0ad566a3fea"); // ResoundingBlow
            channel_smite = Helper.CreateBlueprintFeature("ChannelSmiteFeature",
                displayName: LocalizationTool.GetString("CEE.Smite.Name"),
                description: LocalizationTool.GetString("CEE.Smite.Description"), icon: resounding_blow.Icon, group: FeatureGroup.Feat);
            channel_smite.Groups = channel_smite.Groups.AddToArray(FeatureGroup.CombatFeat);

            foreach (var c in channel_entries.ToArray())
            {
                addToChannelSmite(c);
            }

            Helper.AddCombatFeat(channel_smite);
        }

        static void addToChannelSmite(ChannelEntry c)
        {
            if (channel_smite == null)
            {
                return;
            }

            if (!(c.channel_type.IsOf(ChannelType.Harm) && c.channel_type.IsBase()))
            {
                return;
            }

            help.AddFeaturePrerequisiteOr(channel_smite, c.parent_feature);
            
            var new_actions = help.changeAction<ContextActionDealDamage>(c.ability.GetComponent<AbilityEffectRunAction>().Actions.Actions, cad => cad.IgnoreCritical = true);
            var resounding_blow = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("9047cb1797639924487ec0ad566a3fea");
            var smite_evil = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("7bb9eb2042e67bf489ccd1374423cdec");
            
            var buff = Helper.CreateBlueprintBuff("ChannelSmite" + c.ability.name + "Buff",
                $"{LocalizationTool.GetString("CEE.Smite.Name")} ({c.ability.Name})",
                channel_smite.Description, icon: resounding_blow.Icon);
            buff.SetComponents
                (
                help.CreateAddInitiatorAttackWithWeaponTrigger(Helper.CreateActionList(new_actions),check_weapon_range_type: true),
                help.CreateAddInitiatorAttackWithWeaponTrigger(Helper.CreateActionList(new ContextActionRemoveSelf()), check_weapon_range_type: true, only_hit: false, on_initiator: true)
                );
            buff.AddComponents(c.ability.GetComponents<ContextRankConfig>());

            var apply_buff = Helper.CreateContextActionApplyBuff(buff,
                Helper.CreateContextDurationValue(bonus: Helper.CreateContextValue(1), rate: Kingmaker.UnitLogic.Mechanics.DurationRate.Rounds),
                dispellable: false
                );

            var ability = Helper.CreateBlueprintAbility("ChannelSmite" + c.ability.name, buff.Name, buff.Description, buff.Icon,
                AbilityType.Supernatural, Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Swift, AbilityRange.Personal);
            ability.SetComponents
                (
                smite_evil.GetComponent<AbilitySpawnFx>(),
                c.ability.GetComponent<AbilityResourceLogic>(),
                Helper.CreateAbilityEffectRunAction(save: SavingThrowType.Unknown, apply_buff),
                c.ability.GetComponent<ContextRankConfig>(),
                c.ability.GetComponent<ContextCalculateAbilityParamsBasedOnClasses>(),
                help.CreateAbilityShowIfCasterHasFacts(Array.ConvertAll(c.required_facts.AddToArray(channel_smite), x => x.ToRef()))
                );
            ability.TargetSelf();
            ability.NeedEquipWeapons = true;
            ability.Animation = Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionCastSpell.CastAnimationStyle.EnchantWeapon;

            c.base_ability.addToAbilityVariants(ability.ToRef());
            updateItemsForChannelDerivative(c.ability, ability);

            foreach (var checker in c.ability.GetComponents<IAbilityRestriction>())
            {
                ability.AddComponent(checker as BlueprintComponent);
            }

            var smite_feature = Helper.CreateBlueprintFeature(ability.name + "Feature", displayName: ability.name, description: ability.Description, icon: ability.Icon, FeatureGroup.Feat);
            smite_feature.ComponentsArray = new BlueprintComponent[0];

            normal_smite_map.Add(c.ability.AssetGuid.ToString(), ability.AssetGuid.ToString());

            storeChannel(ability, c.parent_feature, c.channel_type | ChannelType.Smite, c.properties);
        }

        // Snipped: CreateQuickChannel - TTT

        // Snipped: CreateSwiftPositiveChannel

        // Snipped: AddToSwiftPositiveChannel

        // Snipped: AddToQuickChannel

        // Snipped: getQuickChannelVariant

        public static BlueprintAbility getChannelSmiteVariant(BlueprintAbility normal_channel_ability)
        {
            if (channel_smite == null)
            {
                return null;
            }
            return ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(normal_smite_map[normal_channel_ability.AssetGuid.ToString()]);
        }


        static void addToSelectiveChannel(BlueprintFeature parent_feature)
        {
            selective_channel.AddComponent(Helper.CreatePrerequisiteFeature(parent_feature, true));
        }

        // Snipped: CreateExtraChannelFeat - Now Vanilla

        static internal void updateItemsForChannelDerivative(BlueprintAbility original_ability, BlueprintAbility derived_ability)
        {
            var config = derived_ability.GetComponent<ContextRankConfig>();

            ContextRankProgression progression = config.m_Progression;
            int step = config.m_StepLevel;
            int level_scale = (progression == ContextRankProgression.OnePlusDivStep || progression == ContextRankProgression.DivStep || progression == ContextRankProgression.StartPlusDivStep)
                                    ? step : 2;
            //phylacteries bonuses
            BlueprintEquipmentEnchantment[] enchants = new BlueprintEquipmentEnchantment[]{
                ResourcesLibrary.TryGetBlueprint<BlueprintEquipmentEnchantment>("60f06749fa4729c49bc3eb2eb7e3b316"), // NegativeChanneling1
                ResourcesLibrary.TryGetBlueprint<BlueprintEquipmentEnchantment>("f5d0bf8c1b4574848acb8d1fbb544807"), // PositiveChanneling1
                ResourcesLibrary.TryGetBlueprint<BlueprintEquipmentEnchantment>("cb4a39044b59f5e47ad5bc08ff9d6669"), // NegativeChanneling2
                ResourcesLibrary.TryGetBlueprint<BlueprintEquipmentEnchantment>("e988cf802d403d941b2ed8b6016de68f"), // PositiveChanneling2
                // Also both for 4? TODO
            };

            foreach (var e in enchants)
            {
                var boni = e.GetComponents<AddCasterLevelEquipment>().ToArray();
                foreach (var b in boni)
                {
                    if (b.Spell == original_ability)
                    {
                        var b2 = b.Clone();
                        b2.m_Spell = derived_ability.ToRef();
                        b2.Bonus = boni[0].Bonus / 2 * level_scale;
                        e.AddComponent(b2);
                    }
                }
            }


            BlueprintBuff[] buffs = new BlueprintBuff[] { ResourcesLibrary.TryGetBlueprint<BlueprintBuff>("b5ebb94df76531c4ca4f13bfd91efd4e") }; // BlackLinnormStewBuffCompanion

            foreach (var buff in buffs)
            {
                var boni = buff.GetComponents<AddCasterLevelForAbility>().ToArray();
                foreach (var b in boni)
                {
                    if (b.Spell == original_ability)
                    {
                        var b2 = b.Clone();
                        b2.m_Spell = derived_ability.ToRef();
                        b2.Bonus = boni[0].Bonus / 2 * level_scale;
                        buff.AddComponent(b2);
                    }
                }
            }
        }

    }
}
