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
        string? version;

        // keyboard/method data
        RoutedMethodRegistry mr = new RoutedMethodRegistry();
        KeyboardShortcutHandler ksh;
        KeyRegistry kr = new KeyRegistry();

        #region Constructor/ window events/ basic functions

        public MainWindow()
        {
            ud = new UserData(false);
            sheetid = "-1";

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
                        break;
                    case "2":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.GreenOnBlack);
                        break;
                    case "3":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.BlackOnWhite);
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
            foreach (SelectableItem item in selTabs.GetSelectedItemsOfType<SelectableItem>())
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
            if (App.Settings.EditorSyntaxHighlighting && App.Settings.HighContrastTheme == NO_HIGH_CONTRAST)
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
                        break;
                    case "2":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.GreenOnBlack);
                        break;
                    case "3":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.BlackOnWhite);
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

            // setup up raw JSON editor
            if (App.Settings.EditorSyntaxHighlighting && App.Settings.HighContrastTheme == NO_HIGH_CONTRAST)
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
                Title = "Pathfinder JSON";
                displayedTitle = "";
            }
            else
            {
                if (App.Settings.PathInTitleBar)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Title = "Pathfinder JSON - New File";
                        displayedTitle = "New File";
                    }
                    else
                    {
                        Title = "Pathfinder JSON - " + Path.GetFileName(filePath);
                        displayedTitle = Path.GetFileName(filePath);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(fileTitle))
                    {
                        Title = "Pathfinder JSON - (unnamed character)";
                        displayedTitle = fileTitle;
                    }
                    else
                    {
                        Title = "Pathfinder JSON - " + fileTitle;
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

            //NewSheet ns = new NewSheet();
            //ns.Owner = this;
            //ns.ShowDialog();

            //if (ns.DialogResult)
            //{
            //    PathfinderSheet ps = ns.Sheet;

            //    filePath = ns.FileLocation;
            //    fileTitle = ps.Name;
            //    _sheetLoaded = true;

            //    isDirty = false;
            //    _isEditorDirty = false;
            //    _isTabsDirty = false;

            //    UpdateTitlebar();

            //    txtEditRaw.Text = ps.SaveJsonText(App.Settings.IndentJsonData);
            //    ChangeView(App.Settings.StartView, false, false);
            //    LoadPathfinderSheet(ps);
            //    await UpdateCalculations();
            //}
        }

        private void mnuNewWindow_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Process.GetCurrentProcess().MainModule?.FileName);
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
                //SyncSheetFromEditor();
            }
            else
            {
                //SyncEditorFromSheet();
                txtEditRaw.Save(file);
                //PathfinderSheet ps = await CreatePathfinderSheetAsync();
                //ps.SaveJsonFile(file, App.Settings.IndentJsonData);
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
            mi.Click += miRecentFile_Click;
            mi.ContextMenuOpening += miRecentContext_Opening;
            mnuRecent.Items.Insert(0, mi);

            SolidShineUi.ContextMenu cm = new SolidShineUi.ContextMenu();
            cm.PlacementTarget = mi;
            cm.Width = 180;

            MenuItem cm1 = new MenuItem();
            cm1.Header = "Open";
            cm1.Tag = mi;
            cm1.Click += miRecentOpen_Click;
            cm.Items.Add(cm1);

            MenuItem cm4 = new MenuItem();
            cm4.Header = "Open in New Window";
            cm4.Tag = mi;
            cm4.Click += miRecentOpenNew_Click;
            cm.Items.Add(cm4);

            MenuItem cm5 = new MenuItem();
            cm5.Header = "Copy Path";
            cm5.Tag = mi;
            cm5.Click += miRecentCopy_Click;
            cm.Items.Add(cm5);

            MenuItem cm2 = new MenuItem();
            cm2.Header = "View in Explorer";
            cm2.Tag = mi;
            cm2.Click += miRecentView_Click;
            cm.Items.Add(cm2);

            MenuItem cm3 = new MenuItem();
            cm3.Header = "Remove";
            cm3.Tag = mi;
            cm3.Click += miRecentRemove_Click;
            cm.Items.Add(cm3);

            mi.ContextMenu = cm;

            if (storeInSettings)
            {
                App.Settings.RecentFiles.Add(filename);
                JumpList.AddToRecentCategory(filename);
                SaveSettings();
            }

            mnuRecentEmpty.Visibility = Visibility.Collapsed;
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
                        md.ShowDialog("This file does not exist any more. Do you want to remove this file from the list or attempt to open anyway?", App.ColorScheme, this,
                            "File Not Found", MessageDialogButtonDisplay.Auto, MessageDialogImage.Error, MessageDialogResult.Cancel, "Remove file from list", "Attempt to open anyway");
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

        bool AskClearRecentList()
        {
            MessageDialog md = new MessageDialog(App.ColorScheme);
            md.ShowDialog("Are you sure you want to remove all files from the Recent Files list?", App.ColorScheme, this, "Clear Recent Files List", MessageDialogButtonDisplay.Two, MessageDialogImage.Question, MessageDialogResult.Cancel,
                "Yes", "Cancel");

            if (md.DialogResult == MessageDialogResult.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
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
                        Process.Start(Process.GetCurrentProcess().MainModule?.FileName, "\"" + file + "\"");
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

                //txtStrm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                //txtDexm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                //txtCham.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                //txtConm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                //txtIntm.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);
                //txtWism.BorderBrush = new SolidColorBrush(App.ColorScheme.LightDisabledColor);

                //txtStrm.Background = new SolidColorBrush(SystemColors.ControlColor);
                //txtDexm.Background = new SolidColorBrush(SystemColors.ControlColor);
                //txtCham.Background = new SolidColorBrush(SystemColors.ControlColor);
                //txtConm.Background = new SolidColorBrush(SystemColors.ControlColor);
                //txtIntm.Background = new SolidColorBrush(SystemColors.ControlColor);
                //txtWism.Background = new SolidColorBrush(SystemColors.ControlColor);
            }
            else
            {
                foreach (SelectableItem item in selTabs.GetItemsAsType<SelectableItem>())
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

                //txtStrm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                //txtDexm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                //txtCham.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                //txtConm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                //txtIntm.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);
                //txtWism.BorderBrush = new SolidColorBrush(SystemColors.ControlDarkColor);

                //txtStrm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                //txtDexm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                //txtCham.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                //txtConm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                //txtIntm.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
                //txtWism.Background = App.ColorScheme.SecondHighlightColor.ToBrush();
            }

            brdrCalculating.Background = App.ColorScheme.SecondaryColor.ToBrush();
            brdrCalculating.BorderBrush = App.ColorScheme.HighlightColor.ToBrush();

            (txtEditRaw.ContextMenu as SolidShineUi.ContextMenu)!.ApplyColorScheme(App.ColorScheme);

            //foreach (SkillEditor? item in stkSkills.Children)
            //{
            //    if (item == null) continue;
            //    item.UpdateAppearance();
            //}

            //btnNotesEdit.Background = Color.FromArgb(1, 0, 0, 0).ToBrush();
            //btnNotesView.Background = Color.FromArgb(1, 0, 0, 0).ToBrush();

            //foreach (SpellEditor item in selSpells.GetItemsAsType<SpellEditor>())
            //{
            //    item.ApplyColorScheme(App.ColorScheme);
            //}
        }

        void SetupTabs()
        {
            selTabs.AddItem(CreateTab("General"));
            selTabs.AddItem(CreateTab("Skills"));
            selTabs.AddItem(CreateTab("Combat"));
            selTabs.AddItem(CreateTab("Spells"));
            selTabs.AddItem(CreateTab("Feats/Abilities"));
            selTabs.AddItem(CreateTab("Items"));
            selTabs.AddItem(CreateTab("Notes"));

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
            selTabs[0].IsSelected = true;
            LoadTab("General");
        }

        void LoadTab(string text)
        {
            //if (currentView == TABS_VIEW)
            //{
            //    SetAllTabsVisibility(Visibility.Collapsed);

            //    //txtLoc.Text = text;
            //    switch (text)
            //    {
            //        case "General":
            //            grdGeneral.Visibility = Visibility.Visible;
            //            break;
            //        case "Skills":
            //            grdSkills.Visibility = Visibility.Visible;
            //            break;
            //        case "Combat":
            //            grdCombat.Visibility = Visibility.Visible;
            //            break;
            //        case "Spells":
            //            grdSpells.Visibility = Visibility.Visible;
            //            break;
            //        case "Feats/Abilities":
            //            grdFeats.Visibility = Visibility.Visible;
            //            break;
            //        case "Items":
            //            grdItems.Visibility = Visibility.Visible;
            //            break;
            //        case "Notes":
            //            grdNotes.Visibility = Visibility.Visible;
            //            break;
            //        default:
            //            break;
            //    }
            //    scrSheet.ScrollToVerticalOffset(0);
            //}
            //else
            //{
            //    SetAllTabsVisibility();
            //    //txtLoc.Text = "All Tabs";

            //    TextBlock control = titGeneral;

            //    switch (text)
            //    {
            //        case "General":
            //            control = titGeneral;
            //            break;
            //        case "Skills":
            //            control = titSkills;
            //            break;
            //        case "Combat":
            //            control = titCombat;
            //            break;
            //        case "Spells":
            //            control = titSpells;
            //            break;
            //        case "Feats/Abilities":
            //            control = titFeats;
            //            break;
            //        case "Items":
            //            control = titItems;
            //            break;
            //        case "Notes":
            //            control = titNotes;
            //            break;
            //        default:
            //            break;
            //    }

            //    Point relativeLocation = control.TranslatePoint(new Point(0, 0), stkSheet);
            //    scrSheet.ScrollToVerticalOffset(relativeLocation.Y - 5);
            //}
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

        void SetAllTabsVisibility(Visibility visibility = Visibility.Visible)
        {
            //grdGeneral.Visibility = visibility;
            //grdSkills.Visibility = visibility;
            //grdCombat.Visibility = visibility;
            //grdSpells.Visibility = visibility;
            //grdFeats.Visibility = visibility;
            //grdItems.Visibility = visibility;
            //grdNotes.Visibility = visibility;
        }

        private void mnuColors_Click(object sender, RoutedEventArgs e)
        {
            if (App.Settings.HighContrastTheme != NO_HIGH_CONTRAST)
            {
                MessageDialog md = new MessageDialog(App.ColorScheme);
                if (md.ShowDialog("A high-contrast theme is currently being used. Changing the color scheme will turn off the high-contrast theme. Do you want to continue?", null, this, "High Contrast Theme In Use", MessageDialogButtonDisplay.Two,
                    MessageDialogImage.Warning, MessageDialogResult.Cancel, "Continue", "Cancel") == MessageDialogResult.Cancel)
                {
                    return;
                }
            }

            ColorPickerDialog cpd = new ColorPickerDialog(App.ColorScheme, App.ColorScheme.MainColor);
            cpd.Owner = this;
            cpd.ShowDialog();

            if (cpd.DialogResult)
            {
                App.ColorScheme = new ColorScheme(cpd.SelectedColor);
                App.Settings.ThemeColor = cpd.SelectedColor.GetHexString();
                App.Settings.HighContrastTheme = NO_HIGH_CONTRAST;
                SaveSettings();
                UpdateAppearance();
            }
        }

        private void mnuHighContrast_Click(object sender, RoutedEventArgs e)
        {
            MessageDialog md = new MessageDialog(App.ColorScheme)
            {
                ExtraButton1Text = "Use White on Black",
                ExtraButton2Text = "Use Green on Black",
                ExtraButton3Text = "Use Black on White",
                OkButtonText = "Don't use",
                Message = "A high contrast theme is good for users who have vision-impairment or other issues. PathfinderJSON comes with 3 high-contrast options available.",
                Title = "High Contrast Theme"
            };

            md.ShowDialog();

            switch (md.DialogResult)
            {
                case MessageDialogResult.OK:
                    App.Settings.HighContrastTheme = NO_HIGH_CONTRAST;
                    break;
                case MessageDialogResult.Cancel:
                    App.Settings.HighContrastTheme = NO_HIGH_CONTRAST;
                    break;
                case MessageDialogResult.Extra1:
                    App.Settings.HighContrastTheme = "1"; // white on black
                    break;
                case MessageDialogResult.Extra2:
                    App.Settings.HighContrastTheme = "2"; // green on black
                    break;
                case MessageDialogResult.Extra3:
                    App.Settings.HighContrastTheme = "3"; // black on white
                    break;
                default:
                    break;
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
                        break;
                    case "2":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.GreenOnBlack);
                        break;
                    case "3":
                        App.ColorScheme = ColorScheme.GetHighContrastScheme(HighContrastOption.BlackOnWhite);
                        break;
                    default:
                        App.Settings.HighContrastTheme = NO_HIGH_CONTRAST;
                        App.ColorScheme = new ColorScheme(ColorsHelper.CreateFromHex(App.Settings.ThemeColor));
                        break;
                }
            }

            SaveSettings();
            UpdateAppearance();
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
                        //SyncSheetFromEditor();
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
                        //SyncSheetFromEditor();
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
                        //SyncEditorFromSheet();
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
                foreach (SelectableItem item in selTabs.GetSelectedItemsOfType<SelectableItem>())
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

        private void mnuDiceRoll_Click(object sender, RoutedEventArgs e)
        {
            DiceRollerWindow drw = new DiceRollerWindow();
            drw.ColorScheme = App.ColorScheme;
            drw.Show();
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
            //Options o = new Options();
            //o.Owner = this;
            //o.ColorScheme = App.ColorScheme;

            //o.ShowDialog();

            //if (o.DialogResult)
            //{
            //    ReloadSettings();
            //}
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
            OpenBrowser("https://github.com/JaykeBird/PathfinderJson/wiki/Keyboard-Shortcuts");
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
            //About a = new About();
            //a.Owner = this;
            //a.ShowDialog();
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
                using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PathfinderJson.Json.xshd"))
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
                using (Stream? s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PathfinderJson.None.xshd"))
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
                //LoadPathfinderSheet(ps);
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

        void LoadSentinelsSheet(SentinelsSheet ss)
        {

        }

        #endregion

        #region Sync Editors / update sheet / CreatePathfinderSheetAsync

        #region Update UI (Calculate menu)

        private async void mnuUpdate_Click(object sender, RoutedEventArgs e)
        {
            await UpdateCalculations(true, mnuUpdateTotals.IsChecked);
        }

        private void mnuAutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            mnuAutoUpdate.IsChecked = !mnuAutoUpdate.IsChecked;
        }

        private void mnuUpdateTotals_Click(object sender, RoutedEventArgs e)
        {
            mnuUpdateTotals.IsChecked = !mnuUpdateTotals.IsChecked;
        }

        #endregion


        async Task UpdateCalculations(bool skills = true, bool totals = true)
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

            //txtStrm.Text = CalculateModifier(txtStr.Value);
            //txtDexm.Text = CalculateModifier(txtDex.Value);
            //txtCham.Text = CalculateModifier(txtCha.Value);
            //txtConm.Text = CalculateModifier(txtCon.Value);
            //txtIntm.Text = CalculateModifier(txtInt.Value);
            //txtWism.Text = CalculateModifier(txtWis.Value);

            // update core modifier

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


            if (totals)
            {
                // update totals for editors
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
                        "PathfinderJSON will continue, but if you save any changes, any non-character sheet data may be deleted.";
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

        /// <summary>
        /// Create a PathfinderSheet object by loading in all the values from the sheet view.
        /// </summary>
        /// <returns></returns>
        private SentinelsSheet CreateSentinelsSheet()
        {
            return new SentinelsSheet();
        }

        #endregion
    }
}
