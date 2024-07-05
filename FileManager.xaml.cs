using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WK.Libraries.BetterFolderBrowserNS;
using static SkyrimPluginEditor.MainWindow;

namespace SkyrimPluginEditor
{
    /// <summary>
    /// MoveFile.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class FileManager : Window
    {
        List<string> selectedFolders = new List<string>();
        List<MoveFileSet> files = new List<MoveFileSet>();
        List<MoveFileSet> nonSkyrimFiles = new List<MoveFileSet>();
        List<MoveFileSet> filesDisable = new List<MoveFileSet>();
        List<Extensions> extensionList = new List<Extensions>();
        bool isContentEdited = false;

        public FileManager(List<string> folders)
        {
            selectedFolders = folders;
            InitializeComponent();
            LV_FileList_Update(true);
            LV_ExtensionList_Update(true);
            MI_Reset_Active(false);
            MI_Save_Active(false);
            GetFiles();

            CB_MatchCase.IsChecked = Config.GetSingleton.GetFileManager_MatchCase();
            MI_FileOverwrite.IsChecked = Config.GetSingleton.GetFileManager_FileOverwrite();
            MI_ClearSubFolder.IsChecked = Config.GetSingleton.GetFileManager_ClearEmptySubFolder();
            MI_NonSkyrimFile.IsChecked = Config.GetSingleton.GetFileManager_NonSkyrimFile();
        }

        private void Search_Folder()
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

            Task.Run(async () => { GetFiles(); });

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
            Logger.Log.Info("[MoveFile] Selected Folder(s) : " + fullFolderTextBox);
        }

        private void GetFile(string path, object tmpLock)
        {
            string fileName = System.IO.Path.GetFileName(path);
            MoveFileSet newFile = new MoveFileSet();

            newFile.FileBefore = Util.GetRelativePath(path);
            newFile.FileAfter = newFile.FileBefore;
            newFile.IsChecked = true;
            newFile.IsSelected = false;
            newFile.NonSkyrimFile = !Util.IsPossibleRelativePath(path);
            newFile.FileBasePath = Util.GetBasePath(path);
            newFile.DisplayBefore = newFile.FileBefore;
            newFile.DisplayAfter = newFile.FileAfter;
            newFile.IsContentEdited = false;

            if (Util.IsTextFile(fileName))
            {
                newFile.FileContentBefore = File.ReadAllText(path);
                newFile.FileContentAfter = newFile.FileContentBefore;
                newFile.IsContainsContent = true;
            }

            if (CB_FileContent_IsChecked() && newFile.IsContainsContent)
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
            int index = extensionList.FindIndex(x => x.FileExtension == newFile.FileExtension);

            lock (tmpLock)
            {
                if (newFile.NonSkyrimFile)
                {
                    nonSkyrimFiles.Add(newFile);
                }
                else
                {
                    files.Add(newFile);
                }

                if (index == -1)
                {
                    extensionList.Add(new Extensions() { FileExtension = newFile.FileExtension, IsChecked = true, IsSelected = false });
                }
            }
        }
        private async void GetFiles()
        {
            isContentEdited = false;

            ProgressBarInitial();
            double step = ProgressBarMaximum() / selectedFolders.Count;
            object tmpLock = new object();

            LV_FileList_Active(false);
            LV_ExtensionList_Active(false);
            files.Clear();
            nonSkyrimFiles.Clear();
            Dictionary<string, PluginStreamBase._ErrorCode> wrongPlugins = new Dictionary<string, PluginStreamBase._ErrorCode>();
            foreach (var folder in selectedFolders)
            {
                if (Directory.Exists(folder))
                {
                    foreach (var path in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
                    {
                        GetFile(path, tmpLock);
                    }
                }
                ProgressBarStep(step);
            }
            LV_FileList_Sort();
            extensionList.Sort((x, y) => { return x.FileExtension.CompareTo(y.FileExtension); });
            LV_FileList_Update();
            LV_ExtensionList_Update();
            LV_FileList_Active();
            LV_ExtensionList_Active();
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

        private bool CB_FileContent_IsChecked()
        {
            bool isChecked = false;
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                isChecked = CB_FileContent.IsChecked ?? false;
            }));
            return isChecked;
        }

        private async void BT_Apply_Click(object sender, RoutedEventArgs e)
        {
            BT_Apply_Active(false);
            if (TB_ReplaceSearch.Text.Length > 0 || TB_ReplaceResult.Text.Length > 0)
            {
                LV_FileList_Active(false);
                string ReplaceSearch = TB_ReplaceSearch.Text;
                string ReplaceResult = TB_ReplaceResult.Text;
                bool MatchCase = false;
                if (CB_MatchCase.IsChecked == true)
                    MatchCase = true;
                bool FileContent = false;
                if (CB_FileContent.IsChecked  ==  true)
                    FileContent = true;
                isContentEdited = isContentEdited ? isContentEdited : FileContent;
                Parallel.ForEach(files, file =>
                {
                    if (file.IsChecked)
                    {
                        file.FileAfter = Regex.Replace(file.FileAfter, ReplaceSearch, ReplaceResult, MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                        file.DisplayAfter = file.FileAfter;
                        if (FileContent && file.IsContainsContent)
                        {
                            file.FileContentAfter = Regex.Replace(file.FileContentAfter, ReplaceSearch, ReplaceResult, MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                            file.IsContentEdited = true;
                        }
                    }
                });
                LV_FileList_Update();
                LV_FileList_Active();
            }

            BT_Apply_Update();
            MI_Reset_Active();
            MI_Save_Active();
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
        private void LV_FileList_Update(bool binding = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
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
                    Task.Delay(TimeSpan.FromTicks(1));
                }
            }));
        }
        private void LV_FileList_Active(bool Active = true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                LV_FileListBefore.IsEnabled = Active;
                LV_FileListAfter.IsEnabled = Active;
            }));
        }

        private void LV_ExtensionList_Update(bool binding = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
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
                    Task.Delay(TimeSpan.FromTicks(1));
                }
            }));
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
            GetFiles();
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
            bool fileOverwrite = MI_FileOverwrite.IsChecked;
            Parallel.ForEach(files, file =>
            {
                string sourceFilePath = file.FileBasePath + file.FileBefore;
                string targetFilePath = file.FileBasePath + file.FileAfter;
                if (isContentEdited && file.IsContainsContent)
                {
                    File.WriteAllText(sourceFilePath, file.FileContentAfter);
                }
                if (sourceFilePath.ToLower() == targetFilePath.ToLower())
                    return; //don't need move file
                if (File.Exists(targetFilePath))
                {
                    if (fileOverwrite)
                    {
                        System.IO.File.Delete(targetFilePath);
                    }
                }
                if (!File.Exists(targetFilePath))
                {
                    System.IO.Directory.CreateDirectory(Util.GetDirectoryPath(targetFilePath));
                    System.IO.File.Move(sourceFilePath, targetFilePath);
                }
            });
            if (MI_ClearSubFolder.IsChecked)
            {
                Parallel.ForEach(selectedFolders, folder =>
                {
                    Util.ClearSubDirectory(folder);
                });
            }
            System.Windows.MessageBox.Show("Move/Edit done!");
            GetFiles();
        }

        private void MI_NonSkyrimFile_CheckUncheck(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
                return;
            if (mi.IsChecked)
            {
                files.AddRange(nonSkyrimFiles);
            }
            else
            {
                files.RemoveAll(x => x.NonSkyrimFile);
            }
            LV_FileList_Sort();
            LV_FileList_Update();

            Config.GetSingleton.SetFileManager_NonSkyrimFile(mi.IsChecked);
        }

        private async void CB_FileContent_CheckUncheck(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (cb == null)
                return;
            if (cb.IsChecked ?? false)
            {
                Parallel.ForEach(files, file =>
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
            }
            Config.GetSingleton.SetFileManager_FileContent(cb.IsChecked ?? false);
        }

        private void CB_Extensions_CheckUncheck(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (cb == null)
                return;
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
                    Config.GetSingleton.SetFileManager_MatchCase(CB_MatchCase.IsChecked ?? false);
            }
            MenuItem mi = sender as MenuItem;
            if (mi != null)
            {
                if (mi == MI_FileOverwrite)
                    Config.GetSingleton.SetFileManager_FileOverwrite(mi.IsChecked);
                else if (mi == MI_ClearSubFolder)
                    Config.GetSingleton.SetFileManager_ClearEmptySubFolder(mi.IsChecked);
            }
        }
    }

    public class MoveFileSet : INotifyPropertyChanged
    {
        private bool _IsChecked;
        private bool _IsSelected;

        public string FileBasePath { get; set; }
        public string FileBefore { get; set; }
        public string FileAfter { get; set; }
        public string FileExtension { get; set; }

        public string FileContentBefore { get; set; }
        public string FileContentAfter { get; set; }
        public bool IsContainsContent { get; set; }
        public bool IsContentEdited { get; set; }

        public string DisplayBefore { get; set; }
        public string DisplayAfter { get; set; }
        public string TooltipBefore { get; set; }
        public string TooltipAfter { get; set; }

        public bool NonSkyrimFile {  get; set; }

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
    public class Extensions : INotifyPropertyChanged
    {
        private bool _IsChecked;
        private bool _IsSelected;

        public string FileExtension { get; set; }

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
