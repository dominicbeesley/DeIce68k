using DeIceProtocol;
using DisassArm;
using DisassRiscV;
using DisassShared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeIce68k.ViewModel
{
    public class RegisterSetModelRiscV : RegisterSetModelBase, IRegisterSetPredictNext
    {
        public RegisterModel X1 { get; }
        public RegisterModel X2 { get; }
        public RegisterModel X3 { get; }
        public RegisterModel X4 { get; }
        public RegisterModel X5 { get; }
        public RegisterModel X6 { get; }
        public RegisterModel X7 { get; }
        public RegisterModel X8 { get; }
        public RegisterModel X9 { get; }
        public RegisterModel X10 { get; }
        public RegisterModel X11 { get; }
        public RegisterModel X12 { get; }
        public RegisterModel X13 { get; }
        public RegisterModel X14 { get; }
        public RegisterModel X15 { get; }
        public RegisterModel X16 { get; }
        public RegisterModel X17 { get; }
        public RegisterModel X18 { get; }
        public RegisterModel X19 { get; }
        public RegisterModel X20 { get; }
        public RegisterModel X21 { get; }
        public RegisterModel X22 { get; }
        public RegisterModel X23 { get; }
        public RegisterModel X24 { get; }
        public RegisterModel X25 { get; }
        public RegisterModel X26 { get; }
        public RegisterModel X27 { get; }
        public RegisterModel X28 { get; }
        public RegisterModel X29 { get; }
        public RegisterModel X30 { get; }
        public RegisterModel X31 { get; }
        public RegisterModel X32 { get; }

        //PC is not a real register but instead is the Q0 register
        public RegisterModel PC { get; }


        public RegisterModel Q1 { get; }
        public RegisterModel Q2 { get; }
        public RegisterModel Q3 { get; }


        public override bool CanTrace => false;

        public override DisassAddressBase PCValue
        {
            get { return new AddressRiscV(PC.Data); }
            set { PC.Data = (UInt32)value.Canonical; }
        }

        public override IDisassState DisassState => new DisassStateArm { };


        public RegisterSetModelRiscV(DeIceAppModel _parent)
        {
            Parent = _parent;

            X1 = new RegisterModel("x1 ra", RegisterSize.Long, 0);
            X2 = new RegisterModel("x2 sp", RegisterSize.Long, 0);
            X3 = new RegisterModel("x3 gp", RegisterSize.Long, 0);
            X4 = new RegisterModel("x4 tp", RegisterSize.Long, 0);
            X5 = new RegisterModel("x5 t0", RegisterSize.Long, 0);
            X6 = new RegisterModel("x6 t1", RegisterSize.Long, 0);
            X7 = new RegisterModel("x7 t2", RegisterSize.Long, 0);
            X8 = new RegisterModel("x8 s0/fp", RegisterSize.Long, 0);
            X9 = new RegisterModel("x9 s1", RegisterSize.Long, 0);
            X10 = new RegisterModel("x10 a0", RegisterSize.Long, 0);
            X11 = new RegisterModel("x11 a1", RegisterSize.Long, 0);
            X12 = new RegisterModel("x12 a2", RegisterSize.Long, 0);
            X13 = new RegisterModel("x13 a3", RegisterSize.Long, 0);
            X14 = new RegisterModel("x14 a4", RegisterSize.Long, 0);
            X15 = new RegisterModel("x15 a5", RegisterSize.Long, 0);
            X16 = new RegisterModel("x16 a6", RegisterSize.Long, 0);
            X17 = new RegisterModel("x17 a7", RegisterSize.Long, 0);
            X18 = new RegisterModel("x18 s2", RegisterSize.Long, 0);
            X19 = new RegisterModel("x19 s3", RegisterSize.Long, 0);
            X20 = new RegisterModel("x20 s4", RegisterSize.Long, 0);
            X21 = new RegisterModel("x21 s5", RegisterSize.Long, 0);
            X22 = new RegisterModel("x22 s6", RegisterSize.Long, 0);
            X23 = new RegisterModel("x23 s7", RegisterSize.Long, 0);
            X24 = new RegisterModel("x24 s8", RegisterSize.Long, 0);
            X25 = new RegisterModel("x25 s9", RegisterSize.Long, 0);
            X26 = new RegisterModel("x26 s10", RegisterSize.Long, 0);
            X27 = new RegisterModel("x27 s11", RegisterSize.Long, 0);
            X28 = new RegisterModel("x28 t3", RegisterSize.Long, 0);
            X29 = new RegisterModel("x29 t4", RegisterSize.Long, 0);
            X30 = new RegisterModel("x30 t5", RegisterSize.Long, 0);
            X31 = new RegisterModel("x31 t6", RegisterSize.Long, 0);

            PC = new RegisterModel("PC", RegisterSize.Long, 0);
            Q1 = new RegisterModel("q1 irq_mask", RegisterSize.Long, 0);
            Q2 = new RegisterModel("q2", RegisterSize.Long, 0);
            Q3 = new RegisterModel("q3", RegisterSize.Long, 0);



            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
               
            }));

        }

        const int DEICE_REGS_DATA_LENGTH = 1+(31+4)*4;


        public override void FromDeIceProtocolRegData(byte[] deiceData)
        {
            if (deiceData.Length != DEICE_REGS_DATA_LENGTH)
                throw new ArgumentException($"data wrong length for N_READ_RG/FN_RUN_TARG reply {nameof(RegisterSetModelRiscV)}, expecting {DEICE_REGS_DATA_LENGTH} got {deiceData.Length}");

            TargetStatus = deiceData[DEICE_REGS_DATA_LENGTH-1];
            X1.Data = DeIceFnFactory.ReadULong(deiceData, 0x00);
            X2.Data = DeIceFnFactory.ReadULong(deiceData, 0x04);
            X3.Data = DeIceFnFactory.ReadULong(deiceData, 0x08);
            X4.Data = DeIceFnFactory.ReadULong(deiceData, 0x0C);
            X5.Data = DeIceFnFactory.ReadULong(deiceData, 0x10);
            X6.Data = DeIceFnFactory.ReadULong(deiceData, 0x14);
            X7.Data = DeIceFnFactory.ReadULong(deiceData, 0x18);
            X8.Data = DeIceFnFactory.ReadULong(deiceData, 0x1C);
            X9.Data = DeIceFnFactory.ReadULong(deiceData, 0x20);
            X10.Data = DeIceFnFactory.ReadULong(deiceData, 0x24);
            X11.Data = DeIceFnFactory.ReadULong(deiceData, 0x28);
            X12.Data = DeIceFnFactory.ReadULong(deiceData, 0x2C);
            X13.Data = DeIceFnFactory.ReadULong(deiceData, 0x30);
            X14.Data = DeIceFnFactory.ReadULong(deiceData, 0x34);
            X15.Data = DeIceFnFactory.ReadULong(deiceData, 0x38);
            X16.Data = DeIceFnFactory.ReadULong(deiceData, 0x3C);
            X17.Data = DeIceFnFactory.ReadULong(deiceData, 0x40);
            X18.Data = DeIceFnFactory.ReadULong(deiceData, 0x44);
            X19.Data = DeIceFnFactory.ReadULong(deiceData, 0x48);
            X20.Data = DeIceFnFactory.ReadULong(deiceData, 0x4C);
            X21.Data = DeIceFnFactory.ReadULong(deiceData, 0x50);
            X22.Data = DeIceFnFactory.ReadULong(deiceData, 0x54);
            X23.Data = DeIceFnFactory.ReadULong(deiceData, 0x58);
            X24.Data = DeIceFnFactory.ReadULong(deiceData, 0x5C);
            X25.Data = DeIceFnFactory.ReadULong(deiceData, 0x60);
            X26.Data = DeIceFnFactory.ReadULong(deiceData, 0x64);
            X27.Data = DeIceFnFactory.ReadULong(deiceData, 0x68);
            X28.Data = DeIceFnFactory.ReadULong(deiceData, 0x6C);
            X29.Data = DeIceFnFactory.ReadULong(deiceData, 0x70);
            X30.Data = DeIceFnFactory.ReadULong(deiceData, 0x74);
            X31.Data = DeIceFnFactory.ReadULong(deiceData, 0x78);
            PC.Data = DeIceFnFactory.ReadULong(deiceData, 0x7C) & 0xFFFFFFFE;   // discard the compressed flag for now TODO: use for it?
            Q1.Data = DeIceFnFactory.ReadULong(deiceData, 0x80);
            Q2.Data = DeIceFnFactory.ReadULong(deiceData, 0x84);
            Q3.Data = DeIceFnFactory.ReadULong(deiceData, 0x88);
        }

        public override byte[] ToDeIceProtcolRegData()
        {
            byte [] ret = new byte[DEICE_REGS_DATA_LENGTH];
            ret[DEICE_REGS_DATA_LENGTH-1] = TargetStatus;
            DeIceFnFactory.WriteULong(ret, 0x00, X1.Data);
            DeIceFnFactory.WriteULong(ret, 0x04, X2.Data);
            DeIceFnFactory.WriteULong(ret, 0x08, X3.Data);
            DeIceFnFactory.WriteULong(ret, 0x0C, X4.Data);
            DeIceFnFactory.WriteULong(ret, 0x10, X5.Data);
            DeIceFnFactory.WriteULong(ret, 0x14, X6.Data);
            DeIceFnFactory.WriteULong(ret, 0x18, X7.Data);
            DeIceFnFactory.WriteULong(ret, 0x1C, X8.Data);
            DeIceFnFactory.WriteULong(ret, 0x20, X9.Data);
            DeIceFnFactory.WriteULong(ret, 0x24, X10.Data);
            DeIceFnFactory.WriteULong(ret, 0x28, X11.Data);
            DeIceFnFactory.WriteULong(ret, 0x2C, X12.Data);
            DeIceFnFactory.WriteULong(ret, 0x30, X13.Data);
            DeIceFnFactory.WriteULong(ret, 0x34, X14.Data);
            DeIceFnFactory.WriteULong(ret, 0x38, X15.Data);
            DeIceFnFactory.WriteULong(ret, 0x3C, X16.Data);
            DeIceFnFactory.WriteULong(ret, 0x40, X17.Data);
            DeIceFnFactory.WriteULong(ret, 0x44, X18.Data);
            DeIceFnFactory.WriteULong(ret, 0x48, X19.Data);
            DeIceFnFactory.WriteULong(ret, 0x4C, X20.Data);
            DeIceFnFactory.WriteULong(ret, 0x50, X21.Data);
            DeIceFnFactory.WriteULong(ret, 0x54, X22.Data);
            DeIceFnFactory.WriteULong(ret, 0x58, X23.Data);
            DeIceFnFactory.WriteULong(ret, 0x5C, X24.Data);
            DeIceFnFactory.WriteULong(ret, 0x60, X25.Data);
            DeIceFnFactory.WriteULong(ret, 0x64, X26.Data);
            DeIceFnFactory.WriteULong(ret, 0x68, X27.Data);
            DeIceFnFactory.WriteULong(ret, 0x6C, X28.Data);
            DeIceFnFactory.WriteULong(ret, 0x70, X29.Data);
            DeIceFnFactory.WriteULong(ret, 0x74, X30.Data);
            DeIceFnFactory.WriteULong(ret, 0x78, X31.Data);
            DeIceFnFactory.WriteULong(ret, 0x7C, PC.Data);
            DeIceFnFactory.WriteULong(ret, 0x80, Q1.Data);
            DeIceFnFactory.WriteULong(ret, 0x84, Q2.Data);
            DeIceFnFactory.WriteULong(ret, 0x88, Q3.Data);

            return ret;
        }



        public override bool SetTrace(bool trace)
        {

            return false;
        }

        /* Instruction prediction - move to disassembler? */

        private RegisterModel RegisterAt(uint index)
        {
            switch (index)
            {
                case 1: return X1;
                case 2: return X2;
                case 3: return X3;
                case 4: return X4;
                case 5: return X5;
                case 6: return X6;
                case 7: return X7;
                case 8: return X8;
                case 9: return X9;
                case 10: return X10;
                case 11: return X11;
                case 12: return X12;
                case 13: return X13;
                case 14: return X14;
                case 15: return X15;
                case 16: return X16;

                case 17: return X17;
                case 18: return X18;
                case 19: return X19;
                case 20: return X20;
                case 21: return X21;
                case 22: return X22;
                case 23: return X23;
                case 24: return X24;
                case 25: return X25;
                case 26: return X26;
                case 27: return X27;
                case 28: return X28;
                case 29: return X29;
                case 30: return X30;
                case 31: return X31;
                default:
                    throw new ArgumentException($"Index must be 32<n<0 {index}");
            }

        }

        private UInt32 RegisterVal(uint index)
        {
            if (index == 0)
                return 0;
            else
                return RegisterAt(index).Data;
        }
            

        private RegisterModel CRegisterAt(uint index)
        {
            switch (index)
            {
                case 0: return X8;
                case 1: return X9;
                case 2: return X10;
                case 3: return X11;
                case 4: return X12;
                case 5: return X13;
                case 6: return X14;
                case 7: return X15;
                default:
                    throw new ArgumentException($"Index must be 32<n<0 {index}");
            }
        }

        private UInt32 CRegisterVal(uint index)
        {
            if (index == 0)
                return 0;
            else
                return CRegisterAt(index).Data;
        }


        private static long Signed(uint i, int nbits)
        {
            int sb = 1 << (nbits - 1);
            uint m = 0xFFFFFFFF << nbits;
            return (int)(((i & sb) != 0) ? (m | i) : i);

        }


        public DisassAddressBase PredictNext(byte[] programdata)
        {

            DisassAddressBase ret;
            if ((programdata[0] & 0b11) == 0b11)
            {
                //TODO: >32 bits?
                ret = PCValue + 4;

                uint instr = (uint)(
                    ((UInt32)programdata[0]) 
                    | ((UInt32)programdata[1] << 8) 
                    | ((UInt32)programdata[2] << 16) 
                    | ((UInt32)programdata[3] << 24));

                if ((instr & 0b1111111) == 0b1100011)
                {
                    // branches
                    uint offs = (
                                ((instr & 0x80000000) >> 19) | // 12
                                ((instr & 0x7E000000) >> 20) | // 10:5
                                ((instr & 0x00000F00) >> 7) | // 4:1
                                ((instr & 0x00000080) << 4) // 11
                            );
                    uint rs2 = (instr & 0x01F00000) >> 20;
                    uint rs1 = (instr & 0x000F8000) >> 15;
                    uint f3 = (instr & 0x00007000) >> 12;
                    bool branch = f3 switch
                    {
                        //beq
                        0 => RegisterVal(rs1) == RegisterVal(rs2),
                        //bne
                        1 => RegisterVal(rs1) != RegisterVal(rs2),
                        //blt
                        4 => Signed(RegisterVal(rs1), 32) < Signed(RegisterVal(rs2), 32),
                        //bge
                        5 => Signed(RegisterVal(rs1), 32) >= Signed(RegisterVal(rs2), 32),
                        //bltu
                        6 => RegisterVal(rs1) < RegisterVal(rs2),
                        //bgeu
                        7 => RegisterVal(rs1) >= RegisterVal(rs2),
                        _ => false
                    };
                    if (branch)
                    {
                        ret = PCValue + Signed(offs, 12);
                    }
                } else if ((instr & 0b1111111) == 0b1101111)
                {
                    // jump
                    uint imm = (
                                ((instr & 0x80000000) >> 11) | // 20
                                ((instr & 0x7FE00000) >> 20) | // 10:1
                                ((instr & 0x00100000) >> 9) | // 11
                                ((instr & 0x000FF000)) // 19:12
                            );

                    ret = PCValue + Signed(imm, 20);                    
                } else if ((instr & 0b1111111) == 0b1100111)
                {
                    uint imm = (instr & 0xFFF00000) >> 20;
                    uint rs1 = (instr & 0x000F8000) >> 15;
                    uint f3 = (instr & 0x00007000) >> 12;
                    uint rd = (instr & 0x00000F80) >> 7;

                    if (f3 == 0)
                    {
                        return new AddressRiscV((uint)(RegisterAt(rs1).Data + Signed(imm, 12)) & 0xFFFFFFFE);
                    }

                }
            }
            else
            {
                // a 16 bit instruction
                ret = PCValue + 2;
                uint instr = (uint)(programdata[0] | (programdata[1] << 8));
                //check for jumps/branches
                if (
                    ((instr & 0b1110_0000_0000_0011) == 0b0010_0000_0000_0001)
                    || ((instr & 0b1110_0000_0000_0011) == 0b1010_0000_0000_0001)
                ) {
                    uint offs = (uint)(
                   ((instr & 0x1000) >> 1)  // 12   -> 11
                 | ((instr & 0x0800) >> 7)  // 11   -> 4
                 | ((instr & 0x0600) >> 1)  // 10:9 -> 9:8
                 | ((instr & 0x0100) << 2)  // 8    -> 10
                 | ((instr & 0x0080) >> 1)  // 7    -> 6
                 | ((instr & 0x0040) << 1)  // 6    -> 7
                 | ((instr & 0x0038) >> 2)  // 5:3  -> 3:1
                 | ((instr & 0x0004) << 3)  // 2    -> 5
                 );

                    //CJ types
                    ret = PCValue + Signed(offs, 12);
                }
                else if ((instr & 0b1110_0000_0000_0011) == 0b1000_0000_0000_0010)
                {
                    //CR types
                    uint f4 = (uint)((instr & 0xF000) >> 12);
                    uint rdrs1 = (uint)((instr & 0x0F80) >> 7);
                    uint rs2 = (uint)((instr & 0x007C) >> 2);

                    if (f4 == 0b1000 && rs2 == 0)
                    {
                        //c.ret / c.jr
                        if (rdrs1 == 0)
                            ret = new AddressRiscV(0);
                        else
                            ret = new AddressRiscV(RegisterAt(rdrs1).Data);
                    }
                    else if (f4 == 0b1001 && rs2 == 0)
                    {
                        if (rdrs1 == 0 && rs2 == 0)
                            ret = PCValue; // c.ebreak
                        else if (rs2 == 0)
                            ret = new AddressRiscV(RegisterAt(rdrs1).Data);
                    }
                }
                else if ((instr & 0b1100_0000_0000_0011) == 0b1100_0000_0000_0001)
                {
                    uint imm = (uint)(
                            ((instr & 0x1000) >> 4)
                            | ((instr & 0x0C00) >> 7)
                            | ((instr & 0x0060) << 1)
                            | ((instr & 0x0018) >> 2)
                            | ((instr & 0x0004) << 3)
                            );
                    uint rs_ = (uint)((instr & 0x0380) >> 7);

                    //c.b??
                    if ((instr & 0x2000) == 0)
                    {
                        //c.beqz
                        if (CRegisterVal(rs_) == 0)
                            ret = PCValue + Signed(imm, 9);
                    }
                    else
                    {
                        //c.bnez
                        if (CRegisterVal(rs_) != 0)
                            ret = PCValue + Signed(imm, 9);
                    }
                }

            }


            return ret;
        }

        public int PredictProgramDataSize => 4;

    }
}
