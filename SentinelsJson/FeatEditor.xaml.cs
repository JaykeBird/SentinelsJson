using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SentinelsJson.Ild;
using SolidShineUi;
using static SentinelsJson.CoreUtils;

namespace SentinelsJson
{
    /// <summary>
    /// Interaction logic for FeatDisplay.xaml
    /// </summary>
    public partial class FeatEditor : SelectableListItem
    {
        public FeatEditor()
        {
            InitializeComponent();
        }

        public void LoadFeat(Feat f)
        {
            txtName.Text = f.Name;
            txtNotes.Text = f.Notes;
            txtSchool.Text = f.School;
            txtSubschool.Text = f.Subschool;
            txtType.Text = f.Type;
        }

        public Feat GetFeat()
        {
            Feat f = new Feat
            {
                Name = txtName.Text,
                Notes = txtNotes.Text,
                School = txtSchool.Text,
                Subschool = txtSubschool.Text,
                Type = txtType.Text
            };

            return f;
        }

        public bool EnableSpellCheck
        {
            get
            {
                return SpellCheck.GetIsEnabled(txtNotes);
            }
            set
            {
                SpellCheck.SetIsEnabled(txtNotes, value);
            }
        }


        private void btnDetails_IsSelectedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (btnDetails.IsSelected)
            {
                rowDetails.Height = new GridLength(1, GridUnitType.Auto);
                rowDetails.MinHeight = 125;
                imgDetails.ImageName = "UpArrow";
            }
            else
            {
                rowDetails.Height = new GridLength(0);
                rowDetails.MinHeight = 0;
                imgDetails.ImageName = "DownArrow";
            }
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            rowDetails.Height = new GridLength(1, GridUnitType.Auto);
            rowDetails.MinHeight = 125;
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            rowDetails.Height = new GridLength(0);
            rowDetails.MinHeight = 0;
        }

        // event just to update main window's "isDirty" value
        public event EventHandler? ContentChanged;

        private void textbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ContentChanged?.Invoke(this, e);
        }

        public override void ApplyColorScheme(ColorScheme cs)
        {
            base.ApplyColorScheme(cs);

            btnRemove.ApplyColorScheme(cs);
            btnDetails.ApplyColorScheme(cs);
            imgDetails.ApplyColorScheme(cs);
        }

        public override void MapProperties(Dictionary<IldPropertyInfo, object> properties)
        {
            foreach (var item in properties)
            {
                switch (item.Key.Name.ToLowerInvariant())
                {
                    case "name":
                        txtName.Text = item.Value as string;
                        break;
                    case "notes":
                        txtNotes.Text = item.Value as string;
                        break;
                    case "school":
                        txtSchool.Text = item.Value as string;
                        break;
                    case "subschool":
                        txtSubschool.Text = item.Value as string;
                        break;
                    case "type":
                        txtType.Text = item.Value as string;
                        break;
                    default:
                        break;
                }
            }
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            DoRequestDelete();
        }

        private void SelectableListItem_Loaded(object sender, RoutedEventArgs e)
        {
            rowDetails.Height = new GridLength(0);
            rowDetails.MinHeight = 0;
        }
    }
}
