using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kotoba.NhkGui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal enum NhkSite
{
    News,
    Easy
}

internal sealed class NhkArticle
{
    public string Site { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? PublishedAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? Content { get; set; }
    public bool ContentIsTruncated { get; set; }
    public string? ApiUrl { get; set; }
    public string? DatePublished { get; set; }
    public string? DateModified { get; set; }
}

internal static class UiPalette
{
    public static readonly Color Window = Color.FromArgb(249, 246, 255);
    public static readonly Color Panel = Color.FromArgb(255, 253, 255);
    public static readonly Color Toolbar = Color.FromArgb(240, 234, 255);
    public static readonly Color Lavender = Color.FromArgb(220, 209, 255);
    public static readonly Color LavenderSoft = Color.FromArgb(232, 224, 255);
    public static readonly Color LavenderWash = Color.FromArgb(247, 244, 255);
    public static readonly Color LavenderDark = Color.FromArgb(132, 104, 190);
    public static readonly Color Mint = Color.FromArgb(203, 238, 226);
    public static readonly Color MintWash = Color.FromArgb(242, 252, 248);
    public static readonly Color Peach = Color.FromArgb(255, 226, 216);
    public static readonly Color PeachDark = Color.FromArgb(211, 132, 112);
    public static readonly Color Rose = Color.FromArgb(255, 220, 236);
    public static readonly Color Candy = Color.FromArgb(215, 191, 255);
    public static readonly Color Sky = Color.FromArgb(224, 240, 255);
    public static readonly Color Text = Color.FromArgb(49, 43, 68);
    public static readonly Color MutedText = Color.FromArgb(105, 91, 132);
    public static readonly Color Border = Color.FromArgb(219, 208, 242);
}

internal sealed class MainForm : Form
{
    private readonly NhkClient _client = new();
    private readonly ComboBox _siteCombo = new();
    private readonly DateTimePicker _dateInput = new();
    private readonly CheckBox _contentCheck = new();
    private readonly Button _loadButton = new();
    private readonly Button _openButton = new();
    private readonly DataGridView _articlesGrid = new();
    private readonly TextBox _contentText = new();
    private readonly TabControl _vocabularyTabs = new();
    private readonly ListBox _verbList = new();
    private readonly ListBox _properNounList = new();
    private readonly ListBox _otherWordList = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private BindingList<ArticleRow> _rows = new();

    public MainForm()
    {
        Text = "Kotoba NHK";
        MinimumSize = new Size(1180, 720);
        ClientSize = new Size(1280, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UiPalette.Window;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14),
            BackColor = UiPalette.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var toolbar = new KawaiiToolbarPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(16, 15, 16, 12),
            BackColor = UiPalette.Window
        };

        toolbar.Controls.Add(new Label
        {
            Text = "Kotoba NHK",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = UiPalette.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 5, 10, 0)
        });

        toolbar.Controls.Add(new Label
        {
            Text = "pastel news desk",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = UiPalette.LavenderDark,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 11, 28, 0)
        });

        toolbar.Controls.Add(new Label
        {
            Text = "Site",
            AutoSize = true,
            ForeColor = UiPalette.MutedText,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 10, 8, 0)
        });

        _siteCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _siteCombo.Width = 160;
        _siteCombo.BackColor = UiPalette.Panel;
        _siteCombo.ForeColor = UiPalette.Text;
        _siteCombo.FlatStyle = FlatStyle.Flat;
        _siteCombo.Items.Add("Classique");
        _siteCombo.Items.Add("Easy");
        _siteCombo.SelectedIndex = 0;
        _siteCombo.Margin = new Padding(0, 6, 18, 0);
        toolbar.Controls.Add(_siteCombo);

        toolbar.Controls.Add(new Label
        {
            Text = "Date",
            AutoSize = true,
            ForeColor = UiPalette.MutedText,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 10, 8, 0)
        });

        _dateInput.Format = DateTimePickerFormat.Custom;
        _dateInput.CustomFormat = "yyyy-MM-dd";
        _dateInput.Width = 128;
        _dateInput.Value = DateTime.Today;
        _dateInput.BackColor = UiPalette.Panel;
        _dateInput.ForeColor = UiPalette.Text;
        _dateInput.CalendarMonthBackground = UiPalette.Panel;
        _dateInput.CalendarTitleBackColor = UiPalette.Lavender;
        _dateInput.CalendarTitleForeColor = UiPalette.Text;
        _dateInput.CalendarTrailingForeColor = UiPalette.MutedText;
        _dateInput.Margin = new Padding(0, 6, 18, 0);
        toolbar.Controls.Add(_dateInput);

        _contentCheck.Text = "Charger contenu";
        _contentCheck.AutoSize = true;
        _contentCheck.ForeColor = UiPalette.Text;
        _contentCheck.BackColor = UiPalette.Toolbar;
        _contentCheck.Margin = new Padding(0, 10, 18, 0);
        toolbar.Controls.Add(_contentCheck);

        _loadButton.Text = "Recuperer";
        StylePastelButton(_loadButton, UiPalette.Lavender, 132);
        _loadButton.Margin = new Padding(0, 3, 8, 0);
        _loadButton.Click += async (_, _) => await LoadArticlesAsync();
        toolbar.Controls.Add(_loadButton);

        _openButton.Text = "Ouvrir";
        StylePastelButton(_openButton, UiPalette.LavenderSoft, 108);
        _openButton.Enabled = false;
        _openButton.Margin = new Padding(0, 3, 0, 0);
        _openButton.Click += (_, _) => OpenSelectedArticle();
        toolbar.Controls.Add(_openButton);

        _articlesGrid.Dock = DockStyle.Fill;
        _articlesGrid.BackgroundColor = UiPalette.Panel;
        _articlesGrid.BorderStyle = BorderStyle.None;
        _articlesGrid.GridColor = UiPalette.Border;
        _articlesGrid.ReadOnly = true;
        _articlesGrid.AllowUserToAddRows = false;
        _articlesGrid.AllowUserToDeleteRows = false;
        _articlesGrid.AllowUserToResizeRows = false;
        _articlesGrid.AutoGenerateColumns = false;
        _articlesGrid.MultiSelect = false;
        _articlesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _articlesGrid.RowHeadersVisible = false;
        _articlesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _articlesGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _articlesGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _articlesGrid.EnableHeadersVisualStyles = false;
        _articlesGrid.RowTemplate.Height = 34;
        _articlesGrid.ColumnHeadersHeight = 38;
        _articlesGrid.ColumnHeadersDefaultCellStyle.BackColor = UiPalette.Lavender;
        _articlesGrid.ColumnHeadersDefaultCellStyle.ForeColor = UiPalette.Text;
        _articlesGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
        _articlesGrid.DefaultCellStyle.BackColor = UiPalette.Panel;
        _articlesGrid.DefaultCellStyle.ForeColor = UiPalette.Text;
        _articlesGrid.DefaultCellStyle.SelectionBackColor = UiPalette.LavenderSoft;
        _articlesGrid.DefaultCellStyle.SelectionForeColor = UiPalette.Text;
        _articlesGrid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        _articlesGrid.AlternatingRowsDefaultCellStyle.BackColor = UiPalette.LavenderWash;
        _articlesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Date",
            DataPropertyName = nameof(ArticleRow.PublishedAt),
            FillWeight = 20,
            MinimumWidth = 130
        });
        _articlesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Titre",
            DataPropertyName = nameof(ArticleRow.Title),
            FillWeight = 55,
            MinimumWidth = 260
        });
        _articlesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "URL",
            DataPropertyName = nameof(ArticleRow.Url),
            FillWeight = 45,
            MinimumWidth = 260
        });
        _articlesGrid.SelectionChanged += (_, _) => ShowSelectedArticle();

        _contentText.Dock = DockStyle.Fill;
        _contentText.Multiline = true;
        _contentText.ReadOnly = true;
        _contentText.ScrollBars = ScrollBars.Vertical;
        _contentText.Font = new Font("Yu Gothic UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        _contentText.BackColor = UiPalette.Panel;
        _contentText.ForeColor = UiPalette.Text;
        _contentText.BorderStyle = BorderStyle.FixedSingle;

        var detailLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = UiPalette.Window
        };
        detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        detailLayout.Controls.Add(_contentText, 0, 0);
        detailLayout.Controls.Add(CreateVocabularyPanel(), 1, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            BackColor = UiPalette.Window,
            SplitterWidth = 8
        };
        split.Panel1.Controls.Add(_articlesGrid);
        split.Panel2.Controls.Add(detailLayout);
        split.Panel1.Padding = new Padding(0, 8, 0, 4);
        split.Panel2.Padding = new Padding(0, 4, 0, 0);

        var status = new StatusStrip
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.LavenderWash,
            ForeColor = UiPalette.MutedText,
            SizingGrip = false
        };
        _statusLabel.Text = "Pret.";
        _statusLabel.ForeColor = UiPalette.MutedText;
        status.Items.Add(_statusLabel);

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(split, 0, 1);
        root.Controls.Add(status, 0, 2);
        Controls.Add(root);

        _articlesGrid.DataSource = _rows;
        _statusLabel.Text = $"Pret pour le {SelectedDateText()}.";
        UpdateVocabulary(null);
    }

    private Control CreateVocabularyPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10, 0, 0, 0),
            BackColor = UiPalette.Window
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Vocabulaire",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = UiPalette.LavenderDark,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _vocabularyTabs.Dock = DockStyle.Fill;
        _vocabularyTabs.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        _vocabularyTabs.Controls.Add(CreateVocabularyTab("Verbes", _verbList, UiPalette.LavenderWash));
        _vocabularyTabs.Controls.Add(CreateVocabularyTab("Noms propres", _properNounList, UiPalette.MintWash));
        _vocabularyTabs.Controls.Add(CreateVocabularyTab("Autres", _otherWordList, UiPalette.Panel));

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(_vocabularyTabs, 0, 1);
        return panel;
    }

    private static TabPage CreateVocabularyTab(string title, ListBox listBox, Color background)
    {
        var tab = new TabPage(title)
        {
            BackColor = background,
            Padding = new Padding(8)
        };

        listBox.Dock = DockStyle.Fill;
        listBox.BorderStyle = BorderStyle.None;
        listBox.BackColor = background;
        listBox.ForeColor = UiPalette.Text;
        listBox.Font = new Font("Yu Gothic UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        listBox.HorizontalScrollbar = true;
        listBox.IntegralHeight = false;

        tab.Controls.Add(listBox);
        return tab;
    }

    private static void StylePastelButton(Button button, Color fillColor, int width)
    {
        button.AutoSize = false;
        button.Size = new Size(width, 34);
        button.MinimumSize = new Size(width, 34);
        button.BackColor = fillColor;
        button.ForeColor = UiPalette.Text;
        button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = UiPalette.LavenderDark;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.TextAlign = ContentAlignment.MiddleCenter;
    }

    private async Task LoadArticlesAsync()
    {
        _loadButton.Enabled = false;
        _openButton.Enabled = false;
        _contentText.Clear();
        UpdateVocabulary(null);
        _rows = new BindingList<ArticleRow>();
        _articlesGrid.DataSource = _rows;

        var site = _siteCombo.SelectedIndex == 1 ? NhkSite.Easy : NhkSite.News;
        var targetDate = DateOnly.FromDateTime(_dateInput.Value.Date);
        var includeContent = _contentCheck.Checked;
        var progress = new Progress<string>(message => _statusLabel.Text = message);

        try
        {
            var articles = await _client.FetchArticlesAsync(site, targetDate, includeContent, progress);
            _rows = new BindingList<ArticleRow>(articles.Select(ArticleRow.FromArticle).ToList());
            _articlesGrid.DataSource = _rows;
            _statusLabel.Text = $"{_rows.Count} article(s) pour le {SelectedDateText()}.";

            if (_rows.Count > 0)
            {
                _articlesGrid.Rows[0].Selected = true;
                ShowSelectedArticle();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Erreur.";
            MessageBox.Show(this, ex.Message, "Erreur NHK", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _loadButton.Enabled = true;
        }
    }

    private string SelectedDateText()
    {
        return DateOnly.FromDateTime(_dateInput.Value.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private void ShowSelectedArticle()
    {
        var row = CurrentRow();
        _openButton.Enabled = row is not null;

        if (row is null)
        {
            _contentText.Clear();
            UpdateVocabulary(null);
            return;
        }

        var article = row.Article;
        var builder = new StringBuilder();
        builder.AppendLine(article.Title);

        if (!string.IsNullOrWhiteSpace(article.PublishedAt))
        {
            builder.AppendLine(article.PublishedAt);
        }

        builder.AppendLine(article.Url);
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(article.Content))
        {
            builder.AppendLine(article.Content);
        }
        else
        {
            builder.AppendLine("Le contenu n'a pas ete charge. Coche \"Charger contenu\" avant de recuperer la liste.");
        }

        if (article.ContentIsTruncated)
        {
            builder.AppendLine();
            builder.AppendLine("[Contenu partiel]");
        }

        _contentText.Text = builder.ToString();
        _contentText.SelectionStart = 0;
        _contentText.SelectionLength = 0;
        UpdateVocabulary(article);
    }

    private void UpdateVocabulary(NhkArticle? article)
    {
        _verbList.Items.Clear();
        _properNounList.Items.Clear();
        _otherWordList.Items.Clear();

        if (article is null)
        {
            AddPlaceholder(_otherWordList, "Selectionne un article.");
            return;
        }

        if (string.IsNullOrWhiteSpace(article.Content))
        {
            AddPlaceholder(_verbList, "Coche Charger contenu.");
            AddPlaceholder(_properNounList, "Coche Charger contenu.");
            AddPlaceholder(_otherWordList, "Le vocabulaire utilise le texte complet.");
            return;
        }

        var vocabulary = VocabularyExtractor.Extract(article.Content);
        FillVocabularyList(_verbList, vocabulary.Verbs, "Aucun verbe repere.");
        FillVocabularyList(_properNounList, vocabulary.ProperNouns, "Aucun nom propre repere.");
        FillVocabularyList(_otherWordList, vocabulary.OtherWords, "Aucun mot repere.");
    }

    private static void FillVocabularyList(
        ListBox listBox,
        IReadOnlyList<VocabularyEntry> entries,
        string emptyMessage)
    {
        if (entries.Count == 0)
        {
            AddPlaceholder(listBox, emptyMessage);
            return;
        }

        foreach (var entry in entries)
        {
            listBox.Items.Add(entry.Count > 1 ? $"{entry.Word}  ({entry.Count})" : entry.Word);
        }
    }

    private static void AddPlaceholder(ListBox listBox, string message)
    {
        listBox.Items.Add(message);
    }

    private void OpenSelectedArticle()
    {
        var row = CurrentRow();
        if (row is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(row.Article.Url) { UseShellExecute = true });
    }

    private ArticleRow? CurrentRow()
    {
        return _articlesGrid.CurrentRow?.DataBoundItem as ArticleRow;
    }

    private sealed class ArticleRow
    {
        public required string PublishedAt { get; init; }
        public required string Title { get; init; }
        public required string Url { get; init; }
        public required NhkArticle Article { get; init; }

        public static ArticleRow FromArticle(NhkArticle article)
        {
            return new ArticleRow
            {
                PublishedAt = article.PublishedAt ?? article.DatePublished ?? "",
                Title = article.Title,
                Url = article.Url,
                Article = article
            };
        }
    }
}

internal sealed class KawaiiToolbarPanel : FlowLayoutPanel
{
    public KawaiiToolbarPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? UiPalette.Window);

        var bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        using var path = RoundedRectangle(bounds, 16);
        using var shadow = RoundedRectangle(new Rectangle(bounds.X, bounds.Y + 2, bounds.Width, bounds.Height), 16);
        using var shadowBrush = new SolidBrush(Color.FromArgb(45, UiPalette.LavenderDark));
        using var fillBrush = new SolidBrush(UiPalette.Toolbar);
        using var borderPen = new Pen(UiPalette.Lavender, 1.4F);

        e.Graphics.FillPath(shadowBrush, shadow);
        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        using var dotBrush = new SolidBrush(Color.FromArgb(130, UiPalette.Candy));
        e.Graphics.FillEllipse(dotBrush, bounds.Right - 54, bounds.Top + 14, 7, 7);
        e.Graphics.FillEllipse(dotBrush, bounds.Right - 31, bounds.Top + 32, 5, 5);

        using var mintBrush = new SolidBrush(Color.FromArgb(150, UiPalette.Mint));
        e.Graphics.FillEllipse(mintBrush, bounds.Right - 72, bounds.Top + 35, 6, 6);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}

internal sealed record VocabularyEntry(string Word, int Count);

internal sealed record VocabularyResult(
    IReadOnlyList<VocabularyEntry> Verbs,
    IReadOnlyList<VocabularyEntry> ProperNouns,
    IReadOnlyList<VocabularyEntry> OtherWords);

internal static class VocabularyExtractor
{
    private const int MaxItemsPerCategory = 80;
    private const string Kanji = @"\u3400-\u9FFF\uF900-\uFAFF\u3005\u30F6";
    private const string Hiragana = @"\u3041-\u309F";
    private const string Katakana = @"\u30A1-\u30FA\u30FC";
    private static readonly Regex KatakanaWordRegex = new($@"[{Katakana}]{{3,}}", RegexOptions.Compiled);
    private static readonly Regex ProperWithTitleRegex = new(
        $@"[{Kanji}]{{2,8}}(?:\u3055\u3093|\u6C0F|\u9996\u76F8|\u5927\u7D71\u9818|\u77E5\u4E8B|\u5E02\u9577|\u753A\u9577|\u6751\u9577|\u8B70\u54E1|\u9078\u624B|\u76E3\u7763|\u793E\u9577|\u4F1A\u9577|\u6559\u6388|\u9577\u5B98|\u56FD\u5BB6\u4E3B\u5E2D|\u5BB9\u7591\u8005)",
        RegexOptions.Compiled);
    private static readonly Regex PlaceOrOrgRegex = new(
        $@"[{Kanji}]{{2,10}}(?:\u90FD|\u9053|\u5E9C|\u770C|\u5E02|\u533A|\u753A|\u6751|\u56FD|\u7701|\u5E81|\u515A|\u5927\u5B66|\u4F1A\u793E|\u9280\u884C|\u7A7A\u6E2F|\u99C5|\u8B66\u5BDF)",
        RegexOptions.Compiled);
    private static readonly Regex VerbCandidateRegex = new(
        $@"[{Kanji}{Hiragana}]{{2,18}}(?:\u3055\u308C\u3066|\u3055\u308C\u305F|\u3055\u308C\u308B|\u3057\u307E\u3057\u305F|\u3057\u307E\u3059|\u3057\u3066|\u3057\u305F|\u3059\u308B|\u3089\u308C\u3066|\u3089\u308C\u305F|\u3089\u308C\u308B|\u308C\u3066|\u308C\u305F|\u308C\u308B|\u306A\u304B\u3063\u305F|\u306A\u3044|\u307E\u3057\u305F|\u307E\u3059|\u307E\u305B\u3093|\u3066\u3044\u305F|\u3066\u3044\u308B|\u3044\u305F|\u3044\u308B|\u305F|\u3066|\u308B|\u3046|\u304F|\u3050|\u3059|\u3064|\u306C|\u3076|\u3080)",
        RegexOptions.Compiled);
    private static readonly Regex KanjiCompoundRegex = new($@"[{Kanji}]{{2,10}}", RegexOptions.Compiled);
    private static readonly Regex JapaneseRunRegex = new($@"[{Kanji}{Hiragana}{Katakana}]+", RegexOptions.Compiled);

    private static readonly string[] Particles =
    {
        "\u304B\u3089", "\u307E\u3067", "\u3088\u308A", "\u306A\u3069", "\u3067\u306F", "\u306B\u306F",
        "\u3068\u306F", "\u306E", "\u306F", "\u304C", "\u3092", "\u306B", "\u3067", "\u3068", "\u3082", "\u3078"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "\u3053\u308C", "\u305D\u308C", "\u3042\u308C", "\u3053\u3068", "\u3082\u306E", "\u305F\u3081",
        "\u3088\u3046", "\u3055\u3093", "\u3059\u308B", "\u3057\u305F", "\u3057\u3066", "\u3044\u308B",
        "\u3044\u305F", "\u307E\u3059", "\u3067\u3059", "\u306A\u3044"
    };

    public static VocabularyResult Extract(string text)
    {
        var properNouns = new Dictionary<string, int>(StringComparer.Ordinal);
        var verbs = new Dictionary<string, int>(StringComparer.Ordinal);
        var otherWords = new Dictionary<string, int>(StringComparer.Ordinal);

        AddMatches(properNouns, text, KatakanaWordRegex, CleanVocabularyWord);
        AddMatches(properNouns, text, ProperWithTitleRegex, CleanProperNoun);
        AddMatches(properNouns, text, PlaceOrOrgRegex, CleanVocabularyWord);
        AddMatches(verbs, text, VerbCandidateRegex, CleanVerbCandidate);
        AddMatches(otherWords, text, KanjiCompoundRegex, CleanVocabularyWord);
        AddMatches(otherWords, text, KatakanaWordRegex, CleanVocabularyWord);

        RemoveCategoryOverlap(otherWords, properNouns);
        RemoveCategoryOverlap(otherWords, verbs);
        RemoveCategoryOverlap(verbs, properNouns);

        return new VocabularyResult(
            ToEntries(verbs),
            ToEntries(properNouns),
            ToEntries(otherWords));
    }

    private static void AddMatches(
        IDictionary<string, int> target,
        string text,
        Regex regex,
        Func<string, string?> normalizer)
    {
        foreach (Match match in regex.Matches(text))
        {
            var word = normalizer(match.Value);
            if (!IsUsefulWord(word))
            {
                continue;
            }

            target[word!] = target.TryGetValue(word!, out var count) ? count + 1 : 1;
        }
    }

    private static string? CleanProperNoun(string value)
    {
        var word = CleanVocabularyWord(value);
        if (word is null)
        {
            return null;
        }

        foreach (var suffix in new[]
        {
            "\u3055\u3093", "\u6C0F", "\u9996\u76F8", "\u5927\u7D71\u9818", "\u77E5\u4E8B",
            "\u5E02\u9577", "\u753A\u9577", "\u6751\u9577", "\u8B70\u54E1", "\u9078\u624B",
            "\u76E3\u7763", "\u793E\u9577", "\u4F1A\u9577", "\u6559\u6388", "\u9577\u5B98",
            "\u56FD\u5BB6\u4E3B\u5E2D", "\u5BB9\u7591\u8005"
        })
        {
            if (word.EndsWith(suffix, StringComparison.Ordinal) && word.Length > suffix.Length + 1)
            {
                return word[..^suffix.Length];
            }
        }

        return word;
    }

    private static string? CleanVerbCandidate(string value)
    {
        var word = CleanVocabularyWord(value);
        if (word is null)
        {
            return null;
        }

        foreach (var particle in Particles)
        {
            var index = word.LastIndexOf(particle, StringComparison.Ordinal);
            if (index >= 0 && index + particle.Length < word.Length - 1)
            {
                word = word[(index + particle.Length)..];
            }
        }

        if (!ContainsAny(word, Hiragana) || word.Length < 2 || word.Length > 16)
        {
            return null;
        }

        return word;
    }

    private static string? CleanVocabularyWord(string value)
    {
        var word = value.Trim();
        word = Regex.Replace(word, @"[\s\u3000]+", "");
        return word;
    }

    private static bool IsUsefulWord(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        if (word.Length < 2 || StopWords.Contains(word))
        {
            return false;
        }

        return JapaneseRunRegex.IsMatch(word);
    }

    private static void RemoveCategoryOverlap(
        IDictionary<string, int> lowerPriority,
        IDictionary<string, int> higherPriority)
    {
        foreach (var word in higherPriority.Keys)
        {
            lowerPriority.Remove(word);
        }
    }

    private static IReadOnlyList<VocabularyEntry> ToEntries(IDictionary<string, int> counts)
    {
        return counts
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key.Length)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Take(MaxItemsPerCategory)
            .Select(item => new VocabularyEntry(item.Key, item.Value))
            .ToList();
    }

    private static bool ContainsAny(string value, string unicodeRange)
    {
        return Regex.IsMatch(value, $"[{unicodeRange}]");
    }
}

internal sealed class NhkClient : IDisposable
{
    private static readonly Uri NewsUrl = new("https://news.web.nhk/newsweb");
    private static readonly Uri EasyUrl = new("https://news.web.nhk/news/easy/");
    private static readonly Uri EasyListUrl = new("https://news.web.nhk/news/easy/news-list.json");
    private static readonly Uri AuthBaseUrl = new("https://news.web.nhk/tix/build_authorize");
    private static readonly Uri NewsArticleApiBase = new("https://api.web.nhk/r8/t/newsarticle/");
    private static readonly Regex NewsArticlePathRegex = new(
        @"^/newsweb/[a-z]{2}/[a-z]{2}-[a-z0-9]+/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NewsLinkRegex = new(
        @"(?is)<a\b[^>]*href\s*=\s*[""'](?<href>[^""']*/newsweb/[a-z]{2}/[a-z]{2}-[a-z0-9]+[^""']*)[""'][^>]*>(?<body>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"(?is)<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex NewsDateRegex = new(@"\d{1,2}月\d{1,2}日\s+\d{1,2}:\d{2}", RegexOptions.Compiled);
    private static readonly Regex EasyDateRegex = new(@"^\d{4}年\d{1,2}月\d{1,2}日\s+\d{1,2}時\d{1,2}分$", RegexOptions.Compiled);

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;

    public NhkClient()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("kotoba-nhk-gui/0.1");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ja-JP");
    }

    public async Task<List<NhkArticle>> FetchArticlesAsync(
        NhkSite site,
        DateOnly targetDate,
        bool includeContent,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await FetchArticlesWithPythonAsync(site, targetDate, includeContent, progress, cancellationToken);
    }

    private static async Task<List<NhkArticle>> FetchArticlesWithPythonAsync(
        NhkSite site,
        DateOnly targetDate,
        bool includeContent,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        const string pythonCommand = "python3";
        var projectRoot = FindProjectRoot();
        var siteArgument = site == NhkSite.Easy ? "easy" : "news";

        var dateArgument = targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        progress?.Report($"Lancement de {pythonCommand} main.py pour {dateArgument}...");

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonCommand,
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("main.py");
        startInfo.ArgumentList.Add("--site");
        startInfo.ArgumentList.Add(siteArgument);
        startInfo.ArgumentList.Add("--date");
        startInfo.ArgumentList.Add(dateArgument);
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");

        if (includeContent)
        {
            startInfo.ArgumentList.Add("--with-content");
        }

        using var process = StartPythonProcess(startInfo);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException(
                $"{pythonCommand} main.py a echoue avec le code {process.ExitCode}.{Environment.NewLine}{details.Trim()}");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException($"{pythonCommand} main.py n'a renvoye aucun JSON.");
        }

        progress?.Report("Lecture du JSON Python...");
        return ParsePythonArticlesJson(output);
    }

    private static Process StartPythonProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException("Le processus python3 n'a pas pu etre cree.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Impossible de lancer python3. Verifie que la commande `python3 --version` fonctionne dans le meme terminal Windows que `dotnet run`.",
                ex);
        }
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "main.py")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static List<NhkArticle> ParsePythonArticlesJson(string json)
    {
        var trimmed = json.Trim('\uFEFF', ' ', '\r', '\n', '\t');
        using var document = JsonDocument.Parse(trimmed);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Le script Python n'a pas renvoye une liste JSON.");
        }

        var articles = new List<NhkArticle>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var title = GetStringProperty(item, "title");
            var url = GetStringProperty(item, "url");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var contentIsTruncated = false;
            if (TryGetProperty(item, "content_is_truncated", out var truncated)
                && truncated.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                contentIsTruncated = truncated.GetBoolean();
            }

            articles.Add(new NhkArticle
            {
                Site = GetStringProperty(item, "site") ?? "",
                Title = title,
                Url = url,
                PublishedAt = GetStringProperty(item, "published_at"),
                ImageUrl = GetStringProperty(item, "image_url"),
                Content = NormalizePythonContent(ExtractJsonTextProperty(item, "content")),
                ContentIsTruncated = contentIsTruncated,
                ApiUrl = GetStringProperty(item, "api_url"),
                DatePublished = GetStringProperty(item, "date_published"),
                DateModified = GetStringProperty(item, "date_modified")
            });
        }

        return articles;
    }

    private static string? NormalizePythonContent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private async Task<List<NhkArticle>> FetchNewsArticlesAsync(CancellationToken cancellationToken)
    {
        var html = await GetStringAsync(NewsUrl, cancellationToken: cancellationToken);
        var articles = ParseNewsArticles(html);

        if (articles.Count == 0)
        {
            throw new InvalidOperationException("Aucun article classique n'a ete trouve sur NHK News Web.");
        }

        return articles;
    }

    private async Task<List<NhkArticle>> FetchEasyArticlesAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Creation de la session NHK...");
        var token = await TryGetAccountlessTokenAsync(EasyUrl.ToString(), cancellationToken);

        progress?.Report("Lecture de la liste NHK Easy...");
        var json = await GetStringAsync(
            EasyListUrl,
            bearerToken: token,
            referer: EasyUrl,
            cancellationToken: cancellationToken);

        var articles = ParseEasyArticles(json);
        if (articles.Count == 0)
        {
            throw new InvalidOperationException("Aucun article Easy n'a ete trouve. Verifie que le VPN Japon est actif si NHK bloque la requete.");
        }

        return articles;
    }

    private async Task EnrichNewsArticlesAsync(
        IReadOnlyList<NhkArticle> articles,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Creation de la session NHK...");
        var token = await TryGetAccountlessTokenAsync(articles[0].Url, cancellationToken);

        for (var index = 0; index < articles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var article = articles[index];
            progress?.Report($"Contenu classique {index + 1}/{articles.Count}...");

            try
            {
                var apiUrl = BuildNewsApiUrl(article.Url);
                article.ApiUrl = apiUrl?.ToString();

                if (apiUrl is null)
                {
                    article.ContentIsTruncated = true;
                    continue;
                }

                var json = await GetStringAsync(
                    apiUrl,
                    bearerToken: token,
                    referer: new Uri(article.Url),
                    cancellationToken: cancellationToken);

                UpdateArticleFromNewsJson(article, json);
            }
            catch (Exception ex)
            {
                article.Content = $"Impossible de charger le contenu: {ex.Message}";
                article.ContentIsTruncated = true;
            }
        }
    }

    private async Task EnrichEasyArticlesAsync(
        IReadOnlyList<NhkArticle> articles,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Creation de la session NHK...");
        var token = await TryGetAccountlessTokenAsync(EasyUrl.ToString(), cancellationToken);

        for (var index = 0; index < articles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var article = articles[index];
            progress?.Report($"Contenu Easy {index + 1}/{articles.Count}...");

            try
            {
                var html = await GetStringAsync(
                    new Uri(article.Url),
                    bearerToken: token,
                    referer: EasyUrl,
                    cancellationToken: cancellationToken);

                var content = ParseEasyArticleContent(html);
                if (string.IsNullOrWhiteSpace(content))
                {
                    article.ContentIsTruncated = true;
                }
                else
                {
                    article.Content = content;
                    article.ContentIsTruncated = false;
                }
            }
            catch (Exception ex)
            {
                article.Content = $"Impossible de charger le contenu: {ex.Message}";
                article.ContentIsTruncated = true;
            }
        }
    }

    private async Task<string?> TryGetAccountlessTokenAsync(
        string redirectUrl,
        CancellationToken cancellationToken)
    {
        SetConsentCookie();

        var target = redirectUrl.Contains('?', StringComparison.Ordinal)
            ? $"{redirectUrl}&ctu=in"
            : $"{redirectUrl}?ctu=in";
        var authUrl = new Uri($"{AuthBaseUrl}?idp=r-alaz&profileType=anonymous&redirect_uri={Uri.EscapeDataString(target)}");

        using var request = new HttpRequestMessage(HttpMethod.Get, authUrl);
        request.Headers.Referrer = NewsUrl;
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return GetCookieValue("z_at", NewsUrl);
    }

    private void SetConsentCookie()
    {
        var consent = JsonSerializer.Serialize(new
        {
            areaId = "270",
            areaName = "",
            areaNameEn = "",
            fuken = "27",
            jisx0402 = "27128",
            postal = "5408501"
        });
        var cookieValue = Uri.EscapeDataString(consent);
        var cookie = new Cookie("consentToUse", cookieValue, "/", "news.web.nhk")
        {
            Expires = DateTime.Now.AddYears(1)
        };

        _cookies.Add(NewsUrl, cookie);
    }

    private string? GetCookieValue(string name, Uri uri)
    {
        foreach (Cookie cookie in _cookies.GetCookies(uri))
        {
            if (string.Equals(cookie.Name, name, StringComparison.Ordinal))
            {
                return cookie.Value;
            }
        }

        return null;
    }

    private async Task<string> GetStringAsync(
        Uri uri,
        string? bearerToken = null,
        Uri? referer = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (referer is not null)
        {
            request.Headers.Referrer = referer;
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');

        try
        {
            var encoding = string.IsNullOrWhiteSpace(charset)
                ? Encoding.UTF8
                : Encoding.GetEncoding(charset);
            return encoding.GetString(bytes);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static List<NhkArticle> ParseNewsArticles(string html)
    {
        var articlesByUrl = new Dictionary<string, NhkArticle>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in NewsLinkRegex.Matches(html))
        {
            var url = NormalizeNewsUrl(match.Groups["href"].Value);
            if (url is null)
            {
                continue;
            }

            var body = match.Groups["body"].Value;
            var textParts = ExtractTextParts(body);
            var title = ChooseNewsTitle(textParts, url);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var publishedAt = ExtractNewsPublishedAt(textParts);
            var imageUrl = ExtractFirstAttributeUrl(body, "src");

            if (!articlesByUrl.TryGetValue(url, out var article))
            {
                articlesByUrl[url] = new NhkArticle
                {
                    Site = "news",
                    Title = title,
                    Url = url,
                    PublishedAt = publishedAt,
                    ImageUrl = imageUrl
                };
                continue;
            }

            if (string.IsNullOrWhiteSpace(article.PublishedAt) && !string.IsNullOrWhiteSpace(publishedAt))
            {
                article.PublishedAt = publishedAt;
            }

            if (string.IsNullOrWhiteSpace(article.ImageUrl) && !string.IsNullOrWhiteSpace(imageUrl))
            {
                article.ImageUrl = imageUrl;
            }

            if (IsBetterTitle(title, article.Title))
            {
                article.Title = title;
            }
        }

        return articlesByUrl.Values.ToList();
    }

    private static List<NhkArticle> ParseEasyArticles(string json)
    {
        using var document = JsonDocument.Parse(json);
        var articles = new List<NhkArticle>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return articles;
        }

        foreach (var dateGroup in document.RootElement.EnumerateArray())
        {
            if (dateGroup.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var property in dateGroup.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (TryGetProperty(item, "news_display_flag", out var display)
                        && display.ValueKind == JsonValueKind.False)
                    {
                        continue;
                    }

                    var id = GetStringProperty(item, "news_id");
                    var title = GetStringProperty(item, "title");
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var url = BuildEasyArticleUrl(id);
                    if (!seen.Add(url))
                    {
                        continue;
                    }

                    articles.Add(new NhkArticle
                    {
                        Site = "easy",
                        Title = CleanInlineText(title),
                        Url = url,
                        PublishedAt = GetStringProperty(item, "news_prearranged_time", "news_publication_time", "news_creation_time"),
                        ImageUrl = GetStringProperty(item, "news_easy_image_uri", "news_web_image_uri"),
                        ApiUrl = EasyListUrl.ToString()
                    });
                }
            }
        }

        return articles;
    }

    private static void UpdateArticleFromNewsJson(NhkArticle article, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        article.Title = FirstNonEmpty(
            GetStringProperty(root, "headline"),
            GetStringProperty(root, "name"),
            GetStringProperty(root, "title"),
            article.Title) ?? article.Title;

        article.DatePublished = FirstNonEmpty(
            GetStringProperty(root, "datePublished"),
            GetStringProperty(root, "date_published"),
            article.DatePublished);

        article.DateModified = FirstNonEmpty(
            GetStringProperty(root, "dateModified"),
            GetStringProperty(root, "date_modified"),
            article.DateModified);

        article.ImageUrl = FirstNonEmpty(
            ExtractImageUrl(root),
            article.ImageUrl);

        var content = FirstNonEmpty(
            ExtractJsonTextProperty(root, "articleBody"),
            ExtractJsonTextProperty(root, "detailedArticleBody"),
            ExtractJsonTextProperty(root, "abstract"),
            ExtractJsonTextProperty(root, "description"));

        if (string.IsNullOrWhiteSpace(content))
        {
            article.ContentIsTruncated = true;
            return;
        }

        article.Content = NormalizeArticleText(content);
        article.ContentIsTruncated =
            HasProperty(root, "articleBody") is false
            && HasProperty(root, "detailedArticleBody") is false;
    }

    private static string ParseEasyArticleContent(string html)
    {
        var cleaned = Regex.Replace(html, @"(?is)<(script|style|noscript)\b[^>]*>.*?</\1>", " ");
        cleaned = Regex.Replace(cleaned, @"(?is)<rt\b[^>]*>.*?</rt>", "");
        cleaned = Regex.Replace(cleaned, @"(?is)<rp\b[^>]*>.*?</rp>", "");

        var paragraphs = new List<string>();
        foreach (Match match in Regex.Matches(cleaned, @"(?is)<p\b[^>]*>(?<body>.*?)</p>"))
        {
            var text = HtmlToPlainText(match.Groups["body"].Value);
            if (ShouldSkipEasyParagraph(text))
            {
                continue;
            }

            if (IsEasyFooter(text))
            {
                break;
            }

            paragraphs.Add(text);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
    }

    private static bool ShouldSkipEasyParagraph(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            || text.Contains("読みこみ中", StringComparison.Ordinal)
            || EasyDateRegex.IsMatch(text)
            || text.StartsWith("NEWS WEB EASY", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEasyFooter(string text)
    {
        return text.StartsWith("ニュースをさがす", StringComparison.Ordinal)
            || text.StartsWith("このページでは", StringComparison.Ordinal)
            || text.StartsWith("みんなの", StringComparison.Ordinal)
            || text.StartsWith("News Up", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("NHK", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri? BuildNewsApiUrl(string articleUrl)
    {
        if (!Uri.TryCreate(articleUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !string.Equals(parts[0], "newsweb", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new Uri(NewsArticleApiBase, $"{parts[1]}/{parts[2]}.json");
    }

    private static string BuildEasyArticleUrl(string newsId)
    {
        return new Uri(EasyUrl, $"{newsId}/{newsId}.html").ToString();
    }

    private static string? NormalizeNewsUrl(string href)
    {
        var decoded = WebUtility.HtmlDecode(href);
        if (!Uri.TryCreate(NewsUrl, decoded, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Host, NewsUrl.Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!NewsArticlePathRegex.IsMatch(uri.AbsolutePath))
        {
            return null;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1,
            Query = "",
            Fragment = "",
            Path = uri.AbsolutePath.TrimEnd('/')
        };

        return builder.Uri.ToString();
    }

    private static List<string> ExtractTextParts(string html)
    {
        var parts = new List<string>();

        foreach (Match match in Regex.Matches(html, @"(?is)\b(?:alt|aria-label|title)\s*=\s*[""'](?<text>[^""']+)[""']"))
        {
            AddCleanPart(parts, match.Groups["text"].Value);
        }

        var withoutComments = Regex.Replace(html, @"(?is)<!--.*?-->", " ");
        withoutComments = Regex.Replace(withoutComments, @"(?i)<br\s*/?>", "\n");
        var text = HtmlTagRegex.Replace(withoutComments, "\n");
        foreach (var part in text.Split('\n'))
        {
            AddCleanPart(parts, part);
        }

        return parts;
    }

    private static void AddCleanPart(ICollection<string> parts, string value)
    {
        var text = CleanInlineText(WebUtility.HtmlDecode(value));
        if (!string.IsNullOrWhiteSpace(text))
        {
            parts.Add(text);
        }
    }

    private static string? ExtractFirstAttributeUrl(string html, string attributeName)
    {
        var match = Regex.Match(
            html,
            $@"(?is)\b{Regex.Escape(attributeName)}\s*=\s*[""'](?<url>[^""']+)[""']");

        if (!match.Success)
        {
            return null;
        }

        var value = WebUtility.HtmlDecode(match.Groups["url"].Value);
        return Uri.TryCreate(NewsUrl, value, out var uri) ? uri.ToString() : value;
    }

    private static string? ExtractNewsPublishedAt(IEnumerable<string> textParts)
    {
        foreach (var text in textParts)
        {
            var match = NewsDateRegex.Match(text);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    private static string ChooseNewsTitle(IReadOnlyList<string> textParts, string url)
    {
        foreach (var text in textParts)
        {
            var candidate = CleanNewsTitle(text);
            if (IsNewsTitleCandidate(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFileName(new Uri(url).AbsolutePath);
    }

    private static bool IsBetterTitle(string candidate, string current)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        if (!IsNewsTitleCandidate(current) && IsNewsTitleCandidate(candidate))
        {
            return true;
        }

        return candidate.Length > current.Length && current.Length < 8;
    }

    private static string CleanNewsTitle(string value)
    {
        var text = CleanInlineText(value);
        text = Regex.Replace(text, @"\(\d{1,2}:\d{2}\)$", "").Trim();
        return text;
    }

    private static bool IsNewsTitleCandidate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.Length < 2)
        {
            return false;
        }

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "JUST IN",
            "NEWS WEB",
            "NHK",
            "ニュース",
            "動画",
            "配信中",
            "一覧へ",
            "もっと見る"
        };

        if (excluded.Contains(text))
        {
            return false;
        }

        return !NewsDateRegex.IsMatch(text)
            && !Regex.IsMatch(text, @"^\d{1,2}:\d{2}$")
            && !Regex.IsMatch(text, @"^\(\d{1,2}:\d{2}\)$");
    }

    private static string HtmlToPlainText(string html)
    {
        var withBreaks = Regex.Replace(html, @"(?i)<br\s*/?>", "\n");
        var withoutTags = HtmlTagRegex.Replace(withBreaks, " ");
        return CleanInlineText(WebUtility.HtmlDecode(withoutTags));
    }

    private static string NormalizeArticleText(string raw)
    {
        var text = WebUtility.HtmlDecode(raw);
        text = Regex.Replace(text, @"(?i)<br\s*/?>", "\n");
        text = HtmlTagRegex.Replace(text, "\n");
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var lines = text
            .Split('\n')
            .Select(CleanInlineText)
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static string CleanInlineText(string value)
    {
        var text = value.Replace('\u00a0', ' ');
        text = Regex.Replace(text, @"[\t \u3000]+", " ");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static string? GetStringProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var property))
            {
                var value = ExtractJsonText(property);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return CleanInlineText(value);
                }
            }
        }

        return null;
    }

    private static string? ExtractJsonTextProperty(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var property))
        {
            return null;
        }

        return ExtractJsonText(property);
    }

    private static string? ExtractJsonText(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.ToString();
            case JsonValueKind.Array:
                return JoinJsonTexts(element.EnumerateArray().Select(ExtractJsonText));
            case JsonValueKind.Object:
                foreach (var name in new[] { "@value", "value", "text", "body", "html", "plainText", "markedBody", "content" })
                {
                    if (TryGetProperty(element, name, out var property))
                    {
                        var text = ExtractJsonText(property);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }

                return JoinJsonTexts(element.EnumerateObject().Select(property => ExtractJsonText(property.Value)));
            default:
                return null;
        }
    }

    private static string? JoinJsonTexts(IEnumerable<string?> values)
    {
        var parts = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        return parts.Count == 0
            ? null
            : string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static string? ExtractImageUrl(JsonElement root)
    {
        if (!TryGetProperty(root, "image", out var image))
        {
            return null;
        }

        if (image.ValueKind == JsonValueKind.String)
        {
            return image.GetString();
        }

        if (image.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in image.EnumerateArray())
            {
                var url = ExtractImageUrlValue(item);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }

        return ExtractImageUrlValue(image);
    }

    private static string? ExtractImageUrlValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetStringProperty(element, "url", "src", "contentUrl");
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

        if (element.TryGetProperty(name, out property))
        {
            return true;
        }

        foreach (var item in element.EnumerateObject())
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static bool HasProperty(JsonElement element, string name)
    {
        return TryGetProperty(element, name, out _);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
