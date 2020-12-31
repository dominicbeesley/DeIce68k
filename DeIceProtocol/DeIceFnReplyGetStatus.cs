using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyGetStatus : DeIceFnReplyBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_GET_STAT;


        public byte ProcessorType { get; }
        public byte ComBufSize { get; }
        public DeIceTargetOptionFlags TargetOptions { get; }
        public ushort MappedAreaLow { get; }
        public ushort MappedAreaHigh { get; }
        public byte[] BreakPointInstruction { get; }
        public string TargetName { get; }
        public byte CallBouncePage { get; }
        public ushort CallBounceAddr { get; }

        public DeIceFnReplyGetStatus(byte procType, byte comBufSize, DeIceTargetOptionFlags opt, ushort lowMap, ushort hiMap, byte[] bpInst, string targetName, byte callPage, ushort callAddr)
        {
            this.ProcessorType = procType;
            this.ComBufSize = comBufSize;
            this.TargetOptions = opt;
            this.MappedAreaLow = lowMap;
            this.MappedAreaHigh = hiMap;
            this.BreakPointInstruction = bpInst;
            this.TargetName = targetName;
            this.CallBouncePage = callPage;
            this.CallBounceAddr = callAddr;
        }

    }
}
