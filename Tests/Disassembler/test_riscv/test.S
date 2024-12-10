#include "custom_ops.S"


start:

//disassembler test

		.option norvc

		// some branch tests
		bgt	t0,t2, start
		bgt	t2,t0, start
		beqz	t2, start
		bnez	t2, start
		bgtz	t2, start
		bltz	t2, start
		bgez	t2, start
		blez	t2, start
		beq	zero, t2, start
		bne	zero, t2, start

		.option rvc

		// some branch tests
		bgt	t0,t2, start
		bgt	t2,t0, start
		beqz	t2, start
		bnez	t2, start
		bgtz	t2, start
		bltz	t2, start
		bgez	t2, start
		blez	t2, start
		beq	zero, t2, start
		bne	zero, t2, start

		// compressed stuff

		//CR
		c.jr	x5
		c.jalr  x2
		c.mv	a4, a5
		c.mv	x0, x4
		c.add	a6, a7
		ebreak
		ecall

		//CL
		c.lw	a4,0(a5)
		c.lw	a4,0x34(a5)
		c.lw	a4,0x50(a5)
		c.lwsp	ra,12(sp)

		//CS
		c.sw	a4,0(a5)
		c.sw	a4,0x34(a5)
		c.sw	a4,0x50(a5)

		//CA
		c.sub	a3, a0
		c.xor	a3, a0
		c.or	a3, a0
		c.and	a3, a0

		//CI

		c.li	a3, 23
		c.li	a2, -23
		c.lui   a4, 0x2
		c.lui   a4, 0xfffe0

		c.addi	a3, 31
		c.addi	a2, -13
		c.addi16sp	sp, 0x100
		c.addi16sp	sp, -0x70

		c.nop

		//CIW
		c.addi4spn a0, sp, 0x24
		c.addi4spn a3, sp, 0x124
		
		//CB
		c.beqz	a3, start
		c.bnez	s0, start
		c.beqz	a3, test_label_forward
		c.bnez	s0, test_label_forward2

		// CB(2)
		c.srli	a5, 10
		c.srai  a3, 21
		andi  a4, a4, 0x3

		//CJ
		c.jal	start
		c.j		test_label_forward
		c.j		start

		li a7,0xAC0001
		ecall
		li a7,0x1
		ecall


////////// LONG

		.option norvc

		// CR

		add		a3, t0, t2
		add		a5, sp, x0
		sub		a3, a4, a5
		xor		a3, a4, a5
		and		a3, a4, a5

		ebreak
		ecall


		bne	a4,a5,start

		jalr a1,34(x0)
		jalr a1,999(x5)
		jr 4(x9)

		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
test_label_forward2:
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0
		addi x0,x0,0

		li a7,0xAC0001
		ecall
		li a7,0x1
		ecall

	
test_label_forward: