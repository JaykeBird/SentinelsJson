﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SolidShineUi;
using static SentinelsJson.CoreUtils;

namespace SentinelsJson.About
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : FlatWindow
    {
        public About()
        {
            InitializeComponent();

            ApplyColorScheme(App.ColorScheme);
            brdrDivider.BorderBrush = new SolidColorBrush(App.ColorScheme.BorderColor);

            lblVersion.Text = "Version " + App.AppVersion.ToString();
            //btnClose.ApplyColorScheme(App.ColorScheme);
            //btnThirdParty.ApplyColorScheme(App.ColorScheme);
        }

        private void LinkTextBlock_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("http://charactersheet.co.uk/pathfinder/");
        }

        private void LinkTextBlock2_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://jaykebird.com/");
        }

        private void LinkTextBlock5_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://github.com/JaykeBird/SentinelsJson/");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnThirdParty_Click(object sender, RoutedEventArgs e)
        {
            ThirdPartyCredits tpc = new ThirdPartyCredits();
            tpc.Owner = this;
            tpc.ShowDialog();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            OpenBrowser(e.Uri.AbsoluteUri);
        }

        private void BtnPlatformInfo_Click(object sender, RoutedEventArgs e)
        {
            PlatformInfo pi = new PlatformInfo();
            pi.Owner = this;
            pi.ShowDialog();
        }
    }
}
