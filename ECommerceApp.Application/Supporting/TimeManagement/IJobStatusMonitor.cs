using ECommerceApp.Application.Supporting.TimeManagement.Models;
using System.Collections.Generic;

namespace ECommerceApp.Application.Supporting.TimeManagement
{
    public interface IJobStatusMonitor
    {
        JobExecutionRecord? GetLatest(string jobName);
        IReadOnlyList<JobExecutionRecord> GetRecent(string jobName, int count);
        void Record(JobExecutionRecord record);
    }
}
