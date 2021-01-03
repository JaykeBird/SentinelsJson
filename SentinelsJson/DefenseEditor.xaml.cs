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
    /// Interaction logic for ArmorEditor.xaml
    /// </summary>
    public partial class DefenseEditor : UserControl
    {
        public DefenseEditor()
        {
            InitializeComponent();
        }

        private void btnShowHide_Click(object sender, RoutedEventArgs e)
        {
            if (rowDetails.ActualHeight != 0)
            {
                // hide list
                rowDetails.Height = new GridLength(0);

                imgShowHide.ImageName = "DownArrow";
                txtShowHide.Text = "Show Details";
            }
            else
            {
                // show list
                rowDetails.Height = new GridLength(1, GridUnitType.Auto);

                imgShowHide.ImageName = "DownArrow";
                txtShowHide.Text = "Hide Details";
            }
        }

        #region ColorScheme

        public event DependencyPropertyChangedEventHandler? ColorSchemeChanged;

        public static DependencyProperty ColorSchemeProperty
            = DependencyProperty.Register("ColorScheme", typeof(ColorScheme), typeof(DefenseEditor),
            new FrameworkPropertyMetadata(new ColorScheme(), new PropertyChangedCallback(OnColorSchemeChanged)));

        public static void OnColorSchemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ColorScheme cs = (e.NewValue as ColorScheme)!;

            if (d is DefenseEditor s)
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

        public int Prowess { get; set; }

    }
}
