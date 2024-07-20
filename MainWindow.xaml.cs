using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

#pragma warning disable CS4014 // 이 호출을 대기하지 않으므로 호출이 완료되기 전에 현재 메서드가 계속 실행됩니다.

namespace SkyrimPluginTextEditor
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>


    public partial class MainWindow : Window
    {
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
        private Setting setting = new Setting();
        private OpenFolderDialog folderBrowser = new OpenFolderDialog() { Title = "Select plugin folders...", Multiselect = true };
        private bool SafetyMode = false;
        private bool FileBackup = true;
        private CheckBoxBinder matchCase = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetSkyrimPluginEditor_MatchCase() };
        private bool MacroMode = false;

        public MainWindow()
        {
            InitializeComponent();
            SizeChangeAll(Config.GetSingleton.GetSkyrimPluginEditor_Width() / this.Width);
            this.Height = Config.GetSingleton.GetSkyrimPluginEditor_Height();
            this.Width = Config.GetSingleton.GetSkyrimPluginEditor_Width();
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
            CB_MasterPluginBefore_Update(true);
            LV_ConvertList_Update(true);
            CB_MatchCase_Update();

            LV_ConvertList_Active(false);
            LV_PluginList_Active(false);
            LV_FragmentList_Active(false);
            MI_Reset_Active(false);
            MI_Save_Active(false);
            MI_FileManager_Active(false);
            MI_NifManager_Active(false);
            MI_Macro_Active(false);

            SafetyMode = Config.GetSingleton.GetSkyrimPluginEditor_SafetyMode();
            MI_SafetyMode.IsChecked = SafetyMode;
            FileBackup = Config.GetSingleton.GetSkyrimPluginEditor_FileBackup();
            MI_FileBackup.IsChecked = FileBackup;
        }

        private void MI_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            folderBrowser.InitialDirectory = Config.GetSingleton.GetDefaultPath();

            if (folderBrowser.ShowDialog() ?? false)
            {
                selectedFolders.Clear();
                selectedFolders = folderBrowser.FolderNames.ToList();
            }
            else
                return;
            if (selectedFolders.Count == 0)
                return;

            foreach (string folder in selectedFolders)
            {
                Logger.Log.Info("Selected Folder : " + folder);
            }


            Task.Run(async () => SetPluginListView());

            if (selectedFolders.Count == 1)
                Config.GetSingleton.SetDefaultPath(selectedFolders.First());
            else 
            {
                var parent = System.IO.Directory.GetParent(selectedFolders.First());
                if (parent != null)
                    Config.GetSingleton.SetDefaultPath(parent.FullName);
            }

            if (App.fileManager != null && App.fileManager.IsLoaded)
                App.fileManager.UpdateFolderList(selectedFolders);
        }
        private void MI_OpenFolder_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_OpenFolder.IsEnabled = Active;
            }));
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

        public async void SetPluginListView()
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
            MI_Reset_Active(true);
            MI_Save_Active(false);
            MI_FileManager_Active(false);
            MI_NifManager_Active(false);
            MI_OpenFolder_Active(false);

            pluginDatas.Clear();
            Dictionary<string, PluginStreamBase._ErrorCode> wrongPlugins = new Dictionary<string, PluginStreamBase._ErrorCode>();
            if (Config.GetSingleton.GetParallelFolderRead())
            {
                Parallel.ForEach(selectedFolders, async folder =>
                {
                    if (Directory.Exists(folder))
                    {
                        var filesInDirectory = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                        if (filesInDirectory.Length > 0)
                        {
                            double fileStep = step / filesInDirectory.Length;
                            Parallel.ForEach(filesInDirectory, path =>
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
                                ProgressBarStep(fileStep);
                            });
                        }
                        else
                            ProgressBarStep(step);
                    }
                });
            }
            else
            {
                foreach (var folder in selectedFolders)
                {
                    if (Directory.Exists(folder))
                    {
                        var filesInDirectory = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                        if (filesInDirectory.Length > 0)
                        {
                            double fileStep = step / filesInDirectory.Length;
                            foreach (var path in filesInDirectory)
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
                                ProgressBarStep(fileStep);
                            }
                        }
                        else
                            ProgressBarStep(step);
                    }
                }
            }

            bool isEndedPluginName = false;
            bool isEndedFragment = false;
            bool isEndedMasterPlugin = false;
            bool isEndedConvertList = false;

            step = baseStep / (pluginDatas.Count == 0 ? 1 : Math.Sqrt(pluginDatas.Count));
            double maxCount = pluginDatas.Count / (pluginDatas.Count == 0 ? 1 : Math.Sqrt(pluginDatas.Count));

            Task.Run(async () =>
            {
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
                        if (!SafetyMode)
                            LV_PluginList_Update();
                        ProgressBarStep(step);
                        dataCount -= maxCount;
                    }
                }

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
                isEndedPluginName = true;

                if (isEndedPluginName && isEndedFragment && isEndedMasterPlugin && isEndedConvertList)
                {
                    ProgressBarDone();
                    MI_OpenFolder_Active();
                    MI_Macro_Active();
                }
            });

            Task.Run(async () =>
            {
                double dataCount = 0;
                foreach (var plugin in pluginDatas)
                {
                    var list = plugin.Value.GetEditableListOfRecord();
                    foreach (var item in list)
                    {
                        int index = fragmentList.FindIndex(x => x.FragmentType == item.FragmentType);
                        if (index != -1)
                        {
                            fragmentList[index].FromRecordList.Add(item.RecordType);
                            if (fragmentList[index].FromRecordList.Count >= 10 && fragmentList[index].FromRecordList.Count % 10 == 0)
                                fragmentList[index].FromRecoredsToolTip += "\n" + item.RecordType;
                            else
                                fragmentList[index].FromRecoredsToolTip += ", " + item.RecordType;
                            continue;
                        }
                        fragmentList.Add(new FragmentTypeData()
                        {
                            IsChecked = true,
                            FragmentType = item.FragmentType,
                            IsSelected = false,
                            FromRecoredsToolTip = item.RecordType,
                            FromRecordList = new HashSet<string> { item.RecordType },
                            IsEnabled = true,
                            Foreground = System.Windows.Media.Brushes.Black
                        });
                    }
                    dataCount++;
                    if (dataCount >= maxCount)
                    {
                        if (!SafetyMode)
                            LV_FragmentList_Update();
                        ProgressBarStep(step);
                        dataCount -= maxCount;
                    }
                }

                fragmentList.Sort((x, y) => { return x.FragmentType.CompareTo(y.FragmentType); });
                LV_FragmentList_Update();
                LV_FragmentList_Active();

                isEndedFragment = true;

                if (isEndedPluginName && isEndedFragment && isEndedMasterPlugin && isEndedConvertList)
                { 
                    ProgressBarDone();
                    MI_OpenFolder_Active();
                    MI_Macro_Active();
                }
            });

            Task.Run(async () =>
            {
                masterPluginList.Add(new MasterPluginField() { MasterPluginName = "None", MasterPluginNameOrig = "None", FromPlugins = "None" });
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
                        ProgressBarStep(step);
                        dataCount -= maxCount;
                    }
                }

                CB_MasterPluginBefore_Active();

                isEndedMasterPlugin = true;

                if (isEndedPluginName && isEndedFragment && isEndedMasterPlugin && isEndedConvertList)
                {
                    ProgressBarDone();
                    MI_OpenFolder_Active();
                    MI_Macro_Active();
                }
            });

            Task.Run(async () =>
            {
                UInt64 count = 0;
                double dataCount = 0;
                foreach (var data in pluginDatas)
                {
                    var list = data.Value.GetEditableListOfRecord();
                    foreach (var item in list)
                    {
                        dataEditFields.Add(new DataEditField()
                        {
                            PluginName = data.Key,
                            PluginPath = data.Value.GetFilePath(),
                            RecordType = item.RecordType,
                            FragmentType = item.FragmentType,
                            IsChecked = true,
                            IsSelected = false,
                            TextBefore = item.Text,
                            TextAfter = item.Text,
                            TextBeforeDisplay = MakeAltDataEditField(item.RecordType, item.FragmentType, item.Text),
                            TextAfterDisplay = MakeAltDataEditField(item.RecordType, item.FragmentType, item.Text),
                            Index = count,
                            ToolTip = data.Value.GetFilePath(),
                            EditableIndex = item.EditableIndex
                        });
                        count++;
                    }
                    ++dataCount;
                    if (dataCount >= maxCount)
                    {
                        if (!SafetyMode)
                            LV_ConvertList_Update();
                        ProgressBarStep(step);
                        dataCount -= maxCount;
                    }
                }

                LV_ConvertList_Update();
                LV_ConvertList_Active();

                isEndedConvertList = true;

                if (isEndedPluginName && isEndedFragment && isEndedMasterPlugin && isEndedConvertList)
                {
                    ProgressBarDone();
                    MI_OpenFolder_Active();
                    MI_Macro_Active();
                }

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

            BT_Apply_Update();
            MI_FileManager_Active();
            MI_NifManager_Active();
        }

        double ProgressBarMax = 10000;
        double ProgressBarValue = 0;
        private void ProgressBarInitial(double Maximum = 10000)
        {
            ProgressBarValue = 0;
            ProgressBarMax = Maximum;
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                PB_Loading.Value = ProgressBarValue;
                PB_Loading.Minimum = 0;
                PB_Loading.Maximum = ProgressBarMax;
            }));
        }
        private async void ProgressBarStep(double step = 1)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                ProgressBarValue += step;
                PB_Loading.Value = ProgressBarValue;
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
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

        private void SizeChangeAll(double Ratio)
        {
            GVC_ConvertListBefore.Width = GVC_ConvertListBefore.Width * Ratio;
            GVC_ConvertListAfter.Width = GVC_ConvertListAfter.Width * Ratio;
            GVC_Plugins.Width = GVC_Plugins.Width * Ratio;
            GVC_Fragments.Width = GVC_Fragments.Width * Ratio;
        }

        private void BT_Apply_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                BT_Apply.IsEnabled = Active;
            }));
        }
        private async void BT_Apply_Update()
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
                                    data.TextAfterDisplay = MakeAltDataEditField(data);
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
                                    data.TextAfterDisplay = MakeAltDataEditField(data);
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
                Parallel.ForEach(dataEditFields, data =>
                {
                    if (data.IsChecked && data.TextAfter != null && data.TextAfter.Length > 0)
                    {
                        data.TextAfter = Util.Replace(data.TextAfter, ReplaceSearch, ReplaceResult, matchCase.IsChecked);
                        data.TextAfterDisplay = MakeAltDataEditField(data);
                        dataEditFieldsEdited[data.Index] = data;
                    }
                });
                LV_ConvertList_Update();
                LV_ConvertList_Active();
            }

            BT_Apply_Update();
            MI_Save_Active();
            ApplyActiveDelay();
        }
        private async void ApplyActiveDelay()
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                BT_Apply.IsEnabled = false;
            }));
            await Task.Delay(500);
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                BT_Apply.IsEnabled = true;
            }));
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
                FragmentListUpdate();
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
                FragmentListUpdate();
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

        private void FragmentListUpdate()
        {
            foreach (var item in fragmentList)
            {
                if (pluginList.FindIndex(x => x.IsChecked && item.FromRecordList.Contains(x.RecordType)) != -1)
                {
                    item.Foreground = System.Windows.Media.Brushes.Black;
                    item.IsEnabled = true;
                }
                else
                {
                    item.Foreground = System.Windows.Media.Brushes.LightGray;
                    item.IsEnabled = false;
                }
            }
            LV_FragmentList_Update();
        }

        private void InactiveListUpdate(List<PluginToggleData> datas, bool IsChecked)
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
            DataEditFieldUpdate(IsChecked);
        }
        private void InactiveListUpdate(PluginToggleData data, bool IsChecked)
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
            DataEditFieldUpdate(IsChecked);
        }
        private void DataEditFieldUpdate(bool IsChecked)
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

        private async void CB_MasterPluginBefore_Update(bool binding = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    CB_MasterPluginBefore.ItemsSource = masterPluginList;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(masterPluginList);
                    view.Refresh();
                }
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

        private async void CB_MatchCase_Update()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                CB_MatchCase.DataContext = matchCase;
            }));
        }
        private async void LV_PluginList_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
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
                }
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
        }
        private void LV_PluginList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_PluginList.IsEnabled = Active;
            }));
        }
        private async void LV_FragmentList_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    LV_FragmentList.ItemsSource = fragmentList;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(fragmentList);
                    view.Refresh();
                }
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
        }
        private void LV_FragmentList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_FragmentList.IsEnabled = Active;
            }));
        }

        private async void LV_ConvertList_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
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
                }
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
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
            setting.ShowDialog();
            if (setting.IsChangedEncoding() || setting.IsChangedStringLanguage())
                SetPluginListView();
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
                var datafound = dataEditFieldsEdited.Where(x => x.Value.ToolTip == plugin.Key);
                foreach (var item in datafound)
                {
                    plugin.Value.EditEditableList(item.Value.EditableIndex, item.Value.TextAfter);
                }
                plugin.Value.ApplyEditableDatas();
                plugin.Value.Write(FileBackup);
            });
            if (!MacroMode)
                System.Windows.MessageBox.Show("Save done!");
            SetPluginListView();
        }

        private void MI_FileManager_Click(object sender, RoutedEventArgs e)
        {
            if (App.fileManager != null && App.fileManager.IsLoaded)
                return;
            App.fileManager = new FileManager(selectedFolders);
            App.fileManager.Show();
        }
        private void MI_FileManager_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_FileManager.IsEnabled = Active;
            }));
        }

        private void MI_Macro_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_Macro.IsEnabled = Active;
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
            Config.GetSingleton.SetSkyrimPluginEditor_Size(this.Height, this.Width);
            Application.Current.Shutdown();
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
        }

        private void MI_SafetyMode_CheckUncheck(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            SafetyMode = mi.IsChecked;
            Config.GetSingleton.SetSkyrimPluginEditor_SafetyMode(SafetyMode);
        }

        private void MI_Macro_Click(object sender, RoutedEventArgs e)
        {
            string file = Util.GetMacroFile();
            if (file == "")
                return;

            bool isTextEdit = false;
            bool isFileEdit = false;
            bool isNifEdit = false;
            bool isSave = false;

            LV_PluginList_Active(false);
            LV_FragmentList_Active(false);
            CB_MasterPluginBefore_Active(false);
            BT_Apply_Active(false);
            LV_ConvertList_Active(false);
            MI_OpenFolder_Active(false);
            MI_Reset_Active(false);
            MI_FileManager_Active(false);
            MI_NifManager_Active(false);
            MacroMode = true;

            foreach (var line in File.ReadLines(file))
            {
                if (line.Length == 0)
                    continue;

                if (line == "TEXTEDIT")
                    isTextEdit = true;
                else if (line == "FILEEDIT")
                {
                    isTextEdit = false;
                    isFileEdit = true;
                }
                else if (line == "NIFEDIT")
                {
                    isTextEdit = false;
                    isNifEdit = true;
                }

                if (!isTextEdit)
                    continue;

                var macroline = line;
                if (macroline.Contains("#"))
                {
                    if (macroline.StartsWith("#"))
                        continue;
                    var splitline = macroline.Split('#');
                    macroline = splitline[0];
                }

                var macro = macroline.Split('|');
                if (macro.Length < 1)
                    continue;

                var m1 = macro[0];
                if (m1 == "FILTER")
                {
                    if (macro.Length < 3)
                        continue;
                    var m2 = macro[1];
                    var m3 = macro[2];
                    if (m2 == "PLUGINS")
                    {
                        if (m3 == "CHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                foreach (var item in pluginList)
                                {
                                    item.IsChecked = true;
                                }
                            }
                            else
                            {
                                foreach (var item in pluginList)
                                {
                                    if (item.PluginName == m4 || item.RecordType == m4)
                                    {
                                        item.IsChecked = true;
                                    }
                                }
                            }
                        }
                        else if (m3 == "UNCHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                foreach (var item in pluginList)
                                {
                                    item.IsChecked = false;
                                }
                            }
                            else
                            {
                                foreach (var item in pluginList)
                                {
                                    if (item.PluginName == m4 || item.RecordType == m4)
                                    {
                                        item.IsChecked = false;
                                    }
                                }
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            foreach (var item in pluginList)
                            {
                                item.IsChecked = !item.IsChecked;
                            }
                        }
                    }
                    else if (m2 == "FRAGMENTS")
                    {
                        if (m3 == "CHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                foreach (var item in fragmentList)
                                {
                                    item.IsChecked = true;
                                }
                            }
                            else
                            {
                                foreach (var item in fragmentList)
                                {
                                    if (item.FragmentType == m4)
                                    {
                                        item.IsChecked = true;
                                    }
                                }
                            }
                        }
                        else if (m3 == "UNCHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                foreach (var item in fragmentList)
                                {
                                    item.IsChecked = false;
                                }
                            }
                            else
                            {
                                foreach (var item in fragmentList)
                                {
                                    if (item.FragmentType == m4)
                                    {
                                        item.IsChecked = false;
                                    }
                                }
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            foreach (var item in fragmentList)
                            {
                                item.IsChecked = !item.IsChecked;
                            }
                        }
                    }
                    else if (m2 == "CONVERT")
                    {
                        if (m3 == "CHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                Parallel.ForEach(dataEditFields, item =>
                                {
                                    item.IsChecked = true;
                                });
                            }
                            else
                            {
                                Parallel.ForEach(dataEditFields, item =>
                                {
                                    if (item.TextAfter.Contains(m4))
                                    {
                                        item.IsChecked = true;
                                    }
                                });
                            }
                        }
                        else if (m3 == "UNCHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                Parallel.ForEach(dataEditFields, item =>
                                {
                                    item.IsChecked = false;
                                });
                            }
                            else
                            {
                                Parallel.ForEach(dataEditFields, item =>
                                {
                                    if (item.TextAfter.Contains(m4))
                                    {
                                        item.IsChecked = false;
                                    }
                                });
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            Parallel.ForEach(dataEditFields, item =>
                            {
                                item.IsChecked = !item.IsChecked;
                            });
                        }
                    }
                    else if (m2 == "MATCHCASE")
                    {
                        if (m3 == "CHECK")
                        {
                            matchCase.IsChecked = true;
                        }
                        else if (m3 == "UNCHECK")
                        {
                            matchCase.IsChecked = false;
                        }
                    }
                }
                else if (m1 == "APPLY")
                {
                    if (macro.Length < 3)
                        continue;
                    var m2 = macro[1];
                    var m3 = macro[2];
                    if (m2 == "MASTER")
                    {
                        if (macro.Length < 4)
                            continue;
                        var m4 = macro[3];
                        var index = masterPluginList.FindIndex(x => x.MasterPluginName == m3);
                        if (index != -1)
                        {
                            masterPluginList[index].MasterPluginName = m4;
                        }
                    }
                    else if (m2 == "ADDPREFIX")
                    {
                        Parallel.ForEach(dataEditFields, item =>
                        {
                            if (item.IsChecked)
                            {
                                item.TextAfter = m3 + item.TextAfter;
                                item.TextAfterDisplay = MakeAltDataEditField(item);
                                dataEditFieldsEdited.AddOrUpdate(item.Index, item, (key, oldvalue) => item);
                            }
                        });
                    }
                    else if (m2 == "ADDSUFFIX")
                    {
                        Parallel.ForEach(dataEditFields, item =>
                        {
                            if (item.IsChecked)
                            {
                                item.TextAfter = item.TextAfter + m3;
                                item.TextAfterDisplay = MakeAltDataEditField(item);
                                dataEditFieldsEdited.AddOrUpdate(item.Index, item, (key, oldvalue) => item);
                            }
                        });
                    }
                    else if (m2 == "REPLACE")
                    {
                        var m4 = "";
                        if (macro.Length > 3)
                            m4 = macro[3];
                        Parallel.ForEach(dataEditFields, item =>
                        {
                            if (item.IsChecked)
                            {
                                item.TextAfter = Util.Replace(item.TextAfter, m3, m4, matchCase.IsChecked);
                                item.TextAfterDisplay = MakeAltDataEditField(item);
                                dataEditFieldsEdited[item.Index] = item;
                            }
                        });
                    }
                }
                else if (m1 == "SAVE")
                {
                    MI_Save_Click(sender, e);
                    isSave = true;
                }
                Task.Delay(100);
            }
            if (!isSave)
            {
                LV_PluginList_Update();
                LV_FragmentList_Update();
                LV_ConvertList_Update();
            }
            LV_PluginList_Active();
            LV_FragmentList_Active();
            CB_MasterPluginBefore_Active();
            BT_Apply_Update();
            LV_ConvertList_Active();
            MI_OpenFolder_Active();
            MI_Reset_Active();
            MI_FileManager_Active();
            MI_NifManager_Active();

            if (isFileEdit)
            {
                App.fileManager = new FileManager(selectedFolders);
                //App.fileManager.Show();
                while(!App.fileManager.IsInitialDone())
                {
                    Task.Delay(100);
                }
                App.fileManager.Macro_Load(sender, e, file, !App.fileManager.IsLoaded);
            }
            if (isNifEdit)
            {
                App.nifManager = new NifManager(selectedFolders);
                //App.nifManager.Show();
                while (!App.nifManager.IsInitialDone())
                {
                    Task.Delay(100);
                }
                App.nifManager.Macro_Load(sender, e, file, !App.nifManager.IsLoaded);
            }

            MacroMode = false;
            System.Windows.MessageBox.Show("Macro loaded");
        }

        private void MI_HyperLink_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            string url = "";
            if (mi == MI_Discord)
                url = "https://discord.gg/bVTuD2J2bE";
            else if (mi == MI_Patreon)
                url = "https://www.patreon.com/acro748";
            else if (mi == MI_GitHub)
                url = "https://github.com/Acro748/SkyrimPluginTextEditor";

            if (url == "")
                return;

            var sInfo = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
            System.Diagnostics.Process.Start(sInfo);
        }

        private void MI_FileBackup_CheckUncheck(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            FileBackup = mi.IsChecked;
            Config.GetSingleton.SetSkyrimPluginEditor_FileBackup(FileBackup);
        }

        private void MI_NifManager_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_NifManager.IsEnabled = Active;
            }));
        }
        private void MI_NifManager_Click(object sender, RoutedEventArgs e)
        {
            if (App.nifManager != null && App.nifManager.IsLoaded)
                return;
            App.nifManager = new NifManager(selectedFolders);
            App.nifManager.Show();
        }
    }

    public class CheckBoxBinder : INotifyPropertyChanged
    {
        private bool _IsChecked;
        public bool IsChecked
        {
            get { return _IsChecked; }
            set
            {
                _IsChecked = value;
                OnPropertyChanged("IsChecked");
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
    public class PluginListData : INotifyPropertyChanged
    {
        public string PluginName { get; set; }
        public string RecordType { get; set; }

        public string PluginPath { get; set; }

        private bool _IsChecked;
        public bool IsChecked
        {
            get { return _IsChecked; }
            set
            {
                _IsChecked = value;
                OnPropertyChanged("IsChecked");
            }
        }

        private bool _IsSelected;
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
        public string FragmentType { get; set; }
        private string _FromRecoredsToolTip;
        public string FromRecoredsToolTip
        {
            get { return _FromRecoredsToolTip; }
            set
            {
                _FromRecoredsToolTip = value;
                OnPropertyChanged("FromRecoredsToolTip");
            }
        }

        public HashSet<string> FromRecordList { get; set; }

        private bool _IsChecked;
        public bool IsChecked
        {
            get { return _IsChecked; }
            set
            {
                _IsChecked = value;
                OnPropertyChanged("IsChecked");
            }
        }

        private bool _IsSelected;
        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                _IsSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        private bool _IsEnabled;
        public bool IsEnabled
        {
            get { return _IsEnabled; }
            set
            {
                _IsEnabled = value;
                OnPropertyChanged("IsEnable");
            }
        }

        private System.Windows.Media.Brush _Foreground;
        public System.Windows.Media.Brush Foreground
        {
            get { return _Foreground; }
            set
            {
                _Foreground = value;
                OnPropertyChanged("Foreground");
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
    public class MasterPluginField : INotifyPropertyChanged
    {
        private string _MasterPluginName;
        public string MasterPluginName 
        { 
            get { return _MasterPluginName; }
            set
            {
                _MasterPluginName = value;
                OnPropertyChanged("MasterPluginName");
            }
        }
        public string MasterPluginNameOrig { get; set; }
        public string FromPlugins { get; set; }
        public bool IsEdited { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyname)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
            }
        }
    }
    public class DataEditField : INotifyPropertyChanged
    {
        public string PluginName { get; set; }
        public string PluginPath { get; set; }
        public string RecordType { get; set; }
        public string FragmentType { get; set; }
        public string TextBefore { get; set; }
        public string TextBeforeDisplay { get; set; }
        public string TextAfter { get; set; }
        public string TextAfterDisplay { get; set; }

        public UInt64 Index { get; set; }
        private string _ToolTip;
        public string ToolTip
        {
            get { return _ToolTip; }
            set
            {
                _ToolTip = value;
                OnPropertyChanged("ToolTip");
            }
        }
        public int EditableIndex { get; set; }
        public bool IsEdited { get; set; }

        private bool _IsChecked;
        public bool IsChecked
        {
            get { return _IsChecked; }
            set
            {
                _IsChecked = value;
                OnPropertyChanged("IsChecked");
            }
        }

        private bool _IsSelected;
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
