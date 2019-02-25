.include "dynasm_include.s"

	.text
	.globl	c2asm_func2
	.type	c2asm_func2, @function
c2asm_func2:
	.cfi_startproc
	movq	%rcx, %r8
	imull	$42698888, C2ASM_a(%rcx), %edx
	imull	$81920000, C2ASM_b(%rcx), %eax
	addl	%edx, %eax
aa:
	mov %eax, %r12d
	shr %r12d
	and $0xFF, %r12d
	mov 123(%eax, %r11d, 4), %r11d
	mov 0(%rip), %r11
	movl	%eax, C2ASM_c(%rcx)
	SET_D_MEM	C2ASM_f(%rcx)
	cmpl	%eax, %eax
	pushfq
	movl	(%rsp), %r9d
	SET_D	%r9d
	popfq
	ret
	.cfi_endproc



