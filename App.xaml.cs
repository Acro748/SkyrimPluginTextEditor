using System.Windows;

namespace SkyrimPluginTextEditor
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public static readonly string Version = "v1.3.2";
        public static MainWindow mainWindow = new MainWindow();
        public static FileManager fileManager = null;
        public static NifManager nifManager = null;
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Config.GetSingleton.ConfigRead();
            mainWindow.Title += " " + Version;
            mainWindow.Show();
        }
    }
}
