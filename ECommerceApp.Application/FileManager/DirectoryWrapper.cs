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
    }
}
