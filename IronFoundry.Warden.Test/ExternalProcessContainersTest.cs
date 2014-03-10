namespace IronFoundry.Warden.Containers
{
    using IronFoundry.Warden.PInvoke;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ExternalProcessContainersTest : IDisposable
    {
        JobObject jobObject = new JobObject();
        ProcessLauncher launcher = new ProcessLauncher();
        string tempDirectory;

        public ExternalProcessContainersTest()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
        }

        public void Dispose()
        {
            jobObject.TerminateProcesses();
            jobObject.Dispose();
            jobObject = null;

            Directory.Delete(tempDirectory, true);
        }

        [Fact]
        public void StartedProcessLaunchUnderJobObject()
        {
            ProcessStartInfo si = new ProcessStartInfo("cmd.exe");

            using (Process p = launcher.LaunchProcess(si, jobObject))
            {
                bool isInJob = false;

                NativeMethods.IsProcessInJob(p.Handle, jobObject.Handle, out isInJob);
                Assert.True(isInJob);
            }
        }

        [Fact]
        public void SuppliedArgumentsInStartupInfoIsPassedToRemoteProcess()
        {
            var tempFile = Path.Combine(tempDirectory, Guid.NewGuid().ToString());

            ProcessStartInfo si = new ProcessStartInfo("cmd.exe", string.Format(@"/K echo Boomerang > {0}", tempFile));

            using (Process p = launcher.LaunchProcess(si, jobObject))
            {
                var output = File.ReadAllText(tempFile);
                Assert.Contains("Boomerang", output);
            }
        }

        [Fact]
        public void ProcessLaunchFailures_ThrowsAnException()
        {
            ProcessStartInfo si = new ProcessStartInfo("DoesNotExist.exe");

            Assert.Throws<ProcessLauncherException>(() => launcher.LaunchProcess(si, jobObject));
        }

        [Fact]
        public void ProcessLaunchFailures_ThrownExceptionIncludesErrorDetails()
        {
            ProcessStartInfo si = new ProcessStartInfo("DoesNotExist.exe");

            var ex = Record.Exception(()=> launcher.LaunchProcess(si, jobObject));
            ProcessLauncherException processException = (ProcessLauncherException) ex;

            Assert.Equal(-32603, processException.Code);
            Assert.Contains("CreateProcessHandler", processException.RemoteData);
        }

        [Fact]
        public void ProcessLaunchFailures_ThrownExceptionIncludesRemoteStack()
        {
            ProcessStartInfo si = new ProcessStartInfo("DoesNotExist.exe");

            var ex = Record.Exception(() => launcher.LaunchProcess(si, jobObject));
            ProcessLauncherException processException = (ProcessLauncherException)ex;

            Assert.Contains("CreateProcessHandler", processException.RemoteData);
        }

        // Can start process as specific user        
        // Can send stdinput to remote process
        // Can get stdouput from remote process
        // Can get errorinfo from remote process
    }
}
