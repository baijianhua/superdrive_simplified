using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConnectTo.Foundation.Helper
{
    public class BrowseContentHelper
    {
        #region  filter should be complete in later
        private static readonly List<string> picList = new List<string>()
        {
           ".bmp", ".pcx", ".tiff", ".gif", ".jpeg", ".tga",
           ".exif", ".fpx", ".svg", ".psd", ".cdr", ".pcd",
           ".dxf", ".ufo", ".eps", ".ai", ".png", "hdri",
           "raw", ".jpg"
        };

        private static readonly List<string> audioList = new List<string>()
        {
           ".wav", ".mp3", ".aif", ".au", "ram", ".wma",
           ".mmf", ".amr", ".aac", ".flac"

        };

        private static readonly List<string> videoList = new List<string>()
        {
           ".avi", ".mov", ".mpeg", ".mpg", ".qt", ".ram",
           ".viv", ".ra", ".rm", ".rmvb", ".mp4"
        };

        private static readonly List<string> zipList = new List<string>()
        { ".zip",".rar",".7z"};

        public static bool IsPicutre(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string fileExtention = GetExtension(fileName).ToLower();
            return picList.Contains(fileExtention);
        }

        public static bool IsAudio(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string fileExtention = GetExtension(fileName).ToLower();
            return audioList.Contains(fileExtention);
        }

        public static bool IsVideo(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string fileExtention = GetExtension(fileName).ToLower();
            return videoList.Contains(fileExtention);
        }

        public static bool IsDocument(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string fileExtention = GetExtension(fileName).ToLower();
            return (fileExtention.Equals(".doc")||fileExtention.Equals(".docx"));
        }

        public static bool IsTxt(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string fileExtention = GetExtension(fileName).ToLower();
            return fileExtention.Equals(".txt");
        }

        public static bool IsPPT(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string fileExtention = GetExtension(fileName).ToLower();
            return (fileExtention.Equals(".ppt")||fileExtention.Equals(".pptx"));
        }

        public static bool IsExcel(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string fileExtention = GetExtension(fileName).ToLower();
            return (fileExtention.Equals(".xls")||fileExtention.Equals(".xlsx"));
        }

        public static bool IsZip(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string fileExtention = GetExtension(fileName).ToLower();
            return zipList.Contains(fileExtention);
        }

        #endregion

        public static string GetExtension(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && fileName.LastIndexOf('.') != -1)
            {
                int index = fileName.LastIndexOf('.');
                return fileName.Substring(index);
            }
            return string.Empty;
        }
    }
}
