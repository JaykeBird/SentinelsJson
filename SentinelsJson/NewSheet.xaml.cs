using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SolidShineUi;
using static SentinelsJson.CoreUtils;

namespace SentinelsJson
{
    /// <summary>
    /// Interaction logic for NewSheetWindow.xaml
    /// </summary>
    public partial class NewSheet : FlatWindow
    {
        public NewSheet()
        {
            InitializeComponent();
            _isUpdating = false;

            UpdateModifiers();

            ColorScheme = App.ColorScheme;
        }

        private void window_SourceInitialized(object sender, EventArgs e)
        {
            this.DisableMinimizeAndMaximizeActions();
        }

        public string FileLocation { get; private set; } = "";
        UserData ud = new UserData(true);

        public new bool DialogResult { get; set; } = false;
        public SentinelsSheet Sheet { get; private set; } = new SentinelsSheet();

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCharacterName.Text))
            {
                MessageDialog md = new MessageDialog(ColorScheme);
                md.Image = MessageDialogImage.Error;
                md.Message = "Please enter a character name before continuing.";
                md.Title = "Missing Data";
                md.Owner = this;
                md.ShowDialog();
                return;
            }

            // Create Sentinels sheet
            // Including ability scores
            // (I also set up the RawAbilities property despite it not really being used, in case it may become an issue later on)
            Sheet = SentinelsSheet.CreateNewSheet(txtCharacterName.Text, nudLevel.Value, nudCpLevel.Value, nudCpStart.Value, ud);

            Sheet.Strength = txtStr.Value;
            Sheet.Perception = txtPer.Value;
            Sheet.Endurance = txtEnd.Value;
            Sheet.Charisma = txtCha.Value;
            Sheet.Intellect = txtInt.Value;
            Sheet.Agility = txtAgi.Value;
            Sheet.Luck = txtLuk.Value;

            Sheet.PotStrength = txtStrp.Value;
            Sheet.PotPerception = txtPerp.Value;
            Sheet.PotEndurance = txtEndp.Value;
            Sheet.PotCharisma = txtChap.Value;
            Sheet.PotIntellect = txtIntp.Value;
            Sheet.PotAgility = txtAgip.Value;
            Sheet.PotLuck = txtLukp.Value;

            Dictionary<string, string> abilities = new Dictionary<string, string>
            {
                { "str", txtStr.Value.ToString() },
                { "per", txtPer.Value.ToString() },
                { "end", txtEnd.Value.ToString() },
                { "cha", txtCha.Value.ToString() },
                { "int", txtInt.Value.ToString() },
                { "agi", txtAgi.Value.ToString() },
                { "luk", txtLuk.Value.ToString() }
            };
            Sheet.RawAbilities = abilities;
            Dictionary<string, string> potentials = new Dictionary<string, string>
            {
                { "str", txtStrp.Value.ToString() },
                { "per", txtPerp.Value.ToString() },
                { "end", txtEndp.Value.ToString() },
                { "cha", txtChap.Value.ToString() },
                { "int", txtIntp.Value.ToString() },
                { "agi", txtAgip.Value.ToString() },
                { "luk", txtLukp.Value.ToString() }
            };
            Sheet.RawPotential = potentials;
            Sheet.PowerStatName = powerStat;

            Sheet.SkillList = cbbSkillList.SelectedIndex switch
            {
                0 => "standard",
                1 => "simplified",
                2 => "pathfinder",
                3 => "none",
                _ => "standard" // TODO: add handling for custom skill list files
            };

            if (!string.IsNullOrEmpty(FileLocation))
            {
                Sheet.SaveJsonFile(FileLocation, App.Settings.IndentJsonData);
            }

            DialogResult = true;
            Close();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Ability scores

        Dictionary<string, int> abModifiers = new Dictionary<string, int>();

        string powerStat = "PER";

        bool _updatePowerCheck = true;

        bool _isUpdating = true;

        private void txtStr_ValueChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateModifiers();
        }

        public void UpdateModifiers()
        {
            if (_isUpdating) return;

            txtStrm.Text = CalculateModifier(txtStr.Value);
            txtPerm.Text = CalculateModifier(txtPer.Value);
            txtEndm.Text = CalculateModifier(txtEnd.Value);
            txtCham.Text = CalculateModifier(txtCha.Value);
            txtIntm.Text = CalculateModifier(txtInt.Value);
            txtAgim.Text = CalculateModifier(txtAgi.Value);
            txtLukm.Text = CalculateModifier(txtLuk.Value);
        }

        private void PowerStat_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
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

            _updatePowerCheck = true;

            void SetPowerStat(string stat)
            {
                powerStat = stat;
            }
        }

        #endregion

        #region UserData / File Location
        private void chkNoLoc_Checked(object sender, RoutedEventArgs e)
        {
            FileLocation = "";
            //txtFilename.Text = "(location not set)";
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Set File Location",
                Filter = "JSON Character Sheet|*.json",

            };

            if (string.IsNullOrEmpty(FileLocation))
            {
                sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                sfd.InitialDirectory = System.IO.Directory.GetParent(FileLocation).FullName;
            }

            if (sfd.ShowDialog().GetValueOrDefault(false))
            {
                FileLocation = sfd.FileName;
                //txtFilename.Text = System.IO.Path.GetFileName(sfd.FileName);
                //chkNoLoc.IsChecked = false;
            }
        }

        private void btnImportData_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Title = "Import Data from File";
            ofd.Filter = "Pathfinder Character Sheet|*.json|All Files|*.*";

            if (ofd.ShowDialog() ?? false == true)
            {
                string filename = ofd.FileName;
                SentinelsSheet ps = SentinelsSheet.LoadJsonFile(filename);
                ud = ps.Player ?? new UserData(true);
                if (!string.IsNullOrEmpty(ud.DisplayName))
                {
                    txtPlayerName.Text = ud.DisplayName;
                }
                else
                {
                    txtPlayerName.Text = "(not set)";
                }
            }
        }

        private void btnEditData_Click(object sender, RoutedEventArgs e)
        {
            UserdataEditor ude = new UserdataEditor();
            ude.Owner = this;
            ude.LoadUserData(ud);

            ude.ShowDialog();
            if (ude.DialogResult)
            {
                ud = ude.GetUserData();
                if (!string.IsNullOrEmpty(ud.DisplayName))
                {
                    txtPlayerName.Text = ud.DisplayName;
                }
                else
                {
                    txtPlayerName.Text = "(not set)";
                }
            }
        }
        #endregion
    }
}
