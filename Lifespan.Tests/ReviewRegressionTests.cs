using System;
using System.Reflection;
using System.Runtime.Serialization;
using Lifespan;
using NUnit.Framework;

namespace Lifespan.Tests
{
    [TestFixture]
    public class ReviewRegressionTests
    {
        [Test]
        public void QueueFactory_LeavesUnknownJobTypesToTheirLoader()
        {
            Assert.IsNull(JobQueueSaveLoadPatch.CreateJob("OtherCustomJob"));
            Assert.IsNull(JobQueueSaveLoadPatch.CreateJob("Job_Craft"));
        }

        [Test]
        public void OrdinaryDeath_AfterNaturalDeath_UpdatesSurvivalDay()
        {
            var state = new LifespanState();
            state.Deaths.RecordDeath(1, 100, 80 * 52);
            var member = (FamilyMember)FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            typeof(UnityEngine.Object).GetField("m_CachedPtr", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(member, new IntPtr(1));
            member.SetId(2);
            var info = new FamilyManager.DeadCharacterInfo { death_date = 150 };
            GameOverPatches.DeathRecords = state.Deaths;
            try
            {
                GameOverPatches.FamilyManager_CreateObituaryInfo_Patch.Postfix(member, ref info);
                Assert.AreEqual(150, state.Deaths.GetMaxDeathDay());
                Assert.AreEqual(150, info.death_date);
            }
            finally
            {
                GameOverPatches.DeathRecords = null;
            }
        }

    }
}
