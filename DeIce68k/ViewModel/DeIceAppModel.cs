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
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.ComponentModel;
using System.Collections.Specialized;
using Microsoft.Win32;

namespace DeIce68k.ViewModel
{
    /// <summary>
    /// main application model
    /// </summary>
    public class DeIceAppModel : ObservableObject
    {
        private DeIceFnReplyGetStatus _debugHostStatus;
        public DeIceFnReplyGetStatus DebugHostStatus
        {
            get
            {
                if (_debugHostStatus == null)
                {
                    //return a default...
                    return DeIceFnReplyGetStatus.Default;
                }
                else
                {
                    return _debugHostStatus;
                }
            }
            set => Set(ref _debugHostStatus, value);
        }

        public IDossySerial Serial { get; init; }

        DeIceProtocolMain _deIceProto;
        public DeIceProtocolMain DeIceProto { get { return _deIceProto; } }

        ObservableCollection<WatchModel> _watches = new ObservableCollection<WatchModel>();
        public ObservableCollection<WatchModel> Watches { get { return _watches; } }

        ObservableCollection<BreakpointModel> _breakpointsint = new ObservableCollection<BreakpointModel>();
        ReadOnlyObservableCollection<BreakpointModel> _breakpoints;
        public ReadOnlyObservableCollection<BreakpointModel> Breakpoints { get { return _breakpoints; } }


        RegisterSetModel _regs;

        public RegisterSetModel Regs { get { return _regs; } }

        ObservableCollection<string> _messages = new ObservableCollection<string>();
        public ObservableCollection<string> Messages { get { return _messages; } }

        int mn = 0;
        object mnLock = new object();

        public DeIceSymbols Symbols { get; }


        CancellationTokenSource traceCancelSource = null;


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
        public ICommand CmdDumpMem { get; }
        public ICommand CmdStop { get; }
        public ICommand CmdRefresh { get; }
        public ICommand CmdDisassembleAt { get; }

        public ICommand CmdBreakpoints_Add { get; }
        public ICommand CmdBreakpoints_Delete { get; }
        public ICommand CmdWatches_Add { get; }
        public ICommand CmdWatches_Delete { get; }


        public ICommand CmdLoadBinary { get; }
        public ICommand CmdRunScript { get; }
        public ICommand CmdOpenAScript { get; }

        public MainWindow MainWindow { get; init; }


        static Regex reDef = new Regex(@"^\s*DEF(?:INE)?\s+(\w+)\s+(?:0x)?([0-9A-F]+)(?:h)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex reWatchSym = new Regex(@"^w(?:atch)?\s+(\w+)(?:\s+%(\w+))?(\[([0-9]+)\])*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex reBreakpoint = new Regex(@"^b(?:reakpoint)?\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex reLoad = new Regex(@"^l(?:oad)?\s+(\w+)\s+(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex reRemark = new Regex(@"(;|REM)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private bool _busyInt;
        protected bool BusyInt
        {
            get => _busyInt;
            set
            {
                Set(ref _busyInt, value);
                RaisePropertyChangedEvent(nameof(HostBusy));
            }
        }

        /// <summary>
        /// This is true if the host is running or there is a long load/read running
        /// </summary>
        public bool HostBusy
        {
            get => _busyInt || !Regs.IsStopped;
        }


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
                            ParseCommand(l, Path.GetDirectoryName(pathname));
                        }
                        catch (Exception ex)
                        {
                            Messages.Add($"{MessageNo():X4}:Error parsing line {lineno}:{l}\n{ex.Message}");
                        }
                    }
                }

            }

            AddRecentCommandFile(pathname);
        }

        private ObservableCollection<string> _recentCommandFiles;
        public ReadOnlyObservableCollection<string> RecentCommandFiles { get; }

        private void AddRecentCommandFile(string pathname)
        {
            int ix = _recentCommandFiles.IndexOf(pathname);
            if (ix > 0)
            {
                _recentCommandFiles.RemoveAt(ix);
                _recentCommandFiles.Insert(0, pathname);
            }
            else if (ix < 0)
            {
                _recentCommandFiles.Insert(0, pathname);
            }

            while (_recentCommandFiles.Count > 10)
            {
                _recentCommandFiles.RemoveAt(_recentCommandFiles.Count - 1);
            }
        }

        public void ParseCommand(string line, string directory)
        {
            var mRem = reRemark.Match(line);
            if (mRem.Success)
                return;

            var mD = reDef.Match(line);
            if (mD.Success)
            {
                //SYMBOL DEF
                string sym = mD.Groups[1].Value;
                uint addr = Convert.ToUInt32(mD.Groups[2].Value, 16);
                Symbols.Add(sym, addr);
                return;
            }

            var mWA = reWatchSym.Match(line);
            if (mWA.Success)
            {

                string name = null;
                uint addr;

                ParseSymbolOrAddress(mWA.Groups[1].Value, out name, out addr);

                string type = mWA.Groups[2].Value;
                WatchType t = WatchType.X08;
                if (!String.IsNullOrEmpty(type))
                {
                    t = WatchType_Ext.StringToWatchType(type);
                    if (t == WatchType.Empty)
                        throw new ArgumentException($"Unrecognised watch type %{type}");
                }

                uint[] dims;
                try
                {
                    dims = mWA.Groups[4].Captures.Select(o => Convert.ToUInt32(o)).ToArray(); ;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Bad array index");
                }
                Watches.Add(new WatchModel(addr, name, t, dims));
                return;
            }

            var mBP = reBreakpoint.Match(line);
            if (mBP.Success)
            {
                string name = null;
                uint addr;
                ParseSymbolOrAddress(mBP.Groups[1].Value, out name, out addr);
                AddBreakpoint(addr);
                return;
            }

            var mLoad = reLoad.Match(line);
            if (mLoad.Success)
            {
                string name = null;
                uint addr;
                ParseSymbolOrAddress(mLoad.Groups[1].Value, out name, out addr);
                string filename = mLoad.Groups[2].Value;
                LoadBinaryFile(addr, Path.Combine(directory, filename));
                return;

            }
            throw new ArgumentException($"Unrecognised command:{line}");

        }

        private uint _loadBinaryAddr_last = 0x8000;
        public uint LoadBinaryAddr_last
        {
            get => _loadBinaryAddr_last;
            set => Set(ref _loadBinaryAddr_last, value);
        }

        private string _loadBinaryFilename_last = "";
        public string LoadBinaryFilename_last
        {
            get => _loadBinaryFilename_last;
            set => Set(ref _loadBinaryFilename_last, value);
        }



        public BackgroundWorker LoadBinaryFile(uint addr, string filename)
        {
            BackgroundWorker b = new BackgroundWorker();
            b.WorkerSupportsCancellation = true;
            var prg = new DlgProgress();
            prg.Owner = MainWindow;
            prg.Title = "Loading Binary...";
            prg.Message = $"Loading file {filename}";
            prg.Progress = 0;
            prg.Show();
            prg.Cancel += (o, e) => { b.CancelAsync(); };
            BusyInt = true;
            b.DoWork += (o, e) =>
            {
                try
                {
                    try
                    {
                        using (var rd = new FileStream(filename, FileMode.Open, FileAccess.Read))
                        {


                            int maxlen = DebugHostStatus.ComBufSize - 6;
                            byte[] buf = new byte[maxlen];
                            long len = rd.Length;
                            long done = 0;
                            int cur = 0;
                            Stopwatch sw = new Stopwatch();
                            sw.Start();
                            long last = sw.ElapsedMilliseconds - 200;
                            do
                            {
                                cur = rd.Read(buf, 0, maxlen);
                                var reply = DeIceProto.SendReqExpectReply<DeIceFnReplyWriteMem>(new DeIceFnReqWriteMem(addr, buf, 0, cur));
                                if (!reply.Success)
                                    throw new Exception($"Error copying data at {addr:X08}");
                                addr += (uint)cur;
                                done += cur;

                                if (cur == 0 || sw.ElapsedMilliseconds - last >= 200)
                                {
                                    MainWindow.Dispatcher.BeginInvoke(() =>
                                    {
                                        prg.Progress = (double)(100 * done) / (double)len;
                                    });
                                    last = sw.ElapsedMilliseconds;
                                }


                            } while (cur > 0 && !b.CancellationPending);

                        }
                    }
                    catch (Exception ex)
                    {
                        DoInvoke(() =>
                        {
                            Messages.Add($"Error loading binary file {ex.Message}");
                        });
                    }
                }
                finally
                {
                    DoInvoke(() =>
                    {
                        prg.Close();
                        BusyInt = false;
                    });
                }
            };

            LoadBinaryAddr_last = addr;
            LoadBinaryFilename_last = filename;

            b.RunWorkerAsync();
            return b;

        }

        void ParseSymbolOrAddress(string s, out string name, out uint addr)
        {
            name = null;
            if (Symbols.SymbolToAddress(s, out addr))
            {
                name = s;
            }
            else
            {
                //couldn't find symbol try as address
                try
                {
                    addr = Convert.ToUInt32(s, 16);
                }
                catch (Exception)
                {
                    throw new ArgumentException($"\"{s}\" is not a known symbol or hex number");
                }
                name = Symbols.FindNearest(addr);
            }

        }

        protected void Breakpoint_Changed(object sender, PropertyChangedEventArgs e)
        {
            DisassMemBlock?.BreakpointsUpdated();
        }

        public DeIceAppModel(IDossySerial serial, MainWindow mainWindow)
        {
            this.MainWindow = mainWindow;
            this.Serial = serial;

            _recentCommandFiles = new ObservableCollection<string>();
            RecentCommandFiles = new ReadOnlyObservableCollection<string>(_recentCommandFiles);
            _breakpoints = new ReadOnlyObservableCollection<BreakpointModel>(_breakpointsint);

            _regs = new RegisterSetModel(this);
            Regs.PropertyChanged += (o, e) =>
            {
                if (e.PropertyName == nameof(RegisterSetModel.IsStopped))
                {
                    RaisePropertyChangedEvent(nameof(HostBusy));
                }
            };


            if (mainWindow != null)
            {
                _regs.PropertyChanged += (o, e) =>
                {
                    if (e.PropertyName == nameof(RegisterSetModel.TargetStatus))
                    {
                        //really? should this be here?
                        DoInvoke(() => CommandManager.InvalidateRequerySuggested());
                    }
                };
            }

            Symbols = new DeIceSymbols(this);

            _breakpointsint.CollectionChanged += (o, e) =>
            {
                DisassMemBlock?.BreakpointsUpdated();
                if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (BreakpointModel item in e.OldItems)
                    {
                        //Removed items
                        item.PropertyChanged -= Breakpoint_Changed;
                    }
                }
                else if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (BreakpointModel item in e.NewItems)
                    {
                        //Added items
                        item.PropertyChanged += Breakpoint_Changed;
                    }
                }
            };

            CmdNext = new RelayCommand(
            o =>
            {
                try
                {
                    if (Regs.TargetStatus == DeIceProtoConstants.TS_BP)
                        if (ReExecCurBreakpoint())
                        {
                            RunFinish(true);
                            return;
                        }

                    this.Regs.SR.Data |= 0x8000;
                    DeIceProto.SendReqExpectReply<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { Regs = Regs.ToDeIceProtcolRegs() }); //ignore TODO: check?
                    ApplyBreakpoints();
                    DeIceProto.SendReq(new DeIceFnReqRun());
                }
                catch (Exception ex)
                {
                    Messages.Add($"{MessageNo():X4} ERROR:Executing Next\n{ ex.ToString() } ");
                    Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                }
            },
            o =>
            {
                return Regs.IsStopped;
            },
            "Step Next",
            Command_Exception
            );

            CmdCont = new RelayCommand(
                o =>
                {
                    try
                    {
                        ReExecCurBreakpoint();

                        Regs.SR.Data &= 0x7FFF;
                        DeIceProto.SendReqExpectReply<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { Regs = Regs.ToDeIceProtcolRegs() }); // ignore responese: TODO: check?
                        ApplyBreakpoints();
                        DeIceProto.SendReq(new DeIceFnReqRun());
                    }
                    catch (Exception ex)
                    {
                        Messages.Add($"{MessageNo():X4} ERROR:Executing Continue\n{ ex.ToString() } ");
                    }
                    Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                },
                o =>
                {
                    return Regs.IsStopped;
                },
                "Continue",
                Command_Exception

            );

            CmdTraceTo = new RelayCommand(
                o =>
                {
                    var dlg = new DlgTraceTo(this);
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;
                    //TODO: use binding and a viewmodel?

                    if (dlg.ShowDialog() == true)
                    {
                        TraceTo(dlg.Address);
                    }

                },
                o => { return Regs.IsStopped; },
                "Trace To...",
                Command_Exception
                );

            CmdDumpMem = new RelayCommand(
                o =>
                {
                    var dlg = new DlgDumpMem(this);
                    dlg.Title = "Dump Memory";
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;
                    //TODO: use binding and a viewmodel?

                    if (dlg.ShowDialog() == true)
                    {
                        byte[] buf = new byte[256];
                        DeIceProto.ReadMemBlock(dlg.Address, buf, 0, 256);

                        bool first = true;
                        StringBuilder l = new StringBuilder();
                        StringBuilder l2 = new StringBuilder();
                        for (int i = 0; i < 256; i++)
                        {
                            uint a = dlg.Address + (uint)i;
                            if (a % 16 == 0 || first)
                            {
                                first = false;
                                if (l.Length > 0)
                                    Messages.Add($"{l.ToString()} | {l2.ToString()}");
                                l.Clear();
                                l2.Clear();
                                l.Append($"{(a & ~0xF):X8} : ");
                                for (int j = 0; j < a % 16; j++)
                                {
                                    l.Append("   ");
                                    l2.Append(" ");
                                }
                            }
                            l.Append($" {buf[i]:X2}");
                            if (buf[i] > 32 && buf[i] < 128)
                                l2.Append((char)buf[i]);
                            else
                                l2.Append(".");
                        }
                        if (l.Length > 0)
                            Messages.Add($"{l.ToString()} | {l2.ToString()}");
                    }

                },
                o => { return Regs.IsStopped; },
                "Dump Memory",
                Command_Exception
            );


            CmdStop = new RelayCommand(
                o =>
                {
                    if (traceCancelSource is not null)
                    {
                        traceCancelSource.Cancel();
                        Thread.Sleep(100);
                    }
                    else
                    {
                        try
                        {
                            DeIceProto.SendReq(new DeIceFnReqReadRegs());
                        }
                        catch (Exception ex)
                        {
                            Messages.Add($"{MessageNo():X4} ERROR:Executing Stop\n{ ex.ToString() } ");
                            Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                        }
                    }

                },
                o =>
                {
                    return Regs.IsRunning;
                },
                "Stop",
                Command_Exception
            );
            CmdRefresh = new RelayCommand(
                o =>
                {
                    if (Regs.IsStopped)
                    {
                        int len = DisassMemBlock?.Data?.Length ?? 128;
                        uint st = DisassMemBlock?.BaseAddress ?? Regs.PC.Data;

                        var disdat = new byte[len];
                        DeIceProto.ReadMemBlock(st, disdat, 0, len);

                        DisassMemBlock = new DisassMemBlock(this, st, disdat);
                        DisassMemBlock.PC = Regs.PC.Data;
                    }

                },
                o =>
                {
                    return Regs.IsStopped;
                },
                "Refresh",
                Command_Exception
            );
            CmdDisassembleAt = new RelayCommand(
                o =>
                {
                    var dlg = new DlgDumpMem(this);
                    dlg.Title = "Disassemble At";
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;

                    if (dlg.ShowDialog() == true)
                    {
                        DisassembleAt(dlg.Address);
                    }

                },
                o =>
                {
                    return Regs.IsStopped;
                },
                "Disassemble At",
                Command_Exception
            );

            CmdBreakpoints_Add = new RelayCommand(
                o =>
                {
                    var dlg = new DlgDumpMem(this);
                    dlg.Title = "Add breakpoint at";
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;

                    if (dlg.ShowDialog() == true)
                    {
                        AddBreakpoint(dlg.Address);
                    }

                },
                o => true,
                "Add Breakpoint...",
                Command_Exception
                );

            CmdBreakpoints_Delete = new RelayCommand(
                o =>
                {
                    Breakpoints.Where(o => o.Selected).ToList().ForEach(o => RemoveBreakpoint(o.Address));
                },
                o => Breakpoints.Where(o => o.Selected).Any(),
                "Add Breakpoint...",
                Command_Exception
            );

            CmdWatches_Add = new RelayCommand(
                o =>
                {
                    var dlg = new DlgAddWatch(this);
                    dlg.Title = "Add watch";
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;

                    if (dlg.ShowDialog() == true)
                    {
                        Watches.Add(new WatchModel(dlg.Address, dlg.Symbol, dlg.WatchType, dlg.Indices));
                    }

                },
                o => true,
                "Add Breakpoint...",
                Command_Exception
                );

            CmdWatches_Delete = new RelayCommand(
                o =>
                {
                    Watches.Where(o => o.Selected).ToList().ForEach(o => RemoveWatch(o.Address));
                },
                o => Watches.Where(o => o.Selected).Any(),
                "Add Breakpoint...",
                Command_Exception
            );

            CmdLoadBinary = new RelayCommand(
                o =>
                {
                    var dlg = new DlgLoadBinary(this);
                    dlg.Owner = MainWindow;
                    dlg.Address = LoadBinaryAddr_last;
                    dlg.FileName = LoadBinaryFilename_last;
                    if (dlg.ShowDialog() == true)
                    {
                        LoadBinaryFile(dlg.Address, dlg.FileName);
                    }
                },
                o =>
                {
                    return Regs.IsStopped;
                },
                "Load Binary",
                Command_Exception
            );

            CmdRunScript = new RelayCommand(
                o =>
                {
                    var dlg = new OpenFileDialog();
                    dlg.Filter = "Script files|*.noi|All files|*.*";
                    dlg.CheckFileExists = true;
                    dlg.CheckPathExists = true;
                    dlg.FilterIndex = 1;
                    if (dlg.ShowDialog(MainWindow) == true)
                    {
                        ReadCommandFile(dlg.FileName);
                    }
                },
                o =>
                {
                    return Regs.IsStopped;
                },
                "Load Binary",
                Command_Exception
            );

            CmdOpenAScript = new RelayCommand(
                o =>
                {
                    ReadCommandFile((string)o);
                },
                o =>
                {
                    return Regs.IsStopped;
                },
                "Load Binary",
                Command_Exception
            );


            _deIceProto = new DeIceProtocolMain(Serial);

            DeIceProto.CommError += (o, e) =>
            {
                DoInvoke(new Action(
                delegate
                {
                    Messages.Add($"{MessageNo():X4} ERROR:{ e.Exception.ToString() }");
                })
                );
            };
            DeIceProto.OobDataReceived += (o, e) =>
            {
                DoInvoke(new Action(
                delegate
                {
                    Messages.Add($"{MessageNo():X4} OOB:{ e.Data }");
                })
                );
            };
            DeIceProto.FunctionReceived += (o, e) =>
            {
                DoInvoke(new Action(
                delegate
                {
                    Messages.Add($"{MessageNo():X4} FN:{ e.Function.FunctionCode } : { e.Function.GetType().Name }");

                    var x = e.Function as DeIceFnReplyRegsBase;
                    if (x != null)
                    {
                        Regs.FromDeIceRegisters(x.Registers);

                        if (!RunFinish(true))
                        {
                            // breakpoint hit but it returned false...carry on
                            ReExecCurBreakpoint();

                            DeIceProto.SendReqExpectReply<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { Regs = Regs.ToDeIceProtcolRegs() }); // ignore responese: TODO: check?
                            ApplyBreakpoints();

                            DeIceProto.SendReq(new DeIceFnReqRun());
                            Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;

                        }

                        //this is a horrid bodge for when get stuck it really shouldn't be "running" here!?
                        if (Regs.TargetStatus == 0)
                            Regs.TargetStatus = DeIceProtoConstants.TS_TRACE;

                    }
                })
                );
            };
            try
            {
                DebugHostStatus = DeIceProto.SendReqExpectReply<DeIceFnReplyGetStatus>(new DeIceFnReqGetStatus());
                DeIceProto.SendReq(new DeIceFnReqReadRegs());
            }
            catch (Exception)
            {
                // assume running on failuer
                Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
            }

        }

        public bool Init()
        {
            //try and give it a tickle to see if its awake!
            try
            {
                DeIceProto.Flush();
                var r = DeIceProto.SendReqExpectReply<DeIceFnReplyRegsBase>(new DeIceFnReqReadRegs());
                Regs.FromDeIceRegisters(r.Registers);
                RunFinish(false);
                return true;
            }
            catch (Exception ex)
            {
                Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                return false;
            }

        }

        public void Command_Exception(object sender, ExceptionEventArgs args)
        {
            if (sender is RelayCommand)
            {
                Messages.Add($"Error in {(sender as RelayCommand).Name}:{args.Exception.Message}");
            }
            else
            {
                Messages.Add($"Error {args.Exception.Message}");
            }
            Messages.Add(args.Exception.ToString());
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
        /// when running this contains the list of breakpoints that have actually been sucessfully set
        /// </summary>
        private List<BreakpointModel> _activeBreakpoints = new List<BreakpointModel>();

        protected bool ReExecCurBreakpoint()
        {
            // check to see if we are currently in a breakpoint and the PC is pointing just after the breakpoint instruction
            if (Regs.TargetStatus == DeIceProtoConstants.TS_BP)
            {
                try
                {
                    BreakpointModel curbp = _activeBreakpoints.Concat(Breakpoints).Where(b => b.Address == Regs.PC.Data).FirstOrDefault();
                    if (curbp != null)
                    {
                        //remove the breakpoint from the active list
                        _activeBreakpoints.Remove(curbp);

                        //restore the original instruction
                        DeIceProto.SendReqExpectReply<DeIceFnReplySetBytes>(
                            new DeIceFnReqSetBytes()
                            {
                                Items = new[] {
                                    new DeIceFnReqSetBytes.DeIceSetBytesItem()
                                    {
                                        Address = curbp.Address, Data = (byte)(curbp.OldOP >> 8)
                                    },
                                    new DeIceFnReqSetBytes.DeIceSetBytesItem()
                                    {
                                        Address = curbp.Address + 1, Data = (byte)curbp.OldOP
                                    },
                                }
                            }
                        );


                        // save old status register
                        ushort oldSR = (ushort)Regs.SR.Data;

                        // Set Trace mode and execute
                        Regs.SR.Data |= 0x8000;
                        DeIceProto.SendReqExpectReply<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { Regs = Regs.ToDeIceProtcolRegs() });
                        var regs = DeIceProto.SendReqExpectReply<DeIceFnReplyRun>(new DeIceFnReqRun() { });

                        Regs.FromDeIceRegisters(regs.Registers);

                        // Restore Trace mode - TODO: What to do if the BP instruction affected the SR traceflag
                        Regs.SR.Data &= 0x7FFF;
                        Regs.SR.Data |= (uint)(oldSR & 0x8000);

                        return true;

                    }

                }
                catch (Exception ex)
                {
                    Messages.Add("Unexpected error re-executing breakpoint");
                }
            }
            return false;
        }

        public void ApplyBreakpoints()
        {
            ReExecCurBreakpoint();

            // how many breakpoints we can fit in the buffer
            int MAXBP = (DebugHostStatus.ComBufSize / 8) - 2;
            var bp2a = Breakpoints.Where(o => o.Enabled && !_activeBreakpoints.Where(a => a.Address == o.Address).Any());

            while (bp2a.Any())
            {
                var chunk = bp2a.Take(MAXBP).ToArray();
                var req = Breakpoints2Req(chunk, true);
                var ret = DeIceProto.SendReqExpectReply<DeIceFnReplySetBytes>(req);
                for (int i = 0; i < ret.Data.Length / 2; i++)
                {
                    chunk[i].OldOP = (ushort)((ret.Data[i * 2] << 8) | ret.Data[1 + i * 2]);
                }
                _activeBreakpoints.AddRange(bp2a.Take(ret.Data.Length));
                bp2a = bp2a.Skip(ret.Data.Length);

                //if we got fewer back than we sent then it was dodgy memory, skip that in the list and set to disabled.
                if (ret.Data.Length < chunk.Length && bp2a.Any())
                {
                    var bbad = bp2a.First();
                    bbad.Enabled = false;
                    Messages.Add($"The breakpoint at {bbad.Address:X08} couldn't be set and was disabled");
                    bp2a = bp2a.Skip(1);
                }
            }


            //remove any that are now disabled or no longer in the Breakpoints list
            List<BreakpointModel> rembp = _activeBreakpoints.Where(a => a.Enabled == false || !Breakpoints.Where(b => b.Address == a.Address).Any()).ToList();
            UnApplyBreakpoints(rembp);
            rembp.ForEach(a => _activeBreakpoints.Remove(a));

        }

        DeIceFnReqSetBytes Breakpoints2Req(IEnumerable<BreakpointModel> bp, bool apply)
        {
            if (apply)
            {
                return new DeIceFnReqSetBytes()
                {
                    Items = bp.Select(
                    bp => new DeIceFnReqSetBytes.DeIceSetBytesItem[] {
                        new DeIceFnReqSetBytes.DeIceSetBytesItem()
                        {
                            Address = bp.Address,
                            Data = (byte)(DebugHostStatus.BreakPointInstruction >> 8)
                        },
                        new DeIceFnReqSetBytes.DeIceSetBytesItem()
                        {
                            Address = bp.Address + 1,
                            Data = (byte)DebugHostStatus.BreakPointInstruction
                        }
}
                    ).SelectMany(o => o).ToArray()
                };
            }
            else
            {
                return new DeIceFnReqSetBytes()
                {
                    Items = bp.Select(
                        bp => new DeIceFnReqSetBytes.DeIceSetBytesItem[] {
                        new DeIceFnReqSetBytes.DeIceSetBytesItem()
                        {
                            Address = bp.Address,
                            Data = (byte)(bp.OldOP >> 8)
                        },
                        new DeIceFnReqSetBytes.DeIceSetBytesItem()
                        {
                            Address = bp.Address + 1,
                            Data = (byte)bp.OldOP
                        }
}
                    ).SelectMany(o => o).ToArray()
                };

            }
        }


        public void UnApplyBreakpoints(IEnumerable<BreakpointModel> items)
        {
            // how many breakpoints we can fit in the buffer
            int MAXBP = (DebugHostStatus.ComBufSize / 8) - 2;
            while (items.Any())
            {
                var chunk = items.Take(MAXBP).ToArray();
                var req = Breakpoints2Req(chunk, false);
                var ret = DeIceProto.SendReqExpectReply<DeIceFnReplySetBytes>(req);

                for (int i = 0; i < chunk.Length && 2 * i < ret.Data.Length; i++)
                {
                    ushort r = (ushort)((ret.Data[i * 2] << 8) | ret.Data[1 + i * 2]);
                    if (r != DebugHostStatus.BreakPointInstruction)
                    {
                        var bb = chunk[i];
                        Messages.Add($"WARNING: breakpoint at {bb.Address:X08} could not be reset, code is in an unexpected state. Found={ret.Data[i * 2]:X02}{ret.Data[1 + i * 2]:X02}, expected={DebugHostStatus.BreakPointInstruction:X04}");
                    }
                }

                items = items.Skip(ret.Data.Length / 2);

                //if we got fewer back than we sent then it was dodgy memory, skip that in the list and set to disabled.
                if (ret.Data.Length < chunk.Length && _activeBreakpoints.Any())
                {
                    var bbad = items.First();
                    bbad.Enabled = false;
                    Messages.Add($"WARNING: breakpoint at {bbad.Address:X08} could not be reset, code is in an unexpected state. The memory was not writeable!");
                    items.Skip(0);
                }

            }
        }

        /// <summary>
        /// Run has finished (or initial read regs reply received) update the display, registers should already have been updated
        /// </summary>
        /// <returns>If this is a breakpoint the condition is checked and returned here</returns>
        public bool RunFinish(bool unApplyBreakpoints = true)
        {
            bool ret = true;
            try
            {
                //undo old breakpoints
                if (unApplyBreakpoints)
                {
                    UnApplyBreakpoints(_activeBreakpoints);
                    _activeBreakpoints.Clear();
                }

                if (_debugHostStatus == null)
                {
                    DebugHostStatus = DeIceProto.SendReqExpectReply<DeIceFnReplyGetStatus>(new DeIceFnReqGetStatus());
                }

                DisassembleAt(Regs.PC.Data);

                foreach (var w in Watches)
                {
                    byte[] buf = new byte[w.DataSize];
                    DeIceProto.ReadMemBlock(w.Address, buf, 0, (int)w.DataSize);
                    w.Data = buf;
                }

                if (Regs.TargetStatus == DeIceProtoConstants.TS_BP)
                {
                    BreakpointModel curbp = _activeBreakpoints.Concat(Breakpoints).Where(b => b.Address == Regs.PC.Data).FirstOrDefault();
                    if (curbp != null && curbp.ConditionCode != null)
                    {
                        ret = curbp.ConditionCode.Execute();
                        Messages.Add($"Encountered breakpoint at {curbp.Address:X08} [{curbp.SymbolStr ?? ""}] {((!ret) ? " - skipped" : "")}");


                    }
                }


            }
            catch (Exception ex)
            {
                Messages.Add($"{MessageNo():X4} ERROR:reading memory\n{ ex.ToString() } ");
                Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
            }
            return ret;
        }

        public void DisassembleAt(uint addr)
        {
            //check to see if pc is in the current DisassMemBlock, if not either extend or load new
            if (DisassMemBlock == null || DisassMemBlock.BaseAddress > addr || DisassMemBlock.EndPoint <= addr + 64)
            {
                if (DisassMemBlock != null && (addr - DisassMemBlock.EndPoint) < 1024)
                {
                    //close just extend until we're in range
                    DisassMemBlock.MorePlease(addr + 512 - DisassMemBlock.EndPoint);
                }
                else
                {
                    var disdat = new byte[1024];
                    DeIceProto.ReadMemBlock(addr, disdat, 0, 1024);

                    DisassMemBlock = new DisassMemBlock(this, addr, disdat);
                }
            }

            //in range just update pc
            DisassMemBlock.PC = Regs.PC.Data;

        }

        public void TraceTo(uint addr)
        {

            traceCancelSource = new CancellationTokenSource();
            CancellationToken cancellationToken = traceCancelSource.Token;

            Task.Run(() =>
            {

                try
                {
                    byte lastTargetStatus = Regs.TargetStatus;
                    try
                    {
                        Regs.SR.Data |= 0x8000;
                        DeIceProto.SendReqExpectReply<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { Regs = Regs.ToDeIceProtcolRegs() }); //ignore TODO: check?

                        ReExecCurBreakpoint();

                        ApplyBreakpoints();

                        while (Regs.PC.Data != addr && !cancellationToken.IsCancellationRequested)
                        {
                            var rr = DeIceProto.SendReqExpectReply<DeIceFnReplyRun>(new DeIceFnReqRun());
                            if (rr.Registers.TargetStatus != DeIceProtoConstants.TS_TRACE)
                            {
                                Regs.FromDeIceRegisters(rr.Registers);
                                bool cont = false;
                                DoInvoke(() => cont = RunFinish(true));
                                if (!cont)
                                {
                                    break;
                                }
                            }

                            lastTargetStatus = rr.Registers.TargetStatus;
                            var rr_run = rr.Registers with { TargetStatus = DeIceProtoConstants.TS_RUNNING };

                            Regs.FromDeIceRegisters(rr_run);

                            //TODO: Move invoke inside runfinish where it is needed
                            DoInvoke(() => RunFinish(false));

                        }
                    }
                    finally
                    {
                        Regs.TargetStatus = (byte)lastTargetStatus;
                    }
                }
                catch (Exception ex)
                {
                    DoInvoke(() =>
                    {
                        Messages.Add($"TRACE ERROR: {ex.Message}");
                        Messages.Add(ex.ToString());
                        Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                    });

                }
            }).ContinueWith((t) =>
            {
                traceCancelSource = null;
            }); ;
        }

        public BreakpointModel AddBreakpoint(uint address)
        {
            int i = 0;
            while (i < _breakpointsint.Count && _breakpointsint[i].Address < address)
                i++;

            var ret = new BreakpointModel(this) { Address = address, Enabled = true };
            _breakpointsint.Insert(i, ret);
            return ret;
        }

        public void RemoveBreakpoint(uint address)
        {
            _breakpointsint.Where(o => o.Address == address).ToList().ForEach(o => _breakpointsint.Remove(o));
        }

        public void RemoveWatch(uint address)
        {
            _watches.Where(o => o.Address == address).ToList().ForEach(o => _watches.Remove(o));
        }

        public byte GetByte(uint addr)
        {
            var r = DeIceProto.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr, Len = 1 });
            return r.Data[0];
        }

        public ushort GetWord(uint addr)
        {
            var r = DeIceProto.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr, Len = 2 });
            return (ushort)((r.Data[0] << 8) | r.Data[1]);
        }

        public uint GetLong(uint addr)
        {
            var r = DeIceProto.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr, Len = 4 });
            return (ushort)((r.Data[0] << 8) | (r.Data[1] << 16) | (r.Data[2] << 8) | r.Data[3]);
        }


    }
}
