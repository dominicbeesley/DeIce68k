# DeIce68k
 A debugger for the Blitter 68008




##TODO:

- on branch/jump check for jump to badly disassembled instruction (see BRK_HANDLER) and refresh disassembly view
- breakpoints (use ILLEGAL op)
- hardware breakpoints (table of breakpoints in FPGA cause debug NMI?)
- memory DUMP
- memory inspector/editor
- commandline interpreter - using Antlr?


Running:
- set target status to 0 when running

Trace:
 - if returns a timeout set target status to running

TraceTo:
 - speed up - see where the bottleneck is - update ui less frequently?
 - Cancel, cancellation token and a command from the UI somehow