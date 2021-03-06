﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IronFoundry.Warden.Utilities;
using NLog;
using IronFoundry.Warden.Containers.Messages;
using IronFoundry.Warden.Configuration;

namespace IronFoundry.Warden.Containers
{
    public class ContainerManager : IContainerManager
    {
        private readonly ConcurrentDictionary<ContainerHandle, IContainerClient> containers =
            new ConcurrentDictionary<ContainerHandle, IContainerClient>();

        private readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly IContainerJanitor janitor;
        private readonly IWardenConfig wardenConfig;

        public ContainerManager(IContainerJanitor janitor, IWardenConfig wardenConfig)
        {
            this.janitor = janitor;
            this.wardenConfig = wardenConfig;
        }

        public IEnumerable<ContainerHandle> Handles
        {
            get { return containers.Keys; }
        }

        public void AddContainer(IContainerClient container)
        {
            if (!containers.TryAdd(container.Handle, container))
            {
                throw new WardenException("Could not add container '{0}' to collection!", container);
            }
        }

        public IContainerClient GetContainer(string handle)
        {
            var cHandle = new ContainerHandle(handle);
            IContainerClient retrieved;
            if (!containers.TryGetValue(cHandle, out retrieved))
            {
                // TODO: throw exception with message that matches ruby warden
                log.Warn("Expected to find container with handle '{0}'", handle);
            }
            return retrieved;
        }

        public void RestoreContainers(string containerRoot)
        {
            if (Directory.Exists(containerRoot))
            {
                Task.Run(async () =>
                               {
                                   // Recover containers primarily for deletion
                                   foreach (var dirPath in Directory.GetDirectories(containerRoot))
                                   {
                                       var handle = Path.GetFileName(dirPath);
                                       try
                                       {
                                           var container = ContainerProxy.Restore(handle);
                                           containers.TryAdd(container.Handle, container);
                                       }
                                       catch (Exception ex)
                                       {
                                           log.ErrorException(ex);
                                       }
                                   }

                                   try
                                   {
                                       await RemoveAllContainersAsync();
                                   }
                                   catch (Exception ex)
                                   {
                                       log.ErrorException(ex);
                                   }
                               });
            }
        }

        public async Task DestroyContainerAsync(IContainerClient container)
        {
            await DestroyContainerAsync(container.Handle);
        }

        public async Task DestroyContainerAsync(ContainerHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException("handle");
            }

            int? containerPort = null;

            IContainerClient removed;
            if (containers.TryRemove(handle, out removed))
            {
                containerPort = removed.AssignedPort;
            }

            await janitor.DestroyContainerAsync(handle, wardenConfig.ContainerBasePath, wardenConfig.TcpPort.ToString(), wardenConfig.DeleteContainerDirectories, containerPort);
        }

        public void Dispose()
        {
            // TODO - serialize, clear collection
        }

        private async Task RemoveAllContainersAsync()
        {
            foreach (IContainerClient client in containers.Values)
            {
                log.Info("Destroying stale container '{0}'", client.Handle);
                await DestroyContainerAsync(client);
            }
        }
    }
}