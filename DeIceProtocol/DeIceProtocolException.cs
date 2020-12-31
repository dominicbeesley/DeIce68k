using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceProtocolException : Exception
    {
        public DeIceProtocolException(string message) : base(message) { }
    }
}
