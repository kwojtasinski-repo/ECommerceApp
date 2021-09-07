using ECommerceApp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ECommerceApp.Application.FileManager
{
    public class DirectoryWrapper : IDirectoryWrapper
    {
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public DirectoryInfo GetDirectory(string path)
        {
            System.IO.DirectoryInfo dirInfo = new DirectoryInfo(path);
            return dirInfo;
        }

        public void DeleteFolder(DirectoryInfo dirInfo)
        {
            dirInfo.Delete(true);
        }
    }
}
