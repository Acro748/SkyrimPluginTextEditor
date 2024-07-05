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
using System.Web.Security;
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

        public enum AddTextType : UInt16
        {
            None,
            AddPrefix,
            AddSuffix,
            End
        }
        public void Initial()
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

            LV_ConvertList_Active(false);
            LV_PluginList_Active(false);
            LV_FragmentList_Active(false);
            MI_Reset_Active(false);
            MI_Save_Active(false);
            MI_FileManager_Active(false);

            CB_MatchCase.IsChecked = Config.GetSingleton.GetSkyrimPluginEditor_MatchCase();
        }

        private List<string> selectedFolders = new List<string>();
        public List<string> GetSelectedFolders() { return selectedFolders; }
        private Dictionary<string, PluginManager> pluginDatas = new Dictionary<string, PluginManager>(); //fullpath, plugindata
        private List<PluginListData> pluginList = new List<PluginListData>();
        private List<FragmentTypeData> fragmentList = new List<FragmentTypeData>();
        private List<PluginToggleData> inactiveList = new List<PluginToggleData>(); //if there is a data then set invisible it
        private List<MasterPluginField> masterPluginList = new List<MasterPluginField>();
        private List<DataEditField> dataEditFields = new List<DataEditField>();
        private List<DataEditField> dataEditFieldsDisable = new List<DataEditField>();
        private ConcurrentDictionary<UInt64, DataEditField> dataEditFieldsEdited = new ConcurrentDictionary<UInt64, DataEditField>();
        private FileManager fm = null;


        private void MI_OpenFolder_Click(object sender, RoutedEventArgs e)
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

            foreach (string folder in selectedFolders)
            {
                Logger.Log.Info("Selected Folder : " + folder);
            }

            Task.Run(async () => SetPluginListView());
        }

        private PluginStreamBase._ErrorCode GetFile(string path, object tmpLock)
        {
            string fileName = System.IO.Path.GetFileName(path);
            if (Util.IsPluginFIle(fileName))
            {
                PluginManager pm = new PluginManager(path);
                PluginStreamBase._ErrorCode errorCode = pm.Read();
                lock (tmpLock)
                {
                    pluginDatas.Add(path, pm);
                }
                if (errorCode >= 0)
                    Logger.Log.Info(errorCode + " : " + path);
                else if ((Int16)errorCode <= -20)
                    Logger.Log.Warn(errorCode + " : " + path);
                else
                    Logger.Log.Error(errorCode + " : " + path);
                return errorCode;
            }
            return PluginStreamBase._ErrorCode.Readed;
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
            MI_Reset_Active(false);
            MI_Save_Active(false);
            MI_FileManager_Active(false);

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
                            PluginStreamBase._ErrorCode errorCode = GetFile(path, tmpLock);
                            if (errorCode < 0 && -20 < (Int16)errorCode)
                            {
                                string fileName = System.IO.Path.GetFileName(path);
                                lock (tmpLock)
                                {
                                    wrongPlugins.Add(fileName, errorCode);
                                }
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
                            PluginStreamBase._ErrorCode errorCode = GetFile(path, tmpLock);
                            if (errorCode < 0 && -20 < (Int16)errorCode)
                            {
                                string fileName = System.IO.Path.GetFileName(path);
                                lock (tmpLock)
                                {
                                    wrongPlugins.Add(fileName, errorCode);
                                }
                            }
                        }
                    }
                    ProgressBarStep(step);
                }
            }

            step = baseStep / (pluginDatas.Count == 0 ? 1 : Math.Sqrt(pluginDatas.Count));
            double maxCount = pluginDatas.Count / (pluginDatas.Count == 0 ? 1 : Math.Sqrt(pluginDatas.Count));

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

                pluginList.Sort((x, y) => { 
                    var result = x.PluginName.CompareTo(y.PluginName);
                    if (result == 0)
                    {
                        if (x.RecordType == "TES4")
                            return -1;
                        else if (y.RecordType == "TES4")
                            return 1;
                        return x.RecordType.CompareTo(y.RecordType);
                    }
                    return result;
                });
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

                fragmentList.Sort((x, y) => { return x.FragmentType.CompareTo(y.FragmentType); });
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
            MI_FileManager_Active();
        }

        double ProgressBarMax = 10000;
        double ProgressBarValue = 0;
        private void ProgressBarInitial(double Maximum = 10000)
        {
            ProgressBarMax = Maximum;
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                PB_Loading.Value = 0;
                PB_Loading.Minimum = 0;
                PB_Loading.Maximum = ProgressBarMax;
            }));
        }
        private void ProgressBarStep(double step = 1)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                ProgressBarValue += step;
                PB_Loading.Value = ProgressBarValue;
            }));
            Task.Delay(TimeSpan.FromTicks(1));
        }
        private double ProgressBarLeft()
        {
            return ProgressBarMax - ProgressBarValue;
        }
        private double ProgressBarMaximum()
        {
            return ProgressBarMax;
        }
        private void ProgressBarDone()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                ProgressBarValue = ProgressBarMax;
                PB_Loading.Value = ProgressBarValue;
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

        private void LV_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv == null)
                return;
            if (!e.WidthChanged)
                return;
            if (e.PreviousSize.Width <= 10 || e.NewSize.Width <= 10)
                return;
            double Ratio = e.NewSize.Width / e.PreviousSize.Width;
            if (lv == LV_ConvertListBefore)
            {
                GVC_ConvertListBefore.Width = GVC_ConvertListBefore.Width * Ratio;
            }
            else if (lv == LV_ConvertListAfter)
            {
                GVC_ConvertListAfter.Width = GVC_ConvertListAfter.Width * Ratio;
            }
            else if (lv == LV_PluginList)
            {
                GVC_Plugins.Width = GVC_Plugins.Width * Ratio;
            }
            else if (lv == LV_FragmentList)
            {
                GVC_Fragments.Width = GVC_Fragments.Width * Ratio;
            }
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
            if (pluginDatas.Count == 0)
            {
                BT_Apply_Update();
                return;
            }
            BT_Apply_Active(false);

            if (CB_MasterPluginBefore.SelectedIndex > 0 && TB_MasterPluginAfter.Text.Length > 0)
            {
                CB_MasterPluginBefore_Active(false);
                string MasterAfter = TB_MasterPluginAfter.Text;
                if (Util.IsPluginFIle(MasterAfter))
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
            MI_Reset_Active();
            MI_Save_Active();
        }

        private void LV_ConvertList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv == null)
                return;
            ListView lv1 = null;
            ListView lv2 = null;
            if (lv == LV_ConvertListBefore)
            {
                lv1 = LV_ConvertListBefore;
                lv2 = LV_ConvertListAfter;
            }
            else if (lv == LV_ConvertListAfter)
            {
                lv1 = LV_ConvertListAfter;
                lv2 = LV_ConvertListBefore;
            }
            Decorator border1 = VisualTreeHelper.GetChild(lv1, 0) as Decorator;
            Decorator border2 = VisualTreeHelper.GetChild(lv2, 0) as Decorator;
            ScrollViewer scrollViewer1 = border1.Child as ScrollViewer;
            ScrollViewer scrollViewer2 = border2.Child as ScrollViewer;
            scrollViewer2.ScrollToVerticalOffset(scrollViewer1.VerticalOffset);
            scrollViewer2.ScrollToHorizontalOffset(scrollViewer1.HorizontalOffset);
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
                var group = checkBox.DataContext as CollectionViewGroup;
                if (group == null)
                {
                    Logger.Log.Error("Couldn't get CollectionViewGroup from plugins header checkbox");
                    return;
                }

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
                var expander = Util.FindParent<Expander>(checkBox);
                if (expander == null)
                {
                    Logger.Log.Error("Couldn't get Expander form plugins checkbox");
                    return;
                }
                var headerCheckBox = expander.FindName("CB_PluginListHeader") as CheckBox;
                if (headerCheckBox == null)
                {
                    Logger.Log.Error("Couldn't get PluginsListHeader form plugins checkbox");
                    return;
                }
                var group = expander.DataContext as CollectionViewGroup;
                if (group == null)
                {
                    Logger.Log.Error("Couldn't get CollectionViewGroup form expender of plugins checkbox");
                    return;
                }
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

        private void CB_Fragments_CheckUncheck(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;
            if (checkBox == null)
                return;

            FragmentTypeData data = checkBox.DataContext as FragmentTypeData;
            if (data == null)
            {
                Logger.Log.Error("Couldn't get fragment checkbox data");
                return;
            }
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

        private void MI_Setting_Click(object sender, RoutedEventArgs e)
        {
            Setting settingWindow = new Setting();
            settingWindow.ShowDialog();
        }
        private void MI_Reset_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_Reset.IsEnabled = Active;
            }));
        }
        private void MI_Reset_Click(object sender, RoutedEventArgs e)
        {
            SetPluginListView();
        }
        private void MI_Save_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_Save.IsEnabled = Active;
            }));
        }
        private void MI_Save_Click(object sender, RoutedEventArgs e)
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

        private void MI_FileManager_Click(object sender, RoutedEventArgs e)
        {
            if (fm != null && fm.IsLoaded)
                return;
            fm = new FileManager(selectedFolders);
            fm.Show();
        }

        private void MI_FileManager_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_FileManager.IsEnabled = Active;
            }));
        }

        private void LV_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space)
                return;
            ListViewItem lvi = sender as ListViewItem;
            if (lvi == null)
                return;
            ListView lv = Util.FindParent<ListView>(lvi);
            if (lv == null) 
                return;
            if (lv == LV_ConvertListBefore || lv == LV_ConvertListAfter)
            {
                foreach (var data in dataEditFields)
                {
                    if (data.IsSelected)
                        data.IsChecked = !data.IsChecked;
                }
                LV_ConvertList_Update();
            }
            else if (lv == LV_PluginList)
            {
                foreach (var plugin in pluginList)
                {
                    if (plugin.IsSelected)
                        plugin.IsChecked = !plugin.IsChecked;
                }
                LV_PluginList_Update();
            }
            else if (lv == LV_FragmentList)
            {
                foreach (var fragment in fragmentList)
                {
                    if (fragment.IsSelected)
                        fragment.IsChecked = !fragment.IsChecked;
                }
                LV_FragmentList_Update();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (fm == null)
                return;
            if (!fm.IsLoaded)
                return;
            fm.Close();
        }

        private void LV_Check_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            ContextMenu cm = Util.FindParent<ContextMenu>(mi);
            if (cm == null) 
                return;
            if (cm == LV_ConvertListBefore.ContextMenu || cm == LV_ConvertListAfter.ContextMenu)
            {
                Parallel.ForEach(dataEditFields, data =>
                {
                    if (data.IsSelected)
                        data.IsChecked = true;
                });
                LV_ConvertList_Update();
            }
            else if (cm == LV_PluginList.ContextMenu)
            {
                foreach (var plugin in pluginList)
                {
                    if (plugin.IsSelected)
                        plugin.IsChecked = true;
                }
                LV_PluginList_Update();
            }
            else if (cm == LV_FragmentList.ContextMenu)
            {
                foreach (var fragment in fragmentList)
                {
                    if (fragment.IsSelected)
                        fragment.IsChecked = true;
                }
                LV_FragmentList_Update();
            }
        }
        private void LV_Uncheck_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            ContextMenu cm = Util.FindParent<ContextMenu>(mi);
            if (cm == null)
                return;
            if (cm == LV_ConvertListBefore.ContextMenu || cm == LV_ConvertListAfter.ContextMenu)
            {
                Parallel.ForEach(dataEditFields, data =>
                {
                    if (data.IsSelected)
                        data.IsChecked = false;
                });
                LV_ConvertList_Update();
            }
            else if (cm == LV_PluginList.ContextMenu)
            {
                foreach (var plugin in pluginList)
                {
                    if (plugin.IsSelected)
                        plugin.IsChecked = false;
                }
                LV_PluginList_Update();
            }
            else if (cm == LV_FragmentList.ContextMenu)
            {
                foreach (var fragment in fragmentList)
                {
                    if (fragment.IsSelected)
                        fragment.IsChecked = false;
                }
                LV_FragmentList_Update();
            }
        }
        private void LV_Invert_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            ContextMenu cm = Util.FindParent<ContextMenu>(mi);
            if (cm == null)
                return;
            if (cm == LV_ConvertListBefore.ContextMenu || cm == LV_ConvertListAfter.ContextMenu)
            {
                Parallel.ForEach(dataEditFields, data =>
                {
                    if (data.IsSelected)
                        data.IsChecked = !data.IsChecked;
                });
                LV_ConvertList_Update();
            }
            else if (cm == LV_PluginList.ContextMenu)
            {
                foreach (var plugin in pluginList)
                {
                    if (plugin.IsSelected)
                        plugin.IsChecked = !plugin.IsChecked;
                }
                LV_PluginList_Update();
            }
            else if (cm == LV_FragmentList.ContextMenu)
            {
                foreach (var fragment in fragmentList)
                {
                    if (fragment.IsSelected)
                        fragment.IsChecked = !fragment.IsChecked;
                }
                LV_FragmentList_Update();
            }
        }
        private void LV_CheckAll_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            ContextMenu cm = Util.FindParent<ContextMenu>(mi);
            if (cm == null)
                return;
            if (cm == LV_ConvertListBefore.ContextMenu || cm == LV_ConvertListAfter.ContextMenu)
            {
                Parallel.ForEach(dataEditFields, data =>
                {
                    data.IsChecked = true;
                });
                LV_ConvertList_Update();
            }
            else if (cm == LV_PluginList.ContextMenu)
            {
                foreach (var plugin in pluginList)
                {
                    plugin.IsChecked = true;
                }
                LV_PluginList_Update();
            }
            else if (cm == LV_FragmentList.ContextMenu)
            {
                foreach (var fragment in fragmentList)
                {
                    fragment.IsChecked = true;
                }
                LV_FragmentList_Update();
            }
        }
        private void LV_UncheckAll_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            ContextMenu cm = Util.FindParent<ContextMenu>(mi);
            if (cm == null)
                return;
            if (cm == LV_ConvertListBefore.ContextMenu || cm == LV_ConvertListAfter.ContextMenu)
            {
                Parallel.ForEach(dataEditFields, data =>
                {
                    data.IsChecked = false;
                });
                LV_ConvertList_Update();
            }
            else if (cm == LV_PluginList.ContextMenu)
            {
                foreach (var plugin in pluginList)
                {
                    plugin.IsChecked = false;
                }
                LV_PluginList_Update();
            }
            else if (cm == LV_FragmentList.ContextMenu)
            {
                foreach (var fragment in fragmentList)
                {
                    fragment.IsChecked = false;
                }
                LV_FragmentList_Update();
            }
        }
        private void LV_InvertAll_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            ContextMenu cm = Util.FindParent<ContextMenu>(mi);
            if (cm == null)
                return;
            if (cm == LV_ConvertListBefore.ContextMenu || cm == LV_ConvertListAfter.ContextMenu)
            {
                Parallel.ForEach(dataEditFields, data =>
                {
                    data.IsChecked = !data.IsChecked;
                });
                LV_ConvertList_Update();
            }
            else if (cm == LV_PluginList.ContextMenu)
            {
                foreach (var plugin in pluginList)
                {
                    plugin.IsChecked = !plugin.IsChecked;
                }
                LV_PluginList_Update();
            }
            else if (cm == LV_FragmentList.ContextMenu)
            {
                foreach (var fragment in fragmentList)
                {
                    fragment.IsChecked = !fragment.IsChecked;
                }
                LV_FragmentList_Update();
            }
        }

        private void MI_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SaveStateOnConfig(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (cb != null)
            {
                if (cb == CB_MatchCase)
                    Config.GetSingleton.SetSkyrimPluginEditor_MatchCase(CB_MatchCase.IsChecked ?? false);
            }
            Config.GetSingleton.ConfigWrite();
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
