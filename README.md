# DeIce68k
 A debugger for the Blitter 68008

##PROBLEMS:
- double click on continue causes odd behaviour in client (looks to resend original regs twice?) and memory error need to block between memory read and regs?


##TODO:

- fix / remove special ABI
- stop button for trace
- icons
- on branch/jump check for jump to badly disassembled instruction (see BRK_HANDLER) and refresh disassembly view
- breakpoints (use ILLEGAL op)
- hardware breakpoints (table of breakpoints in FPGA cause debug NMI?)
- memory DUMP
- memory inspector/editor
- commandline interpreter - using Antlr?


Refresh:
 - (auto)refresh disassembly on break/load or if current word looks wrong?