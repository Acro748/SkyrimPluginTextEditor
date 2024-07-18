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
            AssemblyResolve();
            Config.GetSingleton.ConfigRead();
            MainWindow newWindow = new MainWindow();
            newWindow.Title += " " + Version;
            newWindow.Show();
        }
        public void AssemblyResolve()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string dllName = new AssemblyName(args.Name).Name + ".dll";
                var assem = Assembly.GetExecutingAssembly();
                string resourceName = null;
                foreach (string str in assem.GetManifestResourceNames())
                {
                    if (str.IndexOf(dllName) != -1)
                    {
                        resourceName = str;
                        break;
                    }
                }
                if (resourceName == null) return null;
                using (var stream = assem.GetManifestResourceStream(resourceName))
                {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };
        }
    }
}
