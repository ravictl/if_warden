﻿using System.Net;
using IronFoundry.Warden.Configuration;
using IronFoundry.Warden.Utilities;
using IronFoundry.Warden.Containers.Messages;
using System.Collections.Generic;
using System;
using System.IO;

namespace IronFoundry.Warden.Containers
{
    public interface IResourceHolder
    {
        ushort? AssignedPort { get; set; }
        IContainerUser User { get; }
        IContainerDirectory Directory { get; }
        ContainerHandle Handle { get; }
        JobObject JobObject { get; }
        ILocalTcpPortManager LocalTcpPortManager { get; }

        void Destroy();
    }

    public class ContainerResourceHolder : IResourceHolder
    {
        private const int TerminateWaitTimeout = 2000; // ms
        private readonly bool deleteDirectories;

        public ContainerResourceHolder(
            ContainerHandle handle, 
            IContainerUser user, 
            IContainerDirectory directory, 
            JobObject jobObject, 
            ILocalTcpPortManager localTcpPortManager, 
            bool deleteDirectories)
        {
            Handle = handle;
            User = user;
            Directory = directory;
            JobObject = jobObject;
            LocalTcpPortManager = localTcpPortManager;
            this.deleteDirectories = deleteDirectories;
        }

        public ushort? AssignedPort { get; set; }
        public IContainerUser User { get; private set; }
        public IContainerDirectory Directory { get; private set; }
        public ContainerHandle Handle { get; private set; }
        public JobObject JobObject { get; private set; }
        public ILocalTcpPortManager LocalTcpPortManager { get; private set; }

        public void Destroy()
        {
            JobObject.TerminateProcessesAndWait(TerminateWaitTimeout);
            JobObject.Dispose();

            try
            {
                if (deleteDirectories)
                    Directory.Delete();
            }
            catch (IOException)
            {
            }

            if (AssignedPort.HasValue)
            {
                try
                {
                    LocalTcpPortManager.ReleaseLocalPort(AssignedPort.Value, User.UserName);
                }
                catch (Exception)
                {
                    // TODO: Log
                }
            }

            try
            {
                User.Delete();
            }
            catch (System.Exception)
            {
                // TODO: Add logging for cleanup case
            }
        }

        public static IResourceHolder Create(IWardenConfig config)
        {   
            var handle = new ContainerHandle();
            var user = ContainerUser.CreateUser(handle, new LocalPrincipalManager(new DesktopPermissionManager()));
            var directory = new ContainerDirectory(handle, user, config.ContainerBasePath, true);
            var localPortManager = new LocalTcpPortManager(new FirewallManager(), new NetShRunner());
            var resoureHolder = new ContainerResourceHolder(
                handle,
                user,
                directory,
                new JobObject(handle.ToString()),
                localPortManager,
                config.DeleteContainerDirectories
                );

            return resoureHolder;
        }

        public static IResourceHolder CreateForDestroy(IWardenConfig config, ContainerHandle handle, ushort? assignedPort)
        {
            var user = new TempUser(handle, new LocalPrincipalManager(new DesktopPermissionManager()));
            var directory = new TempDirectory(handle, config.ContainerBasePath);
            var localPortManager = new LocalTcpPortManager(new FirewallManager(), new NetShRunner());
            var resoureHolder = new ContainerResourceHolder(
                handle,
                user,
                directory,
                new JobObject(handle.ToString()),
                localPortManager,
                config.DeleteContainerDirectories
                );

            resoureHolder.AssignedPort = assignedPort;
 
            return resoureHolder;
        }

        class TempUser : IContainerUser
        {
            private readonly IUserManager userManager;

            public TempUser(string uniqueId, IUserManager userManager)
            {
                this.userManager = userManager;
                UserName = ContainerUser.CreateUserName(uniqueId);
            }

            public string UserName { get; private set; }

            public NetworkCredential GetCredential()
            {
                throw new System.NotImplementedException();
            }

            public void Delete()
            {
                userManager.DeleteUser(UserName);
            }
        }

        /// <summary>
        /// Exists to create a temporary implementation primarily for deleting the directory.
        /// </summary>
        class TempDirectory : IContainerDirectory
        {
            private readonly string fullPath;

            public TempDirectory(ContainerHandle handle, string containerBasePath)
            {
                fullPath = Path.Combine(containerBasePath, handle);
            }

            public string FullName
            {
                get { return fullPath; }
            }

            public void BindMounts(IEnumerable<BindMount> mounts)
            {
                throw new NotImplementedException();
            }

            public void Delete()
            {
                if (System.IO.Directory.Exists(fullPath))
                {
                    System.IO.Directory.Delete(fullPath, true);
                }
            }
        }
    }
}