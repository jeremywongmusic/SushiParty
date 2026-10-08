using SushiParty.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace SushiParty.Audio
{
    public readonly struct AudioEvent
    {
        public readonly string Path;

        public AudioEvent(string path)
        {
            Path = path;
        }

        public bool IsValid => !string.IsNullOrEmpty(Path);
        public override string ToString() => Path;
    }

    public readonly struct AudioParameter
    {
        public readonly string Name;
        public readonly float Value;
        public readonly string Label;
        public readonly bool IgnoreSeekSpeed;

        private AudioParameter(string name, float value, string label, bool ignoreSeekSpeed)
        {
            Name = name;
            Value = value;
            Label = label;
            IgnoreSeekSpeed = ignoreSeekSpeed;
        }

        public bool IsValid => !string.IsNullOrEmpty(Name);
        public bool IsLabelled => Label != null;
        public static AudioParameter None => default;

        public static AudioParameter Number(string name, float value, bool ignoreSeekSpeed = false)
        {
            return new AudioParameter(name, value, null, ignoreSeekSpeed);
        }

        public static AudioParameter Labelled(string name, string label, bool ignoreSeekSpeed = false)
        {
            if (string.IsNullOrEmpty(label))
            {
                UnityEngine.Debug.LogWarning(
                    $"[SushiParty] Parameter '{name}' was given no label. Ignoring it.");
                return None;
            }

            return new AudioParameter(name, 0f, label, ignoreSeekSpeed);
        }

        public override string ToString()
        {
            if (!IsValid)
            {
                return "(none)";
            }

            return IsLabelled ? $"{Name}=\"{Label}\"" : $"{Name}={Value:0.00}";
        }
    }

    public readonly struct AudioHandle
    {
        internal readonly int Id;

        internal AudioHandle(int id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;
        public static readonly AudioHandle None = new AudioHandle(0);
    }

    public static class Sfx
    {
        private const string Root = "event:/SFX/";
        private const string MusicRoot = "event:/Music/";
        private static AudioEvent E(string path) => new AudioEvent(Root + path);
        private static AudioEvent M(string path) => new AudioEvent(MusicRoot + path);
        public const string ForceParameter = "Force";
        public const string SpeedParameter = "Speed";
        public const string StepParameter = "Step";
        public const string ProximityParameter = "Proximity";
        public const string UrgencyParameter = "Urgency";
        public const string AmountParameter = "Amount";
        public const string KindParameter = "Kind";
        public const string ChoiceParameter = "Choice";
        public const string StateParameter = "State";
        public static readonly AudioEvent MenuMove = E("UI/MenuMove");
        public static readonly AudioEvent MenuConfirm = E("UI/MenuConfirm");
        public static readonly AudioEvent MenuToggle = E("UI/MenuToggle");
        public static readonly AudioEvent MenuBack = E("UI/MenuBack");
        public static readonly AudioEvent ButtonOver = E("UI/ButtonOver");
        public static readonly AudioEvent ButtonOut = E("UI/ButtonOut");
        public static readonly AudioEvent ButtonIn = E("UI/ButtonIn");
        public static readonly AudioEvent TitleShow = E("UI/TitleShow");
        public static readonly AudioEvent TitleMove = E("UI/TitleMove");
        public static readonly AudioEvent TitleConfirm = E("UI/TitleConfirm");
        public static readonly AudioEvent BriefingShow = E("Round/BriefingShow");
        public static readonly AudioEvent CountdownTick = E("Round/CountdownTick");
        public static readonly AudioEvent Go = E("Round/Go");
        public static readonly AudioEvent Win = E("Round/Win");
        public static readonly AudioEvent Lose = E("Round/Lose");
        public static readonly AudioEvent Results = E("Round/Results");
        public static readonly AudioEvent ClockWarning = E("Round/ClockWarning");
        public static readonly AudioEvent CharacterFootstep = E("Character/Footstep");
        public static readonly AudioEvent CharacterJump = E("Character/Jump");
        public static readonly AudioEvent CharacterLand = E("Character/Land");
        public static readonly AudioEvent CharacterPoundDive = E("Character/PoundDive");
        public static readonly AudioEvent CharacterPoundRecover = E("Character/PoundRecover");
        public static readonly AudioEvent CharacterCheer = E("Character/Cheer");
        public static readonly AudioEvent BoardDieRoll = E("Board/DieRoll");
        public static readonly AudioEvent BoardDieLand = E("Board/DieLand");
        public static readonly AudioEvent BoardTokenStep = E("Board/TokenStep");
        public static readonly AudioEvent BoardSpaceStart = E("Board/SpaceStart");
        public static readonly AudioEvent BoardSpaceCoinGain = E("Board/SpaceCoinGain");
        public static readonly AudioEvent BoardSpaceCoinLoss = E("Board/SpaceCoinLoss");
        public static readonly AudioEvent BoardSpaceWasabi = E("Board/SpaceWasabi");
        public static readonly AudioEvent BoardSpaceSwap = E("Board/SpaceSwap");
        public static readonly AudioEvent BoardShrineClaim = E("Board/ShrineClaim");
        public static readonly AudioEvent BoardShrineDenied = E("Board/ShrineDenied");
        public static readonly AudioEvent BoardShrineMove = E("Board/ShrineMove");
        public static readonly AudioEvent BoardTurnPass = E("Board/TurnPass");
        public static readonly AudioEvent BoardTurnSkipped = E("Board/TurnSkipped");
        public static readonly AudioEvent BoardRoundEnd = E("Board/RoundEnd");
        public static readonly AudioEvent BoardSessionWin = E("Board/SessionWin");
        public static readonly AudioEvent BoardSessionLose = E("Board/SessionLose");
        public static readonly AudioEvent BumperImpact = E("BumperSparks/Impact");
        public static readonly AudioEvent BumperRingZap = E("BumperSparks/RingZap");
        public static readonly AudioEvent BumperChefRinged = E("BumperSparks/ChefRinged");
        public static readonly AudioEvent BumperChefRespawn = E("BumperSparks/ChefRespawn");
        public static readonly AudioEvent BumperStunRecover = E("BumperSparks/StunRecover");
        public static readonly AudioEvent BumperEngineLoop = E("BumperSparks/EngineLoop");
        public static readonly AudioEvent SandPound = E("SandTrap/Pound");
        public static readonly AudioEvent SandPoundMissed = E("SandTrap/PoundMissed");
        public static readonly AudioEvent SandLineLit = E("SandTrap/LineLit");
        public static readonly AudioEvent SandBlockFall = E("SandTrap/BlockFall");
        public static readonly AudioEvent SandChefStep = E("SandTrap/ChefStep");
        public static readonly AudioEvent SandChefFall = E("SandTrap/ChefFall");
        public static readonly AudioEvent SandNothingThere = E("SandTrap/NothingThere");
        public static readonly AudioEvent CrossfireFire = E("Crossfire/Fire");
        public static readonly AudioEvent CrossfireHitChef = E("Crossfire/HitChef");
        public static readonly AudioEvent CrossfireHitRock = E("Crossfire/HitStalagmite");
        public static readonly AudioEvent CrossfireFireBlocked = E("Crossfire/FireBlocked");
        public static readonly AudioEvent CrossfireCartDisabled = E("Crossfire/CartDisabled");
        public static readonly AudioEvent CrossfireCartRecovered = E("Crossfire/CartRecovered");
        public static readonly AudioEvent CrossfireBallLost = E("Crossfire/BallLost");
        public static readonly AudioEvent CrossfireFriendlyFire = E("Crossfire/FriendlyFire");
        public static readonly AudioEvent CrossfireCartLoop = E("Crossfire/CartLoop");
        public static readonly AudioEvent AcesFire = E("PairOfAces/Fire");
        public static readonly AudioEvent AcesHit = E("PairOfAces/Hit");
        public static readonly AudioEvent AcesMiss = E("PairOfAces/Miss");
        public static readonly AudioEvent AcesWasabiLaunch = E("PairOfAces/WasabiLaunch");
        public static readonly AudioEvent AcesWasabiImpact = E("PairOfAces/WasabiImpact");
        public static readonly AudioEvent AcesDodge = E("PairOfAces/Dodge");
        public static readonly AudioEvent AcesDodgeClean = E("PairOfAces/DodgeClean");
        public static readonly AudioEvent AcesCannonJammed = E("PairOfAces/CannonJammed");
        public static readonly AudioEvent AcesFireBlocked = E("PairOfAces/FireBlocked");
        public static readonly AudioEvent ZoomChaseLoop = E("ZoomRoom/ChaseLoop");
        public static readonly AudioEvent ZoomChefTurn = E("ZoomRoom/ChefTurn");
        public static readonly AudioEvent ZoomWallBump = E("ZoomRoom/WallBump");
        public static readonly AudioEvent ZoomCatch = E("ZoomRoom/Catch");
        public static readonly AudioEvent PedalStomp = E("Pedal/Stomp");
        public static readonly AudioEvent PedalStompWasted = E("Pedal/StompWasted");
        public static readonly AudioEvent PedalWheelLoop = E("Pedal/WheelLoop");
        public static readonly AudioEvent PedalSplash = E("Pedal/Splash");
        public static readonly AudioEvent PedalBubblePop = E("Pedal/BubblePop");
        public static readonly AudioEvent PedalGoal = E("Pedal/Goal");
        public static readonly AudioEvent PedalWheelStall = E("Pedal/WheelStall");
        public static readonly AudioEvent MusicRoundStart = M("Round/Start");
        public static readonly AudioEvent MusicRoundStop = M("Round/Stop");
        public static readonly AudioEvent MusicBoard = M("Board/Session");
        public static readonly AudioEvent MusicMenu = M("Menu/Shell");
        public static readonly AudioEvent MusicBumperSparks = M("Minigame/BumperSparks");
        public static readonly AudioEvent MusicCrossfireCaverns = M("Minigame/CrossfireCaverns");
        public static readonly AudioEvent MusicPairOfAces = M("Minigame/PairOfAces");
        public static readonly AudioEvent MusicPedalToThePaddle = M("Minigame/PedalToThePaddle");
        public static readonly AudioEvent MusicSandTrap = M("Minigame/SandTrap");
        public static readonly AudioEvent MusicZoomRoom = M("Minigame/ZoomRoom");

        public static AudioEvent MusicFor(MinigameId id)
        {
            switch (id)
            {
                case MinigameId.BumperSparks:
                    return MusicBumperSparks;
                case MinigameId.CrossfireCaverns:
                    return MusicCrossfireCaverns;
                case MinigameId.PairOfAces:
                    return MusicPairOfAces;
                case MinigameId.PedalToThePaddle:
                    return MusicPedalToThePaddle;
                case MinigameId.SandTrap:
                    return MusicSandTrap;
                case MinigameId.ZoomRoom:
                    return MusicZoomRoom;
                default:
                    return default(AudioEvent);
            }
        }

        private static Dictionary<string, AudioEvent> byName;
        private static ReadOnlyCollection<string> allNames;

        public static IReadOnlyList<string> AllNames
        {
            get
            {
                EnsureIndex();
                return allNames;
            }
        }

        public static bool TryFind(string name, out AudioEvent audioEvent)
        {
            audioEvent = default(AudioEvent);

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            EnsureIndex();

            if (byName.TryGetValue(name, out audioEvent))
            {
                return true;
            }

            string trimmed = name.Trim();
            return trimmed.Length != name.Length && byName.TryGetValue(trimmed, out audioEvent);
        }

        public static AudioEvent Find(string name)
        {
            TryFind(name, out AudioEvent audioEvent);
            return audioEvent;
        }

        private static void EnsureIndex()
        {
            if (byName != null)
            {
                return;
            }

            Dictionary<string, AudioEvent> map =
                new Dictionary<string, AudioEvent>(StringComparer.OrdinalIgnoreCase);
            List<string> names = new List<string>();

            FieldInfo[] fields = typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(AudioEvent))
                {
                    continue;
                }

                AudioEvent audioEvent = (AudioEvent)field.GetValue(null);
                if (!audioEvent.IsValid)
                {
                    continue;
                }

                names.Add(field.Name);

                map[field.Name] = audioEvent;
                map[audioEvent.Path] = audioEvent;
            }

            allNames = names.AsReadOnly();

            byName = map;
        }
    }
}
