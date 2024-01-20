# DeIce68k
 A debugger for the Blitter 68008, x86, ARM2 and others...

##PROBLEMS:
- double click on continue causes odd behaviour in client (looks to resend original regs twice?) and memory error need to block between memory read and regs?


##TODO:

- fix / remove special ABI
- icons
- on branch/jump check for jump to badly disassembled instruction (see BRK_HANDLER) and refresh disassembly view
- breakpoints (use ILLEGAL op)
- hardware breakpoints (table of breakpoints in FPGA cause debug NMI?)
- memory inspector/editor
- commandline interpreter - using Antlr?


