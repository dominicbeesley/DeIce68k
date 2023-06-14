[map all]

	cpu 386

	;  moves of various widths
	mov	edx,[xxx]

	mov	CL,CH
	mov	CX,DX
	mov	EAX,ESP
	mov	EDX,EBP

	mov	[here],dl
	mov	[here],cx
	mov	[here],ebx
	mov	[ecx],bl
	mov	[DI],DH
	mov	[ecx],ebx

	; scaled index

	mov	AL,[BP+SI]
	mov	AL,[BP+SI+2]

	mov	EBX,[EAX+2*ESI]
	mov	ECX,[EDX+4*EDI+0x23]
	mov	ESI,[EDX+8*EDI+0x230234]

	mov	AL,3
	mov	AH,4
	mov	AX,0x1234
	mov	EAX,0xDEADBEEF

	mov	AL,[here]
	mov	AH,[here]
	mov	AX,[here]
	mov	EAX,[here]

	mov	[here],AL
	mov	[here],AH
	mov	[here],AX
	mov	[here],EAX

	mov	BYTE [here], 0x12
	mov	WORD [here], 0x1234
	mov	DWORD [here], 0x1234

	mov	CX,yyy
	mov	ECX,xxx


	mov	ECX,[xxx+2*ESI]
	mov	ECX,[xxx+4*EDI]
	mov	ECX,[xxx+8*EAX]

	mov	BP,[xxx+2*ESI]
	mov	BP,[xxx+4*EDI]
	mov	BP,[xxx+8*EAX]

	mov	BP,CS:[xxx+2*ESI]
	mov	BP,CS:[xxx+4*EDI]
	mov	BP,CS:[xxx+8*EAX]

	mov	BL,[xxx+2*ESI]
	mov	BL,[xxx+4*EDI]
	mov	BL,[xxx+8*EAX]

	popa
	popad

	pusha
	pushad


	section .data

here:	dw	0xB00B
	dw	0xF00F
there: 	dw	0x9999


	section .bss

	resb	100
yyy:
	resb	100000

xxx:

