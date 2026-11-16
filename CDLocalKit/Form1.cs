using System.Text.RegularExpressions;
using System.Text;
using Microsoft.Win32;
using System.Reflection;

namespace CDLocalKit;

public partial class Form1 : Form
{
    public static string app_lang;

    public sealed class LocalizationDocument
    {
        public string FilePath { get; private set; } = "";

        public string LanguageCode { get; set; } = "";

        public string LanguageName { get; set; } = "";

        public List<LocalizationSection> Sections { get; } = new();

        private readonly List<string> _lines = new();


        public int TotalKeys =>
            Sections.Sum(x => x.Keys.Count);


        public int LocalizedKeys =>
            Sections.Sum(
                x => x.Keys.Count(y => y.IsLocalized));

        internal void UpdateEntryIdLine(LocalizationEntry entry)
        {
            if (entry.IdLine < 0 ||
                entry.IdLine >= _lines.Count)
            {
                return;
            }

            string oldLine =
                _lines[entry.IdLine];

            int commentIndex =
                oldLine.IndexOf(
                    "\t#",
                    StringComparison.Ordinal);

            string suffix =
                commentIndex >= 0
                    ? oldLine[commentIndex..]
                    : "";

            _lines[entry.IdLine] =
                "ID   " +
                entry.Id +
                suffix;
        }

        private void RecalculateLineNumbers()
        {
            LocalizationEntry? currentEntry = null;

            LocalizationSection? currentSection = null;

            for (int i = 0; i < _lines.Count; i++)
            {
                string line = _lines[i];

                if (line.StartsWith(
                        "SECTION:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string sectionName =
                        GetValue(line, "SECTION:");

                    currentSection =
                        Sections.FirstOrDefault(
                            x =>
                                string.Equals(
                                    x.Name,
                                    sectionName,
                                    StringComparison.OrdinalIgnoreCase));

                    currentEntry = null;

                    continue;
                }

                if (currentSection == null)
                    continue;

                if (line.StartsWith(
                        "ID",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string id =
                        Regex.Replace(
                            line.Substring(2),
                            @"^\s+",
                            "")
                        .Trim();

                    currentEntry =
                        currentSection.Keys.FirstOrDefault(
                            x =>
                                string.Equals(
                                    x.Id,
                                    id,
                                    StringComparison.Ordinal));

                    if (currentEntry != null)
                        currentEntry.IdLine = i;

                    continue;
                }

                if (currentEntry == null)
                    continue;

                if (line.StartsWith(
                        "TEXT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentEntry.TextLine = i;
                }
            }
        }

        internal void RebuildAfterSectionMove()
        {
            var result = new List<string>();

            int firstSectionLine =
                _lines.FindIndex(
                    x =>
                        x.StartsWith(
                            "SECTION:",
                            StringComparison.OrdinalIgnoreCase));

            if (firstSectionLine < 0)
                firstSectionLine = _lines.Count;

            for (int i = 0; i < firstSectionLine; i++)
            {
                string line = _lines[i];

                if (line.StartsWith(
                        "Sections:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(
                        "Sections: " +
                        Sections.Count);

                    continue;
                }

                result.Add(line);
            }

            foreach (LocalizationSection section in Sections)
            {
                result.Add("");
                result.Add(
                    "#-------------------------------------------------------------------------------------");

                result.Add(
                    "SECTION: " +
                    section.Name);

                result.Add(
                    "Keys: " +
                    section.Keys.Count);

                result.Add("");

                foreach (LocalizationEntry entry in section.Keys)
                {
                    result.Add(
                        "ID   " +
                        entry.Id);

                    result.Add(
                        "DESC " +
                        entry.Description);

                    result.Add(
                        "TEXT " +
                        entry.Text);

                    result.Add("");
                }

                section.IsNew = false;

                foreach (LocalizationEntry entry in section.Keys)
                {
                    entry.IsNew = false;
                }
            }

            _lines.Clear();
            _lines.AddRange(result);

            RecalculateLineNumbers();
        }

        public static LocalizationDocument Load(string path)
        {
            string text =
                File.ReadAllText(
                    path,
                    Encoding.GetEncoding(1251));


            string[] lines =
                text
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Split('\n');


            var document =
                new LocalizationDocument
                {
                    FilePath = path
                };


            LocalizationSection? currentSection = null;

            LocalizationEntry? currentEntry = null;


            for (int i = 0;
                 i < lines.Length;
                 i++)
            {
                string line =
                    lines[i];

                if (line.StartsWith(
                        "Language:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    document.LanguageCode =
                        GetValue(
                            line,
                            "Language:");

                    continue;
                }

                if (line.StartsWith(
                        "LanguageName:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    document.LanguageName =
                        GetValue(
                            line,
                            "LanguageName:");

                    continue;
                }

                if (line.StartsWith(
                        "SECTION:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentSection =
                        new LocalizationSection
                        {
                            Name =
                                GetValue(
                                    line,
                                    "SECTION:")
                        };


                    document.Sections.Add(
                        currentSection);


                    currentEntry = null;

                    continue;
                }


                if (currentSection == null)
                    continue;

                if (line.StartsWith(
                        "ID",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string id =
                        Regex.Replace(
                            line.Substring(2),
                            @"^\s+",
                            "")
                        .Trim();


                    currentEntry =
                        new LocalizationEntry
                        {
                            Id = id,
                            IdLine = i
                        };


                    currentSection.Keys.Add(
                        currentEntry);

                    continue;
                }

                if (currentEntry == null)
                    continue;

                if (line.StartsWith(
                        "DESC",
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentEntry.Description =
                        GetValue(
                            line,
                            "DESC");

                    continue;
                }

                if (line.StartsWith(
                        "TEXT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentEntry.Text =
                        GetValue(
                            line,
                            "TEXT");

                    currentEntry.TextLine = i;

                    continue;
                }
            }


            document._lines.AddRange(
                lines);


            return document;
        }

        public void Save(string path)
        {
            foreach (LocalizationSection section in Sections)
            {
                foreach (LocalizationEntry entry in section.Keys)
                {
                    if (entry.TextLine < 0 ||
                        entry.TextLine >= _lines.Count)
                    {
                        continue;
                    }

                    string oldLine =
                        _lines[entry.TextLine];

                    int commentIndex =
                        oldLine.IndexOf(
                            "\t#",
                            StringComparison.Ordinal);

                    string suffix =
                        commentIndex >= 0
                            ? oldLine[commentIndex..]
                            : "";

                    _lines[entry.TextLine] =
                        "TEXT " +
                        entry.Text +
                        suffix;
                }
            }

            for (int i = 0;
                 i < _lines.Count;
                 i++)
            {
                if (_lines[i].StartsWith(
                        "Language:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    int commentIndex =
                        _lines[i].IndexOf(
                            "\t#",
                            StringComparison.Ordinal);

                    string suffix =
                        commentIndex >= 0
                            ? _lines[i][commentIndex..]
                            : "";

                    _lines[i] =
                        "Language: " +
                        LanguageCode +
                        suffix;

                    break;
                }
            }

            for (int i = 0;
                 i < _lines.Count;
                 i++)
            {
                if (_lines[i].StartsWith(
                        "LanguageName:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    int commentIndex =
                        _lines[i].IndexOf(
                            "\t#",
                            StringComparison.Ordinal);

                    string suffix =
                        commentIndex >= 0
                            ? _lines[i][commentIndex..]
                            : "";

                    _lines[i] =
                        "LanguageName: " +
                        LanguageName +
                        suffix;

                    break;
                }
            }

            RebuildForNewEntries();

            File.WriteAllText(
                path,
                string.Join(
                    "\r\n",
                    _lines),
                Encoding.GetEncoding(1251));

            FilePath = path;
        }

        private void RebuildForNewEntries()
        {
            bool hasNewData =
                Sections.Any(
                    s =>
                        s.IsNew ||
                        s.Keys.Any(
                            k => k.IsNew));

            if (!hasNewData)
                return;

            var result =
                new List<string>();

            int firstSectionLine =
                _lines.FindIndex(
                    x =>
                        x.StartsWith(
                            "SECTION:",
                            StringComparison.OrdinalIgnoreCase));

            if (firstSectionLine < 0)
                firstSectionLine = _lines.Count;

            for (int i = 0;
                 i < firstSectionLine;
                 i++)
            {
                string line =
                    _lines[i];

                if (line.StartsWith(
                        "Sections:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(
                        "Sections: " +
                        Sections.Count);

                    continue;
                }

                result.Add(line);
            }

            foreach (LocalizationSection section
                     in Sections)
            {
                result.Add("");
                result.Add(
                    "#-------------------------------------------------------------------------------------");

                result.Add(
                    "SECTION: " +
                    section.Name);

                result.Add(
                    "Keys: " +
                    section.Keys.Count);

                result.Add("");

                foreach (LocalizationEntry entry
                         in section.Keys)
                {
                    result.Add(
                        "ID   " +
                        entry.Id);

                    result.Add(
                        "DESC " +
                        entry.Description);

                    result.Add(
                        "TEXT " +
                        entry.Text);

                    result.Add("");
                }

                section.IsNew = false;

                foreach (LocalizationEntry entry
                         in section.Keys)
                {
                    entry.IsNew = false;
                }
            }

            _lines.Clear();

            _lines.AddRange(result);
        }

        private static string GetValue(string line, string prefix)
        {
            string value =
                line[prefix.Length..]
                .TrimStart();

            int commentIndex =
                value.IndexOf(
                    "\t#",
                    StringComparison.Ordinal);

            if (commentIndex >= 0)
            {
                value =
                    value[..commentIndex];
            }

            return value.TrimEnd();
        }

        public void Export(
            IEnumerable<LocalizationSection> sections,
            string path,
            string languageCode,
            string languageName)
        {
            var selected =
                sections.ToList();

            var lines =
                new List<string>();

            lines.Add(
                "# Crashday localized plain text | Made with CDLocalKit by St1ngLeR");

            lines.Add(
                "# ---------------------------------------------------------------------------------");

            lines.Add("");

            lines.Add(
                $"Language: {languageCode}\t# << Change this to your target language code");

            lines.Add(
                $"LanguageName: {languageName}\t# << Change this to your target language's name as it is spelled in that language");

            lines.Add(
                $"Sections: {selected.Count}");

            lines.Add("");

            foreach (LocalizationSection section
                     in selected)
            {
                lines.Add(
                    "#-------------------------------------------------------------------------------------");

                lines.Add(
                    $"SECTION: {section.Name}");

                lines.Add(
                    $"Keys: {section.Keys.Count}");

                lines.Add("");


                foreach (LocalizationEntry entry
                         in section.Keys)
                {
                    lines.Add(
                        $"ID   {entry.Id}");

                    lines.Add(
                        $"DESC {entry.Description}");

                    lines.Add(
                        $"TEXT {entry.Text}");

                    lines.Add("");
                }
            }

            File.WriteAllText(
                path,
                string.Join(
                    "\r\n",
                    lines),
                Encoding.GetEncoding(1251));
        }

        public MergeResult Merge(
            LocalizationDocument source,
            bool overwriteExisting)
        {
            var result =
                new MergeResult();

            foreach (LocalizationSection sourceSection
                     in source.Sections)
            {
                LocalizationSection? targetSection =
                    Sections.FirstOrDefault(
                        x =>
                            string.Equals(
                                x.Name,
                                sourceSection.Name,
                                StringComparison.OrdinalIgnoreCase));

                if (targetSection == null)
                {
                    targetSection =
                        new LocalizationSection
                        {
                            Name =
                                sourceSection.Name,

                            IsNew = true
                        };

                    foreach (LocalizationEntry sourceEntry
                             in sourceSection.Keys)
                    {
                        targetSection.Keys.Add(
                            CloneEntry(
                                sourceEntry,
                                true));


                        result.AddedKeys++;
                    }

                    Sections.Add(
                        targetSection);

                    result.AddedSections++;

                    continue;
                }

                foreach (LocalizationEntry sourceEntry
                         in sourceSection.Keys)
                {
                    LocalizationEntry? targetEntry =
                        targetSection.Keys.FirstOrDefault(
                            x =>
                                string.Equals(
                                    x.Id,
                                    sourceEntry.Id,
                                    StringComparison.OrdinalIgnoreCase));

                    if (targetEntry == null)
                    {
                        targetSection.Keys.Add(
                            CloneEntry(
                                sourceEntry,
                                true));

                        result.AddedKeys++;

                        continue;
                    }

                    result.ExistingKeys++;

                    if (overwriteExisting)
                    {
                        targetEntry.Text =
                            sourceEntry.Text;

                        if (string.IsNullOrWhiteSpace(
                                targetEntry.Description))
                        {
                            targetEntry.Description =
                                sourceEntry.Description;
                        }

                        result.UpdatedKeys++;
                    }
                }
            }

            return result;
        }

        private static LocalizationEntry CloneEntry(
            LocalizationEntry source,
            bool isNew)
        {
            return new LocalizationEntry
            {
                Id =
                    source.Id,

                Description =
                    source.Description,

                Text =
                    source.Text,

                IdLine = -1,

                TextLine = -1,

                IsNew = isNew
            };
        }

        public static LocalizationDocument CreateNew()
        {
            var document = new LocalizationDocument
            {
                FilePath = "",
                LanguageCode = "",
                LanguageName = ""
            };

            document._lines.Add(
                "# Crashday localized plain text | Made with CDLocalKit by St1ngLeR\"");

            document._lines.Add(
                "# ---------------------------------------------------------------------------------");

            document._lines.Add("");

            document._lines.Add(
                "Language: eng\t# << Change this to your target language code");

            document._lines.Add(
                "LanguageName: English\t# << Change this to your target language's name as it is spelled in that language");

            document._lines.Add(
                "Sections: 0");

            document._lines.Add("");

            return document;
        }
    }

    public sealed class LocalizationSection
    {
        public string Name { get; set; } = "";

        public bool IsNew { get; set; }

        public List<LocalizationEntry> Keys { get; } = new();

        public int LocalizedCount =>
            Keys.Count(
                x => x.IsLocalized);

        public int Percent
        {
            get
            {
                if (Keys.Count == 0)
                    return 100;

                return (int)Math.Round(
                    LocalizedCount * 100.0 /
                    Keys.Count);
            }
        }
    }

    public sealed class LocalizationEntry
    {
        public string Id { get; set; } = "";

        public string Description { get; set; } = "";

        public string Text { get; set; } = "";

        public int IdLine { get; set; } = -1;

        public int TextLine { get; set; } = -1;

        public bool IsNew { get; set; }


        public bool IsLocalized =>
            !string.IsNullOrWhiteSpace(Text);
    }

    public sealed class MergeResult
    {
        public int AddedSections { get; set; }

        public int AddedKeys { get; set; }

        public int ExistingKeys { get; set; }

        public int UpdatedKeys { get; set; }
    }

    private LocalizationDocument? _document;
    private LocalizationEntry? _selectedEntry;
    private bool _updatingControls;
    private bool _updatingChecks;

    private TreeView _tree = null!;
    private TextBox _languageName = null!;
    private TextBox _languageCode = null!;
    private TextBox _fileName = null!;
    private TextBox _identifier = null!;
    private TextBox _englishText = null!;
    private TextBox _localizedText = null!;
    private Label _statusLabel = null!;
    private bool _isModified = false;

    ToolStripItem TS_NewFile = null!;
    ToolStripItem TS_OpenFile = null!;
    ToolStripItem TS_SaveFile = null!;
    ToolStripItem TS_SaveAs = null!;
    ToolStripItem TS_ShowFindDialog = null!;
    ToolStripItem TS_OpenMergeDialog = null!;
    ToolStripItem TS_ShowReplaceDialog = null!;
    ToolStripItem TS_ExportSelected = null!;
    ToolStripItem TS_About = null!;
    ToolStripItem TS_CheckAll = null!;
    ToolStripItem TS_FindNextUnlocalized = null!;
    ToolStripItem TS_AddSection = null!;
    ToolStripItem TS_DeleteSelectedSections = null!;
    ToolStripItem TS_AddEntry = null!;
    ToolStripItem TS_DeleteSelectedIdentifiers = null!;

    private sealed class SearchResult
    {
        public LocalizationSection Section { get; init; } = null!;
        public LocalizationEntry? Entry { get; init; }

        public string Type { get; init; } = "";

        public string Text { get; init; } = "";
    }

    private List<SearchResult> FindAll(
    string searchText,
    bool matchCase = false)
    {
        var result = new List<SearchResult>();

        if (_document == null ||
            string.IsNullOrEmpty(searchText))
        {
            return result;
        }

        StringComparison comparison =
            matchCase
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

        foreach (LocalizationSection section in _document.Sections)
        {
            if (section.Name.Contains(
                    searchText,
                    comparison))
            {
                result.Add(
                    new SearchResult
                    {
                        Section = section,
                        Entry = null,
                        Type = "Section",
                        Text = section.Name
                    });
            }

            foreach (LocalizationEntry entry in section.Keys)
            {
                if (entry.Id.Contains(
                        searchText,
                        comparison))
                {
                    result.Add(
                        new SearchResult
                        {
                            Section = section,
                            Entry = entry,
                            Type = "Id",
                            Text = entry.Id
                        });
                }

                if (entry.Description.Contains(
                        searchText,
                        comparison))
                {
                    result.Add(
                        new SearchResult
                        {
                            Section = section,
                            Entry = entry,
                            Type = "Original",
                            Text = entry.Description
                        });
                }

                if (entry.Text.Contains(
                        searchText,
                        comparison))
                {
                    result.Add(
                        new SearchResult
                        {
                            Section = section,
                            Entry = entry,
                            Type = "Translated",
                            Text = entry.Text
                        });
                }
            }
        }

        return result;
    }

    private void SelectSearchResult(
    SearchResult result)
    {
        if (result.Entry != null)
        {
            SelectEntryInTree(result.Entry);
            return;
        }

        SelectSectionInTree(result.Section);
    }

    private void ShowFindDialog()
    {
        string str_find = "";
        string str_find2 = "";
        string str_case = "";
        string str_find3 = "";
        string str_close = "";

        if (app_lang == "en")
        {
            str_find = "Find";
            str_find2 = "Find:";
            str_case = "Case sensitive";
            str_find3 = "Find";
            str_close = "Close";
        }
        else if (app_lang == "ru")
        {
            str_find = "Поиск";
            str_find2 = "Найти:";
            str_case = "Учитывать регистр";
            str_find3 = "Найти";
            str_close = "Закрыть";
        }

        using var dialog = new Form
        {
            Text = str_find,
            Width = 500,
            Height = 180,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false
        };

        var label = new Label
        {
            Left = 12,
            Top = 15,
            Width = 70,
            Text = str_find2
        };

        var textBox = new TextBox
        {
            Left = 90,
            Top = 12,
            Width = 380
        };

        var checkCase = new CheckBox
        {
            Left = 90,
            Top = 45,
            Width = 180,
            Text = str_case
        };

        var info = new Label
        {
            Left = 90,
            Top = 72,
            Width = 380,
            Height = 25,
            Text = ""
        };

        var findButton = new Button
        {
            Text = str_find3,
            Left = 310,
            Top = 105,
            Width = 75
        };

        var closeButton = new Button
        {
            Text = str_close,
            Left = 395,
            Top = 105,
            Width = 75,
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.Add(label);
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(checkCase);
        dialog.Controls.Add(info);
        dialog.Controls.Add(findButton);
        dialog.Controls.Add(closeButton);

        dialog.CancelButton = closeButton;

        List<SearchResult> results = new();
        int currentIndex = -1;

        findButton.Click += (_, _) =>
        {
            string searchText = textBox.Text;

            if (string.IsNullOrEmpty(searchText))
                return;

            results = FindAll(
                searchText,
                checkCase.Checked);

            if (results.Count == 0)
            {
                if (app_lang == "en")
                {
                    info.Text = "No matches found.";
                }
                else if (app_lang == "ru")
                {
                    info.Text = "Совпадений не найдено.";
                }
                currentIndex = -1;
                return;
            }

            currentIndex++;

            if (currentIndex >= results.Count)
                currentIndex = 0;

            SearchResult result =
                results[currentIndex];

            SelectSearchResult(result);

            if (app_lang == "en")
            {
                info.Text =
                $"{currentIndex + 1} of {results.Count} — " +
                $"{result.Type}: {result.Text}";
            }
            else if (app_lang == "ru")
            {
                info.Text =
                $"{currentIndex + 1} из {results.Count} — " +
                $"{result.Type}: {result.Text}";
            }
        };

        dialog.Shown += (_, _) =>
        {
            textBox.Focus();
        };

        dialog.ShowDialog(this);
    }

    private void ShowReplaceDialog()
    {
        string str_replace = "";
        string str_find = "";
        string str_replace2 = "";
        string str_case = "";
        string str_findnext = "";
        string str_replace3 = "";
        string str_replaceall = "";
        string str_close = "";

        if (app_lang == "en")
        {
            str_replace = "Replace";
            str_find = "Find:";
            str_replace2 = "Replace:";
            str_case = "Case sensitive";
            str_findnext = "Find next";
            str_replace3 = "Replace";
            str_replaceall = "Replace all";
            str_close = "Close";
        }
        else if (app_lang == "ru")
        {
            str_replace = "Замена";
            str_find = "Найти:";
            str_replace2 = "Заменить:";
            str_case = "Учитывать регистр";
            str_findnext = "Найти далее";
            str_replace3 = "Заменить";
            str_replaceall = "Заменить все";
            str_close = "Закрыть";
        }

        using var dialog = new Form
            {
                Text = str_replace,
                Width = 540,
                Height = 220,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false
            };

        var findLabel = new Label
        {
            Left = 12,
            Top = 18,
            Width = 90,
            Text = str_find
        };

        var findBox = new TextBox
        {
            Left = 105,
            Top = 15,
            Width = 400
        };

        var replaceLabel = new Label
        {
            Left = 12,
            Top = 53,
            Width = 90,
            Text = str_replace2
        };

        var replaceBox = new TextBox
        {
            Left = 105,
            Top = 50,
            Width = 400
        };

        var checkCase = new CheckBox
        {
            Left = 105,
            Top = 82,
            Width = 180,
            Text = str_case
        };

        var info = new Label
        {
            Left = 105,
            Top = 110,
            Width = 400,
            Height = 25,
            Text = ""
        };

        var findNextButton = new Button
        {
            Text = str_findnext,
            Left = 105,
            Top = 145,
            Width = 100
        };

        var replaceButton = new Button
        {
            Text = str_replace3,
            Left = 215,
            Top = 145,
            Width = 100
        };

        var replaceAllButton = new Button
        {
            Text = str_replaceall,
            Left = 325,
            Top = 145,
            Width = 100
        };

        var closeButton = new Button
        {
            Text = str_close,
            Left = 430,
            Top = 145,
            Width = 75,
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.Add(findLabel);
        dialog.Controls.Add(findBox);
        dialog.Controls.Add(replaceLabel);
        dialog.Controls.Add(replaceBox);
        dialog.Controls.Add(checkCase);
        dialog.Controls.Add(info);
        dialog.Controls.Add(findNextButton);
        dialog.Controls.Add(replaceButton);
        dialog.Controls.Add(replaceAllButton);
        dialog.Controls.Add(closeButton);

        dialog.CancelButton = closeButton;

        List<SearchResult> results = new();
        int currentIndex = -1;

        void RefreshResults()
        {
            results = FindAll(
                findBox.Text,
                checkCase.Checked);

            if (results.Count == 0)
            {
                currentIndex = -1;
                if (app_lang == "en")
                {
                    info.Text = "No matches found.";
                }
                else if (app_lang == "ru")
                {
                    info.Text = "Совпадений не найдено.";
                }
                return;
            }

            if (currentIndex >= results.Count)
                currentIndex = results.Count - 1;
        }

        void ShowCurrentResult()
        {
            if (results.Count == 0 ||
                currentIndex < 0 ||
                currentIndex >= results.Count)
            {
                if (app_lang == "en")
                {
                    info.Text = "No matches found.";
                }
                else if (app_lang == "ru")
                {
                    info.Text = "Совпадений не найдено.";
                }
                return;
            }

            SearchResult result =
                results[currentIndex];

            SelectSearchResult(result);

            if (app_lang == "en")
            {
                info.Text =
                $"{currentIndex + 1} of {results.Count} — " +
                $"{result.Type}: {result.Text}";
            }
            else if (app_lang == "ru")
            {
                info.Text =
                $"{currentIndex + 1} из {results.Count} — " +
                $"{result.Type}: {result.Text}";
            }
        }

        // ------------------------------------------------------------
        // НАЙТИ ДАЛЕЕ
        // ------------------------------------------------------------

        findNextButton.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(findBox.Text))
                return;

            RefreshResults();

            if (results.Count == 0)
                return;

            currentIndex++;

            if (currentIndex >= results.Count)
                currentIndex = 0;

            ShowCurrentResult();
        };

        // ------------------------------------------------------------
        // ЗАМЕНИТЬ ОДНО
        // ------------------------------------------------------------

        replaceButton.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(findBox.Text))
                return;

            RefreshResults();

            if (results.Count == 0)
                return;

            if (currentIndex < 0)
                currentIndex = 0;

            SearchResult result =
                results[currentIndex];

            ReplaceSearchResult(
                result,
                findBox.Text,
                replaceBox.Text,
                checkCase.Checked);

            SetModified(true);

            BuildTree();
            UpdateUi();

            // После замены заново ищем результаты,
            // чтобы индексы оставались корректными.
            results = FindAll(
                findBox.Text,
                checkCase.Checked);

            if (results.Count == 0)
            {
                currentIndex = -1;

                if (app_lang == "en")
                {
                    info.Text = "There are no more matches.";
                }
                else if (app_lang == "ru")
                {
                    info.Text = "Совпадений больше нет.";
                }

                return;
            }

            if (currentIndex >= results.Count)
                currentIndex = 0;

            ShowCurrentResult();
        };

        // ------------------------------------------------------------
        // ЗАМЕНИТЬ ВСЕ
        // ------------------------------------------------------------

        replaceAllButton.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(findBox.Text))
                return;

            List<SearchResult> all =
                FindAll(
                    findBox.Text,
                    checkCase.Checked);

            if (all.Count == 0)
            {
                if (app_lang == "en")
                {
                    info.Text = "No matches found.";
                }
                else if (app_lang == "ru")
                {
                    info.Text = "Совпадений не найдено.";
                }
                
                return;
            }

            string dlg_title = "";
            string dlg_text = "";

            if (app_lang == "en")
            {
                dlg_title = "Replace all";
                dlg_text =
                $"Matches found: {all.Count}\n\n" +
                "Replace all found matches?";
            }
            else if (app_lang == "ru")
            {
                dlg_title = "Заменить все";
                dlg_text =
                $"Найдено совпадений: {all.Count}\n\n" +
                "Заменить все найденные совпадения?";
            }

            DialogResult confirmation = MessageBox.Show(dialog, dlg_text, dlg_title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmation != DialogResult.Yes)
                return;

            int replaced = 0;

            foreach (SearchResult result in all)
            {
                if (ReplaceSearchResult(
                        result,
                        findBox.Text,
                        replaceBox.Text,
                        checkCase.Checked))
                {
                    replaced++;
                }
            }

            SetModified(true);

            BuildTree();
            UpdateUi();

            results = FindAll(
                findBox.Text,
                checkCase.Checked);

            currentIndex = -1;

            if (app_lang == "en")
            {
                info.Text = $"Replaced: {replaced}";
            }
            else if (app_lang == "ru")
            {
                info.Text = $"Заменено: {replaced}";
            }
            
        };

        dialog.Shown += (_, _) =>
        {
            findBox.Focus();
        };

        dialog.ShowDialog(this);
    }

    private bool ReplaceSearchResult(
    SearchResult result,
    string searchText,
    string replaceText,
    bool matchCase)
    {
        if (result.Entry == null)
        {
            // --------------------------------------------------------
            // SECTION
            // --------------------------------------------------------

            if (result.Type == "Section")
            {
                string oldValue = result.Section.Name;

                string newValue =
                    ReplaceText(
                        oldValue,
                        searchText,
                        replaceText,
                        matchCase);

                if (oldValue == newValue)
                    return false;

                // Не допускаем создание двух одинаковых секций.
                LocalizationSection? duplicate =
                    _document?.Sections.FirstOrDefault(
                        x =>
                            !ReferenceEquals(x, result.Section) &&
                            string.Equals(
                                x.Name,
                                newValue,
                                StringComparison.OrdinalIgnoreCase));

                if (duplicate != null)
                {
                    if (app_lang == "en")
                    {
                        MessageBox.Show(this, $"Section \"{newValue}\" already exists.", "Replace", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else if (app_lang == "ru")
                    {
                        MessageBox.Show(this, $"Секция \"{newValue}\" уже существует.", "Замена", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    return false;
                }

                result.Section.Name = newValue;

                if (_document != null)
                    _document.RebuildAfterSectionMove();

                return true;
            }

            return false;
        }

        LocalizationEntry entry =
            result.Entry;

        // ------------------------------------------------------------
        // ID
        // ------------------------------------------------------------

        if (result.Type == "Id")
        {
            string oldValue = entry.Id;

            string newValue =
                ReplaceText(
                    oldValue,
                    searchText,
                    replaceText,
                    matchCase);

            if (oldValue == newValue)
                return false;

            LocalizationEntry? duplicate =
                _document?
                    .Sections
                    .SelectMany(x => x.Keys)
                    .FirstOrDefault(
                        x =>
                            !ReferenceEquals(x, entry) &&
                            string.Equals(
                                x.Id,
                                newValue,
                                StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                if (app_lang == "en")
                {
                    MessageBox.Show(this, $"Identifier \"{newValue}\" already exists.", "Replace", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (app_lang == "ru")
                {
                    MessageBox.Show(this, $"Идентификатор \"{newValue}\" уже существует.", "Замена", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return false;
            }

            entry.Id = newValue;

            if (_document != null)
                _document.UpdateEntryIdLine(entry);

            return true;
        }

        // ------------------------------------------------------------
        // ORIGINAL TEXT
        // ------------------------------------------------------------

        if (result.Type == "Original")
        {
            string oldValue =
                entry.Description;

            string newValue =
                ReplaceText(
                    oldValue,
                    searchText,
                    replaceText,
                    matchCase);

            if (oldValue == newValue)
                return false;

            entry.Description = newValue;

            return true;
        }

        // ------------------------------------------------------------
        // TRANSLATED TEXT
        // ------------------------------------------------------------

        if (result.Type == "Translated")
        {
            string oldValue =
                entry.Text;

            string newValue =
                ReplaceText(
                    oldValue,
                    searchText,
                    replaceText,
                    matchCase);

            if (oldValue == newValue)
                return false;

            entry.Text = newValue;

            return true;
        }

        return false;
    }

    private static string ReplaceText(
    string source,
    string searchText,
    string replaceText,
    bool matchCase)
    {
        if (string.IsNullOrEmpty(searchText))
            return source;

        return source.Replace(
            searchText,
            replaceText,
            matchCase
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase);
    }

    string regkey_path = "HKEY_CURRENT_USER\\Software\\CDLocalKit";
    string regkey_val = "lang";

    public Form1()
    {
        InitializeComponent();

        SetModified(false);

        _tree = treeView1;
        _languageCode = textBox1;
        _languageName = textBox2;
        _fileName = textBox4;
        _identifier = textBox6;
        _englishText = textBox3;
        _localizedText = textBox5;
        _statusLabel = label3;

        AllowDrop = true;
        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;

        _fileName.KeyDown += TextBox6_KeyDown;
        _identifier.KeyDown += TextBox7_KeyDown;

        _fileName.Leave += TextBox6_Leave;
        _identifier.Leave += TextBox7_Leave;

        treeView1.AfterSelect += TreeAfterSelect;
        treeView1.AfterCheck += TreeAfterCheck;
        treeView1.KeyDown += TreeKeyDown;
        textBox1.TextChanged += (_, _) =>
        {
            if (_document != null && !_updatingControls)
            {
                _document.LanguageCode = textBox1.Text;
                SetModified(true);
            }
        };
        textBox2.TextChanged += (_, _) =>
        {
            if (_document != null && !_updatingControls)
            {
                _document.LanguageName = textBox2.Text;
                SetModified(true);
            }
        };
        textBox5.TextChanged += (_, _) =>
        {
            if (_selectedEntry != null && !_updatingControls)
            {
                _selectedEntry.Text = textBox5.Text;

                SetModified(true);

                UpdateEntryNode(_selectedEntry);
                UpdateRootText();
                UpdateStatus();
            }
        };

        newToolStripMenuItem.Click += (_, _) => NewFile();
        openToolStripMenuItem.Click += (_, _) => OpenFile();
        saveToolStripMenuItem.Click += (_, _) => SaveFile();
        saveAsToolStripMenuItem.Click += (_, _) => SaveAs();
        exportSelectedToolStripMenuItem.Click += (_, _) => ExportSelected();
        mergePlainToolStripMenuItem.Click += (_, _) => OpenMergeDialog();
        exitToolStripMenuItem.Click += (_, _) => Close();
        findNextUnlocalizedToolStripMenuItem.Click += (_, _) => FindNextUnlocalized();
        checkAllToolStripMenuItem.Click += (_, _) => CheckAll();
        addNewSectionToolStripMenuItem.Click += (_, _) => AddSection();
        deleteSelectedSectionsToolStripMenuItem.Click += (_, _) => DeleteSelectedSections();
        addNewIdentifierToolStripMenuItem.Click += (_, _) => AddEntry();
        deletedSelectedIdentifiersToolStripMenuItem.Click += (_, _) => DeleteSelectedIdentifiers();
        aboutToolStripMenuItem.Click += (_, _) => About();
        findToolStripMenuItem1.Click += (_, _) => ShowFindDialog();
        replaceToolStripMenuItem1.Click += (_, _) => ShowReplaceDialog();
        englishToolStripMenuItem.Click += (_, _) => SwitchToEnglish();
        русскийRussianToolStripMenuItem.Click += (_, _) => SwitchToRussian();

        TS_NewFile = toolStripButton1;
        TS_OpenFile = toolStripButton2;
        TS_SaveFile = toolStripButton3;
        TS_SaveAs = toolStripButton4;
        TS_ShowFindDialog = toolStripButton5;
        TS_OpenMergeDialog = toolStripButton6;
        TS_ShowReplaceDialog = toolStripButton7;
        TS_ExportSelected = toolStripButton8;
        TS_About = toolStripButton9;
        TS_CheckAll = toolStripButton10;
        TS_FindNextUnlocalized = toolStripButton11;
        TS_AddSection = toolStripButton12;
        TS_DeleteSelectedSections = toolStripButton13;
        TS_AddEntry = toolStripButton14;
        TS_DeleteSelectedIdentifiers = toolStripButton15;

        TS_NewFile.Click += (_, _) => NewFile();
        TS_OpenFile.Click += (_, _) => OpenFile();
        TS_SaveFile.Click += (_, _) => SaveFile();
        TS_SaveAs.Click += (_, _) => SaveAs();
        TS_OpenMergeDialog.Click += (_, _) => OpenMergeDialog();
        TS_ExportSelected.Click += (_, _) => ExportSelected();
        TS_FindNextUnlocalized.Click += (_, _) => FindNextUnlocalized();
        TS_CheckAll.Click += (_, _) => CheckAll();
        TS_About.Click += (_, _) => About();
        TS_AddSection.Click += (_, _) => AddSection();
        TS_DeleteSelectedSections.Click += (_, _) => DeleteSelectedSections();
        TS_AddEntry.Click += (_, _) => AddEntry();
        TS_DeleteSelectedIdentifiers.Click += (_, _) => DeleteSelectedIdentifiers();
        TS_ShowFindDialog.Click += (_, _) => ShowFindDialog();
        TS_ShowReplaceDialog.Click += (_, _) => ShowReplaceDialog();

        button1.Click += (_, _) => ExpandList();
        button2.Click += (_, _) => CollapseList();

        Shown += Form1_Shown;

        UpdateUi();
        UpdateLoc();
    }

    private void Form1_Shown(object? sender, EventArgs e)
    {
        OpenCommandLineFiles();
    }

    public static DateTime GetBuildDate()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var attribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                                .FirstOrDefault(a => a.Key == "BuildDate");

        if (attribute != null && DateTime.TryParse(attribute.Value, out DateTime result))
        {
            return result;
        }

        return DateTime.MinValue;
    }

    private void About()
    {
        if (app_lang == "en")
        {
            MessageBox.Show(this, $"Crashday Localization Toolkit\n\nAuthor: St1ngLeR | The build was compiled on {GetBuildDate()}", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else if (app_lang == "ru")
        {
            MessageBox.Show(this, $"Инструментарий локализации Crashday\n\nАвтор: St1ngLeR | Программа собрана {GetBuildDate()}", "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
    }

    // ========================================================
    // DRAG ENTER
    // ========================================================

    private void MainForm_DragEnter(
        object? sender,
        DragEventArgs e)
    {
        if (!e.Data!.GetDataPresent(
                DataFormats.FileDrop))
        {
            e.Effect =
                DragDropEffects.None;

            return;
        }


        string[] files =
            (string[])e.Data.GetData(
                DataFormats.FileDrop)!;


        bool hasPlain =
            files.Any(
                x =>
                    string.Equals(
                        Path.GetExtension(x),
                        ".plain",
                        StringComparison.OrdinalIgnoreCase));


        e.Effect =
            hasPlain
                ? DragDropEffects.Copy
                : DragDropEffects.None;
    }


    // ========================================================
    // DRAG DROP
    // ========================================================

    // ========================================================
    // DRAG DROP
    // ========================================================

    private void MainForm_DragDrop(
        object? sender,
        DragEventArgs e)
    {
        if (!e.Data!.GetDataPresent(DataFormats.FileDrop))
            return;

        string[] files =
            (string[])e.Data.GetData(
                DataFormats.FileDrop)!;

        string[] plainFiles =
            files
                .Where(
                    x =>
                        string.Equals(
                            Path.GetExtension(x),
                            ".plain",
                            StringComparison.OrdinalIgnoreCase))
                .Where(File.Exists)
                .ToArray();

        if (plainFiles.Length == 0)
            return;

        // ----------------------------------------------------
        // Один файл — обычное поведение DnD.
        // ----------------------------------------------------

        if (plainFiles.Length == 1)
        {
            HandleDroppedFile(plainFiles[0]);
            return;
        }

        // ----------------------------------------------------
        // Несколько файлов — автоматически MERGE.
        // ----------------------------------------------------

        HandleDroppedMultipleFiles(plainFiles);
    }


    private void TreeAfterCheck(
    object? sender,
    TreeViewEventArgs e)
    {
        if (_updatingChecks)
            return;

        _updatingChecks = true;

        try
        {
            if (e.Node.Tag is LocalizationSection)
            {
                foreach (TreeNode child in e.Node.Nodes)
                {
                    child.Checked = e.Node.Checked;
                }
            }
        }
        finally
        {
            _updatingChecks = false;
        }
    }

    // ========================================================
    // CHECK ALL
    // ========================================================

    private void CheckAll()
    {
        _updatingChecks = true;

        try
        {
            // Если хотя бы один элемент не отмечен — ставим галочки всем.
            // Если все уже отмечены — снимаем галочки со всех.
            bool allChecked = _tree.Nodes
                .Cast<TreeNode>()
                .All(IsCheckedRecursive);

            bool newState = !allChecked;

            foreach (TreeNode root in _tree.Nodes)
            {
                SetCheckedRecursive(root, newState);
            }
        }
        finally
        {
            _updatingChecks = false;
        }
    }

    private bool IsCheckedRecursive(TreeNode node)
    {
        if (!node.Checked)
            return false;

        foreach (TreeNode child in node.Nodes)
        {
            if (!IsCheckedRecursive(child))
                return false;
        }

        return true;
    }


    private static void SetCheckedRecursive(
        TreeNode node,
        bool value)
    {
        node.Checked =
            value;


        foreach (TreeNode child
                 in node.Nodes)
        {
            SetCheckedRecursive(
                child,
                value);
        }
    }


    // ========================================================
    // OPEN
    // ========================================================

    private void OpenFile()
    {
        string dlg_title = "";
        string dlg_filter = "";

        if (app_lang == "en")
        {
            dlg_title = "Open Crashday localization file";
            dlg_filter = "Crashday localization (*.plain)|*.plain";
        }
        else if (app_lang == "ru")
        {
            dlg_title = "Открыть файл локализации Crashday";
            dlg_filter = "Локализация Crashday (*.plain)|*.plain";
        }

        using var dialog =
            new OpenFileDialog
            {
                Filter = dlg_filter,
                Title = dlg_title,
                Multiselect = true
            };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        string[] plainFiles =
            dialog.FileNames
                .Where(File.Exists)
                .Where(
                    x =>
                        string.Equals(
                            Path.GetExtension(x),
                            ".plain",
                            StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (plainFiles.Length == 0)
            return;

        // Полностью такое же поведение,
        // как при Drag'n'Drop.
        if (plainFiles.Length == 1)
        {
            HandleDroppedFile(plainFiles[0]);
            return;
        }

        HandleDroppedMultipleFiles(plainFiles);
    }


    // ========================================================
    // SAVE
    // ========================================================

    private void SaveFile()
    {
        if (_document == null)
            return;

        ApplyCurrent();

        // Новый файл ещё не имеет пути.
        if (string.IsNullOrWhiteSpace(
                _document.FilePath))
        {
            SaveAs();
            return;
        }

        try
        {
            _document.Save(
                _document.FilePath);

            SetModified(false);

            UpdateUi();
        }
        catch (Exception ex)
        {
            if (app_lang == "en")
            {
                ShowError("Error saving file", ex);
            }
            else if (app_lang == "ru")
            {
                ShowError("Ошибка сохранения файла", ex);
            }
        }
    }


    // ========================================================
    // SAVE AS
    // ========================================================

    private bool SaveAs()
    {
        if (_document == null)
            return false;


        ApplyCurrent();

        string dlg_filter = "";

        if (app_lang == "en")
        {
            dlg_filter = "Crashday localization (*.plain)|*.plain";
        }
        else if (app_lang == "ru")
        {
            dlg_filter = "Локализация Crashday (*.plain)|*.plain";
        }

        using var dialog =
            new SaveFileDialog
            {
                Filter = dlg_filter,

                FileName =
    string.IsNullOrWhiteSpace(_document.FilePath)
        ? ".plain"
        : Path.GetFileName(_document.FilePath)
            };


        if (dialog.ShowDialog(this)
    != DialogResult.OK)
        {
            return false;
        }


        try
        {
            _document.Save(
                dialog.FileName);

            SetModified(false);

            UpdateUi();

            return true;
        }
        catch (Exception ex)
        {
            if (app_lang == "en")
            {
                ShowError("Error saving file", ex);
            }
            else if (app_lang == "ru")
            {
                ShowError("Ошибка сохранения файла", ex);
            }

            return false;
        }
    }

    private void BuildTree()
    {
        _tree.BeginUpdate();


        try
        {
            _tree.Nodes.Clear();


            if (_document == null)
                return;


            var root =
                new TreeNode
                {
                    Text =
                        GetRootText(),

                    Tag =
                        _document
                };


            _tree.Nodes.Add(
                root);


            foreach (LocalizationSection section
                     in _document.Sections)
            {
                var sectionNode =
                    new TreeNode
                    {
                        Text =
                            GetSectionNodeText(
                                section),

                        Tag =
                            section,

                        Checked = false
                    };


                root.Nodes.Add(
                    sectionNode);


                foreach (LocalizationEntry entry
                         in section.Keys)
                {
                    sectionNode.Nodes.Add(
                        new TreeNode
                        {
                            Text =
                                GetEntryNodeText(
                                    entry),

                            Tag =
                                entry,

                            Checked = false
                        });
                }
            }


            root.Expand();
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    private string GetRootText()
    {
        if (_document == null)
        {
            if (app_lang == "en")
            {
                return "Plain text";
            }
            else if (app_lang == "ru")
            {
                return "Текст локализации";
            }
        }

        int total =
            _document.TotalKeys;


        int localized =
            _document.LocalizedKeys;


        int percent =
            total == 0
                ? 100
                : localized *
                  100 /
                  total;

        string str_root = "";

        if (app_lang == "en")
        {
            str_root = $"Plain text ({total} keywords, {percent}% localized)";
        }
        else if (app_lang == "ru")
        {
            str_root = $"Текст локализации ({total} ключевых слов, {percent}% переведено)";
        }

        return str_root;
    }


    // ========================================================
    // TREE TEXT
    // ========================================================

    private static string GetSectionNodeText(
        LocalizationSection section)
    {
        return
            $"{section.Name} ({section.Percent}%)";
    }


    private static string GetEntryNodeText(
        LocalizationEntry entry)
    {
        if (entry.IsLocalized)
            return entry.Id;


        string str_unlocalized = "";

        if (app_lang == "en")
        {
            str_unlocalized = $"{entry.Id} [UNLOCALIZED]";
        }
        else if (app_lang == "ru")
        {
            str_unlocalized = $"{entry.Id} [НЕ ПЕРЕВЕДЕНО]";
        }

        return str_unlocalized;
    }

    private void TreeAfterSelect(
        object? sender,
        TreeViewEventArgs e)
    {
        if (e.Node.Tag is LocalizationEntry entry)
        {
            SelectEntry(entry);
        }
        else
        {
            _selectedEntry = null;
            UpdateUi();
        }
    }


    private void SelectEntry(
        LocalizationEntry entry)
    {
        _selectedEntry = entry;

        _updatingControls = true;

        try
        {
            _identifier.Text =
                entry.Id;

            _englishText.Text =
                entry.Description;

            _localizedText.Text =
                entry.Text;

            if (_document != null)
            {
                LocalizationSection? section =
                    _document.Sections.FirstOrDefault(
                        x => x.Keys.Contains(entry));

                _fileName.Text =
                    section?.Name ?? "";
            }
            else
            {
                _fileName.Text = "";
            }

            UpdateUi();
        }
        finally
        {
            _updatingControls = false;
        }
    }

    private void UpdateEntryNode(
        LocalizationEntry entry)
    {
        if (_tree.Nodes.Count == 0)
            return;

        foreach (TreeNode sectionNode
                 in _tree.Nodes[0].Nodes)
        {
            foreach (TreeNode entryNode
                     in sectionNode.Nodes)
            {
                if (!ReferenceEquals(
                        entryNode.Tag,
                        entry))
                {
                    continue;
                }

                entryNode.Text =
                    GetEntryNodeText(
                        entry);

                if (sectionNode.Tag
                    is LocalizationSection section)
                {
                    sectionNode.Text =
                        GetSectionNodeText(
                            section);
                }

                UpdateRootText();

                return;
            }
        }
    }

    private void UpdateRootText()
    {
        if (_tree.Nodes.Count > 0)
        {
            _tree.Nodes[0].Text =
                GetRootText();
        }


        UpdateStatus();
    }

    private void ApplyCurrent()
    {
        if (_selectedEntry == null)
            return;


        _selectedEntry.Text =
            _localizedText.Text;


        UpdateEntryNode(
            _selectedEntry);
    }


    private void FindNextUnlocalized()
    {
        if (_document == null)
            return;


        List<LocalizationEntry> all =
            _document.Sections
                .SelectMany(
                    x => x.Keys)
                .ToList();


        if (all.Count == 0)
            return;


        int current =
            _selectedEntry == null
                ? -1
                : all.IndexOf(
                    _selectedEntry);


        for (int i = 1;
             i <= all.Count;
             i++)
        {
            LocalizationEntry candidate =
                all[
                    (current + i) %
                    all.Count];


            if (!candidate.IsLocalized)
            {
                SelectEntryInTree(
                    candidate);

                return;
            }
        }

        if (app_lang == "en")
        {
            MessageBox.Show(this, "All keywords are localized.", "Localization", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else if (app_lang == "ru")
        {
            MessageBox.Show(this, "Все ключевые слова переведены.", "Локализация", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }


    private void SelectEntryInTree(
        LocalizationEntry entry)
    {
        if (_tree.Nodes.Count == 0)
            return;


        foreach (TreeNode sectionNode
                 in _tree.Nodes[0].Nodes)
        {
            foreach (TreeNode entryNode
                     in sectionNode.Nodes)
            {
                if (!ReferenceEquals(
                        entryNode.Tag,
                        entry))
                {
                    continue;
                }


                sectionNode.Expand();


                _tree.SelectedNode =
                    entryNode;


                entryNode.EnsureVisible();


                return;
            }
        }
    }

    private void ExportSelected()
    {
        if (_document == null)
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Open the main localization file first.", "Merge", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Откройте файл локализации для начала.", "Объединение", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        ApplyCurrent();

        var sections =
            GetSelectedSections();

        if (sections.Count == 0)
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Select sections or keys using the checkboxes.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Выберите разделы или ключи с помощью чекбоксов.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        string dlg_title = "";
        string dlg_filter = "";

        if (app_lang == "en")
        {
            dlg_title = "Export checked localization";
            dlg_filter = "Crashday localization (*.plain)|*.plain";
        }
        else if (app_lang == "ru")
        {
            dlg_title = "Экспорт выбранной локализации";
            dlg_filter = "Локализация Crashday (*.plain)|*.plain";
        }

        using var dialog =
            new SaveFileDialog
            {
                Filter = dlg_filter,

                FileName = ".plain",

                Title = dlg_title
            };


        if (dialog.ShowDialog(this)
            != DialogResult.OK)
        {
            return;
        }


        try
        {
            _document.Export(
                sections,
                dialog.FileName,
                _document.LanguageCode,
                _document.LanguageName);

            if (app_lang == "en")
            {
                MessageBox.Show(this, $"Exported {sections.Count} section(s).", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, $"Экспортировано {sections.Count} секций.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            if (app_lang == "en")
            {
                ShowError("Error exporting file", ex);
            }
            else if (app_lang == "ru")
            {
                ShowError("Ошибка экспортирования файла", ex);
            }
        }
    }


    // ========================================================
    // GET SELECTED SECTIONS
    // ========================================================

    private List<LocalizationSection>
        GetSelectedSections()
    {
        var result =
            new List<LocalizationSection>();


        if (_tree.Nodes.Count == 0)
            return result;


        foreach (TreeNode sectionNode
                 in _tree.Nodes[0].Nodes)
        {
            if (sectionNode.Tag
                is not LocalizationSection section)
            {
                continue;
            }


            // -----------------------------------------------
            // Секция отмечена целиком.
            // -----------------------------------------------

            if (sectionNode.Checked)
            {
                result.Add(
                    CloneSectionForExport(
                        section));

                continue;
            }


            // -----------------------------------------------
            // Отмечены отдельные ключи.
            // -----------------------------------------------

            var selectedKeys =
                sectionNode.Nodes
                    .Cast<TreeNode>()
                    .Where(x => x.Checked)
                    .Select(
                        x => x.Tag as LocalizationEntry)
                    .Where(
                        x => x != null)
                    .Cast<LocalizationEntry>()
                    .ToList();


            if (selectedKeys.Count == 0)
                continue;


            result.Add(
                CloneSectionForExport(
                    section,
                    selectedKeys));
        }


        return result;
    }


    private static LocalizationSection
        CloneSectionForExport(
            LocalizationSection source,
            IEnumerable<LocalizationEntry>? keys = null)
    {
        var section =
            new LocalizationSection
            {
                Name =
                    source.Name
            };


        IEnumerable<LocalizationEntry> sourceKeys =
            keys ??
            source.Keys;


        foreach (LocalizationEntry entry
                 in sourceKeys)
        {
            section.Keys.Add(
                new LocalizationEntry
                {
                    Id =
                        entry.Id,

                    Description =
                        entry.Description,

                    Text =
                        entry.Text
                });
        }


        return section;
    }

    private void OpenMergeDialog()
    {
        if (_document == null)
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Open the main localization file first.", "Merge", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Откройте файл локализации для начала.", "Объединение", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        string dlg_title = "";
        string dlg_filter = "";

        if (app_lang == "en")
        {
            dlg_title = "Select plain files to merge";
            dlg_filter =
            "Crashday localization (*.plain)|*.plain|" +
            "All files (*.*)|*.*";
        }
        else if (app_lang == "ru")
        {
            dlg_title = "Выберите файлы локализации для объединения";
            dlg_filter =
            "Локализация Crashday (*.plain)|*.plain|" +
            "Все файлы (*.*)|*.*";
        }
        
        using var dialog =
                new OpenFileDialog
                {
                    Filter = dlg_filter,

                    Multiselect = true,

                    Title = dlg_title
                };

        if (dialog.ShowDialog(this)
            != DialogResult.OK)
        {
            return;
        }

        MergeFiles(
            dialog.FileNames);
    }

    private void MergeFiles(
        IEnumerable<string> paths)
    {
        if (_document == null)
            return;

        string[] files =
            paths
                .Where(File.Exists)
                .ToArray();

        if (files.Length == 0)
            return;

        var sources =
            new List<(string File, LocalizationDocument Document)>();

        bool hasExistingKeys = false;

        foreach (string file in files)
        {
            try
            {
                LocalizationDocument source =
                    LocalizationDocument.Load(file);

                sources.Add((file, source));

                foreach (LocalizationSection sourceSection
                         in source.Sections)
                {
                    LocalizationSection? targetSection =
                        _document.Sections.FirstOrDefault(
                            x =>
                                string.Equals(
                                    x.Name,
                                    sourceSection.Name,
                                    StringComparison.OrdinalIgnoreCase));

                    if (targetSection == null)
                        continue;

                    foreach (LocalizationEntry sourceEntry
                             in sourceSection.Keys)
                    {
                        bool exists =
                            targetSection.Keys.Any(
                                x =>
                                    string.Equals(
                                        x.Id,
                                        sourceEntry.Id,
                                        StringComparison.OrdinalIgnoreCase));

                        if (exists)
                        {
                            hasExistingKeys = true;
                            break;
                        }
                    }

                    if (hasExistingKeys)
                        break;
                }
            }
            catch (Exception ex)
            {
                if (app_lang == "en")
                {
                    ShowError($"Error merging file:\n{file}", ex);
                }
                else if (app_lang == "ru")
                {
                    ShowError($"Ошибка объединения файла:\n{file}", ex);
                }

                return;
            }
        }

        bool replaceExisting = false;

        string msgbox_title = "";
        string msgbox_text = "";

        if (hasExistingKeys)
        {
            if (app_lang == "en")
            {
                msgbox_title = "Merge plain";
                msgbox_text = "If the key already exists, replace its text with the value from the imported file?\n\n" +
                "Yes - replace the existing localization.\n" +
                "No - keep the existing localization.\n" +
                "Cancel - cancel the merge.";
            }
            else if (app_lang == "ru")
            {
                msgbox_title = "Объединить файлы";
                msgbox_text =
                "Если ключ уже существует, заменить его текст значением из импортируемого файла?\n\n" +
                "Да - заменить существующий перевод.\n" +
                "Нет - оставить существующий перевод.\n" +
                "Отмена — отменить объединение.";
            }

            DialogResult overwrite =
                MessageBox.Show(
                    this,
                    "Если ключ уже существует, заменить его TEXT " +
                    "значением из импортируемого файла?\n\n" +
                    "Да — заменить существующий перевод.\n" +
                    "Нет — оставить существующий перевод.\n" +
                    "Отмена — отменить объединение.",
                    "Merge plain",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

            if (overwrite == DialogResult.Cancel)
                return;

            replaceExisting =
                overwrite == DialogResult.Yes;
        }

        int addedSections = 0;
        int addedKeys = 0;
        int existingKeys = 0;
        int updatedKeys = 0;

        foreach ((string file, LocalizationDocument source)
                 in sources)
        {
            try
            {
                MergeResult result =
                    _document.Merge(
                        source,
                        replaceExisting);

                addedSections +=
                    result.AddedSections;

                addedKeys +=
                    result.AddedKeys;

                existingKeys +=
                    result.ExistingKeys;

                updatedKeys +=
                    result.UpdatedKeys;
            }
            catch (Exception ex)
            {
                if (app_lang == "en")
                {
                    ShowError($"Error merging file:\n{file}", ex);
                }
                else if (app_lang == "ru")
                {
                    ShowError($"Ошибка объединения файла:\n{file}", ex);
                }

                return;
            }
        }

        BuildTree();
        UpdateUi();

        SetModified(true);

        if (app_lang == "en")
        {
            MessageBox.Show(
                this,
                $"Merge complete.\n\n" +
                $"Files: {files.Length}\n" +
                $"New sections: {addedSections}\n" +
                $"New keys: {addedKeys}\n" +
                $"Existing keys: {existingKeys}\n" +
                $"Updated keys: {updatedKeys}\n\n" +
                "The new data will be written to the main file after saving.",
                "Merge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else if (app_lang == "ru")
        {
            MessageBox.Show(
                this,
                $"Объединение завершено.\n\n" +
                $"Файлов: {files.Length}\n" +
                $"Новых секций: {addedSections}\n" +
                $"Новых ключей: {addedKeys}\n" +
                $"Существующих ключей: {existingKeys}\n" +
                $"Обновленных ключей: {updatedKeys}\n\n" +
                "Новые данные будут записаны в основной файл после сохранения.",
                "Объединение",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }


    // ========================================================
    // STATUS
    // ========================================================

    private void UpdateStatus()
    {
        if (_document == null)
        {
            if (app_lang == "en")
            {
                _statusLabel.Text = "No file loaded";
            }
            else if (app_lang == "ru")
            {
                _statusLabel.Text = "Файл не загружен";
            }
            
            return;
        }


        int total =
            _document.TotalKeys;


        int localized =
            _document.LocalizedKeys;


        int percent =
            total == 0
                ? 100
                : localized *
                  100 /
                  total;

        if (app_lang == "en")
        {
            _statusLabel.Text = $"{localized} / {total} localized ({percent}%)";
        }
        else if (app_lang == "ru")
        {
            _statusLabel.Text = $"{localized} / {total} переведено ({percent}%)";
        }
    }


    // ========================================================
    // UPDATE UI
    // ========================================================

    private void UpdateUi()
    {
        bool is_document = treeView1.GetNodeCount(true) > 1;
        bool is_section = treeView1.SelectedNode?.Level == 1;
        bool is_id = treeView1.SelectedNode?.Level == 2;

        int checkedIDs = treeView1.Nodes
        .Cast<TreeNode>()
        .SelectMany(rootNode => rootNode.Nodes.Cast<TreeNode>())
        .Count(secondLevelNode => secondLevelNode.Checked);

        TS_SaveAs.Enabled = is_document;
        saveAsToolStripMenuItem.Enabled = is_document;

        TS_SaveFile.Enabled = _isModified;
        saveToolStripMenuItem.Enabled = _isModified;

        TS_OpenMergeDialog.Enabled = is_document;
        mergePlainToolStripMenuItem.Enabled = is_document;

        TS_ExportSelected.Enabled = is_document;
        exportSelectedToolStripMenuItem.Enabled = is_document;

        TS_FindNextUnlocalized.Enabled = is_document;
        findNextUnlocalizedToolStripMenuItem.Enabled = is_document;

        TS_ShowFindDialog.Enabled = is_document;
        findToolStripMenuItem1.Enabled = is_document;

        TS_ShowReplaceDialog.Enabled = is_document;
        replaceToolStripMenuItem1.Enabled = is_document;

        TS_CheckAll.Enabled = is_document;
        checkAllToolStripMenuItem.Enabled = is_document;

        TS_DeleteSelectedSections.Enabled = is_document;
        deleteSelectedSectionsToolStripMenuItem.Enabled = is_document;

        TS_AddEntry.Enabled = is_section || is_id;
        addNewIdentifierToolStripMenuItem.Enabled = is_section || is_id;

        TS_DeleteSelectedIdentifiers.Enabled = checkedIDs != 0;
        deletedSelectedIdentifiersToolStripMenuItem.Enabled = checkedIDs != 0;

        if (treeView1.Nodes.Count > 0)
        {
            button1.Enabled = true;
            button2.Enabled = true;
        }
        else
        {
            button1.Enabled = false;
            button2.Enabled = false;
        }

        _updatingControls = true;

        try
        {
            // --------------------------------------------------------
            // DOCUMENT
            // --------------------------------------------------------

            if (_document == null)
            {
                _languageCode.Text = "";
                _languageName.Text = "";
            }
            else
            {
                _languageCode.Text =
                    _document.LanguageCode;

                _languageName.Text =
                    _document.LanguageName;
            }

            // --------------------------------------------------------
            // RIGHT PANEL
            // --------------------------------------------------------

            if (_selectedEntry == null)
            {
                _fileName.Text = "";
                _identifier.Text = "";
                _englishText.Text = "";
                _localizedText.Text = "";
            }
            else
            {
                _identifier.Text =
                    _selectedEntry.Id;

                _englishText.Text =
                    _selectedEntry.Description;

                _localizedText.Text =
                    _selectedEntry.Text;

                // Находим секцию выбранного ID
                LocalizationSection? section =
                    _document?.Sections.FirstOrDefault(
                        x => x.Keys.Contains(_selectedEntry));

                _fileName.Text =
                    section?.Name ?? "";
            }
        }
        finally
        {
            _updatingControls = false;
        }

        // --------------------------------------------------------
        // ENABLE/DISABLE RIGHT PANEL
        // --------------------------------------------------------

        bool is_entry_selected =
            _selectedEntry != null;

        _fileName.Enabled =
            is_entry_selected;

        _identifier.Enabled =
            is_entry_selected;

        _englishText.Enabled =
            is_entry_selected;

        _localizedText.Enabled =
            is_entry_selected;

        UpdateRootText();
        UpdateStatus();
    }


    // ========================================================
    // ERROR
    // ========================================================

    private void ShowError(
        string title,
        Exception ex)
    {
        MessageBox.Show(
            this,
            ex.ToString(),
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }


    // ========================================================
    // CLOSE
    // ========================================================

    protected override void OnFormClosing(
    FormClosingEventArgs e)
    {
        if (_document != null && _isModified)
        {
            ApplyCurrent();

            string msgbox_title = "";
            string msgbox_text = "";

            if (app_lang == "en")
            {
                msgbox_title = "Crashday Localization Toolkit";
                msgbox_text = "Save changes before closing?";
            }
            else if (app_lang == "ru")
            {
                msgbox_title = "Инструментарий локализации Crashday";
                msgbox_text = "Сохранить изменения перед закрытием?";
            }

            DialogResult result = MessageBox.Show(this, msgbox_text, msgbox_title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == DialogResult.Yes)
            {
                try
                {
                    _document.Save(
                        _document.FilePath);

                    SetModified(false);
                }
                catch (Exception ex)
                {
                    if (app_lang == "en")
                    {
                        ShowError("Error saving file", ex);
                    }
                    else if (app_lang == "ru")
                    {
                        ShowError("Ошибка сохранения файла", ex);
                    }

                    e.Cancel = true;
                    return;
                }
            }
        }

        base.OnFormClosing(e);
    }

    private void UpdateTitle()
    {
        string title = "";

        if (app_lang == "en")
        {
            title = "Crashday Localization Toolkit";
        }
        else if (app_lang == "ru")
        {
            title = "Инструментарий локализации Crashday";
        }

        if (_document != null &&
            !string.IsNullOrEmpty(_document.FilePath))
        {
            title += " - " +
                     Path.GetFileName(_document.FilePath);
        }

        Text = title;
    }

    private void SetModified(bool modified)
    {
        _isModified = modified;

        UpdateTitle();

        if (_isModified)
            Text += "*";
    }

    // ========================================================
    // ADD / DELETE SECTIONS AND IDS
    // ========================================================

    private void InitializeTreeContextMenu()
    {
        var menu = new ContextMenuStrip();

        string str_addSection = "";
        string str_addId = "";
        string str_rename = "";
        string str_deleteSelected = "";

        if (app_lang == "en")
        {
            str_addSection = "Add section";
            str_addId = "Add identifier";
            str_rename = "Rename...";
            str_deleteSelected = "Delete checked";
        }
        else if (app_lang == "ru")
        {
            str_addSection = "Добавить секцию";
            str_addId = "Добавить идентификатор";
            str_rename = "Переименовать...";
            str_deleteSelected = "Удалить выбранное";
        }

        var addSection = new ToolStripMenuItem(str_addSection);
        var addId = new ToolStripMenuItem(str_addId);
        var rename = new ToolStripMenuItem(str_rename);
        var deleteSelected = new ToolStripMenuItem(str_deleteSelected);

        addSection.Click += (_, _) => AddSection();
        addId.Click += (_, _) => AddEntry();
        rename.Click += (_, _) => RenameSelectedTreeItem();
        deleteSelected.Click += (_, _) => DeleteSelectedTreeItem();

        menu.Items.Add(addSection);
        menu.Items.Add(addId);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(rename);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(deleteSelected);

        treeView1.ContextMenuStrip = menu;

        treeView1.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
                treeView1.SelectedNode = e.Node;
        };

        treeView1.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Insert)
            {
                if (treeView1.SelectedNode?.Tag is LocalizationEntry)
                    AddEntry();
                else
                    AddSection();

                e.SuppressKeyPress = true;
            }
        };
    }

    private void TreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Delete)
            return;

        DeleteSelectedTreeItem();
        e.SuppressKeyPress = true;
    }

    private void AddSection()
    {
        if (_document == null)
        {
            _document =
                LocalizationDocument.CreateNew();

            _selectedEntry = null;

            BuildTree();
            UpdateUi();
        }

        string msgbox_title = "";
        string msgbox_text = "";

        if (app_lang == "en")
        {
            msgbox_title = "Add section";
            msgbox_text = "Section title:";
        }
        else if (app_lang == "ru")
        {
            msgbox_title = "Добавить секцию";
            msgbox_text = "Название секции:";
        }

        string? name = PromptText(msgbox_title, msgbox_text);

        if (name == null)
            return;
        
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Section name can't be empty.", "Add section", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Название секции не может быть пустым.", "Добавить секцию", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        if (_document.Sections.Any(
                x => string.Equals(
                    x.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase)))
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, $"Section \"{name}\" already exists.", "Add section", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, $"Секция \"{name}\" уже существует.", "Добавить секцию", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        var section =
            new LocalizationSection
            {
                Name = name,
                IsNew = true
            };

        _document.Sections.Add(section);

        BuildTree();
        UpdateUi();

        SetModified(true);

        SelectSectionInTree(section);
    }

    private void AddEntry()
    {
        // Документ должен существовать.
        // Если его ещё нет, создаём новый.
        if (_document == null)
        {
            _document =
                LocalizationDocument.CreateNew();

            _selectedEntry = null;

            BuildTree();
            UpdateUi();
        }

        LocalizationSection? section =
            GetSelectedSection();

        if (section == null)
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Select a section or an ID first.", "Add ID", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Выберите секцию или идентификатор для начала.", "Добавить идентификатор", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            
            return;
        }

        string msgbox_title = "";
        string msgbox_text = "";

        if (app_lang == "en")
        {
            msgbox_title = "Add idenifier";
            msgbox_text = $"Identifier for section \"{section.Name}\":";
        }
        else if (app_lang == "ru")
        {
            msgbox_title = "Добавить идентификатор";
            msgbox_text = $"Идентификатор для секции \"{section.Name}\":";
        }
        
        string? id = PromptText(msgbox_title, msgbox_text);

        if (id == null)
            return;

        id = id.Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Identifier can't be empty.", "Add identifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Идентификатор не может быть пустым.", "Добавление идентификатора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        if (_document.Sections
            .SelectMany(x => x.Keys)
            .Any(x => string.Equals(
                x.Id,
                id,
                StringComparison.OrdinalIgnoreCase)))
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, $"Identifier \"{id}\" already exists.", "Add identifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, $"Идентификатор \"{id}\" уже существует.", "Добавление идентификатора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        var entry = new LocalizationEntry
        {
            Id = id,
            Description = "",
            Text = "",
            IsNew = true
        };

        section.Keys.Add(entry);

        BuildTree();
        UpdateUi();
        SetModified(true);

        SelectEntryInTree(entry);
    }

    private LocalizationSection? GetSelectedSection()
    {
        if (treeView1.SelectedNode?.Tag is LocalizationSection section)
            return section;

        if (treeView1.SelectedNode?.Tag is LocalizationEntry entry &&
            _document != null)
        {
            return _document.Sections.FirstOrDefault(
                x => x.Keys.Contains(entry));
        }

        return null;
    }

    private void DeleteSelectedSections()
    {
        if (_document == null || _tree.Nodes.Count == 0)
            return;

        var sectionsToDelete =
            new List<LocalizationSection>();

        TreeNode root = _tree.Nodes[0];

        foreach (TreeNode sectionNode in root.Nodes)
        {
            if (sectionNode.Tag is not LocalizationSection section)
                continue;

            if (sectionNode.Checked)
            {
                sectionsToDelete.Add(section);
            }
        }

        if (sectionsToDelete.Count == 0)
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Check the sections to be deleted.", "Delete sections", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Отметьте секции, которые нужно удалить.", "Удаление секций", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        int idCount =
            sectionsToDelete.Sum(
                x => x.Keys.Count);

        if (app_lang == "en")
        {
            DialogResult result =
            MessageBox.Show(
                this,
                $"Sections to be deleted: {sectionsToDelete.Count}\n\n" +
                "Continue?",
                "Deleting sections",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;
        }
        else if (app_lang == "ru")
        {
            DialogResult result =
            MessageBox.Show(
                this,
                $"Будет удалено секций: {sectionsToDelete.Count}\n\n" +
                "Продолжить?",
                "Удаление секций",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;
        }

        foreach (LocalizationSection section
                 in sectionsToDelete)
        {
            if (_selectedEntry != null &&
                section.Keys.Contains(_selectedEntry))
            {
                _selectedEntry = null;
            }

            _document.Sections.Remove(section);
        }

        _document.RebuildAfterSectionMove();

        BuildTree();
        UpdateUi();

        SetModified(true);
    }

    private void DeleteSelectedIdentifiers()
    {
        if (_document == null || _tree.Nodes.Count == 0)
            return;

        var entriesToDelete =
            new List<(LocalizationSection Section, LocalizationEntry Entry)>();

        TreeNode root = _tree.Nodes[0];

        foreach (TreeNode sectionNode in root.Nodes)
        {
            if (sectionNode.Tag is not LocalizationSection section)
                continue;

            foreach (TreeNode entryNode in sectionNode.Nodes)
            {
                if (!entryNode.Checked)
                    continue;

                if (entryNode.Tag is LocalizationEntry entry)
                {
                    entriesToDelete.Add(
                        (section, entry));
                }
            }
        }

        if (entriesToDelete.Count == 0)
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Check the identifiers to be deleted.", "Delete identifiers", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Отметьте идентификаторы, которые нужно удалить.", "Удаление идентификаторов", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        if (app_lang == "en")
        {
            DialogResult result =
            MessageBox.Show(
                this,
                $"Identifiers to be deleted: {entriesToDelete.Count}\n\n" +
                "Continue?",
                "Deleting identifiers",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;
        }
        else if (app_lang == "ru")
        {
            DialogResult result =
            MessageBox.Show(
                this,
                $"Будет удалено идентификаторов: {entriesToDelete.Count}\n\n" +
                "Продолжить?",
                "Удаление идентификаторов",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;
        }

        foreach ((LocalizationSection section,
                      LocalizationEntry entry) in entriesToDelete)
            {
                section.Keys.Remove(entry);

                if (ReferenceEquals(
                        _selectedEntry,
                        entry))
                {
                    _selectedEntry = null;
                }
            }

        _document.RebuildAfterSectionMove();

        BuildTree();
        UpdateUi();

        SetModified(true);
    }

    private void RenameSelectedTreeItem()
    {
        if (_document == null ||
            treeView1.SelectedNode == null)
        {
            return;
        }

        if (treeView1.SelectedNode.Tag is LocalizationSection section)
        {
            string msgbox_title = "";
            string msgbox_text = "";

            if (app_lang == "en")
            {
                msgbox_title = "Rename section";
                msgbox_text = $"New name for section \"{section.Name}\":";
            }
            else if (app_lang == "ru")
            {
                msgbox_title = "Переименование секции";
                msgbox_text = $"Новое название для секции \"{section.Name}\":";
            }

            string? newName = PromptText(msgbox_title, msgbox_text, section.Name);

            if (newName == null)
                return;

            newName = newName.Trim();

            if (string.IsNullOrWhiteSpace(newName))
            {
                if (app_lang == "en")
                {
                    MessageBox.Show(this, "Section name can't be empty.", "Rename section", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (app_lang == "ru")
                {
                    MessageBox.Show(this, "Название секции не может быть пустым.", "Переименование секции", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }

            bool duplicate =
                _document.Sections.Any(
                    x =>
                        !ReferenceEquals(x, section) &&
                        string.Equals(
                            x.Name,
                            newName,
                            StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                if (app_lang == "en")
                {
                    MessageBox.Show(this, $"Section \"{newName}\" already exists.", "Rename section", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (app_lang == "ru")
                {
                    MessageBox.Show(this, $"Секция \"{newName}\" уже существует.", "Переименование секции", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }

            if (string.Equals(
                    section.Name,
                    newName,
                    StringComparison.Ordinal))
            {
                return;
            }

            section.Name = newName;

            _document.RebuildAfterSectionMove();

            SetModified(true);

            BuildTree();
            UpdateUi();

            SelectSectionInTree(section);

            return;
        }

        if (treeView1.SelectedNode.Tag is LocalizationEntry entry)
        {
            string msgbox_title = "";
            string msgbox_text = "";

            if (app_lang == "en")
            {
                msgbox_title = "Rename identifier";
                msgbox_text = $"New identifier for \"{entry.Id}\":";
            }
            else if (app_lang == "ru")
            {
                msgbox_title = "Переименование идентификатора";
                msgbox_text = $"Новый идентификатор для \"{entry.Id}\":";
            }

            string? newId = PromptText(msgbox_title, msgbox_text, entry.Id);

            if (newId == null)
                return;

            newId = newId.Trim();

            if (string.IsNullOrWhiteSpace(newId))
            {
                if (app_lang == "en")
                {
                    MessageBox.Show(this, "Identifier can't be empty.", "Rename identifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (app_lang == "ru")
                {
                    MessageBox.Show(this, "Идентификатор не может быть пустым.", "Переименование идентификатора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }

            bool duplicate =
                _document.Sections
                    .SelectMany(x => x.Keys)
                    .Any(
                        x =>
                            !ReferenceEquals(x, entry) &&
                            string.Equals(
                                x.Id,
                                newId,
                                StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                if (app_lang == "en")
                {
                    MessageBox.Show(this, $"Identifier \"{newId}\" already exists.", "Rename identifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (app_lang == "ru")
                {
                    MessageBox.Show(this, $"Идентификатор \"{newId}\" уже существует.", "Переименование идентификатора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }

            if (string.Equals(
                    entry.Id,
                    newId,
                    StringComparison.Ordinal))
            {
                return;
            }

            entry.Id = newId;

            _document.UpdateEntryIdLine(entry);

            SetModified(true);

            BuildTree();
            UpdateUi();

            SelectEntryInTree(entry);
        }
    }

    private void DeleteSelectedTreeItem()
    {
        if (_document == null || _tree.Nodes.Count == 0)
            return;

        var sectionsToDelete =
            new List<LocalizationSection>();

        var entriesToDelete =
            new List<(LocalizationSection Section, LocalizationEntry Entry)>();

        TreeNode root = _tree.Nodes[0];

        foreach (TreeNode sectionNode in root.Nodes)
        {
            if (sectionNode.Tag is not LocalizationSection section)
                continue;

            if (sectionNode.Checked)
            {
                sectionsToDelete.Add(section);
                continue;
            }

            // ----------------------------------------------------
            // Иначе ищем отдельно отмеченные ID.
            // ----------------------------------------------------

            foreach (TreeNode entryNode in sectionNode.Nodes)
            {
                if (!entryNode.Checked)
                    continue;

                if (entryNode.Tag is LocalizationEntry entry)
                {
                    entriesToDelete.Add(
                        (section, entry));
                }
            }
        }

        // --------------------------------------------------------
        // Ничего не выбрано
        // --------------------------------------------------------

        if (sectionsToDelete.Count == 0 &&
            entriesToDelete.Count == 0)
        {
            if (app_lang == "en")
            {
                MessageBox.Show(this, "Select the sections or identifiers to be deleted.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Отметьте секции или идентификаторы, которые нужно удалить.", "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        // --------------------------------------------------------
        // Подсчитываем количество
        // --------------------------------------------------------

        int deletedSections =
            sectionsToDelete.Count;

        int deletedIds =
            sectionsToDelete.Sum(
                x => x.Keys.Count)
            + entriesToDelete.Count;

        string msgbox_title = "";
        string msgbox_text = "";

        if (app_lang == "en")
        {
            msgbox_title = "Delete";
        }
        else if (app_lang == "ru")
        {
            msgbox_title = "Удаление";
        }

        if (deletedSections > 0)
        {
            if (app_lang == "en")
            {
                msgbox_text =
                $"Sections to be deleted: {deletedSections}\n" +
                $"Identifiers to be deleted: {deletedIds}\n\n" +
                "Continue?";
            }
            else if (app_lang == "ru")
            {
                msgbox_text =
                $"Будет удалено секций: {deletedSections}\n" +
                $"Будет удалено идентификаторов: {deletedIds}\n\n" +
                "Продолжить?";
            }
        }
        else
        {
            if (app_lang == "en")
            {
                msgbox_text =
                $"Identifiers to be deleted: {deletedIds}\n\n" +
                "Continue?";
            }
            else if (app_lang == "ru")
            {
                msgbox_text =
                $"Будет удалено идентификаторов: {deletedIds}\n\n" +
                "Продолжить?";
            }
        }

        DialogResult result = MessageBox.Show(this, msgbox_text, msgbox_title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        // --------------------------------------------------------
        // Удаляем секции
        // --------------------------------------------------------

        foreach (LocalizationSection section
                 in sectionsToDelete)
        {
            if (ReferenceEquals(
                    _selectedEntry,
                    section.Keys.FirstOrDefault(
                        x => ReferenceEquals(
                            x,
                            _selectedEntry))))
            {
                _selectedEntry = null;
            }

            if (section.Keys.Contains(_selectedEntry))
                _selectedEntry = null;

            _document.Sections.Remove(section);
        }

        // --------------------------------------------------------
        // Удаляем отдельные ID
        // --------------------------------------------------------

        foreach ((LocalizationSection section,
                  LocalizationEntry entry) in entriesToDelete)
        {
            // Секция могла быть удалена целиком.
            if (!_document.Sections.Contains(section))
                continue;

            section.Keys.Remove(entry);

            if (ReferenceEquals(
                    _selectedEntry,
                    entry))
            {
                _selectedEntry = null;
            }
        }

        // --------------------------------------------------------
        // Обновляем файл и интерфейс
        // --------------------------------------------------------

        _document.RebuildAfterSectionMove();

        BuildTree();

        UpdateUi();

        SetModified(true);
    }

    private void RebuildDocumentAfterStructureChange()
    {
        if (_document == null)
            return;

        _document.RebuildAfterSectionMove();
    }

    private void SelectSectionInTree(LocalizationSection section)
    {
        if (_tree.Nodes.Count == 0)
            return;

        foreach (TreeNode node in _tree.Nodes[0].Nodes)
        {
            if (!ReferenceEquals(node.Tag, section))
                continue;

            _tree.SelectedNode = node;
            node.EnsureVisible();
            return;
        }
    }

    private static string? PromptText(
        string title,
        string caption,
        string initialValue = "")
    {
        using var dialog = new Form
        {
            Text = title,
            Width = 430,
            Height = 150,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false
        };

        var label = new Label
        {
            Left = 12,
            Top = 12,
            Width = 390,
            Text = caption
        };

        var textBox = new TextBox
        {
            Left = 12,
            Top = 38,
            Width = 390,
            Text = initialValue
        };

        string text_ok = "";
        string text_cancel = "";

        if (app_lang == "en")
        {
            text_ok = "OK";
            text_cancel = "Cancel";
        }
        else if (app_lang == "ru")
        {
            text_ok = "ОК";
            text_cancel= "Отмена";
        }

            var ok = new Button
            {
                Text = text_ok,
                Left = 246,
                Top = 72,
                Width = 75,
                DialogResult = DialogResult.OK
            };

        var cancel = new Button
        {
            Text = text_cancel,
            Left = 327,
            Top = 72,
            Width = 75,
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.Add(label);
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(ok);
        dialog.Controls.Add(cancel);

        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        dialog.Shown += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        return dialog.ShowDialog() == DialogResult.OK
            ? textBox.Text
            : null;
    }

    private void TextBox6_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        RenameSelectedSection();
    }

    private void TextBox7_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        RenameSelectedId();
    }

    private void TextBox6_Leave(object? sender, EventArgs e)
    {
        RenameSelectedSection();
    }

    private void TextBox7_Leave(object? sender, EventArgs e)
    {
        RenameSelectedId();
    }

    private void RenameSelectedId()
    {
        if (_document == null || _selectedEntry == null)
            return;

        string oldId = _selectedEntry.Id;
        string newId = _identifier.Text.Trim();

        if (string.IsNullOrWhiteSpace(newId))
        {
            _identifier.Text = oldId;

            if (app_lang == "en")
            {
                MessageBox.Show(this, "Identifier can't be empty.", "Changing the identifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Идентификатор не может быть пустым.", "Изменение идентификатора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        if (string.Equals(
                oldId,
                newId,
                StringComparison.Ordinal))
        {
            _identifier.Text = oldId;
            return;
        }

        LocalizationEntry? existingEntry =
            _document.Sections
                .SelectMany(x => x.Keys)
                .FirstOrDefault(
                    x =>
                        !ReferenceEquals(x, _selectedEntry) &&
                        string.Equals(
                            x.Id,
                            newId,
                            StringComparison.OrdinalIgnoreCase));

        if (existingEntry != null)
        {
            string msgbox_text = "";
            string msgbox_title = "";

            if (app_lang == "en")
            {
                msgbox_title = "Identifier match";
                msgbox_text =
                $"Identifier \"{newId}\" already exists.\n\n" +
                "Continue renaming?";
            }
            else if (app_lang == "ru")
            {
                msgbox_title = "Совпадение идентификаторов";
                msgbox_text =
                $"Идентификатор \"{newId}\" уже существует.\n\n" +
                "Продолжить переименование?";
            }

            DialogResult result = MessageBox.Show(this, msgbox_text, msgbox_title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                _identifier.Text = oldId;
                return;
            }
        }

        _selectedEntry.Id = newId;

        _document.UpdateEntryIdLine(_selectedEntry);

        BuildTree();

        SelectEntry(_selectedEntry);

        UpdateUi();

        SetModified(true);
    }

    private void RenameSelectedSection()
    {
        if (_document == null || _selectedEntry == null)
            return;

        LocalizationSection? oldSection =
            _document.Sections.FirstOrDefault(
                x => x.Keys.Contains(_selectedEntry));

        if (oldSection == null)
            return;

        string oldSectionName = oldSection.Name;
        string newSectionName = _fileName.Text.Trim();

        if (string.IsNullOrWhiteSpace(newSectionName))
        {
            _fileName.Text = oldSectionName;

            if (app_lang == "en")
            {
                MessageBox.Show(this, "Section name can't be empty.", "Changing the section", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (app_lang == "ru")
            {
                MessageBox.Show(this, "Имя секции не может быть пустым.", "Изменение секции", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            
            return;
        }

        if (string.Equals(
                oldSectionName,
                newSectionName,
                StringComparison.Ordinal))
        {
            _fileName.Text = oldSectionName;
            return;
        }

        LocalizationSection? targetSection =
            _document.Sections.FirstOrDefault(
                x =>
                    string.Equals(
                        x.Name,
                        newSectionName,
                        StringComparison.OrdinalIgnoreCase));

        if (targetSection != null)
        {
            string msgbox_text = "";
            string msgbox_title = "";

            if (app_lang == "en")
            {
                msgbox_title = "The section already exists";
                msgbox_text =
                $"Section \"{newSectionName}\" already exists.\n\n" +
                $"Identifier \"{_selectedEntry.Id}\" will be moved " +
                "to this section.\n\n" +
                "Continue?";
            }
            else if (app_lang == "ru")
            {
                msgbox_title = "Секция уже существует";
                msgbox_text =
                $"Секция \"{newSectionName}\" уже существует.\n\n" +
                $"Идентификатор \"{_selectedEntry.Id}\" будет перемещён " +
                "в эту секцию.\n\n" +
                "Продолжить?";
            }

            DialogResult result = MessageBox.Show(this, msgbox_text, msgbox_title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                _fileName.Text = oldSectionName;
                return;
            }

            oldSection.Keys.Remove(_selectedEntry);

            targetSection.Keys.Add(_selectedEntry);

            if (oldSection.Keys.Count == 0)
                _document.Sections.Remove(oldSection);
        }
        else
        {
            var newSection =
                new LocalizationSection
                {
                    Name = newSectionName,
                    IsNew = true
                };

            oldSection.Keys.Remove(_selectedEntry);

            newSection.Keys.Add(_selectedEntry);

            _document.Sections.Add(newSection);

            if (oldSection.Keys.Count == 0)
                _document.Sections.Remove(oldSection);
        }

        _document.RebuildAfterSectionMove();

        BuildTree();

        SelectEntry(_selectedEntry);

        UpdateUi();

        SetModified(true);
    }

    // ========================================================
    // NEW FILE
    // ========================================================

    private void NewFile()
    {
        // ----------------------------------------------------
        // Если есть несохранённые изменения — спрашиваем.
        // ----------------------------------------------------

        if (_document != null && _isModified)
        {
            string msgbox_text = "";
            string msgbox_title = "";

            if (app_lang == "en")
            {
                msgbox_title = "New file";
                msgbox_text = "Save changes before creating a new file?";
            }
            else if (app_lang == "ru")
            {
                msgbox_title = "Новый файл";
                msgbox_text = "Сохранить изменения перед созданием нового файла?";
            }

            DialogResult result = MessageBox.Show(this, msgbox_text, msgbox_title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return;

            if (result == DialogResult.Yes)
            {
                ApplyCurrent();

                try
                {
                    // Если это уже сохранённый файл —
                    // просто сохраняем его.
                    if (!string.IsNullOrWhiteSpace(
                            _document.FilePath))
                    {
                        _document.Save(
                            _document.FilePath);
                    }
                    else
                    {
                        // Новый документ ещё не имеет имени.
                        SaveAs();

                        // Если пользователь отменил Save As,
                        // новый файл создавать не нужно.
                        if (_isModified)
                            return;
                    }
                }
                catch (Exception ex)
                {
                    if (app_lang == "en")
                    {
                        ShowError("Error opening file", ex);
                    }
                    else if (app_lang == "ru")
                    {
                        ShowError("Ошибка открытия файла", ex);
                    }

                    return;
                }
            }
        }

        // ----------------------------------------------------
        // Создаём полностью новый документ.
        // ----------------------------------------------------

        _document =
            LocalizationDocument.CreateNew();

        _selectedEntry = null;

        SetModified(false);

        BuildTree();

        // ----------------------------------------------------
        // Автоматически выбираем корневой элемент.
        // ----------------------------------------------------

        if (_tree.Nodes.Count > 0)
        {
            _tree.SelectedNode =
                _tree.Nodes[0];
        }

        UpdateUi();
    }

    // ========================================================
    // DRAG & DROP OPEN / MERGE
    // ========================================================

    private void HandleDroppedFile(string path)
    {
        if (!File.Exists(path))
            return;

        // ----------------------------------------------------
        // Если программа полностью пустая —
        // просто открываем файл.
        // ----------------------------------------------------

        if (_document == null ||
            _document.Sections.Count == 0)
        {
            OpenDroppedFile(path);
            return;
        }

        // ----------------------------------------------------
        // В программе уже есть данные.
        // Предлагаем:
        //
        // Да    = открыть файл
        // Нет   = соединить с текущими данными
        // Отмена = ничего не делать
        // ----------------------------------------------------

        string msgbox_text = "";
        string msgbox_title = "";

        if (app_lang == "en")
        {
            msgbox_title = "Opening the file";
            msgbox_text =
            "The program already contains loaded data.\n\n" +
            "Yes - open the dragged file instead of the current data.\n" +
            "No - merge the dragged file with the current data.\n" +
            "Cancel - do nothing.";
        }
        else if (app_lang == "ru")
        {
            msgbox_title = "Открытие файла";
            msgbox_text =
            "В программе уже есть загруженные данные.\n\n" +
            "Да - открыть перетащенный файл вместо текущих данных.\n" +
            "Нет - соединить перетащенный файл с текущими данными.\n" +
            "Отмена - ничего не делать.";
        }

        DialogResult result = MessageBox.Show(this, msgbox_text, msgbox_title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (result == DialogResult.Cancel)
            return;

        // ----------------------------------------------------
        // ОТКРЫТЬ
        // ----------------------------------------------------

        if (result == DialogResult.Yes)
        {
            if (!ConfirmDiscardOrSaveChanges())
                return;

            OpenDroppedFile(path);
            return;
        }

        // ----------------------------------------------------
        // СОЕДИНИТЬ
        // ----------------------------------------------------

        if (result == DialogResult.No)
        {
            MergeFiles(new[] { path });
        }
    }

    // ========================================================
    // OPEN FROM WINDOWS
    // ========================================================

    private void OpenCommandLineFiles()
    {
        string[] args =
            Environment.GetCommandLineArgs();

        // args[0] — путь к exe.
        if (args.Length <= 1)
            return;

        string[] plainFiles =
            args
                .Skip(1)
                .Where(File.Exists)
                .Where(
                    x =>
                        string.Equals(
                            Path.GetExtension(x),
                            ".plain",
                            StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (plainFiles.Length == 0)
            return;

        // ----------------------------------------------------
        // Один файл — точно такое же поведение,
        // как при Drag'n'Drop одного файла.
        // ----------------------------------------------------

        if (plainFiles.Length == 1)
        {
            HandleDroppedFile(plainFiles[0]);
            return;
        }

        // ----------------------------------------------------
        // Несколько файлов — точно такое же поведение,
        // как при Drag'n'Drop нескольких файлов.
        // ----------------------------------------------------

        HandleDroppedMultipleFiles(plainFiles);
    }

    // ========================================================
    // DRAG & DROP MULTIPLE FILES
    // ========================================================

    private void HandleDroppedMultipleFiles(
        string[] paths)
    {
        if (paths.Length < 2)
            return;

        // ----------------------------------------------------
        // Если есть несохранённые изменения,
        // сначала обязательно сохраняем их через Save As.
        // ----------------------------------------------------

        if (_document != null && _isModified)
        {
            if (!ConfirmSaveBeforeDragDrop())
                return;
        }

        // ----------------------------------------------------
        // Если документа нет или он пустой,
        // первый dropped-файл становится основным.
        // ----------------------------------------------------

        if (_document == null ||
            _document.Sections.Count == 0)
        {
            if (!OpenDroppedFile(paths[0]))
                return;

            // Остальные файлы автоматически объединяем.
            MergeFiles(paths.Skip(1));

            return;
        }

        // ----------------------------------------------------
        // Документ уже открыт.
        // Все dropped-файлы автоматически
        // объединяются с текущим документом.
        // ----------------------------------------------------

        MergeFiles(paths);
    }

    // ========================================================
    // SAVE BEFORE DRAG & DROP
    // ========================================================

    private bool ConfirmSaveBeforeDragDrop()
    {
        if (_document == null ||
            !_isModified)
        {
            return true;
        }

        ApplyCurrent();

        string msgbox_text = "";
        string msgbox_title = "";

        if (app_lang == "en")
        {
            msgbox_title = "Unsaved changes";
            msgbox_text =
            "The current file contains unsaved changes.\n\n" +
            "Yes - save changes as...\n" +
            "No - continue without saving.\n" +
            "Cancel - cancel file dragging.";
        }
        else if (app_lang == "ru")
        {
            msgbox_title = "Несохраненные изменения";
            msgbox_text =
            "В текущем файле есть несохраненные изменения.\n\n" +
            "Да - сохранить изменения как...\n" +
            "Нет - продолжить без сохранения.\n" +
            "Отмена - отменить перетаскивание файлов.";
        }

        DialogResult result = MessageBox.Show(this, msgbox_text, msgbox_title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        // ----------------------------------------------------
        // ОТМЕНА
        // ----------------------------------------------------

        if (result == DialogResult.Cancel)
            return false;

        // ----------------------------------------------------
        // НЕ СОХРАНЯТЬ
        // ----------------------------------------------------

        if (result == DialogResult.No)
            return true;

        // ----------------------------------------------------
        // SAVE AS
        // ----------------------------------------------------

        if (result == DialogResult.Yes)
        {
            return SaveAs();
        }

        return false;
    }

    // ========================================================
    // OPEN DROPPED FILE
    // ========================================================

    private bool OpenDroppedFile(string path)
    {
        try
        {
            LocalizationDocument document =
                LocalizationDocument.Load(path);

            _document = document;
            _selectedEntry = null;

            SetModified(false);

            BuildTree();
            UpdateUi();

            return true;
        }
        catch (Exception ex)
        {
            if (app_lang == "en")
            {
                ShowError("Error opening file", ex);
            }
            else if (app_lang == "ru")
            {
                ShowError("Ошибка открытия файла", ex);
            }

            return false;
        }
    }


    // ========================================================
    // SAVE / DISCARD CHANGES
    // ========================================================

    private bool ConfirmDiscardOrSaveChanges()
    {
        if (_document == null ||
            !_isModified)
        {
            return true;
        }

        ApplyCurrent();

        string msgbox_text = "";
        string msgbox_title = "";

        if (app_lang == "en")
        {
            msgbox_title = "Unsaved changes";
            msgbox_text =
            "The current file contains unsaved changes.\n\n" +
            "Yes - save changes as...\n" +
            "No - open a new file without saving.\n" +
            "Cancel – cancel the opening.";
        }
        else if (app_lang == "ru")
        {
            msgbox_title = "Несохраненные изменения";
            msgbox_text =
            "В текущем файле есть несохраненные изменения.\n\n" +
            "Да - сохранить изменения как...\n" +
            "Нет - открыть новый файл без сохранения.\n" +
            "Отмена - отменить открытие.";
        }

        DialogResult result = MessageBox.Show(this, msgbox_text, msgbox_title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

        // ----------------------------------------------------
        // ОТМЕНА
        // ----------------------------------------------------

        if (result == DialogResult.Cancel)
            return false;

        // ----------------------------------------------------
        // НЕ СОХРАНЯТЬ
        // ----------------------------------------------------

        if (result == DialogResult.No)
            return true;

        // ----------------------------------------------------
        // SAVE AS
        // ----------------------------------------------------

        if (result == DialogResult.Yes)
        {
            return SaveAs();
        }

        return false;
    }

    private void ExpandList()
    {
        treeView1.ExpandAll();
    }

    private void CollapseList()
    {
        treeView1.CollapseAll();
    }

    private void SwitchToEnglish()
    {
        Registry.SetValue(regkey_path, regkey_val, "en");
        UpdateLoc();
    }

    private void SwitchToRussian()
    {
        Registry.SetValue(regkey_path, regkey_val, "ru");
        UpdateLoc();
    }

    string[] available_langs = ["en", "ru"];

    private void UpdateLoc()
    {
        app_lang = Registry.GetValue(regkey_path, regkey_val, null) as string;

        if ((app_lang == null) || (!available_langs.Contains(app_lang)))
        {
            SwitchToEnglish();
        }

        if (app_lang == "en")
        {
            englishToolStripMenuItem.Checked = true;
            русскийRussianToolStripMenuItem.Checked = false;
        }
        else if (app_lang == "ru")
        {
            englishToolStripMenuItem.Checked = false;
            русскийRussianToolStripMenuItem.Checked = true;
        }

        if (app_lang == "en")
        {
            label1.Text = "Language code:";
            label2.Text = "User-readable language name:";
            label4.Text = "Identifier:";
            label5.Text = "Section containing text:";
            label9.Text = "English source text:";
            label10.Text = "Localized text:";
            groupBox1.Text = "Selected keyword";
            groupBox2.Text = "Localized source file";
            fileToolStripMenuItem.Text = "&File";
            editToolStripMenuItem.Text = "&Edit";
            helpToolStripMenuItem.Text = "&Help";
            newToolStripMenuItem.Text = "&New";
            openToolStripMenuItem.Text = "&Open...";
            saveToolStripMenuItem.Text = "&Save";
            saveAsToolStripMenuItem.Text = "S&ave As...";
            exportSelectedToolStripMenuItem.Text = "&Export Checked";
            mergePlainToolStripMenuItem.Text = "&Merge Plain";
            exitToolStripMenuItem.Text = "E&xit";
            addNewSectionToolStripMenuItem.Text = "&Add New Section";
            deleteSelectedSectionsToolStripMenuItem.Text = "&Delete Checked Section(s)";
            addNewIdentifierToolStripMenuItem.Text = "Add &New Identifier";
            deletedSelectedIdentifiersToolStripMenuItem.Text = "D&elete Checked Identifier(s)";
            findToolStripMenuItem1.Text = "&Find...";
            findNextUnlocalizedToolStripMenuItem.Text = "F&ind Next Unlocalized";
            replaceToolStripMenuItem1.Text = "&Replace...";
            checkAllToolStripMenuItem.Text = "&Check / Uncheck All";
            changeLanguageToolStripMenuItem.Text = "C&hange Toolkit Language";
            aboutToolStripMenuItem.Text = "&About...";
            toolTip1.SetToolTip(button1, "Expand list");
            toolTip1.SetToolTip(button2, "Collapse list");
        }
        else if (app_lang == "ru")
        {
            label1.Text = "Код языка:";
            label2.Text = "Понятное название языка:";
            label4.Text = "Идентификатор:";
            label5.Text = "Секция, содержащая текст:";
            label9.Text = "Исходный текст на английском языке:";
            label10.Text = "Переведенный текст:";
            groupBox1.Text = "Выбранное ключевое слово";
            groupBox2.Text = "Локализованный исходный файл";
            fileToolStripMenuItem.Text = "&Файл";
            editToolStripMenuItem.Text = "&Правка";
            helpToolStripMenuItem.Text = "П&омощь";
            newToolStripMenuItem.Text = "&Новый";
            openToolStripMenuItem.Text = "&Открыть...";
            saveToolStripMenuItem.Text = "&Сохранить";
            saveAsToolStripMenuItem.Text = "Со&хранить как...";
            exportSelectedToolStripMenuItem.Text = "&Экспортировать выбранное";
            mergePlainToolStripMenuItem.Text = "О&бъединить файлы";
            exitToolStripMenuItem.Text = "&Выход";
            addNewSectionToolStripMenuItem.Text = "&Добавить новую секцию";
            deleteSelectedSectionsToolStripMenuItem.Text = "&Удалить выбранные секции";
            addNewIdentifierToolStripMenuItem.Text = "Д&обавить новый идентификатор";
            deletedSelectedIdentifiersToolStripMenuItem.Text = "Уд&алить выбранные идентификаторы";
            findToolStripMenuItem1.Text = "&Найти...";
            findNextUnlocalizedToolStripMenuItem.Text = "На&йти далее непереведенный текст";
            replaceToolStripMenuItem1.Text = "&Заменить...";
            checkAllToolStripMenuItem.Text = "&Поставить / снять выделение везде";
            changeLanguageToolStripMenuItem.Text = "&Сменить язык программы";
            aboutToolStripMenuItem.Text = "&О программе...";
            toolTip1.SetToolTip(button1, "Развернуть список");
            toolTip1.SetToolTip(button2, "Свернуть список");
        }

        TS_About.Text = aboutToolStripMenuItem.Text;
        TS_AddEntry.Text = addNewIdentifierToolStripMenuItem.Text;
        TS_AddSection.Text = addNewSectionToolStripMenuItem.Text;
        TS_CheckAll.Text = checkAllToolStripMenuItem.Text;
        TS_DeleteSelectedIdentifiers.Text = deletedSelectedIdentifiersToolStripMenuItem.Text;
        TS_DeleteSelectedSections.Text = deleteSelectedSectionsToolStripMenuItem.Text;
        TS_ExportSelected.Text = exportSelectedToolStripMenuItem.Text;
        TS_FindNextUnlocalized.Text = findNextUnlocalizedToolStripMenuItem.Text;
        TS_NewFile.Text = newToolStripMenuItem.Text;
        TS_OpenFile.Text = openToolStripMenuItem.Text;
        TS_OpenMergeDialog.Text = mergePlainToolStripMenuItem.Text;
        TS_SaveAs.Text = saveAsToolStripMenuItem.Text;
        TS_SaveFile.Text = saveToolStripMenuItem.Text;
        TS_ShowFindDialog.Text = findToolStripMenuItem1.Text;
        TS_ShowReplaceDialog.Text = replaceToolStripMenuItem1.Text;

        UpdateUi();
        SetModified(_isModified);
        InitializeTreeContextMenu();
        BuildTree();
    }
}