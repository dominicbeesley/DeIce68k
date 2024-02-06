namespace Disass65816.Emulate
{
    /// <summary>
    /// The entire state of a 65816 is represented in these registers
    /// </summary>
    public interface IRegsEmu65816
    {
        int A { get; set; }
        int B { get; set; }
        Tristate C { get; set; }
        Tristate D { get; set; }
        int DB { get; set; }
        int DP { get; set; }
        Tristate E { get; set; }
        Tristate I { get; set; }
        Tristate MS { get; set; }
        Tristate N { get; set; }
        int PB { get; set; }
        int PC { get; set; }
        int SH { get; set; }
        int SL { get; set; }
        Tristate V { get; set; }
        int X { get; set; }
        Tristate XS { get; set; }
        int Y { get; set; }
        Tristate Z { get; set; }

        IRegsEmu65816 Clone();
        public int memory_read(int ea);
        public void memory_write(int value, int ea);

    }
}