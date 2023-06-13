namespace DeIceProtocol
{
    public static class DeIceProtoConstants
    {
        public const byte FN_GET_STAT = 0xFF;      // reply with device info
        public const byte FN_READ_MEM = 0xFE;      // reply with data
        public const byte FN_WRITE_MEM = 0xFD;       // reply with status(+/-)
        public const byte FN_READ_RG = 0xFC;       // reply with registers
        public const byte FN_WRITE_RG = 0xFB;      // reply with status
        public const byte FN_RUN_TARG = 0xFA;      // reply(delayed) with registers
        public const byte FN_SET_BYTES = 0xF9;     // reply with data(truncate if error)
        public const byte FN_IN = 0xF8;            // input from port
        public const byte FN_OUT = 0xF7;           // output to port

        public const byte FN_MIN = 0xF7;           // MINIMUM RECOGNIZED FUNCTION CODE
        public const byte FN_ERROR = 0xF0;         // error reply to unknown op-code

        public const byte TS_RUNNING = 0;
        public const byte TS_BP = 1;
        public const byte TS_TRACE = 2;
        public const byte TS_ILLEGAL = 3;

        public const byte HOST_68k = 68;
        public const byte HOST_x86_16 = 86;
        public const byte HOST_x86_386 = 87;
        public const byte HOST_ARM2 = 0xA2;
    }
}
