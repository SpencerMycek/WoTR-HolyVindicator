using CodexLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Abilities;
using System.Collections.Generic;
using UnityEngine;
using Kingmaker.Blueprints.Root;
using HarmonyLib;
using BlueprintCore.Utils;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.ElementsSystem;
using Kingmaker.UnitLogic.Mechanics.Components;
using Helper = CodexLib.Helper;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using static HolyVindicator.Mechanics;
using Kingmaker.Enums.Damage;
using Kingmaker.RuleSystem;

namespace HolyVindicator
{
    internal class HolyVindicator
    {
        // Known Issues
        // Angelfire apostel does not fit 100% into channeling yet, I think
        // Oracle and Inquisitor vindicators get 2 more levels of spells known than expected; Because they do not properly skip levels 5 and 9 for spellbook progression.
        // Still working on Divine Wrath/Judgement/Retribution

        internal static readonly ExtraActivatableAbilityGroup StigmataAbilityGroup = new(0x110519);
        internal static readonly List<BlueprintAbilityReference> FaithHealingAbilityReferences = 
            new List<BlueprintAbilityReference> {
                "5590652e1c2225c4ca30c4a699ab3649".ToAny(), // CureLightWoundsCast
                "6b90c773a6543dc49b2505858ce33db5".ToAny(), // CureModerateWoundsCast
                "41c9016596fe1de4faf67425ed691203".ToAny(), // CureCriticalWoundsCast
                "3361c5df793b4c8448756146a88026ad".ToAny(), // CureSeriousWoundsCast
                "5d3d689392e4ff740a761ef346815074".ToAny(), // CureLightWoundsMass
                "571221cc141bc21449ae96b3944652aa".ToAny(), // CureModerateWoundsMass
                "0cea35de4d553cc439ae80b3a8724397".ToAny(), // CureSeriousWoundsMass
                "1f173a16120359e41a20fc75bb53d449".ToAny(), // CureCriticalWoundsMass
                "e84cb97373ca6174397bfe778a039eab".ToAny(), // WhiteMageCureCriticalWoundsCast
                "22a5d013be997dd479c19421343cfb00".ToAny(), // WhiteMageCureCriticalWoundsMass
                "83d6d8f4c4d296941838086f60485fb7".ToAny(), // WhiteMageCureLightWoundsCast
                "44cf8a9f080a23f4689b4bb51e3bdb64".ToAny(), // WhiteMageCureModerateWoundsCast
                "4d616f08e68288f438c8e6ce57672a56".ToAny(), // WhiteMageCureModerateWoundsMass
                "1203e2dab8a593a459c0cc688f568052".ToAny(), // WhiteMageCureSeriousWoundsCast
                "586e964c75e0c6a46884a1bea3e05cdf".ToAny()  // WhiteMageCureSeriousWoundsMass
            };
        internal static List<BlueprintBuffReference> stigmata_buffs = new();

        public static void Configure()
        {
            Main.Print("Configuring");

            // Feats that need to be created somewhere:
            ChannelEnergyEngine.createVersatileChanneler();
            ChannelEnergyEngine.createChannelingScourge();

            CreateHVClassBlueprint();
        }

        private static void CreateHVClassBlueprint()
        {
            Main.Print("Class Blueprint");
            string name = "HolyVindicatorClass";
            string guid = Helper.GetGuid(name);
            var bp = new BlueprintCharacterClass
            {
                SkillPoints = 2,
                HitDie = Kingmaker.RuleSystem.DiceType.D10,
                HideIfRestricted = false,
                PrestigeClass = true,
                IsMythic = false,
                m_IsHigherMythic = false,
                m_BaseAttackBonus = Helper.ToRef<BlueprintStatProgressionReference>("b3057560ffff3514299e8b93e7648a9d"), // BABFull
                m_FortitudeSave = Helper.ToRef<BlueprintStatProgressionReference>("1f309006cd2855e4e91a6c3707f3f700"), // SavesPrestigeHigh
                m_ReflexSave = Helper.ToRef<BlueprintStatProgressionReference>("dc5257e1100ad0d48b8f3b9798421c72"), // SavesPrestigeLow
                m_WillSave = Helper.ToRef<BlueprintStatProgressionReference>("1f309006cd2855e4e91a6c3707f3f700"), // SavesPrestigeHigh
                ClassSkills = CreateHVClassSkills(),
                IsArcaneCaster = false,
                IsDivineCaster = true,
                m_Difficulty = 2,
                LocalizedName = LocalizationTool.GetString("HV.Class.Name"),
                LocalizedDescription = LocalizationTool.GetString("HV.Class.Description"),
                LocalizedDescriptionShort = LocalizationTool.GetString("HV.null"),
            };

            bp.AddAsset(guid);
            RegisterClass(bp);

            bp.SetComponents(CreateHVPrerequisites());
            bp.AddComponents(new SkipLevelsForSpellProgression { Levels = new int[] { 5, 9 } });
            // progression
            var progression = CreateHVProgression(bp.ToRef<BlueprintCharacterClassReference>());
            bp.m_Progression = AnyRef.ToAny(progression);


        }


        // Prereqs
        private static BlueprintComponent[] CreateHVPrerequisites()
        {
            Main.Print("Prereqs");
            // Channel Energy %Any%
            return new BlueprintComponent[]
            {
                new PrerequisiteCasterTypeSpellLevel { RequiredSpellLevel = 1, IsArcane = false, OnlySpontaneous = false },
                new PrerequisiteStatValue { Stat = StatType.BaseAttackBonus, Value = 5 },
                new PrerequisiteStatValue { Stat = StatType.SkillLoreReligion, Value = 5 },
                // Channel Energy
                new PrerequisiteFeature { m_Feature = Helper.ToRef<BlueprintFeatureReference>("fd30c69417b434d47b6b03b9c1f568ff") }, // SelectiveChannel
            };
        }

        // Class Skills
        private static StatType[] CreateHVClassSkills()
        {
            Main.Print("Class Skills");
            return new StatType[] { StatType.SkillAthletics, StatType.SkillKnowledgeArcana, StatType.SkillLoreReligion, StatType.SkillPersuasion };
        }

        private static void RegisterClass(BlueprintCharacterClass classToRegister)
        {
            Main.Print("Register");
            BlueprintCharacterClassReference classRef = classToRegister.ToRef();
            BlueprintProgression.ClassWithLevel classWLvl = new() { AdditionalLevel = 0, m_Class = classRef };
            BlueprintRoot.Instance.Progression.m_CharacterClasses = BlueprintRoot.Instance.Progression.m_CharacterClasses.AddToArray(classRef);
            BlueprintProgression spell_spec_prog = ResourcesLibrary.TryGetBlueprint<BlueprintProgression>("fe9220cdc16e5f444a84d85d5fa8e3d5"); // SpellSpecializationProgression
            spell_spec_prog.m_Classes = spell_spec_prog.m_Classes.AddToArray(classWLvl);
        }

        // Progression

        private static BlueprintProgression CreateHVProgression(BlueprintCharacterClassReference classRef)
        {
            Main.Print("Progression");
            BlueprintFeature holy_vindicator_proficiencies = CreateHVProficiencies();
            BlueprintFeature holy_vindicator_shield = CreateHVShield();
            BlueprintFeature channel_energy_progression = CreateHVChannelProgression(classRef);
            BlueprintFeature spellbook_selection = CreateHVSpellbook(classRef);
            BlueprintFeature channel_smite = CreateHVChannelSmite();
            BlueprintFeature versatile_channel = CreateHVVersatileChannel();
            BlueprintFeature stigmata = CreateHVStigmata(classRef);
            (BlueprintFeature, BlueprintFeature) faith_healing = CreateHVFaithHealing(); // TODO: Can probably fix
            (BlueprintFeature, BlueprintFeature) quicken_stigmata = CreateHVStigmataActionModifiers();
            var (bloodfire, bloodrain) = CreateHVBloodfireandBloodrain();

            var bp = Helper.CreateBlueprintProgression("HolyVindicatorProgression");
            var entry_lvl_1 = Helper.CreateLevelEntry(1, holy_vindicator_proficiencies, holy_vindicator_shield, channel_energy_progression);
            var entry_lvl_2 = Helper.CreateLevelEntry(2, spellbook_selection, stigmata);
            var entry_lvl_3 = Helper.CreateLevelEntry(3, faith_healing.Item1);
            var entry_lvl_4 = Helper.CreateLevelEntry(4, new AnyRef[0]); // Divine Wrath
            var entry_lvl_5 = Helper.CreateLevelEntry(5, channel_smite, bloodfire);
            var entry_lvl_6 = Helper.CreateLevelEntry(6, versatile_channel, quicken_stigmata.Item1);
            var entry_lvl_7 = Helper.CreateLevelEntry(7, new AnyRef[0]); // Divine Judgement
            var entry_lvl_8 = Helper.CreateLevelEntry(8, faith_healing.Item2);
            var entry_lvl_9 = Helper.CreateLevelEntry(9, bloodrain);
            var entry_lvl_0 = Helper.CreateLevelEntry(10, quicken_stigmata.Item2); // Divine Retribution
            Helper.AddEntries(bp, entry_lvl_1, entry_lvl_2, entry_lvl_3, entry_lvl_4, entry_lvl_5, entry_lvl_6, entry_lvl_7, entry_lvl_8, entry_lvl_9, entry_lvl_0);
            bp.m_Classes = new BlueprintProgression.ClassWithLevel { AdditionalLevel = 0, m_Class = classRef }.ObjToArray();

            bp.m_UIDeterminatorsGroup = new BlueprintFeatureBaseReference[] { holy_vindicator_proficiencies.ToRef<BlueprintFeatureBaseReference>() };
            bp.UIGroups = new UIGroup[]  {
                help.CreateUIGroup(channel_energy_progression.ToRef<BlueprintFeatureBaseReference>(), spellbook_selection.ToRef<BlueprintFeatureBaseReference>(), channel_smite.ToRef<BlueprintFeatureBaseReference>(), versatile_channel.ToRef<BlueprintFeatureBaseReference>()),
                help.CreateUIGroup(stigmata.ToRef<BlueprintFeatureBaseReference>(), (quicken_stigmata.Item1).ToRef<BlueprintFeatureBaseReference>(), (quicken_stigmata.Item2).ToRef<BlueprintFeatureBaseReference>()),
                help.CreateUIGroup(holy_vindicator_shield.ToRef<BlueprintFeatureBaseReference>(), (faith_healing.Item1).ToRef<BlueprintFeatureBaseReference>(), (faith_healing.Item2).ToRef<BlueprintFeatureBaseReference>(), bloodfire.ToRef<BlueprintFeatureBaseReference>(), bloodrain.ToRef<BlueprintFeatureBaseReference>()),
            };

            return bp;
        }

        // Proficiencies

        private static BlueprintFeature CreateHVProficiencies()
        {
            var bp = Helper.CreateBlueprintFeature("HolyVindicatorProficiencies",
                displayName: LocalizationTool.GetString("HV.Prof.Name"),
                description: LocalizationTool.GetString("HV.Prof.Description"));
            bp.IsClassFeature = true;
            bp.SetComponents
                (
                Helper.CreateAddFacts
                    (
                    AnyRef.ToAny("6d3728d4e9c9898458fe5e9532951132"), // LightArmorProficiency
                    AnyRef.ToAny("46f4fb320f35704488ba3d513397789d"), // MediumArmorProficiency
                    AnyRef.ToAny("1b0f68188dcc435429fb87a022239681"), // HeavyArmorProficiency
                    AnyRef.ToAny("e70ecf1ed95ca2f40b754f1adb22bbdd"), // SimpleWeaponProficiency
                    AnyRef.ToAny("203992ef5b35c864390b4e4a1e200629"), // MartialWeaponProficiency
                    AnyRef.ToAny("cb8686e7357a68c42bdd9d4e65334633")  // ShieldsProficiency
                    )
                );
            return bp;
        }

        // Spells

        private static BlueprintFeatureSelection CreateHVSpellbook(BlueprintCharacterClassReference classRef)
        {
            var spellbook_progressions = new BlueprintFeatureReference[12];
            #region Individual Spellbooks
            // Each Needs:
            //  Progression:
            //      Group: HolyVindicatorSpellbook
            #region AngelFireApostate
            var str = "AngelFireApostle";
            var advancing_archetype = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>("857bc9fadf70f294795a9cba974a48b8"); // AngelFireApostleArchetype
            var advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0"); // ClericClass  
            var spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("d0313b3110357b14ebd3102c2d4cde20"); // AngelFireApostleSpellbook

            spellbook_progressions[0] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, false, advancing_archetype).ToAny();

            #endregion
            #region Crusader
            str = "Crusader";
            advancing_archetype = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>("6bfb7e74b530f3749b590286dd2b9b30"); // CrusaderArchetype
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0"); // ClericClass  
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("673d39f7da699aa408cdda6282e7dcc0"); // CrusaderSpellbook

            spellbook_progressions[1] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, false, advancing_archetype).ToAny();

            #endregion
            #region Cleric
            str = "Cleric";
            var no_archetype1 = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>("857bc9fadf70f294795a9cba974a48b8"); // AngelFireApostleArchetype
            var no_archetype2 = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>("6bfb7e74b530f3749b590286dd2b9b30"); // CrusaderArchetype
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0"); // ClericClass
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("4673d19a0cf2fab4f885cc4d1353da33"); // ClericSpellbook

            spellbook_progressions[2] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, true, no_archetype1, no_archetype2).ToAny();
            #endregion

            #region Feyspeaker
            str = "Feyspeaker";
            advancing_archetype = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>("da69747aa3dd0044ebff5f3d701cdde3"); // FeyspeakerArchetype
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("610d836f3a3a9ed42a4349b62f002e96"); // DruidClass  
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("c8c471f1f9889e1408347d3c7987b4f1"); // FeyspeakerSpellbook

            spellbook_progressions[3] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, false, advancing_archetype).ToAny();

            #endregion
            #region Druid
            str = "Druid";
            no_archetype1 = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>("da69747aa3dd0044ebff5f3d701cdde3"); // FeyspeakerArchetype
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("610d836f3a3a9ed42a4349b62f002e96"); // DruidClass
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("fc78193f68150454483a7eea8b605b71"); // DruidSpellbook

            spellbook_progressions[4] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, true, no_archetype1).ToAny();

            #endregion

            #region Inquisitor
            // Custom entries 2|2|133|3|3|244|14|4|355|25|5|466|36|6|5|46
            // MysticTheurgeInquisitorLevelSelectionX
            var mtils1 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("8ae18c62c0fbfeb4ea77f877883947fd"); //1
            var mtils2 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("f78f63d364bd9fe4ea2885d95432c068"); //2
            var mtils3 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("5f6c7b84edc68a146955be0600de4095"); //3
            var mtils4 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("b93df7bf0405a974cafcda21cbd070f1"); //4
            var mtils5 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("b7ed1fc44730bd1459c57763378a5a97"); //5
            var mtils6 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("200dec6712442e74c85803a1af72397a"); //6


            str = "Inquisitor";
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("f1a70d9e1b0b41e49874e1fa9052a1ce"); // InquisitorClass
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("57fab75111f377248810ece84193a5a5"); // InquisitorSpellbook
            var level_up = Helper.CreateBlueprintFeature("HolyVindicator" + str + "LevelUp", displayName: advancing_class.LocalizedName, description: LocalizationTool.GetString("HV.Spells.Description"));
            level_up.Ranks = 7;
            level_up.IsClassFeature = true;

            var entry01 = Helper.CreateLevelEntry(5, level_up, mtils2);
            var entry02 = Helper.CreateLevelEntry(6, level_up, mtils2);
            var entry03 = Helper.CreateLevelEntry(7, level_up, mtils1, mtils3, mtils3);
            var entry04 = Helper.CreateLevelEntry(8, level_up, mtils3);
            var entry05 = Helper.CreateLevelEntry(9, level_up, mtils3);
            var entry06 = Helper.CreateLevelEntry(10, level_up, mtils2, mtils4, mtils4);
            var entry07 = Helper.CreateLevelEntry(11, level_up, mtils1, mtils4);
            var entry08 = Helper.CreateLevelEntry(12, level_up, mtils4);
            var entry09 = Helper.CreateLevelEntry(13, level_up, mtils3, mtils5, mtils5);
            var entry10 = Helper.CreateLevelEntry(14, level_up, mtils2, mtils5);
            var entry11 = Helper.CreateLevelEntry(15, level_up, mtils5);
            var entry12 = Helper.CreateLevelEntry(16, level_up, mtils4, mtils6, mtils6);
            var entry13 = Helper.CreateLevelEntry(17, level_up, mtils3, mtils6);
            var entry14 = Helper.CreateLevelEntry(18, level_up, mtils6);
            var entry15 = Helper.CreateLevelEntry(19, level_up, mtils5);
            var entry16 = Helper.CreateLevelEntry(20, level_up, mtils4, mtils6);
            var entries = new LevelEntry[] { entry01, entry02, entry03, entry04, entry05, entry06, entry07, entry08, entry09, entry10, entry11, entry12, entry13, entry14, entry15, entry16 };

            BlueprintProgression inquis_progression = CreateHVSpellbookSelections(str, classRef, spell_book, entries, advancing_class, true, null).ToAny();
            inquis_progression.m_Classes = new BlueprintProgression.ClassWithLevel[] {
                new BlueprintProgression.ClassWithLevel { AdditionalLevel = 0, m_Class = classRef },
                new BlueprintProgression.ClassWithLevel { AdditionalLevel = 0, m_Class = advancing_class.ToAny() } };
            inquis_progression.m_ExclusiveProgression = classRef;
            spellbook_progressions[5] = inquis_progression.ToAny();

            #endregion
            #region Oracle
            // Custom entries X|1|2|12|3|123|4|234|5|2345|4|456|7|67|8|78|9|89|9
            // Start lvl 2
            // MysticTheurgeOracleLevelSelectionX
            mtils1 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("3cb30f805e79a7944b0d0174c9a157b7"); //1
            mtils2 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("5290b422c5c02c44f99291688e798d8f"); //2
            mtils3 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("f8cf4644c7ff15d4799a59593b900091"); //3
            mtils4 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("7f526cef9fcb7934aaaca1bf3d26fc0f"); //4
            mtils5 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("2bf8b6fa7082d45409537d6112cb5647"); //5
            mtils6 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("798bf6e210d9a0e42bf960390b5991ba"); //6
            var mtils7 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("6bbc04c6c95fc694788ddb265dc15ea2"); //7
            var mtils8 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("f6c08ecbd77a43b4dbc0252afc2cb578"); //8
            var mtils9 = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("57dae97f6e587f54e9c0559c6fa6590f"); //9

            str = "Oracle";
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("20ce9bf8af32bee4c8557a045ab499b1"); // OracleClass
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("6c03364712b415941a98f74522a81273"); // OracleSpellbook
            level_up = Helper.CreateBlueprintFeature("HolyVindicator" + str + "LevelUp", displayName: advancing_class.LocalizedName, description: LocalizationTool.GetString("HV.Spells.Description"));
            level_up.Ranks = 7;
            level_up.IsClassFeature = true;

            entry01 = Helper.CreateLevelEntry(2, level_up);
            entry02 = Helper.CreateLevelEntry(3, level_up, mtils1);
            entry03 = Helper.CreateLevelEntry(4, level_up, mtils2);
            entry04 = Helper.CreateLevelEntry(5, level_up, mtils1, mtils2);
            entry05 = Helper.CreateLevelEntry(6, level_up, mtils3);
            entry06 = Helper.CreateLevelEntry(7, level_up, mtils1, mtils2, mtils3);
            entry07 = Helper.CreateLevelEntry(8, level_up, mtils4);
            entry08 = Helper.CreateLevelEntry(9, level_up, mtils2, mtils3, mtils4);
            entry09 = Helper.CreateLevelEntry(10, level_up, mtils5);
            entry10 = Helper.CreateLevelEntry(11, level_up, mtils2, mtils3, mtils4, mtils5);
            entry11 = Helper.CreateLevelEntry(12, level_up, mtils4);
            entry12 = Helper.CreateLevelEntry(13, level_up, mtils4, mtils5, mtils6);
            entry13 = Helper.CreateLevelEntry(14, level_up, mtils7);
            entry14 = Helper.CreateLevelEntry(15, level_up, mtils6, mtils7);
            entry15 = Helper.CreateLevelEntry(16, level_up, mtils8);
            entry16 = Helper.CreateLevelEntry(17, level_up, mtils7, mtils8);
            var entry17 = Helper.CreateLevelEntry(18, level_up, mtils9);
            var entry18 = Helper.CreateLevelEntry(19, level_up, mtils8, mtils9);
            var entry19 = Helper.CreateLevelEntry(20, level_up, mtils9);
            entries = new LevelEntry[] { entry01, entry02, entry03, entry04, entry05, entry06, entry07, entry08, entry09, entry10, entry11, entry12, entry13, entry14, entry15, entry16, entry17, entry18, entry19 };

            BlueprintProgression oracle_progression = CreateHVSpellbookSelections(str, classRef, spell_book, entries, advancing_class, true, null).ToAny();
            oracle_progression.m_Classes = new BlueprintProgression.ClassWithLevel[] {
                new BlueprintProgression.ClassWithLevel { AdditionalLevel = 0, m_Class = classRef },
                new BlueprintProgression.ClassWithLevel { AdditionalLevel = 0, m_Class = advancing_class.ToAny() } };
            oracle_progression.m_ExclusiveProgression = classRef;
            spellbook_progressions[6] = oracle_progression.ToAny();
            #endregion
            #region Paladin
            str = "Paladin";
            no_archetype1 = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>("5693945afac189a469ef970eac8f71d9"); // DivineGuardianArchetype
            no_archetype2 = ResourcesLibrary.TryGetBlueprint<BlueprintArchetype>("cc70a9fdcd8781a4ca2f0e594f066964"); // WarriorOfTheHolyLightArchetype
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("bfa11238e7ae3544bbeb4d0b92e897ec"); // PaladinClass
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("bce4989b070ce924b986bf346f59e885"); // PaladinSpellbook

            spellbook_progressions[7] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, true, no_archetype1, no_archetype2).ToAny();

            #endregion
            #region Ranger
            str = "Ranger";
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("cda0615668a6df14eb36ba19ee881af6"); // RangerClass
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("762858a4a28eaaf43aa00f50441d7027"); // RangerSpellbook

            spellbook_progressions[8] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, true, null).ToAny();
            #endregion
            #region Shaman
            str = "Shaman";
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("145f1d3d360a7ad48bd95d392c81b38e"); // ShamanClass
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("44f16931dabdff643bfe2a48138e769f"); // ShamanSpellbook

            spellbook_progressions[9] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, true, null).ToAny();
            #endregion
            #region Hunter
            str = "Hunter";
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("34ecd1b5e1b90b9498795791b0855239"); // HunterClass
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("885cd422aa357e2409146b38bb1fec51"); // HunterSpellbook

            spellbook_progressions[10] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, true, null).ToAny();
            #endregion
            #region Warpriest
            str = "Warpriest";
            advancing_class = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("30b5e47d47a0e37438cc5a80c96cfb99"); // WarpriestClass  
            spell_book = AnyRef.ToRef<BlueprintSpellbookReference>("7d7d51be2948d2544b3c2e1596fd7603"); // WarpriestSpellbook

            spellbook_progressions[11] = CreateHVSpellbookSelections(str, classRef, spell_book, null, advancing_class, true, null).ToAny();

            #endregion

            #endregion

            var bp = Helper.CreateBlueprintFeatureSelection("HolyVindicatorSpellbook",
                displayName: LocalizationTool.GetString("HV.Spells.Name"),
                description: LocalizationTool.GetString("HV.Spells.Description"),
                group: FeatureGroup.ReplaceSpellbook);
            bp.m_AllFeatures = spellbook_progressions;
            return bp;
        }

        private static BlueprintProgression CreateHVSpellbookSelections(string name, BlueprintCharacterClassReference classRef, BlueprintSpellbookReference spell_book, LevelEntry[] custom_levels, BlueprintCharacterClass advancing_class, bool isBaseClass, params BlueprintArchetype[] advancing_archetype)
        {
            var display = (!isBaseClass && advancing_archetype != null) ? advancing_archetype[0].LocalizedName : advancing_class.LocalizedName;
            LevelEntry[] entries;
            if (custom_levels == null)
            {
                var level_up = Helper.CreateBlueprintFeature("HolyVindicator" + name + "LevelUp",
                    displayName: display, description: LocalizationTool.GetString("HV.Spells.Description"));
                level_up.Ranks = 7;
                level_up.IsClassFeature = true;
                level_up.SetComponents(new AddSpellbookLevel { m_Spellbook = spell_book });
                entries = new LevelEntry[9];
                for (int i = 1; i <= 9; i++)
                {
                    if (i != 4 && i != 8)
                    {
                        entries[i - 1] = Helper.CreateLevelEntry(i, level_up);
                    }
                    else
                    {
                        entries[i - 1] = Helper.CreateLevelEntry(i, new AnyRef[0]);
                    }
                }
            } else
            {
                entries = custom_levels;
            }
            var progression = Helper.CreateBlueprintProgression("HolyVindicator" + name + "Progression",
                displayname: display, description: LocalizationTool.GetString("HV.Spells.Description"));
            progression.m_Classes = new BlueprintProgression.ClassWithLevel() { AdditionalLevel = 0, m_Class = classRef }.ObjToArray();
            progression.SetComponents(new PrerequisiteClassSpellLevel() { m_CharacterClass = advancing_class.ToAny(), RequiredSpellLevel = 1 });

            if (!isBaseClass && advancing_archetype != null)
            {
                foreach (var archetype in advancing_archetype)
                {
                    progression.AddComponents(new PrerequisiteArchetypeLevel() { m_CharacterClass = advancing_class.ToAny(), m_Archetype = archetype.ToAny() });
                }
            } else if (isBaseClass && advancing_archetype != null)
            {
                foreach (var archetype in advancing_archetype)
                {
                    progression.AddComponents(new PrerequisiteNoArchetype() { m_CharacterClass = advancing_class.ToAny(), m_Archetype = archetype.ToAny() });
                }
            }


            Helper.AddEntries(progression, entries);

            return progression;
        }

        // Channel Energy Stack with other sources
        private static BlueprintFeature CreateHVChannelProgression(BlueprintCharacterClassReference classRef)
        {
            ChannelEnergyEngine.addClassToChannelEnergyProgression(classRef);
            var bp = Helper.CreateBlueprintFeature("HolyVindicatorChannelEnergyProgression",
                displayName: LocalizationTool.GetString("HV.Channel.Name"),
                description: LocalizationTool.GetString("HV.Channel.Description"), icon: null, group: FeatureGroup.None);
            return bp;
        }
        // Vindicator Shield, Channel Energy buff to AC
        private static BlueprintFeature CreateHVShield()
        {
            ChannelEnergyEngine.createHolyVindicatorShield();
            return ChannelEnergyEngine.holy_vindicator_shield;
        }
        // Stigmata Toggle buff, DoT, moveable buff
        private static BlueprintFeature CreateHVStigmata(BlueprintCharacterClassReference classRef)
        {
            var icon = Helper.StealIcon("d87665d2bff740d4daf07ddd36759b34"); // MartyrStigmataFeature

            var shield_icon = Helper.StealIcon("ca22afeb94442b64fb8536e7a9f7dc11"); // FightDefensivelyFeature
            var attack_icon = Helper.StealIcon("cb502c65dab407b4e928f5d8355cafc9"); // RecklessStanceFeature
            var damage_icon = Helper.StealIcon("efc60c91b8e64f244b95c66b270dbd7c"); // VitalStrikeAbility
            var caster_icon = Helper.StealIcon("e6bd839cc6bb45ecbae1e98244a704ad"); // Spellcraft
            var saves_icon = Helper.StealIcon("f6388946f9f472f4585591b80e9f2452"); // Bravery

            var cleric = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0"); // ClericClass

            #region Shared Components
            var holy_damage = Helper.CreateContextActionDealDamage(DamageEnergyType.Holy,
                Helper.CreateContextDiceValue(dice: DiceType.Zero,
                diceCount: Helper.CreateContextValue(0),
                bonus: Helper.CreateContextValue(Kingmaker.Enums.AbilityRankType.Default)));
            var holy_damage_comp = Helper.CreateAddFactContextActions(on: null, off: null, round: new GameAction[] { holy_damage });
            var unholy_damage = Helper.CreateContextActionDealDamage(DamageEnergyType.Unholy,
                Helper.CreateContextDiceValue(dice: DiceType.Zero,
                diceCount: Helper.CreateContextValue(0),
                bonus: Helper.CreateContextValue(Kingmaker.Enums.AbilityRankType.Default)));
            var unholy_damage_comp = Helper.CreateAddFactContextActions(on: null, off: null, round: new GameAction[] { unholy_damage });
            var bonus_context = Helper.CreateContextValue(Kingmaker.Enums.AbilityRankType.Default);
            var half_level_context = Helper.CreateContextRankConfig(
                baseValueType: ContextRankBaseValueType.ClassLevel,
                progression: ContextRankProgression.Div2,
                type: Kingmaker.Enums.AbilityRankType.Default, min: 1, max: 5);
            half_level_context.m_ExceptClasses = false;
            half_level_context.m_Class = new BlueprintCharacterClassReference[] { classRef };
            var bleed_immunity = new BuffDescriptorImmunity() { Descriptor = SpellDescriptor.Bleed };
            #endregion
            #region Sacred
            var comp_array = new BlueprintComponent[] { Helper.CreateAddContextStatBonus(bonus_context, StatType.AdditionalAttackBonus, descriptor: Kingmaker.Enums.ModifierDescriptor.Sacred), holy_damage_comp, bleed_immunity, half_level_context };
            var sacred_attack = CreateHVStigmataActives("AttackRolls", true, attack_icon, comp_array);

            comp_array = new BlueprintComponent[] { Helper.CreateAddContextStatBonus(bonus_context, StatType.AdditionalDamage, descriptor: Kingmaker.Enums.ModifierDescriptor.Sacred), holy_damage_comp, bleed_immunity, half_level_context };
            var sacred_damage = CreateHVStigmataActives("WeaponDamage", true, damage_icon, comp_array);

            comp_array = new BlueprintComponent[] { Helper.CreateAddContextStatBonus(bonus_context, StatType.AC, descriptor: Kingmaker.Enums.ModifierDescriptor.Sacred), holy_damage_comp, bleed_immunity, half_level_context };
            var sacred_ac = CreateHVStigmataActives("ArmorClass", true, shield_icon, comp_array);

            comp_array = new BlueprintComponent[] { new CasterLevelCheckBonus() { Value = bonus_context, ModifierDescriptor = Kingmaker.Enums.ModifierDescriptor.Sacred }, holy_damage_comp, bleed_immunity, half_level_context };
            var sacred_caster = CreateHVStigmataActives("CasterLevel", true, caster_icon, comp_array);

            comp_array = new BlueprintComponent[]
            {
                Helper.CreateAddContextStatBonus(bonus_context, StatType.SaveFortitude, descriptor: Kingmaker.Enums.ModifierDescriptor.Sacred),
                Helper.CreateAddContextStatBonus(bonus_context, StatType.SaveReflex, descriptor: Kingmaker.Enums.ModifierDescriptor.Sacred),
                Helper.CreateAddContextStatBonus(bonus_context, StatType.SaveWill, descriptor: Kingmaker.Enums.ModifierDescriptor.Sacred),
                holy_damage_comp, bleed_immunity, half_level_context
            };
            var sacred_saves = CreateHVStigmataActives("Saves", true, saves_icon, comp_array);

            #endregion
            #region Profance
            comp_array = new BlueprintComponent[] { Helper.CreateAddContextStatBonus(bonus_context, StatType.AdditionalAttackBonus, descriptor: Kingmaker.Enums.ModifierDescriptor.Profane), unholy_damage_comp, bleed_immunity, half_level_context };
            var profane_attack = CreateHVStigmataActives("AttackRolls", false, attack_icon, comp_array);

            comp_array = new BlueprintComponent[] { Helper.CreateAddContextStatBonus(bonus_context, StatType.AdditionalDamage, descriptor: Kingmaker.Enums.ModifierDescriptor.Profane), unholy_damage_comp, bleed_immunity, half_level_context };
            var profane_damage = CreateHVStigmataActives("WeaponDamage", false, damage_icon, comp_array);

            comp_array = new BlueprintComponent[] { Helper.CreateAddContextStatBonus(bonus_context, StatType.AC, descriptor: Kingmaker.Enums.ModifierDescriptor.Profane), unholy_damage_comp, bleed_immunity, half_level_context };
            var profane_ac = CreateHVStigmataActives("ArmorClass", false, shield_icon, comp_array);

            comp_array = new BlueprintComponent[] { new CasterLevelCheckBonus() { Value = bonus_context, ModifierDescriptor = Kingmaker.Enums.ModifierDescriptor.Profane }, unholy_damage_comp, bleed_immunity, half_level_context };
            var profane_caster = CreateHVStigmataActives("CasterLevel", false, caster_icon, comp_array);

            comp_array = new BlueprintComponent[]
            {
                Helper.CreateAddContextStatBonus(bonus_context, StatType.SaveFortitude, descriptor: Kingmaker.Enums.ModifierDescriptor.Profane),
                Helper.CreateAddContextStatBonus(bonus_context, StatType.SaveReflex, descriptor: Kingmaker.Enums.ModifierDescriptor.Profane),
                Helper.CreateAddContextStatBonus(bonus_context, StatType.SaveWill, descriptor: Kingmaker.Enums.ModifierDescriptor.Profane),
                unholy_damage_comp, bleed_immunity, half_level_context
            };
            var profane_saves = CreateHVStigmataActives("Saves", false, saves_icon, comp_array);
            #endregion
            var base_ability = Helper.CreateBlueprintActivatableAbility("HolyVindicatorStigmataBase", out var _,
                displayName: LocalizationTool.GetString("HV.Stigmata.Name"),
                description: LocalizationTool.GetString("HV.Stigmata.Description"),
                icon: icon, commandType: Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Standard,
                activationType: AbilityActivationType.WithUnitCommand, group: StigmataAbilityGroup);
            base_ability.SetComponents(new ActivationDisable(),
                new ActivatableAbilityVariants() { m_Variants = new BlueprintActivatableAbilityReference[]
                {
                    sacred_attack.ToAny(),
                    sacred_damage.ToAny(),
                    sacred_ac.ToAny(),
                    sacred_caster.ToAny(),
                    sacred_saves.ToAny(),
                    profane_attack.ToAny(),
                    profane_damage.ToAny(),
                    profane_ac.ToAny(),
                    profane_caster.ToAny(),
                    profane_saves.ToAny()
                } });

            var stigmata = Helper.CreateBlueprintFeature("HolyVindicatorStigmateFeature",
                displayName: LocalizationTool.GetString("HV.Stigmata.Name"),
                description: LocalizationTool.GetString("HV.Stigmata.Description"),
                icon: icon);
            stigmata.IsClassFeature = true;
            stigmata.SetComponents(Helper.CreateAddFeatureIfHasFact(base_ability));

            var add_stigmata = Helper.CreateBlueprintFeature("HolyVindicatorSacredStigmataFeature");
            add_stigmata.SetComponents(Helper.CreateAddFacts(sacred_attack, sacred_damage, sacred_ac, sacred_caster, sacred_saves));
            add_stigmata.HideInUI = true;
            stigmata.AddComponent(new AddFeatureIfHasFactsFromList() { m_Feature = add_stigmata.ToAny(), CheckedFacts = new Kingmaker.Blueprints.Facts.BlueprintUnitFact[0] });
            add_stigmata = Helper.CreateBlueprintFeature("HolyVindicatorProfaneStigmataFeature");
            add_stigmata.SetComponents(Helper.CreateAddFacts(profane_attack, profane_damage, profane_ac, profane_caster, profane_saves));
            add_stigmata.HideInUI = true;
            stigmata.AddComponent(new AddFeatureIfHasFactsFromList() { m_Feature = add_stigmata.ToAny(), CheckedFacts = new Kingmaker.Blueprints.Facts.BlueprintUnitFact[0] });

            ChannelEnergyEngine.registerStigmata(stigmata);

            return stigmata;
        }
        private static (BlueprintFeature, BlueprintFeature) CreateHVStigmataActionModifiers()
        {
            var icon = Helper.StealIcon("d87665d2bff740d4daf07ddd36759b34"); // MartyrStigmataFeature

            var move_action = Helper.CreateBlueprintFeature("HolyVindicatorStigmataMove",
                displayName: LocalizationTool.GetString("HV.Stigmata.Name")+LocalizationTool.GetString("HV.Stigmata.Name.Move"),
                description: LocalizationTool.GetString("HV.Stigmata.Description.Move"), icon: icon);
            move_action.IsClassFeature = true;
            move_action.SetComponents(new QuickenStigmataMove());
            var swift_action = Helper.CreateBlueprintFeature("HolyVindicatorStigmataSwift",
                displayName: LocalizationTool.GetString("HV.Stigmata.Name") + LocalizationTool.GetString("HV.Stigmata.Name.Swift"),
                description: LocalizationTool.GetString("HV.Stigmata.Description.Swift"), icon: icon);
            swift_action.IsClassFeature = true;
            swift_action.SetComponents(new QuickenStigmataSwift(), new RemoveFeatureOnApply() { m_Feature = move_action.ToAny() });

            return (move_action, swift_action);
        }

        private static BlueprintActivatableAbility CreateHVStigmataActives(string target_string, bool sacred, Sprite icon = null, params BlueprintComponent[] components)
        {
            var icon_profane = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("a6e59e74cba46a44093babf6aec250fc").Icon; // SlayLiving
            var icon_sacred = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>("f6f95242abdfac346befd6f4f6222140").Icon; // RemoveSickness

            icon = (icon is null) ? (sacred ? icon_sacred : icon_profane) : icon;
            string bonus_str = sacred ? "Sacred" : "Profane";
            var active = Helper.CreateBlueprintActivatableAbility("HolyVindicatorStigmata" + bonus_str + target_string, out BlueprintBuff buff,
                displayName: LocalizationTool.GetString("HV.Stigmata.Name") + LocalizationTool.GetString("HV.Stigmata." + bonus_str + "." + target_string), description: LocalizationTool.GetString("HV.Stigmata.Description"),
                icon: icon, commandType: Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Standard, activationType: Kingmaker.UnitLogic.ActivatableAbilities.AbilityActivationType.WithUnitCommand,
                group: StigmataAbilityGroup, deactivateImmediately: true, onByDefault: false, onlyInCombat: false, deactivateEndOfCombat: false, deactivateAfterRound: false,
                deactivateWhenStunned: false, deactivateWhenDead: true, deactivateOnRest: true, useWithSpell: false);
            active.HiddenInUI = true;
            buff.Flags(hidden: false, stayOnDeath: false, isFromSpell: false, harmful: false);
            buff.IsClassFeature = true;
            buff.SetComponents(components);

            stigmata_buffs.Add(buff.ToRef());

            return active;
        }

        // Faith Healing Self heals empower, Greater: maximised
        private static (BlueprintFeature, BlueprintFeature) CreateHVFaithHealing()
        {
            // RemoveFeatureOnApply for greater // Autometamagic for all cure spells
            var lesser = Helper.CreateBlueprintFeature("HolyVindicatorFaithHealingEmpower", 
                displayName: LocalizationTool.GetString("HV.Faith.Name")+LocalizationTool.GetString("HV.Faith.Empower.Name"),
                description: LocalizationTool.GetString("HV.Faith.Empower.Description"));
            lesser.IsClassFeature = true;
            lesser.SetComponents(Helper.CreateAutoMetamagic(Metamagic.Empower, abilities: FaithHealingAbilityReferences));
            var greater = Helper.CreateBlueprintFeature("HolyVindicatorFaithHealingMaximize",
                displayName: LocalizationTool.GetString("HV.Faith.Name") + LocalizationTool.GetString("HV.Faith.Maximize.Name"),
                description: LocalizationTool.GetString("HV.Faith.Maximize.Description"));
            greater.IsClassFeature = true;
            greater.SetComponents(
                Helper.CreateAutoMetamagic(Metamagic.Maximize, abilities: FaithHealingAbilityReferences),
                new RemoveFeatureOnApply() { m_Feature = lesser.ToAny() });
            return (lesser, greater);
        }

        // Divine Wrath Toggle sacrifice  1st level spell/slot to invoke "doom", increase to DC based on crit multiplier
        // Option to Consume Slot, like auto convert spells or arcanist or spellslots for buff that has On critical or something
        // Spontaneous Spell Conversion -> Buff -> Something like Blinding Critical (AddInitiatorAttackWithWeaponTrigger) -> Remove Buff (ContextActionRemoveBuff)
        private static BlueprintFeature CreateHVDivineWrath()
        {
            var buff = Helper.CreateBlueprintBuff("HolyVindicatorDivineWrathBuff").Flags(hidden: false);
            var ability = Helper.CreateBlueprintAbility("HolyVindicatorDivineWrathAbility",
                displayName: LocalizationTool.GetString("HV.Wrath.Name"), description: LocalizationTool.GetString("HV.Wrath.Description"));
            var feat = Helper.CreateBlueprintFeature("HolyVindicatorDivineWrathFeature",
                displayName: LocalizationTool.GetString("HV.Wrath.Name"), description: LocalizationTool.GetString("HV.Wrath.Description"));
            return feat;
            //ContextActionCastSpell
            //new AddIncomingDamageTrigger
            //BlueprintArchetype
            //Kingmaker.Blueprints.ReferenceArrayProxy
        }
        private static BlueprintFeature CreateHVDivineJudgment()
        {
            var bp = new BlueprintFeature();
            return bp;
        }
        // Divine Retribution (Same as Divine Judgement)
        private static BlueprintFeature CreateHVDivineRetribution()
        {
            var bp = new BlueprintFeature();
            return bp;
        }

        // Bloodrain Feature checked for in stigmata buff, bonus damage and effects for channel (Needs engine)
        // Bloodfire Feature checked for in Channel Smite, bonus damage and effects for smite
        private static (BlueprintFeature, BlueprintFeature) CreateHVBloodfireandBloodrain()
        {
            var bloodfire = Helper.CreateBlueprintFeature("HolyVindicatorBloodfireFeature",
                displayName: LocalizationTool.GetString("HV.Bloodfire.Name"),
                description: LocalizationTool.GetString("HV.Bloodfire.Description"), icon: null, group: FeatureGroup.None);
            var bloodfire_buff = Helper.CreateBlueprintBuff("HolyVindicatorBloodfireBuff", bloodfire.Name, bloodfire.Description);
            bloodfire_buff.Flags(hidden: true);

            var bloodrain = Helper.CreateBlueprintFeature("HolyVindicatorBloodrainFeature",
                displayName: LocalizationTool.GetString("HV.Bloodrain.Name"),
                description: LocalizationTool.GetString("HV.Bloodrain.Description"), icon: null, group: FeatureGroup.None);
            var bloodrain_buff = Helper.CreateBlueprintBuff("HolyVindicatorBloodrainBuff", bloodrain.Name, bloodrain.Description);
            bloodrain_buff.Flags(hidden: true);

            foreach (var buff in stigmata_buffs)
            {
                help.addContextActionApplyBuffOnFactsToActivatedAbilityBuffNoRemove(buff, bloodfire_buff, bloodfire);
                help.addContextActionApplyBuffOnFactsToActivatedAbilityBuffNoRemove(buff, bloodrain_buff, bloodrain);
            }

            var bleed_buff = ResourcesLibrary.TryGetBlueprint<BlueprintBuff>("75039846c3d85d940aa96c249b97e562"); // Bleed1d6Buff
            var sickened_buff = ResourcesLibrary.TryGetBlueprint<BlueprintBuff>("4e42460798665fd4cb9173ffa7ada323"); // Sickened

            var blood_buff = Helper.CreateBlueprintBuff("HolyVindicatorBloodBuff");
            blood_buff.Flags(hidden: true);
            blood_buff.SetComponents
                (
                Helper.CreateAddFactContextActions(new GameAction[]{
                    Helper.CreateContextActionApplyBuff(bleed_buff, Helper.CreateContextDurationValue(), dispellable: false), 
                    Helper.CreateContextActionApplyBuff(sickened_buff, Helper.CreateContextDurationValue(), dispellable: false) }),
                new BuffRemoveOnSave() { SaveType = SavingThrowType.Will }
                );

            var apply_buff_action = Helper.CreateContextActionApplyBuff(blood_buff, 1, permanent: true);
            var save_failed_action = Helper.CreateContextActionConditionalSaved(null, apply_buff_action);

            var positive_damage = Helper.CreateContextActionDealDamage(DamageEnergyType.PositiveEnergy, Helper.CreateContextDiceValue(DiceType.D6, 1), IgnoreCritical: true);
            var negative_damage = Helper.CreateContextActionDealDamage(DamageEnergyType.NegativeEnergy, Helper.CreateContextDiceValue(DiceType.D6, 1), IgnoreCritical: true);

            var smite_positive_action = help.CreateConditional(Helper.CreateContextConditionCasterHasFact(bloodfire_buff.ToAny()), new GameAction[] { positive_damage, save_failed_action });
            var smite_negative_action = help.CreateConditional(Helper.CreateContextConditionCasterHasFact(bloodfire_buff.ToAny()), new GameAction[] { negative_damage, save_failed_action });
            var positive_action = help.CreateConditional(Helper.CreateContextConditionCasterHasFact(bloodrain_buff.ToAny()), new GameAction[] { positive_damage, save_failed_action });
            var negative_action = help.CreateConditional(Helper.CreateContextConditionCasterHasFact(bloodrain_buff.ToAny()), new GameAction[] { negative_damage, save_failed_action });

            ChannelEnergyEngine.addBloodfireAndBloodrainActions(positive_action, negative_action, smite_positive_action, smite_negative_action);

            return (bloodfire, bloodrain);
        }

        // Channel Smite New feature (Needs engine)
        private static BlueprintFeature CreateHVChannelSmite()
        {
            var bp = Helper.CreateBlueprintFeature("HolyVindicatorChannelSmite",
                displayName: LocalizationTool.GetString("HV.Smite.Name"),
                description: LocalizationTool.GetString("HV.Smite.Description"), icon: ChannelEnergyEngine.channel_smite.Icon, group: FeatureGroup.None);
            bp.SetComponents(Helper.CreateAddFeatureIfHasFact(ChannelEnergyEngine.channel_smite));
            return bp;
        }

        // Versatile Channel ca2772187baf11544a76fe3210926f2d f5fc9a1a2a3c1a946a31b320d1dd31b2 (Needs engine)
        private static BlueprintFeature CreateHVVersatileChannel()
        {
            var versatile_channel = Helper.CreateBlueprintFeature("HolyVindicatorVersatileChannel",
                displayName: LocalizationTool.GetString("HV.Versatile.Name"),
                description: LocalizationTool.GetString("HV.Versatile.Description"), icon: null, group: FeatureGroup.None);
            ChannelEnergyEngine.addVersatileChannel(versatile_channel);
            return versatile_channel;
        }
        // Divine Judgement death knell 9c732cd0c4334d8b9baccf53d71101ba (Hard/impossible): Need help with spontaneous conversion


    }
}
