namespace IronFoundry.Warden.Containers
{
    using IronFoundry.Warden.Test;
    using IronFoundry.Warden.PInvoke;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.DirectoryServices.AccountManagement;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Security;
    using Xunit;
    using System.Security;
    using System.Security.AccessControl;
    using IronFoundry.Warden.Test.TestSupport;
    using IronFoundry.Warden.Shared.Messaging;


 

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
            var si = new CreateProcessStartInfo("cmd.exe");

            using (var p = launcher.LaunchProcess(si, jobObject))
            {
                bool isInJob = false;

                NativeMethods.IsProcessInJob(p.Handle, jobObject.Handle, out isInJob);
                Assert.True(isInJob);
            }
        }

        [Fact]
        public void WhenProcessExitsImmediately_ReturnsProcess()
        {
            var si = new CreateProcessStartInfo("cmd.exe", "/C exit 0");

            using (var p = launcher.LaunchProcess(si, jobObject))
            {
                Assert.NotEqual(0, p.Id);
                Assert.True(p.HasExited);
                Assert.Equal(0, p.ExitCode);
            }
        }

        [Fact]
        public void WhenProcessFailsToStart_ReturnsProcessExitStatus()
        {
            var si = new CreateProcessStartInfo("cmd.exe", "/C exit 10");

            var ex = Assert.Throws<ProcessLauncherException>(() => launcher.LaunchProcess(si, jobObject));

            Assert.Equal(10, ex.Code);
        }

        [Fact]
        public void WhenProcessFailsToStart_ReturnsStandardOutputTail()
        {
            var si = new CreateProcessStartInfo("cmd.exe", "/C echo Failed to start && exit 10");

            var ex = Assert.Throws<ProcessLauncherException>(() => launcher.LaunchProcess(si, jobObject));

            Assert.Contains("Failed to start", ex.RemoteData);
        }

        [Fact]
        public void SuppliedArgumentsInStartupInfoIsPassedToRemoteProcess()
        {
            var tempFile = Path.Combine(tempDirectory, Guid.NewGuid().ToString());

            var si = new CreateProcessStartInfo("cmd.exe", string.Format(@"/K echo Boomerang > {0}", tempFile));

            using (var p = launcher.LaunchProcess(si, jobObject))
            {
                var output = File.ReadAllText(tempFile);
                Assert.Contains("Boomerang", output);
            }
        }

        [Fact]
        public void StartsProcessWithEnvironmentVariables()
        {
            var tempFile = Path.Combine(tempDirectory, Guid.NewGuid().ToString());

            var si = new CreateProcessStartInfo("cmd.exe", string.Format(@"/K echo %FOO% > {0}", tempFile));
            si.EnvironmentVariables["FOO"] = "BAR";
            
            using (var p = launcher.LaunchProcess(si, jobObject))
            {
                var output = File.ReadAllText(tempFile);
                Assert.Contains("BAR", output);
            }
        }

        [Fact]
        public void StartsProcessWithSpecifiedWorkingDirectory()
        {
            var tempFile = Path.Combine(tempDirectory, Guid.NewGuid().ToString());

            var si = new CreateProcessStartInfo("cmd.exe", string.Format(@"/K cd > {0}", tempFile));
            si.WorkingDirectory = tempDirectory;

            using (var p = launcher.LaunchProcess(si, jobObject))
            {
                var output = File.ReadAllText(tempFile);
                Assert.Contains(tempDirectory, output);
            }
        }

        [Fact]
        public void CanGetExitCodeFromCompletedProcess()
        {
            var si = new CreateProcessStartInfo("cmd.exe", @"/C sleep 1s && exit 0");
            si.WorkingDirectory = tempDirectory;

            using (var p = launcher.LaunchProcess(si, jobObject))
            {
                p.WaitForExit();
                Assert.Equal(0, p.ExitCode);
            }
        }

        [Fact]
        public void ProcessLaunchFailures_ThrowsAnException()
        {
            var si = new CreateProcessStartInfo("DoesNotExist.exe");

            Assert.Throws<ProcessLauncherException>(() => launcher.LaunchProcess(si, jobObject));
        }

        [Fact]
        public void ProcessLaunchFailures_ThrownExceptionIncludesErrorDetails()
        {
            var si = new CreateProcessStartInfo("DoesNotExist.exe");

            var ex = Record.Exception(() => launcher.LaunchProcess(si, jobObject));
            ProcessLauncherException processException = (ProcessLauncherException)ex;

            Assert.Equal(-32603, processException.Code);
            Assert.Contains("CreateProcessHandler", processException.RemoteData);
        }

        [Fact]
        public void ProcessLaunchFailures_ThrownExceptionIncludesRemoteStack()
        {
            var si = new CreateProcessStartInfo("DoesNotExist.exe");

            var ex = Record.Exception(() => launcher.LaunchProcess(si, jobObject));
            ProcessLauncherException processException = (ProcessLauncherException)ex;

            Assert.Contains("CreateProcessHandler", processException.RemoteData);
        }

        [FactAdminRequired]
        public void CanLaunchProcessAsAlternateUser()
        {
            string shortId = this.GetType().GetHashCode().ToString();
            string testUserName = "IFTest_" + shortId;

            using (var testUser = TestUserHolder.CreateUser(testUserName))
            {
                AddFileSecurity(tempDirectory, testUser.Principal.Name, FileSystemRights.FullControl, AccessControlType.Allow);

                var tempFile = Path.Combine(tempDirectory, Guid.NewGuid().ToString());

                var si = new CreateProcessStartInfo("cmd.exe", string.Format(@"/K echo %USERNAME% > {0}", tempFile))
                {
                    UserName = testUserName,
                    Password = testUser.Password.ToSecureString()
                };

                using (var p = launcher.LaunchProcess(si, jobObject))
                {
                    var output = File.ReadAllText(tempFile);
                    Assert.Contains(testUserName, output);
                    p.Kill();
                }
            }
        }

        private void AddFileSecurity(string file, string account, FileSystemRights rights, AccessControlType access)
        {

            var fileSecurity = File.GetAccessControl(file);

            fileSecurity.AddAccessRule(new FileSystemAccessRule(account, rights, access));

            File.SetAccessControl(file, fileSecurity);
        }

        // Can send stdinput to remote process

        //[Fact]
        //public void CanGetStdoutFromRemoteProcess()
        //{
        //    ProcessStartInfo si = new ProcessStartInfo("cmd.exe", @"/K echo Boomerang")
        //    {
        //        RedirectStandardOutput = true,
        //    };

        //    using (var p = launcher.LaunchProcess(si, jobObject))
        //    {
        //        StringBuilder builder = new StringBuilder();

        //        var output = p.StandardOutput.ReadLine();
        //        Assert.Contains("Boomerang", output);
        //    }
        //}

        // Can get errorinfo from remote process
    }
}
