using System;
using System.Collections.Generic;
using System.IO;

namespace Codezerg.SqliteStorage.Blobs
{
    /// <summary>
    /// Helper utilities for working with MIME types and file extensions.
    /// </summary>
    public static class MimeTypeHelper
    {
        private static readonly Dictionary<string, string> _mimeTypes = new Dictionary<string, string>
        {
            // Documents
            { ".pdf", "application/pdf" },
            { ".doc", "application/msword" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { ".xls", "application/vnd.ms-excel" },
            { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            { ".ppt", "application/vnd.ms-powerpoint" },
            { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
            { ".txt", "text/plain" },
            { ".csv", "text/csv" },
            { ".json", "application/json" },
            { ".xml", "application/xml" },
            { ".html", "text/html" },
            { ".htm", "text/html" },

            // Images
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".gif", "image/gif" },
            { ".bmp", "image/bmp" },
            { ".svg", "image/svg+xml" },
            { ".webp", "image/webp" },
            { ".ico", "image/x-icon" },
            { ".tif", "image/tiff" },
            { ".tiff", "image/tiff" },

            // Audio
            { ".mp3", "audio/mpeg" },
            { ".wav", "audio/wav" },
            { ".ogg", "audio/ogg" },
            { ".m4a", "audio/mp4" },
            { ".flac", "audio/flac" },
            { ".aac", "audio/aac" },

            // Video
            { ".mp4", "video/mp4" },
            { ".avi", "video/x-msvideo" },
            { ".mov", "video/quicktime" },
            { ".wmv", "video/x-ms-wmv" },
            { ".flv", "video/x-flv" },
            { ".webm", "video/webm" },
            { ".mkv", "video/x-matroska" },

            // Archives
            { ".zip", "application/zip" },
            { ".rar", "application/vnd.rar" },
            { ".7z", "application/x-7z-compressed" },
            { ".tar", "application/x-tar" },
            { ".gz", "application/gzip" },

            // Other
            { ".bin", "application/octet-stream" },
            { ".exe", "application/vnd.microsoft.portable-executable" },
            { ".dll", "application/vnd.microsoft.portable-executable" }
        };

        /// <summary>
        /// Get MIME type from file extension.
        /// Returns "application/octet-stream" if extension not recognized.
        /// </summary>
        public static string GetMimeType(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "application/octet-stream";

            // Ensure extension starts with dot
            if (!extension.StartsWith("."))
                extension = "." + extension;

            return _mimeTypes.TryGetValue(extension.ToLowerInvariant(), out var mimeType)
                ? mimeType
                : "application/octet-stream";
        }

        /// <summary>
        /// Get extension from file path.
        /// </summary>
        public static string? GetExtension(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            var ext = Path.GetExtension(filePath);
            return string.IsNullOrEmpty(ext) ? null : ext.ToLowerInvariant();
        }

        /// <summary>
        /// Get MIME type from file path.
        /// </summary>
        public static string GetMimeTypeFromPath(string filePath)
        {
            var ext = GetExtension(filePath);
            return ext != null ? GetMimeType(ext) : "application/octet-stream";
        }

        /// <summary>
        /// Check if MIME type is an image.
        /// </summary>
        public static bool IsImage(string? mimeType) =>
            mimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Check if MIME type is video.
        /// </summary>
        public static bool IsVideo(string? mimeType) =>
            mimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Check if MIME type is audio.
        /// </summary>
        public static bool IsAudio(string? mimeType) =>
            mimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Check if MIME type is text.
        /// </summary>
        public static bool IsText(string? mimeType) =>
            mimeType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true;
    }
}
