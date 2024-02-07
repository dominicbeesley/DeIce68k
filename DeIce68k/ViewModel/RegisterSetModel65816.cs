using DeIceProtocol;
using Disass65816;
using Disass65816.Emulate;
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
    public class RegisterSetModel65816 : RegisterSetModelBase, IRegisterSetPredictNext
    {
        public RegisterModel A { get; }
        public RegisterModel X { get; }
        public RegisterModel Y { get; }
        public RegisterModel D { get; }
        public RegisterModel S { get; }
        public RegisterModel B { get; }
        public RegisterModel PC { get; }
        public RegisterModel P { get; }
        public RegisterModel E { get; }

        public override bool CanTrace => false;

        public override DisassAddressBase PCValue
        {
            get { return new Address65816_far(PC.Data); }
            set { PC.Data = (UInt32)value.Canonical; }
        }

        public override IDisassState DisassState => new DisassState65816
        {
            RegSizeM8 = E.Data != 0 || (P.Data & 0x20) != 0,
            RegSizeX8 = E.Data != 0 || (P.Data & 0x10) != 0
        };

        public RegisterSetModel65816(DeIceAppModel _parent)
        {
            Parent = _parent;

            A = new RegisterModel("A", RegisterSize.Word, 0);
            X = new RegisterModel("X", RegisterSize.Word, 0);
            Y = new RegisterModel("Y", RegisterSize.Word, 0);
            D = new RegisterModel("D", RegisterSize.Word, 0);
            S = new RegisterModel("S", RegisterSize.Word, 0);
            B = new RegisterModel("B", RegisterSize.Byte, 0);
            PC = new RegisterModel("PC", RegisterSize.Bank24, 0);
            P = new RegisterModel("P", RegisterSize.Byte, 0);
            E = new RegisterModel("E", RegisterSize.Bit, 0);

            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
                new(this) { BitIndex=8, Label="E", Name="Emulation" },
                new(this) { BitIndex=7, Label="N", Name="Negative" },
                new(this) { BitIndex=6, Label="V", Name="Overflow" },
                new(this) { BitIndex=5, Label="M", Name="Mem 8bit" },
                new(this) { BitIndex=4, Label="X", Name="IX 8bit" },
                new(this) { BitIndex=3, Label="D", Name="Decimal" },
                new(this) { BitIndex=2, Label="I", Name="Int Inh" },
                new(this) { BitIndex=1, Label="Z", Name="Zero" },
                new(this) { BitIndex=0, Label="C", Name="Carry" }
            }));

            E.PropertyChanged += E_PropertyChanged;
            P.PropertyChanged += P_PropertyChanged;

            foreach (var sb in StatusBits)
            {
                sb.PropertyChanged += Sb_PropertyChanged;
            }

            UpdateStatusBits();
        }

        const int DEICE_REGS_DATA_LENGTH = 17;

        public override void FromDeIceProtocolRegData(byte[] deiceData)
        {
            if (deiceData.Length < DEICE_REGS_DATA_LENGTH)
                throw new ArgumentException($"data wrong length for N_READ_RG/FN_RUN_TARG reply {nameof(RegisterSetModel68k)}, expecting {DEICE_REGS_DATA_LENGTH} got {deiceData.Length}");

            TargetStatus = deiceData[0x00];
            A.Data = DeIceFnFactory.ReadUShort(deiceData, 1);
            X.Data = DeIceFnFactory.ReadUShort(deiceData, 3);
            Y.Data = DeIceFnFactory.ReadUShort(deiceData, 5);
            D.Data = DeIceFnFactory.ReadUShort(deiceData, 7);
            S.Data = DeIceFnFactory.ReadUShort(deiceData, 9);
            E.Data = (uint)(deiceData[11] & 0x01);
            B.Data = deiceData[12];
            P.Data = deiceData[13];
            PC.Data = DeIceFnFactory.ReadU24(deiceData, 14) & 0xFFFFFF;
        }

        public override byte[] ToDeIceProtcolRegData()
        {
            byte[] ret = new byte[DEICE_REGS_DATA_LENGTH];
            ret[0] = TargetStatus;
            DeIceFnFactory.WriteUShort(ret, 1, A.Data);
            DeIceFnFactory.WriteUShort(ret, 3, X.Data);
            DeIceFnFactory.WriteUShort(ret, 5, Y.Data);
            DeIceFnFactory.WriteUShort(ret, 7, D.Data);
            DeIceFnFactory.WriteUShort(ret, 9, S.Data);
            ret[11] = (byte)(E.Data != 0 ? 1 : 0);
            ret[12] = (byte)B.Data;
            ret[13] = (byte)P.Data;
            DeIceFnFactory.WriteU24(ret, 14, PC.Data);
            return ret;
        }

        private void Sb_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StatusRegisterBitsModel sb = sender as StatusRegisterBitsModel;
            if (sb is not null)
            {
                if (sb.BitIndex == 8)
                {
                    E.Data = sb.Data ? (uint)1 : 0;
                }
                else
                {
                    uint mask = (uint)(1 << sb.BitIndex);
                    P.Data = (P.Data & ~mask) | ((sb.Data) ? mask : 0);
                }
            }
        }

        private void E_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateStatusBits();
        }

        private void P_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateStatusBits();
        }

        private void UpdateStatusBits()
        {
            var sbE = StatusBits[0].Data = E.Data != 0;

            var sr = P.Data;
            foreach (var sb in StatusBits.Where(x => x.BitIndex <= 7))
            {
                sb.Data = (sr & 1 << sb.BitIndex) != 0;
            }
        }

        public override bool SetTrace(bool trace)
        {
            return false;
        }

        public DisassAddressBase? PredictNext(byte[] programdata)
        {
            Emulate65816 em = new Emulate65816();
            Emulate65816.Registers r = new Emulate65816.Registers(em);
            r.A = (int)(this.A.Data & 0xFF);
            r.B = (int)((this.A.Data & 0xFF00) >> 8);
            r.X = (int)(this.X.Data & 0xFFFF);
            r.Y = (int)(this.Y.Data & 0xFFFF);
            r.DP = (int)(this.D.Data & 0xFF);
            r.DB = (int)(this.B.Data & 0xFF);
            r.PB = (int)((this.PC.Data & 0xFF0000) >> 16);
            r.PC = (int)(this.PC.Data & 0xFFFF);
            r.SH = (int)((this.S.Data & 0xFF00) >> 8);
            r.SL = (int)(this.S.Data & 0xFF);

            r.E = this.E.Data != 0;
            r.set_FLAGS((int)(this.P.Data & 0xFF));

            em.em_65816_emulate(programdata, r, out _);

            /*          RegisterSetModel65816 ret = this.Clone();

                        if (r.A >=0)
                            ret.A.Data = (uint)(r.A | (int)(ret.A.Data & 0xFF00));
                        if (r.B >= 0)
                            ret.A.Data = (uint)((r.B << 8) | (int)(ret.A.Data & 0xFF));
                        if (r.X >=0)
                            ret.X.Data = (uint)(r.X);
                        if (r.Y >= 0)
                            ret.Y.Data = (uint)(r.Y);
                        if (r.DP >= 0)
                            ret.D.Data = (uint)(r.DP);
                        if (r.DB >= 0)
                            ret.B.Data = (uint)(r.DB);
                        if (r.PB >= 0)
                            ret.PC.Data = ret.PC.Data & 0xFFFF | (uint)(r.PB << 16);
                        if (r.PC >= 0)
                            ret.PC.Data = ret.PC.Data & 0xFF0000 | (uint)(r.PC & 0xFFFF);
                        if (r.SL >= 0)
                            ret.S.Data = (uint)(r.SL | (int)(ret.S.Data & 0xFF00));
                        if (r.SH >= 0)
                            ret.S.Data = (uint)((r.SH << 8) | (int)(ret.S.Data & 0xFF));

                        return ret;
            */
            
            if (r.PC >= 0 && r.PB >= 0)
                return new Disass65816.Address65816_far((UInt32)((r.PB << 16) | (r.PC)));
            else
                return null;


        }

        public int PredictProgramDataSize { get => 4; }

        public RegisterSetModel65816 Clone()
        {
            var ret = new RegisterSetModel65816(Parent);
            ret.A.Data = this.A.Data;
            ret.X.Data = this.X.Data;
            ret.Y.Data = this.Y.Data;
            ret.D.Data = this.D.Data;
            ret.B.Data = this.B.Data;
            ret.PC.Data = this.PC.Data;
            ret.S.Data = this.S.Data;
            ret.E.Data = this.E.Data;
            ret.P.Data = this.P.Data;
            return ret;
        }
    }
}
