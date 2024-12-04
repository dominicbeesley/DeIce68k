using DeIceProtocol;
using DisassArm;
using DisassRiscV;
using DisassShared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeIce68k.ViewModel
{
    public class RegisterSetModelRiscV : RegisterSetModelBase
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
            get { return new AddressRiscV(PC.Data & 0x0FFFFFFF); }
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

            PC = new RegisterModel("PC (q0)", RegisterSize.Long, 0);
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
            PC.Data = DeIceFnFactory.ReadULong(deiceData, 0x7C);
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
    }
}
