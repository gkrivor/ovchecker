using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OVChecker
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window
    {
        public static SplashScreen? instance;
        public SplashScreen()
        {
            instance = this;
            InitializeComponent();
        }
        public static void ShowWindow(string? InitialStatus = null)
        {
            if (instance != null) return;
            instance = new SplashScreen();
            if (!string.IsNullOrEmpty(InitialStatus))
            {
                instance.LabelStatus.Content = InitialStatus;
            }
            instance.Show();
            System.Windows.Forms.Application.DoEvents();
        }
        public static async void SetStatus(string status)
        {
            if (instance == null) return;
            instance.LabelStatus.Content = status;
            instance.InvalidateVisual();
            System.Windows.Forms.Application.DoEvents();
        }
        public static void CloseWindow()
        {
            if (instance == null) return;
            instance.Close();
            System.Windows.Forms.Application.DoEvents();
            instance = null;
            System.Windows.Forms.Application.DoEvents();
        }
    }
}
