using SolidShineUi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using static SentinelsJson.App;
using SolidShineUi.Keyboard;
using System.Windows.Shell;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Diagnostics;
using System.Windows.Input;
using static SentinelsJson.CoreUtils;
using System.ComponentModel;

namespace SentinelsJson
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FlatWindow
    {
        /// <summary>Path to the AppData folder where settings are stored</summary>
        string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SentinelsJson");
        /// <summary>The path to the currently open file (if a file is loaded but this is empty, this means it is a new unsaved file)</summary>
        string filePath = "";
        /// <summary>Get or set if a file is currently open</summary>
        bool _sheetLoaded = false;
        /// <summary>The name of the character. This MUST match txtCharacterName's text</summary>
        string fileTitle = "";
        /// <summary>The title displayed in the title bar</summary>
        string displayedTitle = "";
        /// <summary>Get or set if a file has unsaved changes</summary>
        bool isDirty = false;
        /// <summary>Get or set if the app should automatically check for updates when you open it</summary>
        bool autoCheckUpdates = true;
        /// <summary>Get or set the date the app last auto checked for updates; only auto check once per day</summary>
        string lastAutoCheckDate = "2020-03-26";

        /// <summary>Get or set the current view ("tabs", "continuous", or "rawjson")</summary>
        string currentView = TABS_VIEW;

        /// <summary>Get or set if the tabbed/continuous view has changes not synced with text editor</summary>
        bool _isTabsDirty = false;
        /// <summary>Get or set if the text editor has changes not synced with the tabbed/continuous view</summary>
        bool _isEditorDirty = false;
        /// <summary>Get or set if a file is currently being opened (and sheet loaded in)</summary>
        bool _isUpdating = false;
        /// <summary>Generic cancellation token, use for lengthy cancellable processes</summary>
        CancellationTokenSource cts = new CancellationTokenSource();
        /// <summary>The search panel associated with the raw JSON editor</summary>
        SearchPanel.SearchPanel sp;
        /// <summary>Get or set if the sheet view is currently running calculations</summary>
        bool _isCalculating = false;
        /// <summary>The timer for the auto save feature. When it ticks, save the file.</summary>
        DispatcherTimer autoSaveTimer = new DispatcherTimer();
        /// <summary>The timer for displaying the "Saved" text in the top.</summary>
        DispatcherTimer saveDisplayTimer = new DispatcherTimer();

        /// <summary>set if the Notes tab is in Edit mode (true) or View mode (false) (if Markdown support is disabled, it is always in Edit mode)</summary>
        bool notesEdit = false;

        // these are stored here as the program doesn't display these values to the user directly
        UserData ud;
        string sheetid;
        Dictionary<string, string> abilities = new Dictionary<string, string>();
        Dictionary<string, string> potentials = new Dictionary<string, string>();
        Dictionary<string, string?> sheetSettings = new Dictionary<string, string?>();
        Dictionary<string, int> abModifiers = new Dictionary<string, int>();
        string powerStat = "PER";
        string? version;
        string skillList = "None";
        int cpPerLevel = 10;
        int cpLevel0 = 10;

        // keyboard/method data
        RoutedMethodRegistry mr = new RoutedMethodRegistry();
        KeyboardShortcutHandler ksh;
        KeyRegistry kr = new KeyRegistry();

        #region Constructor/ window events/ basic functions

        public MainWindow()
        {
            ud = new UserData(false);
            sheetid = "-1";
            bool isHighContrast = false;

            // if true, run SaveSettings at the end of this function, to avoid calling SaveSettings like 5 times at once
            bool updateSettings = false;

            //undoSetTimer.Interval = new TimeSpan(0, 0, 3);
            //undoSetTimer.Tick += UndoSetTimer_Tick;

            autoCheckUpdates = App.Settings.UpdateAutoCheck;
            lastAutoCheckDate = App.Settings.UpdateLastCheckDate;
            DateTime today = DateTime.Today;

            if (autoCheckUpdates && lastAutoCheckDate != today.Year + "-" + today.Month + "-" + today.Day)
            {
                lastAutoCheckDate = today.Year + "-" + today.Month + "-" + today.Day;
                App.Settings.UpdateLastCheckDate = lastAutoCheckDate;
                updateSettings = true;
            }
            else
            {
                autoCheckUpdates = false;
            }

            InitializeComponent();
            ksh = new KeyboardShortcutHandler(this);
            mr.FillFromMenu(menu);

            if (File.Exists(Path.Combine(appDataPath, "keyboard.xml")))
            {
                try
                {
                    ksh.LoadShortcutsFromFile(Path.Combine(appDataPath, "keyboard.xml"), mr);
                }
                catch (ArgumentException)
                {
                    ksh.LoadShortcutsFromList(DefaultKeySettings.CreateDefaultShortcuts(mr));
                }
            }
            else
            {
                ksh.LoadShortcutsFromList(DefaultKeySettings.CreateDefaultShortcuts(mr));
            }

            if (App.Settings.HighContrastTheme == NO_HIGH_CONTRAST)
            {
                App.ColorScheme = new ColorScheme(ColorsHelper.CreateFromHex(App.Settings.ThemeColor));
            }
            else
            {
                switch (App.Settings.HighContrastTheme)
                {
                    case "1":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.WhiteOnBlack);
                        isHighContrast = true;
                        break;
                    case "2":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.GreenOnBlack);
                        isHighContrast = true;
                        break;
                    case "3":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.BlackOnWhite);
                        isHighContrast = true;
                        break;
                    case "4":
                        App.ColorScheme = ColorScheme.CreateLightTheme();
                        break;
                    case "5":
                        App.ColorScheme = ColorScheme.CreateDarkTheme();
                        break;
                    default:
                        App.Settings.HighContrastTheme = NO_HIGH_CONTRAST;
                        App.ColorScheme = new ColorScheme(ColorsHelper.CreateFromHex(App.Settings.ThemeColor));
                        updateSettings = true;
                        break;
                }
            }

            SetupTabs();
            selTabs.IsEnabled = false;
            foreach (SelectableItem item in selTabs.Items.SelectedItems.OfType<SelectableItem>())
            {
                item.IsEnabled = false;
            }

            UpdateAppearance();

            ChangeView(App.Settings.StartView, false, true, false);

            if (App.Settings.RecentFiles.Count > 20)
            {
                // time to remove some old entries
                App.Settings.RecentFiles.Reverse();
                App.Settings.RecentFiles.RemoveRange(20, App.Settings.RecentFiles.Count - 20);
                App.Settings.RecentFiles.Reverse();
                updateSettings = true;
            }

            mnuIndent.IsChecked = App.Settings.IndentJsonData;
            mnuAutoCheck.IsChecked = App.Settings.UpdateAutoCheck;

            mnuFilename.IsChecked = App.Settings.PathInTitleBar;

            foreach (string file in App.Settings.RecentFiles)//.Reverse<string>())
            {
                AddRecentFile(file, false);
            }

            ShowHideToolbar(App.Settings.ShowToolbar);

            int autoSave = App.Settings.AutoSave;
            if (autoSave <= 0)
            {
                autoSaveTimer.IsEnabled = false;
            }
            else if (autoSave > 30)
            {
                autoSaveTimer.IsEnabled = false;
                autoSaveTimer.Interval = new TimeSpan(0, 30, 0);
                autoSaveTimer.IsEnabled = true;
            }
            else
            {
                autoSaveTimer.IsEnabled = false;
                autoSaveTimer.Interval = new TimeSpan(0, autoSave, 0);
                autoSaveTimer.IsEnabled = true;
            }

            saveDisplayTimer.Interval = new TimeSpan(0, 0, 15);

            autoSaveTimer.Tick += autoSaveTimer_Tick;
            saveDisplayTimer.Tick += saveDisplayTimer_Tick;

            // setup up raw JSON editor
            if (App.Settings.EditorSyntaxHighlighting && !isHighContrast)
            {
                using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SentinelsJson.Json.xshd"))
                {
                    if (s != null)
                    {
                        using XmlReader reader = new XmlTextReader(s);
                        txtEditRaw.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }
            else
            {
                using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SentinelsJson.None.xshd"))
                {
                    if (s != null)
                    {
                        using XmlReader reader = new XmlTextReader(s);
                        txtEditRaw.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }

            LoadEditorFontSettings();

            txtEditRaw.ShowLineNumbers = App.Settings.EditorLineNumbers;

            txtEditRaw.WordWrap = App.Settings.EditorWordWrap;
            mnuWordWrap.IsChecked = App.Settings.EditorWordWrap;

            SearchPanel.SearchPanel p = SearchPanel.SearchPanel.Install(txtEditRaw);
            p.FontFamily = SystemFonts.MessageFontFamily; // so it isn't a fixed-width font lol
            sp = p;
            sp.ColorScheme = App.ColorScheme;

            txtEditRaw.Encoding = new System.Text.UTF8Encoding(false);

            if (updateSettings)
            {
                SaveSettings();
            }
        }

        #region Other Base Functions

        /// <summary>
        /// Open a file into the editor. This is intended for when opening files from the command-line arguments.
        /// </summary>
        /// <param name="filename">The path to the file to open.</param>
        public void OpenFile(string filename)
        {
            LoadFile(filename, true);
        }

        /// <summary>
        /// Save the currently used settings to the settings.json file.
        /// </summary>
        /// <param name="updateFonts">If true, will also save the current editor font settings. This takes additional processing which may not be always needed.</param>
        void SaveSettings(bool updateFonts = false)
        {
            if (updateFonts)
            {
                SaveEditorFontSettings();
            }

            try
            {
                App.Settings.Save(Path.Combine(appDataPath, "settings.json"));
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("The settings file for SentinelsJson could not be saved. Please check the permissions for your AppData folder.",
                    "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Security.SecurityException)
            {
                MessageBox.Show("The settings file for SentinelsJson could not be saved. Please check the permissions for your AppData folder.",
                    "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException)
            {
                MessageBox.Show("The settings file for SentinelsJson could not be saved. Please check the permissions for your AppData folder.",
                    "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void ReloadSettings()
        {
            bool updateSettings = false;

            bool isHighContrast = false;

            if (App.Settings.HighContrastTheme == NO_HIGH_CONTRAST)
            {
                App.ColorScheme = new ColorScheme(ColorsHelper.CreateFromHex(App.Settings.ThemeColor));
            }
            else
            {
                switch (App.Settings.HighContrastTheme)
                {
                    case "1":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.WhiteOnBlack);
                        isHighContrast = true;
                        break;
                    case "2":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.GreenOnBlack);
                        isHighContrast = true;
                        break;
                    case "3":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.BlackOnWhite);
                        isHighContrast = true;
                        break;
                    case "4":
                        App.ColorScheme = ColorScheme.CreateLightTheme();
                        break;
                    case "5":
                        App.ColorScheme = ColorScheme.CreateDarkTheme();
                        break;
                    default:
                        App.Settings.HighContrastTheme = NO_HIGH_CONTRAST;
                        App.ColorScheme = new ColorScheme(ColorsHelper.CreateFromHex(App.Settings.ThemeColor));
                        updateSettings = true;
                        break;
                }
            }

            UpdateAppearance();
            UpdateTitlebar();

            mnuIndent.IsChecked = App.Settings.IndentJsonData;
            mnuAutoCheck.IsChecked = App.Settings.UpdateAutoCheck;

            mnuFilename.IsChecked = App.Settings.PathInTitleBar;

            if (App.Settings.RecentFiles.Count > 20)
            {
                // time to remove some old entries
                App.Settings.RecentFiles.Reverse();
                App.Settings.RecentFiles.RemoveRange(20, App.Settings.RecentFiles.Count - 20);
                App.Settings.RecentFiles.Reverse();
                updateSettings = true;
            }

            // clear recent files list in UI (not in backend)
            RebuildRecentMenu();

            ShowHideToolbar(App.Settings.ShowToolbar);

            int autoSave = App.Settings.AutoSave;
            if (autoSave <= 0)
            {
                autoSaveTimer.IsEnabled = false;
            }
            else if (autoSave > 30)
            {
                autoSaveTimer.IsEnabled = false;
                autoSaveTimer.Interval = new TimeSpan(0, 30, 0);
                autoSaveTimer.IsEnabled = true;
            }
            else
            {
                autoSaveTimer.IsEnabled = false;
                autoSaveTimer.Interval = new TimeSpan(0, autoSave, 0);
                autoSaveTimer.IsEnabled = true;
            }

            // setup up raw JSON editor
            if (App.Settings.EditorSyntaxHighlighting && !isHighContrast)
            {
                using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SentinelsJson.Json.xshd"))
                {
                    if (s != null)
                    {
                        using XmlReader reader = new XmlTextReader(s);
                        txtEditRaw.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }
            else
            {
                using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SentinelsJson.None.xshd"))
                {
                    if (s != null)
                    {
                        using XmlReader reader = new XmlTextReader(s);
                        txtEditRaw.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }

            LoadEditorFontSettings();

            txtEditRaw.ShowLineNumbers = App.Settings.EditorLineNumbers;

            txtEditRaw.WordWrap = App.Settings.EditorWordWrap;
            mnuWordWrap.IsChecked = App.Settings.EditorWordWrap;

            if (updateSettings)
            {
                SaveSettings();
            }
        }

        void UpdateTitlebar()
        {
            if (!_sheetLoaded)
            {
                Title = "Sentinels JSON";
                displayedTitle = "";
            }
            else
            {
                if (App.Settings.PathInTitleBar)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Title = "Sentinels JSON - New File";
                        displayedTitle = "New File";
                    }
                    else
                    {
                        Title = "Sentinels JSON - " + Path.GetFileName(filePath);
                        displayedTitle = Path.GetFileName(filePath);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(fileTitle))
                    {
                        Title = "Sentinels JSON - (unnamed character)";
                        displayedTitle = fileTitle;
                    }
                    else
                    {
                        Title = "Sentinels JSON - " + fileTitle;
                        displayedTitle = fileTitle;
                    }
                }
            }

            if (isDirty)
            {
                Title += " *";
            }
        }

        /// <summary>
        /// Set if the current sheet has unsaved changes. Also updates the title bar and by default sets the tabbed/continuous view as unsynced.
        /// </summary>
        /// <param name="isDirty">Set if the sheet is dirty (has unsaved changes).</param>
        /// <param name="updateInternalValues">Set if the value for the tabbed/continuous view should be marked as unsynced with the text editor.</param>
        void SetIsDirty(bool isDirty = true, bool updateInternalValues = true)
        {
            if (!_sheetLoaded) // if no sheet is loaded, nothing is gonna happen lol
            {
                isDirty = false;
            }

            bool update = isDirty != this.isDirty;

            if (isDirty)
            {
                this.isDirty = true;
                if (updateInternalValues) _isTabsDirty = true;
            }
            else
            {
                this.isDirty = false;
                if (updateInternalValues) _isTabsDirty = false;
            }

            if (update || fileTitle != displayedTitle) UpdateTitlebar();
        }

        private void autoSaveTimer_Tick(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // do not auto save if there is no file path already set up
                    //SaveAsFile();
                }
                else
                {
                    SaveFile(filePath);
                }
            });
        }

        private void saveDisplayTimer_Tick(object? sender, EventArgs e)
        {
            brdrSaved.Visibility = Visibility.Collapsed;
        }

        private void brdrSaved_MouseDown(object sender, MouseButtonEventArgs e)
        {
            brdrSaved.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Other Window event handlers

        private async void window_Loaded(object sender, RoutedEventArgs e)
        {
            if (autoCheckUpdates)
            {
                await CheckForUpdates(false);
            }
        }

        private void window_Activated(object sender, EventArgs e)
        {
            menu.Foreground = App.ColorScheme.ForegroundColor.ToBrush();
        }

        private void window_Deactivated(object sender, EventArgs e)
        {
            if (App.Settings.HighContrastTheme == NO_HIGH_CONTRAST)
            {
                menu.Foreground = ColorsHelper.CreateFromHex("#404040").ToBrush();
            }
        }

        private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!SaveDirtyChanges() || CheckCalculating())
            {
                e.Cancel = true;
            }
        }

        private void window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown(0);
        }

        #endregion

        #endregion

        #region File Menu

        private async void mnuNew_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveDirtyChanges() || CheckCalculating())
            {
                return;
            }

            NewSheet ns = new NewSheet();
            ns.Owner = this;
            ns.ShowDialog();

            if (ns.DialogResult)
            {
                SentinelsSheet ps = ns.Sheet;

                filePath = string.Empty;
                fileTitle = ps.Name;
                _sheetLoaded = true;

                isDirty = false;
                _isEditorDirty = false;
                _isTabsDirty = false;

                UpdateTitlebar();

                txtEditRaw.Text = ps.SaveJsonText(App.Settings.IndentJsonData);
                ChangeView(App.Settings.StartView, false, false);
                LoadSentinelsSheet(ps);
                await UpdateCalculations();
            }
        }

        private void mnuNewWindow_Click(object sender, RoutedEventArgs e)
        {
            string? filename = Process.GetCurrentProcess().MainModule?.FileName;
            if (filename == null)
            {
                MessageBox.Show("Cannot open another instance automatically.");
                return;
            }

            Process.Start(filename);
        }

        private void mnuOpen_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveDirtyChanges() || CheckCalculating())
            {
                return;
            }

            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Title = "Open JSON";
            ofd.Filter = "JSON Sheet|*.json|All Files|*.*";

            if (ofd.ShowDialog() ?? false == true)
            {
                LoadFile(ofd.FileName);
            }
        }

        private void mnuSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                SaveAsFile();
            }
            else
            {
                SaveFile(filePath);
            }
        }

        private void mnuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveAsFile();
        }

        private void mnuSheetSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowSheetSettings();
        }

        void SaveFile(string file)
        {
            if (CheckCalculating()) return;

            if (currentView == RAWJSON_VIEW)
            {
                // check if text is valid JSON first
                bool validJson = false;

                if (!string.IsNullOrEmpty(txtEditRaw.Text))
                {
                    try
                    {
                        SentinelsSheet ps = SentinelsSheet.LoadJsonText(txtEditRaw.Text);
                        validJson = true;
                    }
                    catch (Newtonsoft.Json.JsonReaderException) { }
                }

                if (!validJson)
                {
                    MessageDialog md = new MessageDialog(App.ColorScheme);
                    md.ShowDialog("The file's text doesn't seem to be valid JSON. Saving the file as it is may result in lost data or the file not being openable with this program in the future. Do you want to continue?",
                        null, this, "Invalid JSON Detected", MessageDialogButtonDisplay.Auto, image: MessageDialogImage.Warning, customOkButtonText: "Save anyway", customCancelButtonText: "Cancel");

                    if (md.DialogResult == MessageDialogResult.Cancel)
                    {
                        return;
                    }
                }
                txtEditRaw.Save(file);
                SyncSheetFromEditor();
            }
            else
            {
                SyncEditorFromSheet();
                txtEditRaw.Save(file);
                SentinelsSheet ss = CreateSentinelsSheet();
                ss.SaveJsonFile(file, App.Settings.IndentJsonData);
            }

            _isEditorDirty = false;
            _isTabsDirty = false;
            isDirty = false;
            UpdateTitlebar();

            brdrSaved.Visibility = Visibility.Visible;
            saveDisplayTimer.Start();
        }

        bool SaveAsFile()
        {
            if (!_sheetLoaded)
            {
                MessageDialog md = new MessageDialog(App.ColorScheme);
                md.ShowDialog("No sheet is currently open, so saving is not possible.", null, this, "No Sheet Open", MessageDialogButtonDisplay.Auto, image: MessageDialogImage.Error);
                return false;
            }

            Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Title = "Save JSON Sheet As";
            sfd.Filter = "JSON Sheet|*.json";

            if (sfd.ShowDialog() ?? false == true)
            {
                SaveFile(sfd.FileName);
                filePath = sfd.FileName;
                AddRecentFile(filePath, true);
                UpdateTitlebar();
                return true;
            }
            else
            {
                return false;
            }
        }

        void CloseFile()
        {
            if (!SaveDirtyChanges() || CheckCalculating())
            {
                return;
            }

            filePath = "";
            fileTitle = "";
            _sheetLoaded = false;
            SetIsDirty(false);
            txtEditRaw.Text = "";

            brdrSaved.Visibility = Visibility.Collapsed;
            saveDisplayTimer.Stop();

            ChangeView(App.Settings.StartView, false, true, false);
        }

        bool CheckCalculating()
        {
            if (_isCalculating)
            {
                MessageDialog md = new MessageDialog();
                md.ShowDialog("Cannot perform this function while the sheet is calculating. Please try again in just a moment.", App.ColorScheme, this, "Currently Calculating", MessageDialogButtonDisplay.Auto, image: MessageDialogImage.Error);
                return true;
            }
            else
            {
                return false;
            }
        }

        bool SaveDirtyChanges()
        {
            // if there's no sheet loaded, then there should be no dirty changes to save
            if (!_sheetLoaded)
            {
                return true;
            }

            if (isDirty)
            {
                MessageDialog md = new MessageDialog(App.ColorScheme);
                md.ShowDialog("The file has some unsaved changes. Do you want to save them first?", null, this, "Unsaved Changes", MessageDialogButtonDisplay.Three, MessageDialogImage.Question,
                    MessageDialogResult.Cancel, "Save", "Cancel", "Discard");

                if (md.DialogResult == MessageDialogResult.OK)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        return SaveAsFile();
                    }
                    else
                    {
                        SaveFile(filePath);
                        return true;
                    }
                }
                else if (md.DialogResult == MessageDialogResult.Discard)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        bool AskDiscard()
        {
            if (isDirty)
            {
                MessageDialog md = new MessageDialog(App.ColorScheme);
                md.ShowDialog("The file has some unsaved changes. Are you sure you want to discard them?", App.ColorScheme, this, "Unsaved Changes", MessageDialogButtonDisplay.Auto,
                    image: MessageDialogImage.Question, customOkButtonText: "Discard", customCancelButtonText: "Cancel");

                if (md.DialogResult == MessageDialogResult.OK || md.DialogResult == MessageDialogResult.Discard)
                {
                    // Discard and continue
                    return true;
                }
                else
                {
                    // Cancel the operation
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        bool ShowSheetSettings()
        {
            // if there's no sheet loaded, then there should be no dirty changes to save
            if (!_sheetLoaded)
            {
                MessageDialog md = new MessageDialog(App.ColorScheme);
                md.ShowDialog("No sheet is currently open.", null, this, "No Sheet Open", MessageDialogButtonDisplay.Auto, image: MessageDialogImage.Error);
                return false;
            }

            SheetSettings ss = new SheetSettings();
            ss.CpPerLevel = cpPerLevel;
            ss.Level0Cp = cpLevel0;
            ss.SkillList = skillList;
            ss.SheetSettingsList = sheetSettings;
            ss.UpdateUi();

            ss.ShowDialog();

            if (ss.DialogResult)
            {
                string origSkillList = skillList;

                cpPerLevel = ss.CpPerLevel;
                cpLevel0 = ss.Level0Cp;
                skillList = ss.SkillList;
                sheetSettings = ss.SheetSettingsList;

                // reload sheet sections

                // only update skills if the skillList has changed
                if (skillList != origSkillList)
                {
                    SentinelsSheet sh = CreateSentinelsSheet();
                    LoadSkills(sh);
                }

                SetIsDirty();

                return true;
            }
            else
            {
                return false;
            }
        }

        async Task CheckForUpdates(bool dialogIfNone = true)
        {
            try
            {
                UpdateData ud = await UpdateChecker.CheckForUpdatesAsync();
                if (ud.HasUpdate)
                {
                    UpdateDisplay uw = new UpdateDisplay(ud);
                    uw.Owner = this;
                    uw.ShowDialog();
                }
                else
                {
                    if (dialogIfNone)
                    {
                        MessageDialog md = new MessageDialog(App.ColorScheme);
                        md.ShowDialog("There are no updates available. You're on the latest release!", App.ColorScheme, this, "Check for Updates", MessageDialogButtonDisplay.Auto, image: MessageDialogImage.Hand);
                    }
                }
            }
            catch (System.Net.WebException)
            {
                if (dialogIfNone)
                {
                    MessageDialog md = new MessageDialog(App.ColorScheme);
                    md.ShowDialog("Could not check for updates. Make sure you're connected to the Internet.", App.ColorScheme, this, "Check for Updates", MessageDialogButtonDisplay.Auto, image: MessageDialogImage.Error);
                }
            }
        }

        private void mnuRevert_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                if (AskDiscard())
                {
                    LoadFile(filePath, false);
                }
            }
        }

        private void mnuIndent_Click(object sender, RoutedEventArgs e)
        {
            if (mnuIndent.IsChecked)
            {
                mnuIndent.IsChecked = false;
                App.Settings.IndentJsonData = false;
            }
            else
            {
                mnuIndent.IsChecked = true;
                App.Settings.IndentJsonData = true;
            }

            SaveSettings();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseFile();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Recent files

        void AddRecentFile(string filename, bool storeInSettings = true)
        {
            bool submenu = App.Settings.DisplayRecentActionsAsSubmenu;

            if (storeInSettings && App.Settings.RecentFiles.Contains(filename))
            {
                JumpList.AddToRecentCategory(filename);
                return;
            }

            MenuItem mi = new MenuItem();
            string name = Path.GetFileName(filename);
            mi.Header = "_" + name;
            ToolTip tt = new ToolTip
            {
                Content = filename,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                PlacementTarget = mi
            };
            mi.ToolTip = tt;
            mi.Tag = filename;
            mi.Click += MainRecentFileClick;
            mi.ContextMenuOpening += miRecentContext_Opening;
            mnuRecent.Items.Insert(0, mi);

            SolidShineUi.ContextMenu cm = new SolidShineUi.ContextMenu();
            if (!submenu)
            {
                cm.PlacementTarget = mi;
                cm.Width = 180;
            }
            else
            {
                tt.VerticalOffset = -25;
                tt.HorizontalOffset = 3;
            }

            CreateMenuItem("Open", miRecentOpen_Click);
            CreateMenuItem("Open in New Window", miRecentOpenNew_Click);
            CreateMenuItem("Copy Path", miRecentCopy_Click);
            CreateMenuItem("View in Explorer", miRecentView_Click);
            CreateMenuItem("Remove", miRecentRemove_Click);

            if (!submenu)
            {
                mi.ContextMenu = cm;
            }

            if (storeInSettings)
            {
                App.Settings.RecentFiles.Add(filename);
                JumpList.AddToRecentCategory(filename);
                SaveSettings();
            }

            mnuRecentEmpty.Visibility = Visibility.Collapsed;

            MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
            {
                MenuItem mii = new MenuItem();
                mii.Header = header;
                mii.Tag = mi;
                mii.Click += handler;
                if (submenu) mi.Items.Add(mii); else cm.Items.Add(mii);
                return mi;
            }
        }

        private void miRecentContext_Opening(object sender, ContextMenuEventArgs e)
        {
            if (sender is MenuItem m)
            {
                if (m.ContextMenu is SolidShineUi.ContextMenu cm)
                {
                    cm.ApplyColorScheme(App.ColorScheme);
                }
            }
        }

        private void MainRecentFileClick(object sender, RoutedEventArgs e)
        {
            if (App.Settings.DisplayRecentActionsAsSubmenu) return;

            miRecentFile_Click(sender, e);
        }

        private void miRecentFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender == null)
            {
                return;
            }

            if (sender is MenuItem m)
            {
                if (m.Tag is string file)
                {
                    if (File.Exists(file))
                    {
                        if (!SaveDirtyChanges() || CheckCalculating())
                        {
                            return;
                        }

                        LoadFile(file, false);
                    }
                    else
                    {
                        MessageDialog md = new MessageDialog(App.ColorScheme);
                        md.OkButtonText = "Cancel";
                        md.ShowDialog("This file does not seem to exist any more. Do you want to remove this file from the list or attempt to open anyway?", App.ColorScheme, this,
                            "File Not Found", MessageDialogButtonDisplay.Auto, MessageDialogImage.Error, MessageDialogResult.Cancel, "Remove file from list", "Attempt to open");
                        switch (md.DialogResult)
                        {
                            case MessageDialogResult.OK:
                                // do nothing
                                break;
                            case MessageDialogResult.Cancel:
                                // not reached?
                                break;
                            case MessageDialogResult.Extra1:
                                // remove file from list

                                List<FrameworkElement> itemsToRemove = new List<FrameworkElement>();

                                foreach (FrameworkElement? item in mnuRecent.Items)
                                {
                                    if (item != null && item == (sender as MenuItem))
                                    {
                                        itemsToRemove.Add(item);
                                    }
                                }

                                foreach (var item in itemsToRemove)
                                {
                                    mnuRecent.Items.Remove(item);
                                }

                                App.Settings.RecentFiles.Remove(file);
                                SaveSettings();

                                if (App.Settings.RecentFiles.Count == 0)
                                {
                                    mnuRecentEmpty.Visibility = Visibility.Visible;
                                }

                                break;
                            case MessageDialogResult.Extra2:
                                // attempt to open anyway
                                if (!SaveDirtyChanges() || CheckCalculating())
                                {
                                    return;
                                }

                                LoadFile(file, false);
                                break;
                            case MessageDialogResult.Extra3:
                                // not reached
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        private void mnuRecentClear_Click(object sender, RoutedEventArgs e)
        {
            if (AskClearRecentList())
            {
                App.Settings.RecentFiles.Clear();
                SaveSettings();

                List<FrameworkElement> itemsToRemove = new List<FrameworkElement>();

                foreach (FrameworkElement? item in mnuRecent.Items)
                {

                    if (item is MenuItem)
                    {
                        if (item.Tag != null)
                        {
                            itemsToRemove.Add(item);
                        }
                    }
                }

                foreach (var item in itemsToRemove)
                {
                    mnuRecent.Items.Remove(item);
                }

                mnuRecentEmpty.Visibility = Visibility.Visible;
            }
        }

        private void mnuRecentActions_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.DisplayRecentActionsAsSubmenu = !App.Settings.DisplayRecentActionsAsSubmenu;

            SaveSettings();
            RebuildRecentMenu();
        }

        bool AskClearRecentList()
        {
            MessageDialog md = new MessageDialog(App.ColorScheme);
            md.ShowDialog("Are you sure you want to remove all files from the Recent Files list?", App.ColorScheme, this, "Clear Recent Files List", MessageDialogButtonDisplay.Two,
                MessageDialogImage.Question, MessageDialogResult.Cancel, "Yes", "Cancel");

            if (md.DialogResult == MessageDialogResult.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        void RebuildRecentMenu()
        {
            List<FrameworkElement> itemsToRemove = new List<FrameworkElement>();

            foreach (FrameworkElement? item in mnuRecent.Items)
            {

                if (item is MenuItem)
                {
                    if (item.Tag != null)
                    {
                        itemsToRemove.Add(item);
                    }
                }
            }

            foreach (var item in itemsToRemove)
            {
                mnuRecent.Items.Remove(item);
            }

            foreach (string file in App.Settings.RecentFiles)//.Reverse<string>())
            {
                AddRecentFile(file, false);
            }

            mnuRecentActions.IsChecked = App.Settings.DisplayRecentActionsAsSubmenu;
        }

        private void miRecentOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                if (mi.Tag is MenuItem parent)
                {
                    miRecentFile_Click(parent, e);
                }
            }
        }

        private void miRecentOpenNew_Click(object sender, RoutedEventArgs e)
        {
            // Process.GetCurrentProcess().MainModule?.FileName

            if (sender is MenuItem mi)
            {
                if (mi.Tag is MenuItem parent)
                {
                    if (parent.Tag is string file)
                    {
                        string? filename = Process.GetCurrentProcess().MainModule?.FileName;
                        if (filename == null)
                        {
                            // TODO: ask user if they want to open in this instance instead
                            MessageBox.Show("Cannot open another instance automatically.");
                            return;
                        }

                        Process.Start(filename, "\"" + file + "\"");
                    }
                }
            }
        }

        private void miRecentCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                if (mi.Tag is MenuItem parent)
                {
                    if (parent.Tag is string file)
                    {
                        Clipboard.SetText(file);
                    }
                }
            }
        }

        private void miRecentRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                if (mi.Tag is MenuItem parent)
                {
                    List<FrameworkElement> itemsToRemove = new List<FrameworkElement>();

                    foreach (FrameworkElement? item in mnuRecent.Items)
                    {
                        if (item != null && item == parent)
                        {
                            itemsToRemove.Add(item);
                        }
                    }

                    foreach (var item in itemsToRemove)
                    {
                        mnuRecent.Items.Remove(item);
                    }

                    if (parent.Tag is string file)
                    {
                        App.Settings.RecentFiles.Remove(file);
                        SaveSettings();

                        if (App.Settings.RecentFiles.Count == 0)
                        {
                            mnuRecentEmpty.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
        }

        private void miRecentView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                if (mi.Tag is MenuItem parent)
                {
                    if (parent.Tag is string file)
                    {
                        Process.Start("explorer.exe", "/select,\"" + file + "\"");
                    }
                }
            }
        }

        #endregion

        #region Tab bar / visuals / appearance

        void UpdateAppearance()
        {
            ApplyColorScheme(App.ColorScheme);
            menu.ApplyColorScheme(App.ColorScheme);
            toolbar.Background = App.ColorScheme.MainColor.ToBrush();
            if (sp != null) sp.ColorScheme = App.ColorScheme;

            if (App.ColorScheme.IsHighContrast)
            {
                menu.Background = App.ColorScheme.BackgroundColor.ToBrush();
                toolbar.Background = App.ColorScheme.BackgroundColor.ToBrush();
            }

            selTabs.ApplyColorScheme(App.ColorScheme);

            // quick fix until I make a better system post-1.0
            if (App.ColorScheme.IsHighContrast)
            {
                //foreach (SelectableItem item in selTabs.GetItemsAsType<SelectableItem>())
                //{
                //    item.ApplyColorScheme(App.ColorScheme);
                //    //if (selTabs.IsEnabled)
                //    //{
                //    //    item.ApplyColorScheme(App.ColorScheme);
                //    //}
                //    //else
                //    //{
                //    //    item.Foreground = App.ColorScheme.ForegroundColor.ToBrush();
                //    //}
                //}

                txtStrm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                txtPerm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                txtEndm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                txtCham.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                txtIntm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                txtAgim.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                txtLukm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);

                txtStrm.Background = new SolidColorBrush(SystemColors.ControlColor);
                txtPerm.Background = new SolidColorBrush(SystemColors.ControlColor);
                txtEndm.Background = new SolidColorBrush(SystemColors.ControlColor);
                txtCham.Background = new SolidColorBrush(SystemColors.ControlColor);
                txtIntm.Background = new SolidColorBrush(SystemColors.ControlColor);
                txtAgim.Background = new SolidColorBrush(SystemColors.ControlColor);
                txtLukm.Background = new SolidColorBrush(SystemColors.ControlColor);
            }
            else
            {
                foreach (SelectableItem item in selTabs.Items.OfType<SelectableItem>())
                {
                    if (item.IsEnabled)
                    {
                        item.ApplyColorScheme(App.ColorScheme);
                    }
                    else
                    {
                        item.Foreground = App.ColorScheme.DarkDisabledColor.ToBrush();
                    }
                }

                txtStrm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                txtPerm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                txtEndm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                txtCham.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                txtIntm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                txtAgim.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                txtLukm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);

                txtStrm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                txtPerm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                txtEndm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                txtCham.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                txtIntm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                txtAgim.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                txtLukm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
            }

            brdrCalculating.Background = App.ColorScheme.SecondaryColor.ToBrush();
            brdrCalculating.BorderBrush = App.ColorScheme.HighlightColor.ToBrush();

            (txtEditRaw.ContextMenu as SolidShineUi.ContextMenu)!.ApplyColorScheme(App.ColorScheme);

            //foreach (SkillEditor? item in stkSkills.Children)
            //{
            //    if (item == null) continue;
            //    item.UpdateAppearance();
            //}

            btnNotesEdit.Background = Color.FromArgb(1, 0, 0, 0).ToBrush();
            btnNotesView.Background = Color.FromArgb(1, 0, 0, 0).ToBrush();

            //foreach (SpellEditor item in selSpells.GetItemsAsType<SpellEditor>())
            //{
            //    item.ApplyColorScheme(App.ColorScheme);
            //}
        }

        void SetupTabs()
        {
            selTabs.Items.Add(CreateTab("General"));
            selTabs.Items.Add(CreateTab("Skills"));
            selTabs.Items.Add(CreateTab("Combat"));
            selTabs.Items.Add(CreateTab("Attributes/Defects"));
            selTabs.Items.Add(CreateTab("Feats"));
            selTabs.Items.Add(CreateTab("Items"));
            selTabs.Items.Add(CreateTab("Notes"));

            SetAllTabsVisibility(Visibility.Collapsed);
        }

        SelectableItem CreateTab(string name, ImageSource? image = null)
        {
            SelectableItem si = new SelectableItem
            {
                Height = 36,
                Text = name,
                Indent = 6
            };

            if (image == null)
            {
                si.ShowImage = false;
            }
            else
            {
                si.ImageSource = image;
                si.ShowImage = true;
            }

            si.Click += tabItem_Click;
            return si;
        }

        void LoadGeneralTab()
        {
            selTabs.Items[0].IsSelected = true;
            LoadTab("General");
        }

        void LoadTab(string text)
        {
            if (currentView == TABS_VIEW)
            {
                SetAllTabsVisibility(Visibility.Collapsed);

                //txtLoc.Text = text;
                switch (text)
                {
                    case "General":
                        grdGeneral.Visibility = Visibility.Visible;
                        break;
                    case "Skills":
                        grdSkills.Visibility = Visibility.Visible;
                        break;
                    case "Attributes/Defects":
                        grdAttributes.Visibility = Visibility.Visible;
                        break;
                    case "Combat":
                        grdCombat.Visibility = Visibility.Visible;
                        break;
                    case "Feats":
                        grdFeats.Visibility = Visibility.Visible;
                        break;
                    case "Notes":
                        grdNotes.Visibility = Visibility.Visible;
                        break;
                    default:
                        break;
                }
                scrSheet.ScrollToVerticalOffset(0);
            }
            else
            {
                SetAllTabsVisibility();
                //txtLoc.Text = "All Tabs";

                TextBlock control = titGeneral;

                switch (text)
                {
                    case "General":
                        control = titGeneral;
                        break;
                    case "Skills":
                        control = titSkills;
                        break;
                    case "Attributes/Defects":
                        control = titAttributes;
                        break;
                    case "Combat":
                        control = titCombat;
                        break;
                    case "Feats":
                        control = titFeats;
                        break;
                    case "Notes":
                        control = titNotes;
                        break;
                    default:
                        break;
                }

                Point relativeLocation = control.TranslatePoint(new Point(0, 0), stkSheet);
                scrSheet.ScrollToVerticalOffset(relativeLocation.Y - 5);
            }
        }

        private void tabItem_Click(object? sender, EventArgs e)
        {
            if (sender == null) return;
            SelectableItem si = (SelectableItem)sender;

            if (si.CanSelect)
            {
                LoadTab(si.Text);
            }
        }

        void SetAllTabsVisibility(Visibility vis = Visibility.Visible)
        {
            grdGeneral.Visibility = vis;
            grdSkills.Visibility = vis;
            grdAttributes.Visibility = vis;
            grdCombat.Visibility = vis;
            grdFeats.Visibility = vis;
            grdNotes.Visibility = vis;
        }

        private void mnuColors_Click(object sender, RoutedEventArgs e)
        {
            ChangeTheme.ColorSchemeDialog csd = new ChangeTheme.ColorSchemeDialog();
            csd.ColorScheme = this.ColorScheme;

            csd.ShowDialog();

            if (csd.DialogResult)
            {
                App.ColorScheme = csd.SelectedColorScheme;

                if (csd.InternalColorSchemeValue != 0)
                {
                    ColorScheme cs = csd.SelectedColorScheme;

                    App.Settings.HighContrastTheme = csd.InternalColorSchemeValue.ToString();
                }
                else
                {
                    App.Settings.ThemeColor = csd.SelectedColorScheme.MainColor.GetHexString();
                    App.Settings.HighContrastTheme = NO_HIGH_CONTRAST;
                }

                SaveSettings();
                UpdateAppearance();

                // change settings if high contrast is changed
                bool isHighContrast = false;
                if (csd.InternalColorSchemeValue >= 1 && csd.InternalColorSchemeValue <= 3) isHighContrast = true;

                if (App.Settings.EditorSyntaxHighlighting && !isHighContrast)
                {
                    using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SentinelsJson.Json.xshd"))
                    {
                        if (s != null)
                        {
                            using XmlReader reader = new XmlTextReader(s);
                            txtEditRaw.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                        }
                    }
                }
                else
                {
                    using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SentinelsJson.None.xshd"))
                    {
                        if (s != null)
                        {
                            using XmlReader reader = new XmlTextReader(s);
                            txtEditRaw.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                        }
                    }
                }
            }
        }

        void ShowHideToolbar(bool show)
        {
            if (show)
            {
                rowToolbar.Height = new GridLength(1, GridUnitType.Auto);
                rowToolbar.MinHeight = 28;
                toolbar.IsEnabled = true;
                mnuToolbar.IsChecked = true;
            }
            else
            {
                rowToolbar.Height = new GridLength(0);
                rowToolbar.MinHeight = 0;
                toolbar.IsEnabled = false;
                mnuToolbar.IsChecked = false;
            }
        }

        #endregion

        #region View options

        void ChangeView(string view, bool updateSheet = true, bool displayEmptyMessage = false, bool saveSettings = true)
        {
            view = view.ToLowerInvariant();

            // weed out unintended "view" strings
            if (view != CONTINUOUS_VIEW && view != TABS_VIEW && view != RAWJSON_VIEW)
            {
                //await ChangeView(TABS_VIEW, updateSheet, displayEmptyMessage, saveSettings);
                return;
            }

            if (!_sheetLoaded) // don't update sheet if there is no sheet lol
            {
                updateSheet = false;
            }

            // update back-end settings before actually changing views
            currentView = view;

            if (saveSettings)
            {
                App.Settings.StartView = view;
                SaveSettings();
            }

            switch (view)
            {
                case CONTINUOUS_VIEW:
                    SetAllTabsVisibility();
                    LoadGeneralTab();

                    txtEditRaw.Visibility = Visibility.Collapsed;
                    mnuEdit.Visibility = Visibility.Collapsed;
                    colTabs.Width = new GridLength(120, GridUnitType.Auto);
                    colTabs.MinWidth = 120;
                    stkEditToolbar.Visibility = Visibility.Collapsed;

                    mnuTabs.IsChecked = false;
                    mnuScroll.IsChecked = true;
                    mnuRawJson.IsChecked = false;

                    if (_isEditorDirty && updateSheet)
                    {
                        // update sheet from editor
                        SyncSheetFromEditor();
                    }
                    break;
                case TABS_VIEW:
                    LoadGeneralTab();

                    txtEditRaw.Visibility = Visibility.Collapsed;
                    mnuEdit.Visibility = Visibility.Collapsed;
                    colTabs.Width = new GridLength(120, GridUnitType.Auto);
                    colTabs.MinWidth = 120;
                    stkEditToolbar.Visibility = Visibility.Collapsed;

                    mnuTabs.IsChecked = true;
                    mnuScroll.IsChecked = false;
                    mnuRawJson.IsChecked = false;

                    if (_isEditorDirty && updateSheet)
                    {
                        // update sheet from editor
                        SyncSheetFromEditor();
                    }
                    break;
                case RAWJSON_VIEW:
                    txtEditRaw.Visibility = Visibility.Visible;
                    mnuEdit.Visibility = Visibility.Visible;
                    colTabs.Width = new GridLength(0);
                    colTabs.MinWidth = 0;
                    stkEditToolbar.Visibility = Visibility.Visible;

                    mnuTabs.IsChecked = false;
                    mnuScroll.IsChecked = false;
                    mnuRawJson.IsChecked = true;

                    if (_isTabsDirty && updateSheet)
                    {
                        SyncEditorFromSheet();
                    }
                    break;
                default:
                    ChangeView(TABS_VIEW, updateSheet, true, saveSettings);
                    break;
            }

            Visibility v = lblNoSheet.Visibility;

            if (displayEmptyMessage)
            {
                lblNoSheet.Visibility = Visibility.Visible;
                selTabs.IsEnabled = false;
                txtEditRaw.IsEnabled = false;
                grdEditDrop.Visibility = Visibility.Visible;
                SetAllTabsVisibility(Visibility.Collapsed);
                UpdateAppearance();
                foreach (SelectableItem item in selTabs.Items.SelectedItems.OfType<SelectableItem>().ToList())
                {
                    item.IsSelected = false;
                }
            }
            else
            {
                lblNoSheet.Visibility = Visibility.Collapsed;
                selTabs.IsEnabled = true;
                txtEditRaw.IsEnabled = true;
                grdEditDrop.Visibility = Visibility.Collapsed;
                if (lblNoSheet.Visibility != v)
                {
                    UpdateAppearance();
                }
            }
        }

        private void mnuScroll_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(CONTINUOUS_VIEW, true, !_sheetLoaded);
        }

        private void mnuTabs_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(TABS_VIEW, true, !_sheetLoaded);
        }

        private void mnuRawJson_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(RAWJSON_VIEW, true, !_sheetLoaded);
        }

        private void mnuToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (rowToolbar.Height == new GridLength(0))
            {
                ShowHideToolbar(true);
            }
            else
            {
                ShowHideToolbar(false);
            }

            App.Settings.ShowToolbar = toolbar.IsEnabled;
            SaveSettings();
        }

        private void mnuFilename_Click(object sender, RoutedEventArgs e)
        {
            if (mnuFilename.IsChecked)
            {
                mnuFilename.IsChecked = false;
            }
            else
            {
                mnuFilename.IsChecked = true;
            }

            App.Settings.PathInTitleBar = mnuFilename.IsChecked;
            SaveSettings();
            UpdateTitlebar();
        }

        #endregion

        #region Tools menu

        DiceRollerWindow? drw = null;

        private void mnuDiceRoll_Click(object sender, RoutedEventArgs e)
        {
            if (drw != null)
            {
                drw.Show();
                drw.Focus();
            }
            else
            {
                drw = new DiceRollerWindow();
                drw.ColorScheme = App.ColorScheme;
                drw.Closed += DiceRollerWindow_Closed;
                drw.Show();
            }
        }

        private void DiceRollerWindow_Closed(object? sender, EventArgs e)
        {
            if (drw != null)
            {
                drw.Closed -= DiceRollerWindow_Closed;
                drw = null;
            }
        }

        private void mnuEditorFont_Click(object sender, RoutedEventArgs e)
        {
            FontSelectDialog fds = new FontSelectDialog();
            fds.ShowDecorations = false;
            fds.ColorScheme = App.ColorScheme;

            fds.SelectedFontFamily = txtEditRaw.FontFamily;
            fds.SelectedFontSize = txtEditRaw.FontSize;
            fds.SelectedFontStyle = txtEditRaw.FontStyle;
            fds.SelectedFontWeight = txtEditRaw.FontWeight;

            fds.ShowDialog();

            if (fds.DialogResult)
            {
                txtEditRaw.FontFamily = fds.SelectedFontFamily;
                txtEditRaw.FontSize = fds.SelectedFontSize;
                txtEditRaw.FontStyle = fds.SelectedFontStyle;
                txtEditRaw.FontWeight = fds.SelectedFontWeight;
            }

            SaveSettings(true);
        }


        private void mnuOptions_Click(object sender, RoutedEventArgs e)
        {
            Options o = new Options();
            o.Owner = this;
            o.ColorScheme = App.ColorScheme;

            o.ShowDialog();

            if (o.DialogResult)
            {
                ReloadSettings();
            }
        }

        #endregion

        #region Help menu

        private void mnuGithub_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://github.com/JaykeBird/SentinelsJson/");
        }

        private void mnuFeedback_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://github.com/JaykeBird/SentinelsJson/issues/new/choose");
        }

        private void mnuKeyboard_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://github.com/JaykeBird/SentinelsJSON/wiki/Keyboard-Shortcuts");
        }

        private void mnuMarkdown_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://commonmark.org/help/");
        }

        private void mnuAutoCheck_Click(object sender, RoutedEventArgs e)
        {
            if (mnuAutoCheck.IsChecked)
            {
                mnuAutoCheck.IsChecked = false;
                App.Settings.UpdateAutoCheck = false;
            }
            else
            {
                mnuAutoCheck.IsChecked = true;
                App.Settings.UpdateAutoCheck = false;
            }

            SaveSettings();
        }

        private async void mnuCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdates();
        }

        private void mnuAbout_Click(object sender, RoutedEventArgs e)
        {
            About.About a = new About.About();
            a.Owner = this;
            a.ShowDialog();
        }

        #endregion

        #region JSON Editor

        private void mnuUndo_Click(object sender, RoutedEventArgs e)
        {
            txtEditRaw.Undo();
        }

        private void mnuRedo_Click(object sender, RoutedEventArgs e)
        {
            txtEditRaw.Redo();
        }

        private void mnuCopy_Click(object sender, RoutedEventArgs e)
        {
            txtEditRaw.Copy();
        }

        private void mnuCut_Click(object sender, RoutedEventArgs e)
        {
            txtEditRaw.Cut();
        }

        private void mnuPaste_Click(object sender, RoutedEventArgs e)
        {
            if (_sheetLoaded)
            {
                txtEditRaw.Paste();
            }
        }

        private void mnuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            txtEditRaw.SelectAll();
        }

        private void mnuDelete_Click(object sender, RoutedEventArgs e)
        {
            txtEditRaw.Delete();
        }

        private void mnuWordWrap_Click(object sender, RoutedEventArgs e)
        {
            if (mnuWordWrap.IsChecked)
            {
                mnuWordWrap.IsChecked = false;
                txtEditRaw.WordWrap = false;
                App.Settings.EditorWordWrap = false;
            }
            else
            {
                mnuWordWrap.IsChecked = true;
                txtEditRaw.WordWrap = true;
                App.Settings.EditorWordWrap = true;
            }

            SaveSettings();
        }

        private void mnuFind_Click(object sender, RoutedEventArgs e)
        {
            if (_sheetLoaded)
            {

                sp.Open();
                if (!(txtEditRaw.TextArea.Selection.IsEmpty || txtEditRaw.TextArea.Selection.IsMultiline))
                    sp.SearchPattern = txtEditRaw.TextArea.Selection.GetText();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate { sp.Reactivate(); });
            }
        }

        private void txtEditRaw_TextChanged(object sender, EventArgs e)
        {
            if (!_isUpdating)
            {
                _isEditorDirty = true;
                SetIsDirty(true, false);
            }
        }

        private void txtEditRaw_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

                if (!SaveDirtyChanges() || CheckCalculating())
                {
                    return;
                }

                LoadFile(files[0]);
            }
        }

        void SetSyntaxHighlighting(bool value)
        {
            if (value)
            {
                using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SentinelsJSON.Json.xshd"))
                {
                    if (s != null)
                    {
                        using XmlReader reader = new XmlTextReader(s);
                        txtEditRaw.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }
            else
            {
                using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SentinelsJSON.None.xshd"))
                {
                    if (s != null)
                    {
                        using XmlReader reader = new XmlTextReader(s);
                        txtEditRaw.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }

            App.Settings.EditorSyntaxHighlighting = value;
            SaveSettings();
        }

        #region Font Settings
        void LoadEditorFontSettings()
        {
            string family = App.Settings.EditorFontFamily;
            string size = App.Settings.EditorFontSize;
            string style = App.Settings.EditorFontStyle;
            string weight = App.Settings.EditorFontWeight.Replace("w", "").Replace(".", "");

            // sanitizing user input
            if (string.IsNullOrEmpty(family))
            {
                family = "Consolas";
            }
            if (string.IsNullOrEmpty(size))
            {
                size = "12";
            }
            if (string.IsNullOrEmpty(style))
            {
                style = "Normal";
            }
            if (string.IsNullOrEmpty(weight))
            {
                weight = "400";
            }

            if (style == "None")
            {
                style = "Normal";
            }

            // check if weight is an integer value or not; if not, try to convert it
            if (!int.TryParse(weight, out _))
            {
                // converter of common fontweight values
                // taken from https://docs.microsoft.com/en-us/dotnet/api/system.windows.fontweights
                if (weight.ToLowerInvariant() == "thin")
                {
                    weight = "100";
                }
                else if (weight.ToLowerInvariant() == "extralight" || weight.ToLowerInvariant() == "ultralight")
                {
                    weight = "200";
                }
                else if (weight.ToLowerInvariant() == "light")
                {
                    weight = "300";
                }
                else if (weight.ToLowerInvariant() == "normal" || weight.ToLowerInvariant() == "regular")
                {
                    weight = "400";
                }
                else if (weight.ToLowerInvariant() == "medium")
                {
                    weight = "500";
                }
                else if (weight.ToLowerInvariant() == "demibold" || weight.ToLowerInvariant() == "semibold")
                {
                    weight = "600";
                }
                else if (weight.ToLowerInvariant() == "bold")
                {
                    weight = "700";
                }
                else if (weight.ToLowerInvariant() == "extrabold" || weight.ToLowerInvariant() == "ultrabold")
                {
                    weight = "800";
                }
                else if (weight.ToLowerInvariant() == "black" || weight.ToLowerInvariant() == "heavy")
                {
                    weight = "900";
                }
                else if (weight.ToLowerInvariant() == "extrablack" || weight.ToLowerInvariant() == "ultrablack")
                {
                    weight = "950";
                }
                else
                {
                    // don't know what the heck they put in there, but it's not a font weight; set it to normal
                    weight = "400";
                }
            }

            FontFamily ff = new FontFamily(family + ", Consolas"); // use Consolas as fallback in case that font doesn't exist or the font doesn't contain proper glyphs

            double dsz = 12;

            try
            {
                dsz = double.Parse(size.Replace("p", "").Replace("d", "").Replace("x", "").Replace("t", ""));
            }
            catch (FormatException) { } // if "size" is a string that isn't actually a double, just keep it as 12

            FontStyle fs = FontStyles.Normal;
            try
            {
                fs = (FontStyle)new FontStyleConverter().ConvertFromInvariantString(style);
            }
            catch (NotSupportedException) { } // if "style" is a string that isn't actually a FontStyle, just keep it as normal
            catch (FormatException) { }

            int w = int.Parse(weight);
            if (w > 999)
            {
                w = 999;
            }
            else if (w < 1)
            {
                w = 1;
            }
            FontWeight fw = FontWeight.FromOpenTypeWeight(w);

            txtEditRaw.FontFamily = ff;
            txtEditRaw.FontSize = dsz;
            txtEditRaw.FontStyle = fs;
            txtEditRaw.FontWeight = fw;
        }

        void SaveEditorFontSettings()
        {
            string ff = (txtEditRaw.FontFamily.Source).Replace(", Consolas", "");

            App.Settings.EditorFontFamily = ff;
            App.Settings.EditorFontSize = txtEditRaw.FontSize.ToString();

            // because the ToString() method for FontStyle uses CurrentCulture rather than InvariantCulture, I need to convert it to string myself.
            if (txtEditRaw.FontStyle == FontStyles.Italic)
            {
                App.Settings.EditorFontStyle = "Italic";
            }
            else if (txtEditRaw.FontStyle == FontStyles.Oblique)
            {
                App.Settings.EditorFontStyle = "Oblique";
            }
            else
            {
                App.Settings.EditorFontStyle = "Normal";
            }
        }
        #endregion

        #endregion

        #region Load File
        void LoadFile(string filename, bool addToRecent = true)
        {
            //if (filename == null)
            //{
            //    MessageBox.Show(this, "The filename provided is not valid. No file can be opened.",
            //        "Filename Null Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //    return;
            //}

            // Prepare message dialog in case an error occurs
            MessageDialog md = new MessageDialog(App.ColorScheme)
            {
                Title = "File Format Error",
                Image = MessageDialogImage.Error,
            };

            if (IsVisible)
            {
                md.Owner = this;
            }

            try
            {
                SentinelsSheet ss = SentinelsSheet.LoadJsonFile(filename);
                filePath = filename;
                fileTitle = ss.Name;
                _sheetLoaded = true;

                isDirty = false;
                _isEditorDirty = false;
                _isTabsDirty = false;

                UpdateTitlebar();

                _isUpdating = true;
                txtEditRaw.Load(filename);
                _isUpdating = false;
                ChangeView(App.Settings.StartView, false, false);
                LoadSentinelsSheet(ss);
            }
            catch (FileFormatException)
            {
                md.Message = "The file \"" + filename + "\" does not appear to be a JSON file. Check the file in Notepad or another text editor to make sure it's not corrupted.";
                md.ShowDialog();
                return;
            }
            catch (InvalidDataException)
            {
                md.Message = "The file \"" + filename + "\" does not appear to be a JSON file. Check the file in Notepad or another text editor to make sure it's not corrupted.";
                md.ShowDialog();
                return;
            }
            catch (InvalidOperationException e)
            {
                if (e.Message.Contains("error context error is different to requested error"))
                {
                    md.Message = "The file \"" + filename + "\" does not match the JSON format this program is looking for. Check the file in Notepad or another text editor to make sure it's not corrupted.";
                    md.ShowDialog();
                    return;
                }
                else
                {
                    md.Message = "The file \"" + filename + "\" cannot be opened due to this error: \n\n" + e.Message + "\n\n" +
                        "Check the file in Notepad or another text editor, or report this issue via the \"Send Feedback\" option in the Help menu.";
                    md.ShowDialog();
                    return;
                }
            }
            catch (FileNotFoundException)
            {
                md.Message = "The file \"" + filename + "\" cannot be found. Make sure the file exists and then try again.";
                md.ShowDialog();
                return;
            }

            if (addToRecent) AddRecentFile(filename);
        }

        void LoadSentinelsSheet(SentinelsSheet sheet)
        {
            // set this flag so that the program doesn't try to set the sheet as dirty while loading in the file
            _isUpdating = true;

            // check if the userdata structure is present
            bool _userDataCheck = false;

            // General tab
            if (sheet.Player != null)
            {
                txtPlayerName.Text = sheet.Player.DisplayName;

                string email = "";

                foreach (UserData.Email item in sheet.Player.Emails)
                {
                    if (item.Type == "account")
                    {
                        email = item.Value;
                    }
                }
                if (string.IsNullOrEmpty(email))
                {
                    try
                    {
                        email = sheet.Player.Emails[0].Value;
                    }
                    catch (IndexOutOfRangeException) { }
                    catch (ArgumentOutOfRangeException) { }
                }
                txtPlayerEmail.Text = email;

                try
                {
                    if (sheet.Player.Photos != null)
                    {
                        ImageSource iss = new BitmapImage(new Uri(sheet.Player.Photos[0].Value ?? ""));
                        imgPlayer.Source = iss;
                    }
                    else
                    {
                        imgPlayer.Source = null;
                    }
                }
                catch (IndexOutOfRangeException) { imgPlayer.Source = null; }
                catch (NullReferenceException) { imgPlayer.Source = null; }
                catch (ArgumentOutOfRangeException) { imgPlayer.Source = null; }
                catch (System.Net.WebException) { imgPlayer.Source = null; }

                _userDataCheck = false;
            }
            else
            {
                _userDataCheck = true;
                sheet.Player = new UserData(true);
            }

            ud = sheet.Player;
            //ac = sheet.AC;
            //sheetid = sheet.Id ?? "-1";
            //version = sheet.Version;

            cpPerLevel = sheet.PointsPerLevel;
            cpLevel0 = sheet.Level0Points;

            txtCharacter.Text = sheet.Name;
            nudLevel.Value = sheet.BaseLevel;
            nudEcl.Value = sheet.ECL;
            txtAlignment.Text = sheet.Alignment;
            txtHomeland.Text = sheet.Homeland;
            txtDeity.Text = sheet.Deity;

            txtPhyRace.Text = sheet.Race;
            txtPhyGender.Text = sheet.Gender;
            txtPhySize.Text = sheet.Size;
            txtPhyAge.Text = sheet.Age;
            txtPhyHeight.Text = sheet.Height;
            txtPhyWeight.Text = sheet.Weight;
            txtPhyHair.Text = sheet.Hair;
            txtPhyEyes.Text = sheet.Eyes;

            Speed? spd = sheet.Speed;
            if (spd != null)
            {
                txtSpeedBase.Text = spd.Base;
                txtSpeedArmor.Text = spd.WithArmor;
                txtSpeedBurrow.Text = spd.Burrow;
                txtSpeedClimb.Text = spd.Climb;
                txtSpeedFly.Text = spd.Fly;
                txtSpeedSwim.Text = spd.Swim;
                txtSpeedTemp.Text = spd.TempModifier;
            }

            abilities = sheet.RawAbilities;
            potentials = sheet.RawPotential;

            txtStr.Value = sheet.Strength;
            txtPer.Value = sheet.Perception;
            txtEnd.Value = sheet.Endurance;
            txtCha.Value = sheet.Charisma;
            txtInt.Value = sheet.Intellect;
            txtAgi.Value = sheet.Agility;
            txtLuk.Value = sheet.Luck;

            UpdateModifiers();

            txtStrp.Value = sheet.PotStrength;
            txtPerp.Value = sheet.PotPerception;
            txtEndp.Value = sheet.PotEndurance;
            txtChap.Value = sheet.PotCharisma;
            txtIntp.Value = sheet.PotIntellect;
            txtAgip.Value = sheet.PotAgility;
            txtLukp.Value = sheet.PotLuck;

            switch (sheet.PowerStatName.ToUpperInvariant())
            {
                case "STR":
                case "STRENGTH":
                    chkStrw.IsChecked = true;
                    powerStat = "STR";
                    break;
                case "PER":
                case "PERCEPTION":
                    chkPerw.IsChecked = true;
                    powerStat = "PER";
                    break;
                case "END":
                case "ENDURANCE":
                    chkEndw.IsChecked = true;
                    powerStat = "END";
                    break;
                case "CHA":
                case "CHARISMA":
                    chkChaw.IsChecked = true;
                    powerStat = "CHA";
                    break;
                case "INT":
                case "INTELLECT":
                    chkIntw.IsChecked = true;
                    powerStat = "INT";
                    break;
                case "AGI":
                case "AGILITY":
                    chkAgiw.IsChecked = true;
                    powerStat = "AGI";
                    break;
                case "LUK":
                case "LUCK":
                    chkStrw.IsChecked = true;
                    powerStat = "LUK";
                    break;
                default:
                    break;
            }

            if (sheet.SheetSettings != null)
            {
                sheetSettings = sheet.SheetSettings;
            }
            else
            {
                sheetSettings = new Dictionary<string, string?>();
            }

            txtSaveFRanks.Value = sheet.Saves["fort"].Ranks;
            txtSaveRRanks.Value = sheet.Saves["reflex"].Ranks;
            txtSaveWRanks.Value = sheet.Saves["will"].Ranks;

            txtSaveFMisc.Value = sheet.Saves["fort"].Misc;
            txtSaveRMisc.Value = sheet.Saves["reflex"].Misc;
            txtSaveWMisc.Value = sheet.Saves["will"].Misc;

            // Combat tab

            nudDefenseActions.Value = sheet.DefenseActions;

            nudArmorA.Value = sheet.Armor.Attributes;
            nudArmorE.Value = sheet.Armor.Equipment;
            nudArmorN.Value = sheet.Armor.Natural;
            nudArmorS.Value = sheet.Armor.Shield;
            nudArmorM.Value = sheet.Armor.Misc;

            txtCmbM.Value = sheet.CmbMisc;
            txtCmdM.Value = sheet.CmdMisc;
            txtMmbM.Value = sheet.MmbMisc;
            txtMmdM.Value = sheet.MmdMisc;

            UpdateCombatTab();

            // Skills tab
            skillList = sheet.SkillList.ToLowerInvariant().Trim();

            LoadSkills(sheet);

            // Feats tab

            selFeats.LoadList(sheet.Feats);

            // Notes tab
            if (sheetSettings != null)
            {
                if (sheetSettings.ContainsKey("notesMarkdown"))
                {
                    if (sheetSettings["notesMarkdown"]?.ToLowerInvariant() == "enabled")
                    {
                        ShowMarkdownElements();
                        OpenNotesViewTab();
                        chkNotesMarkdown.IsChecked = true;
                    }
                    else
                    {
                        HideMarkdownElements();
                        chkNotesMarkdown.IsChecked = false;
                    }
                }
                else
                {
                    HideMarkdownElements();
                    chkNotesMarkdown.IsChecked = false;
                }
            }
            else
            {
                HideMarkdownElements();
                chkNotesMarkdown.IsChecked = false;
            }

            //if (sheet.NotesMarkdown)
            //{
            //    ShowMarkdownElements();
            //    OpenNotesViewTab();
            //}
            //else
            //{
            //    HideMarkdownElements();
            //}

            txtNotes.Text = sheet.Notes;
            UpdateMarkdownViewerVisuals();

            _isUpdating = false;

            // this is a check to determine if this JSON file looks like a character sheet file or not
            // the program will happily open and work with the file, but if the user saves the file the existing data in the file will be deleted
            // thus, I'm most concerned about data loss for the user, in case the user accidentally opened the wrong file
            // the check looks at two of three things being missing:
            // 1. the character's name (name attribute)
            // 2. the player's info (user data structure)
            // 3. the character's base abilities structure (abilities structure)
            // if two of them are missing, it displays a warning dialog but continues otherwise
            if ((string.IsNullOrEmpty(sheet.Name) && (!sheet.AbilitiesPresent || _userDataCheck)) || (!sheet.AbilitiesPresent && _userDataCheck))
            {
                MessageDialog md = new MessageDialog(App.ColorScheme);
                md.Message = "This JSON file doesn't seem to look like it's a character sheet at all. " +
                    "It may be good to open the Raw JSON view to check that the file matches what you're expecting.\n\n" +
                    "SentinelsJSON will continue, but if you save any changes, any non-character sheet data may be deleted.";
                md.Title = "File Check Warning";
                md.Owner = this;
                md.Image = MessageDialogImage.Hand;
                md.ShowDialog();
            }

        }

        void LoadSkills(SentinelsSheet sheet)
        {
            bool invalidSkillFile = false;

            SkillList sl = skillList switch
            {
                "none" => new SkillList(),
                "pathfinder" => SkillList.LoadPathfinderList(),
                "standard" => SkillList.LoadStandardList(),
                "full" => SkillList.LoadStandardList(),
                "simplified" => SkillList.LoadSimplifiedList(),
                "" => new SkillList(),
                null => new SkillList(),
                _ => LoadSkillListFromFile(), // any other value, let's assume it's a file
            };

            SkillList LoadSkillListFromFile()
            {
                // if the skillList entry is a file, attempt to load it
                if (File.Exists(skillList))
                {
                    try
                    {
                        return SkillList.LoadFile(skillList);
                    }
                    catch (FormatException)
                    {
                        invalidSkillFile = true;
                        return new SkillList();
                    }
                    catch (FileNotFoundException)
                    {
                        invalidSkillFile = true;
                        return new SkillList();
                    }
                }
                else
                {
                    // otherwise, just give out an empty list
                    invalidSkillFile = true;
                    return new SkillList();
                }
            }

            if (invalidSkillFile)
            {
                // display message to user about file
            }

            stkSkills.Children.Clear();

            foreach (SkillListEntry item in sl.SkillEntries)
            {
                if (string.IsNullOrEmpty(item.Name)) continue;

                sheet.Skills.TryGetValue(item.Name, out Skill? s);

                if (s == null) s = new Skill();

                SkillEditor se = new SkillEditor()
                {
                    InternalSkillName = item.Name,
                    SkillName = item.DisplayName ?? (item.Name.First().ToString().ToUpperInvariant() + item.Name.Substring(1)),
                    HasSpecialization = item.HasSpecialization,
                    ModifierName = string.IsNullOrEmpty(s.Modifier) ? item.Modifier : s.Modifier,

                    SkillRanks = s.Ranks,
                    Specialization = s.Specialization ?? "",
                    MiscModifier = s.Misc,
                    IsTrained = s.Trained,

                    Margin = new Thickness(0, 2, 5, 2),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    ColorScheme = ColorScheme,
                };

                se.ModifierValue = abModifiers[se.ModifierName];
                se.UpdateCalculations();

                se.ModifierChanged += se_ModifierChanged;
                se.ContentChanged += editor_ContentChanged;

                stkSkills.Children.Add(se);
            }
        }

        #endregion

        #region Create Sheet

        /// <summary>
        /// Create a SentinelsSheet object by loading in all the values from the sheet view.
        /// </summary>
        /// <returns></returns>
        private SentinelsSheet CreateSentinelsSheet()
        {
            SentinelsSheet sheet = new SentinelsSheet();

            sheet.Player = ud;
            sheet.Id = sheetid;
            //sheet.Version = version;

            sheet.Notes = txtNotes.Text;

            if (chkNotesMarkdown.IsChecked)
            {
                sheetSettings["notesMarkdown"] = "enabled";
            }

            sheet.PointsPerLevel = cpPerLevel;
            sheet.Level0Points = cpLevel0;

            sheet.Name = txtCharacter.Text;
            sheet.BaseLevel = nudLevel.Value;
            sheet.ECL = nudEcl.Value;
            sheet.Alignment = GetStringOrNull(txtAlignment.Text);
            sheet.Homeland = GetStringOrNull(txtHomeland.Text);
            sheet.Deity = GetStringOrNull(txtDeity.Text);

            sheet.Race = GetStringOrNull(txtPhyRace.Text);
            sheet.Gender = GetStringOrNull(txtPhyGender.Text);
            sheet.Size = GetStringOrNull(txtPhySize.Text);
            sheet.Age = GetStringOrNull(txtPhyAge.Text);
            sheet.Height = GetStringOrNull(txtPhyHeight.Text);
            sheet.Weight = GetStringOrNull(txtPhyWeight.Text);
            sheet.Hair = GetStringOrNull(txtPhyHair.Text);
            sheet.Eyes = GetStringOrNull(txtPhyEyes.Text);

            Speed? sp = new Speed();
            sp.Base = GetStringOrNull(txtSpeedBase.Text, true);
            sp.Burrow = GetStringOrNull(txtSpeedBase.Text, true);
            sp.Climb = GetStringOrNull(txtSpeedBase.Text, true);
            sp.Fly = GetStringOrNull(txtSpeedBase.Text, true);
            sp.Swim = GetStringOrNull(txtSpeedBase.Text, true);
            sp.WithArmor = GetStringOrNull(txtSpeedBase.Text, true);
            sp.TempModifier = GetStringOrNull(txtSpeedTemp.Text);

            if (sp.Base == null && sp.Burrow == null && sp.Climb == null &&
                sp.Fly == null && sp.Swim == null && sp.WithArmor == null
                && sp.TempModifier == null)
            {
                sp = null;
            }
            sheet.Speed = sp;

            if (sheetSettings.Count > 0)
            {
                sheet.SheetSettings = sheetSettings;
            }

            abilities["str"] = txtStr.Value.ToString();
            abilities["per"] = txtPer.Value.ToString();
            abilities["end"] = txtEnd.Value.ToString();
            abilities["cha"] = txtCha.Value.ToString();
            abilities["int"] = txtInt.Value.ToString();
            abilities["agi"] = txtAgi.Value.ToString();
            abilities["luk"] = txtLuk.Value.ToString();

            sheet.RawAbilities = abilities;

            potentials["str"] = txtStrp.Value.ToString();
            potentials["per"] = txtPerp.Value.ToString();
            potentials["end"] = txtEndp.Value.ToString();
            potentials["cha"] = txtChap.Value.ToString();
            potentials["int"] = txtIntp.Value.ToString();
            potentials["agi"] = txtAgip.Value.ToString();
            potentials["luk"] = txtLukp.Value.ToString();

            sheet.RawPotential = potentials;

            Dictionary<string, Save> saves = new Dictionary<string, Save>()
            {
                { "fort", new Save(txtSaveFRanks.Value, txtSaveFMisc.Value) },
                { "reflex", new Save(txtSaveRRanks.Value, txtSaveRMisc.Value) },
                { "will", new Save(txtSaveWRanks.Value, txtSaveWMisc.Value) },
            };

            sheet.Saves = saves;

            // combat tab

            sheet.TrainedProwess = nudProwessT.Value;
            var a = new Armor() { Natural = nudArmorN.Value, Equipment = nudArmorE.Value, Shield = nudArmorS.Value, Attributes = nudArmorA.Value, Misc = nudArmorM.Value };
            sheet.Armor = a;

            sheet.BrawlUseAgi = chkBrawlAgi.IsChecked;
            sheet.MeleeUseAgi = chkMeleeAgi.IsChecked;

            sheet.DefenseActions = nudDefenseActions.Value;

            sheet.CmbMisc = txtCmbM.Value;
            sheet.CmdMisc = txtCmdM.Value;
            sheet.MmbMisc = txtMmbM.Value;
            sheet.MmdMisc = txtMmdM.Value;

            // skills tab

            sheet.SkillList = skillList;
            sheet.Skills = new Dictionary<string, Skill>();

            foreach (SkillEditor se in stkSkills.Children.OfType<SkillEditor>())
            {
                sheet.Skills[se.InternalSkillName] = new Skill(se.IsTrained, se.SkillRanks, se.MiscModifier, se.ModifierName, string.IsNullOrEmpty(se.Specialization) ? null : se.Specialization);
            }

            // feats tab

            sheet.Feats = selFeats.GetItems<Feat>();

            return sheet;
        }

        #endregion

        #region Sync Editors / update sheet

        #region Update UI (Calculate menu)

        private async void mnuUpdate_Click(object sender, RoutedEventArgs e)
        {
            await UpdateCalculations(true);
        }

        private void mnuAutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            mnuAutoUpdate.IsChecked = !mnuAutoUpdate.IsChecked;
        }

        #endregion

        async Task UpdateCalculations(bool skills = true)
        {
            if (!_sheetLoaded)
            {
                MessageDialog md = new MessageDialog(App.ColorScheme);
                md.ShowDialog("Cannot run calculations when no sheet is opened.", App.ColorScheme, this, "Update Calculations", MessageDialogButtonDisplay.Auto, image: MessageDialogImage.Error);
                return;
            }

            if (_isCalculating)
            {
                return;
            }

            _isUpdating = true;

            if (currentView == RAWJSON_VIEW && _isEditorDirty)
            {
                SyncSheetFromEditor();
            }

            _isCalculating = true;
            brdrCalculating.Visibility = Visibility.Visible;

            // start updates/calculations

            UpdateModifiers();

            UpdateCombatTab();

            if (skills)
            {
                //foreach (SkillEditor? item in stkSkills.Children)
                //{
                //    if (item == null)
                //    {
                //        continue;
                //    }

                //    string modifier = "";

                //    switch (item.SkillAbility)
                //    {
                //        case "DEX":
                //            modifier = txtDexm.Text;
                //            break;
                //        case "INT":
                //            modifier = txtIntm.Text;
                //            break;
                //        case "CHA":
                //            modifier = txtCham.Text;
                //            break;
                //        case "STR":
                //            modifier = txtStrm.Text;
                //            break;
                //        case "WIS":
                //            modifier = txtWism.Text;
                //            break;
                //        case "CON":
                //            modifier = txtConm.Text;
                //            break;
                //        default:
                //            break;
                //    }
                //    item.LoadModifier(modifier);

                //    if (totals)
                //    {
                //        await item.UpdateTotals(cts.Token);
                //    }
                //}
            }

            if (currentView == RAWJSON_VIEW)
            {
                SyncEditorFromSheet();
            }

            _isCalculating = false;
            brdrCalculating.Visibility = Visibility.Collapsed;

            _isUpdating = false;

            SetIsDirty();
        }

        public void UpdateModifiers()
        {
            abModifiers["STR"] = CalculateModifierInt(txtStr.Value);
            abModifiers["PER"] = CalculateModifierInt(txtPer.Value);
            abModifiers["END"] = CalculateModifierInt(txtEnd.Value);
            abModifiers["CHA"] = CalculateModifierInt(txtCha.Value);
            abModifiers["INT"] = CalculateModifierInt(txtInt.Value);
            abModifiers["AGI"] = CalculateModifierInt(txtAgi.Value);
            abModifiers["LUK"] = CalculateModifierInt(txtLuk.Value);

            txtStrm.Text = DisplayModifier(abModifiers["STR"]);
            txtPerm.Text = DisplayModifier(abModifiers["PER"]);
            txtEndm.Text = DisplayModifier(abModifiers["END"]);
            txtCham.Text = DisplayModifier(abModifiers["CHA"]);
            txtIntm.Text = DisplayModifier(abModifiers["INT"]);
            txtAgim.Text = DisplayModifier(abModifiers["AGI"]);
            txtLukm.Text = DisplayModifier(abModifiers["LUK"]);
        }

        public void UpdateCombatTab()
        {
            int prBase = (int)Math.Ceiling(nudLevel.Value / 2d);
            int svBase = (int)Math.Floor(nudLevel.Value / 2d);
            txtProwessBase.Text = prBase.ToString();
            int pr = prBase + nudProwessT.Value;
            if (pr > nudEcl.Value) pr = nudEcl.Value;
            txtProwess.Text = pr.ToString();

            txtBrawl.Text = DisplayModifier((chkBrawlAgi.IsChecked ? abModifiers["AGI"] : abModifiers["STR"]) + pr + 1);
            txtMelee.Text = DisplayModifier((chkMeleeAgi.IsChecked ? abModifiers["AGI"] : abModifiers["STR"]) + pr);
            txtRangd.Text = DisplayModifier(abModifiers["AGI"] + pr);

            txtBlock.Text = DisplayModifier(abModifiers["STR"] + pr + (int)Math.Floor(nudArmorS.Value / 2d));
            txtDodge.Text = DisplayModifier(abModifiers["AGI"] + pr);
            txtPassd.Text = (10 + nudArmorN.Value + nudProwessT.Value).ToString();
            txtTouch.Text = (10 + nudProwessT.Value).ToString();

            txtArmor.Text = (nudArmorA.Value + nudArmorE.Value + nudArmorN.Value + nudArmorS.Value + nudArmorM.Value).ToString();

            txtCmb.Text = DisplayModifier(abModifiers["STR"] + pr + txtCmbM.Value);
            txtCmd.Text = DisplayModifier(abModifiers["STR"] + abModifiers["AGI"] + pr + txtCmdM.Value);
            txtMmb.Text = DisplayModifier(abModifiers[powerStat] + pr + txtMmbM.Value);
            txtMmd.Text = DisplayModifier(abModifiers[powerStat] + pr + txtMmdM.Value);

            txtMmbPwrm.Text = DisplayModifier(abModifiers[powerStat]);
            lblMmbPwrm.Text = powerStat + " is your Power stat.";
            txtMmdPwrm.Text = DisplayModifier(abModifiers[powerStat]);
            lblMmdPwrm.Text = powerStat + " is your Power stat.";

            txtSaveFLvl.Text = svBase.ToString();
            txtSaveRLvl.Text = svBase.ToString();
            txtSaveWLvl.Text = svBase.ToString();

            txtSaveF.Text = DisplayModifier(svBase + abModifiers["END"] + txtSaveFRanks.Value + txtSaveFMisc.Value);
            txtSaveR.Text = DisplayModifier(svBase + abModifiers["AGI"] + txtSaveRRanks.Value + txtSaveRMisc.Value);
            txtSaveW.Text = DisplayModifier(svBase + abModifiers["PER"] + txtSaveWRanks.Value + txtSaveWMisc.Value);
        }

        #region Sync

        /// <summary>
        /// Update the sheet views from data in the text editor. Also sets the editor as no longer dirty (out-of-sync), as long as the editor has valid JSON.
        /// </summary>
        /// <returns></returns>
        void SyncSheetFromEditor()
        {
            if (!string.IsNullOrEmpty(txtEditRaw.Text))
            {
                // if no file is loaded, we don't want to write empty data into the raw JSON editor
                if (!_sheetLoaded)
                {
                    return;
                }

                try
                {
                    SentinelsSheet ss = SentinelsSheet.LoadJsonText(txtEditRaw.Text);
                    LoadSentinelsSheet(ss);
                    fileTitle = ss.Name;
                    _isEditorDirty = false;
                    if (fileTitle != displayedTitle) UpdateTitlebar();
                }
                catch (Newtonsoft.Json.JsonReaderException)
                {
                    _isEditorDirty = false;
                    _isTabsDirty = true;
                    SetIsDirty(true, false);
                }
                catch (Newtonsoft.Json.JsonSerializationException)
                {
                    _isEditorDirty = false;
                    _isTabsDirty = true;
                    SetIsDirty(true, false);

                    MessageDialog md = new MessageDialog(App.ColorScheme);
                    md.Message = "This JSON file doesn't seem to look like it's a character sheet at all. " +
                        "It may be good to open the Raw JSON view to check that the file matches what you're expecting.\n\n" +
                        "SentinelsJSON will continue, but if you save any changes, any non-character sheet data may be deleted.";
                    md.Title = "File Check Warning";
                    md.Owner = this;
                    md.Image = MessageDialogImage.Hand;
                    md.ShowDialog();
                }
            }
            else
            {
                _isEditorDirty = false;
                _isTabsDirty = true;
                SetIsDirty(true, false);
            }
        }

        /// <summary>
        /// Update the editor view from data in the sheet views. Also sets the sheet as no longer dirty (out-of-sync).
        /// </summary>
        /// <returns></returns>
        void SyncEditorFromSheet()
        {
            // if no file is loaded, we don't want to write empty data into the raw JSON editor
            if (!_sheetLoaded)
            {
                return;
            }

            SentinelsSheet ps = CreateSentinelsSheet();
            txtEditRaw.Text = ps.SaveJsonText(App.Settings.IndentJsonData);
            _isTabsDirty = false;
        }

        // these two menu commands are hidden
        // hopefully, we shouldn't be needing these commands at all, as the program should automatically do the syncing as needed
        // but we'll have to see if any bugs come up
        private void mnuRefresh_Click(object sender, RoutedEventArgs e)
        {
            SyncSheetFromEditor();
        }

        private void mnuRefreshEditor_Click(object sender, RoutedEventArgs e)
        {
            SyncEditorFromSheet();
        }

        #endregion

        #endregion

        #region General sheet event handlers

        private void scrSheet_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

                if (!SaveDirtyChanges() || CheckCalculating())
                {
                    return;
                }

                LoadFile(files[0]);
            }
        }

        private void textbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                SetIsDirty();
            }

            //lastEditedBox = sender as TextBox;
        }

        private void editor_ContentChanged(object? sender, EventArgs e)
        {
            if (!_isUpdating)
            {
                SetIsDirty();
            }
        }

        private void se_ModifierChanged(object? sender, EventArgs e)
        {
            if (sender is SkillEditor se)
            {
                se.ModifierValue = abModifiers[se.ModifierName];
            }
        }

        private async void txtStr_ValueChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                if (mnuAutoUpdate.IsChecked)
                {
                    await UpdateCalculations(false);
                }

                SetIsDirty();
            }
        }

        private void txtCharacter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                if (fileTitle != txtCharacter.Text)
                {
                    fileTitle = txtCharacter.Text;
                }

                SetIsDirty(); // <-- this includes updating the title bar
            }
        }

        private void btnEditPlayerData_Click(object sender, RoutedEventArgs e)
        {
            UserdataEditor ude = new UserdataEditor();
            ude.LoadUserData(ud);
            ude.Owner = this;

            ude.ShowDialog();
            if (ude.DialogResult)
            {
                // update userdata
                ud = ude.GetUserData();

                // update UI with new userdata
                txtPlayerName.Text = ud.DisplayName;

                string email = "";

                foreach (UserData.Email item in ud.Emails)
                {
                    if (item.Type == "account")
                    {
                        email = item.Value;
                    }
                }
                if (string.IsNullOrEmpty(email))
                {
                    try
                    {
                        email = ud.Emails[0].Value;
                    }
                    catch (IndexOutOfRangeException) { }
                    catch (ArgumentOutOfRangeException) { }
                }
                txtPlayerEmail.Text = email;

                try
                {
                    if (ud.Photos != null)
                    {
                        ImageSource iss = new BitmapImage(new Uri(ud.Photos[0].Value ?? ""));
                        imgPlayer.Source = iss;
                    }
                    else
                    {
                        imgPlayer.Source = null;
                    }
                }
                catch (IndexOutOfRangeException) { imgPlayer.Source = null; }
                catch (NullReferenceException) { imgPlayer.Source = null; }
                catch (ArgumentOutOfRangeException) { imgPlayer.Source = null; }
                catch (System.Net.WebException) { imgPlayer.Source = null; }

                SetIsDirty();
            }
        }

        bool _updatePowerCheck = true;

        private void PowerStat_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_updatePowerCheck) return;

            _updatePowerCheck = false;

            chkStrw.IsChecked = false;
            chkPerw.IsChecked = false;
            chkEndw.IsChecked = false;
            chkIntw.IsChecked = false;
            chkChaw.IsChecked = false;
            chkAgiw.IsChecked = false;
            chkLukw.IsChecked = false;

            if (sender is SolidShineUi.CheckBox c)
            {
                if (c == chkStrw)
                {
                    SetPowerStat("STR");
                    c.IsChecked = true;
                }
                else if (c == chkPerw)
                {
                    SetPowerStat("PER");
                    c.IsChecked = true;
                }
                else if (c == chkEndw)
                {
                    SetPowerStat("END");
                    c.IsChecked = true;
                }
                else if (c == chkIntw)
                {
                    SetPowerStat("INT");
                    c.IsChecked = true;
                }
                else if (c == chkChaw)
                {
                    SetPowerStat("CHA");
                    c.IsChecked = true;
                }
                else if (c == chkAgiw)
                {
                    SetPowerStat("AGI");
                    c.IsChecked = true;
                }
                else if (c == chkLukw)
                {
                    SetPowerStat("LUK");
                    c.IsChecked = true;
                }
                else
                {
                    // not sure what happened, but let's set the default to PER
                    SetPowerStat("PER");
                    chkPerw.IsChecked = true;
                }
            }

            // update MMB and MMD
            if (mnuAutoUpdate.IsChecked)
            {
                UpdateCombatTab();
            }

            _updatePowerCheck = true;

            if (!_isUpdating)
            {
                SetIsDirty();
            }

            void SetPowerStat(string stat)
            {
                powerStat = stat;
            }
        }

        //private void SkillHeaderGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        //{
        //    if (grdSkillHeader.ActualWidth > SkillEditor.WIDE_STATE_THRESHOLD)
        //    {
        //        // activate wide state for the header grid
        //        colSkillModifiers.Width = new GridLength(0);
        //        colSkillExtra.Width = new GridLength(3, GridUnitType.Star);
        //        colSkillExtra.MinWidth = 280;
        //    }
        //    else
        //    {
        //        // disable wide state for the header grid
        //        colSkillModifiers.Width = new GridLength(85);
        //        colSkillExtra.Width = new GridLength(0);
        //        colSkillExtra.MinWidth = 0;
        //    }
        //}


        private void CombatTabChange(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            if (mnuAutoUpdate.IsChecked)
            {
                UpdateCombatTab();
            }

            SetIsDirty();
        }

        private void LevelValueChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (nudEcl.Value < nudLevel.Value) nudEcl.Value = nudLevel.Value;

            if (mnuAutoUpdate.IsChecked)
            {
                UpdateCombatTab();
            }

            SetIsDirty();
        }

        private void CombatTabValueChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (mnuAutoUpdate.IsChecked)
            {
                UpdateCombatTab();
            }

            SetIsDirty();
        }

        #endregion

        #region Notes tab

        private void chkNotesMarkdown_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
            {
                if (chkNotesMarkdown.IsChecked)
                {
                    ShowMarkdownElements();
                }
                else
                {
                    HideMarkdownElements();
                }

                SetIsDirty();
            }
        }

        private void btnNotesEdit_Click(object sender, RoutedEventArgs e)
        {
            OpenNotesEditTab();
        }

        private void btnNotesView_Click(object sender, RoutedEventArgs e)
        {
            OpenNotesViewTab();
        }

        private void txtNotes_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                SetIsDirty();
            }

            vwrNotes.Markdown = txtNotes.Text;
            UpdateMarkdownViewerVisuals();

            //lastEditedBox = sender as TextBox;
        }

        private void HyperlinkCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            string? s = e.Parameter.ToString();
            if (s != null)

                try
                {
                    if (s != null)
                    {
                        OpenBrowser(s);
                    }
                }
                catch (ArgumentNullException)
                {
                    // could not open the link as it is null
                    if (Debugger.IsAttached)
                    {
                        Debugger.Log(0, "vwrNotes", "Link is null, from Notes viewer.\n");
                    }
                }
                catch (System.IO.FileNotFoundException)
                {
                    OpenBrowser(s);
                    // could not open the link as it doesn't exist
                    if (Debugger.IsAttached)
                    {
                        Debugger.Log(0, "vwrNotes", "Link \"" + s + "\" does not exist, from Notes viewer.\n");
                    }
                }
                catch (InvalidOperationException)
                {
                    // could not open the link for some reason
                    if (Debugger.IsAttached)
                    {
                        Debugger.Log(0, "vwrNotes", "Link \"" + s + "\" was not defined, from Notes viewer.\n");
                    }
                }
                catch (Win32Exception)
                {
                    // could not open the link for some reason
                    if (Debugger.IsAttached)
                    {
                        Debugger.Log(0, "vwrNotes", "Link \"" + s + "\" could not be opened, from Notes viewer.\n");
                    }
                }
        }

        void UpdateMarkdownViewerVisuals()
        {
            if (vwrNotes.Document != null)
            {
                vwrNotes.Document.PagePadding = new Thickness(2);

                foreach (var item in vwrNotes.Document.Blocks)
                {
                    item.Padding = new Thickness(0);
                    item.Margin = new Thickness(0, 1, 0, 4);
                }
            }
        }

        public void OpenNotesEditTab()
        {
            notesEdit = true;

            vwrNotes.Visibility = Visibility.Collapsed;
            txtNotes.Visibility = Visibility.Visible;

            btnNotesView.IsSelected = false;
            btnNotesEdit.IsSelected = true;

            btnNotesView.BorderThickness = new Thickness(0, 0, 0, 1);
            btnNotesEdit.BorderThickness = new Thickness(1, 1, 1, 0);
        }

        public void OpenNotesViewTab()
        {
            notesEdit = false;

            vwrNotes.Visibility = Visibility.Visible;
            txtNotes.Visibility = Visibility.Collapsed;

            btnNotesView.IsSelected = true;
            btnNotesEdit.IsSelected = false;

            btnNotesView.BorderThickness = new Thickness(1, 1, 1, 0);
            btnNotesEdit.BorderThickness = new Thickness(0, 0, 0, 1);
        }

        public void HideMarkdownElements()
        {
            OpenNotesEditTab();

            btnNotesEdit.Visibility = Visibility.Collapsed;
            btnNotesView.Visibility = Visibility.Collapsed;
            brdrNotesMarkdown.Visibility = Visibility.Collapsed;

            txtNotes.BorderThickness = new Thickness(1, 1, 1, 1);
        }

        public void ShowMarkdownElements()
        {
            btnNotesEdit.Visibility = Visibility.Visible;
            btnNotesView.Visibility = Visibility.Visible;
            brdrNotesMarkdown.Visibility = Visibility.Visible;

            txtNotes.BorderThickness = new Thickness(1, 0, 1, 1);
        }


        #endregion
    }
}
