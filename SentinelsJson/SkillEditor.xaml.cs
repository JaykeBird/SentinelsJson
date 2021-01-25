using SolidShineUi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SentinelsJson
{
    /// <summary>
    /// Interaction logic for SkillEditor.xaml
    /// </summary>
    public partial class SkillEditor : UserControl
    {

        public SkillEditor()
        {
            InitializeComponent();
            _init = false;
        }


        public event EventHandler? ContentChanged;
        public event EventHandler? ModifierChanged;

        bool _init = true;
        bool _cbbc = false;

        #region ColorScheme

        public event DependencyPropertyChangedEventHandler? ColorSchemeChanged;

        public static DependencyProperty ColorSchemeProperty
            = DependencyProperty.Register("ColorScheme", typeof(ColorScheme), typeof(SkillEditor),
            new FrameworkPropertyMetadata(new ColorScheme(), new PropertyChangedCallback(OnColorSchemeChanged)));

        public static void OnColorSchemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ColorScheme cs = (e.NewValue as ColorScheme)!;

            if (d is SkillEditor s)
            {
                s.ColorSchemeChanged?.Invoke(d, e);
                s.ApplyColorScheme(cs);
            }
        }

        public ColorScheme ColorScheme
        {
            get => (ColorScheme)GetValue(ColorSchemeProperty);
            set => SetValue(ColorSchemeProperty, value);
        }

        public void ApplyColorScheme(ColorScheme cs)
        {
            if (cs != ColorScheme)
            {
                ColorScheme = cs;
                return;
            }

            BorderBrush = cs.BorderColor.ToBrush();
        }

        #endregion

        public static DependencyProperty HasSpecializationProperty
            = DependencyProperty.Register("HasSpecialization", typeof(bool), typeof(SkillEditor),
            new FrameworkPropertyMetadata(false));

        public bool HasSpecialization
        {
            get => (bool)GetValue(HasSpecializationProperty);
            set => SetValue(HasSpecializationProperty, value);
        }

        public static DependencyProperty SkillNameProperty
            = DependencyProperty.Register("SkillName", typeof(string), typeof(SkillEditor),
            new FrameworkPropertyMetadata("Skill name here"));

        public string SkillName
        {
            get => (string)GetValue(SkillNameProperty);
            set => SetValue(SkillNameProperty, value);
        }

        public static DependencyProperty SpecializationProperty
            = DependencyProperty.Register("Specialization", typeof(string), typeof(SkillEditor),
            new FrameworkPropertyMetadata(""));

        public string Specialization
        {
            get => (string)GetValue(SpecializationProperty);
            set => SetValue(SpecializationProperty, value);
        }

        public static DependencyProperty ModifierNameProperty
            = DependencyProperty.Register("ModifierName", typeof(string), typeof(SkillEditor),
            new FrameworkPropertyMetadata("INT", new PropertyChangedCallback(OnModifierNameChanged)));

        public string ModifierName
        {
            get => (string)GetValue(ModifierNameProperty);
            set => SetValue(ModifierNameProperty, value);
        }

        private static void OnModifierNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkillEditor s)
            {
                s.ModifierNameChanged();
            }
        }

        protected void ModifierNameChanged()
        {
            if (_cbbc) return;

            cbbStat.SelectedIndex = ModifierName switch
            {
                "STR" => 0,
                "PER" => 1,
                "END" => 2,
                "CHA" => 3,
                "INT" => 4,
                "AGI" => 5,
                "LUK" => 6,
                _ => 4,
            };
        }

        private void btnSpecialization_Click(object sender, RoutedEventArgs e)
        {
            StringInputDialog sid = new StringInputDialog(ColorScheme, "Edit Specialzation", "Edit the specialization for the \"" + SkillName + "\" skill.", Specialization.Trim('(', ')', ' ', '\t'));
            sid.SelectTextOnFocus = true;
            sid.ShowDialog();

            if (sid.DialogResult)
            {
                Specialization = "(" + sid.Value + ")";
                ContentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            popEdit.PlacementTarget = btnEdit;
            popEdit.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

            popEdit.IsOpen = true;
            nudRanks.Focus();
        }

        private void cbbStat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_init) return;

            if (cbbStat.SelectedItem is ComboBoxItem cbbi)
            {
                if (cbbi.Content is string s)
                {
                    _cbbc = true;
                    ModifierName = s;
                    ModifierChanged?.Invoke(this, EventArgs.Empty);
                    ContentChanged?.Invoke(this, EventArgs.Empty);
                    _cbbc = false;
                }
            }
        }

        private void popEdit_Opened(object sender, EventArgs e)
        {
            btnEdit.IsSelected = true;
        }

        private void popEdit_Closed(object sender, EventArgs e)
        {
            btnEdit.IsSelected = false;
        }

        private void popEdit_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                popEdit.IsOpen = false;
                btnEdit.Focus();
            }
        }

        void UpdateCalculations()
        {
            int miscTotal = nudRanks.Value + nudMisc.Value + (chkSkill.IsChecked ? 3 : 0);
            int modifier = 0;

            txtMiscTotal.Text = miscTotal.ToString();
            txtMod.Text = modifier.ToString();

            txtTotal.Text = (miscTotal + modifier).ToString();
        }

        private void chkSkill_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdateCalculations();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void nudRanks_ValueChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateCalculations();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
