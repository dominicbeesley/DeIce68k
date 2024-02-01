using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public abstract class DeIceFnReplyStatusBase : DeIceFnReplyBase
    {
        public bool Success { get; }
        public DeIceFnReplyStatusBase(bool success) { this.Success = success; } 
    }
}
