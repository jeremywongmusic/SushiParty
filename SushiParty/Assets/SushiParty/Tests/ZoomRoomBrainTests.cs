using NUnit.Framework;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Minigames.ZoomRoom;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class ZoomRoomBrainTests
    {
        private const int CorridorCells = 9;
        private const int CorridorRow = 1;
        private const CpuSkill Precise = CpuSkill.Sharp;

        private static MazeGrid Corridor()
        {
            return new MazeGrid(CorridorCells, 1, 0);
        }

        private static Vector2 At(MazeGrid maze, int tileX)
        {
            return maze.TileToWorld(new Vector2Int(tileX, CorridorRow));
        }

        private static ZoomRoomBrain Seat(
            ParticipantSlot slot,
            ZoomRoomSnapshot world,
            CpuSkill skill = Precise)
        {
            return new ZoomRoomBrain(() => world, slot, skill);
        }

        [Test]
        public void A_brain_with_no_maze_yet_stands_still()
        {
            ZoomRoomBrain brain = Seat(ParticipantSlot.One, default(ZoomRoomSnapshot));

            brain.Tick(0.1f);

            Assert.That(brain.Move, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void It_walks_straight_at_him_once_it_is_a_tile_away()
        {
            MazeGrid maze = Corridor();
            ZoomRoomBrain brain = Seat(
                ParticipantSlot.One,
                new ZoomRoomSnapshot(maze, At(maze, 9), At(maze, 8), At(maze, 17)));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.GreaterThan(0.9f), "close enough to just take him");
            Assert.That(Mathf.Abs(brain.Move.y), Is.LessThan(0.01f), "the corridor has no other way out");
        }

        [Test]
        public void It_steers_away_from_him_rather_than_tailing_a_faster_chef()
        {
            MazeGrid maze = Corridor();

            ZoomRoomBrain brain = Seat(
                ParticipantSlot.One,
                new ZoomRoomSnapshot(maze, At(maze, 9), At(maze, 3), At(maze, 15)));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.LessThan(-0.9f), "it must not join the tail-chase");
        }

        [Test]
        public void The_two_seats_hold_opposite_ends_of_the_corridor()
        {
            MazeGrid maze = Corridor();
            ZoomRoomSnapshot world = new ZoomRoomSnapshot(maze, At(maze, 9), At(maze, 3), At(maze, 15));

            ZoomRoomBrain one = Seat(ParticipantSlot.One, world);
            ZoomRoomBrain two = Seat(ParticipantSlot.Two, world);

            one.Tick(0.1f);
            two.Tick(0.1f);

            Assert.That(one.Move.x, Is.LessThan(-0.9f), "seat one owns the left approach");
            Assert.That(two.Move.x, Is.GreaterThan(0.9f), "seat two owns the right one");
        }

        [Test]
        public void It_stands_its_ground_once_it_reaches_the_tile_it_picked()
        {
            MazeGrid maze = Corridor();

            ZoomRoomBrain brain = Seat(
                ParticipantSlot.One,
                new ZoomRoomSnapshot(maze, At(maze, 9), At(maze, 2), At(maze, 15)));

            brain.Tick(0.1f);

            Assert.That(brain.Move.magnitude, Is.LessThan(1e-4f));
        }

        [Test]
        public void It_swaps_sides_when_its_partner_crosses_over()
        {
            MazeGrid maze = Corridor();
            ZoomRoomSnapshot live = new ZoomRoomSnapshot(maze, At(maze, 9), At(maze, 3), At(maze, 15));
            ZoomRoomBrain brain = new ZoomRoomBrain(() => live, ParticipantSlot.One, Precise);

            brain.Tick(0.3f);
            Assert.That(brain.Move.x, Is.LessThan(-0.9f), "partner is on the right, so it takes the left");

            live = new ZoomRoomSnapshot(maze, At(maze, 9), At(maze, 3), At(maze, 1));
            brain.Tick(0.3f);

            Assert.That(brain.Move.x, Is.GreaterThan(0.9f), "that end is taken now — go round the other way");
        }

        [Test]
        public void A_relaxed_seat_dithers_but_never_wanders_onto_its_partners_side()
        {
            MazeGrid maze = Corridor();

            for (int attempt = 0; attempt < 20; attempt++)
            {
                ZoomRoomBrain brain = Seat(
                    ParticipantSlot.One,
                    new ZoomRoomSnapshot(maze, At(maze, 9), At(maze, 3), At(maze, 15)),
                    CpuSkill.Relaxed);

                brain.Tick(0.1f);

                Assert.That(brain.Move.x, Is.LessThanOrEqualTo(0f), $"crossed over on attempt {attempt}");
            }
        }

        [Test]
        public void A_zoom_room_brain_is_still_just_a_participant_input()
        {
            MazeGrid maze = Corridor();
            IParticipantInput input = Seat(
                ParticipantSlot.One,
                new ZoomRoomSnapshot(maze, At(maze, 9), At(maze, 8), At(maze, 17)));

            input.Tick(0.1f);

            Assert.That(input.Move.x, Is.GreaterThan(0.9f),
                "gameplay must not be able to tell this seat from a human one");
            Assert.That(input.WasPressed(MinigameAction.Primary), Is.False,
                "there is no button in Zoom Room — you catch him by walking into him");
        }
    }
}
