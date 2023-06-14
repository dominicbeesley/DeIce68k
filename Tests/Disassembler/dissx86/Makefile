AS=nasm
ASFLAGS=-O0

MAP2SYM=../scripts/map2sym.pl

TARGETS=x86.bin 186.bin 286.bin 386.bin

BUILD=./build


TARGETS2=$(addprefix $(BUILD)/,$(TARGETS))

$(shell mkdir -p $(BUILD))

all:: $(TARGETS2)

$(BUILD)/x86.bin: disasstest.asm
	$(AS) $(ASFLAGS) -DCPU_x86 -fbin -o $@ -l $(basename $@).lst $< >$(basename $@).map
	$(MAP2SYM) $(basename $@).map $(basename $@).noi .text=0000 .bss=0000

$(BUILD)/186.bin: disasstest.asm
	$(AS) $(ASFLAGS) -DCPU_186 -fbin -o $@ -l $(basename $@).lst $< >$(basename $@).map
	$(MAP2SYM) $(basename $@).map $(basename $@).noi .text=0000 .bss=0000

$(BUILD)/286.bin: disasstest.asm
	$(AS) $(ASFLAGS) -DCPU_286 -fbin -o $@ -l $(basename $@).lst $< >$(basename $@).map
	$(MAP2SYM) $(basename $@).map $(basename $@).noi .text=0000 .bss=0000

$(BUILD)/386.bin: disasstest.asm
	$(AS) $(ASFLAGS) -DCPU_386 -fbin -o $@ -l $(basename $@).lst $< >$(basename $@).map
	$(MAP2SYM) $(basename $@).map $(basename $@).noi .text=0000 .bss=0000


clean::
	-rm $(TARGETS2) 
	-rm $(patsubst %.bin, %.lst, $(TARGETS2))
	-rm $(patsubst %.bin, %.map, $(TARGETS2))
	-rm $(patsubst %.bin, %.noi, $(TARGETS2))
	-rm $(OBJS2) 
	-rm $(BUILD)/*