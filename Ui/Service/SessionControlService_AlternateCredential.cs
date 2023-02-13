﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using ProtocolHostStatus = _1RM.View.Host.ProtocolHosts.ProtocolHostStatus;
using _1RM.Service.DataSource;
using System.Collections.Generic;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        private static void ApplyAlternateCredential(ref ProtocolBase serverClone, string assignCredentialName)
        {
            if (serverClone is not ProtocolBaseWithAddressPortUserPwd protocol 
                || protocol.AlternateCredentials == null 
                || protocol.AlternateCredentials.Count <= 0) 
                return;


            bool addressIsSwitched = false;
            bool useAssignCredential = false;

            var newCredential = protocol.GetCredential();

            // auto switching address
            if (protocol.IsAutoAlternateAddressSwitching == true)
            {
                int milliseconds = 5000;
                // TODO show a progress window
                var credentials = new List<Credential>()
                {
                    newCredential,
                };
                credentials.AddRange(protocol.AlternateCredentials
                    .Where(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port)));

                var cts = new CancellationTokenSource();
                var tasks = credentials.Select(x =>
                        Task.Factory.StartNew(() =>
                        {
                            if (Credential.TestAddressPortIsAvailable(protocol, x, milliseconds))
                            {
                                return x;
                            }
                            Thread.Sleep(milliseconds);
                            return null;
                        }, cts.Token))
                    .ToArray();
                var t = Task.WaitAny(tasks);
                cts.Cancel(false);
                if (tasks[t].Result != null)
                {
                    SimpleLogHelper.Info("Auto switching address.");
                    newCredential.SetAddress(tasks[t].Result!);
                    newCredential.SetPort(tasks[t].Result!);
                    addressIsSwitched = true;
                }
            }

            // use assign credential
            var assignCredentialNameClone = assignCredentialName;
            var assignCredential = protocol.AlternateCredentials.FirstOrDefault(x => x.Name == assignCredentialNameClone);
            if (assignCredential != null)
            {
                if (addressIsSwitched)
                {
                    var a = newCredential.SetUserName(assignCredential);
                    var b = newCredential.SetPassword(assignCredential);
                    useAssignCredential = a || b;
                }
                else
                {
                    useAssignCredential = newCredential.SetCredential(assignCredential);
                }
            }


            if (addressIsSwitched || useAssignCredential)
            {
                protocol.SetCredential(newCredential);
            }

        }
    }
}