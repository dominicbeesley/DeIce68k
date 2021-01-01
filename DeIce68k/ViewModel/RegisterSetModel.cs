using DeIceProtocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public class RegisterSetModel : ObservableObject
    {
        byte _targetStatus;

        public byte TargetStatus
        {
            get
            {
                return _targetStatus;
            }
            set
            {
                _targetStatus = value;
                RaisePropertyChangedEvent(nameof(TargetStatus));
            }
        }
        public RegisterModel D0 { get; }
        public RegisterModel D1 { get; }
        public RegisterModel D2 { get; }
        public RegisterModel D3 { get; }
        public RegisterModel D4 { get; }
        public RegisterModel D5 { get; }
        public RegisterModel D6 { get; }
        public RegisterModel D7 { get; }

        public RegisterModel A0 { get; }
        public RegisterModel A1 { get; }
        public RegisterModel A2 { get; }
        public RegisterModel A3 { get; }
        public RegisterModel A4 { get; }
        public RegisterModel A5 { get; }
        public RegisterModel A6 { get; }
        public RegisterModel A7u { get; }
        public RegisterModel A7s { get; }

        public RegisterModel PC { get; }
        public RegisterModel SR { get; }

        public ReadOnlyObservableCollection<StatusRegisterBitsModel> StatusBits { get; init; }

        public RegisterSetModel()
        {
            D0 = new RegisterModel("D0", RegisterSize.Long, 0);
            D1 = new RegisterModel("D1", RegisterSize.Long, 0);
            D2 = new RegisterModel("D2", RegisterSize.Long, 0);
            D3 = new RegisterModel("D3", RegisterSize.Long, 0);
            D4 = new RegisterModel("D4", RegisterSize.Long, 0);
            D5 = new RegisterModel("D5", RegisterSize.Long, 0);
            D6 = new RegisterModel("D6", RegisterSize.Long, 0);
            D7 = new RegisterModel("D7", RegisterSize.Long, 0);

            A0 = new RegisterModel("A0", RegisterSize.Long, 0);
            A1 = new RegisterModel("A1", RegisterSize.Long, 0);
            A2 = new RegisterModel("A2", RegisterSize.Long, 0);
            A3 = new RegisterModel("A3", RegisterSize.Long, 0);
            A4 = new RegisterModel("A4", RegisterSize.Long, 0);
            A5 = new RegisterModel("A5", RegisterSize.Long, 0);
            A6 = new RegisterModel("A6", RegisterSize.Long, 0);
            A7u = new RegisterModel("A7u", RegisterSize.Long, 0);
            A7s = new RegisterModel("A7s", RegisterSize.Long, 0);

            PC = new RegisterModel("PC", RegisterSize.Long, 0);
            SR = new RegisterModel("SR", RegisterSize.Word, 0);

            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
                new() { BitIndex=15, Label="T", Name="Trace" },
                new() { BitIndex=14, Label="-", Name="Uk 14" },
                new() { BitIndex=13, Label="S", Name="Supervisor" },
                new() { BitIndex=12, Label="-", Name="Uk 12" },
                new() { BitIndex=11, Label="-", Name="Uk 11" },
                new() { BitIndex=10, Label="I2", Name="Int.2" },
                new() { BitIndex=9, Label="I1", Name="Int.1" },
                new() { BitIndex=8, Label="I0", Name="Int.0" },
                new() { BitIndex=7, Label="-", Name="Uk 7" },
                new() { BitIndex=6, Label="-", Name="Uk 6" },
                new() { BitIndex=5, Label="-", Name="Uk 5" },
                new() { BitIndex=4, Label="X", Name="Extend" },
                new() { BitIndex=3, Label="N", Name="Negative" },
                new() { BitIndex=2, Label="Z", Name="Zero" },
                new() { BitIndex=1, Label="V", Name="Overflow" },
                new() { BitIndex=0, Label="C", Name="Carry" }
            }));

            SR.PropertyChanged += SR_PropertyChanged;
            UpdateStatusBits();
        }

        private void SR_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterModel.Data))
            {
                UpdateStatusBits();
            }
        }

        private void UpdateStatusBits()
        {
            var sr = SR.Data;
            foreach (var sb in StatusBits)
            {
                sb.Data = (sr & 1 << sb.BitIndex) != 0;
            }
        }

        public void FromDeIceRegisters(DeIceRegisters other)
        {
            TargetStatus = other.TargetStatus;
            D0.Data = other.D0;
            D1.Data = other.D1;
            D2.Data = other.D2;
            D3.Data = other.D3;
            D4.Data = other.D4;
            D5.Data = other.D5;
            D6.Data = other.D6;
            D7.Data = other.D7;
            A0.Data = other.A0;
            A1.Data = other.A1;
            A2.Data = other.A2;
            A3.Data = other.A3;
            A4.Data = other.A4;
            A5.Data = other.A5;
            A6.Data = other.A6;
            A7u.Data = other.A7u;
            A7s.Data = other.A7s;
            PC.Data = other.PC;
            SR.Data = other.SR;
        }

        public DeIceRegisters ToDeIceProtcolRegs()
        {
            return new DeIceRegisters()
            {
                TargetStatus = TargetStatus,
                A0 = A0.Data,
                A1 = A1.Data,
                A2 = A2.Data,
                A3 = A3.Data,
                A4 = A4.Data,
                A5 = A5.Data,
                A6 = A6.Data,
                A7s = A7s.Data,
                A7u = A7u.Data,
                D0 = D0.Data,
                D1 = D1.Data,
                D2 = D2.Data,
                D3 = D3.Data,
                D4 = D4.Data,
                D5 = D5.Data,
                D6 = D6.Data,
                D7 = D7.Data,
                PC = PC.Data,
                SR = (ushort)SR.Data
            };
        }

        public static RegisterSetModel TestRegisterSet = new RegisterSetModel();
        public static ReadOnlyObservableCollection<StatusRegisterBitsModel> TestStatusRegisterBits = new RegisterSetModel().StatusBits;

    }
}
