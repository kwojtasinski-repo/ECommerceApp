using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using System.Collections.Generic;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class InMemoryJobStatusMonitor : IJobStatusMonitor
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, List<JobExecutionRecord>> _buffer = new();
        private const int MaxPerJob = 50;

        public void Record(JobExecutionRecord record)
        {
            lock (_lock)
            {
                if (!_buffer.TryGetValue(record.JobName, out var list))
                {
                    list = new List<JobExecutionRecord>();
                    _buffer[record.JobName] = list;
                }
                list.Add(record);
                if (list.Count > MaxPerJob)
                    list.RemoveAt(0);
            }
        }

        public JobExecutionRecord? GetLatest(string jobName)
        {
            lock (_lock)
            {
                if (_buffer.TryGetValue(jobName, out var list) && list.Count > 0)
                    return list[^1];
                return null;
            }
        }

        public IReadOnlyList<JobExecutionRecord> GetRecent(string jobName, int count)
        {
            lock (_lock)
            {
                if (!_buffer.TryGetValue(jobName, out var list))
                    return System.Array.Empty<JobExecutionRecord>();
                return list.Count <= count
                    ? list.ToArray()
                    : list.GetRange(list.Count - count, count).ToArray();
            }
        }
    }
}
