using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using DeIceProtocol;
using DossySerialPort;
using DeIce68k.Lib;

namespace DeIce68k.ViewModel
{
    /// <summary>
    /// main application model
    /// </summary>
    public class DeIceAppModel : ObservableObject
    {
        IDossySerial _serial;
        DeIceProtocolMain _deIceProtocol;

        public DeIceProtocolMain Serial { get { return _deIceProtocol; } }

        ObservableCollection<WatchModel> _watches = new ObservableCollection<WatchModel>();
        public ObservableCollection<WatchModel> Watches { get { return _watches; } }

        RegisterSetModel _regs;

        public RegisterSetModel Regs { get { return _regs; } }

        ObservableCollection<string> _messages = new ObservableCollection<string>();
        public ObservableCollection<string> Messages { get { return _messages; } }

        int mn = 0;
        object mnLock = new object();

        static Regex reDef = new Regex(@"^\s*DEF(?:INE)?\s+(\w+)\s+(?:0x)?([0-9A-F]+)(?:h)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex reWatchSym = new Regex(@"^w(?:atch)?\s+(\w+)(?:\s+%(\w+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Dictionary<string, uint> _symbol2AddressDictionary = new Dictionary<string, uint>();
        Dictionary<uint, string> _address2SymboldDictionary = new Dictionary<uint, string>();


        public ReadOnlyDictionary<string, uint> Symbol2AddressDictionary
        {
            get;
        }

        public ReadOnlyDictionary<uint, string> Address2SymboldDictionary
        {
            get;
        }


        private DisassMemBlock _disassMemBlock;
        public DisassMemBlock DisassMemBlock
        {
            get
            {
                return _disassMemBlock;
            }
            set
            {
                _disassMemBlock = value;
                this.RaisePropertyChangedEvent(nameof(DisassMemBlock));
            }
        }

        private int MessageNo()
        {
            lock (mnLock)
                return mn++;
        }


        public ICommand CmdNext { get; }
        public ICommand CmdCont { get; }
        public ICommand CmdTraceTo { get; }
        public MainWindow MainWindow { get; init; }

        public void ReadCommandFile(string pathname)
        {
            using (var s = new FileStream(pathname, FileMode.Open, FileAccess.Read))
            {
                using (var tr = new StreamReader(s))
                {
                    string l;
                    int lineno = 0;
                    while ((l = tr.ReadLine()) != null)
                    {
                        lineno++;
                        try
                        {
                            ParseCommand(l);
                        }
                        catch (Exception ex)
                        {
                            Messages.Add($"{MessageNo():X4}:Error parsing line {lineno}:{l}\n{ex.Message}");
                        }
                    }
                }

            }


        }

        public void ParseCommand(string line)
        {
            var mD = reDef.Match(line);
            if (mD.Success)
            {
                string sym = mD.Groups[1].Value;
                uint add = Convert.ToUInt32(mD.Groups[2].Value, 16);
                _address2SymboldDictionary[add] = sym;
                _symbol2AddressDictionary[sym] = add;
                return;
            }
            var mWA = reWatchSym.Match(line);
            if (mWA.Success)
            {

                string name = null;
                uint addr;

                if (_symbol2AddressDictionary.TryGetValue(mWA.Groups[1].Value, out addr))
                {
                    name = mWA.Groups[1].Value;
                }
                else
                {
                    //couldn't find symbol try as address
                    try
                    {
                        addr = Convert.ToUInt32(mWA.Groups[1].Value, 16);
                    }
                    catch (Exception)
                    {
                        throw new ArgumentException($"\"{mWA.Groups[1]}\" is not a known symbol or hex number");
                    }
                    _address2SymboldDictionary.TryGetValue(addr, out name);
                }

                string type = mWA.Groups[2].Value;
                WatchType t = WatchType.X08;
                if (!String.IsNullOrEmpty(type))
                {
                    t = WatchType_Ext.StringToWatchType(type);
                    if (t == WatchType.Empty)
                        throw new ArgumentException($"Unrecognised watch type %{type}");
                }
                Watches.Add(new WatchModel(addr, name, t, null));
                return;
            }
            throw new ArgumentException("Unrecognised command");

        }

        public DeIceAppModel(IDossySerial serial, MainWindow mainWindow)
        {
            this.MainWindow = mainWindow;
            this._serial = serial;

            _regs = new RegisterSetModel(this);

            Symbol2AddressDictionary = new ReadOnlyDictionary<string, uint>(_symbol2AddressDictionary);
            Address2SymboldDictionary = new ReadOnlyDictionary<uint, string>(_address2SymboldDictionary);

            CmdNext = new RelayCommand<object>(
            o => {
                try
                {
                    this.Regs.SR.Data |= 0x8000;
                    _deIceProtocol.SendReqExpectReply<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { Regs = Regs.ToDeIceProtcolRegs() }); //ignore TODO: check?
                    _deIceProtocol.SendReq(new DeIceFnReqRun());
                }
                catch (Exception ex)
                {
                    Messages.Add($"{MessageNo():X4} ERROR:Executing Next\n{ ex.ToString() } ");
                }
            },
            o =>
            {
                return Regs.IsStopped;
            }
            );

            CmdCont = new RelayCommand<object>(
                o =>
                {
                    try
                    {
                        Regs.SR.Data &= 0x7FFF;
                        _deIceProtocol.SendReqExpectReply<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { Regs = Regs.ToDeIceProtcolRegs() }); // ignore responese: TODO: check?
                        _deIceProtocol.SendReq(new DeIceFnReqRun());
                    }
                    catch (Exception ex)
                    {
                        Messages.Add($"{MessageNo():X4} ERROR:Executing Continue\n{ ex.ToString() } ");
                    }
                },
                o=>
                {
                    return Regs.IsStopped;
                }

            );

            CmdTraceTo = new RelayCommand<object>(
                o => {
                    var dlg = new DlgTraceTo(this);
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;
                    dlg.ShowDialog();

                },
                o => { return Regs.IsStopped; }
                );

            _deIceProtocol = new DeIceProtocolMain(_serial);

            Task.Factory.StartNew(() =>
            {
                _deIceProtocol.CommError += (o, e) =>
                {
                    DoInvoke(new Action(
                    delegate
                    {
                        Messages.Add($"{MessageNo():X4} ERROR:{ e.Exception.ToString() }");
                    })
                    );
                };
                _deIceProtocol.OobDataReceived += (o, e) =>
                {
                    DoInvoke(new Action(
                    delegate
                    {
                        Messages.Add($"{MessageNo():X4} OOB:{ e.Data }");
                    })
                    );
                };
                _deIceProtocol.FunctionReceived += (o, e) =>
                {
                    DoInvoke(new Action(
                    delegate
                    {
                        Messages.Add($"{MessageNo():X4} FN:{ e.Function.FunctionCode } : { e.Function.GetType().Name }");

                        var x = e.Function as DeIceFnReplyRegsBase;
                        if (x != null)
                        {
                            Regs.FromDeIceRegisters(x.Registers);

                            RunFinish();
                        }
                    })
                    );
                };
                _deIceProtocol.SendReq(new DeIceFnReqReadRegs());
                Thread.Sleep(100);
                Thread.Sleep(100);
                Thread.Sleep(100);
            });


        }

        //TODO: Check if there's a better way of doing this
        void DoInvoke(Action a)
        {
            if (MainWindow is not null)
                MainWindow.Dispatcher.Invoke(a);
            else
                a.Invoke();
        }

        /// <summary>
        /// Run has finished (or initial read regs reply received) update the display, registers should already have been updated
        /// </summary>
        public void RunFinish()
        {
            try
            {
                //check to see if pc is in the current DisassMemBlock, if not either extend or load new
                if (DisassMemBlock != null && DisassMemBlock.BaseAddress < Regs.PC.Data && DisassMemBlock.EndPoint > Regs.PC.Data + 16)
                {
                    //in range just update pc
                    DisassMemBlock.PC = Regs.PC.Data;
                }
                else if (DisassMemBlock != null && (Regs.PC.Data - DisassMemBlock.EndPoint) < 256)
                {
                    //close just extend until we're in range
                    DisassMemBlock.MorePlease(Regs.PC.Data + 128 - DisassMemBlock.EndPoint);
                    DisassMemBlock.PC = Regs.PC.Data;
                }
                else
                {
                    var disdat = new byte[128];
                    _deIceProtocol.ReadMemBlock(Regs.PC.Data, disdat, 0, 128);

                    DisassMemBlock = new DisassMemBlock(this, Regs.PC.Data, disdat, _address2SymboldDictionary);

                    foreach (var w in Watches)
                    {
                        byte[] buf = new byte[w.DataSize];
                        _deIceProtocol.ReadMemBlock(w.Address, buf, 0, w.DataSize);
                        w.Data = buf;
                    }
                    DisassMemBlock.PC = Regs.PC.Data;
                }
            }
            catch (Exception ex)
            {
                Messages.Add($"{MessageNo():X4} ERROR:reading memory\n{ ex.ToString() } ");
            }
        }
    }
}
