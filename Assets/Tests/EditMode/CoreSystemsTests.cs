using NUnit.Framework;
using UnityEngine;

namespace BaziBaqa.Tests
{
    public class CoreSystemsTests
    {
        [Test]
        public void NewSaveContainsCooperativeGroupAndCamp()
        {
            GameSaveData save = GameSaveData.CreateNew(1234);
            Assert.AreEqual(6, save.survivors.Count);
            Assert.AreEqual(SurvivorRole.Medic, save.survivors[2].role);
            Assert.IsTrue(save.buildings.Exists(building => building.type == BuildingType.Camp));
        }

        [Test]
        public void ResourceStateNeverDropsBelowZero()
        {
            ResourceState resources = new ResourceState();
            resources.Add(ResourceType.Wood, -999);
            Assert.AreEqual(0, resources.wood);
            resources.Add(ResourceType.Water, 12);
            Assert.AreEqual(102, resources.water);
        }

        [Test]
        public void ClockChangesDayAndEmitsEvent()
        {
            GameClock clock = new GameClock { DayLengthSeconds = 10f };
            clock.Initialize(1, 0.99f);
            int changedDay = 0;
            clock.DayChanged += day => changedDay = day;
            clock.Advance(1f);
            Assert.AreEqual(2, clock.Day);
            Assert.AreEqual(2, changedDay);
        }

        [Test]
        public void SaveVectorRoundTrips()
        {
            SerializableVector3 serializable = new SerializableVector3(new Vector3(3f, 2f, -4f));
            Vector3 value = serializable.ToVector3();
            Assert.AreEqual(3f, value.x);
            Assert.AreEqual(2f, value.y);
            Assert.AreEqual(-4f, value.z);
        }
    }
}
