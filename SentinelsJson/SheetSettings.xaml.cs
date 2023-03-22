using SolidShineUi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using SentinelsJson;

namespace SentinelsJson
{
    /// <summary>
    /// Interaction logic for SheetSettings.xaml
    /// </summary>
    public partial class SheetSettings : FlatWindow
    {
        public SheetSettings()
        {
            InitializeComponent();

            ColorScheme = App.ColorScheme;
        }

        private void window_SourceInitialized(object sender, EventArgs e)
        {
            this.DisableMinimizeAndMaximizeActions();
        }

        public void UpdateUi()
        {
            nudCpLevel.Value = CpPerLevel;
            nudCpStart.Value = Level0Cp;

            cbbSkillList.SelectedIndex = SkillList switch
            {
                "none" => 3,
                "pathfinder" => 2,
                "standard" => 0,
                "full" => 0,
                "simplified" => 1,
                "" => 3,
                null => 3,
                _ => 3,
            };

            foreach (KeyValuePair<string, string?> kvp in SheetSettingsList)
            {
                SelectableItem si = new SelectableItem("Name: \"" + kvp.Key + "\", Value: \"" + (kvp.Value ?? "(empty)") + "\"", null, 5);
                si.Tag = kvp;
                selSheetSettings.Items.Add(si);
            }
        }

        public Dictionary<string, string?> SheetSettingsList { get; set; } = new Dictionary<string, string?>();

        public int CpPerLevel { get; set; } = 10;

        public int Level0Cp { get; set; } = 10;

        public string SkillList { get; set; } = "None";

        public new bool DialogResult { get; set; } = false;

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            CpPerLevel = nudCpLevel.Value;
            Level0Cp = nudCpStart.Value;

            if (fileSelect.SelectedFiles.Count == 0)
            {
                rdoSkillList.IsChecked = true;
            }

            if (rdoSkillList.IsChecked.GetValueOrDefault(false))
            {
                SkillList = cbbSkillList.SelectedIndex switch
                {
                    0 => "standard",
                    1 => "simplified",
                    2 => "pathfinder",
                    3 => "none",
                    _ => "standard"
                };
            }
            else
            {
                SkillList = fileSelect.SelectedFiles[0];
            }

            Dictionary<string, string?> newShettings = new Dictionary<string, string?>();

            foreach (SelectableUserControl item in selSheetSettings.Items)
            {
                if (item.Tag is KeyValuePair<string, string?> kvp)
                {
                    newShettings[kvp.Key] = kvp.Value;
                }
            }

            SheetSettingsList = newShettings;

            DialogResult = true;
            Close();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void fileSelect_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (fileSelect.SelectedFiles.Count == 0)
            {
                rdoSkillList.IsChecked = true;
            }
            else
            {
                rdoSelectFile.IsChecked = true;
            }
        }

        private void cbbSkillList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            rdoSkillList.IsChecked = true;
        }

        private void btnAddSetting_Click(object sender, RoutedEventArgs e)
        {
            // TO BE ADDED
            AddSettingValueWindow asv = new AddSettingValueWindow();
            asv.ColorScheme = ColorScheme;
            asv.ShowDialog();

            if (asv.DialogResult)
            {
                SelectableItem si = new SelectableItem();
                si.Tag = new KeyValuePair<string, string>(asv.SettingName, asv.SettingValue);
                si.Text = $"Name: \"{asv.SettingName}\", Value: \"{asv.SettingValue}\"";

                selSheetSettings.Items.Add(si);
            }
        }

        private void btnEditSetting_Click(object sender, RoutedEventArgs e)
        {
            SelectableItem si = selSheetSettings.Items.SelectedItems.OfType<SelectableItem>().First();
            if (si.Tag is KeyValuePair<string, string?> kvp)
            {
                StringInputDialog sid = new StringInputDialog(App.ColorScheme, "Set Setting Value", "Set the value for the setting \"" + kvp.Key + "\":", kvp.Value ?? "");
                sid.SelectTextOnFocus = true;
                sid.ShowDialog();

                if (sid.DialogResult)
                {
                    si.Tag = new KeyValuePair<string, string?>(kvp.Key, sid.Value);
                    si.Text = "Name: \"" + kvp.Key + "\", Value: \"" + sid.Value + "\"";
                }
            }
        }

        private void btnRemoveSetting_Click(object sender, RoutedEventArgs e)
        {
            MessageDialog md = new MessageDialog(ColorScheme);
            md.ShowDialog("Are you sure you want to remove the selected setting?", null, this, "Confirm Remove", MessageDialogButtonDisplay.Two, MessageDialogImage.Question);
            if (md.DialogResult == MessageDialogResult.OK)
            {
                selSheetSettings.RemoveSelectedItems();
            }
        }

        private void selSheetSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnEditSetting.IsEnabled = selSheetSettings.Items.SelectedItems.Count != 0;
            btnRemoveSetting.IsEnabled = selSheetSettings.Items.SelectedItems.Count != 0;
        }

    }
}
