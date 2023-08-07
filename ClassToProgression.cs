using System;
using System.Linq;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Enums.Damage;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Utility;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using CodexLib;
using HarmonyLib;
using static HolyVindicator.Mechanics;

namespace HolyVindicator
{
    public class ClassToProgression
    {

        public enum DomainSpellsType
        {
            NoSpells = 1,
            SpecialList = 2,
            NormalList = 3
        }

        // Add class to domains removed

        // Snipped Progression and Fact

        static public void addClassToAbility(BlueprintCharacterClass class_to_add, BlueprintArchetype[] archetypes_to_add, BlueprintAbility a, BlueprintCharacterClass class_to_check)
        {
            var components = a.ComponentsArray.ToArray();

            foreach (var c in components.ToArray())
            {
                if (c is AbilityVariants)
                {
                    foreach (var v in (c as AbilityVariants).Variants)
                    {
                        addClassToAbility(class_to_add, archetypes_to_add, v, class_to_check);
                    }
                }
                else if (c is ContextRankConfig)
                {
                    addClassToContextRankConfig(class_to_add, archetypes_to_add, c as ContextRankConfig, a.name, class_to_check);
                }
                else if (c is AbilityEffectRunAction)
                {
                    addClassToActionList(class_to_add, archetypes_to_add, (c as AbilityEffectRunAction).Actions, class_to_check);
                }
                else if (c is ContextCalculateAbilityParamsBasedOnClass)
                {
                    var c_typed = c as ContextCalculateAbilityParamsBasedOnClass;
                    if (c_typed.CharacterClass == class_to_check)
                    {
                        a.ReplaceComponent(c, help.createContextCalculateAbilityParamsBasedOnClassesWithArchetypes(new BlueprintCharacterClass[] { c_typed.CharacterClass, class_to_add }.Distinct().ToArray(), archetypes_to_add, c_typed.StatType));
                    }
                }
                else if (c is ContextCalculateAbilityParamsBasedOnClasses)
                {
                    var c_typed = c as ContextCalculateAbilityParamsBasedOnClasses;
                    if (c_typed.CharacterClasses.Contains(class_to_check))
                    {
                        a.ReplaceComponent(c,
                            help.createContextCalculateAbilityParamsBasedOnClassesWithArchetypes(
                                c_typed.CharacterClasses.AddToArray(class_to_add).Distinct().ToArray(),
                                c_typed.archetypes.AddRangeToArray(archetypes_to_add).Distinct().ToArray(),
                                c_typed.StatType));
                    }
                }
                else if (c is AbilityEffectStickyTouch)
                {
                    addClassToAbility(class_to_add, archetypes_to_add, (c as AbilityEffectStickyTouch).TouchDeliveryAbility, class_to_check);
                }
            }
        }

        static void addClassToActionList(BlueprintCharacterClass class_to_add, BlueprintArchetype[] archetypes_to_add, ActionList action_list, BlueprintCharacterClass class_to_check)
        {
            foreach (var a in action_list.Actions)
            {
                if (a == null)
                {
                    continue;
                }
                if (a is ContextActionApplyBuff)
                {
                    addClassToBuff(class_to_add, archetypes_to_add, (a as ContextActionApplyBuff).Buff, class_to_check);
                }
                //else if (a is ContextActionSpawnAreaEffect)
                //{
                //    addClassToAreaEffect(class_to_add, archetypes_to_add, (a as ContextActionSpawnAreaEffect).AreaEffect, class_to_check);
                //}
                else if (a is Conditional)
                {
                    var a_conditional = (a as Conditional);
                    addClassToActionList(class_to_add, archetypes_to_add, a_conditional.IfTrue, class_to_check);
                    addClassToActionList(class_to_add, archetypes_to_add, a_conditional.IfFalse, class_to_check);
                }
                else if (a is ContextActionConditionalSaved)
                {
                    var a_conditional = (a as ContextActionConditionalSaved);
                    addClassToActionList(class_to_add, archetypes_to_add, a_conditional.Failed, class_to_check);
                    addClassToActionList(class_to_add, archetypes_to_add, a_conditional.Succeed, class_to_check);
                }
            }
        }

        public static void addClassToContextRankConfig(BlueprintCharacterClass class_to_add, BlueprintArchetype[] archetypes_to_add, ContextRankConfig c, string archetypes_list_prefix, BlueprintCharacterClass class_to_check)
        {
            var classes = c.m_Class;

            if (classes == null || classes.Empty() || !classes.Contains(class_to_check))
            {
                return;
            }

            classes = classes.AddToArray(class_to_add.ToRef()).Distinct().ToArray();

            c.m_Class = classes;


            // Snipped check and logic for if there were any archetypes to add (We call once, there are no cases of this)
        }

        public static void addClassToBuff(BlueprintCharacterClass class_to_add, BlueprintArchetype[] archetypes_to_add, BlueprintBuff b, BlueprintCharacterClass class_to_check)
        {
            var components = b.ComponentsArray;
            // Snipped, the only case where we call addClassToBuff, the buff/ability does not contain any of the indicated types
        }

        // Snipped AreaEffect, Resource, and Feat

        // Snipped Prefix/Postfix for opposition schools, etc
    }
}