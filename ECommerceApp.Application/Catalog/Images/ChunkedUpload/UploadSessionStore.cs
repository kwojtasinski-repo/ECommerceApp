using System;
using System.Collections.Concurrent;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    internal sealed class UploadSessionStore
    {
        private readonly ConcurrentDictionary<Guid, UploadSession> _sessions = new();

        public UploadSession Create(UploadSession session)
        {
            _sessions[session.SessionId] = session;
            return session;
        }

        public bool TryGet(Guid sessionId, out UploadSession session)
            => _sessions.TryGetValue(sessionId, out session);

        public void Remove(Guid sessionId)
            => _sessions.TryRemove(sessionId, out _);
    }
}
