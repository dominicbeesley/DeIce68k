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

l1:		inc	byte [DATA1]
l2:		inc	byte [DI+DATA2]
l3:		inc	byte [CS:BP+SI+6]

l4:		inc	word [DATA1]
l5:		inc	word [DI+DATA2]
l6:		inc	word [CS:BP+SI+6]

		;;;;;;;;;;;;;; INS ;;;;;;;;;;;;;;;;

l7:		rep insb
l8:		rep insw
l9:		repnz insb
l10:		rep insw

		;;;;;;;;;;;;;; int ;;;;;;;;;;;;;;;;

		int 1
		int 2
		int3
		int 0xFF

		;;;;;;;;;;;;;; iret ;;;;;;;;;;;;;;;;

		into


		;;;;;;;;;;;;;; iret ;;;;;;;;;;;;;;;;

		iret

		;;;;;;;;;;;;;; out ;;;;;;;;;;;;;;;;

		out	1, AL
		out	0x34, AL
		out	1, AX
		out	0x34, AX

		out	DX, AL
		out	DX, AX

		;;;;;;;;;;;;;; Jcc ;;;;;;;;;;;;;;;;;

l15:		jo	l15
l16		jno	l16
		jb	l1
		jae	l2
		je	l3
		jne	l4
		jbe	l5
		ja	l6
		js	l7
		jns	l8
		jp	l9
		jnp	l10
		jl	l11
		jge	l12
		jle	l13
		jg	l14

l11:		rep mov  AL,3
l12:		repe mov  AL,3
l13:		repnz mov  AL,3
l14:		repne mov  AL,3


		;;;;;;;;;;;;;; JCXZ ;;;;;;;;;;;;;;;;;

		jcxz	l15

		;;;;;;;;;;;;;; jumps ;;;;;;;;;;;;;;;;;		

		jmp	l15

		jmp	DATA1

		jmp	1234h:5678h

		jmp	AX
		jmp	BX
		jmp	CX
		jmp	SI

		jmp	near [SI]
		jmp	near [ES:SI]
		jmp	far  [BP+DI]

		jmp	far  [CS:DATA1]
		jmp	near [ES:DATA2]

		;;;;;;;;;;;;;; lahf ;;;;;;;;;;;;;;;;;		

		lahf

		;;;;;;;;;;;;;; lds ;;;;;;;;;;;;;;;;;		

		lds	DI,[DATA1]
		lds	SP,[DATA1]
		lds	BP,[SI]
		lds	BP,[SI+BP]
		lds	BP,[SI+BP+3]
		; TODO: more examples?

		;;;;;;;;;;;;;; lea ;;;;;;;;;;;;;;;;;		

		lea	DI,[DATA1]
		lea	SP,[DATA1]
		lea	BP,[SI]
		lea	BP,[SI+BP]
		lea	BP,[SI+BP+3]
		; TODO: more examples?

		;;;;;;;;;;;;;; les ;;;;;;;;;;;;;;;;;		

		les	DI,[DATA1]
		les	SP,[DATA1]
		les	BP,[SI]
		les	BP,[SI+BP]
		les	BP,[SI+BP+3]
		; TODO: more examples?

		;;;;;;;;;;;;;; LODS ;;;;;;;;;;;;;;;;

		rep lodsb
		rep lodsw
		repnz lodsb
		rep lodsw

		cs rep lodsb
		es rep lodsw
		ds repnz lodsb
		ss rep lodsw
x1:

		;;;;;;;;;;;;;; LOOP ;;;;;;;;;;;;;;;;

		loop	x1
		loope	x2
		loopne	x2

x2:
		;;;;;;;;;;;;;; MOVS ;;;;;;;;;;;;;;;;
		rep movsb  
		repe movsb 
		repnz movsb 
		repne movsb 

		rep cs movsb  
		repe ds movsb 
		repnz es movsb 
		repne ss movsb

		rep movsw 
		repe movsw
		repnz movsw
		repne movsw

		rep cs movsw  
		repe ds movsw 
		repnz es movsw 
		repne ss movsw

		;;;;;;;;;;;;;; mul ;;;;;;;;;;;;;;;;

		; reg
		mul	AX
		mul	AL
		mul	DX
		mul	bl

		; mem

		mul	byte [DATA1]
		mul	byte [DI+DATA2]
		mul	byte [CS:BP+SI+6]

		mul	word [DATA1]
		mul	word [DI+DATA2]
		mul	word [CS:BP+SI+6]


		;;;;;;;;;;;;;; neg ;;;;;;;;;;;;;;;;

		; reg
		neg	AX
		neg	AL
		neg	DX
		neg	bl

		; mem

		neg	byte [DATA1]
		neg	byte [DI+DATA2]
		neg	byte [CS:BP+SI+6]

		neg	word [DATA1]
		neg	word [DI+DATA2]
		neg	word [CS:BP+SI+6]

		;;;;;;;;;;;;;; nop ;;;;;;;;;;;;;;;

		nop

		;;;;;;;;;;;;;; not ;;;;;;;;;;;;;;;;

		; reg
		not	AX
		not	AL
		not	DX
		not	bl

		; mem

		not	byte [DATA1]
		not	byte [DI+DATA2]
		not	byte [CS:BP+SI+6]

		not	word [DATA1]
		not	word [DI+DATA2]
		not	word [CS:BP+SI+6]

		;;;;;;;;;;;;;; or ;;;;;;;;;;;;;;;;

		; reg,reg
		or	AX,CX
		or	al,cl
		or	DX,BX

		; mem,reg

		or	[DATA1],al
		or	al,[DATA1]

		or	[DI+DATA2],BX

		or	CX,[CS:BP+SI+6]

		; reg, imm

		or	CX,0x23
		or	CX,0x2323
		or	DL,23
		or	DL,0x22
		or	word [BP+SI],99

		; acc, imm
		or	AL,3
		or	AX,9999
		or	AH,3

		;;;;;;;;;;;;;; out ;;;;;;;;;;;;;;;;

		out	1,AL
		out	0x34,AL
		out	1,AX
		out	0x34,AX

		out	DX,AL
		out	DX,AX

		;;;;;;;;;;;;;; outs ;;;;;;;;;;;;;;;;

		rep outsb
		rep outsw
		repnz outsb
		rep outsw

		;;;;;;;;;;;;;; pop ;;;;;;;;;;;;;;;;;

		pop	AX
		pop	BX
		pop	CX
		pop	DX
		pop	SP
		pop	BP
		pop	SI
		pop	DI

		pop	CS
		pop	DS
		pop	ES
		pop	SS

		pop	word [DATA1]
		pop	word [DI+DATA2]
		pop	word [CS:BP+SI+6]

		popf

		;;;;;;;;;;;;;; push ;;;;;;;;;;;;;;;;;

		push	AX
		push	BX
		push	CX
		push	DX
		push	SP
		push	BP
		push	SI
		push	DI

		push	CS
		push	DS
		push	ES
		push	SS

		push	word [DATA1]
		push	word [DI+DATA2]
		push	word [CS:BP+SI+6]

		push	0x1234
		push	0x99
		push	'.'

		pushf	

		;;;;;;;;;;;;;; RCL/RCR/ROL/ROR ;;;;;;;;;;;;;;;;;;

		rcl	AL,1
		rcl	AH,1
		rcl	AX,1
		rcr	BL,1
		rcr	BH,1
		rcr	BX,1
		rol	CL,1
		rol	CH,1
		rol	CX,1
		ror	DL,1
		ror	DH,1
		ror	DX,1

		rcl	SI,1
		rcr	DI,1
		rol	BP,1
		ror	SP,1

		rol	byte [DATA1],1
		rol	byte [DI+DATA2],1
		rol	byte [CS:BP+SI+6],1

		ror	word [DATA1],1
		ror	word [DI+DATA2],1
		ror	word [CS:BP+SI+6],1

		rcl	AL,CL
		rcl	AH,CL
		rcl	AX,CL
		rcr	BL,CL
		rcr	BH,CL
		rcr	BX,CL
		rol	CL,CL
		rol	CH,CL
		rol	CX,CL
		ror	DL,CL
		ror	DH,CL
		ror	DX,CL

		rcl	SI,CL
		rcr	DI,CL
		rol	BP,CL
		ror	SP,CL

		rol	byte [DATA1],CL
		rol	byte [DI+DATA2],CL
		rol	byte [CS:BP+SI+6],CL

		ror	word [DATA1],CL
		ror	word [DI+DATA2],CL
		ror	word [CS:BP+SI+6],CL


		rcl	AL,2
		rcl	AH,3
		rcl	AX,4
		rcr	BL,5
		rcr	BH,6
		rcr	BX,7
		rol	CL,8
		rol	CH,9
		rol	CX,10
		ror	DL,11
		ror	DH,12
		ror	DX,13

		rcl	SI,2
		rcr	DI,3
		rol	BP,4
		ror	SP,5

		rol	byte [DATA1],6
		rol	byte [DI+DATA2],7
		rol	byte [CS:BP+SI+6],8

		ror	word [DATA1],9
		ror	word [DI+DATA2],10
		ror	word [CS:BP+SI+6],11
