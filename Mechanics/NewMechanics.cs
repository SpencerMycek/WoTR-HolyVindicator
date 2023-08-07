using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Class.Kineticist;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Properties;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic;
using Kingmaker.Utility;
using Owlcat.QA.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.Blueprints;
using UnityEngine.Serialization;
using UnityEngine;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.Items.Slots;
using Kingmaker.Blueprints.Root;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.Blueprints.Classes.Selection;
using Newtonsoft.Json;
using static Kingmaker.Blueprints.Area.FactHolder;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Controllers.Units;
using Kingmaker.RuleSystem;
using Kingmaker;

namespace HolyVindicator
{
    public class Mechanics
    {
        public class ContextCalculateAbilityParamsBasedOnClasses : ContextAbilityParamsCalculator
        {
            public StatType StatType = StatType.Charisma;
            public BlueprintCharacterClass[] CharacterClasses = new BlueprintCharacterClass[0];
            public BlueprintArchetype[] archetypes = new BlueprintArchetype[0];
            public BlueprintUnitProperty property = null;

            public override AbilityParams Calculate(MechanicsContext context)
            {
                UnitEntityData maybeCaster = context?.MaybeCaster;
                if (maybeCaster == null)
                {
                    return context?.Params;
                }

                StatType statType = this.StatType;

                AbilityData ability = context.SourceAbilityContext?.Ability;
                RuleCalculateAbilityParams rule = !(ability != (AbilityData)null) ? new RuleCalculateAbilityParams(maybeCaster, context.AssociatedBlueprint, (Spellbook)null) : new RuleCalculateAbilityParams(maybeCaster, ability);
                rule.ReplaceStat = new StatType?(statType);

                int class_level = 0;
                foreach (var c in this.CharacterClasses)
                {
                    var class_archetypes = archetypes.Where(a => a.GetParentClass() == c);

                    if (class_archetypes.Empty() || class_archetypes.Any(a => maybeCaster.Descriptor.Progression.IsArchetype(a)))
                    {
                        class_level += maybeCaster.Descriptor.Progression.GetClassLevel(c);
                    }

                }
                rule.ReplaceCasterLevel = new int?(class_level);
                rule.ReplaceSpellLevel = new int?(class_level / 2);
                return context.TriggerRule<RuleCalculateAbilityParams>(rule).Result;
            }
        }

        public class ContextConditionHasNegativeEnergyAffinity : ContextCondition
        {
            public override string GetConditionCaption()
            {
                return string.Empty;
            }

            public override bool CheckCondition()
            {
                return this.Target.Unit.Descriptor.IsUndead;
            }
        }

        [ComponentName("Buff remove on save")]
        [AllowedOn(typeof(BlueprintUnitFact))]
        [AllowMultipleComponents]
        public class BuffRemoveOnSave : UnitBuffComponentDelegate, ITickEachRound
        {
            public SavingThrowType SaveType;
            public void OnNewRound()
            {

                Rulebook rulebook = Game.Instance.Rulebook;
                RuleSavingThrow ruleSavingThrow = new RuleSavingThrow(this.Owner.Descriptor.Unit, this.SaveType, this.Buff.Context.Params.DC);
                ruleSavingThrow.Reason = (RuleReason)this.Fact;
                RuleSavingThrow evt = ruleSavingThrow;
                if (!rulebook.TriggerEvent<RuleSavingThrow>(evt).IsPassed)
                    return;
                this.Buff.Remove();
            }

            public override void OnTurnOn()
            {
            }

            public override void OnTurnOff()
            {
            }
        }

        public class AbilityShowIfCasterHasFacts : BlueprintComponent, IAbilityVisibilityProvider
        {
            [ValidateNotNull]
            [SerializeField]
            public BlueprintUnitFactReference[] m_UnitFacts;

            public BlueprintUnitFactReference UnitFact => m_UnitFacts[0];

            public bool Any;

            public bool IsAbilityVisible(AbilityData ability)
            {
                foreach(BlueprintUnitFactReference fact in m_UnitFacts)
                {
                    if (!ability.Caster.Progression.Features.HasFact(fact) && !Any)
                    {
                        return false;
                    }
                    if (Any && ability.Caster.Progression.Features.HasFact(fact))
                    {
                        return true;
                    }
                }
                return true;
            }
        }

        [ComponentName("Add stat bonus if owner has shield")]
        [AllowedOn(typeof(BlueprintUnitFact))]
        [AllowMultipleComponents]
        public class AddStatBonusIfHasShield : UnitFactComponentDelegate, IUnitActiveEquipmentSetHandler, IUnitEquipmentHandler, IGlobalSubscriber
        {
            private ModifiableValue.Modifier m_Modifier;
            public ContextValue value;
            public ModifierDescriptor descriptor;
            public StatType stat;

            public override void OnTurnOn()
            {
                this.CheckShield();
            }

            public override void OnTurnOff()
            {
                this.DeactivateModifier();
            }

            public void HandleUnitChangeActiveEquipmentSet(UnitDescriptor unit)
            {
                this.CheckShield();
            }

            public void CheckShield()
            {
                if (this.Owner.Body.SecondaryHand.HasShield)
                    this.ActivateModifier();
                else
                    this.DeactivateModifier();
            }

            public void ActivateModifier()
            {
                if (this.m_Modifier != null)
                    return;
                this.m_Modifier = this.Owner.Stats.GetStat(stat).AddModifier(value.Calculate(this.Fact.MaybeContext), this.Fact, descriptor);
            }

            public void DeactivateModifier()
            {
                if (this.m_Modifier != null)
                    this.m_Modifier.Remove();
                this.m_Modifier = (ModifiableValue.Modifier)null;
            }

            public void HandleEquipmentSlotUpdated(ItemSlot slot, ItemEntity previousItem)
            {
                if (slot.Owner != this.Owner)
                    return;
                this.CheckShield();
            }
        }

        [AllowedOn(typeof(BlueprintAbility))]
        [AllowMultipleComponents]
        public class AbilityCasterHasShield : BlueprintComponent, IAbilityCasterRestriction
        {
            public string GetAbilityCasterRestrictionUIText()
            {
                return (string)LocalizedTexts.Instance.Reasons.SpecificWeaponRequired;
            }

            public bool IsCasterRestrictionPassed(UnitEntityData caster)
            {
                return caster.Body.SecondaryHand.HasShield;
            }
        }

        [ComponentName("Increase specific spells CL")]
        [AllowedOn(typeof(BlueprintUnitFact))]
        /*RuleInitiatorLogicComponent<RuleCalculateAbilityParams>,*/
        public class ContextIncreaseCasterLevelForSelectedSpells : UnitFactComponentDelegate, 
            IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IRulebookHandler<RuleCalculateAbilityParams>, ISubscriber, IInitiatorRulebookSubscriber
        {
            public ContextValue value;
            public BlueprintAbility[] spells;
            public bool correct_dc = true;
            public int multiplier = 1;

            private new MechanicsContext Context
            {
                get
                {
                    MechanicsContext context = (this.Fact as Buff)?.Context;
                    if (context != null)
                        return context;
                    return (this.Fact as Feature)?.Context;
                }
            }

            public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
            {
                if (!spells.Contains(evt.Spell))
                {
                    return;
                }
                int bonus = this.value.Calculate(this.Context);
                evt.AddBonusCasterLevel(multiplier * bonus);

                if (!correct_dc && !evt.Spell.IsSpell)
                {
                    evt.AddBonusDC(-(multiplier * bonus / 2));
                }
            }

            public void OnEventDidTrigger(RuleCalculateAbilityParams evt)
            {
            }
        }

        [AllowedOn(typeof(BlueprintUnitFact))]
        /*RuleInitiatorLogicComponent<RuleSavingThrow>*/
        public class ContextSavingThrowBonusAgainstSpecificSpells : UnitFactComponentDelegate,
            IInitiatorRulebookHandler<RuleSavingThrow>, IRulebookHandler<RuleSavingThrow>, ISubscriber, IInitiatorRulebookSubscriber
        {
            public BlueprintAbility[] Spells;
            public ModifierDescriptor ModifierDescriptor;
            public ContextValue Value;
            public BlueprintUnitFact[] BypassFeatures;

            public void OnEventAboutToTrigger(RuleSavingThrow evt)
            {
                BlueprintAbility sourceAbility = evt.Reason.Context?.SourceAbility;
                UnitEntityData maybeCaster = evt.Reason.Context?.MaybeCaster;
                bool flag = maybeCaster != null;
                if (flag)
                {
                    flag = false;
                    foreach (BlueprintUnitFact bypassFeature in this.BypassFeatures)
                        flag = maybeCaster.Descriptor.HasFact(bypassFeature);
                }
                if (!(sourceAbility != null) || !((IEnumerable<BlueprintAbility>)this.Spells).Contains<BlueprintAbility>(sourceAbility) || flag)
                    return;

                int val = this.Value.Calculate(this.Fact.MaybeContext);
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveWill.AddModifier(val, this.Fact, this.ModifierDescriptor));
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveReflex.AddModifier(val, this.Fact, this.ModifierDescriptor));
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveFortitude.AddModifier(val, this.Fact, this.ModifierDescriptor));
            }

            public void OnEventDidTrigger(RuleSavingThrow evt)
            {
            }
        }

        [AllowedOn(typeof(BlueprintUnitFact))]
        /*RuleInitiatorLogicComponent<RuleSpellResistanceCheck>*/
        public class CasterLevelCheckBonus : UnitFactComponentDelegate,
            IInitiatorRulebookHandler<RuleSpellResistanceCheck>, IRulebookHandler<RuleSpellResistanceCheck>, ISubscriber, IInitiatorRulebookSubscriber
        {
            public ContextValue Value;
            public ModifierDescriptor ModifierDescriptor = ModifierDescriptor.UntypedStackable;

            private new MechanicsContext Context
            {
                get
                {
                    MechanicsContext context = (this.Fact as Buff)?.Context;
                    if (context != null)
                        return context;
                    return (this.Fact as Feature)?.Context;
                }
            }

            public void OnEventAboutToTrigger(RuleDispelMagic evt)
            {
                int num = this.Value.Calculate(this.Context);
                evt.Bonus += num;
            }

            public void OnEventAboutToTrigger(RuleSpellResistanceCheck evt)
            {
                int num = this.Value.Calculate(this.Context);
                evt.AddSpellPenetration(num, ModifierDescriptor);
            }

            public void OnEventDidTrigger(RuleDispelMagic evt)
            {
            }

            public  void OnEventDidTrigger(RuleSpellResistanceCheck evt)
            {

            }
        }

        [ComponentName("Increase specified spells  DC")]
        [AllowedOn(typeof(BlueprintBuff))]
        public class IncreaseSpecifiedSpellsDC : UnitBuffComponentDelegate, IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IRulebookHandler<RuleCalculateAbilityParams>, IInitiatorRulebookSubscriber
        {
            public BlueprintAbility[] spells;
            public ContextValue BonusDC;

            public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
            {
                if (spells.Contains(evt.Spell) || (evt.Spell.Parent != null && spells.Contains(evt.Spell.Parent)))
                {
                    evt.AddBonusDC(this.BonusDC.Calculate(this.Fact.MaybeContext));
                }
            }

            public void OnEventDidTrigger(RuleCalculateAbilityParams evt)
            {

            }
        }

        [AllowMultipleComponents]
        [AllowedOn(typeof(BlueprintUnitFact))]
        public class AddFeatureIfHasFactAndNotHasFact : UnitFactComponentDelegate, IOwnerGainLevelHandler, IGlobalSubscriber
        {
            public BlueprintUnitFact HasFact;
            public BlueprintUnitFact NotHasFact;
            public BlueprintUnitFact Feature;

            [JsonProperty]
            private Fact m_AppliedFact;

            public void OnFactActivate()
            {
                this.Apply();
            }

            public void OnFactDeactivate()
            {
                this.Owner.RemoveFact(this.m_AppliedFact);
                this.m_AppliedFact = (Fact)null;
            }

            public void HandleUnitGainLevel()
            {
                this.Apply();
            }

            private void Apply()
            {
                if (this.m_AppliedFact != null || !this.Owner.HasFact(this.HasFact) || this.Owner.HasFact(this.NotHasFact))
                    return;
                this.m_AppliedFact = (Fact)this.Owner.AddFact(this.Feature, (MechanicsContext)null, (FeatureParam)null);
            }
        }

        [AllowMultipleComponents]
        [AllowedOn(typeof(BlueprintUnitFact))]
        public class AddFeatureIfHasFactsFromList : UnitFactComponentDelegate<AddFeatureIfHasFactData>, IUnitSubscriber, ISubscriber, IOwnerGainLevelHandler
        {
            public bool not = false;
            public int amount = 1;

            [FormerlySerializedAs("CheckedFact")]
            [SerializeField]
            public BlueprintUnitFact[] CheckedFacts;

            [FormerlySerializedAs("Feature")]
            [SerializeField]
            public BlueprintUnitFactReference m_Feature;


            public BlueprintUnitFact Feature => m_Feature?.Get();

            public override void OnActivate()
            {
                this.Apply();
            }

            public override void OnDeactivate()
            {
                this.Owner.RemoveFact(base.Data.AppliedFact);
                base.Data.AppliedFact = null;
            }

            public void HandleUnitGainLevel()
            {
                this.Apply();
            }

            private void Apply()
            {
                if (Owner.HasFact(Feature))
                {
                    return;
                }
                if (base.Data.AppliedFact != null)
                    return;

                int facts_found = 0;

                foreach (var f in CheckedFacts)
                {
                    if (facts_found == amount)
                    {
                        break;
                    }
                    if (this.Owner.HasFact(f))
                    {
                        facts_found++;
                    }
                }
                if ((facts_found == amount) != not)
                {
                    base.Data.AppliedFact = base.Owner.AddFact(Feature);
                }
            }
        }
    }
}
