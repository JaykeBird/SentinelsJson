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

        //public void LoadFeat(Feat f)
        //{
        //    txtName.Text = f.Name;
        //    txtNotes.Text = f.Notes;
        //    txtSchool.Text = f.School;
        //    txtSubschool.Text = f.Subschool;
        //    txtType.Text = f.Type;
        //}

        //public Feat GetFeat()
        //{
        //    Feat f = new Feat
        //    {
        //        Name = txtName.Text,
        //        Notes = txtNotes.Text,
        //        School = txtSchool.Text,
        //        Subschool = txtSubschool.Text,
        //        Type = txtType.Text
        //    };

        //    return f;
        //}

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

        private void textbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            DoContentChanged();
        }

        public override void ApplyColorScheme(ColorScheme cs)
        {
            base.ApplyColorScheme(cs);

            btnRemove.ApplyColorScheme(cs);
            btnDetails.ApplyColorScheme(cs);
        }

        public override void LoadValues(Dictionary<IldPropertyInfo, object> properties)
        {
            foreach (var item in properties)
            {
                //var pi = this.GetType().GetProperty(item.Key.Name);
                //pi.SetValue(this, item.Value);
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

        public override object? GetPropertyValue(IldPropertyInfo property)
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "name":
                    return txtName.Text;
                case "notes":
                    return txtNotes.Text;
                case "school":
                    return txtSchool.Text;
                case "subschool":
                    return txtSubschool.Text;
                case "type":
                    return txtType.Text;
                default:
                    return null;
            }
        }

        public override Dictionary<string, object> GetAllProperties()
        {
            return new Dictionary<string, object>()
            {
                {"name", txtName.Text },
                {"notes", txtNotes.Text },
                {"school", txtSchool.Text },
                {"subschool", txtSubschool.Text },
                {"type", txtType.Text },
            };
        }
    }
}
