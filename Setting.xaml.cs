using System;
using System.Collections.Generic;
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

namespace SkyrimPluginEditor
{
    /// <summary>
    /// Setting.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Setting : Window
    {
        const string Version = "v1.1.1";
        List<CBLogLevel> LogLevel = new List<CBLogLevel>();
        List<CBEncoding> Encoding = new List<CBEncoding>();
        List<CBStringLanguage> StringLanguage = new List<CBStringLanguage>();

        public Setting()
        {
            InitializeComponent();
            TB_Version.Text = Version;
            InitialLogLevel();
            InitialEncoding();
            InitialStringLanguage();

            GetConfig();
        }

        private void GetConfig()
        {
            TB_DefaultPath.Text = Config.GetSingleton.GetDefaultPath();
            CB_FixedDefaultPath.IsChecked = Config.GetSingleton.GetIsFixedDefaultPath();
            CB_ParallelFolderRead.IsChecked = Config.GetSingleton.GetParallelFolderRead();
            CB_LogLevel.SelectedIndex = (int)Config.GetSingleton.GetLogLevel();
            CB_Encoding.SelectedIndex = (int)Config.GetSingleton.GetEncoding();
            TB_StringLanguage.Text = Config.GetSingleton.GetStringLanguage();
        }

        private void InitialLogLevel()
        {
            for (Logger.LogLevel i = Logger.LogLevel.None; i < Logger.LogLevel.Max; i++)
            {
                LogLevel.Add(new CBLogLevel() { LogLevel_ = i } );
            }
            CB_LogLevel.ItemsSource = LogLevel;
        }
        private void InitialEncoding()
        {
            for (PluginStreamBase._Encoding i = PluginStreamBase._Encoding.Default; i < PluginStreamBase._Encoding.Max; i++)
            {
                Encoding.Add(new CBEncoding() { Encoding_ = i } );
            }
            CB_Encoding.ItemsSource = Encoding;
        }
        private void InitialStringLanguage()
        {
            for (CBStringLanguage.Language i = CBStringLanguage.Language.ManualEdit; i < CBStringLanguage.Language.Max; i++)
            {
                StringLanguage.Add(new CBStringLanguage() { Language_ = i } );
            }
            CB_StringLanguage.ItemsSource = StringLanguage;
            CB_StringLanguage.SelectedIndex = 0;
        }

        private void CB_StringLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CB_StringLanguage.SelectedIndex == 0)
                return;
            TB_StringLanguage.Text = (CB_StringLanguage.SelectedItem as CBStringLanguage).Language_.ToString();
        }

        private void BT_SetAsDefault_Click(object sender, RoutedEventArgs e)
        {
            Config.ConfigContainer configContainer = new Config.ConfigContainer();
            TB_DefaultPath.Text = configContainer.DefaultPath;
            CB_FixedDefaultPath.IsChecked = configContainer.IsFixedDefaultPath;
            CB_ParallelFolderRead.IsChecked = configContainer.ParallelFolderRead;
            CB_LogLevel.SelectedIndex = (int)configContainer.Logging;
            CB_Encoding.SelectedIndex = (int)configContainer.Encoding;
            CB_StringLanguage.SelectedIndex = 0;
            TB_StringLanguage.Text = configContainer.StringLanguage;
        }
        private void BT_Save_Click(object sender, RoutedEventArgs e)
        {
            Config.GetSingleton.SetDefaultPath(TB_DefaultPath.Text);
            Config.GetSingleton.SetIsFixedDefaultPath(CB_FixedDefaultPath.IsChecked ?? false);
            Config.GetSingleton.SetParallelFolderRead(CB_ParallelFolderRead.IsChecked ?? false);
            Config.GetSingleton.SetLogLevel((CB_LogLevel.SelectedItem as CBLogLevel).LogLevel_);
            Config.GetSingleton.SetEncoding((CB_Encoding.SelectedItem as CBEncoding).Encoding_);
            Config.GetSingleton.SetStringLanguage(TB_StringLanguage.Text);
            Config.GetSingleton.ConfigWrite();
            GetConfig();
            this.Close();
        }
    }

    public class CBLogLevel
    {
        public Logger.LogLevel LogLevel_ { get; set; }
        public string Tooltip { get; set; }
    }
    public class CBEncoding
    {
        public PluginStreamBase._Encoding Encoding_ { get; set; }
        public string Tooltip { get; set; }
    }
    public class CBStringLanguage
    {
        public enum Language
        {
            ManualEdit,
            Chinese,
            Czech,
            English,
            French,
            German,
            Italian,
            Japanese,
            Polish,
            Russian,
            Spanish,
            Max
        }
        public Language Language_ { get; set; }
        public string Tooltip { get; set; }
    }
}
