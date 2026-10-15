using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    /// <summary>
    /// Upload sessions: an in-memory cache backed by <c>{root}/{sessionId}/session.json</c> plus the
    /// <c>{chunkId}.part</c> files, so sessions survive an application restart.
    /// </summary>
    internal sealed class UploadSessionStore
    {
        public const string MetadataFileName = "session.json";
        public const string PartExtension = ".part";

        private readonly ConcurrentDictionary<Guid, UploadSession> _sessions = new();
        private readonly object _gate = new();
        private readonly string _root;

        public UploadSessionStore(IOptions<ChunkedUploadOptions> options)
        {
            var configured = options.Value.TempPath;
            _root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Directory.GetCurrentDirectory(), "Upload", "Temp")
                : configured;
        }

        public string GetSessionDirectory(Guid sessionId) => Path.Combine(_root, sessionId.ToString("D"));

        public string GetPartPath(Guid sessionId, int chunkId)
            => Path.Combine(GetSessionDirectory(sessionId), chunkId + PartExtension);

        /// <summary>Atomically enforces the per-user session cap and persists the new session.</summary>
        public bool TryCreate(UploadSession session, int maxOpenSessionsPerUser)
        {
            lock (_gate)
            {
                if (CountOpenSessions(session.UserId) >= maxOpenSessionsPerUser)
                {
                    return false;
                }

                var dir = GetSessionDirectory(session.SessionId);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, MetadataFileName), JsonSerializer.Serialize(new PersistedSession(session)));
                _sessions[session.SessionId] = session;
                return true;
            }
        }

        public bool TryGet(Guid sessionId, out UploadSession session)
        {
            if (_sessions.TryGetValue(sessionId, out session))
            {
                return true;
            }

            lock (_gate)
            {
                if (_sessions.TryGetValue(sessionId, out session))
                {
                    return true;
                }

                session = LoadFromDisk(sessionId);
                if (session == null)
                {
                    return false;
                }

                _sessions[sessionId] = session;
                return true;
            }
        }

        public void Remove(Guid sessionId)
        {
            lock (_gate)
            {
                _sessions.TryRemove(sessionId, out _);
                DeleteDirectory(GetSessionDirectory(sessionId));
            }
        }

        /// <summary>Removes (memory and disk) every session directory whose last activity is older than the cutoff.</summary>
        public int RemoveExpired(DateTime cutoffUtc)
        {
            lock (_gate)
            {
                if (!Directory.Exists(_root))
                {
                    return 0;
                }

                var removed = 0;
                foreach (var dir in Directory.EnumerateDirectories(_root))
                {
                    if (GetLastActivityUtc(dir) >= cutoffUtc)
                    {
                        continue;
                    }

                    if (Guid.TryParse(Path.GetFileName(dir), out var id))
                    {
                        _sessions.TryRemove(id, out _);
                    }

                    DeleteDirectory(dir);
                    removed++;
                }

                return removed;
            }
        }

        private int CountOpenSessions(string userId)
        {
            if (!Directory.Exists(_root))
            {
                return 0;
            }

            var count = 0;
            foreach (var dir in Directory.EnumerateDirectories(_root))
            {
                if (!Guid.TryParse(Path.GetFileName(dir), out var id))
                {
                    continue;
                }

                if (!_sessions.TryGetValue(id, out var session))
                {
                    session = LoadFromDisk(id);
                    if (session != null)
                    {
                        _sessions[id] = session;
                    }
                }

                if (session != null && string.Equals(session.UserId, userId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static DateTime GetLastActivityUtc(string dir)
        {
            var latest = Directory.GetLastWriteTimeUtc(dir);
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var written = File.GetLastWriteTimeUtc(file);
                if (written > latest)
                {
                    latest = written;
                }
            }

            return latest;
        }

        private UploadSession LoadFromDisk(Guid sessionId)
        {
            var dir = GetSessionDirectory(sessionId);
            var metadataPath = Path.Combine(dir, MetadataFileName);
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            PersistedSession persisted;
            try
            {
                persisted = JsonSerializer.Deserialize<PersistedSession>(File.ReadAllText(metadataPath));
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                return null;
            }

            if (persisted == null || persisted.SessionId != sessionId || string.IsNullOrEmpty(persisted.UserId)
                || persisted.FileSizeBytes <= 0 || persisted.ChunkSize <= 0)
            {
                return null;
            }

            // Derived, not trusted from disk.
            var totalChunks = (int)((persisted.FileSizeBytes + persisted.ChunkSize - 1) / persisted.ChunkSize);
            var session = new UploadSession
            {
                SessionId = sessionId,
                UserId = persisted.UserId,
                FileName = persisted.FileName,
                FileSizeBytes = persisted.FileSizeBytes,
                ItemId = persisted.ItemId,
                ChunkSize = persisted.ChunkSize,
                TotalChunks = totalChunks,
                CreatedUtc = persisted.CreatedUtc
            };

            foreach (var part in Directory.EnumerateFiles(dir, "*" + PartExtension))
            {
                if (int.TryParse(Path.GetFileNameWithoutExtension(part), out var chunkId)
                    && chunkId >= 1 && chunkId <= totalChunks
                    && new FileInfo(part).Length == session.ExpectedChunkLength(chunkId))
                {
                    session.ReceivedChunkIds.Add(chunkId);
                }
            }

            return session;
        }

        private static void DeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch (IOException)
            {
                // best-effort; the cleanup job retries once the session is old enough
            }
            catch (UnauthorizedAccessException)
            {
                // best-effort, same as above
            }
        }

        private sealed class PersistedSession
        {
            public PersistedSession()
            {
            }

            public PersistedSession(UploadSession session)
            {
                SessionId = session.SessionId;
                UserId = session.UserId;
                FileName = session.FileName;
                FileSizeBytes = session.FileSizeBytes;
                ItemId = session.ItemId;
                ChunkSize = session.ChunkSize;
                CreatedUtc = session.CreatedUtc;
            }

            public Guid SessionId { get; set; }
            public string UserId { get; set; }
            public string FileName { get; set; }
            public long FileSizeBytes { get; set; }
            public int? ItemId { get; set; }
            public int ChunkSize { get; set; }
            public DateTime CreatedUtc { get; set; }
        }
    }
}
