using DeIce68k.Lib;
using DeIce68k.ViewModel.Scripts;
using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeIce68k.ViewModel
{
    public class BreakpointModel : ObservableObject
    {
        private DeIceAppModel _app;

        public uint Address {
            get;
            init;
        }

        private bool _enabled;
        public bool Enabled {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        private byte[] _oldOP;
        public byte[] OldOP
        {
            get => _oldOP;
            set => Set(ref _oldOP, value);
        }

        public string SymbolStr
        {
            get
            {
                return _app.Symbols.FindNearest(Address, SymbolType.Pointer);
            }
        }


        private ScriptBase _conditionCode = null;
        public ScriptBase ConditionCode
        {
            get => _conditionCode;
            set
            {
                Set(ref _conditionCode, value);
                RaisePropertyChangedEvent(nameof(HasCode));
            }
        }

        public bool HasCode { get => ConditionCode != null; }


        public BreakpointModel(DeIceAppModel app)
        {
            _app = app;

            CmdEditCode = new RelayCommand(
                o =>
                {
                    EditCondition();
                },
                o =>
                {
                    return true;
                },
                "Edit Breakpoint Condition code",
                _app.Command_Exception
                );
        }

        public ICommand CmdEditCode { get; }

        public void EditCondition()
        {
            string code = (ConditionCode == null)?"return true;":ConditionCode.OrgCode;

            var dlg = new dlgEditCode(_app, code);
            if (dlg.ShowDialog() ?? false)
            {
                ConditionCode = dlg.CompiledCode;
            }
        }
    }
}
