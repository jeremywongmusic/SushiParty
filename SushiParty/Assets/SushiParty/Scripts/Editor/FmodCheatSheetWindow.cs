using SushiParty.Audio;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SushiParty.EditorTools
{
    public sealed class FmodCheatSheetWindow : EditorWindow
    {
        private enum Tab
        {
            HowToTrigger,
            Catalogue,
            Globals,
        }

        private static readonly string[] TabNames =
        {
            "How to trigger",
            "Catalogue",
            "Globals",
        };

        private static readonly FmodFormKind[] KindOrder =
        {
            FmodFormKind.OneShot,
            FmodFormKind.Loop,
            FmodFormKind.Global,
            FmodFormKind.Mixer,
            FmodFormKind.Catalogue,
            FmodFormKind.Diagnostics,
        };

        private static readonly Dictionary<FmodFormKind, string> KindTitles =
            new Dictionary<FmodFormKind, string>
            {
                { FmodFormKind.OneShot, "One-shots — it plays once and nobody holds anything" },
                { FmodFormKind.Loop, "Loops — you get a handle, and you owe it a Stop" },
                { FmodFormKind.Global, "Globals — state the whole mix can read" },
                { FmodFormKind.Mixer, "The mixer — buses and VCAs, rather than anything that plays" },
                { FmodFormKind.Catalogue, "The catalogue — getting from a name to an entry" },
                { FmodFormKind.Diagnostics, "Wiring and diagnostics" },
            };

        private const float ProseWidth = 760f;
        private const float GutterWidth = 300f;
        private const float CopyWidth = 54f;
        private FmodCheatSheet sheet;
        // Serialized so a recompile keeps the tab and the search box. The sheet itself is
        // rebuilt in OnEnable.
        [SerializeField] private Tab tab;
        [SerializeField] private string search = string.Empty;
        [SerializeField] private string formFilter = string.Empty;
        private string activeFilter = string.Empty;
        [SerializeField] private string selected;
        private Vector2 listScroll;
        private Vector2 bodyScroll;
        private Vector2 detailScroll;

        private readonly Dictionary<FmodFormKind, float> groupTops =
            new Dictionary<FmodFormKind, float>();

        private GUIStyle code;
        private GUIStyle wrapped;
        private GUIStyle heading;
        private GUIStyle badge;
        private GUIStyle groupHeading;
        private GUIStyle signature;
        private GUIStyle note;

        [MenuItem("SushiParty/FMOD Cheat Sheet")]
        public static void Open()
        {
            FmodCheatSheetWindow window = GetWindow<FmodCheatSheetWindow>("FMOD Cheat Sheet");
            window.minSize = new Vector2(720f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            sheet = FmodCheatSheet.FromProject();

            if (selected != null && sheet.Events.All(e => e.Name != selected))
            {
                selected = null;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (sheet == null || sheet.Events.Count == 0)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(
                    "No catalogue found. The cheat sheet reads Assets/" + FmodCheatSheet.ScriptFolder +
                    " for " + FmodCheatSheet.CatalogueFileName + " and the call sites around it. " +
                    "Press Rescan once the scripts are there.",
                    MessageType.Warning);
                return;
            }

            switch (tab)
            {
                case Tab.Catalogue:
                    DrawCatalogue();
                    break;
                case Tab.Globals:
                    DrawScrolled(DrawGlobals);
                    break;
                default:
                    DrawFormsTab();
                    break;
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                tab = (Tab)GUILayout.Toolbar((int)tab, TabNames, EditorStyles.toolbarButton, GUILayout.Width(320f));

                GUILayout.FlexibleSpace();

                GUILayout.Label(
                    GameAudio.IsLive
                        ? "FMOD is on (SUSHIPARTY_FMOD)"
                        : "FMOD is off — every call is a silent no-op",
                    EditorStyles.miniLabel);

                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    Rebuild();
                }
            }
        }

        private void DrawScrolled(System.Action body)
        {
            using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(bodyScroll))
            {
                bodyScroll = scroll.scrollPosition;

                EditorGUILayout.Space(4f);
                body();
                EditorGUILayout.Space(12f);
            }
        }

        private void DrawFormsTab()
        {
            activeFilter = formFilter ?? string.Empty;

            DrawFormFilter();
            DrawPinnedGroupHeader();

            using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(bodyScroll))
            {
                bodyScroll = scroll.scrollPosition;

                EditorGUILayout.Space(6f);
                DrawForms();
                EditorGUILayout.Space(24f);
            }
        }

        private void DrawFormFilter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                formFilter = EditorGUILayout.TextField(
                    formFilter ?? string.Empty,
                    EditorStyles.toolbarSearchField,
                    GUILayout.MaxWidth(ProseWidth));

                GUILayout.FlexibleSpace();

                GUILayout.Label(
                    string.IsNullOrEmpty(activeFilter)
                        ? string.Empty
                        : MatchingForms().Count + " of " + FmodCheatSheet.Forms.Count,
                    EditorStyles.miniLabel);
            }
        }

        private void DrawPinnedGroupHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(CurrentGroupTitle(), EditorStyles.miniBoldLabel);
            }
        }

        private string CurrentGroupTitle()
        {
            string current = string.Empty;

            foreach (FmodFormKind kind in KindOrder)
            {
                if (groupTops.TryGetValue(kind, out float top) && top <= bodyScroll.y + 6f)
                {
                    current = KindTitles.TryGetValue(kind, out string title) ? title : kind.ToString();
                }
            }

            return current;
        }

        private void DrawForms()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(ProseWidth)))
            {
                Paragraph(
                    "Every way into the audio system, and nothing else — the test suite holds this " +
                    "list to GameAudio in both directions, so it cannot fall behind the facade or " +
                    "get ahead of it. Snippets are shown with " +
                    (selected == null ? "an example event" : "Sfx." + selected) +
                    "; pick an entry on the Catalogue tab and they change to that one.");
            }

            FmodEventFacts facts = SelectedFacts();
            bool drewAnything = false;

            if (Event.current.type == EventType.Repaint)
            {
                groupTops.Clear();
            }

            foreach (FmodFormKind kind in KindOrder)
            {
                List<FmodApiForm> forms = FmodCheatSheet.Forms
                    .Where(f => f.Kind == kind && MatchesFilter(f))
                    .ToList();

                if (forms.Count == 0)
                {
                    continue;
                }

                drewAnything = true;
                DrawGroupHeading(kind);

                for (int i = 0; i < forms.Count; i++)
                {
                    DrawForm(forms[i], facts);

                    if (i < forms.Count - 1)
                    {
                        Divider();
                    }
                }
            }

            if (!drewAnything)
            {
                EditorGUILayout.Space(12f);
                EditorGUILayout.HelpBox(
                    "Nothing matches that. The filter reads the title, the description, the " +
                    "snippet and the notes, so try a word you would expect to find in any of " +
                    "them — or clear it to read the whole sheet.",
                    MessageType.Info);
            }
        }

        private void DrawGroupHeading(FmodFormKind kind)
        {
            EditorGUILayout.Space(22f);

            GUILayout.Label(
                KindTitles.TryGetValue(kind, out string title) ? title : kind.ToString(),
                groupHeading);

            if (Event.current.type == EventType.Repaint)
            {
                groupTops[kind] = GUILayoutUtility.GetLastRect().y;
            }

            Rule(0.35f);
            EditorGUILayout.Space(6f);
        }

        private void DrawForm(FmodApiForm form, FmodEventFacts facts)
        {
            EditorGUILayout.Space(10f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(ProseWidth)))
                {
                    GUILayout.Label(form.Title, EditorStyles.boldLabel);
                    Paragraph(form.When);

                    EditorGUILayout.Space(3f);
                    DrawSnippet(form.For(facts?.Name, facts?.Parameter), ProseWidth);

                    if (!string.IsNullOrEmpty(form.Notes))
                    {
                        EditorGUILayout.Space(3f);
                        Paragraph(form.Notes, note);
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(GutterWidth)))
                {
                    GUILayout.Label(form.Returns + " " + form.Owner + "." + form.Signature, signature);
                    GUILayout.FlexibleSpace();
                }
            }

            EditorGUILayout.Space(10f);
        }

        private bool MatchesFilter(FmodApiForm form)
        {
            if (string.IsNullOrWhiteSpace(activeFilter))
            {
                return true;
            }

            string needle = activeFilter.Trim();

            return Contains(form.Title, needle)
                   || Contains(form.When, needle)
                   || Contains(form.Notes, needle)
                   || Contains(form.Snippet, needle)
                   || Contains(form.Signature, needle)
                   || Contains(form.Owner, needle);
        }

        private List<FmodApiForm> MatchingForms()
        {
            return FmodCheatSheet.Forms.Where(MatchesFilter).ToList();
        }

        private static bool Contains(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack)
                   && haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Divider()
        {
            Rule(0.14f);
        }

        private static void Rule(float alpha)
        {
            Rect line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(0.5f, 0.5f, 0.5f, alpha));
        }

        private void DrawCatalogue()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawEventList();
                DrawSelectedEvent();
            }
        }

        private void DrawEventList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(260f)))
            {
                search = EditorGUILayout.TextField(search ?? string.Empty, EditorStyles.toolbarSearchField);

                using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(listScroll))
                {
                    listScroll = scroll.scrollPosition;

                    string category = null;

                    foreach (FmodEventFacts facts in sheet.Events)
                    {
                        if (!Matches(facts))
                        {
                            continue;
                        }

                        if (facts.Category != category)
                        {
                            category = facts.Category;
                            EditorGUILayout.Space(4f);
                            GUILayout.Label(category, EditorStyles.miniBoldLabel);
                        }

                        bool on = facts.Name == selected;
                        if (GUILayout.Toggle(on, Summary(facts), EditorStyles.miniButton) != on)
                        {
                            selected = facts.Name;
                            GUI.FocusControl(null);
                        }
                    }
                }
            }
        }

        private static string Summary(FmodEventFacts facts)
        {
            string marks = string.Empty;

            if (facts.Loops)
            {
                marks += " ∞";
            }

            if (facts.Spatial)
            {
                marks += " ◎";
            }

            if (facts.Parameter != null)
            {
                marks += " " + facts.Parameter;
            }

            return facts.Name + marks;
        }

        private bool Matches(FmodEventFacts facts)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            return facts.Name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0
                   || facts.Path.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawSelectedEvent()
        {
            using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(detailScroll))
            {
                detailScroll = scroll.scrollPosition;

                FmodEventFacts facts = SelectedFacts();
                if (facts == null)
                {
                    EditorGUILayout.Space(8f);
                    Paragraph(
                        "Pick an entry on the left. ∞ marks an event something loops, ◎ one " +
                        "something plays positioned, and a word is the parameter it carries. " +
                        "All three are read off the call sites rather than written down, so " +
                        "what this window says an event is, is what the FMOD project holds.");
                    return;
                }

                EditorGUILayout.Space(4f);
                GUILayout.Label(facts.Name, heading);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(facts.Path, GUILayout.Height(18f));
                    if (GUILayout.Button("Copy path", EditorStyles.miniButton, GUILayout.Width(80f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = facts.Path;
                    }
                }

                DrawBadges(facts);
                EditorGUILayout.Space(6f);

                GUILayout.Label("How to play this one", heading);

                foreach (FmodApiForm form in FmodCheatSheet.FormsFor(facts))
                {
                    DrawForm(form, facts);
                }

                DrawCallSites(facts);
            }
        }

        private void DrawBadges(FmodEventFacts facts)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Badge(facts.Loops ? "Looping" : "One-shot");
                Badge(facts.Spatial ? "Positioned" : "Not positioned");
                Badge(facts.Parameter == null ? "No parameter" : "Parameter: " + facts.Parameter);
                Badge(facts.IsMusic ? "Music bank" : "SFX bank");
                GUILayout.FlexibleSpace();
            }
        }

        private void Badge(string text)
        {
            GUILayout.Label(text, badge);
        }

        private void DrawCallSites(FmodEventFacts facts)
        {
            EditorGUILayout.Space(6f);
            GUILayout.Label("Where it is played", heading);

            if (facts.CallSites.Count == 0)
            {
                Paragraph(
                    "No line of C# names this one. That does not mean it is dead: an animation " +
                    "clip can fire any entry by name through CharacterAudioRelay, which resolves " +
                    "it with Sfx.TryFind and plays it positioned. If it is not on a clip either, " +
                    "it is an event the FMOD project will carry and nothing will ever trigger.",
                    EditorStyles.miniLabel);
                return;
            }

            foreach (FmodCallSite site in facts.CallSites)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(site.File + ":" + site.Line, EditorStyles.miniBoldLabel, GUILayout.Width(200f));
                    EditorGUILayout.SelectableLabel(site.Statement, code, GUILayout.Height(18f));
                }
            }
        }

        private void DrawGlobals()
        {
            Paragraph(
                "Seven parameters published on the Studio system every frame, separate from the " +
                "per-event ones. Any event, snapshot or bus can read them, so a bed can tighten " +
                "as the clock runs down or a snapshot can duck the mix on the results card with " +
                "no code change at all. Values that have not moved are dropped, so publishing " +
                "costs almost nothing.");

            foreach (FmodGlobalFacts global in FmodCheatSheet.Globals)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(global.Name, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(global.Range, EditorStyles.miniLabel);
                    }

                    Paragraph(global.DrivenBy);
                    DrawSnippet(SnippetFor(global));
                }
            }

            EditorGUILayout.Space(8f);
            GUILayout.Label("The three a minigame owns", heading);
            Paragraph(
                "PlayerMotion, ChefMotion and Objective come from each minigame's own reading of " +
                "itself. Override SampleTelemetry and the framework publishes them for you — " +
                "there is no reason for a minigame to call SetGlobal directly.");

            DrawSnippet(
                "protected override MinigameTelemetry SampleTelemetry()\n" +
                "{\n" +
                "    return new MinigameTelemetry\n" +
                "    {\n" +
                "        PlayerMotion = speed / TopSpeed,\n" +
                "        ChefMotion = chefSpeed / TopSpeed,\n" +
                "        Objective = hits / (float)HitsToWin,\n" +
                "    };\n" +
                "}");
        }

        private static string SnippetFor(FmodGlobalFacts global)
        {
            bool labelled = global.Range.StartsWith("Labelled", System.StringComparison.Ordinal);
            string constant = "Global." + ConstantFor(global.Name);

            return labelled
                ? "GameAudio.SetGlobalLabel(" + constant + ", value.ToString());"
                : "GameAudio.SetGlobal(" + constant + ", value);";
        }

        private static string ConstantFor(string name)
        {
            foreach (System.Reflection.FieldInfo field in typeof(Global)
                         .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.FieldType == typeof(string) && (string)field.GetValue(null) == name)
                {
                    return field.Name;
                }
            }

            return name;
        }

        private void DrawSnippet(string snippet, float maxWidth = 0f)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                float row = maxWidth > 0f ? maxWidth : position.width - 24f;
                float field = Mathf.Max(120f, row - CopyWidth - 6f);

                float height = code.CalcHeight(new GUIContent(snippet), field);

                EditorGUILayout.SelectableLabel(
                    snippet,
                    code,
                    GUILayout.Height(Mathf.Max(20f, height)),
                    GUILayout.MaxWidth(field));

                if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(CopyWidth), GUILayout.Height(20f)))
                {
                    EditorGUIUtility.systemCopyBuffer = snippet;
                }

                if (maxWidth > 0f)
                {
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void Paragraph(string text, GUIStyle style = null)
        {
            GUILayout.Label(text, style ?? wrapped);
        }

        private FmodEventFacts SelectedFacts()
        {
            return selected == null || sheet == null
                ? null
                : sheet.Events.FirstOrDefault(e => e.Name == selected);
        }

        private void EnsureStyles()
        {
            if (code != null)
            {
                return;
            }

            code = new GUIStyle(EditorStyles.textArea)
            {
                font = EditorStyles.miniFont,
                wordWrap = false,
                richText = false,
            };

            wrapped = new GUIStyle(EditorStyles.label) { wordWrap = true };
            heading = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

            groupHeading = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                margin = new RectOffset(0, 0, 0, 4),
            };

            signature = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperRight,
                normal = { textColor = Dimmed(EditorStyles.miniLabel.normal.textColor, 0.8f) },
            };

            note = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal = { textColor = Dimmed(EditorStyles.label.normal.textColor, 0.72f) },
            };

            badge = new GUIStyle(EditorStyles.miniLabel)
            {
                padding = new RectOffset(6, 6, 1, 1),
                margin = new RectOffset(0, 4, 2, 2),
            };
        }

        private static Color Dimmed(Color colour, float amount)
        {
            return new Color(colour.r, colour.g, colour.b, colour.a * amount);
        }
}
}
