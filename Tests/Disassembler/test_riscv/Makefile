		include ./Makefile.defs

ASMINCS=$(wildcard $(TOP)/asm-includes/*.inc) $(filter-out version-date.inc, $(wildcard *.inc))
DEPS=$(INCS) $(ASMINCS)
OBJS=test


TARGETS=test

OBJS_O = $(addprefix $(BUILD)/, $(addsuffix .o, $(OBJS)))
TARGETS_O = $(addprefix $(BUILD)/, $(addsuffix .bin, $(TARGETS)))

.PHONY: all clean

all:	$(TARGETS_O)
ssd:	all
deploy:	all

.PRECIOUS:build/*

$(BUILD)/%.s:	%.c
	$(RISCV_CC) $(RISCV_CC_FLAGS) -O3 -c -S -o $@ -g $<

$(BUILD)/%.o: 	$(BUILD)/%.s $(DEPS) 
	$(RISCV_AS) $(RISCV_AS_FLAGS) -c -o $@ -g -Wa,-alghmns=$(basename $@).lst $<


$(BUILD)/%.o: 	%.S $(DEPS) 
	$(RISCV_AS) $(RISCV_AS_FLAGS) -c -o $@ -g -Wa,-alghmns=$(basename $@).lst $<

$(BUILD)/%.elf: $(OBJS_O) %.lds
	$(RISCV_LD) $(RISCV_LD_FLAGS) -Os -ffreestanding -nostdlib -o $@ \
	-Wl,--build-id=none,-Bstatic,-T,$(filter %.lds, $^),-Map,$(basename $@).map,--strip-debug \
		$(filter %.o,$^) -lgcc 
	$(RISCV_NM) --format=posix $@ >$(basename $@).sy1
	sed -n -E 's/(\w+)\s+\w\s+([0-9a-f]+)/DEF \1 \2/ip' <$(basename $@).sy1 >$(basename $@).noi


$(BUILD)/test.bin: $(BUILD)/test.elf
	$(RISCV_OBJCOPY) -O binary $< $@



clean:
	-rm $(TARGETS_O)
	-rm $(addsuffix .inf, $(TARGETS_O))
	-rm $(patsubst %.bin, %.map, $(TARGETS_O))
	-rm $(patsubst %.bin, %.sy1, $(TARGETS_O))
	-rm $(patsubst %.bin, %.elf, $(TARGETS_O))
	-rm $(OBJS_O)
	-rm $(patsubst %.o, %.lst, $(OBJS_O))


