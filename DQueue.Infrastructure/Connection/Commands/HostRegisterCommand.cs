﻿using DQueue.Infrastructure.Connection.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.Infrastructure.Connection.Commands
{
    public class HostRegisterCommand : BaseCommand
    {
        public HostRegisterCommand()
        {
        }

        public override object GetCommandBody()
        {
            return new HostInfo
            {

            };
        }
    }
}