using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Lifespan.Tests
{
    [TestFixture]
    public class GameContractTests
    {
        [Test]
        public void PatchTargets_HaveTheExpectedConcreteSignatures()
        {
            Method(typeof(GameTime), "Awake");
            Method(typeof(BaseCharacter), "GetWalkSpeed");
            Method(typeof(BaseCharacter), "SaveLoadCharacter", typeof(SaveData));
            Method(typeof(BaseCharacter), "OnTraitsChanged");
            Method(typeof(JobQueue), "AddJob", typeof(Job));
            Method(typeof(JobQueue), "SaveLoadJobQueue", typeof(SaveData), typeof(string));
            Method(typeof(ExpeditionMainPanelNew), "OnShow");
            Method(typeof(ExpeditionMainPanelNew), "Update");
            Method(typeof(InteractionManager), "Update_Standard");
            Method(typeof(BehaviourStats), "UpdateStats");
            Method(typeof(UI_CharacterTooltip), "UpdateValues");
            Method(typeof(UI_CharacterTooltip), "HideTooltip", typeof(bool));
            Method(typeof(FamilyManager), "CreateObituaryInfo", typeof(BaseCharacter));
            Method(typeof(FamilyManager), "AdoptNpc", typeof(NpcVisitor));
            Method(typeof(ObituaryInfo), "SetupObituary", typeof(FamilyManager.DeadCharacterInfo));
            Method(typeof(GameOverPanel), "OnShow");
            Method(typeof(PartyMapPanel), "OnShow");
            var initialize = Method(typeof(NpcVisitor), "Initialize", typeof(Vector3), typeof(Vector3),
                typeof(NpcVisitor.NpcType), typeof(NpcVisitor), typeof(int));
            Assert.AreEqual("npcType", initialize.GetParameters()[2].Name);
            Method(typeof(ExplorationParty), "RemoveDeadPartyMembers", typeof(bool));
            Method(typeof(JournalManager), "InsertJournalEntry", typeof(string), typeof(string), typeof(bool));
        }

        [Test]
        public void ReflectedState_HasTheExpectedTypes()
        {
            Field(typeof(BaseCharacter), "m_child", typeof(bool));
            Field(typeof(BaseCharacter), "m_characterMeshId", typeof(string));
            Field(typeof(BaseCharacter), "m_mesh", typeof(CharacterMesh));
            Field(typeof(BaseCharacter), "m_hairColor", typeof(Color));
            foreach (string name in new[] { "m_headTexture", "m_torsoTexture", "m_legTexture",
                "m_originalHeadTexture", "m_originalTorsoTexture", "m_originalLegTexture" })
                Field(typeof(BaseCharacter), name, typeof(string));
            Field(typeof(BaseStat), "cap", typeof(int));
            Field(typeof(UI_Character), "m_height", typeof(float));
            Field(typeof(UI_CharacterTooltip), "m_member", typeof(FamilyMember));
            Field(typeof(UI_CharacterTooltip), "m_name", typeof(UILabel));
            Field(typeof(BehaviourStats), "m_member", typeof(FamilyMember));
            Field(typeof(InteractionManager), "m_WorldCamera", typeof(Camera));
            Field(typeof(InteractionManager), "m_UICamera", typeof(Camera));
            Field(typeof(ExpeditionMainPanelNew), "m_eligiblePeople", typeof(System.Collections.Generic.List<FamilyMember>));
            Field(typeof(ExpeditionMainPanelNew), "m_currentPerson1Index", typeof(int));
            Field(typeof(ExpeditionMainPanelNew), "m_currentPerson2Index", typeof(int));
            Field(typeof(ExpeditionMainPanelNew), "m_isReadyToGo", typeof(bool));
            Field(typeof(ExpeditionMainPanelNew), "m_mapScreenLegend", typeof(LegendContainer));
            Field(typeof(JobQueue), "jobs", typeof(System.Collections.Generic.List<Job>));
            Field(typeof(Job_GoToLocation), "m_callbackAction", typeof(Job_GoToLocation.LocationReachedAction));
            Field(typeof(PartyMapPanel), "m_allParties", typeof(System.Collections.Generic.List<ExplorationParty>));
            Field(typeof(PartyMapPanel), "m_currentPartyIndex", typeof(int));
            Field(typeof(PartyMapPanel), "m_mapUI", typeof(UI_ExpeditionMap));
            Field(typeof(ObituaryInfo), "dayLabel", typeof(UILabel));
            Field(typeof(GameOverPanel), "daysLastedLabel", typeof(UILabel));
            Field(typeof(GameTime), "game_time", typeof(float));
        }

        private static MethodInfo Method(Type owner, string name, params Type[] arguments)
        {
            var method = owner.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, arguments, null);
            Assert.IsNotNull(method, owner.Name + "." + name);
            Assert.IsNotNull(method.GetMethodBody(), owner.Name + "." + name + " has a managed body");
            Assert.AreEqual(0, (int)(method.GetMethodImplementationFlags() & MethodImplAttributes.AggressiveInlining),
                owner.Name + "." + name + " has no forced inlining");
            return method;
        }

        private static void Field(Type owner, string name, Type expected)
        {
            var field = owner.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Assert.IsNotNull(field, owner.Name + "." + name);
            Assert.AreEqual(expected, field.FieldType, owner.Name + "." + name);
        }
    }
}
