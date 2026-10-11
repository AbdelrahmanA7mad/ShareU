using System;
using System.ComponentModel;

namespace ShareU
{
    public class SharedFileItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime DateModified { get; set; }

        public string FormattedSize => IsDirectory ? "Folder" : FormatBytes(Size);
        public string ItemType => IsDirectory ? "📁 Folder" : "📄 File";

        public event PropertyChangedEventHandler? PropertyChanged;

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
