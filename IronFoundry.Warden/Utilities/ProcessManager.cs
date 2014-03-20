namespace IronFoundry.Warden.Utilities
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Containers;
    using IronFoundry.Warden.Shared.Messaging;
    using NLog;

    public class ProcessManager : IDisposable
    {
        private readonly JobObject jobObject;
        private readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<int, IProcess> processes = new ConcurrentDictionary<int, IProcess>();
        private readonly ProcessLauncher processLauncher;
        private readonly string containerUser;

        private readonly Func<Process, bool> processMatchesUser;

        public ProcessManager(string containerUser) : this(new JobObject(), new ProcessLauncher(), containerUser)
        {
        }

        public ProcessManager(JobObject jobObject, ProcessLauncher processLauncher, string containerUser)
        {
            if (containerUser == null)
                throw new ArgumentNullException("containerUser");

            this.jobObject = jobObject;
            this.processLauncher = processLauncher;
            this.containerUser = containerUser;

            this.processMatchesUser = (process) =>
                {
                    string processUser = process.GetUserName();
                    return processUser == containerUser && !process.HasExited;
                };
        }

        public bool HasProcesses
        {
            get { return processes.Count > 0; }
        }

        public bool ContainsProcess(int processId)
        {
            return processes.ContainsKey(processId);
        }

        public virtual IProcess GetProcessById(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return new RealProcessWrapper(process);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public virtual IProcess CreateProcess(CreateProcessStartInfo startInfo)
        {
            var process = processLauncher.LaunchProcess(startInfo, jobObject);
            if (!processes.TryAdd(process.Id, process))
            {
                throw new InvalidOperationException("A process with the id " + process.Id + " is already being tracked.");
            }

            process.Exited += process_Exited;

            return process;
        }

        public void Dispose()
        {
            jobObject.Dispose();
            processLauncher.Dispose();
        }

        public void RestoreProcesses()
        {
            GetMatchingUserProcesses().Foreach(log,
                (p) =>
                {
                    var wrappedProcess = new RealProcessWrapper(p);
                    if (processes.TryAdd(wrappedProcess.Id, wrappedProcess))
                    {
                        log.Trace("Added process with PID '{0}' to container with user '{1}'", wrappedProcess.Id, containerUser);
                    }
                    else
                    {
                        log.Trace("Could NOT add process with PID '{0}' to container with user '{1}'", wrappedProcess.Id, containerUser);
                    }
                });
        }

        public void StopProcesses()
        {
            var processList = processes.Values.ToListOrNull();
            Debug.Assert(processList.All(p => {
                bool isInJob = false;
                IronFoundry.Warden.PInvoke.NativeMethods.IsProcessInJob(p.Handle, jobObject.Handle, out isInJob);
                return isInJob;
            }));

            jobObject.TerminateProcesses();
            
            processList.Clear();
        }

        public IEnumerable<Process> GetMatchingUserProcesses()
        {
            var allProcesses = Process.GetProcesses();
            return allProcesses.Where(p => processMatchesUser(p));
        }

        private void process_Exited(object sender, EventArgs e)
        {
            var process = (IProcess)sender;
            process.Exited -= process_Exited;

            log.Trace("Process exited PID '{0}' exit code '{1}'", process.Id, process.ExitCode);

            RemoveProcess(process.Id);
        }

        private void RemoveProcess(int pid)
        {
            IProcess removed;
            if (processes.ContainsKey(pid) && !processes.TryRemove(pid, out removed))
            {
                log.Warn("Could not remove process '{0}' from collection!", pid);
            }
        }

        public ProcessStats GetProcessStats()
        {
            var cpuStatistics = jobObject.GetCpuStatistics();
            var processIds = jobObject.GetProcessIds();

            var processes = processIds
                .Select(id => GetProcessById(id))
                .Where(p => p != null)
                .ToList();

            long privateMemory = 0;
            long pagedMemory = 0;
            long workingSet = 0;

            foreach (var process in processes)
            {
                privateMemory += process.PrivateMemoryBytes;
                pagedMemory += process.PagedMemoryBytes;
                workingSet += process.WorkingSet;
            }

            return new ProcessStats
            {
                TotalProcessorTime = cpuStatistics.TotalKernelTime + cpuStatistics.TotalUserTime,
                TotalUserProcessorTime = cpuStatistics.TotalUserTime,
                PrivateMemory = privateMemory,
                PagedMemory = pagedMemory,
                WorkingSet = workingSet,
            };
        }

        class RealProcessWrapper : IProcess
        {
            private readonly Process process;
            public event EventHandler Exited;

            public RealProcessWrapper(Process process)
            {
                this.process = process;
                Id = process.Id;
                process.Exited += (o, e) => this.OnExited();
            }

            public int Id { get; private set; }

            public int ExitCode
            {
                get { return process.ExitCode; }
            }

            public IntPtr Handle
            {
                get { return process.Handle; }
            }

            public bool HasExited
            {
                get { return process.HasExited; }
            }

            public TimeSpan TotalProcessorTime
            {
                get { return process.TotalProcessorTime; }
            }

            public TimeSpan TotalUserProcessorTime
            {
                get { return process.UserProcessorTime; }
            }

            public void Kill()
            {
                process.Kill();
            }

            protected virtual void OnExited()
            {
                var handlers = Exited;
                if (handlers != null)
                {
                    handlers.Invoke(this, EventArgs.Empty);
                }
            }

            public long PrivateMemoryBytes
            {
                get { return process.PrivateMemorySize64; }
            }

            public long PagedMemoryBytes
            {
                get { return process.PagedMemorySize64; }
            }

            public long WorkingSet
            {
                get { return process.WorkingSet64; }
            }

            public void Dispose()
            {
                process.Dispose();
            }

            public void WaitForExit()
            {
                process.WaitForExit();
            }
        }
    }

    public struct ProcessStats
    {
        public TimeSpan TotalProcessorTime { get; set; }
        public TimeSpan TotalUserProcessorTime { get; set; }
        public long PrivateMemory { get; set; }
        public long PagedMemory { get; set; }
        public long WorkingSet { get; set; }
    }
}
