﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public class ReceptionContext
    {
        private Action<ReceptionStatus> _action;

        public ReceptionContext(Action<ReceptionStatus> action)
        {
            _action = action;
        }

        public void Continue()
        {
            _action(ReceptionStatus.Listen);
        }
    }
}
