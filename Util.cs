using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System.Security.Cryptography.X509Certificates;

namespace SkyrimPluginEditor
{
    internal class Util
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

        static public bool IsTextFile(string filename)
        {
            string lowFileName = filename.ToLower();
            return lowFileName.EndsWith(".ini") || lowFileName.EndsWith(".ec") || lowFileName.EndsWith(".txt");
        }

        static public int IsSkyrimFile(List<string> SplitPath)
        {
            return SplitPath.FindIndex(x => x == "meshes" || x == "textures" || x == "interface" || x == "scripts" || x == "strings" || x == "sound" || x == "skse");
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
    }
}
