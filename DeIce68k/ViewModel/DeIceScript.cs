using DisassShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DeIce68k.ViewModel
{
    public class DeIceScript
    {
        private DeIceAppModel _app;

        Regex reDef;
        Regex reWatchSym;
        Regex reBreakpoint;
        //TODO: reLoad is in wrong order compared to NoIce!
        Regex reLoad;
        Regex reRemark;
        Regex reReg;

        public DeIceScript(DeIceAppModel app)
        {
            this._app = app;

            var reAd = app.GetDisass().AddressFactory.AddressRegEx;
            var reSymbol = @"\w+";
            var reSymbolOrAddr = $@"(?:{reSymbol}|{reAd})";

            reDef = new Regex(@$"^\s*DEF(?:INE)?\s+(\w+)\s+({reSymbolOrAddr})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            reWatchSym = new Regex(@$"^w(?:atch)?\s+({reSymbolOrAddr})(?:\s+%(\w+))?(\[([0-9]+)\])*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            reBreakpoint = new Regex(@$"^b(?:reakpoint)?\s+({reSymbolOrAddr})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            //TODO: reLoad is in wrong order compared to NoIce!
            reLoad = new Regex(@$"^l(?:oad)?\s+({reSymbolOrAddr})\s+(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            
            reRemark = new Regex(@"(;|REM)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            reReg = new Regex(@$"REG\s+(\w+)\s+({reSymbolOrAddr})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        }

        public void ExecuteScript(string pathname)
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
                            _app.AppendMessage($"Error parsing line {lineno}:{l}\n{ex.Message}");
                        }
                    }
                }

            }
        }


        public void ParseCommand(string line, string directory)
        {
            var mRem = reRemark.Match(line);
            if (mRem.Success)
                return;

            var mD = reDef.Match(line); //TODO: symbolt types
            if (mD.Success)
            {
                //SYMBOL DEF
                string sym = mD.Groups[1].Value;

                string name;
                DisassAddressBase addr;
                _app.ParseSymbolOrAddress(mD.Groups[2].Value, out name, out addr);

                _app.Symbols.Add(sym, addr, DisassShared.SymbolType.Pointer);
                return;
            }

            var mWA = reWatchSym.Match(line);
            if (mWA.Success)
            {

                string name = null;
                DisassAddressBase addr;

                _app.ParseSymbolOrAddress(mWA.Groups[1].Value, out name, out addr);

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
                _app.Watches.Add(new WatchModel(addr, name, t, dims));
                return;
            }

            var mBP = reBreakpoint.Match(line);
            if (mBP.Success)
            {
                string name = null;
                DisassAddressBase addr;
                _app.ParseSymbolOrAddress(mBP.Groups[1].Value, out name, out addr);
                _app.AddBreakpoint(addr);
                return;
            }

            var mLoad = reLoad.Match(line);
            if (mLoad.Success)
            {
                string name = null;
                DisassAddressBase addr;
                _app.ParseSymbolOrAddress(mLoad.Groups[1].Value, out name, out addr);
                string filename = mLoad.Groups[2].Value;
                _app.LoadBinaryFile(addr, Path.Combine(directory, filename));
                return;

            }

            var mReg = reReg.Match(line);
            if (mReg.Success)
            {
                string reg = mReg.Groups[1].Value;
                string val = mReg.Groups[2].Value;

                DisassAddressBase addr;
                string name = null;
                _app.ParseSymbolOrAddress(val, out name, out addr);

                RegisterModel r = _app.Regs?.RegByName(reg);
                if (r == null)
                    throw new ArgumentException($"Unrecognised register {reg}");

                //TODO: sort this out it's no right
                r.Data = (uint)addr.Canonical;
                return;
            }
            throw new ArgumentException($"Unrecognised command:{line}");

        }


    }
}
