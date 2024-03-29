﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SolidShineUi;
using static SentinelsJson.CoreUtils;

namespace SentinelsJson
{
    /// <summary>
    /// Interaction logic for UserdataEditor.xaml
    /// </summary>
    public partial class UserdataEditor : FlatWindow
    {
        public UserdataEditor()
        {
            InitializeComponent();
            ApplyColorScheme(App.ColorScheme);
        }

        private void window_SourceInitialized(object sender, EventArgs e)
        {
            DisableMinimizeAction();
        }

        public new bool DialogResult { get; set; } = false;

        public void LoadUserData(UserData ud)
        {
            cbbProvider.SelectedIndex = ud.Provider.ToLowerInvariant() switch
            {
                "google" => 0,
                "github" => 1,
                "jaykebird" => 2,
                "local" => 3,
                _ => 3,
            };

            txtName.Text = ud.DisplayName;
            txtProfileUrl.Text = ud.ProfileUrl;
            txtUserId.Text = ud.Id;

            selEmails.Items.Clear();
            selPhotos.Items.Clear();

            foreach (UserData.Email item in ud.Emails)
            {
                SelectableItem si = new SelectableItem(item.Value);
                si.AllowTextEditing = true;

                //if (!ColorScheme.IsHighContrast)
                //{

                //}

                var img = App.GetResourcesImage("Email", ColorScheme);
                si.ImageSource = img;
                si.ShowImage = true;

                selEmails.Items.Add(si);
            }

            foreach (UserData.Photo item in ud.Photos)
            {
                SelectableItem si = new SelectableItem(item.Value ?? "");
                si.AllowTextEditing = true;

                //if (!ColorScheme.IsHighContrast)
                //{

                //}

                si.ImageSource = App.GetResourcesImage("Link", ColorScheme);
                si.ShowImage = true;

                selPhotos.Items.Add(si);
            }
        }

        public UserData GetUserData()
        {
            UserData ud = new UserData();

            ud.Provider = cbbProvider.SelectedIndex switch
            {
                0 => "google",
                1 => "github",
                2 => "jaykebird",
                3 => "local",
                _ => "local",
            };

            ud.DisplayName = GetStringOrNull(txtName.Text);
            ud.ProfileUrl = GetStringOrNull(txtProfileUrl.Text);
            ud.Id = GetStringOrNull(txtUserId.Text);

            foreach (SelectableItem item in selEmails.Items.OfType<SelectableItem>())
            {
                ud.Emails.Add(new UserData.Email { Value = item.Text });
            }

            foreach (SelectableItem item in selPhotos.Items.OfType<SelectableItem>())
            {
                ud.Photos.Add(new UserData.Photo { Value = item.Text });
            }

            return ud;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Title = "Import Data from File";
            ofd.Filter = "Pathfinder Character Sheet|*.json|All Files|*.*";

            if (ofd.ShowDialog() ?? false == true)
            {
                string filename = ofd.FileName;
                SentinelsSheet ps = SentinelsSheet.LoadJsonFile(filename);
                LoadUserData(ps.Player ?? new UserData(true));
            }

            btnImport.Focus();
            foreach (SelectableItem item in selEmails.Items.OfType<SelectableItem>())
            {
                item.CancelEdit();
            }
            foreach (SelectableItem item in selPhotos.Items.OfType<SelectableItem>())
            {
                item.CancelEdit();
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        #region Select Panel controls
        private void btnAddEmail_Click(object sender, RoutedEventArgs e)
        {
            SelectableItem si = new SelectableItem();
            si.AllowTextEditing = true;

            si.ImageSource = App.GetResourcesImage("Email", ColorScheme);
            si.ShowImage = true;

            selEmails.Items.Add(si);
            si.Focus();
            si.DisplayEditText();
        }

        private void btnDeleteEmail_Click(object sender, RoutedEventArgs e)
        {
            selEmails.RemoveSelectedItems();
        }

        private void btnDeselectEmail_Click(object sender, RoutedEventArgs e)
        {
            selEmails.Items.ClearSelection();
        }

        private void btnAddPhoto_Click(object sender, RoutedEventArgs e)
        {
            SelectableItem si = new SelectableItem();
            si.AllowTextEditing = true;

            si.ImageSource = App.GetResourcesImage("Link", ColorScheme);
            si.ShowImage = true;

            selPhotos.Items.Add(si);
            si.Focus();
            si.DisplayEditText();
        }

        private void btnDeletePhoto_Click(object sender, RoutedEventArgs e)
        {
            selPhotos.RemoveSelectedItems();
        }

        private void btnDeselectPhoto_Click(object sender, RoutedEventArgs e)
        {
            selPhotos.Items.ClearSelection();
        }
        #endregion

    }
}
