using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace IronFoundry.Warden.Containers
{
    public class JobObjectTests
    {
        [Fact]
        public void CreatingJobObjectGetsValidHandle()
        {
            JobObject jobObject = new JobObject();

            Assert.False(jobObject.Handle.IsInvalid);
        }

        [Fact]
        public void DisposingJobObjectCleansUpHandle()
        {
            JobObject jobObject = null;
            jobObject = new JobObject();

            jobObject.Dispose();

            Assert.Null(jobObject.Handle);
        }

        [Fact]
        public void CanAssignProcessToJobObject()
        {
            JobObject jobObject = new JobObject();

            Process p = null;
            try
            {
                p = Process.Start("cmd.exe");

                jobObject.AssignProcessToJob(p);

                bool isInJob;
                IronFoundry.Warden.PInvoke.NativeMethods.IsProcessInJob(p.Handle, jobObject.Handle, out isInJob);
                Assert.True(isInJob);
            }
            finally
            {
                p.Kill();
            }
        }

        [Fact]
        public void CanTerminateObjectsUnderJobObject()
        {
            JobObject jobObject = new JobObject();

            Process p = null;

            try
            {
                p = Process.Start("cmd.exe");

                jobObject.AssignProcessToJob(p);

                jobObject.TerminateProcesses();

                Assert.True(p.HasExited);
            }
            finally
            {
                if (!p.HasExited)
                    p.Kill();
            }
        }

        [Fact]
        public void TerminateThrowsIfJobObjectIsDisposed()
        {
            JobObject jobObject = new JobObject();
            jobObject.Dispose();

            Assert.Throws<ObjectDisposedException>(() => jobObject.TerminateProcesses());
        }
    }
}
