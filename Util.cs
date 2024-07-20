using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.IO;
using System.Windows.Forms.Design;
using System.Text;

namespace SkyrimPluginTextEditor
{
    static class Util
    {
        static public bool IsPluginFIle(string file)
        {
            string lowFileName = file.ToLower();
            return lowFileName.EndsWith(".esp") || lowFileName.EndsWith(".esm") || lowFileName.EndsWith(".esl");
        }
        static public T FindParent<T>(DependencyObject dependencyObject) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(dependencyObject);
            if (parent == null)
                return null;

            var parentT = parent as T;
            return parentT ?? FindParent<T>(parent);
        }

        static public bool IsTextFile(string fileName)
        {
            return Config.GetSingleton.IsEditableFileContent(fileName);
        }

        static public int IsSkyrimFile(List<string> splitPath)
        {
            return splitPath.FindIndex(x => x == "meshes" || x == "textures" || x == "interface" || x == "scripts" || x == "strings" || x == "sound" || x == "skse");
        }
        static public string GetRelativePath(string path)
        {
            if (path == null)
                return path;
            var lowPath = path.ToLower();
            var lowSplitPath = lowPath.Split('\\').ToList();
            if (lowSplitPath.Count == 0)
                return path;
            var splitPath = path.Split('\\').ToList();
            int index = IsSkyrimFile(lowSplitPath);
            if (index == -1)
            {
                if (IsPluginFIle(lowPath))
                {
                    return splitPath[splitPath.Count - 1];
                }
                return path;
            }
            string result = splitPath[index];
            for (int i = index + 1; i < splitPath.Count; i++)
            {
                result += @"\" + splitPath[i];
            }
            return result;
        }
        static public string GetBasePath(string path)
        {
            if (path == null)
                return "";
            var lowPath = path.ToLower();
            var lowSplitPath = lowPath.Split('\\').ToList();
            if (lowSplitPath.Count == 0)
                return "";
            var splitPath = path.Split('\\').ToList();
            int index = IsSkyrimFile(lowSplitPath); 
            if (index == -1)
            {
                if (IsPluginFIle(lowPath))
                {
                    string result_ = "";
                    for (int i = 0; i < splitPath.Count - 1; i++)
                    {
                        result_ += splitPath[i] + @"\";
                    }
                    return result_;
                }
                return "";
            }
            string result = "";
            for (int i = 0; i < index; i++)
            {
                result += splitPath[i] + @"\";
            }
            return result;
        }
        static public bool IsPossibleRelativePath(string path)
        {
            if (path == null)
                return false;
            var lowPath = path.ToLower();
            if (IsPluginFIle(lowPath))
                return true;
            var splitPath = lowPath.Split('\\').ToList();
            if (splitPath.Count == 0)
                return false;
            int index = IsSkyrimFile(splitPath); 
            if (index == -1)
                return false;
            return true;
        }
        static public string GetDirectoryPath(string path)
        {
            if (path == null)
                return path;
            var splitPath = path.Split('\\').ToList();
            if (splitPath.Count == 0)
                return path;
            string result = splitPath[0];
            for (int i = 1; i < splitPath.Count-1; i++)
            {
                result += @"\" + splitPath[i];
            }
            return result;
        }

        static public void ClearSubDirectory(string directory) //delete sub directories if empty
        {
            Parallel.ForEach(System.IO.Directory.GetDirectories(directory), d =>
            {
                ClearSubDirectory(d);
                if (!System.IO.Directory.EnumerateFileSystemEntries(d).Any())
                    System.IO.Directory.Delete(d, false);
            });
        }

        static public string Replace(string source, string search, string replace, bool MatchCase)
        {
            return Regex.Replace(source, Regex.Escape(search), replace, MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        }

        static public char IsValidPath(string path)
        {
            foreach (var c in path)
            {
                if (c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == 'I')
                    return c;
            }
            return '0';
        }

        static public string GetMacroFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Title = "Open macro...";
            openFileDialog.Filter = "macro file (*.spt, *.txt)|*.spt;*.txt|All file (*.*)|*.*";

            if (!(openFileDialog.ShowDialog() ?? false))
                return "";
            return openFileDialog.FileName;
        }

        static public bool IsFacegenMesh(string path)
        {
            return path.Contains("FaceGeom", System.StringComparison.OrdinalIgnoreCase) 
                && path.Contains("FaceGenData", System.StringComparison.OrdinalIgnoreCase);
        }

        static public string ReadAllText(string file, out Encoding encoding)
        {
            using (var reader = new StreamReader(file, true))
            {
                reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
            }
            return File.ReadAllText(file, encoding);
        }
    }
}
