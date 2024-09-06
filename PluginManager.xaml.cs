using log4net.Plugin;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading;
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

    public partial class PluginManager : Window
    {
        private List<string> selectedFolders = new List<string>();
        private List<string> selectedFiles = new List<string>();
        private Dictionary<string, SkyrimPluginFile> pluginDatas = new Dictionary<string, SkyrimPluginFile>(); //fullpath, plugindata
        private List<PluginListData> pluginList = new List<PluginListData>();
        private List<FragmentTypeData> fragmentList = new List<FragmentTypeData>();
        private List<PluginToggleData> inactiveList = new List<PluginToggleData>(); //if there is a data then set invisible it
        private List<MasterPluginField> masterPluginList = new List<MasterPluginField>();
        private List<DataEditField> dataEditFields = new List<DataEditField>();
        private List<DataEditField> dataEditFieldsDisable = new List<DataEditField>();
        private ConcurrentDictionary<string, SkyrimPluginFile> dataEditFieldsEdited = new ConcurrentDictionary<string, SkyrimPluginFile>();
        private Setting setting = new Setting();
        private OpenFolderDialog folderBrowser = new OpenFolderDialog() { Title = "Select plugin folders...", Multiselect = true };
        private CheckBoxBinder SafetyMode = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetSkyrimPluginEditor_SafetyMode() };
        private CheckBoxBinder FileBackup = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetSkyrimPluginEditor_FileBackup() };
        private CheckBoxBinder matchCase = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetSkyrimPluginEditor_MatchCase() };
        private bool MacroMode = false;

        public PluginManager()
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

            LV_ConvertList_Active(false);
            LV_PluginList_Active(false);
            LV_FragmentList_Active(false);
            MI_Reset_Active(false);
            MI_Save_Active(false);
            MI_Macro_Active(false);

            SafetyMode.IsChecked = Config.GetSingleton.GetSkyrimPluginEditor_SafetyMode();
            FileBackup.IsChecked = Config.GetSingleton.GetSkyrimPluginEditor_FileBackup();
            matchCase.IsChecked = Config.GetSingleton.GetSkyrimPluginEditor_MatchCase();
            MI_SafetyMode.DataContext = SafetyMode;
            MI_FileBackup.DataContext = FileBackup;
            CB_MatchCase.DataContext = matchCase;
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

            LoadFolderList(selectedFolders);

            if (selectedFolders.Count == 1)
                Config.GetSingleton.SetDefaultPath(selectedFolders.First());
            else 
            {
                var parent = System.IO.Directory.GetParent(selectedFolders.First());
                if (parent != null)
                    Config.GetSingleton.SetDefaultPath(parent.FullName);
            }

            if (App.fileManager != null && App.fileManager.IsLoaded)
                App.fileManager.LoadFolderList(selectedFolders);
            if (App.nifManager != null && App.nifManager.IsLoaded)
                App.nifManager.LoadNifFilesFromFolders(selectedFolders);
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
                SkyrimPluginFile pm = new SkyrimPluginFile(path);
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
        public void LoadFolderList(List<string> folders)
        {
            selectedFolders = folders;
            selectedFiles = Util.GetAllFilesFromFolders(selectedFolders, SearchOption.TopDirectoryOnly).FindAll(x => Util.IsPluginFIle(x));
            Load();
        }
        public void LoadFileList(List<string> files)
        {
            selectedFiles = files;
            Load();
        }
        public void LoadFileList(List<MoveFileSet> files)
        {
            foreach (var file in files.FindAll(x => selectedFiles.Find(y => Util.IsSameStringIgnoreCase(x.FileBasePath + x.FileBefore, y)) != null))
            {
                selectedFiles.Remove(file.FileBasePath + file.FileBefore);
                selectedFiles.Add(file.FileBasePath + file.FileAfter);
            }
            Load();
        }
        public async void Load()
        {
            await Task.Run(() => SetPluginListView_Impl());
        }
        public void SetPluginListView_Impl()
        {
            ProgressBarInitial();

            double baseStep = ProgressBarMaximum() / 5;
            double step = baseStep / selectedFiles.Count;

            masterPluginList.Clear();
            pluginList.Clear();
            fragmentList.Clear();
            dataEditFields.Clear();
            dataEditFieldsDisable.Clear();
            dataEditFieldsEdited.Clear();
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
            MI_OpenFolder_Active(false);

            pluginDatas.Clear();
            ConcurrentDictionary<string, SkyrimPluginFile> pluginDatasTemp = new ConcurrentDictionary<string, SkyrimPluginFile>();
            ConcurrentDictionary<string, PluginStreamBase._ErrorCode> wrongPlugins = new ConcurrentDictionary<string, PluginStreamBase._ErrorCode>();
            Parallel.ForEach(selectedFiles, path =>
            {
                if (File.Exists(path))
                {
                    PluginStreamBase._ErrorCode errorCode = PluginStreamBase._ErrorCode.Passed;
                    string fileName = System.IO.Path.GetFileName(path);
                    if (Util.IsPluginFIle(fileName))
                    {
                        SkyrimPluginFile pm = new SkyrimPluginFile(path);
                        errorCode = pm.Read();
                        if (errorCode >= 0)
                        {
                            Logger.Log.Info(errorCode + " : " + path);
                            pluginDatasTemp.AddOrUpdate(path, pm, (key, oldValue) => pm);
                        }
                        else if ((Int16)errorCode <= -20)
                            Logger.Log.Warn(errorCode + " : " + path);
                        else
                            Logger.Log.Error(errorCode + " : " + path);
                    }

                    if (errorCode < 0 && -20 < (int)errorCode)
                    {
                        wrongPlugins.AddOrUpdate(fileName, errorCode, (key, oldValue) => errorCode);
                    }
                }
                ProgressBarStep(step);
            });
            pluginDatas = pluginDatasTemp.ToDictionary();

            step = baseStep / (pluginDatas.Count == 0 ? 1 : pluginDatas.Count) * 3;

            ConcurrentDictionary<string, PluginListData> newPluginList = new ConcurrentDictionary<string, PluginListData>();
            ConcurrentDictionary<string, FragmentTypeData> newFragmentList = new ConcurrentDictionary<string, FragmentTypeData>();
            masterPluginList.Add(new MasterPluginField() { MasterPluginName = "None", MasterPluginNameOrig = "None" });
            ConcurrentDictionary<string, MasterPluginField> newMasterPluginList = new ConcurrentDictionary<string, MasterPluginField>();
            ConcurrentBag<DataEditField> newDataEditFields = new ConcurrentBag<DataEditField>(); 
            ulong dataIndex = 0;
            Parallel.ForEach(pluginDatas, plugin =>
            {
                var list = GetEditableFragments(plugin.Value);
                double miniStep = step / (list.Count == 0 ? 1 : list.Count);
                if (list.Count > 0)
                {
                    foreach(var item in list)
                    {
                        string recordType = plugin.Value.GetRecordTypeByFragment(item);
                        var newPlugin = new PluginListData()
                        {
                            File = plugin.Value,
                            Fragment = item,
                            PluginName = plugin.Value.GetFileName(),
                            IsChecked = true,
                            RecordType = recordType,
                            IsSelected = false,
                            PluginPath = plugin.Value.GetFilePath()
                        };
                        newPluginList.AddOrUpdate(plugin.Value.GetFilePath() + recordType, newPlugin, (key, oldValue) => oldValue);

                        //fragment list
                        var newFragment = new FragmentTypeData()
                        {
                            IsChecked = true,
                            FragmentType = item.GetFragmentType(),
                            IsSelected = false,
                            FromRecoredsToolTip = "",
                            FromRecordList = new ConcurrentDictionary<string, string>(),
                            IsEnabled = true,
                            Foreground = System.Windows.Media.Brushes.Black
                        };
                        newFragment.FromRecordList.AddOrUpdate(recordType, recordType, (key, oldValue) => oldValue);
                        newFragmentList.AddOrUpdate(item.GetFragmentType(), newFragment, (key, oldValue) =>
                        {
                            oldValue.FromRecordList.AddOrUpdate(recordType, recordType, (key, oldValue) => oldValue);
                            return oldValue;
                        });

                        DataEditField newDataEditField = new DataEditField();
                        newDataEditField.file = plugin.Value;
                        newDataEditField.fragment = item;
                        newDataEditField.PluginName = plugin.Value.GetFileName();
                        newDataEditField.PluginPath = plugin.Value.GetFilePath();
                        newDataEditField.RecordType = recordType;
                        newDataEditField.FragmentType = item.GetFragmentType();
                        newDataEditField.Index = Interlocked.Increment(ref dataIndex);
                        newDataEditField.IsChecked = true;
                        newDataEditField.IsSelected = false;
                        newDataEditField.TextBefore = plugin.Value.GetString(item);
                        newDataEditField.TextAfter = newDataEditField.TextBefore;
                        newDataEditField.TextBeforeDisplay = MakeAltDataEditField(recordType, item.GetFragmentType(), item.GetDataAsString());
                        newDataEditField.TextAfterDisplay = newDataEditField.TextBeforeDisplay;
                        newDataEditField.ToolTip = newDataEditField.PluginPath;
                        newDataEditFields.Add(newDataEditField);

                        ProgressBarStep(miniStep);
                    };
                }
                else
                    ProgressBarStep(step);

                var masters = plugin.Value.GetMasterPlugins();
                foreach (var m in masters)
                {
                    var newMaster = new MasterPluginField()
                    {
                        Master = new List<SkyrimPluginData._MAST> { m },
                        Owner = new List<SkyrimPluginFile> { plugin.Value },
                        MasterPluginName = m.GetMasterPluginName(),
                        MasterPluginNameOrig = m.GetMasterPluginName(),
                    };
                    newMasterPluginList.AddOrUpdate(m.GetMasterPluginName(), newMaster, (key, oldValue) => {
                        oldValue.Master.Add(m);
                        oldValue.Owner.Add(plugin.Value);
                        return oldValue;
                    });
                }

                LV_PluginList_Update();
                LV_FragmentList_Update();
                LV_ConvertList_Update();
                CB_MasterPluginBefore_Update();
            });
            pluginList.AddRange(newPluginList.Values.ToList().Except(pluginList));
            fragmentList.AddRange(newFragmentList.Values.ToList().Except(fragmentList));
            masterPluginList.AddRange(newMasterPluginList.Values.ToList().Except(masterPluginList));
            dataEditFields.AddRange(newDataEditFields);

            LV_PluginList_Update();
            LV_FragmentList_Update();
            LV_ConvertList_Update();

            step = ProgressBarLeft() / 4;

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
            ProgressBarStep(step);

            fragmentList.Sort((x, y) => { return x.FragmentType.CompareTo(y.FragmentType); });
            foreach (var itemX in fragmentList)
            {
                bool isFirst = true;
                int count = 0;
                foreach (var itemY in itemX.FromRecordList)
                {
                    if (!isFirst)
                        itemX.FromRecoredsToolTip += ", ";
                    if (count > 0 && count % 10 == 0)
                        itemX.FromRecoredsToolTip += "\n";
                    itemX.FromRecoredsToolTip += itemY.Key;
                    count++;
                    isFirst = true;
                }
            }
            LV_FragmentList_Update();
            ProgressBarStep(step);

            DataEditFieldSort();
            LV_ConvertList_Update();
            ProgressBarStep(step);

            LV_PluginList_Update();
            LV_FragmentList_Update();
            LV_ConvertList_Update();

            LV_PluginList_Active();
            LV_FragmentList_Active();
            CB_MasterPluginBefore_Active();
            LV_ConvertList_Active();

            ProgressBarDone();
            MI_OpenFolder_Active();
            MI_Macro_Active();

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
                System.Windows.MessageBox.Show(wronglist, "ERROR : Wrong Plugin!");
            }

            BT_Apply_Update();

            Logger.Log.Info("Loaded " + dataEditFields.Count + " from " + selectedFiles.Count + " files");
        }

        double ProgressBarMax = 100000000;
        double ProgressBarValue = 0;
        private void ProgressBarInitial(double Maximum = 100000000)
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
        object progressLock = new object();
        private async Task ProgressBarStep(double step = 1)
        {
            lock (progressLock)
            {
                ProgressBarValue += step;
            }
            await Task.Run(() => ProgressBarUpdate());
        }
        private async Task ProgressBarUpdate()
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
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
        private async Task ProgressBarDone()
        {
            lock (progressLock)
            {
                ProgressBarValue = ProgressBarMax;
            }
            await Task.Run(() => ProgressBarUpdate());
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
        private void BT_Apply_Update()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (selectedFiles.Count == 0)
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
        private void BT_Apply_Click(object sender, RoutedEventArgs e)
        {
            if (pluginDatas.Count == 0)
            {
                BT_Apply_Update();
                return;
            }
            BT_Apply_Active(false);

            if (CB_MasterPluginBefore.SelectedIndex > 0 && TB_MasterPluginAfter.Text.Length > 0)
            {
                Apply_Master(masterPluginList[CB_MasterPluginBefore.SelectedIndex].MasterPluginName, TB_MasterPluginAfter.Text);
            }

            if (CB_AddTextType.SelectedIndex > 0 && TB_AddText.Text.Length > 0)
            {
                Apply_AddText((AddTextType)CB_AddTextType.SelectedIndex, TB_AddText.Text);
            }

            if (TB_ReplaceSearch.Text.Length > 0 || TB_ReplaceResult.Text.Length > 0)
            {
                Apply_Replace(TB_ReplaceSearch.Text, TB_ReplaceResult.Text);
            }

            BT_Apply_Update();
            MI_Save_Active();
            ApplyActiveDelay();
        }

        private void Apply_Master(string search, string result)
        {
            CB_MasterPluginBefore_Active(false);
            search = Util.GetStringWithNullEnd(search);
            int index = masterPluginList.FindIndex(x => x.MasterPluginName.Contains(search));
            if (index != -1)
            {
                if (Util.IsPluginFIle(result))
                {
                    masterPluginList[index].MasterPluginName = result;
                    foreach (var mast in masterPluginList[index].Master)
                    {
                        mast.SetMasterPluginName(result);
                    }
                    foreach (var owner in masterPluginList[index].Owner)
                    {
                        dataEditFieldsEdited.AddOrUpdate(owner.GetFilePath(), owner, (key, oldvalue) => owner);
                    }
                }
                else
                    System.Windows.MessageBox.Show("Couldn't use " + result + " as plugin name!");
            }
            CB_MasterPluginBefore_Active();
        }
        private void Apply_AddText(AddTextType adType, string addText)
        {
            LV_ConvertList_Active(false);
            switch (adType)
            {
                case AddTextType.AddPrefix:
                    {
                        Parallel.ForEach(dataEditFields, data =>
                        {
                            if (data.IsChecked)
                            {
                                data.TextAfter = addText + data.TextAfter;
                                data.TextAfterDisplay = MakeAltDataEditField(data);
                                data.file.SetString(data.fragment, data.TextAfter);
                                dataEditFieldsEdited.AddOrUpdate(data.PluginPath, data.file, (key, oldvalue) => data.file);
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
                                data.TextAfter = data.TextAfter + addText;
                                data.TextAfterDisplay = MakeAltDataEditField(data);
                                data.file.SetString(data.fragment, data.TextAfter);
                                dataEditFieldsEdited.AddOrUpdate(data.PluginPath, data.file, (key, oldvalue) => data.file);
                            }
                        });
                        break;
                    }
            }
            LV_ConvertList_Update();
            LV_ConvertList_Active();
        }
        private void Apply_Replace(string search, string result)
        {
            LV_ConvertList_Active(false);
            Parallel.ForEach(dataEditFields, data =>
            {
                if (data.IsChecked && data.TextAfter != null && data.TextAfter.Length > 0)
                {
                    string strAftertemp = data.TextAfter;
                    data.TextAfter = Util.Replace(data.TextAfter, search, result, matchCase.IsChecked);
                    data.TextAfterDisplay = MakeAltDataEditField(data);
                    data.file.SetString(data.fragment, data.TextAfter);

                    if (strAftertemp != data.TextAfter)
                        dataEditFieldsEdited[data.PluginPath] = data.file;
                }
            });
            LV_ConvertList_Update();
            LV_ConvertList_Active();
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
            var plugin = sender as PluginListData;
            if (checkBox != null)
            {
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
            else if (plugin != null && !this.IsLoaded)
            {
                List<PluginToggleData> toggleDatas = new List<PluginToggleData>();
                foreach (PluginListData item in pluginList.FindAll(x => x.PluginPath == plugin.PluginPath))
                {
                    PluginToggleData toggleData = new PluginToggleData()
                    {
                        PluginPath = plugin.PluginPath,
                        RecordType = plugin.RecordType,
                        FragmentType = "0000"
                    };
                    toggleDatas.Add(toggleData);
                }
                InactiveListUpdate(toggleDatas, plugin.IsChecked);
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
            var plugin = sender as PluginListData;
            if (checkBox != null)
            {
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
            else if (plugin != null && !this.IsLoaded)
            {
                PluginToggleData toggleData = new PluginToggleData()
                {
                    PluginPath = plugin.PluginPath,
                    RecordType = plugin.RecordType,
                    FragmentType = "0000"
                };
                InactiveListUpdate(toggleData, plugin.IsChecked);
                FragmentListUpdate();
            }
        }

        private void CB_Fragments_CheckUncheck(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;
            var fragment = sender as FragmentTypeData;
            if (checkBox != null)
            {
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
            else if (fragment != null && !this.IsLoaded)
            {
                PluginToggleData toggleData = new PluginToggleData()
                {
                    PluginPath = "0000",
                    RecordType = "0000",
                    FragmentType = fragment.FragmentType
                };
                InactiveListUpdate(toggleData, fragment.IsChecked);
            }
        }

        private void FragmentListUpdate()
        {
            foreach (var item in fragmentList)
            {
                if (pluginList.FindIndex(x => x.IsChecked && item.FromRecordList.ContainsKey(x.RecordType)) != -1)
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
            DataEditFieldSort();
            LV_ConvertList_Update();
            LV_ConvertList_Active(true);
        }
        private async Task DataEditFieldSort()
        {
            int count = 0;
            await Task.Run(() => dataEditFields.Sort((x, y) =>
            {
                int result = x.PluginPath.CompareTo(y.PluginPath);
                if (result == 0)
                {
                    result = x.RecordType.CompareTo(y.RecordType);
                    if (result == 0)
                    {
                        result = x.FragmentType.CompareTo(y.FragmentType);
                    }
                }
                if (count < 1000)
                    count++;
                else
                {
                    count = 0;
                    LV_ConvertList_Update();
                }
                return result;
            }));
        }

        private async Task CB_MasterPluginBefore_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
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

        private async Task LV_PluginList_Update(bool binding = false)
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
        private async Task LV_FragmentList_Update(bool binding = false)
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

        private async Task LV_ConvertList_Update(bool binding = false)
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
                Load();
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
            Load();
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
            Save();
        }
        private void Save()
        {
            ConcurrentBag<string> failFiles = new ConcurrentBag<string>();
            Parallel.ForEach(dataEditFieldsEdited, plugin =>
            {
                if (!plugin.Value.Write(FileBackup.IsChecked))
                {
                    failFiles.Add(plugin.Key);
                    Logger.Log.Fatal("Unable to access the file : " + plugin.Key);
                }
            });
            if (failFiles.Count > 0)
            {
                bool first = true;
                string failFileList = "";
                foreach (var item in failFiles)
                {
                    if (!first)
                        failFileList += "\n";
                    failFileList += item;
                    first = false;
                }
                System.Windows.MessageBox.Show(failFileList, "ERROR : Unable to access the file!");
            }
            if (!MacroMode)
                System.Windows.MessageBox.Show("Save done!");
            Load();
        }

        private List<SkyrimPluginData._Record_Fragment> GetEditableFragments(SkyrimPluginData data)
        {
            List<SkyrimPluginData._Record_Fragment> finds = new List<SkyrimPluginData._Record_Fragment>();
            foreach (var editRecSig in Config.GetSingleton.GetEditableType())
            {
                foreach (var editFragSig in editRecSig.Value)
                {
                    finds.AddRange(data.GetFragments(editRecSig.Key, editFragSig).FindAll(
                        x => !Config.GetSingleton.IsEditableBlackList(data.GetRecordTypeByFragment(x), editFragSig)));
                }
            }
            return finds;
        }

        private void MI_FileManager_Click(object sender, RoutedEventArgs e)
        {
            if (App.fileManager != null)
            {
                if (App.fileManager.IsLoaded)
                    return;
            }
            else if (App.fileManager == null)
                App.fileManager = new FileManager();
            App.fileManager.Show(); 
            if (selectedFolders.Count > 0)
                App.fileManager.LoadFolderList(selectedFolders);
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
            Config.GetSingleton.SetSkyrimPluginEditor_SafetyMode(SafetyMode.IsChecked);
        }

        private void MI_Macro_Click(object sender, RoutedEventArgs e)
        {
            string file = Util.GetMacroFile();
            if (file == "")
                return;

            Macro_Load(file);
        }
        private void Macro_Load(string file)
        {
            if (!Util.CanRead(file))
                return;
            bool isPluginEdit = false;
            bool isFileEdit = false;
            bool isSave = false;

            LV_PluginList_Active(false);
            LV_FragmentList_Active(false);
            CB_MasterPluginBefore_Active(false);
            BT_Apply_Active(false);
            LV_ConvertList_Active(false);
            MI_OpenFolder_Active(false);
            MI_Reset_Active(false);
            MacroMode = true;

            foreach (var line in File.ReadLines(file))
            {
                if (line.Length == 0)
                    continue;

                if (line == "PLUGINEDIT")
                    isPluginEdit = true;
                else if (line == "FILEEDIT")
                {
                    isPluginEdit = false;
                    isFileEdit = true;
                }
                else if (line == "NIFEDIT")
                {
                    isPluginEdit = false;
                }

                if (!isPluginEdit)
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
                                    CB_Plugins_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in pluginList)
                                {
                                    if (Util.IsSameStringIgnoreCase(item.PluginName, m4) || Util.IsSameStringIgnoreCase(item.RecordType, m4))
                                    {
                                        item.IsChecked = true;
                                        CB_Plugins_CheckUncheck(item, new RoutedEventArgs());
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
                                    CB_Plugins_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in pluginList)
                                {
                                    if (Util.IsSameStringIgnoreCase(item.PluginName, m4) || Util.IsSameStringIgnoreCase(item.RecordType, m4))
                                    {
                                        item.IsChecked = false;
                                        CB_Plugins_CheckUncheck(item, new RoutedEventArgs());
                                    }
                                }
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            foreach (var item in pluginList)
                            {
                                item.IsChecked = !item.IsChecked;
                                CB_Plugins_CheckUncheck(item, new RoutedEventArgs());
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
                                    CB_Fragments_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in fragmentList)
                                {
                                    if (Util.IsSameStringIgnoreCase(item.FragmentType, m4))
                                    {
                                        item.IsChecked = true;
                                        CB_Fragments_CheckUncheck(item, new RoutedEventArgs());
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
                                    CB_Fragments_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in fragmentList)
                                {
                                    if (Util.IsSameStringIgnoreCase(item.FragmentType, m4))
                                    {
                                        item.IsChecked = false;
                                        CB_Fragments_CheckUncheck(item, new RoutedEventArgs());
                                    }
                                }
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            foreach (var item in fragmentList)
                            {
                                item.IsChecked = !item.IsChecked;
                                CB_Fragments_CheckUncheck(item, new RoutedEventArgs());
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
                                    if (item.TextAfter.Contains(m4, StringComparison.OrdinalIgnoreCase))
                                        item.IsChecked = true;
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
                                    if (item.TextAfter.Contains(m4, StringComparison.OrdinalIgnoreCase))
                                        item.IsChecked = false;
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
                        Apply_Master(m3, m4);
                    }
                    else if (m2 == "ADDPREFIX")
                    {
                        Apply_AddText(AddTextType.AddPrefix, m3);
                    }
                    else if (m2 == "ADDSUFFIX")
                    {
                        Apply_AddText(AddTextType.AddSuffix, m3);
                    }
                    else if (m2 == "REPLACE")
                    {
                        var m4 = "";
                        if (macro.Length > 3)
                            m4 = macro[3];
                        Apply_Replace(m3, m4);
                    }
                }
                else if (m1 == "SAVE")
                {
                    Save();
                    isSave = true;
                }
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

            if (isFileEdit)
            {
                if (App.fileManager == null)
                    App.fileManager = new FileManager();
                App.fileManager.LoadFolderList(selectedFolders, false);
                App.fileManager.Macro_Load(file, !App.fileManager.IsLoaded);
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
            Config.GetSingleton.SetSkyrimPluginEditor_FileBackup(FileBackup.IsChecked);
        }

        private void MI_NifManager_Click(object sender, RoutedEventArgs e)
        {
            if (App.nifManager != null)
            {
                if (App.nifManager.IsLoaded)
                    return;
            }
            else if (App.nifManager == null)
                App.nifManager = new NifManager();
            App.nifManager.Show();
            if (selectedFolders.Count > 0)
                App.nifManager.LoadNifFilesFromFolders(selectedFolders);
        }

        private void FileOrFolderDrop(object sender, DragEventArgs e)
        {
            if (e == null)
                return;
            string[] FileOrFolder = e.Data.GetData(DataFormats.FileDrop) as string[];
            e.Handled = true;

            foreach (var macro in FileOrFolder.ToList().FindAll(x => Util.IsMacroFile(x) && File.Exists(x)))
            {
                if (System.Windows.MessageBox.Show("Do you want to load the macro file?\n\n" + System.IO.Path.GetFileName(macro),
                    "Macro Load", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    Macro_Load(macro);
            }

            bool isUpdateFileList = false;
            var folders = FileOrFolder.ToList().FindAll(x => Directory.Exists(x));
            if (folders.Count > 0)
            {
                selectedFolders = folders;
                selectedFiles = Util.GetAllFilesFromFolders(selectedFolders.ToList(), SearchOption.TopDirectoryOnly).FindAll(x => Util.IsPluginFIle(x));
                isUpdateFileList = true;
            }
            var files = FileOrFolder.ToList().FindAll(x => File.Exists(x) && Util.IsPluginFIle(x));
            if (files.Count > 0)
            {
                if (isUpdateFileList)
                {
                    selectedFiles.AddRange(files);
                }
                else
                {
                    selectedFolders.Clear();
                    selectedFiles = files;
                }
                isUpdateFileList = true;
            }

            if (isUpdateFileList)
                Load();

            if (App.fileManager != null && App.fileManager.IsLoaded)
                App.fileManager.LoadFolderList(selectedFolders);
            if (App.nifManager != null && App.nifManager.IsLoaded)
                App.nifManager.LoadNifFilesFromFolders(selectedFolders);
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
        public SkyrimPluginFile File { get; set; }
        public SkyrimPluginData._Record_Fragment Fragment { get; set; }

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

        public ConcurrentDictionary<string, string> FromRecordList { get; set; }

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
        public List<SkyrimPluginData._MAST> Master;
        public List<SkyrimPluginFile> Owner;
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
        public SkyrimPluginFile file;
        public SkyrimPluginFile._Record_Fragment fragment;

        public string PluginName { get; set; }
        public string PluginPath { get; set; }
        public string RecordType { get; set; }
        public string FragmentType { get; set; }
        public string TextBefore { get; set; }
        public string TextBeforeDisplay { get; set; }
        public string TextAfter { get; set; }
        public string TextAfterDisplay { get; set; }

        public UInt64 Index = 0;

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
