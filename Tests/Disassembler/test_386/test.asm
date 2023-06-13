[map all]

	cpu 386

	;  moves of various widths

	mov	CL,CH
	mov	CX,DX
	mov	EAX,ESP

	mov	[here],dl
	mov	[here],cx
	mov	[here],ebx
	mov	[ecx],bl
	mov	[DI],DH
	mov	[ecx],ebx

	; scaled index

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


here:	dw	0xB00B
	dw	0xF00F