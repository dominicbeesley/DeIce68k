using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyGetStatus : DeIceFnReplyBase
    {
        private static DeIceFnReplyGetStatus _default = new DeIceFnReplyGetStatus(
            68, 0x80, 0, 0, 0xFFFF, 0x4E4F, "Unknown 68K", 0, 0 
            );
        
        public static DeIceFnReplyGetStatus Default { get => _default; }

        public override byte FunctionCode => DeIceProtoConstants.FN_GET_STAT;


        public byte ProcessorType { get; }
        public byte ComBufSize { get; }
        public DeIceTargetOptionFlags TargetOptions { get; }
        public ushort MappedAreaLow { get; }
        public ushort MappedAreaHigh { get; }
        public ushort BreakPointInstruction { get; }
        public string TargetName { get; }
        public byte CallBouncePage { get; }
        public ushort CallBounceAddr { get; }

        public DeIceFnReplyGetStatus(byte procType, byte comBufSize, DeIceTargetOptionFlags opt, ushort lowMap, ushort hiMap, ushort bpInst, string targetName, byte callPage, ushort callAddr)
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
