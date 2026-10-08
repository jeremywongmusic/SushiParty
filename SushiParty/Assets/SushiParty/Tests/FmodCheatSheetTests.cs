using NUnit.Framework;
using SushiParty.Audio;
using SushiParty.Core;
using SushiParty.EditorTools;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SushiParty.Tests
{
    public sealed class FmodCheatSheetTests
    {
        private static FmodCheatSheet project;
        private static FmodCheatSheet Project => project ?? (project = FmodCheatSheet.FromProject());

        [Test]
        public void Every_GameAudio_method_is_documented()
        {
            HashSet<string> documented = new HashSet<string>(
                FmodCheatSheet.Forms.Where(f => f.Owner == nameof(GameAudio)).Select(f => f.Signature));

            foreach (MethodInfo method in PublicStaticMethods(typeof(GameAudio)))
            {
                string signature = Canonical(method);

                Assert.That(
                    documented,
                    Contains.Item(signature),
                    $"GameAudio.{signature} is a way to reach the audio system that the cheat " +
                    "sheet does not show. Add a form for it to FmodCheatSheet.Forms.");
            }
        }

        [Test]
        public void Every_documented_GameAudio_method_exists()
        {
            HashSet<string> real = new HashSet<string>(
                PublicStaticMethods(typeof(GameAudio)).Select(Canonical));

            foreach (FmodApiForm form in FmodCheatSheet.Forms)
            {
                if (form.Owner != nameof(GameAudio) || !form.Signature.Contains("("))
                {
                    continue;
                }

                Assert.That(
                    real,
                    Contains.Item(form.Signature),
                    $"The cheat sheet documents GameAudio.{form.Signature}, which does not " +
                    "exist. A snippet nobody can compile is worse than a missing one.");
            }
        }

        [Test]
        public void Every_GameAudio_property_is_documented()
        {
            HashSet<string> documented = new HashSet<string>(
                FmodCheatSheet.Forms.Where(f => f.Owner == nameof(GameAudio)).Select(f => f.Signature));

            foreach (PropertyInfo property in typeof(GameAudio)
                         .GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                Assert.That(
                    documented,
                    Contains.Item(property.Name),
                    $"GameAudio.{property.Name} is not on the cheat sheet.");
            }
        }

        [Test]
        public void Every_form_carries_a_snippet_that_names_its_owner()
        {
            foreach (FmodApiForm form in FmodCheatSheet.Forms)
            {
                Assert.That(form.Title, Is.Not.Null.And.Not.Empty, form.Signature);
                Assert.That(form.When, Is.Not.Null.And.Not.Empty, form.Signature);
                Assert.That(
                    form.Snippet,
                    Does.Contain(form.Owner),
                    $"The snippet for {form.Signature} never mentions {form.Owner}, so copying " +
                    "it would not compile where it lands.");
            }
        }

        [Test]
        public void Snippets_take_the_event_they_are_given()
        {
            foreach (FmodApiForm form in FmodCheatSheet.Forms)
            {
                bool namesAnEvent = form.Snippet.Contains("{event}");
                string rendered = form.For("BoardDieLand", "Step");

                Assert.That(
                    rendered,
                    Does.Not.Contain("{event}").And.Not.Contain("{param}"),
                    $"{form.Signature} left a placeholder unfilled: {rendered}");

                if (namesAnEvent)
                {
                    Assert.That(
                        rendered,
                        Does.Contain("Sfx.BoardDieLand"),
                        $"{form.Signature} has a placeholder for an event but did not take the " +
                        "one it was handed.");
                }
            }
        }

        [Test]
        public void Every_global_parameter_is_documented()
        {
            foreach (FieldInfo field in typeof(Global)
                         .GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                string name = (string)field.GetValue(null);

                Assert.That(
                    FmodCheatSheet.Globals.Select(g => g.Name),
                    Contains.Item(name),
                    $"Global.{field.Name} is published every frame but the cheat sheet does " +
                    "not say what reads it.");
            }
        }

        [Test]
        public void A_PlayAt_call_site_makes_an_event_spatialised()
        {
            FmodEventFacts facts = ScanOne(
                "Thud",
                "GameAudio.PlayAt(Sfx.Thud, transform.position);");

            Assert.That(facts.Spatial, Is.True);
            Assert.That(facts.Loops, Is.False);
            Assert.That(facts.Used, Is.True);
        }

        [Test]
        public void A_PlayLabelledAt_call_site_also_makes_an_event_spatialised()
        {
            FmodEventFacts facts = ScanOne(
                "Thud",
                "GameAudio.PlayLabelledAt(Sfx.Thud, transform.position, Sfx.KindParameter, kind.ToString());");

            Assert.That(facts.Spatial, Is.True,
                "these are the rules the FMOD project is authored from, so a positional form " +
                "the scanner does not know about is an event built as 2D in Studio");
            Assert.That(facts.Parameter, Is.EqualTo("Kind"));
        }

        [Test]
        public void A_Play_call_site_leaves_an_event_unspatialised()
        {
            FmodEventFacts facts = ScanOne("Thud", "GameAudio.Play(Sfx.Thud);");

            Assert.That(facts.Spatial, Is.False);
            Assert.That(facts.Used, Is.True);
        }

        [Test]
        public void A_Loop_call_site_makes_an_event_loop()
        {
            FmodEventFacts facts = ScanOne("Engine", "handle = GameAudio.Loop(Sfx.Engine);");

            Assert.That(facts.Loops, Is.True);
            Assert.That(facts.Spatial, Is.False);
        }

        [Test]
        public void A_LoopAt_call_site_makes_an_event_both()
        {
            FmodEventFacts facts = ScanOne("Engine", "h = GameAudio.LoopAt(Sfx.Engine, car.position);");

            Assert.That(facts.Loops, Is.True);
            Assert.That(facts.Spatial, Is.True);
        }

        [Test]
        public void The_parameter_passed_at_a_call_site_is_the_events_parameter()
        {
            FmodEventFacts facts = ScanOne(
                "Tick",
                "GameAudio.Play(Sfx.Tick, Sfx.StepParameter, value);");

            Assert.That(facts.Parameter, Is.EqualTo(Sfx.StepParameter));
        }

        [Test]
        public void A_loops_parameter_falls_back_to_the_one_in_its_doc_comment()
        {
            FmodEventFacts facts = Scan(
                CatalogueSource(
                    "Engine",
                    "/// <summary>Looping. Uses <see cref=\"SpeedParameter\"/>.</summary>"),
                Source("Car.cs", "handle = GameAudio.Loop(Sfx.Engine);"))
                .Single();

            Assert.That(facts.Loops, Is.True);
            Assert.That(facts.Parameter, Is.EqualTo(Sfx.SpeedParameter));
        }

        [Test]
        public void An_event_nothing_calls_is_reported_as_unused()
        {
            FmodEventFacts facts = Scan(CatalogueSource("Forgotten", null)).Single();

            Assert.That(facts.Used, Is.False);
            Assert.That(facts.CallSites, Is.Empty);
        }

        [Test]
        public void A_call_site_records_where_it_was_found()
        {
            FmodEventFacts facts = Scan(
                CatalogueSource("Thud", null),
                Source("Wall.cs", "void Hit()\n{\n    GameAudio.Play(Sfx.Thud);\n}"))
                .Single();

            Assert.That(facts.CallSites, Has.Count.EqualTo(1));
            Assert.That(facts.CallSites[0].File, Is.EqualTo("Wall.cs"));
            Assert.That(facts.CallSites[0].Line, Is.EqualTo(3));
        }

        [Test]
        public void The_real_project_scans_to_the_whole_catalogue()
        {
            CollectionAssert.AreEquivalent(
                Sfx.AllNames.ToArray(),
                Project.Events.Select(e => e.Name).ToArray(),
                "The cheat sheet's catalogue and Sfx have drifted apart.");
        }

        [Test]
        public void The_real_project_agrees_with_the_documented_parameters()
        {
            AssertParameter("CountdownTick", Sfx.StepParameter);
            AssertParameter("BumperImpact", Sfx.ForceParameter);
            AssertParameter("BumperEngineLoop", Sfx.SpeedParameter);
            AssertParameter("ZoomChaseLoop", Sfx.ProximityParameter);
        }

        [Test]
        public void Music_is_never_spatialised_in_the_real_project()
        {
            foreach (FmodEventFacts facts in Project.Events.Where(e => e.IsMusic))
            {
                Assert.That(
                    facts.Spatial,
                    Is.False,
                    $"{facts.Name} is a track. Spatialising one pans the soundtrack round the " +
                    "arena as the camera moves.");
            }
        }

        [Test]
        public void Every_minigame_track_is_a_loop_in_the_real_project()
        {
            foreach (MinigameId id in System.Enum.GetValues(typeof(MinigameId)))
            {
                string path = Sfx.MusicFor(id).Path;
                FmodEventFacts facts = Project.Events.Single(e => e.Path == path);

                Assert.That(
                    facts.Loops,
                    Is.True,
                    $"{facts.Name} is handed out by Sfx.MusicFor, so it is a bed and has to " +
                    "loop. Authored as a one-shot it stops dead mid-round.");
            }
        }

        [Test]
        public void Forms_offered_for_an_event_match_what_it_is()
        {
            FmodEventFacts loop = Project.Events.Single(e => e.Name == "BumperEngineLoop");
            FmodEventFacts oneShot = Project.Events.Single(e => e.Name == "CountdownTick");

            Assert.That(FmodCheatSheet.FormsFor(loop).Select(f => f.Signature),
                Contains.Item("Loop(AudioEvent)"));
            Assert.That(FmodCheatSheet.FormsFor(loop).Select(f => f.Signature),
                Contains.Item("Stop(ref AudioHandle)"));

            Assert.That(FmodCheatSheet.FormsFor(oneShot).Select(f => f.Signature),
                Has.No.Member("Loop(AudioEvent)"));
            Assert.That(FmodCheatSheet.FormsFor(oneShot).Select(f => f.Signature),
                Contains.Item("Play(AudioEvent, string, float)"));
        }

        [Test]
        public void The_cheat_sheet_never_cites_itself_as_a_call_site()
        {
            foreach (FmodEventFacts facts in Project.Events)
            {
                foreach (FmodCallSite site in facts.CallSites)
                {
                    Assert.That(
                        FmodCheatSheet.NotCallSites,
                        Has.No.Member(site.File),
                        $"{facts.Name} is credited to {site.File}, which only quotes the API.");
                }
            }
        }

        [Test]
        public void Every_event_is_filed_under_a_category()
        {
            foreach (FmodEventFacts facts in Project.Events)
            {
                Assert.That(
                    facts.Category,
                    Is.Not.Null.And.Not.Empty,
                    $"{facts.Name} ({facts.Path}) landed in no group, so the window cannot list it.");
            }
        }

        private static void AssertParameter(string eventName, string expected)
        {
            FmodEventFacts facts = Project.Events.Single(e => e.Name == eventName);

            Assert.That(
                facts.Parameter,
                Is.EqualTo(expected),
                $"{eventName} should carry {expected}. Either a call site changed or the " +
                "doc comment in Sfx did.");
        }

        private static FmodEventFacts ScanOne(string eventName, string callSite)
        {
            return Scan(CatalogueSource(eventName, null), Source("Caller.cs", callSite)).Single();
        }

        private static IReadOnlyList<FmodEventFacts> Scan(params FmodSourceFile[] sources)
        {
            return FmodCheatSheet.Build(sources).Events;
        }

        private static FmodSourceFile CatalogueSource(string eventName, string docComment)
        {
            string doc = docComment == null ? string.Empty : "        " + docComment + "\n";

            return Source(
                FmodCheatSheet.CatalogueFileName,
                "public static class Sfx\n{\n" +
                doc +
                $"        public static readonly AudioEvent {eventName} = E(\"Test/{eventName}\");\n" +
                "}");
        }

        private static FmodSourceFile Source(string name, string text)
        {
            return new FmodSourceFile(name, text);
        }

        private static IEnumerable<MethodInfo> PublicStaticMethods(System.Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => !m.IsSpecialName);
        }

        private static string Canonical(MethodInfo method)
        {
            IEnumerable<string> parameters = method.GetParameters().Select(p =>
                p.ParameterType.IsByRef
                    ? "ref " + Simple(p.ParameterType.GetElementType())
                    : Simple(p.ParameterType));

            return method.Name + "(" + string.Join(", ", parameters) + ")";
        }

        private static string Simple(System.Type type)
        {
            if (type == typeof(float))
            {
                return "float";
            }

            if (type == typeof(string))
            {
                return "string";
            }

            if (type == typeof(bool))
            {
                return "bool";
            }

            if (type == typeof(int))
            {
                return "int";
            }

            return type.Name;
        }
    }
}
