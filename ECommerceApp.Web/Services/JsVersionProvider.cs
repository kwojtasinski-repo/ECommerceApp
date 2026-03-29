using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ECommerceApp.Web.Services
{
    public class JsVersionProvider
    {
        private readonly IFileVersionProvider _fileVersionProvider;
        private readonly IWebHostEnvironment _env;

        public JsVersionProvider(IFileVersionProvider fileVersionProvider, IWebHostEnvironment env)
        {
            _fileVersionProvider = fileVersionProvider;
            _env = env;
        }

        public string GetVersion()
        {
            var jsPath = Path.Combine(_env.WebRootPath, "js");
            if (!Directory.Exists(jsPath))
            {
                return "1";
            }

            using var sha256 = SHA256.Create();
            var files = Directory.GetFiles(jsPath, "*.js", SearchOption.AllDirectories).OrderBy(f => f);
            foreach (var file in files)
            {
                var relativePath = "/" + Path.GetRelativePath(_env.WebRootPath, file).Replace('\\', '/');
                var versionedPath = _fileVersionProvider.AddFileVersionToPath(PathString.Empty, relativePath);
                var bytes = Encoding.UTF8.GetBytes(versionedPath);
                sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha256.Hash!)[..8].ToLowerInvariant();
        }
    }
}
