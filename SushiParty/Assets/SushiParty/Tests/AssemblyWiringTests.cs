using NUnit.Framework;
using SushiParty.Core;

namespace SushiParty.Tests
{
    public sealed class AssemblyWiringTests
    {
        [Test]
        public void Library_lists_every_minigame_id()
        {
            foreach (MinigameId id in System.Enum.GetValues(typeof(MinigameId)))
            {
                Assert.That(MinigameLibrary.Get(id), Is.Not.Null, $"no definition for {id}");
            }
        }

        [Test]
        public void Coin_stake_is_five_each_way()
        {
            Assert.That(MinigameLibrary.CoinStake, Is.EqualTo(5));
        }
    }
}
