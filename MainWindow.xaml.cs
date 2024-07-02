using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using log4net.Core;
using log4net.Plugin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using WK.Libraries.BetterFolderBrowserNS;
using static SkyrimPluginEditor.PluginData;
using static SkyrimPluginEditor.PluginStreamBase;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

#pragma warning disable CS4014 // 이 호출을 대기하지 않으므로 호출이 완료되기 전에 현재 메서드가 계속 실행됩니다.

namespace SkyrimPluginEditor
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>


    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Initial();
        }

        enum AddTextType : UInt16
        {
            None,
            AddPrefix,
            AddSuffix,
            End
        }
        public async void Initial()
        {
            for (AddTextType i = 0; i < AddTextType.End; i++)
            {
                CB_AddTextType.Items.Add(i);
            }
            CB_AddTextType.SelectedIndex = 0;
            LV_PluginList_Update(true);
            LV_FragmentList_Update(true);
            CB_MasterPluginBefore_Update();
            LV_ConvertList_Update(true);
        }

        private List<string> selectedFolders = new List<string>();
        private Dictionary<string, PluginManager> pluginDatas = new Dictionary<string, PluginManager>(); //fullpath, plugindata
        private List<PluginListData> pluginList = new List<PluginListData>();
        private List<FragmentTypeData> fragmentList = new List<FragmentTypeData>();
        private List<PluginToggleData> inactiveList = new List<PluginToggleData>(); //if there is a data then set invisible it
        private List<MasterPluginField> masterPluginList = new List<MasterPluginField>();
        private List<DataEditField> dataEditFields = new List<DataEditField>();
        private List<DataEditField> dataEditFieldsDisable = new List<DataEditField>();
        private ConcurrentDictionary<UInt64, DataEditField> dataEditFieldsEdited = new ConcurrentDictionary<UInt64, DataEditField>();

        private void TB_PluginFolder_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            selectedFolders.Clear();
            string path = TB_PluginFolder.Text;
            if (Directory.Exists(path))
            {
                selectedFolders.Add(path);
            }
            else
            {
                string[] paths = path.Split(',');
                foreach (string folder in paths)
                {
                    folder.TrimStart();
                    selectedFolders.Add(folder);
                }
            }

            SetPluginListView();
        }
        private void BT_Search_Click(object sender, RoutedEventArgs e)
        {
            var betterFolderBrowser = new BetterFolderBrowser();

            //browser initial
            betterFolderBrowser.Title = "Select plugin folders...";
            betterFolderBrowser.RootFolder = Config.GetSingleton.GetDefaultPath();
            betterFolderBrowser.Multiselect = true;

            if (betterFolderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                selectedFolders.Clear();
                selectedFolders = betterFolderBrowser.SelectedFolders.ToList();
            }
            else
            {
                return;
            }

            string fullFolderTextBox = "";
            bool firstFolder = true;
            foreach (string folder in selectedFolders)
            {
                if (!firstFolder)
                {
                    fullFolderTextBox += ", ";
                }
                fullFolderTextBox += folder;
                firstFolder = false;
            }
            TB_PluginFolder.Text = fullFolderTextBox;
            Logger.Log.Info("Selected Folder(s) : " + fullFolderTextBox);

            Task.Run(async () => SetPluginListView());

            if (selectedFolders.Count() > 0 && !Config.GetSingleton.GetIsFixedDefaultPath())
            {
                if (selectedFolders.Count() == 1)
                    Config.GetSingleton.SetDefaultPath(selectedFolders.First());
                else
                {
                    var folder = Directory.GetParent(selectedFolders.First());
                    if (folder != null)
                        Config.GetSingleton.SetDefaultPath(folder.FullName);
                    else
                        Config.GetSingleton.SetDefaultPath(selectedFolders.First());
                }
            }
        }
        private async void SetPluginListView()
        {
            ProgressBarInitial();

            double baseStep = ProgressBarMaximum() / 5;
            double step = baseStep / selectedFolders.Count;
            object tmpLock = new object();

            masterPluginList.Clear();
            pluginList.Clear();
            fragmentList.Clear();
            dataEditFields.Clear();
            dataEditFieldsDisable.Clear();
            inactiveList.Clear();
            LV_PluginList_Update();
            LV_FragmentList_Update();
            CB_MasterPluginBefore_Update();
            LV_ConvertList_Update();
            LV_PluginList_Active(false);
            LV_FragmentList_Active(false);
            LV_ConvertList_Active(false);
            CB_MasterPluginBefore_Active(false);

            pluginDatas.Clear();
            Dictionary<string, PluginStreamBase._ErrorCode> wrongPlugins = new Dictionary<string, PluginStreamBase._ErrorCode>();
            if (Config.GetSingleton.GetParallelFolderRead())
            {
                Parallel.ForEach(selectedFolders, async folder =>
                {
                    if (Directory.Exists(folder))
                    {
                        Parallel.ForEach(Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly), path =>
                        {
                            string fileName = System.IO.Path.GetFileName(path);
                            if (IsPluginFIle(fileName))
                            {
                                PluginManager pm = new PluginManager(path);
                                PluginStreamBase._ErrorCode errorCode = pm.Read();
                                if (errorCode < 0 && -20 < (Int16)errorCode)
                                {
                                    lock (tmpLock)
                                    {
                                        wrongPlugins.Add(fileName, errorCode);
                                    }
                                }
                                else
                                {
                                    lock (tmpLock)
                                    {
                                        pluginDatas.Add(path, pm);
                                    }
                                }
                                if (errorCode >= 0)
                                    Logger.Log.Info(errorCode + " : " + path);
                                else if ((Int16)errorCode <= -20)
                                    Logger.Log.Warn(errorCode + " : " + path);
                                else
                                    Logger.Log.Error(errorCode + " : " + path);
                            }
                        });
                    }
                    ProgressBarStep(step);
                });
            }
            else
            {
                foreach (var folder in selectedFolders)
                {
                    if (Directory.Exists(folder))
                    {
                        foreach (var path in Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            string fileName = System.IO.Path.GetFileName(path);
                            if (IsPluginFIle(fileName))
                            {
                                PluginManager pm = new PluginManager(path);
                                PluginStreamBase._ErrorCode errorCode;
                                errorCode = pm.Read();
                                if (errorCode < 0 && -20 < (Int16)errorCode)
                                {
                                    lock (tmpLock)
                                    {
                                        wrongPlugins.Add(fileName, errorCode);
                                    }
                                }
                                else
                                {
                                    lock (tmpLock)
                                    {
                                        pluginDatas.Add(path, pm);
                                    }
                                }
                            }
                        }
                    }
                    ProgressBarStep(step);
                }
            }

            step = baseStep / Math.Sqrt(pluginDatas.Count);
            double maxCount = pluginDatas.Count / Math.Sqrt(pluginDatas.Count);

            Task.Run(async () =>
            {
                double stepCount = 0;
                double dataCount = 0;
                foreach (var plugin in pluginDatas)
                {
                    var list = plugin.Value.GetEditableListOfRecord();
                    foreach (var item in list)
                    {
                        if (pluginList.FindIndex(x => x.PluginName == plugin.Value.GetFileName() && x.RecordType == item.RecordType) != -1)
                            continue;
                        pluginList.Add(new PluginListData()
                        {
                            PluginName = plugin.Value.GetFileName(),
                            IsChecked = true,
                            RecordType = item.RecordType,
                            IsSelected = false,
                            PluginPath = plugin.Value.GetFilePath()
                        });
                    }
                    dataCount++;
                    if (dataCount >= maxCount)
                    {
                        LV_PluginList_Update();
                        ProgressBarStep(step);
                        stepCount += step;
                        dataCount = 0;
                    }
                }
                if (stepCount < baseStep)
                    ProgressBarStep(step);

                LV_PluginList_Update();
                LV_PluginList_Active();
            });
            
            Task.Run(async () =>
            {
                double stepCount = 0;
                double dataCount = 0;
                foreach (var plugin in pluginDatas)
                {
                    var list = plugin.Value.GetEditableListOfRecord();
                    foreach (var item in list)
                    {
                        if (fragmentList.FindIndex(x => x.FragmentType == item.FragmentType) != -1)
                            continue;
                        fragmentList.Add(new FragmentTypeData()
                        {
                            IsChecked = true,
                            FragmentType = item.FragmentType,
                            IsSelected = false
                        });
                    }
                    dataCount++;
                    if (dataCount >= maxCount)
                    {
                        LV_FragmentList_Update();
                        ProgressBarStep(step);
                        stepCount += step;
                        dataCount = 0;
                    }
                }
                if (stepCount < baseStep)
                    ProgressBarStep(step);

                LV_FragmentList_Update();
                LV_FragmentList_Active();
            });

            Task.Run(async () =>
            {
                masterPluginList.Add(new MasterPluginField() { MasterPluginName = "None", MasterPluginNameOrig = "None", FromPlugins = "None" });
                double stepCount = 0;
                double dataCount = 0;
                foreach (var data in pluginDatas)
                {
                    var list = data.Value.GetEditableListOfMAST();
                    foreach (var m in list)
                    {
                        var index = masterPluginList.FindIndex(x => x.MasterPluginNameOrig == m.Text);
                        if (index != -1)
                        {
                            masterPluginList[index].FromPlugins += "\n" + data.Value.GetFilePath();
                            continue;
                        }
                        masterPluginList.Add(new MasterPluginField()
                        {
                            MasterPluginName = m.Text,
                            MasterPluginNameOrig = m.Text,
                            FromPlugins = data.Value.GetFilePath()
                        });
                    }
                    dataCount++;
                    if (dataCount >= maxCount)
                    {
                        CB_MasterPluginBefore_Update();
                        ProgressBarStep(step);
                        stepCount += step;
                        dataCount = 0;
                    }
                }
                if (stepCount < baseStep)
                    ProgressBarStep(step);

                CB_MasterPluginBefore_Update();
                CB_MasterPluginBefore_Active();
            });

            Task.Run(async () =>
            {
                UInt64 count = 0;
                double stepCount = 0;
                double dataCount = 0;
                foreach (var data in pluginDatas)
                {
                    var list = data.Value.GetEditableListOfRecord();
                    foreach (var item in list)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
                        {
                            dataEditFields.Add(new DataEditField()
                            {
                                PluginName = data.Key,
                                RecordType = item.RecordType,
                                FragmentType = item.FragmentType,
                                IsChecked = true,
                                IsSelected = false,
                                TextBefore = item.Text,
                                TextAfter = item.Text,
                                TextBeforeAlt = MakeAltDataEditField(item.RecordType, item.FragmentType, item.Text),
                                TextAfterAlt = MakeAltDataEditField(item.RecordType, item.FragmentType, item.Text),
                                Index = count,
                                PluginPath = data.Value.GetFilePath(),
                                EditableIndex = item.EditableIndex
                            });
                            count++;
                        }));
                    }
                    ++dataCount;
                    if (dataCount >= maxCount)
                    {
                        LV_ConvertList_Update();
                        ProgressBarStep(step);
                        stepCount += step;
                        dataCount = 0;
                    }
                }
                if (stepCount < baseStep)
                    ProgressBarStep(step);

                LV_ConvertList_Update();
                LV_ConvertList_Active();
            });

            if (wrongPlugins.Count > 0)
            {
                string wronglist = "";
                bool firstLine = true;
                foreach (var plugin in wrongPlugins)
                {
                    if (!firstLine)
                        wronglist += Environment.NewLine;
                    wronglist += plugin.Key + " -> " + plugin.Value.ToString();
                    firstLine = false;
                }
                System.Windows.MessageBox.Show(wronglist, "Wrong Plugin!");
            }

            if (pluginDatas.Count == 0)
                ProgressBarDone();
            BT_Apply_Update();
            BT_Reset_Update();
            BT_Save_Update();
        }

        private void ProgressBarInitial(double Maximum = 10000)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                PB_Loading.Value = 0;
                PB_Loading.Minimum = 0;
                PB_Loading.Maximum = Maximum;
            }));
        }
        private void ProgressBarStep(double step = 1)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                PB_Loading.Value += step;
            }));
            Task.Delay(TimeSpan.FromTicks(1));
        }
        private double ProgressBarLeft()
        {
            double result = 0;
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                result = PB_Loading.Maximum - PB_Loading.Value;
            }));
            return result;
        }
        private double ProgressBarMaximum()
        {
            double result = 0;
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                result = PB_Loading.Maximum;
            }));
            return result;
        }
        private void ProgressBarDone()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                PB_Loading.Value = PB_Loading.Maximum;
            }));
        }

        private string MakeAltDataEditField(DataEditField field, bool isAfter = true)
        {
            return MakeAltDataEditField(field.RecordType, field.FragmentType, isAfter ? field.TextAfter : field.TextBefore);
        }
        private string MakeAltDataEditField(string RecordType, string FragmentType, string Text)
        {
            return "[" + RecordType + "|" + FragmentType + "] " + Text;
        }

        private void Checkbox_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var checkbox = sender as CheckBox;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (checkbox != null)
                {
                    checkbox.IsChecked = !(checkbox.IsChecked ?? true);
                }
            }
        }
        private void UIElement_OnGotMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var checkbox = sender as CheckBox;
                if (checkbox != null)
                {
                    checkbox.IsChecked = !(checkbox.IsChecked ?? true);
                    checkbox.ReleaseMouseCapture();
                }
            }
        }

        private void LV_ConvertList_SizeChanged_Before(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
                return;
            if (e.PreviousSize.Width <= 10 || e.NewSize.Width <= 10)
                return;
            double Ratio = e.NewSize.Width / e.PreviousSize.Width;
            GVC_ConvertListBefore.Width = GVC_ConvertListBefore.Width * Ratio;
        }
        private void LV_ConvertList_SizeChanged_After(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
                return;
            if (e.PreviousSize.Width <= 10 || e.NewSize.Width <= 10)
                return;
            double Ratio = e.NewSize.Width / e.PreviousSize.Width;
            GVC_ConvertListAfter.Width = GVC_ConvertListAfter.Width * Ratio;
        }

        private void LV_PluginList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
                return;
            if (e.PreviousSize.Width <= 10 || e.NewSize.Width <= 10)
                return;
            double Ratio = e.NewSize.Width / e.PreviousSize.Width;
            GVC_Plugins.Width = GVC_Plugins.Width * Ratio;
        }
        private void LV_FragmentList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
                return;
            if (e.PreviousSize.Width <= 10 || e.NewSize.Width <= 10)
                return;
            double Ratio = e.NewSize.Width / e.PreviousSize.Width;
            GVC_Fragments.Width = GVC_Fragments.Width * Ratio;
        }

    private bool IsPluginFIle(string file)
        {
            string lowFileName = file.ToLower();
            return lowFileName.EndsWith(".esp") || lowFileName.EndsWith(".esm") || lowFileName.EndsWith(".esl");
        }
        private void BT_Apply_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                BT_Apply.IsEnabled = Active;
            }));
        }
        private void BT_Apply_Update()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (selectedFolders.Count == 0)
                {
                    BT_Apply.IsEnabled = false;
                    return;
                }

                if (CB_MasterPluginBefore.SelectedIndex > 0 && TB_MasterPluginAfter.Text.Length > 0)
                {
                    BT_Apply.IsEnabled = true;
                    return;
                }
                else if (CB_AddTextType.SelectedIndex > 0 && TB_AddText.Text.Length > 0)
                {
                    BT_Apply.IsEnabled = true;
                    return;
                }
                else if (TB_ReplaceSearch.Text.Length > 0 || TB_ReplaceResult.Text.Length > 0)
                {
                    BT_Apply.IsEnabled = true;
                    return;
                }

                BT_Apply.IsEnabled = false;
            }));
        }
        private async void BT_Apply_Click(object sender, RoutedEventArgs e)
        {
            if (TB_PluginFolder.Text.Length == 0)
            {
                BT_Apply_Update();
                return;
            }
            BT_Apply_Active(false);

            if (CB_MasterPluginBefore.SelectedIndex > 0 && TB_MasterPluginAfter.Text.Length > 0)
            {
                CB_MasterPluginBefore_Active(false);
                string MasterAfter = TB_MasterPluginAfter.Text;
                if (IsPluginFIle(MasterAfter))
                {
                    masterPluginList[CB_MasterPluginBefore.SelectedIndex].MasterPluginName = MasterAfter;
                    CB_MasterPluginBefore_Update();
                }
                CB_MasterPluginBefore_Active();
            }

            if (CB_AddTextType.SelectedIndex > 0 && TB_AddText.Text.Length > 0)
            {
                LV_ConvertList_Active(false);
                AddTextType adType = (AddTextType)CB_AddTextType.SelectedIndex;
                string AddText = TB_AddText.Text;
                switch (adType)
                {
                    case AddTextType.AddPrefix:
                        {
                            Parallel.ForEach(dataEditFields, data =>
                            {
                                if (data.IsChecked)
                                {
                                    data.TextAfter = AddText + data.TextAfter;
                                    data.TextAfterAlt = MakeAltDataEditField(data);
                                    dataEditFieldsEdited.AddOrUpdate(data.Index, data, (key, oldvalue) => data);
                                }
                            });
                            break;
                        }
                    case AddTextType.AddSuffix:
                        {
                            Parallel.ForEach(dataEditFields, data =>
                            {
                                if (data.IsChecked)
                                {
                                    data.TextAfter = data.TextAfter + AddText;
                                    data.TextAfterAlt = MakeAltDataEditField(data);
                                    dataEditFieldsEdited.AddOrUpdate(data.Index, data, (key, oldvalue) => data);
                                }
                            });
                            break;
                        }
                }
                LV_ConvertList_Update();
                LV_ConvertList_Active();
            }

            if (TB_ReplaceSearch.Text.Length > 0 || TB_ReplaceResult.Text.Length > 0)
            {
                LV_ConvertList_Active(false);
                string ReplaceSearch = TB_ReplaceSearch.Text;
                string ReplaceResult = TB_ReplaceResult.Text;
                bool MatchCase = false;
                if ((bool)CB_MatchCase.IsChecked)
                    MatchCase = true;
                Parallel.ForEach(dataEditFields, data =>
                {
                    if (data.IsChecked && data.TextAfter != null && data.TextAfter.Length > 0)
                    {
                        data.TextAfter = Regex.Replace(data.TextAfter, ReplaceSearch, ReplaceResult, MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                        data.TextAfterAlt = MakeAltDataEditField(data);
                        dataEditFieldsEdited[data.Index] = data;
                    }
                });
                LV_ConvertList_Update();
                LV_ConvertList_Active();
            }

            BT_Apply_Update();
        }

        private void LV_ConvertListBefore_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            Decorator borderBefore = VisualTreeHelper.GetChild(LV_ConvertListBefore, 0) as Decorator;
            Decorator borderAfter = VisualTreeHelper.GetChild(LV_ConvertListAfter, 0) as Decorator;
            ScrollViewer scrollViewerBefore = borderBefore.Child as ScrollViewer;
            ScrollViewer scrollViewerAfter = borderAfter.Child as ScrollViewer;
            scrollViewerAfter.ScrollToVerticalOffset(scrollViewerBefore.VerticalOffset);
            scrollViewerAfter.ScrollToHorizontalOffset(scrollViewerBefore.HorizontalOffset);
        }
        private void LV_ConvertListAfter_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            Decorator borderBefore = VisualTreeHelper.GetChild(LV_ConvertListBefore, 0) as Decorator;
            Decorator borderAfter = VisualTreeHelper.GetChild(LV_ConvertListAfter, 0) as Decorator;
            ScrollViewer scrollViewerBefore = borderBefore.Child as ScrollViewer;
            ScrollViewer scrollViewerAfter = borderAfter.Child as ScrollViewer;
            scrollViewerBefore.ScrollToVerticalOffset(scrollViewerAfter.VerticalOffset);
            scrollViewerBefore.ScrollToHorizontalOffset(scrollViewerAfter.HorizontalOffset);
        }

        int CheckUncheckIgnoreCount = 0;
        private void CB_Plugins_Header_CheckUncheck(object sender, RoutedEventArgs e)
        {
            if (CheckUncheckIgnoreCount > 0)
            {
                CheckUncheckIgnoreCount--;
                return;
            }
            var checkBox = e.OriginalSource as CheckBox;
            if (checkBox == null)
                return;

            if (checkBox.Name == "CB_PluginListHeader")
            {
                var group = (CollectionViewGroup)checkBox.DataContext;
                if (group.Items.Count == 0)
                    return;
                bool isChecked = checkBox.IsChecked ?? false;
                List<PluginToggleData> toggleDatas = new List<PluginToggleData>();
                foreach (PluginListData item in group.Items)
                {
                    CheckUncheckIgnoreCount += (item.IsChecked != isChecked ? 1 : 0);
                    item.IsChecked = isChecked;
                    PluginToggleData toggleData = new PluginToggleData()
                    {
                        PluginPath = item.PluginPath,
                        RecordType = item.RecordType,
                        FragmentType = "0000"
                    };
                    toggleDatas.Add(toggleData);
                }
                InactiveListUpdate(toggleDatas, isChecked);
            }
        }
        private void CB_Plugins_CheckUncheck(object sender, RoutedEventArgs e)
        {
            if (CheckUncheckIgnoreCount > 0)
            {
                CheckUncheckIgnoreCount--;
                return;
            }
            var checkBox = e.OriginalSource as CheckBox;
            if (checkBox == null)
                return;

            if (checkBox.Tag.ToString() == "")
            {
                var expander = FindParent<Expander>(checkBox);
                var headerCheckBox = expander.FindName("CB_PluginListHeader") as CheckBox;
                var group = (CollectionViewGroup)expander.DataContext;
                if (group.Items.OfType<PluginListData>().All(x => x.IsChecked))
                {
                    CheckUncheckIgnoreCount = 1;
                    headerCheckBox.IsChecked = true;
                }
                else if (group.Items.OfType<PluginListData>().Any(x => x.IsChecked))
                {
                    headerCheckBox.IsChecked = null;
                }
                else
                {
                    CheckUncheckIgnoreCount = 1;
                    headerCheckBox.IsChecked = false;
                }

                PluginListData data = (PluginListData)checkBox.DataContext;
                PluginToggleData toggleData = new PluginToggleData()
                {
                    PluginPath = data.PluginPath,
                    RecordType = data.RecordType,
                    FragmentType = "0000"
                };
                InactiveListUpdate(toggleData, data.IsChecked);
            }
        }
        private T FindParent<T>(DependencyObject dependencyObject) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(dependencyObject);
            if (parent == null)
                return null;

            var parentT = parent as T;
            return parentT ?? FindParent<T>(parent);
        }
        private void CB_Fragments_CheckedUnchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;
            if (checkBox == null)
                return;

            FragmentTypeData data = (FragmentTypeData)checkBox.DataContext;
            PluginToggleData toggleData = new PluginToggleData()
            {
                PluginPath = "0000",
                RecordType = "0000",
                FragmentType = data.FragmentType
            };
            InactiveListUpdate(toggleData, data.IsChecked);
        }
        private async void InactiveListUpdate(List<PluginToggleData> datas, bool IsChecked)
        {
            if (IsChecked)
            {
                foreach (var data in datas)
                {
                    int index = inactiveList.FindIndex(x => x.PluginPath == data.PluginPath && x.RecordType == data.RecordType && x.FragmentType == data.FragmentType);
                    if (index != -1)
                    {
                        inactiveList.RemoveAt(index);
                    }
                }
            }
            else
            {
                foreach (var data in datas)
                {
                    if (inactiveList.FindIndex(x => x.PluginPath == data.PluginPath && x.RecordType == data.RecordType && x.FragmentType == data.FragmentType) == -1)
                    {

                        inactiveList.Add(data);

                    }
                }
            }
            await DataEditFieldUpdate(IsChecked);
        }
        private async void InactiveListUpdate(PluginToggleData data, bool IsChecked)
        {
            if (IsChecked)
            {
                int index = inactiveList.FindIndex(x => x.PluginPath == data.PluginPath && x.RecordType == data.RecordType && x.FragmentType == data.FragmentType);
                if (index != -1)
                {
                    inactiveList.RemoveAt(index);
                }
            }
            else if (inactiveList.FindIndex(x => x.PluginPath == data.PluginPath && x.RecordType == data.RecordType && x.FragmentType == data.FragmentType) == -1)
            {
                inactiveList.Add(data);
            }
            await DataEditFieldUpdate(IsChecked);
        }
        private async Task DataEditFieldUpdate(bool IsChecked)
        {
            LV_ConvertList_Active(false);
            List<DataEditField> found = new List<DataEditField>();
            if (IsChecked)
            {
                found.AddRange(dataEditFieldsDisable.FindAll(x =>
                    inactiveList.FindIndex(y =>
                        (y.PluginPath != "0000" ? x.PluginPath == y.PluginPath : true)
                        && (y.RecordType != "0000" ? x.RecordType == y.RecordType : true)
                        && (y.FragmentType != "0000" ? x.FragmentType == y.FragmentType : true)
                        ) == -1
                    ));
                dataEditFields.AddRange(found);
                found.ForEach(x => dataEditFieldsDisable.Remove(x));
            }
            else
            {
                found.AddRange(dataEditFields.FindAll(x =>
                    inactiveList.FindIndex(y =>
                        (y.PluginPath != "0000" ? x.PluginPath == y.PluginPath : true)
                        && (y.RecordType != "0000" ? x.RecordType == y.RecordType : true)
                        && (y.FragmentType != "0000" ? x.FragmentType == y.FragmentType : true)
                    ) != -1
                ));
                dataEditFieldsDisable.AddRange(found);
                found.ForEach(x => dataEditFields.Remove(x));
            }
            dataEditFields.Sort((x, y) => { return x.Index.CompareTo(y.Index); });
            LV_ConvertList_Update();
            LV_ConvertList_Active(true);
        }

        private void CB_MasterPluginBefore_Update()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                CB_MasterPluginBefore.ItemsSource = null;
                CB_MasterPluginBefore.ItemsSource = masterPluginList;
                CB_MasterPluginBefore.SelectedIndex = 0;
            }));
        }
        private void CB_MasterPluginBefore_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                CB_MasterPluginBefore.IsEnabled = Active;
            }));
        }

        private void LV_PluginList_Update(bool binding = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(pluginList);
                    PropertyGroupDescription groupDescription = new PropertyGroupDescription("PluginName");
                    view.GroupDescriptions.Clear();
                    view.GroupDescriptions.Add(groupDescription);
                    LV_PluginList.ItemsSource = pluginList;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(pluginList);
                    view.Refresh();
                    Task.Delay(TimeSpan.FromTicks(1));
                }
            }));
        }
        private void LV_PluginList_Active(bool Acvite = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_PluginList.IsEnabled = Acvite;
            }));
        }
        private void LV_FragmentList_Update(bool binding = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    LV_FragmentList.ItemsSource = fragmentList;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(fragmentList);
                    view.Refresh();
                    Task.Delay(TimeSpan.FromTicks(1));
                }
            }));
        }
        private void LV_FragmentList_Active(bool Acvite = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_FragmentList.IsEnabled = Acvite;
            }));
        }

        private void LV_ConvertList_Update(bool binding = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    LV_ConvertListBefore.ItemsSource = null;
                    LV_ConvertListAfter.ItemsSource = null;
                    LV_ConvertListBefore.ItemsSource = dataEditFields;
                    LV_ConvertListAfter.ItemsSource = dataEditFields;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(dataEditFields);
                    view.Refresh();
                    Task.Delay(TimeSpan.FromTicks(1));
                }
            }));
        }
        private void LV_ConvertList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_ConvertListBefore.IsEnabled = Active;
                LV_ConvertListAfter.IsEnabled = Active;
            }));
        }

        private void CB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BT_Apply_Update();
        }

        private void TB_TextChanged(object sender, TextChangedEventArgs e)
        {
            BT_Apply_Update();
        }

        private void BT_Setting_Click(object sender, RoutedEventArgs e)
        {
            Setting settingWindow = new Setting();
            settingWindow.ShowDialog();
        }
        private void BT_Reset_Update()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (pluginDatas.Count > 0)
                {
                    BT_Reset.IsEnabled = true;
                }
                else
                {
                    BT_Reset.IsEnabled = false;
                }
            }));
        }
        private void BT_Reset_Click(object sender, RoutedEventArgs e)
        {
            SetPluginListView();
        }
        private void BT_Save_Update()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (pluginDatas.Count > 0)
                {
                    BT_Save.IsEnabled = true;
                }
                else
                {
                    BT_Save.IsEnabled = false;
                }
            }));
        }
        private void BT_Save_Click(object sender, RoutedEventArgs e)
        {
            Parallel.ForEach(pluginDatas, plugin =>
            {
                foreach (var item in masterPluginList)
                {
                    plugin.Value.EditEditableList("TES4", "MAST", item.MasterPluginNameOrig, item.MasterPluginName);
                }
                var datafound = dataEditFieldsEdited.Where(x => x.Value.PluginPath == plugin.Key);
                foreach (var item in datafound)
                {
                    plugin.Value.EditEditableList(item.Value.EditableIndex, item.Value.TextAfter);
                }
                plugin.Value.ApplyEditableDatas();
                plugin.Value.Write();
            });
            System.Windows.MessageBox.Show("Save done!");
            SetPluginListView();
        }
    }

    public class PluginListData : INotifyPropertyChanged
    {
        private bool _IsChecked;
        private bool _IsSelected;

        public string PluginName { get; set; }
        public string RecordType { get; set; }

        public string PluginPath { get; set; }

        public bool IsChecked
        {
            get { return _IsChecked; }
            set
            {
                _IsChecked = value;
                OnPropertyChanged("IsChecked");
            }
        }

        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                _IsSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyname)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
            }
        }
    }
    public class FragmentTypeData : INotifyPropertyChanged
    {
        private bool _IsChecked;
        private bool _IsSelected;

        public string FragmentType { get; set; }

        public bool IsChecked
        {
            get { return _IsChecked; }
            set
            {
                _IsChecked = value;
                OnPropertyChanged("IsChecked");
            }
        }

        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                _IsSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyname)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
            }
        }
    }
    public class PluginToggleData
    {
        public string PluginPath {  get; set; }
        public string RecordType { get; set; }
        public string FragmentType { get; set; }
    }
    public class MasterPluginField
    {
        public string MasterPluginName { get; set; }
        public string MasterPluginNameOrig { get; set; }
        public string FromPlugins { get; set; }
    }
    public class DataEditField : INotifyPropertyChanged
    {
        private bool _IsChecked;
        private bool _IsSelected;

        public string PluginName { get; set; }
        public string RecordType { get; set; }
        public string FragmentType { get; set; }
        public string TextBefore { get; set; }
        public string TextBeforeAlt { get; set; }
        public string TextAfter { get; set; }
        public string TextAfterAlt { get; set; }

        public UInt64 Index { get; set; }
        public string PluginPath { get; set; }
        public int EditableIndex { get; set; }

        public bool IsChecked
        {
            get { return _IsChecked; }
            set
            {
                _IsChecked = value;
                OnPropertyChanged("IsChecked");
            }
        }

        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                _IsSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyname)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
            }
        }
    }
}
