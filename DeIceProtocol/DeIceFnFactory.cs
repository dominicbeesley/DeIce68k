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

                ret = new byte[0x4C + 3];
                ret[0] = req.FunctionCode;
                ret[1] = 0x4C;
                ret[2] = wr.Regs.TargetStatus;
                WriteBEULong(ret, 0x04, wr.Regs.A7u);
                WriteBEULong(ret, 0x08, wr.Regs.A7s);
                WriteBEULong(ret, 0x0C, wr.Regs.D0);
                WriteBEULong(ret, 0x10, wr.Regs.D1);
                WriteBEULong(ret, 0x14, wr.Regs.D2);
                WriteBEULong(ret, 0x18, wr.Regs.D3);
                WriteBEULong(ret, 0x1C, wr.Regs.D4);
                WriteBEULong(ret, 0x20, wr.Regs.D5);
                WriteBEULong(ret, 0x24, wr.Regs.D6);
                WriteBEULong(ret, 0x28, wr.Regs.D7);
                WriteBEULong(ret, 0x2C, wr.Regs.A0);
                WriteBEULong(ret, 0x30, wr.Regs.A1);
                WriteBEULong(ret, 0x34, wr.Regs.A2);
                WriteBEULong(ret, 0x38, wr.Regs.A3);
                WriteBEULong(ret, 0x3C, wr.Regs.A4);
                WriteBEULong(ret, 0x40, wr.Regs.A5);
                WriteBEULong(ret, 0x44, wr.Regs.A6);
                WriteBEUShort(ret, 0x48, (ushort)wr.Regs.SR);
                WriteBEULong(ret, 0x4A, wr.Regs.PC);


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
                case DeIceProtoConstants.FN_WRITE_RG:
                    ret = new DeIceFnReplyWriteRegs(data[2] == 0);
                    break;
                case DeIceProtoConstants.FN_READ_RG:
                case DeIceProtoConstants.FN_RUN_TARG:

                    if (l < 0x4C)
                        throw new ArgumentException("data too short FN_READ_RG/FN_RUN_TARG reply");

                    DeIceRegisters regs = new() {


                        TargetStatus = data[2],
                        A7u = ReadBEULong(data, 0x04),
                        A7s = ReadBEULong(data, 0x08),
                        D0 = ReadBEULong(data, 0x0C),
                        D1 = ReadBEULong(data, 0x10),
                        D2 = ReadBEULong(data, 0x14),
                        D3 = ReadBEULong(data, 0x18),
                        D4 = ReadBEULong(data, 0x1C),
                        D5 = ReadBEULong(data, 0x20),
                        D6 = ReadBEULong(data, 0x24),
                        D7 = ReadBEULong(data, 0x28),
                        A0 = ReadBEULong(data, 0x2C),
                        A1 = ReadBEULong(data, 0x30),
                        A2 = ReadBEULong(data, 0x34),
                        A3 = ReadBEULong(data, 0x38),
                        A4 = ReadBEULong(data, 0x3C),
                        A5 = ReadBEULong(data, 0x40),
                        A6 = ReadBEULong(data, 0x44),
                        SR = ReadBEUShort(data, 0x48),
                        PC = ReadBEULong(data, 0x4A)
                    };

                    if (fc == DeIceProtoConstants.FN_READ_RG)
                        return new DeIceFnReplyReadRegs(regs);
                    else
                        return new DeIceFnReplyRun(regs);

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
                        byte[] bpInst = new byte[bpSize];
                        int i;
                        for (i = 0; i < bpSize; i++)
                            bpInst[i] = data[10 + i];

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

        private static UInt32 ReadBEULong(byte[] data, int index)
        {
            return (UInt32)(data[index + 3]
                + (data[index + 2] << 8)
                + (data[index + 1] << 16)
                + (data[index + 0] << 24)
                );
        }
        private static UInt16 ReadBEUShort(byte[] data, int index)
        {
            return (UInt16)(data[index + 1]
                + (data[index + 0] << 8)
                );
        }

        private static void WriteBEULong(byte[] data, int index, uint val)
        {
            data[index + 0] = (byte)(val >> 24);
            data[index + 1] = (byte)(val >> 16);
            data[index + 2] = (byte)(val >> 8);
            data[index + 3] = (byte)val;
        }
        private static void WriteBEUShort(byte[] data, int index, ushort val)
        {
            data[index + 0] = (byte)(val >> 8);
            data[index + 1] = (byte)val;
        }
    }
}
