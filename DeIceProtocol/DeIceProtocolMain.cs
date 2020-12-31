using System;
using System.Linq;
using System.Text;
using System.Threading;
using DossySerialPort;

namespace DeIceProtocol
{

    public class DeIceOobDataReceivedEventArgs : EventArgs
    {
        public string Data { get; }

        public DeIceOobDataReceivedEventArgs(string data)
            : base()
        {
            this.Data = data;
        }
    }

    public delegate void DeIceOobDataReceivedEvent(object sender, DeIceOobDataReceivedEventArgs e);

    public class DeIceFunctionReceivedEventArgs : EventArgs
    {
        public DeIceFnReplyBase Function { get; }

        public DeIceFunctionReceivedEventArgs(DeIceFnReplyBase fn)
            : base()
        {
            this.Function = fn;
        }
    }

    public delegate void DeIceFunctionReceivedEvent(object sender, DeIceFunctionReceivedEventArgs e);


    public class DeIceComErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public DeIceComErrorEventArgs(Exception e)
            : base()
        {
            this.Exception = e;
        }
    }

    public delegate void DeIceCommErrorEvent(object sender, DeIceComErrorEventArgs e);


    public class DeIceProtocolMain
    {
        const int RESPONSE_TIMEOUT = 100; //how long to wait until a command's response time's out
        const int SHORT_TIMEOUT = 100; // how long to wait for more oob data before showing
        const int LONG_TIMEOUT = 2000; // how long to wait for other threads to release
        const int BUF_SIZE = 256;

        public event DeIceOobDataReceivedEvent OobDataReceived;
        public event DeIceCommErrorEvent CommError;
        public event DeIceFunctionReceivedEvent FunctionReceived;

        public enum State_e
        {
            /// <summary>
            /// Nothin is happening, if data arrives go to commandRead/oobRead state and consume data
            /// </summary>
            idle,
            /// <summary>
            /// Set when a command is been written or an immediate response is being awaited
            /// </summary>
            blocking,
            /// <summary>
            /// when data are being received from the client that started with an Fx command
            /// </summary>
            commandRead,
            /// <summary>
            /// When data are being received from the client that is not a command, this will be returned as plain characters to be consumed
            /// </summary>
            oobRead
        }

        IDossySerial _serial = null;

        private object stateLock = new object();

        State_e state;

        public DeIceProtocolMain(IDossySerial serial)
        {
            this._serial = serial;

            _serial.DataReceived += _serial_DataReceived;
            
        }

        private void _serial_DataReceived(object o, EventArgs e) // object sender, SerialDataReceivedEventArgs e)
        {
            while (true)
            {
                bool ok = false;
                byte b = 0;
                lock (stateLock)
                {
                    if (state == State_e.idle)
                    {
                        if (_serial.Available <= 0)
                            return;

                        try
                        {
                            b = _serial.ReadByte(SHORT_TIMEOUT);
                            ok = true;
                        }
                        catch (TimeoutException)
                        {
                            return;
                        }
                    }
                }
                if (ok)
                {
                    if (b >= DeIceProtoConstants.FN_MIN)
                        state = State_e.commandRead;
                    else
                        state = State_e.oobRead;

                    if (state == State_e.commandRead)
                        //Task.Factory.StartNew(() => DoCommandRead((byte)i));
                        DoCommandReadAndRaise(b);
                    else if (state == State_e.oobRead)
                        //Task.Factory.StartNew(() => DoOobRead((byte)i));
                        DoOobRead(b);
                }
                else
                {
                    return;
                }
            }
        }

        public T SendReqExpectReply<T>(DeIceFnReqBase fn) where T : DeIceFnReplyBase
        {
            //wait for idle
            long timeout = 0;
            while (timeout < LONG_TIMEOUT)
            {
                bool ok = false;
                lock (stateLock)
                {
                    if (state == State_e.idle)
                    {
                        ok = true;
                        state = State_e.blocking;
                    }
                }

                if (ok)
                {
                    try
                    {
                        byte[] buf = DeIceFnFactory.CreateDataFromReq(fn);
                        _serial.Write(buf, 0, buf.Length, SHORT_TIMEOUT);
                        var r = DoCommandReadInt();
                        if (r is not T)
                            throw new DeIceProtocolException($"Unexpected return from client, expected a { nameof(T) }, received a { r?.GetType()?.Name ?? null }");
                        return (T)r;
                    }
                    finally
                    {
                        lock (stateLock)
                        {
                            state = State_e.idle;
                        }
                    }
                } 
                else
                {
                    Thread.Sleep(SHORT_TIMEOUT);
                }

                timeout += SHORT_TIMEOUT;
            }
            throw new TimeoutException("Timed out waiting for idle in SendReq");
        }

        public void SendReq(DeIceFnReqBase fn)
        {
            //wait for idle
            long timeout = 0;
            while (timeout < LONG_TIMEOUT)
            {
                bool ok = false;
                lock (stateLock)
                {
                    if (state == State_e.idle)
                    {
                        ok = true;
                        state = State_e.blocking;
                    }
                }

                if (ok)
                {
                    try
                    {
                        byte[] buf = DeIceFnFactory.CreateDataFromReq(fn);
                        _serial.Write(buf, 0, buf.Length, SHORT_TIMEOUT);
                        return;
                    }
                    finally
                    {
                        lock (stateLock)
                        {
                            state = State_e.idle;
                        }
                    }
                }
                else
                {
                    Thread.Sleep(SHORT_TIMEOUT);
                }

                timeout += SHORT_TIMEOUT;
            }
            throw new TimeoutException("Timed out waiting for idle in SendReq");
        }

        const int READMEMMAX = 32;

        public void ReadMemBlock(uint addr, byte[] disdat, int offset, int len)
        {
            int offs = offset;

            while (len > 0)
            {
                byte l2;
                if (len > READMEMMAX)
                    l2 = READMEMMAX;
                else
                    l2 = (byte)len;

                var rm = this.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr, Len = l2 });

                Array.Copy(rm.Data, 0, disdat, offs, l2);

                len -= l2;
                addr += l2;
                offs += l2;
            }

        }

        private void DoOobRead(byte first)
        {
            byte[] buf = new byte[BUF_SIZE];
            int bufptr = 0;

            try
            {
                try
                {
                    bool to = false;
                    byte b = first;
                    bufptr = 0;
                    //keep reading until \r or \n timeout or bufferfull
                    do
                    {
                        buf[bufptr++] = b;

                        if (bufptr >= BUF_SIZE - 1 || b == 13 || b == 10)
                        {
                            if (b == 13 || b == 10)
                            {
                                bufptr--;
                                byte nb;
                                try
                                {
                                    nb = _serial.PeekByte(SHORT_TIMEOUT);
                                } catch(TimeoutException)
                                {
                                    nb = 0;
                                }
                                if (
                                    (b == 13 && nb == 10) ||
                                    (b == 10 && nb == 13))
                                {
                                    try
                                    {
                                        _serial.ReadByte(SHORT_TIMEOUT);
                                    }
                                    catch (TimeoutException) { }
                                }
                            }

                            RaiseOobData(Encoding.GetEncoding(28591).GetString(buf, 0, bufptr));
                            bufptr = 0;
                        }


                        try
                        {
                            b = _serial.PeekByte(SHORT_TIMEOUT);
                            if (b >= DeIceProtoConstants.FN_MIN)
                                to = true;
                            else
                                b = _serial.ReadByte(SHORT_TIMEOUT);
                        }
                        catch (TimeoutException)
                        {
                            to = true;
                        }

                    } while (!to);

                    if (bufptr != 0)
                        RaiseOobData(Encoding.GetEncoding(28591).GetString(buf, 0, bufptr));

                }
                finally
                {
                    lock (stateLock)
                    {
                        state = State_e.idle;
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseComError(ex);
            }
        }

        private void RaiseComError(Exception ex)
        {
            CommError?.Invoke(this, new DeIceComErrorEventArgs(ex));
        }

        private void RaiseOobData(string data)
        {
            OobDataReceived?.Invoke(this, new DeIceOobDataReceivedEventArgs(data));
        }

        private void DoCommandReadAndRaise(byte first)
        {

            try
            {
                DeIceFnReplyBase cmd;

                try
                {
                    cmd = DoCommandReadInt(first, true);

                }
                finally
                {
                    lock (stateLock)
                    {
                        state = State_e.idle;
                    }
                }

                RaiseFunctionReceived(cmd);

            }
            catch (Exception ex)
            {
                RaiseComError(ex);
            }
        }


        private DeIceFnReplyBase DoCommandReadInt(byte first=0, bool hasFirst=false)
        {
            byte[] buf = new byte[BUF_SIZE];
            int bufptr = 0;
            byte lenctr = 0;
            if (!hasFirst)
                first = _serial.ReadByte(SHORT_TIMEOUT);
            bufptr = 0;
            buf[bufptr++] = first;
            byte ck = first;

            // get length
            lenctr = _serial.ReadByte(SHORT_TIMEOUT);
            buf[bufptr++] = lenctr;
            ck += lenctr;

            byte i;

            for (byte j = 0; j < lenctr; j++)
            {
                i = _serial.ReadByte(SHORT_TIMEOUT);
                if (bufptr >= BUF_SIZE - 1)
                    throw new Exception("Buffer full");
                buf[bufptr++] = (byte)i;
                ck += (byte)i;
            }

            i = _serial.ReadByte(SHORT_TIMEOUT);
            buf[bufptr++] = i;

            ck += i;
            if (ck != 0)
                throw new Exception("Bad checksum");

            return DeIceFnFactory.CreateReplyFromData(buf.Take(lenctr + 3).ToArray());
        }

        private void RaiseFunctionReceived(DeIceFnReplyBase deIceFnBase)
        {
            FunctionReceived?.Invoke(this, new DeIceFunctionReceivedEventArgs(deIceFnBase));
        }
    
    }
}
