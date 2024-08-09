using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace MiniGameRouter.Benchmark;

public class DefaultConfig : ManualConfig
{
    public DefaultConfig()
    {
        AddJob(Job.MediumRun.WithGcServer(true).WithGcForce(true).WithId("ServerForce"));
        // AddJob(Job.MediumRun.WithGcServer(true).WithGcForce(false).WithId("Server"));
        // AddJob(Job.MediumRun.WithGcServer(false).WithGcForce(true).WithId("Workstation"));
        // AddJob(Job.MediumRun.WithGcServer(false).WithGcForce(false).WithId("WorkstationForce"));
    }
}