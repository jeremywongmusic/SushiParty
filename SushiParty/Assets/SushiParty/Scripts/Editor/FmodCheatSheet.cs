using SushiParty.Audio;
using SushiParty.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SushiParty.EditorTools
{
    public enum FmodFormKind
    {
        OneShot,
        Loop,
        Global,
        Mixer,
        Catalogue,
        Diagnostics,
    }

    public sealed class FmodApiForm
    {
        public string Owner;
        public string Signature;
        public string Returns;
        public string Title;
        public string When;
        public string Snippet;
        public string Notes = string.Empty;
        public FmodFormKind Kind;

        public string For(string eventName, string parameter)
        {
            string sound = "Sfx." + (string.IsNullOrEmpty(eventName) ? "MenuConfirm" : eventName);

            return Snippet
                .Replace("{event}", sound)
                .Replace("{param}", "Sfx." + FmodCheatSheet.ParameterConstant(parameter));
        }
    }

    public sealed class FmodCallSite
    {
        public string File;
        public int Line;
        public string Statement;
    }

    public sealed class FmodEventFacts
    {
        public string Name;
        public string Path;
        public string Category;
        public bool IsMusic;
        public bool Spatial;
        public bool Loops;
        public string Parameter;
        public bool Used;
        public IReadOnlyList<FmodCallSite> CallSites = Array.Empty<FmodCallSite>();
    }

    public sealed class FmodGlobalFacts
    {
        public string Name;
        public string Range;
        public string DrivenBy;
    }

    public readonly struct FmodSourceFile
    {
        public readonly string Path;
        public readonly string Text;

        public FmodSourceFile(string path, string text)
        {
            Path = path;
            Text = text ?? string.Empty;
        }

        public string Name
        {
            get
            {
                int slash = Path.LastIndexOfAny(new[] { '/', '\\' });
                return slash < 0 ? Path : Path.Substring(slash + 1);
            }
        }
    }

    public sealed class FmodCheatSheet
    {
        public const string CatalogueFileName = "AudioEvent.cs";
        public const string ScriptFolder = "SushiParty/Scripts";

        public static readonly IReadOnlyList<string> NotCallSites = new[]
        {
            "FmodCheatSheet.cs",
            "FmodCheatSheetWindow.cs",
        };

        private const string SfxRoot = "event:/SFX/";
        private const string MusicSelector = "MusicFor";
        private const string MusicRoot = "event:/Music/";

        private static readonly Regex Declaration = new Regex(
            @"public\s+static\s+readonly\s+AudioEvent\s+(?<name>\w+)\s*=\s*(?<factory>\w+)\s*\(\s*""(?<path>[^""]*)""",
            RegexOptions.Compiled);

        private static readonly Regex DocParameter = new Regex(
            @"<see\s+cref=""(?<name>\w+Parameter)""", RegexOptions.Compiled);

        private static readonly Regex SfxReference = new Regex(@"\bSfx\.(?<name>\w+)\b", RegexOptions.Compiled);

        private static readonly Regex Positioned =
            new Regex(@"GameAudio\.(PlayAt|LoopAt|PlayLabelledAt)\s*\(", RegexOptions.Compiled);

        private static readonly Regex Looping = new Regex(@"GameAudio\.(Loop|LoopAt)\s*\(", RegexOptions.Compiled);

        private static readonly Regex ParameterAtCallSite =
            new Regex(@"Sfx\.(?<name>\w+Parameter)\b", RegexOptions.Compiled);

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        private FmodCheatSheet(IReadOnlyList<FmodEventFacts> events)
        {
            Events = events;
        }

        public IReadOnlyList<FmodEventFacts> Events { get; }

        public static readonly IReadOnlyList<FmodApiForm> Forms = new[]
        {
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "Play(AudioEvent)",
                Returns = "void",
                Kind = FmodFormKind.OneShot,
                Title = "Play a one-shot",
                When = "Anything that happens once and is not at a place: UI, banners, stingers.",
                Snippet = "GameAudio.Play({event});",
                Notes = "Nothing to hold and nothing to stop. Naming an entry that is missing " +
                        "from the FMOD banks logs once and is then silent, so a round never " +
                        "dies of a sound.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "Play(AudioEvent, string, float)",
                Returns = "void",
                Kind = FmodFormKind.OneShot,
                Title = "Play a one-shot carrying a value",
                When = "Pitch ladders and impact weight — a countdown tick, a cleared pair, a die landing.",
                Snippet = "GameAudio.Play({event}, {param}, value);",
                Notes = "The parameter has to exist on that event in Studio. Which one an event " +
                        "takes is the Parameter column on the left, and it is read off this " +
                        "call site — so passing a different one here changes what the FMOD " +
                        "generator authors.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "PlayAt(AudioEvent, Vector3)",
                Returns = "void",
                Kind = FmodFormKind.OneShot,
                Title = "Play a one-shot somewhere",
                When = "Anything with a position in the arena: an impact, a pop, a footstep.",
                Snippet = "GameAudio.PlayAt({event}, transform.position);",
                Notes = "Using this once is what marks the event spatialised for the whole " +
                        "project. Never use it for music — a track is not a thing happening " +
                        "at a place, and panning one round the arena is the result.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "PlayAt(AudioEvent, Vector3, string, float)",
                Returns = "void",
                Kind = FmodFormKind.OneShot,
                Title = "Play a one-shot somewhere, carrying a value",
                When = "A positioned hit whose force matters — a bumper collision, a stomp.",
                Snippet = "GameAudio.PlayAt({event}, transform.position, {param}, value);",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "Loop(AudioEvent)",
                Returns = "AudioHandle",
                Kind = FmodFormKind.Loop,
                Title = "Start a loop",
                When = "A bed that runs for as long as something is true: a track, an engine, a chase.",
                Snippet = "AudioHandle handle = GameAudio.Loop({event});",
                Notes = "Whoever starts a loop stops it. Keep the handle somewhere that sees " +
                        "every way the thing can end, including the ones nobody planned — " +
                        "MinigameController holds the round bed and stops it in OnDisable for " +
                        "exactly that reason.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "LoopAt(AudioEvent, Vector3)",
                Returns = "AudioHandle",
                Kind = FmodFormKind.Loop,
                Title = "Start a loop somewhere",
                When = "A bed that belongs to an object rather than to the round — one per vehicle, one per cart.",
                Snippet = "AudioHandle handle = GameAudio.LoopAt({event}, transform.position);",
                Notes = "It does not follow anything on its own. Call SetPosition every frame " +
                        "the object moves, or the sound stays where the object used to be.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "SetParameter(AudioHandle, string, float, bool)",
                Returns = "void",
                Kind = FmodFormKind.Loop,
                Title = "Drive a running loop",
                When = "Every frame, to hand the loop the thing it is a loop about: speed, proximity.",
                Snippet = "GameAudio.SetParameter(handle, {param}, speed01);",
                Notes = "This is how a looping event gets its parameter — never at the Loop call " +
                        "itself. Which is why a loop's Parameter column is read off its doc " +
                        "comment in Sfx rather than off any line of C#.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "SetPosition(AudioHandle, Vector3)",
                Returns = "void",
                Kind = FmodFormKind.Loop,
                Title = "Move a running loop",
                When = "Every frame, for a loop started with LoopAt on something that moves.",
                Snippet = "GameAudio.SetPosition(handle, transform.position);",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "Stop(ref AudioHandle)",
                Returns = "void",
                Kind = FmodFormKind.Loop,
                Title = "Stop a loop",
                When = "The moment the thing the loop is about stops being true.",
                Snippet = "GameAudio.Stop(ref handle);",
                Notes = "Takes the handle by reference and clears it, so the same loop cannot " +
                        "be stopped twice or driven after it has gone. Calling it on a handle " +
                        "that is already None does nothing.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "StopAll()",
                Returns = "void",
                Kind = FmodFormKind.Diagnostics,
                Title = "Stop every loop at once",
                When = "Almost never from gameplay — the scene loader already does it, twice.",
                Snippet = "GameAudio.StopAll();",
                Notes = "The backstop for a loop nobody owns any more. If you need this in a " +
                        "minigame, the handle is being dropped somewhere and that is the bug.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "SetGlobal(string, float, float)",
                Returns = "void",
                Kind = FmodFormKind.Global,
                Title = "Publish a number to the whole mix",
                When = "Every frame. State any event, snapshot or bus in Studio can read.",
                Snippet = "GameAudio.SetGlobal(Global.Objective, progress01);",
                Notes = "Values that have not moved are dropped, so calling this per frame " +
                        "costs almost nothing. The third argument is that dead band — widen it " +
                        "for something noisy, leave it alone otherwise. A minigame normally " +
                        "gets this for free by overriding SampleTelemetry.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "SetGlobalLabel(string, string)",
                Returns = "void",
                Kind = FmodFormKind.Global,
                Title = "Publish a label to the whole mix",
                When = "On a change of state, not per frame: which minigame, which phase.",
                Snippet = "GameAudio.SetGlobalLabel(Global.RoundPhase, phase.ToString());",
                Notes = "The label has to be one Studio knows. Both labelled parameters take " +
                        "theirs from the C# enums at generation time, so anything ToString() " +
                        "produces already has somewhere to land.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "OnSceneReady()",
                Returns = "void",
                Kind = FmodFormKind.Diagnostics,
                Title = "Hand the mix a new scene",
                When = "Never from gameplay. GameAudio hooks sceneLoaded itself.",
                Snippet = "GameAudio.OnSceneReady();",
                Notes = "Stops everything looping, re-attaches the FMOD listener to the new " +
                        "camera, and forgets the published globals so the next round " +
                        "republishes from scratch.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "IsLive",
                Returns = "bool",
                Kind = FmodFormKind.Diagnostics,
                Title = "Ask whether FMOD is actually there",
                When = "Editor tooling and diagnostics. Gameplay should never branch on it.",
                Snippet = "if (GameAudio.IsLive) { ... }",
                Notes = "False means SUSHIPARTY_FMOD is off and every call is a no-op. The game " +
                        "has to run in both states, which is why nothing in a minigame asks.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "LogEvents",
                Returns = "bool",
                Kind = FmodFormKind.Diagnostics,
                Title = "Print the event stream",
                When = "Checking the wiring, with or without any middleware installed.",
                Snippet = "GameAudio.LogEvents = true;",
                Notes = "The whole event stream a round produces, in order, in the console. " +
                        "This is how these call sites were verified before FMOD existed here, " +
                        "and it is still the fastest way to see that a sound was asked for.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "PlayLabelled(AudioEvent, string, string)",
                Returns = "void",
                Kind = FmodFormKind.OneShot,
                Title = "Play a one-shot carrying a named state",
                When = "When the variation has a name rather than a number: a tile kind, a surface, a hit that missed.",
                Snippet = "GameAudio.PlayLabelled({event}, Sfx.KindParameter, kind.ToString());",
                Notes = "This is what lets one event stand in for several. Pass an enum's " +
                        "ToString() rather than a literal — the label has to match Studio " +
                        "exactly, and unlike a number a wrong one is caught at the call.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "PlayLabelledAt(AudioEvent, Vector3, string, string)",
                Returns = "void",
                Kind = FmodFormKind.OneShot,
                Title = "Play a named state somewhere",
                When = "The same, for something that happened at a place.",
                Snippet = "GameAudio.PlayLabelledAt({event}, transform.position, Sfx.StateParameter, state.ToString());",
                Notes = "Same rules as the unpositioned one. Using it marks the event " +
                        "spatialised for the whole project, exactly as PlayAt does.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "SetParameterLabel(AudioHandle, string, string)",
                Returns = "void",
                Kind = FmodFormKind.Loop,
                Title = "Switch a loop into a named state",
                When = "A running loop that changes character rather than degree — grounded to airborne, calm to alarmed.",
                Snippet = "GameAudio.SetParameterLabel(handle, Sfx.StateParameter, state.ToString());",
                Notes = "The labelled counterpart of SetParameter. Use it where the states " +
                        "are a list rather than a range; a number would make Studio " +
                        "interpolate between two things that do not blend.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "StateOf(AudioHandle)",
                Returns = "AudioPlayback",
                Kind = FmodFormKind.Loop,
                Title = "Ask where a loop has got to",
                When = "Letting go of an instance that ends by itself, and telling silence apart from stopped.",
                Snippet = "if (GameAudio.StateOf(handle) == AudioPlayback.Stopped) { GameAudio.Stop(ref handle); }",
                Notes = "The one question a handle cannot answer for itself. Stopped covers " +
                        "both never-started and finished, so a track missing from the banks " +
                        "reads as stopped rather than as an error to think about. Starting " +
                        "and Stopping are the two you will see when something looks wrong.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "Settings",
                Returns = "MixerSettings",
                Kind = FmodFormKind.Mixer,
                Title = "Turn a bus or a VCA up and down",
                When = "The settings screen, and anything that wants to duck the mix by hand.",
                Snippet = "GameAudio.Settings.SetVolume(Mixer.Music, 0.5f);",
                Notes = "The player's slider positions, loaded once and owned here so a scene " +
                        "change cannot lose them. Setting a volume writes straight through to " +
                        "the bus; call Save() when a screen closes rather than on every nudge.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "Pause",
                Returns = "PauseController",
                Kind = FmodFormKind.Mixer,
                Title = "Hold the mix where it stands",
                When = "A pause menu, and the scene transitions that must not leak one.",
                Snippet = "GameAudio.Pause.Toggle();",
                Notes = "Holds the music and game buses and leaves the interface bus running, " +
                        "so the menu doing the pausing can still click. A held bus resumes " +
                        "mid-tail; stopping and restarting would clip it and retrigger it.",
            },
            new FmodApiForm
            {
                Owner = nameof(GameAudio),
                Signature = "Mixer",
                Returns = "IAudioMixer",
                Kind = FmodFormKind.Mixer,
                Title = "Read what a bus reports",
                When = "Diagnostics. Prefer Settings and Pause for anything that changes the mix.",
                Snippet = "MixerReading reading = GameAudio.Mixer.Read(Mixer.Master);",
                Notes = "The raw seam under the two above. Read() makes one pass over every " +
                        "call on a bus that only reads — identity, both volumes, mute, pause, " +
                        "channel group, CPU, memory and the output port.",
            },
            new FmodApiForm
            {
                Owner = nameof(Sfx),
                Signature = "TryFind(string, out AudioEvent)",
                Returns = "bool",
                Kind = FmodFormKind.Catalogue,
                Title = "Turn a typed name into an entry",
                When = "The animation seam, and editor tooling. Not gameplay.",
                Snippet = "if (Sfx.TryFind(name, out AudioEvent sound)) { GameAudio.PlayAt(sound, transform.position); }",
                Notes = "An animation event is a string in a clip, so it cannot be an " +
                        "AudioEvent and cannot be a compile error. Both spellings resolve — " +
                        "the member name, or the full Studio path. Gameplay that knows which " +
                        "sound it wants names the field instead and gets the compile error.",
            },
            new FmodApiForm
            {
                Owner = nameof(Sfx),
                Signature = "MusicFor(MinigameId)",
                Returns = "AudioEvent",
                Kind = FmodFormKind.Catalogue,
                Title = "Get a minigame's track",
                When = "Only MinigameController needs this, and it already calls it.",
                Snippet = "AudioHandle bed = GameAudio.Loop(Sfx.MusicFor(Id));",
                Notes = "An id nothing has been scored for comes back invalid, and playing an " +
                        "invalid event does nothing — a new minigame starts silent rather than " +
                        "failing to start. The test suite is where a missing track is loud.",
            },
        };

        public static readonly IReadOnlyList<FmodGlobalFacts> Globals = new[]
        {
            new FmodGlobalFacts
            {
                Name = Global.Minigame,
                Range = "Labelled — the MinigameId names",
                DrivenBy = "MinigameController.Initialize, once per round.",
            },
            new FmodGlobalFacts
            {
                Name = Global.RoundPhase,
                Range = "Labelled — Briefing / Countdown / Playing / Settling / Finished",
                DrivenBy = "Every phase change. A snapshot can duck the bed under the rules card " +
                           "and open it on GO with no code change.",
            },
            new FmodGlobalFacts
            {
                Name = Global.RoundProgress,
                Range = "0 - 1",
                DrivenBy = "Elapsed fraction of the clock, per frame.",
            },
            new FmodGlobalFacts
            {
                Name = Global.TimeRemaining,
                Range = "0 - " + Global.LongestRound + " seconds",
                DrivenBy = "Seconds left, unnormalised, for anything that has to tick.",
            },
            new FmodGlobalFacts
            {
                Name = Global.PlayerMotion,
                Range = "0 - 1",
                DrivenBy = "The minigame's own SampleTelemetry. Usually the faster of the two seats.",
            },
            new FmodGlobalFacts
            {
                Name = Global.ChefMotion,
                Range = "0 - 1",
                DrivenBy = "The minigame's own SampleTelemetry.",
            },
            new FmodGlobalFacts
            {
                Name = Global.Objective,
                Range = "0 - 1",
                DrivenBy = "The minigame's own SampleTelemetry — hits landed, pearls popped, " +
                           "how boxed in Chef Tako is. Several games read naturally as tension.",
            },
            new FmodGlobalFacts
            {
                Name = Global.Paused,
                Range = "0 or 1",
                DrivenBy = "PauseController, whenever the buses are held or let go. Follows the " +
                           "held state rather than the menu being up, so the audio settings " +
                           "screen opened over a pause does not duck the mix it is auditioning.",
            },
            new FmodGlobalFacts
            {
                Name = Global.Coins,
                Range = "Unnormalised count",
                DrivenBy = "BoardAudioReporter, per frame. The two octopuses' coins together — " +
                           "Chef Tako's pile is deliberately not in it.",
            },
            new FmodGlobalFacts
            {
                Name = Global.CoinSwing,
                Range = "Signed count, usually 0",
                DrivenBy = "BoardAudioReporter. A spike rather than a level: the change since " +
                           "the last frame, so it is zero except on the frame a plate paid out " +
                           "or Chef Tako took a cut.",
            },
            new FmodGlobalFacts
            {
                Name = Global.BoardRound,
                Range = "1 - " + Global.BoardRounds,
                DrivenBy = "BoardAudioReporter. Which round of the session is being played.",
            },
            new FmodGlobalFacts
            {
                Name = Global.BoardProgress,
                Range = "0 - 1",
                DrivenBy = "BoardAudioReporter. How far through the session, for a bed that " +
                           "tightens as the ring runs out of rounds.",
            },
            new FmodGlobalFacts
            {
                Name = Global.Difficulty,
                Range = "Labelled — the CpuSkill names",
                DrivenBy = "BoardAudioReporter, from the live setup. How hard Chef Tako is playing.",
            },
            new FmodGlobalFacts
            {
                Name = Global.HumanCount,
                Range = "1 or 2",
                DrivenBy = "BoardAudioReporter. Whether anybody is sitting in the second chair.",
            },
        };

        public static IEnumerable<FmodApiForm> FormsFor(FmodEventFacts facts)
        {
            if (facts == null)
            {
                yield break;
            }

            if (facts.Loops)
            {
                yield return Form("Loop(AudioEvent)");
                yield return Form("LoopAt(AudioEvent, Vector3)");

                if (facts.Parameter != null)
                {
                    yield return Form("SetParameter(AudioHandle, string, float, bool)");
                }

                if (facts.Spatial)
                {
                    yield return Form("SetPosition(AudioHandle, Vector3)");
                }

                yield return Form("Stop(ref AudioHandle)");
                yield break;
            }

            if (facts.Spatial)
            {
                yield return Form("PlayAt(AudioEvent, Vector3)");
                yield return Form("Play(AudioEvent)");
            }
            else
            {
                yield return Form("Play(AudioEvent)");
                yield return Form("PlayAt(AudioEvent, Vector3)");
            }

            if (facts.Parameter != null)
            {
                yield return Form("Play(AudioEvent, string, float)");
                yield return Form("PlayAt(AudioEvent, Vector3, string, float)");
            }
        }

        private static FmodApiForm Form(string signature)
        {
            for (int i = 0; i < Forms.Count; i++)
            {
                if (Forms[i].Signature == signature)
                {
                    return Forms[i];
                }
            }

            throw new InvalidOperationException(
                "No form for " + signature + ". FormsFor and Forms have drifted apart.");
        }

        public static string ParameterConstant(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (constantsByValue == null)
                {
                    constantsByValue = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (FieldInfo field in typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (field.IsLiteral && field.FieldType == typeof(string))
                        {
                            constantsByValue[(string)field.GetValue(null)] = field.Name;
                        }
                    }
                }

                if (constantsByValue.TryGetValue(value, out string constant))
                {
                    return constant;
                }
            }

            return nameof(Sfx.StepParameter);
        }

        private static Dictionary<string, string> constantsByValue;

        public static FmodCheatSheet FromProject()
        {
            string root = UnityEngine.Application.dataPath + "/" + ScriptFolder;
            List<FmodSourceFile> sources = new List<FmodSourceFile>();

            if (System.IO.Directory.Exists(root))
            {
                foreach (string path in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
                {
                    sources.Add(new FmodSourceFile(path, System.IO.File.ReadAllText(path)));
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[SushiParty] No scripts at " + root + " — the cheat sheet has nothing to read.");
            }

            return Build(sources);
        }

        public static FmodCheatSheet Build(IReadOnlyList<FmodSourceFile> sources)
        {
            Dictionary<string, FmodEventFacts> byName = new Dictionary<string, FmodEventFacts>(StringComparer.Ordinal);
            Dictionary<string, string> documented = new Dictionary<string, string>(StringComparer.Ordinal);
            List<FmodEventFacts> ordered = new List<FmodEventFacts>();

            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].Name != CatalogueFileName)
                {
                    continue;
                }

                ReadCatalogue(sources[i], byName, documented, ordered);
            }

            Dictionary<string, List<FmodCallSite>> sites =
                new Dictionary<string, List<FmodCallSite>>(StringComparer.Ordinal);

            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].Name == CatalogueFileName || IsExample(sources[i].Name))
                {
                    continue;
                }

                ReadCallSites(sources[i], byName, sites);
            }

            ApplyMusicSelector(byName, sites);

            for (int i = 0; i < ordered.Count; i++)
            {
                FmodEventFacts facts = ordered[i];

                if (facts.Parameter == null && documented.TryGetValue(facts.Name, out string fromDoc))
                {
                    facts.Parameter = fromDoc;
                }

                if (facts.IsMusic)
                {
                    facts.Spatial = false;
                }

                if (sites.TryGetValue(facts.Name, out List<FmodCallSite> found))
                {
                    facts.CallSites = found;
                }
            }

            return new FmodCheatSheet(ordered);
        }

        private static bool IsExample(string fileName)
        {
            for (int i = 0; i < NotCallSites.Count; i++)
            {
                if (NotCallSites[i] == fileName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ReadCatalogue(
            FmodSourceFile source,
            Dictionary<string, FmodEventFacts> byName,
            Dictionary<string, string> documented,
            List<FmodEventFacts> ordered)
        {
            string[] lines = source.Text.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                Match declaration = Declaration.Match(lines[i]);
                if (!declaration.Success)
                {
                    continue;
                }

                string name = declaration.Groups["name"].Value;
                bool music = declaration.Groups["factory"].Value == "M";
                string path = (music ? MusicRoot : SfxRoot) + declaration.Groups["path"].Value;

                FmodEventFacts facts = new FmodEventFacts
                {
                    Name = name,
                    Path = path,
                    IsMusic = music,
                    Category = CategoryOf(path, music),
                };

                byName[name] = facts;
                ordered.Add(facts);

                string parameter = DocumentedParameter(lines, i);
                if (parameter != null)
                {
                    documented[name] = parameter;
                }
            }
        }

        private static string DocumentedParameter(string[] lines, int declarationLine)
        {
            for (int i = declarationLine - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("///", StringComparison.Ordinal))
                {
                    return null;
                }

                Match found = DocParameter.Match(line);
                if (found.Success)
                {
                    return ParameterValue(found.Groups["name"].Value);
                }
            }

            return null;
        }

        private static void ReadCallSites(
            FmodSourceFile source,
            Dictionary<string, FmodEventFacts> byName,
            Dictionary<string, List<FmodCallSite>> sites)
        {
            string text = source.Text;

            foreach (Match reference in SfxReference.Matches(text))
            {
                string name = reference.Groups["name"].Value;
                bool selector = name == MusicSelector;

                if (!selector && !byName.ContainsKey(name))
                {
                    continue;
                }

                string statement = StatementAround(text, reference.Index, reference.Index + reference.Length);
                if (statement.IndexOf("GameAudio.", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                Append(sites, name, new FmodCallSite
                {
                    File = source.Name,
                    Line = LineOf(text, reference.Index),
                    Statement = Whitespace.Replace(statement.Trim(), " "),
                });

                if (!selector)
                {
                    ApplyStatement(byName[name], statement);
                }
            }
        }

        private static void ApplyMusicSelector(
            Dictionary<string, FmodEventFacts> byName,
            Dictionary<string, List<FmodCallSite>> sites)
        {
            HashSet<string> selectable = new HashSet<string>(StringComparer.Ordinal);

            foreach (MinigameId id in Enum.GetValues(typeof(MinigameId)))
            {
                AudioEvent track = Sfx.MusicFor(id);
                if (!track.IsValid)
                {
                    continue;
                }

                foreach (KeyValuePair<string, FmodEventFacts> pair in byName)
                {
                    if (pair.Value.Path == track.Path)
                    {
                        selectable.Add(pair.Key);
                    }
                }
            }

            sites.TryGetValue(MusicSelector, out List<FmodCallSite> selectorSites);
            sites.Remove(MusicSelector);

            foreach (string name in selectable)
            {
                FmodEventFacts facts = byName[name];
                facts.Loops = true;

                if (selectorSites == null)
                {
                    continue;
                }

                facts.Used = true;

                for (int i = 0; i < selectorSites.Count; i++)
                {
                    Append(sites, name, selectorSites[i]);
                }
            }
        }

        private static void ApplyStatement(FmodEventFacts facts, string statement)
        {
            facts.Used = true;

            if (Positioned.IsMatch(statement))
            {
                facts.Spatial = true;
            }

            if (Looping.IsMatch(statement))
            {
                facts.Loops = true;
            }

            Match parameter = ParameterAtCallSite.Match(statement);
            if (parameter.Success)
            {
                string value = ParameterValue(parameter.Groups["name"].Value);
                if (value != null)
                {
                    facts.Parameter = value;
                }
            }
        }

        private static void Append(
            Dictionary<string, List<FmodCallSite>> sites, string name, FmodCallSite site)
        {
            if (!sites.TryGetValue(name, out List<FmodCallSite> list))
            {
                list = new List<FmodCallSite>();
                sites[name] = list;
            }

            list.Add(site);
        }

        private static string StatementAround(string text, int start, int end)
        {
            int open = Math.Max(
                text.LastIndexOf(';', Math.Max(0, start - 1)),
                Math.Max(
                    text.LastIndexOf('{', Math.Max(0, start - 1)),
                    text.LastIndexOf('}', Math.Max(0, start - 1))));

            int close = text.IndexOf(';', end);
            if (close < 0)
            {
                close = end;
            }

            return text.Substring(open + 1, close - open - 1);
        }

        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        private static string ParameterValue(string constantName)
        {
            FieldInfo field = typeof(Sfx).GetField(constantName, BindingFlags.Public | BindingFlags.Static);

            return field != null && field.IsLiteral && field.FieldType == typeof(string)
                ? (string)field.GetValue(null)
                : null;
        }

        private static string CategoryOf(string path, bool music)
        {
            string root = music ? MusicRoot : SfxRoot;
            string tail = path.StartsWith(root, StringComparison.Ordinal)
                ? path.Substring(root.Length)
                : path;

            int slash = tail.IndexOf('/');
            string group = slash < 0 ? tail : tail.Substring(0, slash);

            if (string.IsNullOrEmpty(group))
            {
                return "Uncategorised";
            }

            return music ? "Music/" + group : group;
        }
    }
}
