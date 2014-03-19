using IronFoundry.Warden.Containers;
using IronFoundry.Warden.PInvoke;
using IronFoundry.Warden.Shared.Messaging;
using IronFoundry.Warden.Utilities;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace IronFoundry.Warden.Test
{
    public class ProcessManagerTests
    {
        [Fact]
        public void StoppingProcessManager_StopsProcesses()
        {
            var launcher = new ProcessLauncher();
            var manager = new ProcessManager(launcher, "TestUser");

            var si = new CreateProcessStartInfo("cmd.exe");
            using (var process = manager.CreateProcess(si))
            {
                IntPtr pInt = process.Handle;
                manager.StopProcesses();
                
                uint exitCode = 0;
                NativeMethods.GetExitCodeProcess(pInt, out exitCode);

                Assert.NotEqual(
                    (uint)NativeMethods.ProcessExitCode.StillActive, 
                    exitCode);
            }
        }

    }
}
