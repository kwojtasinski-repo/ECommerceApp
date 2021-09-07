using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IDirectoryWrapper
    {
        void CreateDirectory(string path);
        void DeleteFolder(DirectoryInfo directoryInfo);
        DirectoryInfo GetDirectory(string path);
    }
}
