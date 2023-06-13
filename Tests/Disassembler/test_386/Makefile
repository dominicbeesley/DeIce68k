AS=nasm
ASFLAGS=-I $(INC)

BUILD=./build

X:=$(shell mkdir -p $(BUILD))

OBJ=test
OBJ_P=$(addprefix $(BUILD)/, $(OBJ))
OBJ_B=$(addsuffix .bin, $(OBJ_P))

all: $(OBJ_B)

$(BUILD)/%.bin: %.asm
		$(AS) $(ASFLAGS) -fbin -o $@ -l $(basename $@).lst $< >$(basename $@).map

clean:
	-rm $(OBJ_B) > /dev/null 2>&1
	-rm $(addsuffix .lst, $(OBJ_P)) > /dev/null 2>&1
	-rm $(addsuffix .map, $(OBJ_P)) > /dev/null 2>&1
	
