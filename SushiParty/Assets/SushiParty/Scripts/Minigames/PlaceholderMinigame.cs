using SushiParty.Audio;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SushiParty.Minigames
{
    public sealed class PlaceholderMinigame : MinigameController
    {
        private MinigameDefinition resolved;
        public override MinigameId Id => Resolve().Id;
        protected override bool UsesRoundStructure => false;

        private MinigameDefinition Resolve()
        {
            if (resolved != null)
            {
                return resolved;
            }

            string sceneName = gameObject.scene.name;
            if (!MinigameLibrary.TryGetBySceneName(sceneName, out resolved))
            {
                Debug.LogError(
                    $"PlaceholderMinigame is in scene '{sceneName}', which no MinigameDefinition " +
                    "claims. Check the SceneName values in MinigameLibrary.",
                    this);
                resolved = MinigameLibrary.Get(MinigameId.BumperSparks);
            }

            return resolved;
        }

        protected override void OnPrepare()
        {
            MinigameDefinition definition = Resolve();

            Canvas canvas = UiKit.CreateCanvas("SpecSheet", 50);
            canvas.transform.SetParent(transform, false);

            UiKit.Panel(canvas.transform, "Backdrop", UiKit.PartyBlue).Rt().Stretch();

            RectTransform card = UiKit.Rect(canvas.transform, "Spec");
            card.Pin(new Vector2(0.5f, 0.5f), new Vector2(1420f, 820f), Vector2.zero);
            UiKit.Window(card, UiKit.Cream);
            UiKit.Banner(card, definition.DisplayName, definition.Accent, 46, 900f).Chunky(2.4f);

            UiKit.Label(card, "NOT BUILT YET  ·  DESIGN SPEC", 24, TextAnchor.UpperLeft,
                    new Color(0.95f, 0.72f, 0.35f), FontStyle.Bold)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(1340f, 32f), new Vector2(46f, -122f));

            UiKit.Label(card, "OBJECTIVE", 22, TextAnchor.UpperLeft, UiKit.DarkInkDim, FontStyle.Bold)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(1340f, 30f), new Vector2(44f, -180f));

            UiKit.Label(card, definition.Objective, 28, TextAnchor.UpperLeft, UiKit.DarkInk)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(1340f, 90f), new Vector2(44f, -214f));

            UiKit.Label(card, "CO-OP ROLES", 22, TextAnchor.UpperLeft, UiKit.DarkInkDim, FontStyle.Bold)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(1340f, 30f), new Vector2(44f, -312f));

            UiKit.Label(card,
                    $"Seat 1 — {definition.RoleP1}\nSeat 2 — {definition.RoleP2}\n"
                    + $"Time limit — {definition.TimeLimit:0}s",
                    26, TextAnchor.UpperLeft, UiKit.DarkInk)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(1340f, 110f), new Vector2(44f, -346f));

            UiKit.Label(card, "WHAT IT NEEDS", 22, TextAnchor.UpperLeft, UiKit.DarkInkDim, FontStyle.Bold)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(1340f, 30f), new Vector2(44f, -466f));

            UiKit.Label(card, definition.DesignNotes, 24, TextAnchor.UpperLeft, UiKit.DarkInkDim)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(1340f, 250f), new Vector2(44f, -500f));

            UiKit.Label(card, "Action button: back to the minigame list",
                    26, TextAnchor.LowerCenter, definition.Accent)
                .Rt().Pin(new Vector2(0.5f, 0f), new Vector2(1340f, 44f), new Vector2(0f, 30f));
        }

        protected override void OnPlay(float deltaTime)
        {
            if (AnyHumanPressed(MinigameAction.Primary))
            {
                ReturnToMenu();
            }
        }

        protected override IParticipantInput CreateCpuBrain(Participant participant)
        {
            return NullParticipantInput.Instance;
        }
    }
}
