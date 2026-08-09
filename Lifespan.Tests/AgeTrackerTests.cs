using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using Moq;
using System.Collections.Generic;

namespace Lifespan.Tests
{
    [TestFixture]
    public class AgeTrackerTests
    {
        private Mock<IPluginContext> _mockCtx;
        private Mock<IModLogger> _mockLog;
        private LifespanConfig _config;
        private AgeTracker _tracker;

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);

            var mockSave = new Mock<ISaveSystem>();
            _mockCtx.Setup(c => c.SaveSystem).Returns(mockSave.Object);

            _config = new LifespanConfig();
            _config.enableChildDevelopment = true;
            _tracker = new AgeTracker(_mockCtx.Object, _config, new ModRandomStream(12345));
        }

        [Test]
        public void GenerateInitialAge_WithinBounds()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            bool isChild = true;
            _config.initialChildAgeYears = 10;
            
            int ageWeeks = _tracker.GenerateInitialAge(member, isChild);
            int ageYears = ageWeeks / 52;

            Assert.GreaterOrEqual(ageYears, 0);
            Assert.LessOrEqual(ageYears, 17);
        }

        [Test]
        public void IncrementAge_PersistsCorrectly()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            int memberId = 123;
            typeof(FamilyMember).GetField("familyId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, memberId);
            
            // Manually set an initial age in the tracker's internal data to avoid Gaussian side effects
            _tracker.SetAgeWeeks(member, 1000);
            
            int initialAge = _tracker.GetAgeWeeks(member);
            int increment = 10;
            
            int newAge = _tracker.IncrementAge(member, increment);
            
            Assert.AreEqual(initialAge + increment, newAge);
            Assert.AreEqual(newAge, _tracker.GetAgeWeeks(member));
        }

        [Test]
        public void IncrementAge_NegativeProgressionIsNormalizedToZero()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            typeof(FamilyMember).GetField("familyId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, 127);

            _tracker.SetAgeWeeks(member, 5 * LifespanConstants.WeeksPerYear);

            int newAge = _tracker.IncrementAge(member, -1000);

            Assert.AreEqual(0, newAge);
            Assert.AreEqual(0, _tracker.GetAgeWeeks(member));
        }

        [Test]
        public void GenerateAgeForContext_FamilyMemberUsesChildStatus()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            typeof(FamilyMember).GetField("familyId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, 128);
            typeof(BaseCharacter).GetField("m_child", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, true);
            typeof(UnityEngine.Object).GetField("m_CachedPtr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, new System.IntPtr(1));

            int ageWeeks = _tracker.GenerateAgeForContext(member, AgeContext.FamilyMember);

            Assert.GreaterOrEqual(ageWeeks, _config.initialChildAgeMinYears * LifespanConstants.WeeksPerYear);
            Assert.LessOrEqual(ageWeeks, _config.initialChildAgeMaxYears * LifespanConstants.WeeksPerYear);
        }

        [Test]
        public void IncrementAge_UsesFixedChildCutoffAtTenYears()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            int memberId = 124;
            typeof(FamilyMember).GetField("familyId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, memberId);

            // Age 9 -> accelerated (+2 years per 1-year tick)
            _tracker.SetAgeWeeks(member, 9 * 52);
            int nineYearResult = _tracker.IncrementAge(member, 52);
            Assert.AreEqual((9 * 52) + 104, nineYearResult);

            // Age 10 -> normal speed (+1 year per 1-year tick)
            _tracker.SetAgeWeeks(member, 10 * 52);
            int tenYearResult = _tracker.IncrementAge(member, 52);
            Assert.AreEqual((10 * 52) + 52, tenYearResult);
        }

        [Test]
        public void IncrementAge_UsesConfiguredChildhoodCutoffAge()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            typeof(FamilyMember).GetField("familyId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, 125);
            _config.childhoodAccelerationCutoffAge = 8;

            _tracker.SetAgeWeeks(member, 7 * 52);
            Assert.AreEqual((7 * 52) + (2 * 52), _tracker.IncrementAge(member, 52));

            _tracker.SetAgeWeeks(member, 8 * 52);
            Assert.AreEqual((8 * 52) + 52, _tracker.IncrementAge(member, 52));
        }

        [Test]
        public void IsElder_UsesConfiguredThresholdBoundary()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            typeof(FamilyMember).GetField("familyId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, 126);

            _tracker.SetAgeWeeks(member, (_config.elderAgeYears * 52) - 1);
            Assert.IsFalse(_tracker.IsElder(member));

            _tracker.SetAgeWeeks(member, _config.elderAgeYears * 52);
            Assert.IsTrue(_tracker.IsElder(member));
        }

        [Test]
        public void AgeData_RoundTrip_PreservesExistingSaveSchemaFields()
        {
            var saved = new AgeDataSerializable();
            saved.ages.Add(new AgeEntry { id = 1, weeks = 520 });
            saved.externalAges.Add(new AgeEntry { id = 90, weeks = 2080 });
            saved.illnesses.Add(new IllnessEntry { id = 1, illnessIds = new List<string> { ElderIllnessManager.ILLNESS_MILD_HEART } });
            saved.onsetData.Add(new OnsetEntry { id = 1, onsetWeeks = new Dictionary<string, int> { { ElderIllnessManager.ILLNESS_MILD_HEART, 600 } } });
            saved.deathDays.Add(new AgeEntry { id = 2, weeks = 125 });
            saved.deathAges.Add(new AgeEntry { id = 2, weeks = 4200 });
            saved.triggeredMilestones.Add(new MilestoneEntry { id = 1, keys = new List<string> { "Milestone_Adult" } });
            saved.lastBirthdays.Add(new BirthdayEntry { id = 1, year = 10 });
            saved.dialogueHistory.Add(new DialogueHistoryEntry { key = "Birthday_1", hashes = new List<int> { 1234 } });
            saved.lastProcessedAgingWeek = 44;

            AgeData data = AgeData.FromSerializable(saved);
            AgeDataSerializable roundTrip = data.ToSerializable();

            Assert.AreEqual(520, roundTrip.ages[0].weeks);
            Assert.AreEqual(2080, roundTrip.externalAges[0].weeks);
            Assert.AreEqual(ElderIllnessManager.ILLNESS_MILD_HEART, roundTrip.illnesses[0].illnessIds[0]);
            Assert.AreEqual(600, roundTrip.onsetData[0].onsetWeeks[ElderIllnessManager.ILLNESS_MILD_HEART]);
            Assert.AreEqual(125, roundTrip.deathDays[0].weeks);
            Assert.AreEqual(4200, roundTrip.deathAges[0].weeks);
            Assert.AreEqual("Milestone_Adult", roundTrip.triggeredMilestones[0].keys[0]);
            Assert.AreEqual(10, roundTrip.lastBirthdays[0].year);
            Assert.AreEqual(1234, roundTrip.dialogueHistory[0].hashes[0]);
            Assert.AreEqual(44, roundTrip.lastProcessedAgingWeek);
        }

        [Test]
        public void AgeData_RoundTrip_PreservesGreyingProfileBehavior()
        {
            var saved = new AgeDataSerializable();
            saved.greyProfiles.Add(new GreyProfileEntry
            {
                id = 7,
                profile = new GreyProfile
                {
                    OriginalColor = new[] { 0.2f, 0.4f, 0.6f },
                    Gene = new GreyingGene(42, 12, 0.8f),
                    StartAgeWeeks = 42 * 52,
                    DurationWeeks = 12 * 52
                }
            });

            AgeData data = AgeData.FromSerializable(saved);
            AgeDataSerializable roundTrip = data.ToSerializable();
            GreyProfile restored = roundTrip.greyProfiles[0].profile;

            Assert.AreEqual(7, roundTrip.greyProfiles[0].id);
            Assert.AreEqual(42, restored.Gene.StartAge);
            Assert.AreEqual(12, restored.Gene.DurationYears);
            Assert.AreEqual(0.8f, restored.Gene.MaxCoverage, 0.001f);
            Assert.AreEqual(0.4f, restored.Gene.GetGreyFactor(48f), 0.001f);
            CollectionAssert.AreEqual(new[] { 0.2f, 0.4f, 0.6f }, restored.OriginalColor);
            Assert.AreEqual(42 * 52, restored.StartAgeWeeks);
            Assert.AreEqual(12 * 52, restored.DurationWeeks);
        }

        [Test]
        public void AgeData_FromSerializable_NormalizesAgesAndCopiesMutableCollections()
        {
            var saved = new AgeDataSerializable();
            saved.ages.Add(new AgeEntry { id = 2, weeks = -20 });
            saved.illnesses.Add(new IllnessEntry
            {
                id = 2,
                illnessIds = new List<string> { ElderIllnessManager.ILLNESS_MILD_HEART, "", ElderIllnessManager.ILLNESS_MILD_HEART }
            });
            saved.onsetData.Add(new OnsetEntry
            {
                id = 2,
                onsetWeeks = new Dictionary<string, int> { { ElderIllnessManager.ILLNESS_MILD_HEART, -5 } }
            });

            AgeData data = AgeData.FromSerializable(saved);
            saved.illnesses[0].illnessIds.Clear();
            saved.onsetData[0].onsetWeeks.Clear();

            Assert.AreEqual(0, data.familyMemberAges[2]);
            CollectionAssert.AreEqual(new[] { ElderIllnessManager.ILLNESS_MILD_HEART }, data.elderIllnesses[2]);
            Assert.AreEqual(0, data.onsetTiming[2][ElderIllnessManager.ILLNESS_MILD_HEART]);
        }

        [Test]
        public void AgeData_FromSerializable_NormalizesPersistedDeathAge()
        {
            var saved = new AgeDataSerializable();
            saved.deathAges.Add(new AgeEntry { id = 9, weeks = -40 });

            AgeData data = AgeData.FromSerializable(saved);

            Assert.AreEqual(0, data.deceasedDeathAges[9]);
        }

        [Test]
        public void AgeDataSerializable_CopyFrom_KeepsRegisteredListInstances()
        {
            var container = new AgeDataSerializable();
            List<AgeEntry> originalAgesList = container.ages;

            var source = new AgeDataSerializable();
            source.ages.Add(new AgeEntry { id = 7, weeks = 700 });

            container.CopyFrom(source);

            Assert.AreSame(originalAgesList, container.ages);
            Assert.AreEqual(1, container.ages.Count);
            Assert.AreEqual(700, container.ages[0].weeks);
        }

        [Test]
        public void AgeDataSerializable_CopyFromRuntimeData_KeepsRegisteredListInstances()
        {
            var container = new AgeDataSerializable();
            List<AgeEntry> originalAgesList = container.ages;
            List<DialogueHistoryEntry> originalDialogueList = container.dialogueHistory;

            var data = new AgeData();
            data.familyMemberAges[7] = 700;
            data.dialogueHistory["Birthday_7"] = new HashSet<int> { 42 };

            container.CopyFrom(data);

            Assert.AreSame(originalAgesList, container.ages);
            Assert.AreSame(originalDialogueList, container.dialogueHistory);
            Assert.AreEqual(700, container.ages[0].weeks);
            Assert.AreEqual(42, container.dialogueHistory[0].hashes[0]);
        }
    }
}
