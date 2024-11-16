using log4net.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

#pragma warning disable CS4014 // 이 호출을 대기하지 않으므로 호출이 완료되기 전에 현재 메서드가 계속 실행됩니다.

namespace SkyrimPluginTextEditor
{
    /// <summary>
    /// MoveFile.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class FileManager : Window
    {
        private List<string> selectedFolders = new List<string>();
        private List<string> selectedFiles = new List<string>();
        private List<MoveFileSet> files = new List<MoveFileSet>();
        private List<MoveFileSet> nonSkyrimFiles = new List<MoveFileSet>();
        private List<MoveFileSet> filesDisable = new List<MoveFileSet>();
        private ConcurrentDictionary<string, MoveFileSet> filesEdited = new ConcurrentDictionary<string, MoveFileSet>();
        private List<Extensions> extensionList = new List<Extensions>();
        private bool isContentEdited = false;
        private CheckBoxBinder matchCase = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetFileManager_MatchCase() };
        private bool initialDone = false;
        private FileContent fileContent = new FileContent() { IsChecked = Config.GetSingleton.GetFileManager_FileContent() };
        private NonSkyrimFile nonSkyrimFile = new NonSkyrimFile() { IsChecked = Config.GetSingleton.GetFileManager_NonSkyrimFile() };
        private CheckBoxBinder fileOverwrite = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetFileManager_FileOverwrite() };
        private CheckBoxBinder clearSubFolder = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetFileManager_ClearEmptySubFolder() };
        private CheckBoxBinder nonSkyrimFileCheck = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetFileManager_NonSkyrimFile() };
        private CheckBoxBinder facegenFolderEdit = new CheckBoxBinder() { IsChecked = Config.GetSingleton.GetFileManager_FacegenFolderEdit() };
        private bool MacroMode = false;

        public FileManager()
        {
            InitializeComponent();
            SizeChangeAll(Config.GetSingleton.GetFileManager_Width() / this.Width);
            this.Height = Config.GetSingleton.GetFileManager_Height();
            this.Width = Config.GetSingleton.GetFileManager_Width();
            fileOverwrite.IsChecked = Config.GetSingleton.GetFileManager_FileOverwrite();
            clearSubFolder.IsChecked = Config.GetSingleton.GetFileManager_ClearEmptySubFolder();
            nonSkyrimFileCheck.IsChecked = Config.GetSingleton.GetFileManager_NonSkyrimFile();
            matchCase.IsChecked = Config.GetSingleton.GetFileManager_MatchCase();
            MI_FileOverwrite.DataContext = fileOverwrite;
            MI_ClearSubFolder.DataContext = clearSubFolder;
            MI_NonSkyrimFile.DataContext = nonSkyrimFileCheck;
            MI_FacegenFolderEdit.DataContext = facegenFolderEdit;

            initialDone = true;
        }

        public void LoadFolderList(List<string> folders, bool multiThread = true)
        {
            selectedFolders = folders;
            selectedFiles = Util.GetAllFilesFromFolders(selectedFolders, SearchOption.AllDirectories);
            if (multiThread)
                Load();
            else
                LoadSingle();
        }
        public void LoadFileList(List<string> files, bool multiThread = true)
        {
            selectedFolders.Clear();
            selectedFiles = files;
            if (multiThread)
                Load();
            else
                LoadSingle();
        }
        public void Load()
        {
            LV_FileList_Update(true);
            LV_ExtensionList_Update(true);
            MI_Reset_Active(true);
            MI_Save_Active(false);
            MI_Macro_Active(false);
            files.Clear();
            nonSkyrimFiles.Clear();
            filesEdited.Clear();
            GetFilesView();
            MI_Macro_Active(true);
        }
        public void LoadSingle()
        {
            LV_FileList_Update(true);
            LV_ExtensionList_Update(true);
            MI_Reset_Active(true);
            MI_Save_Active(false);
            MI_Macro_Active(false);
            files.Clear();
            nonSkyrimFiles.Clear();
            filesEdited.Clear();
            GetFiles();
            MI_Macro_Active(true);
        }

        public async void GetFilesView()
        {
            await Task.Run(() => GetFiles());
        }

        public bool IsInitialDone() { return initialDone; }

        private void GetFiles()
        {
            isContentEdited = false;

            ProgressBarInitial();
            double step = ProgressBarMaximum() / selectedFiles.Count;
            object tmpLock = new object();

            LV_FileList_Active(false);
            LV_ExtensionList_Active(false);
            files.Clear();
            nonSkyrimFiles.Clear();
            filesEdited.Clear();

            ConcurrentBag<string> failFiles = new ConcurrentBag<string>();
            ConcurrentBag<MoveFileSet> fileTemp = new ConcurrentBag<MoveFileSet>();
            ConcurrentBag<MoveFileSet> nonSkyrimFileTemp = new ConcurrentBag<MoveFileSet>();
            ConcurrentDictionary<string, Extensions> extensionTemp = new ConcurrentDictionary<string, Extensions>();
            Parallel.ForEach(selectedFiles, path =>
            {
                if (!Util.CanRead(path))
                {
                    Logger.Log.Error("Unable to access : " + path);
                    failFiles.Add(path);
                }
                else
                {
                    string fileName = System.IO.Path.GetFileName(path);
                    MoveFileSet newFile = new MoveFileSet();

                    newFile.FileBefore = Util.GetRelativePath(path);
                    newFile.FileAfter = newFile.FileBefore;
                    newFile.FileFullPath = path;
                    newFile.IsChecked = true;
                    newFile.IsSelected = false;
                    newFile.NonSkyrimFile = !Util.IsPossibleRelativePath(path);
                    newFile.FileBasePath = Util.GetBasePath(path);
                    newFile.DisplayBefore = newFile.FileBefore;
                    newFile.DisplayAfter = newFile.FileAfter;
                    newFile.IsContentEdited = false;

                    if (Util.IsTextFile(fileName))
                    {
                        newFile.FileContentBefore = Util.ReadAllText(path, out newFile.FileContentEncoding);
                        newFile.FileContentAfter = newFile.FileContentBefore;
                        newFile.IsContainsContent = true;
                    }

                    if (fileContent.IsChecked && newFile.IsContainsContent)
                    {
                        newFile.TooltipBefore = newFile.FileContentBefore;
                        newFile.TooltipAfter = newFile.FileContentAfter;
                    }
                    else
                    {
                        if (newFile.NonSkyrimFile)
                        {
                            newFile.TooltipBefore = newFile.FileBefore;
                            newFile.TooltipAfter = newFile.FileAfter;
                        }
                        else
                        {
                            newFile.TooltipBefore = newFile.FileBasePath + newFile.FileBefore;
                            newFile.TooltipAfter = newFile.FileBasePath + newFile.FileAfter;
                        }
                    }

                    newFile.FileExtension = System.IO.Path.GetExtension(path).ToLower();
                    if (newFile.NonSkyrimFile)
                    {
                        nonSkyrimFileTemp.Add(newFile);
                    }
                    else
                    {
                        fileTemp.Add(newFile);
                    }
                    Extensions newExtension = new Extensions() 
                    {
                        FileExtension = newFile.FileExtension,
                        IsChecked = true,
                        IsSelected = false
                    };
                    extensionTemp.AddOrUpdate(newFile.FileExtension, newExtension, (key, oldValue) => newExtension);
                }
                ProgressBarStep(step);
            });
            files.AddRange(fileTemp);
            nonSkyrimFiles.AddRange(nonSkyrimFileTemp);
            extensionList.AddRange(extensionTemp.Values.ToList());

            LV_FileList_Update();
            LV_ExtensionList_Update();

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
                System.Windows.MessageBox.Show(wrongFiles, "ERROR : Failed to access file!");
            }

            LV_FileList_Sort();
            extensionList.Sort((x, y) => { return x.FileExtension.CompareTo(y.FileExtension); });
            LV_FileList_Update();
            LV_ExtensionList_Update();
            LV_FileList_Active();
            LV_ExtensionList_Active();

            ProgressBarDone();
        }
        private void LV_FileList_Sort()
        {
            files.Sort((x, y) => {
                if (x.NonSkyrimFile && !y.NonSkyrimFile)
                    return -1;
                else if (!x.NonSkyrimFile && y.NonSkyrimFile)
                    return 1;
                else if (x.NonSkyrimFile && y.NonSkyrimFile)
                    return x.FileBefore.CompareTo(y.FileBefore);
                int xl = x.FileBefore.Split('\\').Length;
                int yl = y.FileBefore.Split('\\').Length;
                if (xl == 1 && yl != 1)
                    return -1;
                else if (xl != 1 && yl == 1)
                    return 1;
                return x.FileBefore.CompareTo(y.FileBefore); 
            });
        }

        private void BT_Apply_Click(object sender, RoutedEventArgs e)
        {
            BT_Apply_Active(false);
            if (TB_ReplaceSearch.Text.Length > 0 || TB_ReplaceResult.Text.Length > 0)
            {
                string ReplaceSearch = TB_ReplaceSearch.Text;
                string ReplaceResult = TB_ReplaceResult.Text;
                Apply_Replace(ReplaceSearch, ReplaceResult);
            }

            BT_Apply_Update();
            MI_Reset_Active();
            MI_Save_Active();
            ApplyActiveDelay();
        }
        private void Apply_Replace(string search, string result)
        {
            LV_FileList_Active(false);
            char IsValidPathSearch = Util.IsValidPath(search);
            char IsValidPathResult = Util.IsValidPath(result);
            if (!fileContent.IsChecked)
            {
                if (IsValidPathSearch != '0')
                {
                    System.Windows.MessageBox.Show("Contains invalid character! <" + IsValidPathSearch + ">");
                    return;
                }
                else if (IsValidPathResult != '0')
                {
                    System.Windows.MessageBox.Show("Contains invalid character! <" + IsValidPathResult + ">");
                    return;
                }
            }
            isContentEdited = isContentEdited ? isContentEdited : fileContent.IsChecked;
            Parallel.ForEach(files, file =>
            {
                if (file.IsChecked)
                {
                    if (!fileContent.IsChecked)
                    {
                        if (IsValidPathSearch == '0' && IsValidPathResult == '0')
                        {
                            if (!Util.IsFacegenThing(file.FileBefore) || (Util.IsFacegenThing(file.FileBefore) && facegenFolderEdit.IsChecked))
                            {
                                file.FileAfter = Util.Replace(file.FileAfter, search, result, matchCase.IsChecked);
                                file.DisplayAfter = file.FileAfter;
                            }
                        }
                    }
                    else if (file.IsContainsContent)
                    {
                        file.FileContentAfter = Util.Replace(file.FileContentAfter, search, result, matchCase.IsChecked);
                        file.TooltipAfter = file.FileContentAfter;
                        file.IsContentEdited = true;
                    }
                    filesEdited.AddOrUpdate(file.FileBefore, file, (key, oldValue) => file);
                }
            });
            LV_FileList_Update();
            LV_FileList_Active();
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

        private void LV_FileList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv == null) 
                return;
            ListView lv1 = null;
            ListView lv2 = null;
            if (lv == LV_FileListBefore)
            {
                lv1 = LV_FileListBefore;
                lv2 = LV_FileListAfter;
            }
            else if (lv == LV_FileListAfter)
            {
                lv1 = LV_FileListAfter;
                lv2 = LV_FileListBefore;
            }
            Decorator border1 = VisualTreeHelper.GetChild(lv1, 0) as Decorator;
            Decorator border2 = VisualTreeHelper.GetChild(lv2, 0) as Decorator;
            ScrollViewer scrollViewer1 = border1.Child as ScrollViewer;
            ScrollViewer scrollViewer2 = border2.Child as ScrollViewer;
            scrollViewer2.ScrollToVerticalOffset(scrollViewer1.VerticalOffset);
            scrollViewer2.ScrollToHorizontalOffset(scrollViewer1.HorizontalOffset);
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
            lock(progressLock)
            {
                ProgressBarValue = ProgressBarMax;
            }
            await Task.Run(() => ProgressBarUpdate());
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
            if (lv == LV_FileListBefore)
            {
                GVC_FileListBefore.Width = GVC_FileListBefore.Width * Ratio;
            }
            else if (lv == LV_FileListAfter)
            {
                GVC_FileListAfter.Width = GVC_FileListAfter.Width * Ratio;
            }
            else if (lv == LV_ExtensionList)
            {
                GVC_Extensions.Width = GVC_Extensions.Width * Ratio;
            }
        }
        private void SizeChangeAll(double Ratio)
        {
            GVC_FileListBefore.Width = GVC_FileListBefore.Width * Ratio;
            GVC_FileListAfter.Width = GVC_FileListAfter.Width * Ratio;
            GVC_Extensions.Width = GVC_Extensions.Width * Ratio;
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
                else if (TB_ReplaceSearch.Text.Length > 0 || TB_ReplaceResult.Text.Length > 0)
                {
                    BT_Apply.IsEnabled = true;
                    return;
                }

                BT_Apply.IsEnabled = false;
            }));
        }
        private async Task LV_FileList_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    LV_FileListBefore.ItemsSource = files;
                    LV_FileListAfter.ItemsSource = files;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(files);
                    view.Refresh();
                }
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
        }
        private void LV_FileList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_FileListBefore.IsEnabled = Active;
                LV_FileListAfter.IsEnabled = Active;
            }));
        }

        private async Task LV_ExtensionList_Update(bool binding = false)
        {
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (binding)
                {
                    LV_ExtensionList.ItemsSource = extensionList;
                    LV_ExtensionList.DataContext = extensionList;
                }
                else
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(extensionList);
                    view.Refresh();
                }
            }));
            await Task.Delay(TimeSpan.FromTicks(1));
        }
        private void LV_ExtensionList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_ExtensionList.IsEnabled = Active;
            }));
        }

        private void TB_TextChanged(object sender, TextChangedEventArgs e)
        {
            BT_Apply_Update();
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
            GetFilesView();
        }
        private void MI_Save_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_Save.IsEnabled = Active;
            }));
        }
        private void MI_Macro_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MI_Macro.IsEnabled = Active;
            }));
        }
        private void MI_Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }
        private void Save()
        {
            ConcurrentBag<string> failFiles = new ConcurrentBag<string>();
            Parallel.ForEach(filesEdited, file =>
            {
                string sourceFilePath = file.Value.FileBasePath + file.Value.FileBefore;
                string targetFilePath = file.Value.FileBasePath + file.Value.FileAfter;

                bool UnableWrite = false;
                if (!Util.CanRead(sourceFilePath) || !Util.CanWrite(targetFilePath))
                {
                    UInt16 count = 0;
                    while (count < 100)
                    {
                        Task.Delay(1).Wait();
                        if (Util.CanRead(sourceFilePath) && Util.CanWrite(targetFilePath))
                            break;
                        count++;
                    }
                    if (count == 100)
                    {
                        failFiles.Add(sourceFilePath);
                        Logger.Log.Fatal("Unable to move the file : " + sourceFilePath + " => " + targetFilePath);
                        UnableWrite = true;
                    }
                }

                if (!UnableWrite)
                {
                    if (isContentEdited && file.Value.IsContainsContent)
                    {
                        File.WriteAllText(sourceFilePath, file.Value.FileContentAfter, file.Value.FileContentEncoding);
                    }
                    if (sourceFilePath.ToLower() == targetFilePath.ToLower())
                        return; //don't need move file
                    if (File.Exists(targetFilePath))
                    {
                        if (fileOverwrite.IsChecked)
                        {
                            System.IO.File.Delete(targetFilePath);
                        }
                    }
                    if (!File.Exists(targetFilePath))
                    {
                        System.IO.Directory.CreateDirectory(Util.GetDirectoryPath(targetFilePath));
                        System.IO.File.Move(sourceFilePath, targetFilePath);
                        selectedFiles.Remove(sourceFilePath);
                        selectedFiles.Add(targetFilePath);
                    }
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
            if (clearSubFolder.IsChecked)
            {
                Parallel.ForEach(selectedFolders, folder =>
                {
                    Util.ClearSubDirectory(folder);
                });
            }
            if (!MacroMode)
                System.Windows.MessageBox.Show("Move/Edit done!");
            App.mainWindow.LoadFileList(filesEdited.Values.ToList());
            LoadFileList(selectedFiles, !MacroMode);
            if (App.nifManager != null && App.nifManager.IsLoaded)
                App.nifManager.LoadNifFiles(selectedFiles, !MacroMode);
        }

        private void MI_NonSkyrimFile_CheckUncheck(object sender, RoutedEventArgs e)
        {
            if (nonSkyrimFile.IsChecked)
            {
                files.AddRange(nonSkyrimFiles);
                nonSkyrimFiles.Clear();
            }
            else
            {
                nonSkyrimFiles.AddRange(files.FindAll(x => x.NonSkyrimFile));
                nonSkyrimFiles.AddRange(filesDisable.FindAll(x => x.NonSkyrimFile));
                files.RemoveAll(x => x.NonSkyrimFile);
                filesDisable.RemoveAll(x => x.NonSkyrimFile);
            }
            FileListUpdate();
            LV_FileList_Sort();
            LV_FileList_Update();

            Config.GetSingleton.SetFileManager_NonSkyrimFile(nonSkyrimFile.IsChecked);
        }

        private void CB_FileContent_CheckUncheck(object sender, RoutedEventArgs e)
        {
            if (fileContent.IsChecked)
            {
                Parallel.ForEach(files, file =>
                {
                    if (file.IsContainsContent)
                    {
                        file.TooltipBefore = file.FileContentBefore;
                        file.TooltipAfter = file.FileContentAfter;
                    }
                });
                Parallel.ForEach(filesDisable, file =>
                {
                    if (file.IsContainsContent)
                    {
                        file.TooltipBefore = file.FileContentBefore;
                        file.TooltipAfter = file.FileContentAfter;
                    }
                });
                Parallel.ForEach(nonSkyrimFiles, file =>
                {
                    if (file.IsContainsContent)
                    {
                        file.TooltipBefore = file.FileContentBefore;
                        file.TooltipAfter = file.FileContentAfter;
                    }
                });
            }
            else
            {
                Parallel.ForEach(files, file =>
                {
                    if (file.IsContainsContent)
                    {
                        if (file.NonSkyrimFile)
                        {
                            file.TooltipBefore = file.FileBefore;
                            file.TooltipAfter = file.FileAfter;
                        }
                        else
                        {
                            file.TooltipBefore = file.FileBasePath + file.FileBefore;
                            file.TooltipAfter = file.FileBasePath + file.FileAfter;
                        }
                    }
                });
                Parallel.ForEach(filesDisable, file =>
                {
                    if (file.IsContainsContent)
                    {
                        if (file.NonSkyrimFile)
                        {
                            file.TooltipBefore = file.FileBefore;
                            file.TooltipAfter = file.FileAfter;
                        }
                        else
                        {
                            file.TooltipBefore = file.FileBasePath + file.FileBefore;
                            file.TooltipAfter = file.FileBasePath + file.FileAfter;
                        }
                    }
                });
                Parallel.ForEach(nonSkyrimFiles, file =>
                {
                    if (file.IsContainsContent)
                    {
                        if (file.NonSkyrimFile)
                        {
                            file.TooltipBefore = file.FileBefore;
                            file.TooltipAfter = file.FileAfter;
                        }
                        else
                        {
                            file.TooltipBefore = file.FileBasePath + file.FileBefore;
                            file.TooltipAfter = file.FileBasePath + file.FileAfter;
                        }
                    }
                });
            }
            Config.GetSingleton.SetFileManager_FileContent(fileContent.IsChecked);
        }

        private void CB_Extensions_CheckUncheck(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Extensions extensions = sender as Extensions;
            if (cb != null)
            {
                Extensions ex = cb.DataContext as Extensions;
                if (ex == null)
                {
                    Logger.Log.Error("Couldn't get Extensions from checkbox data");
                    return;
                }

                if (cb.IsChecked ?? false)
                {
                    files.AddRange(filesDisable.FindAll(x => x.FileExtension == ex.FileExtension));
                    filesDisable.RemoveAll(x => x.FileExtension == ex.FileExtension);
                }
                else
                {
                    filesDisable.AddRange(files.FindAll(x => x.FileExtension == ex.FileExtension));
                    files.RemoveAll(x => x.FileExtension == ex.FileExtension);
                }
                LV_FileList_Sort();
                LV_FileList_Update();
            }
            else if (extensions != null && !this.IsLoaded)
            {
                if (extensions.IsChecked)
                {
                    files.AddRange(filesDisable.FindAll(x => x.FileExtension == extensions.FileExtension));
                    filesDisable.RemoveAll(x => x.FileExtension == extensions.FileExtension);
                }
                else
                {
                    filesDisable.AddRange(files.FindAll(x => x.FileExtension == extensions.FileExtension));
                    files.RemoveAll(x => x.FileExtension == extensions.FileExtension);
                }
                LV_FileList_Sort();
                LV_FileList_Update();
            }
        }

        private void LV_Check_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            ContextMenu cm = Util.FindParent<ContextMenu>(mi);
            if (cm == null)
                return;
            if (cm == LV_FileListBefore.ContextMenu || cm == LV_FileListAfter.ContextMenu)
            {
                Parallel.ForEach(files, file =>
                {
                    if (file.IsSelected)
                        file.IsChecked = true;
                });
                LV_FileList_Update();
            }
            else if (cm == LV_ExtensionList.ContextMenu)
            {
                foreach (var ex in extensionList)
                {
                    if (ex.IsSelected)
                        ex.IsChecked = true;
                }
                LV_ExtensionList_Update();
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
            if (cm == LV_FileListBefore.ContextMenu || cm == LV_FileListAfter.ContextMenu)
            {
                Parallel.ForEach(files, file =>
                {
                    if (file.IsSelected)
                        file.IsChecked = false;
                });
                LV_FileList_Update();
            }
            else if (cm == LV_ExtensionList.ContextMenu)
            {
                foreach (var ex in extensionList)
                {
                    if (ex.IsSelected)
                        ex.IsChecked = false;
                }
                LV_ExtensionList_Update();
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
            if (cm == LV_FileListBefore.ContextMenu || cm == LV_FileListAfter.ContextMenu)
            {
                Parallel.ForEach(files, file =>
                {
                    if (file.IsSelected)
                        file.IsChecked = !file.IsChecked;
                });
                LV_FileList_Update();
            }
            else if (cm == LV_ExtensionList.ContextMenu)
            {
                foreach (var ex in extensionList)
                {
                    if (ex.IsSelected)
                        ex.IsChecked = !ex.IsChecked;
                }
                LV_ExtensionList_Update();
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
            if (cm == LV_FileListBefore.ContextMenu || cm == LV_FileListAfter.ContextMenu)
            {
                Parallel.ForEach(files, file =>
                {
                    file.IsChecked = true;
                });
                LV_FileList_Update();
            }
            else if (cm == LV_ExtensionList.ContextMenu)
            {
                foreach (var ex in extensionList)
                {
                    ex.IsChecked = true;
                }
                LV_ExtensionList_Update();
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
            if (cm == LV_FileListBefore.ContextMenu || cm == LV_FileListAfter.ContextMenu)
            {
                Parallel.ForEach(files, file =>
                {
                    file.IsChecked = false;
                });
                LV_FileList_Update();
            }
            else if (cm == LV_ExtensionList.ContextMenu)
            {
                foreach (var ex in extensionList)
                {
                    ex.IsChecked = false;
                }
                LV_ExtensionList_Update();
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
            if (cm == LV_FileListBefore.ContextMenu || cm == LV_FileListAfter.ContextMenu)
            {
                Parallel.ForEach(files, file =>
                {
                    file.IsChecked = !file.IsChecked;
                });
                LV_FileList_Update();
            }
            else if (cm == LV_ExtensionList.ContextMenu)
            {
                foreach (var ex in extensionList)
                {
                    ex.IsChecked = !ex.IsChecked;
                }
                LV_ExtensionList_Update();
            }
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
            if (lv == LV_FileListBefore || lv == LV_FileListAfter)
            {
                foreach (var file in files)
                {
                    if (file.IsSelected)
                        file.IsChecked = !file.IsChecked;
                }
                LV_FileList_Update();
            }
            else if (lv == LV_ExtensionList)
            {
                foreach (var ex in extensionList)
                {
                    if (ex.IsSelected)
                        ex.IsChecked = !ex.IsChecked;
                }
                LV_ExtensionList_Update();
            }
        }

        private void FileListUpdate()
        {
            var disables = files.FindAll(x => extensionList.FindIndex(y => y.FileExtension == x.FileExtension && !y.IsChecked) != -1);
            var enables = filesDisable.FindAll(x => extensionList.FindIndex(y => y.FileExtension == x.FileExtension && y.IsChecked) != -1);
            disables.ForEach(x => files.Remove(x));
            enables.ForEach(x => filesDisable.Remove(x));
            files.AddRange(enables);
            filesDisable.AddRange(disables);
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
                    Config.GetSingleton.SetFileManager_MatchCase(matchCase.IsChecked);
            }
            MenuItem mi = sender as MenuItem;
            if (mi != null)
            {
                if (mi == MI_FileOverwrite)
                    Config.GetSingleton.SetFileManager_FileOverwrite(fileOverwrite.IsChecked);
                else if (mi == MI_ClearSubFolder)
                    Config.GetSingleton.SetFileManager_ClearEmptySubFolder(clearSubFolder.IsChecked);
                else if (mi == MI_FacegenFolderEdit)
                    Config.GetSingleton.SetFileManager_FacegenFolderEdit(facegenFolderEdit.IsChecked);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Config.GetSingleton.SetFileManager_Size(this.Height, this.Width);
            App.fileManager = null;
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
            bool isNifEdit = false;
            bool isSave = false;

            BT_Apply_Active(false);
            LV_ExtensionList_Active(false);
            LV_FileList_Active(false);
            MI_Reset_Active(false);
            MI_Save_Active(false);
            MI_Macro_Active(false);
            MacroMode = true;

            foreach (var line in File.ReadLines(file))
            {
                if (line == "PLUGINEDIT")
                    isFileEdit = false;
                else if (line == "NIFEDIT")
                {
                    isFileEdit = false;
                    isNifEdit = true;
                }
                else if (line == "FILEEDIT")
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
                    if (m2 == "EXTENSIONS")
                    {
                        if (m3 == "CHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                foreach (var item in extensionList)
                                {
                                    item.IsChecked = true;
                                    CB_Extensions_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in extensionList)
                                {
                                    if (item.FileExtension.Contains(m4, StringComparison.OrdinalIgnoreCase))
                                    {
                                        item.IsChecked = true;
                                        CB_Extensions_CheckUncheck(item, new RoutedEventArgs());
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
                                foreach (var item in extensionList)
                                {
                                    item.IsChecked = false;
                                    CB_Extensions_CheckUncheck(item, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                foreach (var item in extensionList)
                                {
                                    if (item.FileExtension.Contains(m4, StringComparison.OrdinalIgnoreCase))
                                    {
                                        item.IsChecked = false;
                                        CB_Extensions_CheckUncheck(item, new RoutedEventArgs());
                                    }
                                }
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            foreach (var item in extensionList)
                            {
                                item.IsChecked = !item.IsChecked;
                                CB_Extensions_CheckUncheck(item, new RoutedEventArgs());
                            }
                        }
                    }
                    else if (m2 == "FILELIST")
                    {
                        if (m3 == "CHECK")
                        {
                            if (macro.Length < 4)
                                continue;
                            var m4 = macro[3];
                            if (m4 == "ALL")
                            {
                                Parallel.ForEach(files, item =>
                                {
                                    item.IsChecked = true;
                                });
                            }
                            else
                            {
                                Parallel.ForEach(files, item =>
                                {
                                    if (item.FileAfter.Contains(m4, StringComparison.OrdinalIgnoreCase))
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
                                Parallel.ForEach(files, item =>
                                {
                                    item.IsChecked = false;
                                });
                            }
                            else
                            {
                                Parallel.ForEach(files, item =>
                                {
                                    if (item.FileAfter.Contains(m4, StringComparison.OrdinalIgnoreCase))
                                        item.IsChecked = false;
                                });
                            }
                        }
                        else if (m3 == "INVERT")
                        {
                            Parallel.ForEach(files, item =>
                            {
                                item.IsChecked = !item.IsChecked;
                            }); ;
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
                    else if (m2 == "FILECONTENT")
                    {
                        if (m3 == "CHECK")
                        {
                            fileContent.IsChecked = true;
                            if (!this.IsLoaded)
                                CB_FileContent_CheckUncheck(new object(), new RoutedEventArgs());
                        }
                        else if (m3 == "UNCHECK")
                        {
                            fileContent.IsChecked = false;
                            if (!this.IsLoaded)
                                CB_FileContent_CheckUncheck(new object(), new RoutedEventArgs());
                        }
                    }
                    else if (m2 == "NONSKYRIM")
                    {
                        if (m3 == "CHECK")
                        {
                            nonSkyrimFile.IsChecked = true;
                            if (!this.IsLoaded)
                                MI_NonSkyrimFile_CheckUncheck(new object(), new RoutedEventArgs());
                        }
                        else if (m3 == "UNCHECK")
                        {
                            nonSkyrimFile.IsChecked = false;
                            if (!this.IsLoaded)
                                MI_NonSkyrimFile_CheckUncheck(new object(), new RoutedEventArgs());
                        }
                    }
                    else if (m2 == "FACEGENFOLDER")
                    {
                        if (m3 == "CHECK")
                        {
                            facegenFolderEdit.IsChecked = true;
                        }
                        else if (m3 == "UNCHECK")
                        {
                            facegenFolderEdit.IsChecked = false;
                        }
                    }
                }
                else if (m1 == "APPLY")
                {
                    if (macro.Length < 3)
                        continue;
                    var m2 = macro[1];
                    var m3 = macro[2];
                    if (m2 == "REPLACE")
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
                LV_FileList_Sort();
                LV_FileList_Update();
                LV_ExtensionList_Update();
            }
            BT_Apply_Update();
            LV_ExtensionList_Active();
            LV_FileList_Active();
            MI_Reset_Active();
            MI_Save_Active();
            MI_Macro_Active();

            if (isNifEdit)
            {
                if (App.nifManager == null)
                    App.nifManager = new NifManager();
                App.nifManager.LoadNifFiles(selectedFiles, false);
                App.nifManager.Macro_Load(file, !App.nifManager.IsLoaded);
            }

            MacroMode = false;

            if (endClose && isSave && this.IsLoaded)
                this.Close();
            else if (!endClose)
                System.Windows.MessageBox.Show("Macro loaded");
        }

        private void MI_NifManager_Click(object sender, RoutedEventArgs e)
        {
            if (App.nifManager != null)
            {
                if (App.nifManager.IsLoaded)
                    return;
            }
            else
                App.nifManager = new NifManager();
            App.nifManager.Show();
            if (selectedFolders.Count > 0)
                App.nifManager.LoadNifFiles(selectedFiles);
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
                selectedFiles = Util.GetAllFilesFromFolders(selectedFolders, SearchOption.AllDirectories);
                isUpdateFileList = true;
            }
            var files = FileOrFolder.ToList().FindAll(x => File.Exists(x));
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

            if (App.nifManager != null && App.nifManager.IsLoaded)
                App.nifManager.LoadNifFiles(selectedFiles);
        }
    }
    public class FileContent : INotifyPropertyChanged
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
    public class NonSkyrimFile : INotifyPropertyChanged
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

    public class MoveFileSet : INotifyPropertyChanged
    {
        public string FileBasePath { get; set; }
        public string FileBefore { get; set; }
        public string FileAfter { get; set; }
        public string FileExtension { get; set; }
        public string FileFullPath { get; set; }

        public string FileContentBefore { get; set; }
        public string FileContentAfter { get; set; }
        public Encoding FileContentEncoding = Encoding.UTF8;
        public bool IsContainsContent { get; set; }
        public bool IsContentEdited { get; set; }

        public string DisplayBefore { get; set; }
        public string DisplayAfter { get; set; }
        private string _TooltipBefore;
        public string TooltipBefore
        {
            get { return _TooltipBefore; }
            set
            {
                _TooltipBefore = value;
                OnPropertyChanged("TooltipBefore");
            }
        }
        private string _TooltipAfter;
        public string TooltipAfter
        {
            get { return _TooltipAfter; }
            set
            {
                _TooltipAfter = value;
                OnPropertyChanged("TooltipAfter");
            }
        }

        public bool NonSkyrimFile {  get; set; }

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
    public class Extensions : INotifyPropertyChanged
    {
        public string FileExtension { get; set; }

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
