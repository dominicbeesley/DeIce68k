using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public static class DeIceFnFactory
    {

        public static byte[] CreateDataFromReq(DeIceFnReqBase req)
        {
            byte[] ret;

            if (req is DeIceFnReqReadMem)
            {
                DeIceFnReqReadMem rm = (DeIceFnReqReadMem)req;
                ret = new byte[7];
                ret[0] = req.FunctionCode;
                ret[1] = 4;
                ret[2] = (byte)(rm.Address >> 16);
                ret[3] = (byte)(rm.Address >> 8);
                ret[4] = (byte)(rm.Address);
                ret[5] = rm.Len;
            }
            else if (req is DeIceFnReqWriteMem)
            {
                DeIceFnReqWriteMem rm = (DeIceFnReqWriteMem)req;
                ret = new byte[6 + rm.Data.Length];
                ret[0] = req.FunctionCode;
                ret[1] = (byte)(3 + rm.Data.Length);
                ret[2] = (byte)(rm.Address >> 16);
                ret[3] = (byte)(rm.Address >> 8);
                ret[4] = (byte)(rm.Address);
                rm.Data.CopyTo(ret, 5);
            }
            else if (req is DeIceFnReqRun)
            {
                ret = new byte[3];
                ret[0] = req.FunctionCode;
                ret[1] = 0;
            }
            else if (req is DeIceFnReqReadRegs)
            {
                ret = new byte[3];
                ret[0] = req.FunctionCode;
                ret[1] = 0;
            }
            else if (req is DeIceFnReqWriteRegs)
            {
                DeIceFnReqWriteRegs wr = (DeIceFnReqWriteRegs)req;

                var ll = (byte)wr.RegData.Length;

                ret = new byte[ll + 3];
                ret[0] = wr.FunctionCode;
                ret[1] = ll;

                Array.Copy(wr.RegData, 0, ret, 2, ll);

            }
            else if (req is DeIceFnReqSetBytes)
            {
                var sb = (DeIceFnReqSetBytes)req;

                int ll = sb.Items.Length * 4;

                if (ll > 255)
                    throw new ArgumentException($"DeIceFnReqSetBytes data too long, > 255 bytes in payload");

                ret = new byte[3 + ll];
                ret[0] = req.FunctionCode;
                ret[1] = (byte)(ll);

                for (int i = 0; i < sb.Items.Length; i++)
                {
                    WriteBEULong24(ret, 2 + i * 4, sb.Items[i].Address);
                    ret[5 + i * 4] = sb.Items[i].Data;
                }
            }
            else if (req is DeIceFnReqGetStatus)
            {
                ret = new byte[3];
                ret[0] = req.FunctionCode;
                ret[1] = 0;
            }
            else
                throw new NotImplementedException($"Unimplemented Request 0x{req.FunctionCode:X2} in {nameof(CreateDataFromReq)}");

            int ck = 0;
            for (int i = 0; i < ret.Length - 1; i++)
                ck = ck + ret[i];

            ret[ret.Length - 1] = (byte)(-ck);

            return ret;
        }


        public static DeIceFnReplyBase CreateReplyFromData(byte [] data)
        {
            DeIceFnReplyBase ret;

            if (data.Length < 3)
                throw new ArgumentException("data too short must be at list 3 bytes");

            byte l = data[1];
            byte fc = data[0];
            if (data.Length != l + 3)
                throw new ArgumentException("data length conflicts with data[1]");

            switch (fc)
            {
                case DeIceProtoConstants.FN_ERROR:
                    ret = new DeIceFnReplyError((l > 0) ? data[2] : (byte)0);
                    break;
                case DeIceProtoConstants.FN_READ_MEM:
                    ret = new DeIceFnReplyReadMem(data.Skip(2).Take(l).ToArray());
                    break;
                case DeIceProtoConstants.FN_WRITE_MEM:
                    ret = new DeIceFnReplyWriteMem(data[2] == 0);
                    break;
                case DeIceProtoConstants.FN_WRITE_RG:
                    ret = new DeIceFnReplyWriteRegs(data[2] == 0);
                    break;
                case DeIceProtoConstants.FN_READ_RG:
                case DeIceProtoConstants.FN_RUN_TARG:

                    int ll = data.Length - 3;
                    byte [] dat2 = new byte[ll];
                    Array.Copy(data, 2, dat2, 0, ll);

                    if (fc == DeIceProtoConstants.FN_READ_RG)
                        return new DeIceFnReplyReadRegs(dat2);
                    else
                        return new DeIceFnReplyRun(dat2);

                case DeIceProtoConstants.FN_SET_BYTES:

                    var bytes = new byte[l];
                    for (int i = 0; i < l; i++)
                    {
                        bytes[i] = data[2 + i];
                    }

                    return new DeIceFnReplySetBytes(bytes);
                case DeIceProtoConstants.FN_GET_STAT:
                    {
                        if (l < 8)
                            throw new ArgumentException("data too short of FN_GET_STAT reply");

                        byte procType = data[2];
                        byte comBufSize = data[3];
                        DeIceTargetOptionFlags opt = (DeIceTargetOptionFlags)data[4];
                        ushort lowMap = (ushort)(data[5] + data[6] << 8);
                        ushort hiMap = (ushort)(data[7] + data[8] << 8);
                        byte bpSize = data[9];

                        if (bpSize == 0)
                            throw new ArgumentException("zero length breakpoint instruction in FN_GET_STAT reply");
                        if (8 + bpSize > l)
                            throw new ArgumentException("Out of data in FN_GET_STAT reply");
                        byte [] bpInst = new byte[bpSize];
                        Array.Copy(data, 10, bpInst, 0, bpSize); // ReadBEUShort(data, 10);

                        int i;
                        i = 10 + bpSize;
                        while (i < data.Length - 1 && data[i] != 0)
                            i++;
                        string targetName = Encoding.GetEncoding(28591).GetString(data, 10 + bpSize, i - (10 + bpSize));
                        byte callPage = 0;
                        ushort callAddr = 0;
                        if ((opt & DeIceTargetOptionFlags.HasFNCall) != 0)
                        {
                            i++;
                            if (l < i - 2 + 3)
                                throw new ArgumentException("Out of data for call in FN_GET_STAT reply");
                            callPage = data[i];
                            callAddr = (ushort)(data[i + 1] + data[i + 2] << 8);
                        }

                        ret = new DeIceFnReplyGetStatus(procType, comBufSize, opt, lowMap, hiMap, bpInst, targetName, callPage, callAddr);

                    }
                    break;
                default:
                    throw new ArgumentException($"Unknown function code {fc:X2}");
            }
            
            return ret;
        }

        public static uint ReadBEULong(byte[] data, int index)
        {
            return (uint)(data[index + 3]
                + (data[index + 2] << 8)
                + (data[index + 1] << 16)
                + (data[index + 0] << 24)
                );
        }
        public static UInt16 ReadBEUShort(byte[] data, int index)
        {
            return (UInt16)(data[index + 1]
                + (data[index + 0] << 8)
                );
        }

        public static void WriteBEULong(byte[] data, int index, uint val)
        {
            data[index + 0] = (byte)(val >> 24);
            data[index + 1] = (byte)(val >> 16);
            data[index + 2] = (byte)(val >> 8);
            data[index + 3] = (byte)val;
        }
        public static void WriteBEUShort(byte[] data, int index, ushort val)
        {
            data[index + 0] = (byte)(val >> 8);
            data[index + 1] = (byte)val;
        }

        public static void WriteBEULong24(byte[] data, int index, uint val)
        {
            data[index + 0] = (byte)(val >> 16);
            data[index + 1] = (byte)(val >> 8);
            data[index + 2] = (byte)val;
        }

    }
}
