﻿namespace IronFoundry.Warden.Tasks
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Containers;
    using IronFoundry.Warden.Shared.Messaging;
    using NLog;
    using Protocol;
    using Utilities;
    using IronFoundry.Warden.Containers.Messages;

    public abstract class ProcessCommand : TaskCommand
    {
        private readonly StringBuilder stdout = new StringBuilder();
        private readonly StringBuilder stderr = new StringBuilder();

        private readonly bool privileged;
        private readonly ResourceLimits rlimits;

        private readonly Logger log = LogManager.GetCurrentClassLogger();

        public ProcessCommand(IContainer container, string[] arguments, bool privileged, ResourceLimits rlimits)

            : base(container, arguments)
        {
            this.privileged = privileged;
            this.rlimits = rlimits;
        }

        public override TaskCommandResult Execute()
        {
            return DoExecute();
        }

        /*
         * Asynchronous execution
         */
        public event EventHandler<TaskCommandStatusEventArgs> StatusAvailable;

        public Task<TaskCommandResult> ExecuteAsync()
        {
            return Task.Run<TaskCommandResult>((Func<TaskCommandResult>)DoExecute);
        }

        protected abstract TaskCommandResult DoExecute();

        protected TaskCommandResult RunProcess(string workingDirectory, string executable, string processArguments)
        {
            log.Trace("Running process{0}: {1} {2}", privileged ? " (privileged)" : " (non-privileged)", executable, processArguments);

            var si = new CreateProcessStartInfo(executable, processArguments);
            si.WorkingDirectory = workingDirectory;

            var process = container.CreateProcess(si, !privileged);

            log.Trace("Process ID: '{0}'", process.Id);

            process.WaitForExit();

            int exitCode = process.ExitCode;
            log.Trace("Process ended with exit code: {0}", exitCode);

            return new TaskCommandResult(exitCode, null, null);
        }

        private void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string outputLine = e.Data + '\n';
                stdout.Append(outputLine);
                OnStatusAvailable(new TaskCommandStatus(null, outputLine, null));
            }
        }

        private void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string outputLine = e.Data + '\n';
                stderr.Append(outputLine);
                OnStatusAvailable(new TaskCommandStatus(null, null, outputLine));
            }
        }

        private void OnStatusAvailable(TaskCommandStatus status)
        {
            if (StatusAvailable != null)
            {
                StatusAvailable(this, new TaskCommandStatusEventArgs(status));
            }
        }
    }
}
