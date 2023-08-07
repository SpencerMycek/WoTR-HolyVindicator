using CodexLib;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using static HolyVindicator.Mechanics;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics.Components;
using Helper = CodexLib.Helper;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Classes.Prerequisites;

namespace HolyVindicator
{
    internal static class help
    {
        public static BlueprintAbility createVariantWrapper(string name, params BlueprintAbilityReference[] variants)
        {
            var wrapper = Clone<BlueprintAbility>(variants[0].Get(), name);
            //var guid = Helper.GetGuid(name);
            //wrapper.AddAsset(guid);
            List<BlueprintComponent> components = new List<BlueprintComponent>();
            components.Add(CreateAbilityVariants(wrapper, variants));
            wrapper.ComponentsArray = components.ToArray();

            return wrapper;
        }

        public static T Clone<T>(T original, String newName) where T : BlueprintScriptableObject
        {
            var clone = Helper.Clone<T>(original);
            var newGuid = Helper.GetGuid(newName);
            clone.AddAsset(newGuid);
            clone.name = newName;
            return clone;
        }


        //public static AbilityVariants CreateAbilityVariants(this BlueprintAbility parent, IEnumerable<BlueprintAbilityReference> variants) => CreateAbilityVariants(parent, variants.ToArray());

        public static AbilityVariants CreateAbilityVariants(this BlueprintAbility parent, params BlueprintAbilityReference[] variants)
        {
            var a = new AbilityVariants();
            a.m_Variants = variants;
            foreach (var v in variants)
            {
                v.Get().m_Parent = parent.ToRef();
            }
            return a;
        }

        public static GameAction[] changeAction<T>(GameAction[] action_list, Action<T> change) where T : GameAction
        {
            //we assume that only possible actions are actual actions, conditionals, ContextActionSavingThrow or ContextActionConditionalSaved
            var actions = action_list.ToList();
            int num_actions = actions.Count();
            for (int i = 0; i < num_actions; i++)
            {
                if (actions[i] == null)
                {
                    continue;
                }
                else if (actions[i] is T)
                {
                    actions[i] = actions[i].Clone();
                    change(actions[i] as T);
                    //continue;
                }

                if (actions[i] is Conditional)
                {
                    actions[i] = actions[i].Clone();
                    (actions[i] as Conditional).IfTrue = CreateActionList(changeAction<T>((actions[i] as Conditional).IfTrue.Actions, change));
                    (actions[i] as Conditional).IfFalse = CreateActionList(changeAction<T>((actions[i] as Conditional).IfFalse.Actions, change));
                }
                else if (actions[i] is ContextActionConditionalSaved)
                {
                    actions[i] = actions[i].Clone();
                    (actions[i] as ContextActionConditionalSaved).Succeed = CreateActionList(changeAction<T>((actions[i] as ContextActionConditionalSaved).Succeed.Actions, change));
                    (actions[i] as ContextActionConditionalSaved).Failed = CreateActionList(changeAction<T>((actions[i] as ContextActionConditionalSaved).Failed.Actions, change));
                }
                else if (actions[i] is ContextActionSavingThrow)
                {
                    actions[i] = actions[i].Clone();
                    (actions[i] as ContextActionSavingThrow).Actions = CreateActionList(changeAction<T>((actions[i] as ContextActionSavingThrow).Actions.Actions, change));
                }
                else if (actions[i] is ContextActionOnContextCaster)
                {
                    actions[i] = actions[i].Clone();
                    (actions[i] as ContextActionOnContextCaster).Actions = CreateActionList(changeAction<T>((actions[i] as ContextActionOnContextCaster).Actions.Actions, change));
                }
            }

            return actions.ToArray();
        }

        public static GameAction[] addMatchingAction<T>(GameAction[] action_list, params GameAction[] actions_to_add) where T : GameAction
        {
            //we assume that only possible actions are actual actions, conditionals, ContextActionSavingThrow or ContextActionConditionalSaved
            var actions = action_list.ToList();
            int num_actions = actions.Count();
            for (int i = 0; i < num_actions; i++)
            {
                if (actions[i] == null)
                {
                    continue;
                }
                else if (actions[i] is T)
                {
                    actions.AddRange(actions_to_add);
                }

                if (actions[i] is Conditional)
                {
                    actions[i] = actions[i].Clone();
                    (actions[i] as Conditional).IfTrue = CreateActionList(addMatchingAction<T>((actions[i] as Conditional).IfTrue.Actions, actions_to_add));
                    (actions[i] as Conditional).IfFalse = CreateActionList(addMatchingAction<T>((actions[i] as Conditional).IfFalse.Actions, actions_to_add));
                }
                else if (actions[i] is ContextActionConditionalSaved)
                {
                    actions[i] = actions[i].Clone();
                    (actions[i] as ContextActionConditionalSaved).Succeed = CreateActionList(addMatchingAction<T>((actions[i] as ContextActionConditionalSaved).Succeed.Actions, actions_to_add));
                    (actions[i] as ContextActionConditionalSaved).Failed = CreateActionList(addMatchingAction<T>((actions[i] as ContextActionConditionalSaved).Failed.Actions, actions_to_add));
                }
                else if (actions[i] is ContextActionSavingThrow)
                {
                    actions[i] = actions[i].Clone();
                    (actions[i] as ContextActionSavingThrow).Actions = CreateActionList(addMatchingAction<T>((actions[i] as ContextActionSavingThrow).Actions.Actions, actions_to_add));
                }
                else if (actions[i] is ContextActionOnContextCaster)
                {
                    actions[i] = actions[i].Clone();
                    (actions[i] as ContextActionOnContextCaster).Actions = CreateActionList(addMatchingAction<T>((actions[i] as ContextActionOnContextCaster).Actions.Actions, actions_to_add));
                }
            }

            return actions.ToArray();
        }

        public static ActionList CreateActionList(params GameAction[] actions)
        {
            if (actions == null || actions.Length == 1 && actions[0] == null) actions = Array.Empty<GameAction>();
            return new ActionList() { Actions = actions };
        }

        public static AddTargetAttackWithWeaponTrigger CreateAddTargetAttackWithWeaponTrigger(ActionList action_self = null, ActionList action_attacker = null, WeaponCategory[] categories = null,
                                                                                     bool only_hit = true, bool not_reach = true, bool only_melee = true, bool not = false,
                                                                                     bool wait_for_attack_to_resolve = false, bool only_critical_hit = false)
        {
            var a = new AddTargetAttackWithWeaponTrigger();
            a.ActionOnSelf = action_self != null ? action_self : Helper.CreateActionList();
            a.ActionsOnAttacker = action_attacker != null ? action_attacker : Helper.CreateActionList();
            a.OnlyHit = only_hit;
            a.NotReach = not_reach;
            a.OnlyMelee = only_melee;
            a.CheckCategory = categories != null;
            a.Categories = categories;
            a.Not = not;
            a.WaitForAttackResolve = wait_for_attack_to_resolve;
            a.CriticalHit = only_critical_hit;
            return a;
        }

        static public AddInitiatorAttackWithWeaponTrigger CreateAddInitiatorAttackWithWeaponTrigger(Kingmaker.ElementsSystem.ActionList action, bool only_hit = true, bool critical_hit = false,
                                                                                                      bool check_weapon_range_type = false, bool reduce_hp_to_zero = false,
                                                                                                      bool on_initiator = false,
                                                                                                      WeaponRangeType range_type = WeaponRangeType.Melee,
                                                                                                      bool only_first_hit = false)
        {
            var t = new AddInitiatorAttackWithWeaponTrigger();
            t.Action = action;
            t.OnlyHit = only_hit;
            t.CriticalHit = critical_hit;
            t.CheckWeaponRangeType = check_weapon_range_type;
            t.RangeType = range_type;
            t.ReduceHPToZero = reduce_hp_to_zero;
            t.ActionsOnInitiator = on_initiator;
            t.OnlyOnFirstAttack = only_first_hit;

            return t;
        }

        static public ContextActionRemoveBuffsByDescriptor CreateContextActionRemoveBuffsByDescriptor(SpellDescriptor descriptor, bool not_self = true)
        {
            var r = new ContextActionRemoveBuffsByDescriptor();
            r.SpellDescriptor = descriptor;
            r.NotSelf = not_self;
            return r;
        }

        static public void addContextActionApplyBuffOnFactsToActivatedAbilityBuffNoRemove(BlueprintBuff target_buff, BlueprintBuff buff_to_add, params BlueprintUnitFact[] facts)
        {
            addContextActionApplyBuffOnFactsToActivatedAbilityBuffNoRemove(target_buff, buff_to_add, new GameAction[0], facts);
        }

        static public void addContextActionApplyBuffOnFactsToActivatedAbilityBuffNoRemove(BlueprintBuff target_buff, BlueprintBuff buff_to_add, Kingmaker.ElementsSystem.GameAction[] pre_actions,
                                                                              params BlueprintUnitFact[] facts)
        {
            Main.Print($"Target: {target_buff.name}, Add: {buff_to_add.name}");
            var condition = new ContextConditionHasFact[facts.Length];
            for (int i = 0; i < facts.Length; i++)
            {
                condition[i] = Helper.CreateContextConditionHasFact(facts[i]);
            }
            var action = Helper.CreateConditional(condition, pre_actions.AddToArray(Helper.CreateContextActionApplyBuff(buff_to_add, 6,
                                                                                     dispellable: false, asChild: true, permanent: true)));
            addContextActionApplyBuffOnConditionToActivatedAbilityBuff(target_buff, action);
        }

        static public void addContextActionApplyBuffOnConditionToActivatedAbilityBuff(BlueprintBuff target_buff, Conditional conditional_action)
        {
            if (target_buff.GetComponent<AddFactContextActions>() == null)
            {
                var context_actions = new BlueprintComponent[] { new AddFactContextActions() { Activated = Helper.CreateActionList(), Deactivated = Helper.CreateActionList(), NewRound = Helper.CreateActionList() } };
                target_buff.ComponentsArray = context_actions.AddRangeToArray(target_buff.ComponentsArray);
            }

            var activated = target_buff.GetComponent<AddFactContextActions>().Activated;
            activated.Actions = activated.Actions.AddToArray(conditional_action);
        }

        static public AddFeatureIfHasFactAndNotHasFact CreateAddFeatureIfHasFactAndNotHasFact(BlueprintUnitFact has_fact, BlueprintUnitFact not_has_fact, BlueprintUnitFact feature)
        {
            var a = new AddFeatureIfHasFactAndNotHasFact();
            a.HasFact = has_fact;
            a.NotHasFact = not_has_fact;
            a.Feature = feature;
            return a;
        }

        static public PrerequisiteAlignment CreatePrerequisiteAlignment(Kingmaker.UnitLogic.Alignments.AlignmentMaskType alignment)
        {
            var p = new Kingmaker.Blueprints.Classes.Prerequisites.PrerequisiteAlignment();
            p.Alignment = alignment;
            return p;
        }

        public static void AddFeaturePrerequisiteAny(BlueprintFeature feature, BlueprintFeature prerequisite)
        {
            var features_prereq = feature.GetComponents<PrerequisiteFeature>().Where(f => f.Group == Prerequisite.GroupType.Any);
            foreach (var fp in features_prereq)
            {
                if (fp.Feature == prerequisite)
                {
                    return;
                }
            }

            feature.AddComponent(Helper.CreatePrerequisiteFeature(prerequisite, any: true));
        }

        public static void AddFeaturePrerequisiteOr(BlueprintFeature feature, BlueprintFeature prerequisite)
        {
            var features_from_list = feature.GetComponent<PrerequisiteFeaturesFromList>();
            if (features_from_list == null)
            {
                features_from_list = Helper.CreatePrerequisiteFeaturesFromList(prerequisite);
                feature.AddComponent(features_from_list);
            }

            if (!features_from_list.Features.Contains(prerequisite))
            {
                features_from_list.m_Features = features_from_list.m_Features.AddToArray(prerequisite.ToRef());
            }
        }

        public static void AddComponent(this BlueprintScriptableObject obj, BlueprintComponent component)
        {
            obj.SetComponents(obj.ComponentsArray.AddToArray(component));
        }

        public static void ReplaceComponent<T>(this BlueprintScriptableObject obj, BlueprintComponent replacement) where T : BlueprintComponent
        {
            ReplaceComponent(obj, obj.GetComponent<T>(), replacement);
        }


        public static void ReplaceComponent<T>(this BlueprintScriptableObject obj, Action<T> action) where T : BlueprintComponent
        {
            var replacement = obj.GetComponent<T>().Clone();
            action(replacement);
            ReplaceComponent(obj, obj.GetComponent<T>(), replacement);
        }

        public static void ReplaceComponent(this BlueprintScriptableObject obj, BlueprintComponent original, BlueprintComponent replacement)
        {
            // Note: make a copy so we don't mutate the original component
            // (in case it's a clone of a game one).
            var components = obj.ComponentsArray;
            var newComponents = new BlueprintComponent[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                newComponents[i] = c == original ? replacement : c;
            }
            obj.SetComponents(newComponents); // fix up names if needed
        }

        public static ContextConditionHasNegativeEnergyAffinity CreateHNEA(Action<ContextConditionHasNegativeEnergyAffinity> init = null)
        {
            var result = new ContextConditionHasNegativeEnergyAffinity();
            if (init != null) init(result);
            return result;
        }

        public static void setMiscAbilityParametersRangedDirectional(this BlueprintAbility ability, bool works_on_units = true,
                                                                     AbilityEffectOnUnit effect_on_ally = AbilityEffectOnUnit.Harmful,
                                                                     AbilityEffectOnUnit effect_on_enemy = AbilityEffectOnUnit.Harmful,
                                                                     Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionCastSpell.CastAnimationStyle animation = Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionCastSpell.CastAnimationStyle.Directional,
                                                                     Kingmaker.View.Animation.CastAnimationStyle animation_style = Kingmaker.View.Animation.CastAnimationStyle.CastActionDirectional)
        {
            ability.CanTargetFriends = works_on_units;
            ability.CanTargetEnemies = works_on_units;
            ability.CanTargetSelf = works_on_units;
            ability.CanTargetPoint = true;
            ability.EffectOnEnemy = effect_on_enemy;
            ability.EffectOnAlly = effect_on_ally;
            ability.Animation = animation;
        }

        public static AbilityShowIfCasterHasFacts CreateAbilityShowIfCasterHasFacts(params BlueprintUnitFactReference[] UnitFacts)
        {
            AbilityShowIfCasterHasFacts abilityShowIfCasterHasFacts = new AbilityShowIfCasterHasFacts();
            abilityShowIfCasterHasFacts.m_UnitFacts = UnitFacts;
            return abilityShowIfCasterHasFacts;
        }

        public static bool addToAbilityVariants(this BlueprintAbility parent, params BlueprintAbilityReference[] variants)
        {
            var comp = parent.GetComponent<AbilityVariants>();
            foreach ( BlueprintAbilityReference ability in variants )
            {
                comp.m_Variants = comp.m_Variants.AddToArray(ability);
            }

            foreach (var v in variants)
            {
                v.Get().m_Parent = parent.ToRef();
            }
            return true;
        }

        public static ContextCalculateAbilityParamsBasedOnClasses CreateContextCalculateAbilityParamsBasedOnClasses(BlueprintCharacterClass[] character_classes,
                                                                                                                                            StatType stat)
        {
            var c = new ContextCalculateAbilityParamsBasedOnClasses();
            c.CharacterClasses = character_classes;
            c.StatType = stat;
            return c;
        }

        public static ContextCalculateAbilityParamsBasedOnClasses createContextCalculateAbilityParamsBasedOnClassesWithArchetypes(BlueprintCharacterClass[] character_classes,BlueprintArchetype[] archetypes, StatType stat)
        {
            var c = new ContextCalculateAbilityParamsBasedOnClasses();
            c.CharacterClasses = character_classes;
            c.StatType = stat;
            c.archetypes = archetypes == null ? new BlueprintArchetype[0] : archetypes;
            return c;
        }

        public static void SetIncreasedByStat(this BlueprintAbilityResource resource, int baseValue, StatType stat)
        {
            var amount = resource.m_MaxAmount;
            amount.BaseValue = baseValue;
            amount.IncreasedByStat = true;
            amount.ResourceBonusStat = stat;

            // Enusre arrays are at least initialized to empty.
            var emptyClasses = Array.Empty<BlueprintCharacterClassReference>();
            var emptyArchetypes = Array.Empty<BlueprintArchetypeReference>();
            if (amount.m_Class == null) amount.m_Class = emptyClasses;
            if (amount.m_ClassDiv == null) amount.m_ClassDiv = emptyClasses;
            if (amount.m_Archetypes == null) amount.m_Archetypes = emptyArchetypes;
            if (amount.m_ArchetypesDiv == null) amount.m_ArchetypesDiv = emptyArchetypes;

            resource.m_MaxAmount = amount;
        }

        public static UIGroup CreateUIGroup(params BlueprintFeatureBaseReference[] features) => CreateUIGroup((IEnumerable<BlueprintFeatureBaseReference>)features);

        public static UIGroup CreateUIGroup(IEnumerable<BlueprintFeatureBaseReference> features)
        {
            var result = new UIGroup();
            result.m_Features.AddRange(features);
            return result;
        }

        public static Conditional CreateConditional(Condition condition, GameAction[] ifTrue = null, GameAction[] ifFalse = null, bool OperationAnd = true)
        {
            Conditional conditional = new Conditional();
            conditional.ConditionsChecker = new ConditionsChecker
            {
                Conditions = condition.ObjToArray(),
                Operation = ((!OperationAnd) ? Operation.Or : Operation.And)
            };
            conditional.IfTrue = Helper.CreateActionList(ifTrue);
            conditional.IfFalse = Helper.CreateActionList(ifFalse);
            return conditional;
        }
    }
}
