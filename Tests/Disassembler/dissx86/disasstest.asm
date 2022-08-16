	absolute 0
DATA1	resw	44
DATA2	resw	44



cpu 186



		section .text

		aaa
		aad
		aam
		aas

		mov cl,ch
		mov ch,cl
		mov ax,dx
		mov cx,bx

		mov [DATA1],DL
		mov DL,[DATA2]

		mov [DATA1],AX
		mov AX,[DATA2]

		mov AX,[BX+SI+10]
		mov [ES:BX+DI],AL

		mov AX,[BX+SI]
		mov AX,[BX+DI]
		mov AX,[BP+SI]
		mov AX,[BP+DI]
		mov AX,[SI]
		mov AX,[DI]
		mov AX,[BP]
		mov AX,[BX]

		mov AX,[ES:BX+SI]
		mov AX,[SS:BX+DI]
		mov AX,[DS:BP+SI]
		mov AX,[DS:BP+DI]
		mov AX,[SS:SI]
		mov AX,[ES:DI]
		mov AX,[DS:BP]
		mov AX,[SS:BX]

		; mem - imm

		mov byte [DATA1], 23H
		mov word [DATA2], 0xbeef

		mov word [ES:DI], 0xface

		; reg - imm

		mov BX,0x2255
		mov CX,12H
		mov di,100

		; acc - disp

		mov al,[DATA2]
		mov AX,[DATA1]
		mov [ES:DATA2],AX
		mov [DATA1],AH

		; segmem

		mov AX,ES
		mov SS,AX

		mov SS,[DATA1]
		mov [DATA2],ES

		mov [BX+SI],ES
		mov SS,[ds:BP+SI+3]

		mov CS,AX
		mov DS,AX
		mov ES,AX
		mov SS,AX


		;;;;;;;;;;;;;; ADC ;;;;;;;;;;;;;;;;

		; reg,reg
		adc	AX,CX
		adc	al,cl
		adc	DX,BX

		; mem,reg

		adc	[DATA1],al
		adc	al,[DATA1]

		adc	[DI+DATA2],BX

		adc	CX,[CS:BP+SI+6]

		; reg, imm

		adc	CX,0x23
		adc	CX,0x2323
		adc	DL,23
		adc	DL,0x22
		adc	word [BP+SI],99

		; acc, imm
		adc	AL,3
		adc	AX,9999
		adc	AH,3

		;;;;;;;;;;;;;; ADD ;;;;;;;;;;;;;;;;

		; reg,reg
		add	AX,CX
		add	al,cl
		add	DX,BX

		; mem,reg

		add	[DATA1],al
		add	al,[DATA1]

		add	[DI+DATA2],BX

		add	CX,[CS:BP+SI+6]

		; reg, imm

		add	CX,0x23
		add	CX,0x2323
		add	DL,23
		add	DL,0x22
		add	word [BP+SI],99

		; acc, imm
		add	AL,3
		add	AX,9999
		add	AH,3

		;;;;;;;;;;;;;; AND ;;;;;;;;;;;;;;;;

		; reg,reg
		and	AX,CX
		and	al,cl
		and	DX,BX

		; mem,reg

		and	[DATA1],al
		and	al,[DATA1]

		and	[DI+DATA2],BX

		and	CX,[CS:BP+SI+6]

		; reg, imm
c:
		and	CX,0x23
		and	CX,0x2323
		and	DL,23
		and	DL,0x22
		and	word [BP+SI],99

		; acc, imm
		and	AL,3
		and	AX,9999
		and	AH,3

		;;; BOUND

		bound 	AX,[DATA1]
		bound	BX,[DI+3]


		;;; CALL

		call 	near c
		call	0x444:0x555
		call	AX
		call	BX
		call	CX
		call	SI

		call	near [SI]
		call	near [ES:SI]
		call	far  [BP+DI]

		call	far  [CS:DATA1]
		call	near [ES:DATA2]

		;;; CBW

		cbw

		;;; CLC

		clc

		;;; CLD

		cld

		;;; CLI

		cli

		;;; CMC

		cmc


		;;;;;;;;;;;;;; CMP ;;;;;;;;;;;;;;;;

		; reg,reg
		cmp	AX,CX
		cmp	al,cl
		cmp	DX,BX

		; mem,reg

		cmp	[DATA1],al
		cmp	al,[DATA1]

		cmp	[DI+DATA2],BX

		cmp	CX,[CS:BP+SI+6]

		; reg, imm

		cmp	CX,0x23
		cmp	CX,0x2323
		cmp	DL,23
		cmp	DL,0x22
		cmp	word [BP+SI],99

		; acc, imm
		cmp	AL,3
		cmp	AX,9999
		cmp	AH,3


		;;;;;;;;;;;;;; CMPS ;;;;;;;;;;;;;;;;

		rep cmpsb
		rep cmpsw
		repnz cmpsb
		rep cmpsw

		;;;;;;;;;;;;;; CWD ;;;;;;;;;;;;;;;;

		cwd

		;;;;;;;;;;;;;; DAA ;;;;;;;;;;;;;;;;

		daa

		;;;;;;;;;;;;;; DAS ;;;;;;;;;;;;;;;;

		das

		;;;;;;;;;;;;;; DEC ;;;;;;;;;;;;;;;;

		; reg
		dec	AX
		dec	AL
		dec	DX
		dec	bl

		; mem

		dec	byte [DATA1]
		dec	byte [DI+DATA2]
		dec	byte [CS:BP+SI+6]

		dec	word [DATA1]
		dec	word [DI+DATA2]
		dec	word [CS:BP+SI+6]

		;;;;;;;;;;;;;; DIV ;;;;;;;;;;;;;;;;

		; reg
		div	AX
		div	AL
		div	DX
		div	bl

		; mem

		div	byte [DATA1]
		div	byte [DI+DATA2]
		div	byte [CS:BP+SI+6]

		div	word [DATA1]
		div	word [DI+DATA2]
		div	word [CS:BP+SI+6]

		;;;;;;;;;;;;;; HLT ;;;;;;;;;;;;;;;;

		hlt

		;;;;;;;;;;;;;; IDIV ;;;;;;;;;;;;;;;;

		; reg
		idiv	AX
		idiv	AL
		idiv	DX
		idiv	bl

		; mem

		idiv	byte [DATA1]
		idiv	byte [DI+DATA2]
		idiv	byte [CS:BP+SI+6]

		idiv	word [DATA1]
		idiv	word [DI+DATA2]
		idiv	word [CS:BP+SI+6]

		;;;;;;;;;;;;;; IMUL ;;;;;;;;;;;;;;;;

		; reg
		imul	AX
		imul	AL
		imul	DX
		imul	bl

		; mem

		imul	byte [DATA1]
		imul	byte [DI+DATA2]
		imul	byte [CS:BP+SI+6]

		imul	word [DATA1]
		imul	word [DI+DATA2]
		imul	word [CS:BP+SI+6]


		;;;;;;;;;;;;;; IN ;;;;;;;;;;;;;;;;

		in	AL, 1
		in	AL, 0x34
		in	AX, 1
		in	AX, 0x34

		in	AL,DX
		in	AX,DX

		;;;;;;;;;;;;;; inc ;;;;;;;;;;;;;;;;

		; reg
		inc	AX
		inc	AL
		inc	DX
		inc	bl

		; mem

		inc	byte [DATA1]
		inc	byte [DI+DATA2]
		inc	byte [CS:BP+SI+6]

		inc	word [DATA1]
		inc	word [DI+DATA2]
		inc	word [CS:BP+SI+6]



		;;;;;;;;;;;;;; out ;;;;;;;;;;;;;;;;

		out	1, AL
		out	0x34, AL
		out	1, AX
		out	0x34, AX

		out	DX, AL
		out	DX, AX



		rep mov  AL,3
		repe mov  AL,3
		repnz mov  AL,3
		repne mov  AL,3
