using nifly;
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
using System.Windows.Shapes;
using System.Windows.Threading;
using static SkyrimPluginTextEditor.PluginManager;

#pragma warning disable CS4014 // 이 호출을 대기하지 않으므로 호출이 완료되기 전에 현재 메서드가 계속 실행됩니다.

namespace SkyrimPluginTextEditor
{
    /// <summary>
    /// NifManager.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NifManager : Window
    {
        private List<NifData> nifDatas = new List<NifData>();
        private List<NifData> nifDatasDisable = new List<NifData>();
        private List<NifData> nifDatas_Temp = new List<NifData>();
        private List<NifData> nifDatas_Facegen = new List<NifData>();
        private ConcurrentDictionary<string, NifData> editedNifDatas = new ConcurrentDictionary<string, NifData>(); //path, nifdata
        private List<NifDataToggleData> inactiveList = new List<NifDataToggleData>();
        private List<BlockName> blockNames = new List<BlockName>();
        private List<StringType> stringTypes = new List<StringType>();
        private List<string> selectedFolders = new List<string>();
        private List<string> meshes = new List<string>();
        private CheckBoxBinder matchCase = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetNifManager_MatchCase() };
        private CheckBoxBinder FacegenEdit = new CheckBoxBinder() { IsChecked = false };
        private CheckBoxBinder fileBackup = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetNifManager_FileBackup() };
        private bool MacroMode = false;
        private bool initialDone = false;
        private double stepSub = 3;

        public NifManager()
        {
            InitializeComponent();
            LV_NifDataList_Update(true);
            LV_BlockNameList_Update(true);
            LV_StringTypeList_Update(true);
            for (AddTextType i = 0; i < AddTextType.End; i++)
            {
                CB_AddTextType.Items.Add(i);
            }
            CB_AddTextType.SelectedIndex = 0;
            SizeChangeAll(Config.GetSingleton.GetNifManager_Width() / this.Width);
            this.Height = Config.GetSingleton.GetNifManager_Height();
            this.Width = Config.GetSingleton.GetNifManager_Width();
            matchCase.IsChecked = Config.GetSingleton.GetNifManager_MatchCase();
            fileBackup.IsChecked = Config.GetSingleton.GetNifManager_FileBackup();
            CB_MatchCase.DataContext = matchCase;
            MI_FileBackup.DataContext = fileBackup;
            MI_FaceGenEdit.DataContext = FacegenEdit;

            initialDone = true;
        }
        private List<string> GetNifFiles(List<string> FileOrFolder)
        {
            List<string> newMeshes = new List<string>();
            foreach (var fof in FileOrFolder)
            {
                if (Directory.Exists(fof))
                {
                    var filesInDirectory = Directory.GetFiles(fof, "*.nif", SearchOption.AllDirectories);
                    foreach (var path in filesInDirectory)
                    {
                        newMeshes.Add(path);
                    }
                }
                else if (File.Exists(fof))
                {
                    if (Util.IsNifFile(fof))
                        newMeshes.Add(fof);
                }
            }
            return newMeshes;
        }
        public void LoadNifFilesFromFolders(List<string> folders, bool macro = false)
        {
            selectedFolders = folders;
            meshes = GetNifFiles(selectedFolders);
            LoadNifFiles(macro);
        }
        public void LoadNifFiles(List<string> files, bool multiThread = true)
        {
            selectedFolders.Clear();
            meshes = GetNifFiles(files);
            LoadNifFiles(multiThread);
        }

        public bool IsInitialDone() { return initialDone; }

        private void LoadNifFiles(bool multiThread = true)
        {
            nifDatas.Clear();
            nifDatas_Facegen.Clear();
            nifDatas_Temp.Clear();
            editedNifDatas.Clear();
            blockNames.Clear();
            stringTypes.Clear();
            if (multiThread)
                Load();
            else
                LoadSingle();
        }
        private async void Load()
        {
            await Task.Run(() => LoadNifFiles_Impl());
        }
        private void LoadSingle()
        {
            LoadNifFiles_Impl();
        }

        private void LoadNifFiles_Impl()
        {
            nifDatas.Clear();
            nifDatas_Facegen.Clear();
            nifDatas_Temp.Clear();
            editedNifDatas.Clear();
            blockNames.Clear();
            stringTypes.Clear();
            LV_NifDataList_Active(false);
            LV_BlockNameList_Active(false);
            LV_StringTypeList_Active(false);
            MI_Macro_Active(false);
            ProgressBarInitial();

            double mainStep = ProgressBarMaximum() / stepSub;
            double step = mainStep / meshes.Count;
            ConcurrentBag<string> failFiles = new ConcurrentBag<string>();
            ConcurrentBag<NifData> newNifDatas = new ConcurrentBag<NifData>();
            Parallel.ForEach(meshes, mesh =>
            {
                Logger.Log.Info("reading " + mesh + " file...");
                if (!LoadNifFile(newNifDatas, mesh))
                {
                    Logger.Log.Error("Unable to read : " + mesh);
                    failFiles.Add(mesh);
                }
                ProgressBarStep(step);
            });
            ConcurrentBag<NifData> newNifDatas_Facegen = new ConcurrentBag<NifData>();
            Parallel.ForEach(newNifDatas, data =>
            {
                if (data.isFacegenMesh)
                    newNifDatas_Facegen.Add(data);
            });
            nifDatas_Facegen.AddRange(newNifDatas_Facegen);
            nifDatas.AddRange(newNifDatas.Except(newNifDatas_Facegen));
            LV_NifDataList_Update();

            if (failFiles.Count > 0)
            {
                string wrongFiles = "";
                bool first = true;
                foreach (var file in failFiles)
                {
                    if (!first)
                        wrongFiles += "\n";
                    wrongFiles += file;
                    first = false;
                }
                System.Windows.MessageBox.Show(wrongFiles, "ERROR : Failed to read files");
            }
            SortingNifData();
            ProgressBarStep(mainStep);
            LV_NifDataList_Update();
            LV_NifDataList_Active();

            InitialCategories();

            ProgressBarDone();
            MI_Macro_Active();
        }

        NifLoadOptions loadOptions = new NifLoadOptions() { isTerrain = false };
        private bool LoadNifFile(ConcurrentBag<NifData> newNifDatas, string path)
        {
            if (!Util.CanRead(path))
                return false;

            NifFile nifFile = new NifFile(true);
            if (nifFile.Load(path, loadOptions) != 0)
                return false;

            Parallel.ForEach(nifFile.GetNodes(), node =>
            {
                GetNodeData(newNifDatas, nifFile, path, node);
            });
            Parallel.ForEach(nifFile.GetShapes(), node =>
            {
                GetShapeData(newNifDatas, nifFile, path, node);
            });
            return true;
        }

        private void GetNodeData(ConcurrentBag<NifData> newNifDatas, NifFile nifFile, string path, NiAVObject obj)
        {
            if (nifFile == null || obj == null)
                return;

            NifData nifData = new NifData() { IsChecked = true, IsSelected = false };
            nifData.nifFile = nifFile;
            nifData.path = path;
            nifData.obj = obj;
            nifData.blockName = obj.GetBlockName();
            nifData.stringType = _StringType.Name;
            nifData.strBefore = obj.name.get();
            nifData.strAfter = nifData.strBefore;
            nifData.strBeforeDisplay = MakeDisplayName(nifData);
            nifData.strAfterDisplay = nifData.strBeforeDisplay;
            nifData.ToolTip = nifData.path;
            nifData.isFacegenMesh = Util.IsFacegenMesh(nifData.path);
            newNifDatas.Add(nifData);

            GetNiStringExtraData(newNifDatas, nifFile, path, obj);
        }
        private void GetShapeData(ConcurrentBag<NifData> newNifDatas, NifFile nifFile, string path, NiAVObject obj)
        {
            if (nifFile == null || obj == null)
                return;

            NifData nifData = new NifData() { IsChecked = true, IsSelected = false };
            nifData.nifFile = nifFile;
            nifData.path = path;
            nifData.obj = obj;
            nifData.blockName = obj.GetBlockName();
            nifData.stringType = _StringType.Name;
            nifData.strBefore = obj.name.get();
            nifData.strAfter = nifData.strBefore;
            nifData.strBeforeDisplay = MakeDisplayName(nifData);
            nifData.strAfterDisplay = nifData.strBeforeDisplay;
            nifData.ToolTip = nifData.path;
            nifData.isFacegenMesh = Util.IsFacegenMesh(nifData.path);
            newNifDatas.Add(nifData);

            GetNiStringExtraData(newNifDatas, nifFile, path, obj);
            GetTextureData(newNifDatas, nifFile, path, obj);
        }
        private void GetTextureData(ConcurrentBag<NifData> newNifDatas, NifFile nifFile, string path, NiAVObject obj)
        {
            if (nifFile == null || obj == null)
                return;

            string blockName = obj.GetBlockName();
            if (blockName != "BSDynamicTriShape" && blockName != "BSTriShape")
                return;

            NiShape shape = obj as NiShape;
            if (shape == null)
                return;

            for (uint texindex = 0; texindex < 9; texindex++)
            {
                string texture = nifFile.GetTexturePathByIndex(shape, texindex);
                //nifFile.GetTextureSlot(shape, texture, texindex);

                NifData nifData = new NifData() { IsChecked = true, IsSelected = false };
                nifData.nifFile = nifFile;
                nifData.path = path;
                nifData.obj = obj;
                nifData.blockName = "BSShaderTextureSet";
                nifData.stringType = (_StringType)texindex + (int)_StringType.Texture + 1;
                nifData.textureIndex = (int)texindex;
                nifData.strBefore = texture;
                nifData.strAfter = nifData.strBefore;
                nifData.strBeforeDisplay = MakeDisplayName(nifData);
                nifData.strAfterDisplay = nifData.strBeforeDisplay;
                nifData.ToolTip = nifData.path;
                nifData.isFacegenMesh = Util.IsFacegenMesh(nifData.path);
                newNifDatas.Add(nifData);
            }
        }
        private void GetNiStringExtraData(ConcurrentBag<NifData> newNifDatas, NifFile nifFile, string path, NiAVObject obj)
        {
            if (nifFile == null || obj == null)
                return;

            foreach (var extra in obj.extraDataRefs.GetRefs())
            {
                NiExtraData extraData = nifFile.GetHeader().GetBlockById(extra.index) as NiExtraData;
                if (extraData == null)
                    continue;

                NifData nifData = new NifData() { IsChecked = true, IsSelected = false };
                nifData.nifFile = nifFile;
                nifData.path = path;
                nifData.obj = extraData;
                nifData.blockName = extraData.GetBlockName();
                nifData.stringType = _StringType.ExtraDataName;
                nifData.strBefore = extraData.name.get();
                nifData.strAfter = nifData.strBefore;
                nifData.strBeforeDisplay = MakeDisplayName(nifData);
                nifData.strAfterDisplay = nifData.strBeforeDisplay;
                nifData.ToolTip = nifData.path;
                nifData.isFacegenMesh = Util.IsFacegenMesh(nifData.path);
                newNifDatas.Add(nifData);

                NiStringExtraData stringExtraData = extraData as NiStringExtraData;
                if (stringExtraData == null)
                    continue;

                NifData nifDataAlt = new NifData() { IsChecked = true, IsSelected = false };
                nifDataAlt.nifFile = nifFile;
                nifDataAlt.path = path;
                nifDataAlt.obj = stringExtraData;
                nifDataAlt.blockName = stringExtraData.GetBlockName();
                nifDataAlt.stringType = _StringType.ExtraData;
                nifDataAlt.strBefore = stringExtraData.stringData.get();
                nifDataAlt.strAfter = nifDataAlt.strBefore;
                nifDataAlt.strBeforeDisplay = MakeDisplayName(nifDataAlt);
                nifDataAlt.strAfterDisplay = nifDataAlt.strBeforeDisplay;
                nifDataAlt.ToolTip = nifDataAlt.path;
                nifDataAlt.isFacegenMesh = Util.IsFacegenMesh(nifDataAlt.path);
                newNifDatas.Add(nifDataAlt);
                //NiStringsExtraData stringsExtraData = extraData as NiStringsExtraData;
            }
        }

        private void SortingNifData()
        {
            int count = 0;
            nifDatas.Sort((x, y) =>
            {
                var result = x.path.CompareTo(y.path);
                if (result == 0)
                {
                    result = x.stringType.CompareTo(y.stringType);
                    if (result == 0)
                    {
                        result = x.textureIndex.CompareTo(y.textureIndex);
                        if (result == 0)
                        {
                            result = x.strBefore.CompareTo(y.strBefore);
                        }
                    }
                }
                if (count < 1000)
                    count++;
                else
                {
                    count = 0;
                    LV_NifDataList_Update();
                }
                return result;
            });
        }

        private void InitialCategories()
        {
            double step = ProgressBarMaximum() / stepSub / nifDatas.Count;

            blockNames.Clear();
            stringTypes.Clear();
            foreach (var item in nifDatas)
            {
                bool blockUpdate = false;
                bool typeUpdate = false;

                int blockIndex = blockNames.FindIndex(x => x.blockName == item.blockName);
                if (blockIndex == -1)
                {
                    var newBlock = new BlockName()
                    {
                        blockName = item.blockName,
                        FromStringTypes = new HashSet<_StringType>(),
                        IsChecked = true,
                        IsSelected = false,
                        IsEnabled = true,
                        Foreground = System.Windows.Media.Brushes.Black
                    };
                    newBlock.FromStringTypes.Add(item.stringType);
                    blockNames.Add(newBlock);
                }
                else
                    blockNames[blockIndex].FromStringTypes.Add(item.stringType);

                int typeIndex = stringTypes.FindIndex(x => x.stringType == item.stringType);
                if (typeIndex == -1)
                {
                    stringTypes.Add(new StringType()
                    {
                        stringType = item.stringType,
                        FromBlockNames = new HashSet<string>() { item.blockName },
                        IsChecked = true,
                        IsSelected = false,
                        IsEnabled = true,
                        Foreground = System.Windows.Media.Brushes.Black
                    });
                }
                else
                    stringTypes[typeIndex].FromBlockNames.Add(item.blockName);
                ProgressBarStep(step);

                if (blockUpdate)
                    LV_BlockNameList_Update();
                if (typeUpdate)
                    LV_StringTypeList_Update();
            }
            blockNames.Sort((x, y) => x.blockName.CompareTo(y.blockName));
            stringTypes.Sort((x, y) => x.stringType.CompareTo(y.stringType));

            LV_BlockNameList_Update();
            LV_StringTypeList_Update();
            LV_BlockNameList_Active();
            LV_StringTypeList_Active();
        }

        private string MakeDisplayName(NifData nifData)
        {
            _StringTypeShort altType = (_StringTypeShort)nifData.stringType;
            return "[" + altType.ToString() + "] " + nifData.strAfter;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Config.GetSingleton.SetNifManager_Size(this.Height, this.Width);
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
        private async void ProgressBarStep(double step = 1)
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
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                PB_Loading.Value = ProgressBarValue;
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
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
            LoadNifFiles();
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
            Parallel.ForEach(editedNifDatas, nif =>
            {
                if (!Util.CanWrite(nif.Value.path))
                {
                    failFiles.Add(nif.Value.path);
                    Logger.Log.Fatal("Unable to access the file : " + nif.Value.path);
                }
                else
                {
                    if (fileBackup.IsChecked)
                        NifBackup(nif.Value.path);
                    nif.Value.nifFile.Save(nif.Value.path);
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
                System.Windows.MessageBox.Show(failFileList, "ERROR : Unable to move the file!");
            }
            if (!MacroMode)
                System.Windows.MessageBox.Show("Save done!");
            LoadNifFiles();
        }
        private void NifBackup(string path)
        {
            string targetBase = path + ".backup";
            string target = targetBase;
            UInt64 num = 0;
            while (System.IO.File.Exists(target))
            {
                target = targetBase + num;
                num++;
            }
            System.IO.File.Copy(path, target, true);
            Logger.Log.Info("Backup file... : " + path);
        }
        private void MI_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
                if (meshes.Count == 0)
                {
                    BT_Apply.IsEnabled = false;
                    return;
                }

                if (CB_AddTextType.SelectedIndex > 0 && TB_AddText.Text.Length > 0)
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
            if (nifDatas.Count == 0)
            {
                BT_Apply_Update();
                return;
            }
            BT_Apply_Active(false);

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
        private void Apply_AddText(AddTextType adType, string addText)
        {
            LV_NifDataList_Active(false);
            char IsValidPath = Util.IsValidPath(addText);
            bool isInvalidEdit = false;
            switch (adType)
            {
                case AddTextType.AddPrefix:
                    {
                        Parallel.ForEach(nifDatas, data =>
                        {
                            if (data.IsChecked)
                            {
                                if (IsValidPath == '0' || data.textureIndex == -1)
                                {
                                    data.strAfter = addText + data.strAfter;
                                    data.strAfterDisplay = MakeDisplayName(data);
                                    editedNifDatas.AddOrUpdate(data.path, data, (Key, oldValue) => data);
                                    ApplyNifFile(data);
                                }
                                else
                                    isInvalidEdit = true;
                            }
                        });
                        break;
                    }
                case AddTextType.AddSuffix:
                    {
                        Parallel.ForEach(nifDatas, data =>
                        {
                            if (data.IsChecked)
                            {
                                if (IsValidPath == '0' || data.textureIndex == -1)
                                {
                                    data.strAfter = data.strAfter + addText;
                                    data.strAfterDisplay = MakeDisplayName(data);
                                    editedNifDatas.AddOrUpdate(data.path, data, (Key, oldValue) => data);
                                    ApplyNifFile(data);
                                }
                                else
                                    isInvalidEdit = true;
                            }
                        });
                        break;
                    }
            }
            if (isInvalidEdit)
                System.Windows.MessageBox.Show("Contains invalid character <" + isInvalidEdit + "> for texture path!");
            LV_NifDataList_Update();
            LV_NifDataList_Active();
        }
        private void Apply_Replace(string search, string result)
        {
            LV_NifDataList_Active(false);
            char IsValidPath = Util.IsValidPath(result);
            bool isInvalidEdit = false;
            Parallel.ForEach(nifDatas, data =>
            {
                if (data.IsChecked && data.strAfter != null && data.strAfter.Length > 0)
                {
                    if (IsValidPath == '0' || data.textureIndex == -1)
                    {
                        string strAftertemp = data.strAfter;
                        data.strAfter = Util.Replace(data.strAfter, search, result, matchCase.IsChecked);
                        data.strAfterDisplay = MakeDisplayName(data);

                        if (strAftertemp != data.strAfter)
                            editedNifDatas.AddOrUpdate(data.path, data, (Key, oldValue) => data);
                        ApplyNifFile(data);
                    }
                    else
                        isInvalidEdit = true;
                }
            });
            if (isInvalidEdit)
                System.Windows.MessageBox.Show("Contains invalid character <" + IsValidPath + "> for texture path!");
            LV_NifDataList_Update();
            LV_NifDataList_Active();
        }

        private async Task ApplyActiveDelay()
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

        private void ApplyNifFile(NifData data)
        {
            if (data.textureIndex > -1) //no texture
            {
                data.nifFile.SetTextureSlot(data.obj as NiShape, data.strAfter, (uint)data.textureIndex);
            }
            else if (data.stringType == _StringType.Name)
            {
                (data.obj as NiAVObject).name = new NiStringRef(data.strAfter);
            }
            else if (data.stringType == _StringType.ExtraDataName)
            {
                (data.obj as NiExtraData).name = new NiStringRef(data.strAfter);
            }
            else if (data.stringType == _StringType.ExtraData)
            {
                (data.obj as NiStringExtraData).stringData = new NiStringRef(data.strAfter);
            }
        }


        private void CB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BT_Apply_Update();
        }

        private void TB_TextChanged(object sender, TextChangedEventArgs e)
        {
            BT_Apply_Update();
        }

        private void MI_Macro_Click(object sender, RoutedEventArgs e)
        {
            string file = Util.GetMacroFile();
            if (file == "")
                return;
            Macro_Load(file, false);
        }
        public void Macro_Load(string file, bool endClose)
        {
            bool isFileEdit = false;
            bool isSave = false;

            BT_Apply_Active(false);
            LV_NifDataList_Active(false);
            LV_StringTypeList_Active(false);
            LV_BlockNameList_Active(false);
            MI_Reset_Active(false);
            MI_Save_Active(false);
            MI_Macro_Active(false);
            MacroMode = true;

            foreach (var line in System.IO.File.ReadLines(file))
            {
                if (line == "PLUGINEDIT" || line == "FILEEDIT")
                    isFileEdit = false;
                else if (line == "NIFEDIT")
                    isFileEdit = true;

                if (!isFileEdit)
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
                    if (m2 == "BLOCKNAME")
                    {
                        if (m3 == "CHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                foreach (var item in blockNames)
                                {
                                    item.IsChecked = true;
                                    CB_BlockNames_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in blockNames)
                                {
                                    if (Util.IsSameStringIgnoreCase(item.blockName, m4))
                                    {
                                        item.IsChecked = true;
                                        CB_BlockNames_CheckUncheck(item, new RoutedEventArgs());
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
                                foreach (var item in blockNames)
                                {
                                    item.IsChecked = false;
                                    CB_BlockNames_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in blockNames)
                                {
                                    if (Util.IsSameStringIgnoreCase(item.blockName, m4))
                                    {
                                        item.IsChecked = false;
                                        CB_BlockNames_CheckUncheck(item, new RoutedEventArgs());
                                    }
                                }
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            foreach (var item in blockNames)
                            {
                                item.IsChecked = !item.IsChecked;
                                CB_BlockNames_CheckUncheck(item, new RoutedEventArgs());
                            }
                        }
                    }
                    else if (m2 == "STRINGTYPE")
                    {
                        if (m3 == "CHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                foreach (var item in stringTypes)
                                {
                                    item.IsChecked = true;
                                    CB_StringTypes_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in stringTypes)
                                {
                                    if (Util.IsSameStringIgnoreCase(item.stringType.ToString(), m4))
                                    {
                                        item.IsChecked = true;
                                        CB_StringTypes_CheckUncheck(item, new RoutedEventArgs());
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
                                foreach (var item in stringTypes)
                                {
                                    item.IsChecked = false;
                                    CB_StringTypes_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in stringTypes)
                                {
                                    if (Util.IsSameStringIgnoreCase(item.stringType.ToString(), m4))
                                    {
                                        item.IsChecked = false;
                                        CB_StringTypes_CheckUncheck(item, new RoutedEventArgs());
                                    }
                                }
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            foreach (var item in stringTypes)
                            {
                                item.IsChecked = !item.IsChecked;
                                CB_StringTypes_CheckUncheck(item, new RoutedEventArgs());
                            }
                        }
                    }
                    else if (m2 == "NIFDATALIST")
                    {
                        if (m3 == "CHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                Parallel.ForEach(nifDatas, item =>
                                {
                                    item.IsChecked = true;
                                });
                            }
                            else
                            {
                                Parallel.ForEach(nifDatas, item =>
                                {
                                    if (item.strAfter.Contains(m4, StringComparison.OrdinalIgnoreCase))
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
                                Parallel.ForEach(nifDatas, item =>
                                {
                                    item.IsChecked = false;
                                });
                            }
                            else
                            {
                                Parallel.ForEach(nifDatas, item =>
                                {
                                    if (item.strAfter.Contains(m4, StringComparison.OrdinalIgnoreCase))
                                        item.IsChecked = false;
                                });
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            Parallel.ForEach(nifDatas, item =>
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
                    else if (m2 == "FACEGENEDIT")
                    {
                        if (m3 == "CHECK")
                        {
                            FacegenEdit.IsChecked = true;
                            if (!this.IsLoaded)
                                MI_FacegenEdit_CheckUncheck(new object(), new RoutedEventArgs());
                        }
                        else if (m3 == "UNCHECK")
                        {
                            FacegenEdit.IsChecked = false;
                            if (!this.IsLoaded)
                                MI_FacegenEdit_CheckUncheck(new object(), new RoutedEventArgs());
                        }
                    }
                }
                else if (m1 == "APPLY")
                {
                    if (macro.Length < 3)
                        continue;
                    var m2 = macro[1];
                    var m3 = macro[2];

                    if (m2 == "ADDPREFIX")
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
                SortingNifData();
                LV_NifDataList_Update();
                LV_BlockNameList_Update();
                LV_StringTypeList_Update();
            }

            BT_Apply_Update();
            LV_NifDataList_Active();
            LV_BlockNameList_Active();
            LV_StringTypeList_Active();
            MI_Reset_Active();
            MI_Save_Active();
            MI_Macro_Active();

            MacroMode = false;
            if (endClose && isSave)
                this.Close();
            else if (!endClose)
                System.Windows.MessageBox.Show("Macro loaded");
        }

        private async Task LV_BlockNameList_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    LV_BlockNameList.ItemsSource = blockNames;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(blockNames);
                    view.Refresh();
                }
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
        }
        private void LV_BlockNameList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_BlockNameList.IsEnabled = Active;
            }));
        }
        private async Task LV_StringTypeList_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    LV_StringTypeList.ItemsSource = stringTypes;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(stringTypes);
                    view.Refresh();
                }
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
        }
        private void LV_StringTypeList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_StringTypeList.IsEnabled = Active;
            }));
        }

        private async Task LV_NifDataList_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    LV_NifDataListBefore.ItemsSource = nifDatas;
                    LV_NifDataListAfter.ItemsSource = nifDatas;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(nifDatas);
                    view.Refresh();
                }
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
        }
        private void LV_NifDataList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_NifDataListBefore.IsEnabled = Active;
                LV_NifDataListAfter.IsEnabled = Active;
            }));
        }
        private void LV_Check_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            ContextMenu cm = Util.FindParent<ContextMenu>(mi);
            if (cm == null)
                return;
            if (cm == LV_NifDataListBefore.ContextMenu || cm == LV_NifDataListAfter.ContextMenu)
            {
                Parallel.ForEach(nifDatas, data =>
                {
                    if (data.IsSelected)
                        data.IsChecked = true;
                });
                LV_NifDataList_Update();
            }
            else if (cm == LV_BlockNameList.ContextMenu)
            {
                foreach (var name in blockNames)
                {
                    if (name.IsSelected)
                        name.IsChecked = true;
                }
                LV_BlockNameList_Update();
            }
            else if (cm == LV_StringTypeList.ContextMenu)
            {
                foreach (var type in stringTypes)
                {
                    if (type.IsSelected)
                        type.IsChecked = true;
                }
                LV_StringTypeList_Update();
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
            if (cm == LV_NifDataListBefore.ContextMenu || cm == LV_NifDataListAfter.ContextMenu)
            {
                Parallel.ForEach(nifDatas, data =>
                {
                    if (data.IsSelected)
                        data.IsChecked = false;
                });
                LV_NifDataList_Update();
            }
            else if (cm == LV_BlockNameList.ContextMenu)
            {
                foreach (var name in blockNames)
                {
                    if (name.IsSelected)
                        name.IsChecked = false;
                }
                LV_BlockNameList_Update();
            }
            else if (cm == LV_StringTypeList.ContextMenu)
            {
                foreach (var type in stringTypes)
                {
                    if (type.IsSelected)
                        type.IsChecked = false;
                }
                LV_StringTypeList_Update();
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
            if (cm == LV_NifDataListBefore.ContextMenu || cm == LV_NifDataListAfter.ContextMenu)
            {
                Parallel.ForEach(nifDatas, data =>
                {
                    if (data.IsSelected)
                        data.IsChecked = !data.IsChecked;
                });
                LV_NifDataList_Update();
            }
            else if (cm == LV_BlockNameList.ContextMenu)
            {
                foreach (var name in blockNames)
                {
                    if (name.IsSelected)
                        name.IsChecked = !name.IsChecked;
                }
                LV_BlockNameList_Update();
            }
            else if (cm == LV_StringTypeList.ContextMenu)
            {
                foreach (var type in stringTypes)
                {
                    if (type.IsSelected)
                        type.IsChecked = !type.IsChecked;
                }
                LV_StringTypeList_Update();
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
            if (cm == LV_NifDataListBefore.ContextMenu || cm == LV_NifDataListAfter.ContextMenu)
            {
                Parallel.ForEach(nifDatas, data =>
                {
                    data.IsChecked = true;
                });
                LV_NifDataList_Update();
            }
            else if (cm == LV_BlockNameList.ContextMenu)
            {
                foreach (var name in blockNames)
                {
                    name.IsChecked = true;
                }
                LV_BlockNameList_Update();
            }
            else if (cm == LV_StringTypeList.ContextMenu)
            {
                foreach (var type in stringTypes)
                {
                    type.IsChecked = true;
                }
                LV_StringTypeList_Update();
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
            if (cm == LV_NifDataListBefore.ContextMenu || cm == LV_NifDataListAfter.ContextMenu)
            {
                Parallel.ForEach(nifDatas, data =>
                {
                    data.IsChecked = false;
                });
                LV_NifDataList_Update();
            }
            else if (cm == LV_BlockNameList.ContextMenu)
            {
                foreach (var name in blockNames)
                {
                    name.IsChecked = false;
                }
                LV_BlockNameList_Update();
            }
            else if (cm == LV_StringTypeList.ContextMenu)
            {
                foreach (var type in stringTypes)
                {
                    type.IsChecked = false;
                }
                LV_StringTypeList_Update();
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
            if (cm == LV_NifDataListBefore.ContextMenu || cm == LV_NifDataListAfter.ContextMenu)
            {
                Parallel.ForEach(nifDatas, data =>
                {
                    data.IsChecked = !data.IsChecked;
                });
                LV_NifDataList_Update();
            }
            else if (cm == LV_BlockNameList.ContextMenu)
            {
                foreach (var name in blockNames)
                {
                    name.IsChecked = !name.IsChecked;
                }
                LV_BlockNameList_Update();
            }
            else if (cm == LV_StringTypeList.ContextMenu)
            {
                foreach (var type in stringTypes)
                {
                    type.IsChecked = !type.IsChecked;
                }
                LV_StringTypeList_Update();
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
            if (lv == LV_NifDataListBefore)
            {
                GVC_NifDataListBefore.Width = GVC_NifDataListBefore.Width * Ratio;
            }
            else if (lv == LV_NifDataListAfter)
            {
                GVC_NifDataListAfter.Width = GVC_NifDataListAfter.Width * Ratio;
            }
            else if (lv == LV_BlockNameList)
            {
                GVC_BlockNames.Width = GVC_BlockNames.Width * Ratio;
            }
            else if (lv == LV_StringTypeList)
            {
                GVC_StringTypes.Width = GVC_StringTypes.Width * Ratio;
            }
        }

        private void SizeChangeAll(double Ratio)
        {
            GVC_NifDataListBefore.Width *= Ratio;
            GVC_NifDataListAfter.Width *= Ratio;
            GVC_BlockNames.Width *= Ratio;
            GVC_StringTypes.Width *= Ratio;
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

        private void CB_BlockNames_CheckUncheck(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            BlockName blockName = sender as BlockName;
            if (cb != null)
            {
                BlockName data = cb.DataContext as BlockName;
                if (data == null)
                {
                    Logger.Log.Error("Couldn't get block name checkbox data");
                    return;
                }
                NifDataToggleData toggleData = new NifDataToggleData()
                {
                    blockName = data.blockName,
                    stringType = _StringType.None
                };
                InactiveListUpdate(toggleData, data.IsChecked);
                StringTypeUpdate();
            }
            else if (blockName != null && !this.IsLoaded)
            {
                NifDataToggleData toggleData = new NifDataToggleData()
                {
                    blockName = blockName.blockName,
                    stringType = _StringType.None
                };
                InactiveListUpdate(toggleData, blockName.IsChecked);
                StringTypeUpdate();
            }
        }

        private void CB_StringTypes_CheckUncheck(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            StringType stringType = sender as StringType;
            if (cb != null)
            {
                StringType data = cb.DataContext as StringType;
                if (data == null)
                {
                    Logger.Log.Error("Couldn't get block name checkbox data");
                    return;
                }
                NifDataToggleData toggleData = new NifDataToggleData()
                {
                    blockName = "0000",
                    stringType = data.stringType
                };
                InactiveListUpdate(toggleData, data.IsChecked);
                BlockNameUpdate();
            }
            else if (stringType != null && !this.IsLoaded)
            {
                NifDataToggleData toggleData = new NifDataToggleData()
                {
                    blockName = "0000",
                    stringType = stringType.stringType
                };
                InactiveListUpdate(toggleData, stringType.IsChecked);
                BlockNameUpdate();
            }
        }

        private void InactiveListUpdate(NifDataToggleData data, bool IsChecked)
        {
            if (IsChecked)
            {
                int index = inactiveList.FindIndex(x => x.blockName == data.blockName && x.stringType == data.stringType);
                if (index != -1)
                {
                    inactiveList.RemoveAt(index);
                }
            }
            else if (inactiveList.FindIndex(x => x.blockName == data.blockName && x.stringType == data.stringType) == -1)
            {
                inactiveList.Add(data);
            }
            NifDataListUpdate(IsChecked);
        }
        private void BlockNameUpdate()
        {
            foreach (var item in blockNames)
            {
                if (stringTypes.FindIndex(x => x.IsChecked && item.FromStringTypes.Contains(x.stringType)) != -1)
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
            LV_BlockNameList_Update();
        }
        private void StringTypeUpdate()
        {
            foreach (var item in stringTypes)
            {
                if (blockNames.FindIndex(x => x.IsChecked && item.FromBlockNames.Contains(x.blockName)) != -1)
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
            LV_StringTypeList_Update();
        }

        private void NifDataListUpdate(bool IsChecked)
        {
            LV_NifDataList_Active(false);
            List<NifData> found = new List<NifData>();
            if (IsChecked)
            {
                found.AddRange(nifDatasDisable.FindAll(x =>
                    inactiveList.FindIndex(y =>
                        (y.blockName != "0000" ? x.blockName == y.blockName : true)
                        && (y.stringType != _StringType.None ? x.stringType == y.stringType : true)
                        ) == -1
                    ));
                nifDatas.AddRange(found);
                found.ForEach(x => nifDatasDisable.Remove(x));
            }
            else
            {
                found.AddRange(nifDatas.FindAll(x =>
                    inactiveList.FindIndex(y =>
                        (y.blockName != "0000" ? x.blockName == y.blockName : true)
                        && (y.stringType != _StringType.None ? x.stringType == y.stringType : true)
                    ) != -1
                ));
                nifDatasDisable.AddRange(found);
                found.ForEach(x => nifDatas.Remove(x));
            }
            SortingNifData();
            LV_NifDataList_Update();
            LV_NifDataList_Active(true);
        }

        private void SaveStateOnConfig(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (cb != null)
            {
                if (cb == CB_MatchCase)
                    Config.GetSingleton.SetNifManager_MatchCase(CB_MatchCase.IsChecked ?? false);
            }
            MenuItem mi = sender as MenuItem;
            if (mi != null)
            {
                if (mi == MI_FileBackup)
                    Config.GetSingleton.SetNifManager_FileBackup(mi.IsChecked);
            }
        }

        private void LV_NifDataList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv == null)
                return;
            ListView lv1 = null;
            ListView lv2 = null;
            if (lv == LV_NifDataListBefore)
            {
                lv1 = LV_NifDataListBefore;
                lv2 = LV_NifDataListAfter;
            }
            else if (lv == LV_NifDataListAfter)
            {
                lv1 = LV_NifDataListAfter;
                lv2 = LV_NifDataListBefore;
            }
            Decorator border1 = VisualTreeHelper.GetChild(lv1, 0) as Decorator;
            Decorator border2 = VisualTreeHelper.GetChild(lv2, 0) as Decorator;
            ScrollViewer scrollViewer1 = border1.Child as ScrollViewer;
            ScrollViewer scrollViewer2 = border2.Child as ScrollViewer;
            scrollViewer2.ScrollToVerticalOffset(scrollViewer1.VerticalOffset);
            scrollViewer2.ScrollToHorizontalOffset(scrollViewer1.HorizontalOffset);
        }

        private void MI_FileBackup_CheckUncheck(object sender, RoutedEventArgs e)
        {
            Config.GetSingleton.SetNifManager_FileBackup(fileBackup.IsChecked);
        }

        private void MI_FacegenEdit_CheckUncheck(object sender, RoutedEventArgs e)
        {
            if (FacegenEdit.IsChecked)
            {
                if (nifDatas_Temp.Count > 0)
                    return;
                nifDatas_Temp.AddRange(nifDatas);
                nifDatas_Temp.AddRange(nifDatasDisable);
                nifDatas.Clear();
                nifDatasDisable.Clear();
                nifDatas.AddRange(nifDatas_Facegen);
                nifDatas_Facegen.Clear();
            }
            else
            {
                if (nifDatas_Facegen.Count > 0)
                    return;
                nifDatas_Facegen.AddRange(nifDatas);
                nifDatas_Facegen.AddRange(nifDatasDisable);
                nifDatas.Clear();
                nifDatasDisable.Clear();
                nifDatas.AddRange(nifDatas_Temp);
                nifDatas_Temp.Clear();
            }
            inactiveList.Clear();
            InitialCategories();
            NifDataListUpdate(false);
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
            if (lv == LV_NifDataListBefore || lv == LV_NifDataListAfter)
            {
                foreach (var data in nifDatas)
                {
                    if (data.IsSelected)
                        data.IsChecked = !data.IsChecked;
                }
                LV_NifDataList_Update();
            }
            else if (lv == LV_BlockNameList)
            {
                foreach (var name in blockNames)
                {
                    if (name.IsSelected)
                        name.IsChecked = !name.IsChecked;
                }
                LV_BlockNameList_Update();
            }
            else if (lv == LV_StringTypeList)
            {
                foreach (var type in stringTypes)
                {
                    if (type.IsSelected)
                        type.IsChecked = !type.IsChecked;
                }
                LV_StringTypeList_Update();
            }
        }

        private void MI_Macro_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_Macro.IsEnabled = Active;
            }));
        }

        private void FileOrFolderDrop(object sender, DragEventArgs e)
        {
            if (e == null)
                return;
            string[] FileOrFolder = e.Data.GetData(DataFormats.FileDrop) as string[];
            e.Handled = true;

            foreach (var macro in FileOrFolder.ToList().FindAll(x => Util.IsMacroFile(x) && File.Exists(x)))
            {
                if (System.Windows.MessageBox.Show("Do you want to load the macro file?\n" + System.IO.Path.GetFileName(macro),
                    "Macro Load", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    Macro_Load(macro, false);
            }

            bool isUpdateFileList = false;
            var folders = FileOrFolder.ToList().FindAll(x => Directory.Exists(x));
            if (folders.Count > 0)
            {
                selectedFolders = folders;
                meshes = Util.GetAllFilesFromFolders(selectedFolders, SearchOption.AllDirectories).FindAll(x => Util.IsNifFile(x));
                isUpdateFileList = true;
            }
            var files = FileOrFolder.ToList().FindAll(x => File.Exists(x) && Util.IsNifFile(x));
            if (files.Count > 0)
            {
                if (isUpdateFileList)
                {
                    meshes.AddRange(files);
                }
                else
                {
                    selectedFolders.Clear();
                    meshes = files;
                }
                isUpdateFileList = true;
            }

            if (isUpdateFileList)
                LoadNifFiles();
        }
    }
    public class NifDataToggleData
    {
        public string blockName { get; set; }
        public _StringType stringType { get; set; }
    }

    public enum _StringType : short
    {
        None = -1,
        Name,
        ExtraDataName,
        ExtraData,

        Texture = 10,
        Diffuse,
        Normal,
        //Gloss = Normal,
        EnvironmentMask,
        //Subsurface = EnvironmentMask,
        DetailMap,
        //Glow = DetailMap,
        Height,
        Environment,
        Multilayer,
        Specular,
        //BackLight = Specular,
        Decal,

        Total
    }
    public enum _StringTypeShort : short
    {
        None = -1,
        Name,
        ExName,
        ExData,

        Texture = 10,
        Diffuse,
        Normal,
        EnvMask,
        Detail,
        Height,
        Env,
        Multi,
        Specular,
        Decal,

        Total
    }
    public class NifData : INotifyPropertyChanged
    {
        public NifFile nifFile = null;
        public string path = "";
        public NiObject obj = null;
        public string blockName = "";
        public _StringType stringType = _StringType.None;
        public int textureIndex = -1;

        public bool isFacegenMesh = false;

        public string strBefore { get; set; }
        private string _strBeforeDisplay;
        public string strBeforeDisplay
        {
            get { return _strBeforeDisplay; }
            set
            {
                _strBeforeDisplay = value;
                OnPropertyChanged("strBeforeDisplay");
            }
        }
        public string strAfter { get; set; }
        private string _strAfterDisplay;
        public string strAfterDisplay
        {
            get { return _strAfterDisplay; }
            set
            {
                _strAfterDisplay = value;
                OnPropertyChanged("strAfterDisplay");
            }
        }
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
    public class BlockName : INotifyPropertyChanged
    {
        private string _blockName;
        public string blockName
        {
            get { return _blockName; }
            set
            {
                _blockName = value;
                OnPropertyChanged("blockName");
            }
        }

        public HashSet<_StringType> FromStringTypes { get; set; }

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

    public class StringType : INotifyPropertyChanged
    {
        private _StringType _stringType;
        public _StringType stringType
        {
            get { return _stringType; }
            set
            {
                _stringType = value;
                OnPropertyChanged("stringType");
            }
        }

        public HashSet<string> FromBlockNames { get; set; }

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
