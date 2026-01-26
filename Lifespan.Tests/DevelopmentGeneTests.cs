using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using UnityEngine;
using Moq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Lifespan.Tests
{
    [TestFixture]
    public class DevelopmentGeneTests
    {
        private Mock<IPluginContext> _mockCtx;
        private Mock<IModLogger> _mockLog;
        private LifespanConfig _config;
        private DevelopmentGeneManager _manager;

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);
            
            _config = new LifespanConfig();
            _manager = new DevelopmentGeneManager(_mockCtx.Object, _config, null);
        }

        private void SetTraumaValue(FamilyMember member, float value)
        {
            if (member.stats == null) member.stats = (BehaviourStats)FormatterServices.GetUninitializedObject(typeof(BehaviourStats));
            if (member.stats.trauma == null) member.stats.trauma = (TraumaStat)FormatterServices.GetUninitializedObject(typeof(TraumaStat));
            
            // Use reflection to set private m_value in TraumaStat
            FieldInfo field = typeof(TraumaStat).GetField("m_value", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(member.stats.trauma, value);
            
            // Also set m_maxValue to ensure NormalizedValue works
            FieldInfo maxField = typeof(TraumaStat).GetField("m_maxValue", BindingFlags.NonPublic | BindingFlags.Instance);
            maxField.SetValue(member.stats.trauma, 100f);
        }

        [Test]
        public void StatGainChance_WithMaxStress_ReducesByMaxInfluence()
        {
            // Arrange
            // Bypass Unity constructor
            var member = (FamilyMember)FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            
            // Initialize stats
            SetTraumaValue(member, 100f); // 100% trauma
            
            var gene = new DevelopmentGene();
            int ageYears = 20;
            int yearsToMilestone = 10;
            int remaining = 5;
            int weeksAged = 1;

            _config.baseStatGainChance = 10.0f; // Was 0.1f, now 10%
            _config.maxStressInfluence = 0.3f;
            _manager.RefreshSettings(_config);

            // Act
            float chance = _manager.CalculateStatGainChance(member, gene, LifeStage.PostAdult, ageYears, yearsToMilestone, remaining, weeksAged);

            // Assert
            Assert.AreEqual(0.07f, chance, 0.001f);
        }

        [Test]
        public void CatchUpBonus_IncreasesNearMilestone()
        {
            // Arrange
            var member = (FamilyMember)FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            SetTraumaValue(member, 50f); // 50% trauma -> 0 stress modifier
            
            var gene = new DevelopmentGene();
            int ageYears = 58; 
            int yearsToMilestone = 2; // Within CatchUpWindow (3)
            int remaining = 5;
            int weeksAged = 1;

            _config.baseStatGainChance = 10.0f; // Was 0.1f, now 10%
            _config.maxStressInfluence = 0.3f;
            _config.catchUpBonusPerPoint = 0.05f;
            _config.catchUpWindowYears = 3;
            _manager.RefreshSettings(_config);

            // Act
            float chance = _manager.CalculateStatGainChance(member, gene, LifeStage.PreElder, ageYears, yearsToMilestone, remaining, weeksAged);

            // Assert
            Assert.AreEqual(0.6f, chance, 0.001f);
        }
    }
}