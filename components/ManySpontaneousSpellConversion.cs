using JetBrains.Annotations;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using System.Linq;
using UnityEngine;

namespace HolyVindicator.components
{
    internal class ManySpontaneousSpellConversion : SpontaneousSpellConversion
    {
        [NotNull]
        [SerializeField]
        public new BlueprintCharacterClassReference m_CharacterClass => m_Classes[0];

        [NotNull]
        [SerializeField]
        public BlueprintCharacterClassReference[] m_Classes => new BlueprintCharacterClassReference[0];

        public override void OnTurnOff()
        {
            //base.OnTurnOff();
            //base.Owner.DemandSpellbook(CharacterClass).RemoveSpellConversionList(Id);
            foreach(BlueprintCharacterClassReference classRef in m_Classes) 
            {
                base.Owner.DemandSpellbook(classRef.Get()).RemoveSpellConversionList(Id);
            }
        }

        public override void OnTurnOn()
        {
            //base.OnTurnOn();
            //base.Owner.DemandSpellbook(CharacterClass).AddSpellConversionList(Id, SpellsByLevel.ToArray());
            foreach (BlueprintCharacterClassReference classRef in m_Classes)
            {
                base.Owner.DemandSpellbook(classRef.Get()).AddSpellConversionList(Id, SpellsByLevel.ToArray());
            }
        }
    }
}
