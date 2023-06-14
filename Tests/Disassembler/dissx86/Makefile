AS=nasm
ASFLAGS=

TARGETS=disasstest.bin

BUILD=./build

TARGETS2=$(addprefix $(BUILD)/,$(TARGETS))

$(shell mkdir -p $(BUILD))

all:: $(TARGETS2)

$(BUILD)/disasstest.bin: disasstest.asm
	$(AS) $(ASFLAGS) -fbin -o $@ -l $(basename $@).lst $<

clean::
	-rm $(TARGETS2) 
	-rm $(OBJS2) 
	-rm $(BUILD)/*