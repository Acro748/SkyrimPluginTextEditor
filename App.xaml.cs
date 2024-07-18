using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace SkyrimPluginTextEditor
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public static readonly string Version = "v1.2.0";
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Config.GetSingleton.ConfigRead();
            MainWindow newWindow = new MainWindow();
            newWindow.Title += " " + Version;
            newWindow.Show();
        }
    }
}
