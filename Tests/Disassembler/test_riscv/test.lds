/*
This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.
*/

MEMORY {
	/* the memory in the testbench is 128k in size;
	 * set LENGTH=96k and leave at least 32k for stack */
	ROM : ORIGIN = 0xFFFFC000, LENGTH = 0x00004000
	RAM : ORIGIN = 0x00010000, LENGTH = 0x00100000
}

SECTIONS {
	.code : {
		. = 0x0000;
		start*(.text);
		*(.text);
		*(.text*);
		*(.rodata);
		*(.rodata*);
		*(.srodata);
		. = ALIGN(4);
		_cend = .;
	} > ROM

	.data : {
		. = ALIGN(4);
		_data_start = .;
		*(.data);	
		*(.data*);	
		*(.sdata);	
		*(.sdata*);	
		. = ALIGN(4);
	} >RAM AT> ROM
	_data_size = SIZEOF(.data);
    _data_load_start = LOADADDR(.data);

	.bss (NOLOAD): {
		. = ALIGN(4);
		_bss_start = .;
		*(.bss);
		*(.memory);
		*(.sbss);
		. = ALIGN(4);
	} > RAM
	_bss_size = SIZEOF(.bss);

	.hwvects : {
		. = (0x3FF8 - SIZEOF(.code) - SIZEOF(.data)); 
		*(reset);
		*(irq);
		. = ALIGN(4);
	} > ROM

}
