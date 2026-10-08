using System.Collections.Generic;
using UnityEngine;

namespace SushiParty.Core
{
    public static class MinigameLibrary
    {
        public const int CoinStake = 5;

        private static readonly MinigameDefinition[] Entries =
        {
            new MinigameDefinition(
                MinigameId.BumperSparks,
                "Bumper Sparks",
                "BumperSparks",
                "Ram into Chef Tako to knock him into the electrified ring around the platform. Knock him into the ring three times to win!",
                "Drive a bumper vehicle",
                "Drive a bumper vehicle",
                30f,
                implemented: true,
                new Color(1f, 0.82f, 0.15f),
                "Symmetric top-down arena brawl. Vehicles accelerate toward the stick direction and " +
                "bounce off each other. Chef Tako loses a heart and respawns at centre with brief " +
                "invincibility when he touches the ring; players are stunned if they touch it " +
                "themselves. Chef Tako mostly flees, but will occasionally charge a player who is " +
                "standing near the ring to turn their own hazard against them."),

            new MinigameDefinition(
                MinigameId.SandTrap,
                "Sand Trap",
                "SandTrap",
                "Ground-pound a switch to highlight a row of blocks. Do it at the same time as your teammate to make a block disappear where your two rows intersect.",
                "Highlight rows (side switches)",
                "Highlight columns (front switches)",
                30f,
                implemented: true,
                new Color(0.99f, 0.68f, 0.24f),
                "Asymmetric grid coordination. One player owns rows, the other owns columns. A " +
                "ground-pound highlights that line for one second; when a row highlight and a " +
                "column highlight overlap in time, the block at their intersection falls away. " +
                "Chef Tako walks the remaining blocks and flees toward safety, so the win comes " +
                "from cutting off his escape squares before removing the one he stands on."),

            new MinigameDefinition(
                MinigameId.CrossfireCaverns,
                "Crossfire Caverns",
                "CrossfireCaverns",
                "Blast Chef Tako as he tries to evade you and your teammate. Score three direct hits to win!",
                "Left minecart cannon",
                "Right minecart cannon",
                30f,
                implemented: true,
                new Color(0.71f, 0.43f, 0.97f),
                "Facing-tracks shooter. Each player rides a minecart on a rail and fires rice balls " +
                "across the cavern. Chef Tako weaves between the tracks in the middle. Friendly " +
                "fire is live: a shot that misses can cross the field and disable your teammate, so " +
                "the tension is timing shots around each other. Destructible stalagmites block lanes."),

            new MinigameDefinition(
                MinigameId.PairOfAces,
                "Pair of Aces",
                "PairOfAces",
                "Blast Chef Tako out of the sky! Hit him with three shots from your cannon to win!",
                "Aim and fire / dodge",
                "Aim and fire / dodge",
                30f,
                implemented: true,
                new Color(0.34f, 0.63f, 1f),
                "Aiming game — the only one in the set that needs a screen-space cursor rather than " +
                "a movement vector, so it exercises the Aim channel of the input abstraction. " +
                "Chef Tako flies evasive patterns (circles, zigzags) in his flying wok. Three " +
                "hits win; each hit costs him a heart and grants brief invincibility. He returns " +
                "fire with wasabi bombs that must be dodged or the player's cannon is disabled briefly."),

            new MinigameDefinition(
                MinigameId.PedalToThePaddle,
                "Pedal to the Paddle",
                "PedalToThePaddle",
                "Work with your teammate to rotate your wheel by jumping on the platforms that will spin it to the right. Reach the goal before Chef Tako to win!",
                "Jump the paddle wheel",
                "Jump the paddle wheel",
                60f,
                implemented: true,
                new Color(0.16f, 0.8f, 0.91f),
                "Co-op race. The pair share one paddle-wheel platform: landing weight on the " +
                "right-hand paddles rotates the wheel forward and drives the craft toward the goal. " +
                "Both players pushing in rhythm is far faster than one. Falling off puts you in a " +
                "bubble for a few seconds, which is the real cost of a greedy jump. Beat Chef " +
                "Jr.'s platform to the finish to win."),

            new MinigameDefinition(
                MinigameId.ZoomRoom,
                "Zoom Room",
                "ZoomRoom",
                "Work with your teammate to catch Chef Tako as he races around the maze.",
                "Chase through the maze",
                "Chase through the maze",
                30f,
                implemented: true,
                new Color(1f, 0.45f, 0.75f),
                "Pursuit with a genuinely uncatchable-alone target: Chef Tako is strictly faster " +
                "than the players, so the only solve is a pincer — one player herds him into a dead " +
                "end while the other closes from the far side. He sweats visibly as players close."),
        };

        public static IReadOnlyList<MinigameDefinition> All => Entries;

        public static MinigameDefinition Get(MinigameId id)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Id == id)
                {
                    return Entries[i];
                }
            }

            throw new KeyNotFoundException($"No MinigameDefinition authored for {id}.");
        }

        public static bool TryGetBySceneName(string sceneName, out MinigameDefinition definition)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].SceneName == sceneName)
                {
                    definition = Entries[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
