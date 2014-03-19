﻿namespace IronFoundry.Warden.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Containers;
    using Protocol;

    public class WebApplicationCommand : ProcessCommand
    {
        private static readonly string RuntimeVersionTwo = "2.0";
        private static readonly string RuntimeVersionFour = "4.0";

        private readonly string port;
        private readonly string runtimeVersion;

        public WebApplicationCommand(Container container, string[] arguments, bool shouldImpersonate, ResourceLimits rlimits)
            : base(container, arguments, shouldImpersonate, rlimits)
        {
            if (arguments.IsNullOrEmpty())
            {
                throw new ArgumentException("Expected one or more arguments");
            }

            if (String.IsNullOrWhiteSpace(arguments[0]))
            {
                throw new ArgumentException("Expected port as first argument");
            }
            port = arguments[0];

            if (arguments.Length > 1)
            {
                if (arguments[1] != RuntimeVersionTwo && arguments[1] != RuntimeVersionFour)
                {
                    throw new ArgumentException("Expected runtime version value of '2.0' or '4.0', default is '4.0'.");
                }
                else
                {
                    runtimeVersion = arguments[1];
                }
            }
        }

        protected override TaskCommandResult DoExecute()
        {
            var webRoot = Path.Combine(container.Directory.FullName, "app");
            var args = String.Format(@"--webroot=""{0}"" --port={1}{2}", webRoot, port, runtimeVersion == null
                    ? String.Empty
                    : String.Concat(" --runtimeVersion=", runtimeVersion));

            return RunProcess(container.Directory.FullName, Path.Combine(container.Directory.FullName, "iishost.exe"), args);
        }
    }
}
