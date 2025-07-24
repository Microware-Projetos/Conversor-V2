using System;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace eCommerce.Server.Helpers
{
    public static class FileHelper
    {
        public static string SaveFile(IFormFile file, string directory)
        {
            Directory.CreateDirectory(directory);
            var fileName = Guid.NewGuid() + "_" + file.FileName;
            var filePath = Path.Combine(directory, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                file.CopyTo(stream);
            return filePath;
        }

        public static void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
} 