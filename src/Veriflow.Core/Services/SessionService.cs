using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Veriflow.Core.Models;

namespace Veriflow.Core.Services
{
    /// <summary>
    /// Service for managing Veriflow sessions (save/load)
    /// </summary>
    public class SessionService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Creates a new empty session
        /// </summary>
        public Session CreateNewSession(string name = "Untitled Session")
        {
            return new Session
            {
                SessionName = name,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };
        }

        /// <summary>
        /// Saves a session to a file
        /// </summary>
        public async Task SaveSessionAsync(Session session, string filePath)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            try
            {
                // Update last modified date
                session.LastModifiedDate = DateTime.Now;

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Serialize to JSON
                var json = JsonSerializer.Serialize(session, JsonOptions);

                // Write to file
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save session: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a session from a file
        /// </summary>
        public async Task<Session> LoadSessionAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Session file not found: {filePath}");

            try
            {
                // Read file
                var json = await File.ReadAllTextAsync(filePath);

                // Deserialize
                var session = JsonSerializer.Deserialize<Session>(json, JsonOptions);

                if (session == null)
                    throw new InvalidOperationException("Failed to deserialize session");

                return session;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid session file format: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load session: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates if a file is a valid session file
        /// </summary>
        public bool IsValidSessionFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var extension = Path.GetExtension(filePath);
                return extension.Equals(".vfsession", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
