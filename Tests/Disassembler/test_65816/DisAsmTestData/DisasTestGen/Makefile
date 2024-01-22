
AS=ca65
ASOPT=
LD=ld65
LDOPT=
DA=da65
DAOPT=

BUILD=./build

X:=(shell mkdir -p $(BUILD))

TARGETS=$(BUILD)/test.bin #$(BUILD)/test.da.s

all: $(TARGETS)

$(BUILD)/test.s:gen.pl
	./gen.pl >$@


$(BUILD)/%.o:$(BUILD)/%.s
	$(AS) $(ASOPT) -o $@ -l $(patsubst %.o, %.lst, $@) $<

$(BUILD)/%.bin:$(BUILD)/%.o link.cfg
	$(LD) $(LDOPT) -C link.cfg -o $@ -m $(patsubst %.bin, %.map, $@) $<

#$(BUILD)/%.da.s:$(BUILD)/%.bin
#	$(DA) $(DAOPT) --cpu 65C816 -S 0x1000 -o $@ $<


clean::
	-rm $(BUILD)/test.bin
	-rm $(BUILD)/test.lst
	-rm $(BUILD)/test.o	
	-rm $(BUILD)/test.s
	-rm $(BUILD)/test.map	